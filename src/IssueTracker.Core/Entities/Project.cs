namespace IssueTracker.Core.Entities;

public class Project
{
    private Project()
    {
        Name = string.Empty;
    }
    public Project(string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Empty name");
        }
        var nameTrimed = name.Trim();
        if (nameTrimed.Length > 100)
        {
            throw new ArgumentException("The project name is too long");
        }
        if (!string.IsNullOrEmpty(description) && description.Length > 2000) 
        {
            throw new ArgumentException("The desc is too long");

        }
        Id = Guid.NewGuid();
        Name = nameTrimed;
        Description = description;
        CreatedAt = DateTimeOffset.UtcNow;

    }
    public void Update(string name, string? description)
    {
        // implementation
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("name cannot be empty");
        }
        string name_trimed = name.Trim();
        if (name_trimed.Length > 100)
        {
            throw new ArgumentException("name length cannot be over 100");
        }
        string? desc_trimed = description?.Trim();
        if (desc_trimed?.Length > 2000)
        {
            throw new ArgumentException("desc length cannot be over 2000");
        }
        Name = name_trimed;
        Description = desc_trimed;
    }
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}