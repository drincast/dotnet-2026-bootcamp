//Entities/TaskItem.cs  (Task es palabra reservada en C#)
public class TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public TaskStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    
    public int ProjectId { get; set; }
    public Project Project { get; set; } = default!;
    
    public int? AssignedToId { get; set; }
    public User? AssignedTo { get; set; }
}

public enum TaskStatus { Todo, InProgress, Done, Cancelled }