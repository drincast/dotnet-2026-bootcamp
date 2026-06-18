using TaskManager.Api;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

//registro de configuraciones
builder.Services.Configure<TaskManagerOptions>(
    builder.Configuration.GetSection(TaskManagerOptions.SectionName));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

//uso de servicios basicos
//exepciones no controladas
app.UseMiddleware<ExceptionHandlingMiddleware>();
//registro de loggin - tiempos
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseHttpsRedirection();

//uso de servicios de logica
// El nombre "GetWeatherForecast" ya está registrado internamente
app.MapWeatherForecastEndpoint();

//para llamar al api de taskmanager
app.MapTaskEndpoints();

app.Run();



