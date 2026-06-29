namespace TaskManager.Api
{
    // Record para el request (pon esto fuera del Program.cs o al final del archivo)
    record CreateTaskRequest(string Title, int ProjectId, int ? AssignedToId);
}
