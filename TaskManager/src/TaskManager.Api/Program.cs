using TaskManager.Application;
using TaskManager.Application.Common;
using TaskManager.Application.Common.Behaviors;
using TaskManager.Infrastructure;
using TaskManager.Infrastructure.Persistence;
using TaskManager.Api;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureService(builder.Configuration);

//registro de configuraciones
builder.Services.Configure<TaskManagerOptions>(
    builder.Configuration.GetSection(TaskManagerOptions.SectionName)
);

// SQLite para desarrollo local, sin instalar SQL Server
// builder.Services.AddDbContext<TaskManagerDbContext>(options =>
//     options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"))
//            .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
//            .EnableDetailedErrors(builder.Environment.IsDevelopment())
// );

//registro de mediatr -> en el momento la última con licencia Apache, luego se creara una rama para actualizarla a la nueva con licencia dual
// builder.Services.AddMediatR(cfg =>
// {
//     cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);

//     // Pipeline Behaviors — se ejecutan en el orden de registro
//     // LoggingBehavior va PRIMERO para que envuelva a todos los demás
//     cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
// });

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

//para llamar al api de taskmanager
app.MapTaskEndpoints();

app.Run();



