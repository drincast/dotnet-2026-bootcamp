namespace TaskManager.Api
{
    //public class CreateTaskRequest
    //{
    //}

    // Record para el request (pon esto fuera del Program.cs o al final del archivo)
    record UpdateTaskRequest(string Title, string Status);
}
