using MediatR;
using TaskManager.Application.Common;
using TaskManager.Application.Common.Interfaces;
using TaskManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using TaskStatus = TaskManager.Domain.Entities.TaskStatus;

namespace TaskManager.Application.Features.Task
{
    public static class GetAllDetailedTasks
    {
        // 1. El DTO de salida (contrato público, desacoplado de la entidad)
        //public sealed record TaskListItemDto(int Id, string Title, bool Done);
        public sealed record TaskDetailDto(int Id, string Title, string? Description, TaskStatus Status
        , DateTime CreatedAt, int ProjectId, string ProjectName, int? AssignedToId, string? AssignedToName);

        // 2. La Query — es un IRequest que DEVUELVE algo
        public sealed record Query(int Page, int? PageSize)
            : IRequest<PagedResult<TaskDetailDto>>;

        // 3. El Handler — la lógica que antes vivía en el endpoint
        public sealed class Handler
            :IRequestHandler<Query, PagedResult<TaskDetailDto>>
        {
            private readonly IApplicationDbContext _db;
            private readonly TaskManagerOptions _options;

            public Handler(IApplicationDbContext db, IOptions<TaskManagerOptions> options)
            {
                _db = db;
                _options = options.Value;
            }

            public async Task<PagedResult<TaskDetailDto>> Handle(
                Query request, CancellationToken ct)
            {
                var size = request.PageSize ?? _options.DefaultPageSize;
                var totalItems = await _db.TaskItems.CountAsync(ct);

                var items = await _db.TaskItems
                    .AsNoTracking()
                    .OrderBy(t => t.Id)
                    .Skip((request.Page - 1) * size)
                    .Take(size)
                    .Select(t => new TaskDetailDto(
                        t.Id,
                        t.Title,
                        t.Description,
                        t.Status,
                        t.CreatedAt,
                        t.ProjectId,
                        t.Project!.Name, //es obligatorio el proyecto debe existir
                        t.AssignedToId,
                        t.AssignedTo != null ? t.AssignedTo.Name : null
                    ))
                    .ToListAsync(ct);

                return new PagedResult<TaskDetailDto>(
                    request.Page, size, totalItems, items);
            }
        }
    }
}
