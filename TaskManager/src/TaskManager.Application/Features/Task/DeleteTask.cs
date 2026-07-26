using MediatR;
using TaskManager.Application.Common.Interfaces;
using TaskManager.Domain.Entities;

namespace TaskManager.Application.Features.Task
{
    public static class DeleteTask
    {
        // La entrada (lo que antes era CreateTaskRequest)
        public sealed record Command(int Id)
            : IRequest<bool>; // devuelve el Id de la tarea creada

        public sealed class Handler : IRequestHandler<Command, bool>
        {
            private readonly IApplicationDbContext _db;
            public Handler(IApplicationDbContext db) => _db = db;

            public async Task<bool> Handle(Command request, CancellationToken ct)
            {
                var taskItem = await _db.TaskItems.FindAsync([request.Id], ct);

                if (taskItem is null)
                    return false;

                _db.TaskItems.Remove(taskItem);
                await _db.SaveChangesAsync(ct);

                return true;
            }
        }
    }
}
