using Microsoft.Extensions.Options;

namespace TaskManager.Api
{
    public static class TaskEndpoints
    {
        public static void MapTaskEndpoints(this WebApplication app)
        {
            app.MapGet("/tasks", GetAll);
            app.MapGet("/tasks/{id}", GetById);
            app.MapPost("/tasks", Create);
            app.MapDelete("/tasks/{id}", Delete);
        }


        //GET /tasks - retorna lista harcodeada por ahora
        private static IResult GetAll(IOptions<TaskManagerOptions> options
                , int page = 1
                , int ? pageSize = null)
        {

            var tasks = new[]
            {
                new {Id = 1, Title = "Aprender .Net 10", Done = false},
                new {Id = 2, Title = "Construir taskManager API", Done = false}

                ,new {Id = 3, Title = "3 Aprender .Net 10", Done = false}
                ,new {Id = 4, Title = "4 Construir taskManager API", Done = false}
                ,new {Id = 5, Title = "5 Aprender .Net 10", Done = false}
                ,new {Id = 6, Title = "6 Construir taskManager API", Done = false}
                ,new {Id = 7, Title = "7 Aprender .Net 10", Done = false}
                ,new {Id = 8, Title = "8 Construir taskManager API", Done = false}
                ,new {Id = 9, Title = "9 Aprender .Net 10", Done = false}
                ,new {Id = 10, Title = "10 Construir taskManager API", Done = false}
                ,new {Id = 11, Title = "11 Construir taskManager API", Done = false}
            };

            //paginación
            //este es para option monitors
            //var size = pageSize ?? options.CurrentValue.DefaultPageSize;
            var size = pageSize ?? options.Value.DefaultPageSize;
            var items = tasks
                    .Skip((page - 1) * size) //salta las paginas anteriores
                    .Take(size);

            //throw new Exception("Prueba de excepción no controlada");

            return Results.Ok(new
            {
                Page = page,
                PageSize = size,
                TotalItems = tasks.Length,
                Items = items
            });

            //return Results.Ok(tasks.Take(options.Value.DefaultPageSize));
        }

        //GET /tasks - retorna una tarea por id
        private static IResult GetById(int id)
        {
            return id > 0
                ? Results.Ok(new { Id = id, Title = $"Tarea {id}", Done = false })
                : Results.NotFound();
        }

        //POST /tasks - crea una tarea
        private static IResult Create(CreateTaskRequest request)
        //app.MapPost("/tasks", (CreateTaskRequest request) =>
        {
            // Por ahora solo echamos el request de vuelta
            return Results.Created($"/tasks/1"
                , new { Id = 1, request.Title, Done = false });
        }

        //DELETE /tasks - elimina una tarea
        private static IResult Delete(int id)
        {
            // Por ahora siempre responde NoContent — la lógica real llega en Semana 2
            return Results.NoContent();
        }
    }
}
