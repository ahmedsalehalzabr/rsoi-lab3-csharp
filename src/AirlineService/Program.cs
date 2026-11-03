var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();
var app = builder.Build();

app.MapHealthChecks("/manage/health");

var airlines = new[]
{
    new { Id = 1, Name = "Aeroflot" },
    new { Id = 2, Name = "Lufthansa" },
    new { Id = 3, Name = "Air France" }
};

app.MapGet("/api/airlines", () => Results.Ok(airlines));

app.Run();