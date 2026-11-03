var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();
var app = builder.Build();

app.MapHealthChecks("/manage/health");

var flights = new[]
{
    new { Id = 1, From = "Moscow", To = "Berlin", Number = "SU100" },
    new { Id = 2, From = "Berlin", To = "Paris",  Number = "SU200" },
    new { Id = 3, From = "Paris",  To = "London", Number = "SU300" }
};

app.MapGet("/api/flights", () => Results.Ok(flights));
app.MapGet("/api/flights/{id:int}", (int id) =>
{
    var flight = flights.FirstOrDefault(f => f.Id == id);
    return flight is null ? Results.NotFound() : Results.Ok(flight);
});

app.Run();