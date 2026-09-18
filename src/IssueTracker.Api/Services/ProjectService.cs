using System.Collections.Concurrent;
using IssueTracker.Api.Data;
using IssueTracker.Core.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IssueTracker.Core.Enums;

namespace IssueTracker.Api.Services;

public class ProjectService
{
    // private readonly ConcurrentDictionary<Guid, Project> _projects = new();
    private readonly AppDbContext _dbContext;
    public ProjectService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public async Task<ProjectRole?> GetRoleAsync(
    Guid projectId,
    string userId,
    CancellationToken cancellationToken)
    {
        return await _dbContext.ProjectMembers
            .Where(member =>
                member.ProjectId == projectId &&
                member.UserId == userId)
            .Select(member => (ProjectRole?)member.Role)
            .SingleOrDefaultAsync(cancellationToken);
    }

    // public async Task<Project> CreateAsync(string name, string? description, CancellationToken cancellationToken)
    // {
    //     var project = new Project(name, description);
    //     _dbContext.Projects.Add(project);
    //     await _dbContext.SaveChangesAsync(cancellationToken);
    //     return project;
    // }
    public async Task<Project> CreateAsync(
    string name,
    string? description,
    string ownerId,
    CancellationToken cancellationToken)
    {
        var project = new Project(name, description);

        var membership = new ProjectMember(
            project.Id,
            ownerId,
            ProjectRole.Owner);

        _dbContext.Projects.Add(project);
        _dbContext.ProjectMembers.Add(membership);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return project;
    }
    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Projects.AsNoTracking()
        .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    // public async Task<List<Project>> GetAllAsync(CancellationToken cancellationToken)
    // {
    //     return await _dbContext.Projects.AsNoTracking().ToListAsync(cancellationToken);
    // }
    public async Task<List<Project>> GetAllAsync(
    string userId,
    CancellationToken cancellationToken)
    {
        return await _dbContext.Projects
            .AsNoTracking()
            .Where(project => _dbContext.ProjectMembers.Any(member =>
                member.ProjectId == project.Id &&
                member.UserId == userId))
            .ToListAsync(cancellationToken);
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
        var project = await _dbContext.Projects.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (project is null)
        {
            return false;
        }
        _dbContext.Projects.Remove(project);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;

    }
    public async Task<bool> IsMemberAsync(
    Guid projectId,
    string userId,
    CancellationToken cancellationToken)
    {
        return await _dbContext.ProjectMembers
            .AnyAsync(member =>
                member.ProjectId == projectId &&
                member.UserId == userId,
                cancellationToken);
    }
}