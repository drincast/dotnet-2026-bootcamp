using MediatR;
using TaskManager.Application.Common.Interfaces;
using TaskManager.Domain.Entities;

using TaskStatus = TaskManager.Domain.Entities.TaskStatus;

namespace TaskManager.Application.Features.Task
{
    public static class CreateTask
    {
        // La entrada (lo que antes era CreateTaskRequest)
        public sealed record Command(string Title, int ProjectId, int? AssignedToId)
            : IRequest<int>; // devuelve el Id de la tarea creada

        public sealed class Handler : IRequestHandler<Command, int>
        {
            private readonly IApplicationDbContext _db; //TaskManagerDbContext
            public Handler(IApplicationDbContext db) => _db = db;

            public async Task<int> Handle(Command request, CancellationToken ct)
            {
                var task = new TaskItem
                {
                    Title = request.Title,
                    Status = TaskStatus.Todo,
                    ProjectId = request.ProjectId,
                    AssignedToId = request.AssignedToId,
                    CreatedAt = DateTime.UtcNow,
                };

                _db.TaskItems.Add(task);
                await _db.SaveChangesAsync(ct);

                return task.Id;
            }
        }
    }
}
