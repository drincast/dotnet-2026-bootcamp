//Entities/User.cs
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = default!;
    public string Name { get; set; } = default!;

    public ICollection<TaskItem> AssignedTasks { get; } = new List<TaskItem>();
}