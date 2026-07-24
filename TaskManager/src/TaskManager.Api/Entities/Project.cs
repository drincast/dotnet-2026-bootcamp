//Entities/Project.cs
public class Project
{
    public int Id {get; set;}
    public string Name {get; set;} = default!;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<TaskItem> Tasks { get; } = new List<TaskItem>();

}