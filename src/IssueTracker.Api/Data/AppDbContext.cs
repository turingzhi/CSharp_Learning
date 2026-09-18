using IssueTracker.Api.Security;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using IssueTracker.Core.Entities;
using Microsoft.EntityFrameworkCore;
using SQLitePCL;

namespace IssueTracker.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {

    }
    // public DbSet<Project> Projects { get; set; }
    // public DbSet<WorkItem> WorkItems { get; set; }
    // public DbSet<Comment> Comments { get; set; }
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Project>()
            .HasKey(p => p.Id);
        modelBuilder.Entity<Project>()
            .Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);
        modelBuilder.Entity<Project>()
            .Property(p => p.Description)
            .HasMaxLength(2000);

        modelBuilder.Entity<WorkItem>()
            .HasKey(w => w.Id);
        modelBuilder.Entity<WorkItem>()
            .Property(w => w.Title)
            .IsRequired()
            .HasMaxLength(200);
        modelBuilder.Entity<WorkItem>()
            .Property(w => w.Description)
            .HasMaxLength(2000);
        modelBuilder.Entity<WorkItem>()
            .HasOne<Project>()
            .WithMany()
            .HasForeignKey(w => w.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<WorkItem>()
            .Property(w => w.CreatedAt)
            .HasConversion(
                value => value.UtcDateTime.Ticks,
                value => new DateTimeOffset(value, TimeSpan.Zero));

        modelBuilder.Entity<WorkItem>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(w => w.AssigneeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Comment>()
            .HasKey(c => c.Id);
        modelBuilder.Entity<Comment>()
            .Property(c => c.Body)
            .IsRequired()
            .HasMaxLength(2000);
        modelBuilder.Entity<Comment>()
            .HasOne<WorkItem>()
            .WithMany()
            .HasForeignKey(c => c.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Comment>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProjectMember>()
        .HasKey(member => new
        {
            member.ProjectId,
            member.UserId
        });

    modelBuilder.Entity<ProjectMember>()
        .HasOne<Project>()
        .WithMany()
        .HasForeignKey(member => member.ProjectId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<ProjectMember>()
        .HasOne<ApplicationUser>()
        .WithMany()
        .HasForeignKey(member => member.UserId)
        .OnDelete(DeleteBehavior.Restrict);

    }
}