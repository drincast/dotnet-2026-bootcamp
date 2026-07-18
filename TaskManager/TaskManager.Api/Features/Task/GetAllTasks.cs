using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskManager.Api.Common;

namespace TaskManager.Api.Features.Task
{
    public static class GetAllTasks
    {
        // 1. El DTO de salida (contrato público, desacoplado de la entidad)
        public sealed record TaskListItemDto(int Id, string Title, bool Done);

        // 2. La Query — es un IRequest que DEVUELVE algo
        public sealed record Query(int Page, int? PageSize)
            : IRequest<PagedResult<TaskListItemDto>>;

        // 3. El Handler — la lógica que antes vivía en el endpoint
        public sealed class Handler
            :IRequestHandler<Query, PagedResult<TaskListItemDto>>
        {
            private readonly TaskManagerDbContext _db;
            private readonly TaskManagerOptions _options;

            public Handler(TaskManagerDbContext db, IOptions<TaskManagerOptions> options)
            {
                _db = db;
                _options = options.Value;
            }

            public async Task<PagedResult<TaskListItemDto>> Handle(
                Query request, CancellationToken ct)
            {
                var size = request.PageSize ?? _options.DefaultPageSize;
                var totalItems = await _db.TaskItems.CountAsync(ct);

                var items = await _db.TaskItems
                    .AsNoTracking()
                    .OrderBy(t => t.Id)
                    .Skip((request.Page - 1) * size)
                    .Take(size)
                    .Select(t => new TaskListItemDto(
                        t.Id, t.Title, t.Status == TaskStatus.Done
                    ))
                    .ToListAsync(ct);

                return new PagedResult<TaskListItemDto>(
                    request.Page, size, totalItems, items);
            }
        }
    }
}
