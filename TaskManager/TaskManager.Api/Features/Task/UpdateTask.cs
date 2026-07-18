using MediatR;

namespace TaskManager.Api.Features.Task
{
    public static class UpdateTask
    {
        //salida
        public enum UpdateResult { Success, NotFound, InvalidStatus }

        //entrada con datos a modificar y retorna id de la tarea modificada
        public sealed record Command(int Id, string Title, string Status)
            : IRequest<UpdateResult>;

        public sealed class Handler : IRequestHandler<Command, UpdateResult>
        {
            private readonly TaskManagerDbContext _db;
            public Handler(TaskManagerDbContext db) => _db = db;

            public async Task<UpdateResult> Handle(Command request, CancellationToken ct)
            {

                TaskStatus status;

                //validacion del estado
                if (!Enum.TryParse<TaskStatus>(request.Status, ignoreCase: true, out status))
                    return UpdateResult.InvalidStatus; // Results.BadRequest($"Estado inválido: {request.Status}");

                //buscamos la tarea
                var taskItem = await _db.TaskItems.FindAsync([request.Id], ct);

                if (taskItem is null)
                    return UpdateResult.NotFound;

                taskItem.Title = request.Title;
                taskItem.Status = status;
                await _db.SaveChangesAsync(ct);

                return UpdateResult.Success;
            }
        }
    }
}
