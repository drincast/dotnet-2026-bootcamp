namespace TaskManager.Api
{
    public class TaskManagerOptions
    {
        //sección especifica de configuración para endpoints TaskManager
        public const string SectionName = "TaskManager";

        //configuraciones
        public int MaxTasksPerUser { get; set; }
        public bool AllowCompletedTaskDeletion { get; set; }
        public int DefaultPageSize { get; set; }
    }
}
