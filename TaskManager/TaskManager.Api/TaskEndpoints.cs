using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.NetworkInformation;

namespace TaskManager.Api
{
    public static class TaskEndpoints
    {
        public static void MapTaskEndpoints(this WebApplication app)
        {
            app.MapGet("/tasks", GetAll);
            app.MapGet("/tasks/{id:int}", GetById);
            app.MapGet("/tasks/detailed", GetTaskDetailed);
            app.MapPost("/tasks", Create);
            app.MapPut("/tasks/{id:int}", Update);
            app.MapDelete("/tasks/{id:int}", Delete);
        }


        //GET /tasks - retorna lista de la base de datos
        private static async Task<IResult> GetAll(IOptions<TaskManagerOptions> options
                , TaskManagerDbContext db
                , CancellationToken ct
                , int page = 1
                , int ? pageSize = null)
        {

            //paginación
            var size = pageSize ?? options.Value.DefaultPageSize;
            var totalItems = await db.TaskItems.CountAsync(ct);

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

            return Results.Ok(new
            {
                Page = page,
                PageSize = size,
                TotalItems = totalItems,
                Items = items
            });
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

            return task is null 
                ? Results.NotFound() 
                : Results.Ok(task);
        }

        //GEt /tasks/detailed - retorna el detalle de las tareas
        private static async Task<IResult> GetTaskDetailed(IOptions<TaskManagerOptions> options
            , TaskManagerDbContext db
            , CancellationToken ct
            , int page = 1
            , int? pageSize = null
        )
        {
            //paginación
            var size = pageSize ?? options.Value.DefaultPageSize;
            var totalItems = await db.TaskItems.CountAsync(ct);

            var items = await db.TaskItems
                   .AsNoTracking()
                   .OrderBy(t => t.Id)
                   .Skip((page - 1) * size) //salta las paginas anteriores
                   .Take(size)
                   .Select(t => new TaskDetailDto
                   (
                       t.Id,
                       t.Title,
                       t.Description,
                       t.Status,
                       t.CreatedAt,
                       t.ProjectId,
                       t.Project!.Name, // Project es obligatorio (FK no-null) → ok dejar el !
                       t.AssignedToId,
                       t.AssignedTo != null ? t.AssignedTo.Name : null  // AssignedTo es opcional → maneja el null de verdad
                   ))
                   .ToListAsync(ct);

            return Results.Ok(new
            {
                Page = page,
                PageSize = size,
                TotalItems = totalItems,
                Items = items
            });
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

            TaskStatus status;

            //validacion del estado
            if(!Enum.TryParse<TaskStatus>(request.Status, ignoreCase: true, out status))
                return Results.BadRequest($"Estado inválido: {request.Status}");

            taskItem.Title = request.Title;
            taskItem.Status = status;
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

        ///Sección para algunas funciones de utileria en el momento
        ///o código que se tenia antes de la implementación adecuada        
        #region utileria
        ///este método es como se inicio el proyecto una lista hadcodea de tareas, se borrara en la semana 3 o 4
        private static TaskItem[] TaskItemsListDummy()
        {
            var tasks = new[]
            {
                new TaskItem { Id = 1,  Title = "Aprender .Net 10",            Status = TaskStatus.Done },
                new TaskItem { Id = 2,  Title = "Construir taskManager API",   Status = TaskStatus.Done },
                new TaskItem { Id = 3,  Title = "3 Aprender .Net 10",          Status = TaskStatus.Done },
                new TaskItem { Id = 4,  Title = "4 Construir taskManager API", Status = TaskStatus.Done },
                new TaskItem { Id = 5,  Title = "5 Aprender .Net 10",          Status = TaskStatus.Done },
                new TaskItem { Id = 6,  Title = "6 Construir taskManager API", Status = TaskStatus.Done },
                new TaskItem { Id = 7,  Title = "7 Aprender .Net 10",          Status = TaskStatus.Done },
                new TaskItem { Id = 8,  Title = "8 Construir taskManager API", Status = TaskStatus.Done },
                new TaskItem { Id = 9,  Title = "9 Aprender .Net 10",          Status = TaskStatus.Done },
                new TaskItem { Id = 10, Title = "10 Construir taskManager API",Status = TaskStatus.Done },
                new TaskItem { Id = 11, Title = "11 Construir taskManager API",Status = TaskStatus.Done }
            };

            return tasks;
        }
        #endregion
    }
}
