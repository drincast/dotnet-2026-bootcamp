namespace TaskManager.Api
{
    public static class WeatherForecastEndpoint
    {
        public static void MapWeatherForecastEndpoint(this WebApplication app)
        {
            app.MapGet("/weatherforecast", GetAll)
                .WithName("GetWeatherForecast");
        }

        //GET /tasks - retorna lista harcodeada por ahora
        private static IResult GetAll()
        {
            var summaries = new[]
            {
                "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
            };

            var forecast = Enumerable.Range(1, 5).Select(index =>
                    new WeatherForecast
                    (
                        DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                        Random.Shared.Next(-20, 55),
                        summaries[Random.Shared.Next(summaries.Length)]
                    ))
                    .ToArray();
            return Results.Ok(forecast);
        }
    }
}

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}


