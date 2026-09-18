using IssueTracker.Api.Contracts.WorkItems;
using System.Collections.Concurrent;
using IssueTracker.Api.Data;
using IssueTracker.Core.Entities;
using IssueTracker.Core.Enums;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IssueTracker.Api.Contracts;

namespace IssueTracker.Api.Services;

public class WorkItemService
{
    // private readonly ConcurrentDictionary<Guid, WorkItem> _WorkItems = new();
    private readonly AppDbContext _dbContext;
    private readonly ILogger<WorkItemService> _logger;

    public WorkItemService(
        AppDbContext dbContext,
        ILogger<WorkItemService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<WorkItem> CreateAsync(Guid projectId, string title, string? description, CancellationToken cancellationToken)
    {
        var workItem = new WorkItem(projectId, title, description);
        _dbContext.WorkItems.Add(workItem);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
        "Created work item {WorkItemId} in project {ProjectId}",
        workItem.Id,
        projectId);
        return workItem;
    }
    public async Task<WorkItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.WorkItems.AsNoTracking()
        .SingleOrDefaultAsync(w => w.Id == id, cancellationToken);
    }
    // public async Task<List<WorkItem>> GetAllByProjectIdAsync(Guid projectId, CancellationToken cancellationToken)
    // {
    //     return await _dbContext.WorkItems.AsNoTracking()
    //     .Where(w => w.ProjectId == projectId).ToListAsync(cancellationToken);
    // }
    // public async Task<List<WorkItem>> GetAllByProjectIdAsync(
    // Guid projectId,
    // WorkItemQuery request,
    // CancellationToken cancellationToken)
    // {
    //     var query = _dbContext.WorkItems
    //         .AsNoTracking()
    //         .Where(w => w.ProjectId == projectId);

    //     if (request.Status.HasValue)
    //     {
    //         query = query.Where(w => w.Status == request.Status.Value);
    //     }

    //     if (!string.IsNullOrWhiteSpace(request.Search))
    //     {
    //         var search = request.Search.Trim();

    //         query = query.Where(w => w.Title.Contains(search));
    //     }

    //     return await query.ToListAsync(cancellationToken);
    // }

    // public async Task<PagedResponse<WorkItem>> GetAllByProjectIdAsync(
    //     Guid projectId,
    //     WorkItemQuery request,
    //     CancellationToken cancellationToken)
    // {
    //     var query = _dbContext.WorkItems
    //         .AsNoTracking()
    //         .Where(w => w.ProjectId == projectId);

    //     if (request.Status.HasValue)
    //     {
    //         query = query.Where(w => w.Status == request.Status.Value);
    //     }

    //     if (!string.IsNullOrWhiteSpace(request.Search))
    //     {
    //         var search = request.Search.Trim();
    //         query = query.Where(w => w.Title.Contains(search));
    //     }

    //     var totalCount = await query.CountAsync(cancellationToken);

    //     var offset = (request.Page - 1L) * request.PageSize;

    //     if (offset >= totalCount)
    //     {
    //         return new PagedResponse<WorkItem>(
    //             Array.Empty<WorkItem>(),
    //             request.Page,
    //             request.PageSize,
    //             totalCount);
    //     }

    //     var items = await query
    //         .OrderBy(w => w.CreatedAt)
    //         .ThenBy(w => w.Id)
    //         .Skip((int)offset)
    //         .Take(request.PageSize)
    //         .ToListAsync(cancellationToken);

    //     return new PagedResponse<WorkItem>(
    //         items,
    //         request.Page,
    //         request.PageSize,
    //         totalCount);
    // }
    public async Task<PagedResponse<WorkItem>> GetAllByProjectIdAsync(
    Guid projectId,
    WorkItemQuery request,
    CancellationToken cancellationToken)
    {
        var query = _dbContext.WorkItems
            .AsNoTracking()
            .Where(w => w.ProjectId == projectId);

        if (request.Status.HasValue)
        {
            query = query.Where(w => w.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(w => w.Title.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var offset = (request.Page - 1L) * request.PageSize;

        if (offset >= totalCount)
        {
            return new PagedResponse<WorkItem>(
                Array.Empty<WorkItem>(),
                request.Page,
                request.PageSize,
                totalCount);
        }

        var items = await query
            .OrderBy(w => w.CreatedAt)
            .ThenBy(w => w.Id)
            .Skip((int)offset)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<WorkItem>(
            items,
            request.Page,
            request.PageSize,
            totalCount);
    }
    public async Task<List<WorkItem>> GetAllAsync(string userId, CancellationToken cancellationToken)
    {
        return await _dbContext.WorkItems.AsNoTracking()
            .Where(w => _dbContext.ProjectMembers.Any(m =>
                m.ProjectId == w.ProjectId && m.UserId == userId))
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkItem?> UpdateAsync(Guid id, string title, string? description, CancellationToken cancellationToken)
    {
        var workItem = await _dbContext.WorkItems.SingleOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (workItem is null)
        {
            return null;
        }
        workItem.Update(title, description);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return workItem;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var workItem = await _dbContext.WorkItems.SingleOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (workItem is null)
        {
            return false;
        }
        _dbContext.WorkItems.Remove(workItem);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;

    }
    public async Task<WorkItem?> ChangeStatusAsync(Guid id, WorkItemStatus status, CancellationToken cancellationToken)
    {
        var workItem = await _dbContext.WorkItems.SingleOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (workItem is null)
        {
            return null;
        }
        workItem.ChangeStatus(status);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return workItem;
    }
}