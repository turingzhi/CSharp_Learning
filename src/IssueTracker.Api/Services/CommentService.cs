using System.Collections.Concurrent;
using IssueTracker.Api.Data;
using IssueTracker.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssueTracker.Api.Services;

public class CommentService
{
    // private readonly ConcurrentDictionary<Guid, Project> _projects = new();
    private readonly AppDbContext _dbContext;
    public CommentService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Comment> CreateAsync(Guid workItemId, string body, string authorId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorId);
        var comment = new Comment(workItemId, body, authorId);
        _dbContext.Comments.Add(comment);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return comment;
    }
    public async Task<Comment?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Comments.AsNoTracking()
        .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<List<Project>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Projects.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<Project?> UpdateAsync(Guid id, string name, string? description, CancellationToken cancellationToken)
    {
        var project = await _dbContext.Projects.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (project is null)
        {
            return null;
        }
        project.Update(name, description);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return project;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var comment = await _dbContext.Comments.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (comment is null)
        {
            return false;
        }
        _dbContext.Comments.Remove(comment);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<Comment>> GetAllCommentsByWorkItemIdAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        return await _dbContext.Comments.AsNoTracking()
        .Where(c => c.WorkItemId == workItemId).ToListAsync(cancellationToken);
        

    }
}