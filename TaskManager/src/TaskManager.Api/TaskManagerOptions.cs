namespace TaskManager.Api
{
    public class TaskManagerOptions
    {
        //sección especifica de configuración para endpoints TaskManager
        public const string SectionName = "TaskManager";

        //configuraciones
        public bool AllowCompletedTaskDeletion { get; set; }
        public int DefaultPageSize { get; set; }
        public string ? GeneralErrorApp { get; set; }
        public int MaxTasksPerUser { get; set; }
    }
}
