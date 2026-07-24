using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using MediatR;
using TaskManager.Api.Features.Task;

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

        /// <summary>
        /// GET /tasks - retorna lista de la base de datos
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="ct"></param>
        /// <param name="page"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        private static async Task<IResult> GetAll(
            ISender sender, CancellationToken ct, int page = 1, int? pageSize = null)
        {
            var result = await sender.Send(new GetAllTasks.Query(page, pageSize), ct);
            return Results.Ok(result);
        }

        /// <summary>
        /// GET /tasks - retorna una tarea por id
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="ct"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        private static async Task<IResult> GetById(ISender sender
            , CancellationToken ct, int id)
        {
            var result = await sender.Send(new GetTask.Query(id), ct);
            return result is null
                ? Results.NotFound()
                : Results.Ok(result);
        }

        /// <summary>
        /// GET /tasks/detailed - retorna el detalle de las tareas
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="ct"></param>
        /// <param name="page"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        private static async Task<IResult> GetTaskDetailed(
            ISender sender, CancellationToken ct, int page = 1, int? pageSize = null)
        {
            var result = await sender.Send(new GetAllDetailedTasks.Query(page, pageSize), ct);
            return Results.Ok(result);
        }

        /// <summary>
        /// //POST /tasks - crea una tarea
        /// </summary>
        /// <param name="request"></param>
        /// <param name="sender"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        private static async Task<IResult> Create(
            CreateTaskRequest request, ISender sender, CancellationToken ct)
        {
            var id = await sender.Send(
                new CreateTask.Command(request.Title, request.ProjectId, request.AssignedToId)
                , ct
            );

            return Results.Created($"/tasks/{id}", new { Id = id });
        }

        /// <summary>
        /// PUT actualizar una tarea
        /// </summary>
        /// <param name="request"></param>
        /// <param name="sender"></param>
        /// <param name="ct"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        private static async Task<IResult> Update(UpdateTaskRequest request
            , ISender sender
            , CancellationToken ct
            , int id)
        {
           var response = await sender.Send(
                new UpdateTask.Command(id, request.Title, request.Status)
                , ct
            );

            return response switch
            {
                UpdateTask.UpdateResult.Success => Results.NoContent(),
                UpdateTask.UpdateResult.NotFound => Results.NotFound(),
                UpdateTask.UpdateResult.InvalidStatus => Results.BadRequest($"Estado inválido: {request.Status}"),
                _ => Results.Problem($"Error en proceso de respuesta, respuesta no identificada")
            };
        }

        /// <summary>
        /// DELETE /tasks - elimina una tarea
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="ct"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        private static async Task<IResult> Delete(
            ISender sender, CancellationToken ct, int id)
        {
            var response = await sender.Send(
                new DeleteTask.Command(id)
                , ct
            );

            return response
                ? Results.NoContent() 
                : Results.NotFound();
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
