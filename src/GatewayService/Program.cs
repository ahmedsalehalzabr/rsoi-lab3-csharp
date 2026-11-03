using System.Threading.Channels;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Fallback;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<RetryQueue>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RetryQueue>());

builder.Services.AddHttpClient("FlightClient", c => c.BaseAddress = new Uri("http://flight-service:8080"))
.AddResilienceHandler("flight", b => {
    b.AddFallback(new FallbackStrategyOptions<HttpResponseMessage> {
        ShouldHandle = args => ValueTask.FromResult(args.Outcome.Exception is not null),
        FallbackAction = args => { var r = new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json") }; return ValueTask.FromResult(Outcome.FromResult(r)); }
    });
    b.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions { FailureRatio = 0.5, SamplingDuration = TimeSpan.FromSeconds(10), MinimumThroughput = 5, BreakDuration = TimeSpan.FromSeconds(15) });
});

builder.Services.AddHttpClient("TicketClient", c => c.BaseAddress = new Uri("http://ticket-service:8080"))
.AddResilienceHandler("ticket", b => {
    b.AddFallback(new FallbackStrategyOptions<HttpResponseMessage> {
        ShouldHandle = args => ValueTask.FromResult(args.Outcome.Exception is not null),
        FallbackAction = args => { var r = new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json") }; return ValueTask.FromResult(Outcome.FromResult(r)); }
    });
    b.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions { FailureRatio = 0.5, SamplingDuration = TimeSpan.FromSeconds(10), MinimumThroughput = 5, BreakDuration = TimeSpan.FromSeconds(15) });
});

builder.Services.AddHttpClient("AirlineClient", c => c.BaseAddress = new Uri("http://airline-service:8080"))
.AddResilienceHandler("airline", b => {
    b.AddFallback(new FallbackStrategyOptions<HttpResponseMessage> {
        ShouldHandle = args => ValueTask.FromResult(args.Outcome.Exception is not null),
        FallbackAction = args => { var r = new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json") }; return ValueTask.FromResult(Outcome.FromResult(r)); }
    });
    b.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions { FailureRatio = 0.5, SamplingDuration = TimeSpan.FromSeconds(10), MinimumThroughput = 5, BreakDuration = TimeSpan.FromSeconds(15) });
});

// RetryClient: بدون Fallback — يُستخدم فقط في RetryQueue لإعادة المحاولات الحقيقية
builder.Services.AddHttpClient("RetryClient", c => c.BaseAddress = new Uri("http://ticket-service:8080"));

var app = builder.Build();
app.MapHealthChecks("/manage/health");

app.MapGet("/api/flights", async (IHttpClientFactory f) => { var c = f.CreateClient("FlightClient"); var r = await c.GetAsync("/api/flights"); return Results.Content(await r.Content.ReadAsStringAsync(), "application/json"); });
app.MapGet("/api/tickets", async (IHttpClientFactory f) => { var c = f.CreateClient("TicketClient"); var r = await c.GetAsync("/api/tickets"); return Results.Content(await r.Content.ReadAsStringAsync(), "application/json"); });
app.MapGet("/api/airlines", async (IHttpClientFactory f) => { var c = f.CreateClient("AirlineClient"); var r = await c.GetAsync("/api/airlines"); return Results.Content(await r.Content.ReadAsStringAsync(), "application/json"); });

app.MapPost("/api/book", async (BookingRequest req, IHttpClientFactory f, RetryQueue q) => {
    var tc = f.CreateClient("TicketClient"); var fc = f.CreateClient("FlightClient");
    var rc = f.CreateClient("RetryClient");
    try {
        var fr = await fc.GetAsync($"/api/flights/{req.FlightId}");
        if (!fr.IsSuccessStatusCode) return Results.BadRequest(new { error = "Flight not found" });
        var tr = await tc.PostAsJsonAsync("/api/tickets", req);
        var trBody = await tr.Content.ReadAsStringAsync();
        if (!tr.IsSuccessStatusCode || string.IsNullOrEmpty(trBody) || trBody == "[]") throw new HttpRequestException("Ticket failed");
        return Results.Ok(new { status = "success", ticket = trBody });
    } catch {
        await q.EnqueueAsync(async () => { var r = await rc.PostAsJsonAsync("/api/tickets", req); r.EnsureSuccessStatusCode(); });
        return Results.Ok(new { status = "accepted", message = "Queued for retry" });
    }
});

app.Run();
record BookingRequest(int FlightId, string Username);
public class RetryQueue : BackgroundService {
    private readonly Channel<Func<Task>> _q = Channel.CreateUnbounded<Func<Task>>();
    private readonly ILogger<RetryQueue> _l;
    public RetryQueue(ILogger<RetryQueue> l) => _l = l;
    public ValueTask EnqueueAsync(Func<Task> w) => _q.Writer.WriteAsync(w);
    protected override async Task ExecuteAsync(CancellationToken ct) {
        await foreach (var w in _q.Reader.ReadAllAsync(ct)) {
            try { await w(); } catch (Exception ex) { _l.LogError(ex, "Retry failed"); await _q.Writer.WriteAsync(w); await Task.Delay(5000, ct); }
        }
    }
}