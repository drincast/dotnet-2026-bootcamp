using Microsoft.EntityFrameworkCore;
using TaskManager.Api;



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

//registro de configuraciones
builder.Services.Configure<TaskManagerOptions>(
    builder.Configuration.GetSection(TaskManagerOptions.SectionName)
);

// SQLite para desarrollo local, sin instalar SQL Server
builder.Services.AddDbContext<TaskManagerDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"))
           .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
           .EnableDetailedErrors(builder.Environment.IsDevelopment())
);

//registro de mediatr -> en el momento la última con licencia Apache, luego se creara una rama para actualizarla a la nueva con licencia dual
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly)
);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    //Esta seccion aun no la vemos pero la dejo comentada cuando se toque el tema
    //using var scope = app.Services.CreateScope();
    //var db = scope.ServiceProvider.GetRequiredService<TaskManagerDbContext>();
    //db.Database.Migrate();

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



