using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TaskManager.Api.Features.Task
{
    public static class GetTask
    {
        // 1. El DTO de salida (contrato público, desacoplado de la entidad)
        public sealed record ItemDto(int Id, string Title, bool Done);

        // 2. La Query — es un IRequest que DEVUELVE algo
        public sealed record Query(int id)
            : IRequest<ItemDto?>;

        // 3. El Handler — la lógica que antes vivía en el endpoint
        public sealed class Handler
            : IRequestHandler<Query, ItemDto?>
        {
            private readonly TaskManagerDbContext _db;
            //private readonly TaskManagerOptions _options;

            public Handler(TaskManagerDbContext db)
            {
                _db = db;
                //_options = options.Value;
            }

            public async Task<ItemDto?> Handle(
                Query request, CancellationToken ct)
            {
                var task = await _db.TaskItems
                    .AsNoTracking()
                    .Where(t => t.Id == request.id)
                    .Select(t => new ItemDto
                    (
                        t.Id,
                        t.Title,
                        t.Status == TaskStatus.Done
                    ))
                    .FirstOrDefaultAsync(ct);

                return task;
            }
        }
    }
}
