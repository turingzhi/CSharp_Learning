using IssueTracker.Api.Data;
using IssueTracker.Core.Entities;
using IssueTracker.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace IssueTracker.Api.Services;

public enum MembershipResult { Success, NotFound, InvalidUser, Duplicate, OwnerProtected }

public sealed class MembershipService(AppDbContext db)
{
    public async Task<MembershipResult> AddAsync(Guid projectId, string userId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await db.Projects.AnyAsync(p => p.Id == projectId, ct)) return MembershipResult.NotFound;
        if (!await db.Users.AnyAsync(u => u.Id == userId, ct)) return MembershipResult.InvalidUser;
        if (await db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId, ct))
            return MembershipResult.Duplicate;
        db.ProjectMembers.Add(new ProjectMember(projectId, userId, ProjectRole.Member));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return MembershipResult.Success;
    }

    public async Task<MembershipResult> RemoveAsync(Guid projectId, string userId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var membership = await db.ProjectMembers.SingleOrDefaultAsync(m =>
            m.ProjectId == projectId && m.UserId == userId, ct);
        if (membership is null) return MembershipResult.NotFound;
        if (membership.Role == ProjectRole.Owner) return MembershipResult.OwnerProtected;
        var items = await db.WorkItems.Where(w => w.ProjectId == projectId && w.AssigneeId == userId)
            .ToListAsync(ct);
        foreach (var item in items) item.AssignTo(null);
        db.ProjectMembers.Remove(membership);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return MembershipResult.Success;
    }

    public async Task<MembershipResult> AssignAsync(Guid itemId, string? userId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var item = await db.WorkItems.SingleOrDefaultAsync(w => w.Id == itemId, ct);
        if (item is null) return MembershipResult.NotFound;
        if (userId is not null && (string.IsNullOrWhiteSpace(userId) ||
            !await db.ProjectMembers.AnyAsync(m => m.ProjectId == item.ProjectId && m.UserId == userId, ct)))
            return MembershipResult.InvalidUser;
        item.AssignTo(userId);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return MembershipResult.Success;
    }
}
