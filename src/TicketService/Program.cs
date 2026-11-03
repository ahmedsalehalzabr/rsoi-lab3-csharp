var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();
var app = builder.Build();

app.MapHealthChecks("/manage/health");

var tickets = new List<object>
{
    new { Id = 1, FlightId = 1, Username = "user1" },
    new { Id = 2, FlightId = 2, Username = "user2" }
};
int nextId = 3;

app.MapGet("/api/tickets", () => Results.Ok(tickets));

app.MapPost("/api/tickets", (TicketRequest req) =>
{
    if (req.FlightId <= 0 || string.IsNullOrEmpty(req.Username))
        return Results.BadRequest();
    var ticket = new { Id = nextId++, FlightId = req.FlightId, Username = req.Username };
    tickets.Add(ticket);
    return Results.Ok(ticket);
});

app.Run();

record TicketRequest(int FlightId, string Username);