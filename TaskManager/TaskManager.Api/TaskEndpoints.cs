using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace TaskManager.Api
{
    public static class TaskEndpoints
    {
        public static void MapTaskEndpoints(this WebApplication app)
        {
            app.MapGet("/tasks", GetAll);
            app.MapGet("/tasks/{id:int}", GetById);
            app.MapPost("/tasks", Create);
            app.MapPut("/tasks/{id:int}", Update);
            app.MapDelete("/tasks/{id:int}", Delete);
        }


        //GET /tasks - retorna lista harcodeada por ahora
        private static async Task<IResult> GetAll(IOptions<TaskManagerOptions> options
                , TaskManagerDbContext db
                , CancellationToken ct
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
            var totalItems = await db.TaskItems.CountAsync(ct);

            // var items = tasks
            //         .Skip((page - 1) * size) //salta las paginas anteriores
            //         .Take(size);

            var items = await db.TaskItems
                    .AsNoTracking()
                    .OrderBy(t => t.Id)
                    .Skip((page - 1) * size) //salta las paginas anteriores
                    .Take(size)
                    .Select( t => new
                    {
                        t.Id, t.Title, Done = t.Status == TaskStatus.Done
                    })
                    .ToListAsync(ct);

            //throw new Exception("Prueba de excepción no controlada");

            return Results.Ok(new
            {
                Page = page,
                PageSize = size,
                TotalItems = tasks.Length,
                TotalItems2 = totalItems,
                Items = items
            });

            //return Results.Ok(tasks.Take(options.Value.DefaultPageSize));
        }

        //GET /tasks - retorna una tarea por id
        private static async Task<IResult> GetById(TaskManagerDbContext db
            , CancellationToken ct
            , int id)
        {
            var task = await db.TaskItems
                .AsNoTracking()
                .Where(t => t.Id == id)
                .Select( t => new
                {
                    t.Id, t.Title, Done = t.Status == TaskStatus.Done
                })
                .FirstOrDefaultAsync(ct);

            // return id > 0
            //     ? Results.Ok(new { Id = id, Title = $"Tarea {id}", Done = false })
            //     : Results.NotFound();

            return task is null 
                ? Results.NotFound() 
                : Results.Ok(task);
        }

        //POST /tasks - crea una tarea
        private static async Task<IResult> Create(CreateTaskRequest request
            , TaskManagerDbContext db
            , CancellationToken ct)
        //app.MapPost("/tasks", (CreateTaskRequest request) =>
        {
            var task = new TaskItem
            {
                Title = request.Title
                , Status = TaskStatus.Todo
                , AssignedToId = request.AssignedToId
                , ProjectId = request.ProjectId
            };

            db.TaskItems.Add(task);
            await db.SaveChangesAsync(ct);

            // Por ahora solo echamos el request de vuelta
            return Results.Created($"/tasks/{task.Id}"
                , new {task.Id, task.Title, Done = false });
        }

        //PUT actualizar una tarea
        private static async Task<IResult> Update(UpdateTaskRequest request
            , TaskManagerDbContext db
            , CancellationToken ct
            , int id)
        {
            var taskItem = await db.TaskItems.FindAsync([id], ct);

            if(taskItem is null)
                return Results.NotFound();

            taskItem.Title = request.Title;
            taskItem.Status = Enum.Parse<TaskStatus>(request.Status);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        }

        //DELETE /tasks - elimina una tarea
        private static async Task<IResult> Delete(TaskManagerDbContext db
            , CancellationToken ct
            ,int id)
        {
            var taskItem = await db.TaskItems.FindAsync([id], ct);

            if(taskItem is null)
                return Results.NotFound();

            db.TaskItems.Remove(taskItem);
            await db.SaveChangesAsync(ct);            
            
            // Por ahora siempre responde NoContent — la lógica real llega en Semana 2
            return Results.NoContent();
        }
    }
}
