// Persistence/Configurations/TaskItemConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Status)
            .HasConversion<string>() // guarda "Todo", "InProgress"... no 0,1,2
            .HasMaxLength(20);

        // builder.Property(t => t.CreatedAt)
        //     //.HasDefaultValueSql("GETUTCDATE()"); //solo SQL Server
        //     .HasDefaultValue(DateTime.UtcNow); //solo se ejecuta una vez y todas las tareas nuevas registradas quedarian con la misma fecha

        builder.HasOne(t => t.Project)
            .WithMany(p => p.Tasks)
            .HasForeignKey(t => t.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.AssignedTo)
            .WithMany(u => u.AssignedTasks)
            .HasForeignKey(t => t.AssignedToId)
            .OnDelete(DeleteBehavior.SetNull);  // si borras un User, la tarea queda sin asignar
    }
}