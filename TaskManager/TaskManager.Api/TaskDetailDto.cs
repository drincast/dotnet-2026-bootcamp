namespace TaskManager.Api
{
    record TaskDetailDto(int Id, string Title, string? Description, TaskStatus Status
        , DateTime CreatedAt, int ProjectId, string ProjectName, int? AssignedToId, string? AssignedToName);
}
