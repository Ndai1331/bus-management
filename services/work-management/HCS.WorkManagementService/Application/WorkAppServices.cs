using HCS.WorkManagementService.Contracts;
using HCS.WorkManagementService.Contracts.Integration;
using HCS.WorkManagementService.Data;
using HCS.WorkManagementService.Domain;
using HCS.WorkManagementService.Integration;
using HCS.IntegrationEvents.Work;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;

namespace HCS.WorkManagementService.Application;

public static class SurveySubmissionIdentity
{
    public static Guid Resolve(Guid authenticatedUserId, Guid? untrustedRequestedUserId) => authenticatedUserId;
}

public sealed class ProjectAppService(WorkManagementDbContext db, WorkRecordAuthorization access, OutboxDispatcher outbox) : ITransientDependency
{
    public async Task<PagedWorkDto<ProjectDto>> GetListAsync(string? filter, string? status, int skip, int take, CancellationToken ct)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 100);
        var query = access.VisibleProjects().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var value = filter.Trim().ToLowerInvariant();
            query = query.Where(x => EF.Functions.ILike(x.Code, $"%{value}%")
                                  || EF.Functions.ILike(x.Name, $"%{value}%"));
        }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var total = await query.LongCountAsync(ct);
        var page = await query.OrderBy(x => x.Code).Skip(skip).Take(take).ToListAsync(ct);
        var ids = page.Select(x => x.Id).ToList();
        var memberCounts = await db.ProjectMembers.AsNoTracking()
            .Where(x => ids.Contains(x.ProjectId) && x.IsActive)
            .GroupBy(x => x.ProjectId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var taskCounts = await db.ProjectTasks.AsNoTracking()
            .Where(x => ids.Contains(x.ProjectId))
            .GroupBy(x => x.ProjectId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var items = page.Select(x => Map(x, memberCounts.GetValueOrDefault(x.Id), taskCounts.GetValueOrDefault(x.Id))).ToList();
        return new(total, items);
    }

    public async Task<ProjectDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        await access.DemandProjectMemberAsync(id, ct);
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new EntityNotFoundException(typeof(Project), id);
        var members = await db.ProjectMembers.AsNoTracking().Where(x => x.ProjectId == id)
            .OrderBy(x => x.Role).Select(x => new ProjectMemberDto(x.Id, x.ProjectId, x.UserId, x.Role, x.IsActive))
            .ToListAsync(ct);
        var tasks = await db.ProjectTasks.AsNoTracking().Where(x => x.ProjectId == id).OrderBy(x => x.Code)
            .Select(x => MapTask(x)).ToListAsync(ct);
        return new(Map(project), members, tasks);
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectDto input, CancellationToken ct)
    {
        if (await db.Projects.AnyAsync(x => x.Code == input.Code, ct)) throw new BusinessException("Work:DuplicateProjectCode");
        var project = new Project(Guid.NewGuid(), input.Code, input.Name, input.StartDate, input.EndDate,
            input.Status, input.OwnerDepartmentId, access.UserId, input.Description);
        db.Projects.Add(project);
        await WorkCalendarLinker.SyncProjectAsync(db, project, ct);
        AddEvent(new ProjectChangedEto(Guid.NewGuid(), DateTime.UtcNow, project.Id, "Created", project.Status));
        AddAccessEvent(project.Id, null, false, [project.OwnerUserId]);
        await db.SaveChangesAsync(ct);
        await outbox.DispatchAsync(ct);
        return Map(project);
    }

    public async Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectDto input, CancellationToken ct)
    {
        await access.DemandProjectOwnerAsync(id, ct);
        var project = await db.Projects.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new EntityNotFoundException(typeof(Project), id);
        project.Change(input.Name, input.Description, input.StartDate, input.EndDate, input.Status);
        await WorkCalendarLinker.SyncProjectAsync(db, project, ct);
        AddEvent(new ProjectChangedEto(Guid.NewGuid(), DateTime.UtcNow, project.Id, "Updated", project.Status));
        await db.SaveChangesAsync(ct);
        return Map(project);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await access.DemandProjectOwnerAsync(id, ct);
        var project = await db.Projects.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new EntityNotFoundException(typeof(Project), id);
        if (await db.ProjectTasks.AnyAsync(x => x.ProjectId == id, ct)) throw new BusinessException("Work:ProjectHasTasks");
        await WorkCalendarLinker.DeleteRelatedAsync(db, WorkCalendarSync.ProjectRelatedType, id, ct);
        db.Projects.Remove(project);
        AddEvent(new ProjectChangedEto(Guid.NewGuid(), DateTime.UtcNow, id, "Deleted", project.Status));
        AddAccessEvent(project.Id, null, true, []);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ProjectMemberDto> AddMemberAsync(Guid projectId, AddProjectMemberDto input, CancellationToken ct)
    {
        await access.DemandProjectOwnerAsync(projectId, ct);
        if (!await db.Projects.AnyAsync(x => x.Id == projectId, ct)) throw new EntityNotFoundException(typeof(Project), projectId);
        if (await db.ProjectMembers.AnyAsync(x => x.ProjectId == projectId && x.UserId == input.UserId, ct))
            throw new BusinessException("Work:DuplicateProjectMember");
        if (input.Role is not ("Manager" or "Supervisor" or "Member"))
            throw new BusinessException("Work:InvalidProjectRole");
        var member = new ProjectMember(Guid.NewGuid(), projectId, input.UserId, input.Role);
        db.ProjectMembers.Add(member);
        var project = await db.Projects.AsNoTracking().SingleAsync(x => x.Id == projectId, ct);
        if (input.UserId != access.UserId)
        {
            db.OutboxMessages.Add(WorkOutbox.CreateCanonical(new ProjectMemberAssignedEto(
                Guid.NewGuid(), DateTimeOffset.UtcNow, Correlation(), projectId, input.UserId, project.Name), Correlation()));
        }
        var users = await AuthorizedUsers(projectId, ct); users.Add(input.UserId);
        await WorkCalendarLinker.ReplaceParticipantsAsync(db, WorkCalendarSync.ProjectRelatedType, projectId, users, ct);
        AddAccessEvent(projectId, null, false, users.Distinct().ToArray());
        var tasks = await db.ProjectTasks.Where(x => x.ProjectId == projectId).Select(x => x.Id).ToListAsync(ct);
        var taskAssignments = await db.ProjectTaskAssignments.Where(x => tasks.Contains(x.ProjectTaskId))
            .Select(x => new { x.ProjectTaskId, x.UserId }).ToListAsync(ct);
        foreach (var task in tasks)
        {
            var taskUsers = taskAssignments.Where(x => x.ProjectTaskId == task).Select(x => x.UserId).ToList();
            taskUsers.AddRange(users);
            AddAccessEvent(projectId, task, false, taskUsers.Distinct().ToArray());
        }
        await db.SaveChangesAsync(ct);
        await outbox.DispatchAsync(ct);
        return new(member.Id, member.ProjectId, member.UserId, member.Role, member.IsActive);
    }

    public async Task RemoveMemberAsync(Guid projectId, Guid memberId, CancellationToken ct)
    {
        await access.DemandProjectOwnerAsync(projectId, ct);
        var member = await db.ProjectMembers.SingleOrDefaultAsync(x => x.Id == memberId && x.ProjectId == projectId, ct)
            ?? throw new EntityNotFoundException(typeof(ProjectMember), memberId);
        db.ProjectMembers.Remove(member);
        var users = (await AuthorizedUsers(projectId, ct)).Where(x => x != member.UserId).ToList();
        users.Add(await OwnerUserId(projectId, ct));
        await WorkCalendarLinker.ReplaceParticipantsAsync(db, WorkCalendarSync.ProjectRelatedType, projectId, users, ct);
        AddAccessEvent(projectId, null, false, users.Distinct().ToArray());
        await db.SaveChangesAsync(ct);
        await outbox.DispatchAsync(ct);
    }

    public async Task SyncChatAccessAsync(Guid projectId, CancellationToken ct)
    {
        await access.DemandProjectMemberAsync(projectId, ct);
        var users = (await AuthorizedUsers(projectId, ct)).Distinct().ToArray();
        AddAccessEvent(projectId, null, false, users);
        await db.SaveChangesAsync(ct);
        await outbox.DispatchAsync(ct);
    }

    private void AddEvent(IWorkIntegrationEvent value) => db.OutboxMessages.Add(WorkOutbox.Create(value, Correlation()));
    private void AddAccessEvent(Guid projectId, Guid? taskId, bool deleted, IReadOnlyList<Guid> users) =>
        db.OutboxMessages.Add(WorkOutbox.CreateCanonical(new WorkSubjectAccessChangedEto(Guid.NewGuid(), DateTimeOffset.UtcNow,
            Correlation(), taskId.HasValue ? "Task" : "Project", projectId, taskId, deleted, users), Correlation()));
    private async Task<List<Guid>> AuthorizedUsers(Guid projectId, CancellationToken ct)
    {
        var owner = await OwnerUserId(projectId, ct);
        var users = await db.ProjectMembers.Where(x => x.ProjectId == projectId && x.IsActive).Select(x => x.UserId).ToListAsync(ct);
        users.Add(owner); return users;
    }
    private Task<Guid> OwnerUserId(Guid projectId, CancellationToken ct) =>
        db.Projects.Where(x => x.Id == projectId).Select(x => x.OwnerUserId).SingleAsync(ct);
    private static ProjectDto Map(Project x, int memberCount = 0, int taskCount = 0) =>
        new(x.Id, x.Code, x.Name, x.Description, x.StartDate, x.EndDate, x.Status, x.OwnerDepartmentId, x.OwnerUserId, memberCount, taskCount);
    private static ProjectTaskDto MapTask(ProjectTask x) => new(x.Id, x.ProjectId, x.ParentTaskId, x.Code, x.Title,
        x.Description, x.StartDate, x.DueDate, x.Priority, x.Status, x.ProgressPercent);
    private static string Correlation() => System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
}

public sealed class ProjectTaskAppService(WorkManagementDbContext db, WorkRecordAuthorization access) : ITransientDependency
{
    public async Task<PagedWorkDto<ProjectTaskDto>> GetListAsync(Guid? projectId, string? filter, string? status, int skip, int take, CancellationToken ct)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 100);
        var visibleProjectIds = access.VisibleProjects().Select(x => x.Id);
        var query = db.ProjectTasks.AsNoTracking().Where(x => visibleProjectIds.Contains(x.ProjectId) ||
            db.ProjectTaskAssignments.Any(a => a.ProjectTaskId == x.Id && a.UserId == access.UserId));
        if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var value = filter.Trim().ToLowerInvariant();
            query = query.Where(x => EF.Functions.ILike(x.Code, $"%{value}%")
                                  || EF.Functions.ILike(x.Title, $"%{value}%"));
        }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var total = await query.LongCountAsync(ct);
        var items = await query.OrderBy(x => x.Code).Skip(skip).Take(take).Select(x => Map(x)).ToListAsync(ct);
        return new(total, items);
    }

    public async Task<ProjectTaskDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        await access.DemandTaskMemberAsync(id, ct);
        var task = await db.ProjectTasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new EntityNotFoundException(typeof(ProjectTask), id);
        var assignments = await db.ProjectTaskAssignments.AsNoTracking().Where(x => x.ProjectTaskId == id)
            .Select(x => new TaskAssignmentDto(x.Id, x.ProjectTaskId, x.UserId, x.AssignmentType)).ToListAsync(ct);
        var documents = await db.ProjectTaskDocuments.AsNoTracking().Where(x => x.ProjectTaskId == id)
            .Select(x => new TaskDocumentReferenceDto(x.Id, x.ProjectTaskId, x.DocumentId, x.DocumentCode)).ToListAsync(ct);
        return new(Map(task), assignments, documents);
    }

    public async Task<ProjectTaskDto> CreateAsync(CreateProjectTaskDto input, CancellationToken ct)
    {
        await access.DemandProjectOwnerAsync(input.ProjectId, ct);
        if (!await db.Projects.AnyAsync(x => x.Id == input.ProjectId, ct)) throw new EntityNotFoundException(typeof(Project), input.ProjectId);
        if (input.ParentTaskId.HasValue && !await db.ProjectTasks.AnyAsync(x => x.Id == input.ParentTaskId && x.ProjectId == input.ProjectId, ct))
            throw new BusinessException("Work:InvalidParentTask");
        if (await db.ProjectTasks.AnyAsync(x => x.ProjectId == input.ProjectId && x.Code == input.Code, ct))
            throw new BusinessException("Work:DuplicateTaskCode");
        var task = new ProjectTask(Guid.NewGuid(), input.ProjectId, input.ParentTaskId, input.Code, input.Title,
            input.Description, input.StartDate, input.DueDate, input.Priority, input.Status, input.ProgressPercent);
        db.ProjectTasks.Add(task); AddEvent(task, "Created", []);
        await WorkCalendarLinker.SyncTaskAsync(db, task, await OwnerUserId(task.ProjectId, ct), ct);
        await AddAccessEvent(task, false, [], ct); await db.SaveChangesAsync(ct); return Map(task);
    }

    public async Task<ProjectTaskDto> UpdateAsync(Guid id, UpdateProjectTaskDto input, CancellationToken ct)
    {
        await access.DemandTaskMemberAsync(id, ct);
        var task = await db.ProjectTasks.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new EntityNotFoundException(typeof(ProjectTask), id);
        task.Change(input.Title, input.Description, input.StartDate, input.DueDate, input.Priority, input.Status, input.ProgressPercent);
        await WorkCalendarLinker.SyncTaskAsync(db, task, await OwnerUserId(task.ProjectId, ct), ct);
        var users = await db.ProjectTaskAssignments.Where(x => x.ProjectTaskId == id).Select(x => x.UserId).Distinct().ToListAsync(ct);
        AddEvent(task, "Updated", users); await db.SaveChangesAsync(ct); return Map(task);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await access.DemandTaskOwnerAsync(id, ct);
        var task = await db.ProjectTasks.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new EntityNotFoundException(typeof(ProjectTask), id);
        if (await db.ProjectTasks.AnyAsync(x => x.ParentTaskId == id, ct)) throw new BusinessException("Work:TaskHasChildren");
        var assignments = await db.ProjectTaskAssignments.Where(x => x.ProjectTaskId == id).ToListAsync(ct);
        var documents = await db.ProjectTaskDocuments.Where(x => x.ProjectTaskId == id).ToListAsync(ct);
        db.ProjectTaskAssignments.RemoveRange(assignments); db.ProjectTaskDocuments.RemoveRange(documents);
        await WorkCalendarLinker.DeleteRelatedAsync(db, WorkCalendarSync.TaskRelatedType, id, ct);
        db.ProjectTasks.Remove(task);
        AddEvent(task, "Deleted", assignments.Select(x => x.UserId).Distinct().ToList());
        await AddAccessEvent(task, true, [], ct); await db.SaveChangesAsync(ct);
    }

    public async Task<TaskAssignmentDto> AddAssignmentAsync(Guid taskId, AddTaskAssignmentDto input, CancellationToken ct)
    {
        await access.DemandTaskOwnerAsync(taskId, ct);
        var task = await db.ProjectTasks.SingleOrDefaultAsync(x => x.Id == taskId, ct)
            ?? throw new EntityNotFoundException(typeof(ProjectTask), taskId);
        if (await db.ProjectTaskAssignments.AnyAsync(x => x.ProjectTaskId == taskId && x.UserId == input.UserId && x.AssignmentType == input.AssignmentType, ct))
            throw new BusinessException("Work:DuplicateTaskAssignment");
        var assignment = new ProjectTaskAssignment(Guid.NewGuid(), taskId, input.UserId, input.AssignmentType);
        db.ProjectTaskAssignments.Add(assignment); AddEvent(task, "AssignmentChanged", [input.UserId]);
        var users = await db.ProjectTaskAssignments.Where(x => x.ProjectTaskId == taskId).Select(x => x.UserId).ToListAsync(ct);
        users.Add(input.UserId);
        await WorkCalendarLinker.ReplaceParticipantsAsync(db, WorkCalendarSync.TaskRelatedType, taskId, users, ct);
        await AddAccessEvent(task, false, [input.UserId], ct); await db.SaveChangesAsync(ct);
        return new(assignment.Id, assignment.ProjectTaskId, assignment.UserId, assignment.AssignmentType);
    }

    public async Task<TaskDocumentReferenceDto> AddDocumentAsync(Guid taskId, AddTaskDocumentReferenceDto input, CancellationToken ct)
    {
        await access.DemandTaskMemberAsync(taskId, ct);
        if (!await db.ProjectTasks.AnyAsync(x => x.Id == taskId, ct)) throw new EntityNotFoundException(typeof(ProjectTask), taskId);
        if (await db.ProjectTaskDocuments.AnyAsync(x => x.ProjectTaskId == taskId && x.DocumentId == input.DocumentId, ct))
            throw new BusinessException("Work:DuplicateTaskDocument");
        var reference = new ProjectTaskDocument(Guid.NewGuid(), taskId, input.DocumentId, input.DocumentCode);
        db.ProjectTaskDocuments.Add(reference); await db.SaveChangesAsync(ct);
        return new(reference.Id, reference.ProjectTaskId, reference.DocumentId, reference.DocumentCode);
    }

    public async Task RemoveAssignmentAsync(Guid taskId, Guid assignmentId, CancellationToken ct)
    {
        await access.DemandTaskOwnerAsync(taskId, ct);
        var assignment = await db.ProjectTaskAssignments.SingleOrDefaultAsync(x => x.Id == assignmentId && x.ProjectTaskId == taskId, ct)
            ?? throw new EntityNotFoundException(typeof(ProjectTaskAssignment), assignmentId);
        var task = await db.ProjectTasks.SingleAsync(x => x.Id == taskId, ct);
        db.ProjectTaskAssignments.Remove(assignment);
        AddEvent(task, "AssignmentChanged", [assignment.UserId]);
        var users = await db.ProjectTaskAssignments.Where(x => x.ProjectTaskId == taskId && x.Id != assignmentId)
            .Select(x => x.UserId).ToListAsync(ct);
        await WorkCalendarLinker.ReplaceParticipantsAsync(db, WorkCalendarSync.TaskRelatedType, taskId, users, ct);
        await AddAccessEvent(task, false, [], ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveDocumentAsync(Guid taskId, Guid referenceId, CancellationToken ct)
    {
        await access.DemandTaskMemberAsync(taskId, ct);
        var reference = await db.ProjectTaskDocuments.SingleOrDefaultAsync(x => x.Id == referenceId && x.ProjectTaskId == taskId, ct)
            ?? throw new EntityNotFoundException(typeof(ProjectTaskDocument), referenceId);
        db.ProjectTaskDocuments.Remove(reference);
        await db.SaveChangesAsync(ct);
    }

    private void AddEvent(ProjectTask task, string change, IReadOnlyList<Guid> users) => db.OutboxMessages.Add(
        WorkOutbox.CreateCanonical(new ProjectTaskChangedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, Correlation(), task.ProjectId,
            task.Id, change, task.Status, users.Count == 0 ? null : users[0], task.Title), Correlation()));
    private async Task AddAccessEvent(ProjectTask task, bool deleted, IReadOnlyCollection<Guid> additionalUsers, CancellationToken ct)
    {
        var users = await db.ProjectMembers.Where(x => x.ProjectId == task.ProjectId && x.IsActive).Select(x => x.UserId).ToListAsync(ct);
        users.Add(await OwnerUserId(task.ProjectId, ct));
        users.AddRange(await db.ProjectTaskAssignments.Where(x => x.ProjectTaskId == task.Id).Select(x => x.UserId).ToListAsync(ct));
        users.AddRange(additionalUsers);
        db.OutboxMessages.Add(WorkOutbox.CreateCanonical(new WorkSubjectAccessChangedEto(Guid.NewGuid(), DateTimeOffset.UtcNow,
            Correlation(), "Task", task.ProjectId, task.Id, deleted, users.Distinct().ToArray()), Correlation()));
    }
    private Task<Guid> OwnerUserId(Guid projectId, CancellationToken ct) =>
        db.Projects.Where(x => x.Id == projectId).Select(x => x.OwnerUserId).SingleAsync(ct);
    private static ProjectTaskDto Map(ProjectTask x) => new(x.Id, x.ProjectId, x.ParentTaskId, x.Code, x.Title,
        x.Description, x.StartDate, x.DueDate, x.Priority, x.Status, x.ProgressPercent);
    private static string Correlation() => System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
}

public sealed class CalendarAppService(WorkManagementDbContext db, WorkRecordAuthorization access) : ITransientDependency
{
    public async Task<List<CalendarEventDto>> GetListAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var me = access.UserId;
        var query = db.CalendarEvents.AsNoTracking().Where(x => access.IsAdministrator || x.Visibility == "Public" ||
            x.OwnerUserId == me || (x.Visibility == "Participants" &&
                db.CalendarEventParticipants.Any(p => p.CalendarEventId == x.Id && p.UserId == me)));
        if (from.HasValue) query = query.Where(x => x.EndTime >= from);
        if (to.HasValue) query = query.Where(x => x.StartTime <= to);
        var items = await query.OrderBy(x => x.StartTime).ToListAsync(ct);
        var ids = items.Select(x => x.Id).ToArray();
        var participants = await db.CalendarEventParticipants.AsNoTracking().Where(x => ids.Contains(x.CalendarEventId)).ToListAsync(ct);
        return items.Select(x => Map(x, participants.Where(p => p.CalendarEventId == x.Id).Select(p => p.UserId).ToList())).ToList();
    }

    public async Task<CalendarEventDto> GetAsync(Guid id, CancellationToken ct)
    {
        var item = await db.CalendarEvents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new EntityNotFoundException(typeof(CalendarEvent), id);
        EnsureCanView(item);
        var people = await db.CalendarEventParticipants.AsNoTracking().Where(x => x.CalendarEventId == id)
            .Select(x => x.UserId).ToListAsync(ct);
        return Map(item, people);
    }

    public async Task<CalendarEventDto> CreateAsync(CreateCalendarEventDto input, CancellationToken ct)
    {
        await EnsureSyncedRelatedAvailableAsync(input.EventType, input.RelatedType, input.RelatedId, null, ct);
        var item = new CalendarEvent(Guid.NewGuid(), input.Title, input.Description, input.StartTime, input.EndTime,
            input.AllDay, input.EventType, input.Location, input.RelatedType, input.RelatedId, input.Visibility, access.UserId);
        var users = (input.ParticipantUserIds ?? []).Distinct().Take(501).ToList();
        if (users.Count > 500) throw new BusinessException("Work:TooManyCalendarParticipants");
        db.CalendarEvents.Add(item);
        db.CalendarEventParticipants.AddRange(users.Select(x => new CalendarEventParticipant(Guid.NewGuid(), item.Id, x)));
        db.OutboxMessages.Add(WorkOutbox.Create(new CalendarEventChangedEto(Guid.NewGuid(), DateTime.UtcNow, item.Id, "Created", users), Correlation()));
        await db.SaveChangesAsync(ct); return Map(item, users);
    }

    public async Task<CalendarEventDto> UpdateAsync(Guid id, UpdateCalendarEventDto input, CancellationToken ct)
    {
        await DemandCalendarOwnerAsync(id, ct);
        await EnsureSyncedRelatedAvailableAsync(input.EventType, input.RelatedType, input.RelatedId, id, ct);
        var item = await db.CalendarEvents.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new EntityNotFoundException(typeof(CalendarEvent), id);
        item.Change(input.Title, input.Description, input.StartTime, input.EndTime, input.AllDay, input.EventType,
            input.Location, input.RelatedType, input.RelatedId, input.Visibility);
        var existing = await db.CalendarEventParticipants.Where(x => x.CalendarEventId == id).ToListAsync(ct);
        db.CalendarEventParticipants.RemoveRange(existing);
        var users = (input.ParticipantUserIds ?? []).Distinct().Take(501).ToList();
        if (users.Count > 500) throw new BusinessException("Work:TooManyCalendarParticipants");
        db.CalendarEventParticipants.AddRange(users.Select(x => new CalendarEventParticipant(Guid.NewGuid(), item.Id, x)));
        db.OutboxMessages.Add(WorkOutbox.Create(new CalendarEventChangedEto(Guid.NewGuid(), DateTime.UtcNow, item.Id, "Updated", users), Correlation()));
        await db.SaveChangesAsync(ct);
        return Map(item, users);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await DemandCalendarOwnerAsync(id, ct);
        var item = await db.CalendarEvents.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new EntityNotFoundException(typeof(CalendarEvent), id);
        var people = await db.CalendarEventParticipants.Where(x => x.CalendarEventId == id).ToListAsync(ct);
        db.CalendarEventParticipants.RemoveRange(people); db.CalendarEvents.Remove(item);
        db.OutboxMessages.Add(WorkOutbox.Create(new CalendarEventChangedEto(Guid.NewGuid(), DateTime.UtcNow, id, "Deleted", people.Select(x => x.UserId).ToList()), Correlation()));
        await db.SaveChangesAsync(ct);
    }
    private async Task EnsureSyncedRelatedAvailableAsync(string eventType, string relatedType, string? relatedId, Guid? exceptId, CancellationToken ct)
    {
        if (!WorkCalendarSync.IsSyncedEventType(eventType) || string.IsNullOrWhiteSpace(relatedId)) return;
        var taken = await db.CalendarEvents.AnyAsync(x =>
            x.EventType == eventType && x.RelatedType == relatedType && x.RelatedId == relatedId &&
            (!exceptId.HasValue || x.Id != exceptId), ct);
        if (taken) throw new BusinessException("Work:DuplicateRelatedCalendar");
    }

    private async Task DemandCalendarOwnerAsync(Guid id, CancellationToken ct)
    {
        if (!access.IsAdministrator && !await db.CalendarEvents.AnyAsync(x => x.Id == id && x.OwnerUserId == access.UserId, ct))
            throw new Volo.Abp.Authorization.AbpAuthorizationException("Calendar owner required.");
    }
    private void EnsureCanView(CalendarEvent item)
    {
        var me = access.UserId;
        if (access.IsAdministrator || item.Visibility == "Public" || item.OwnerUserId == me) return;
        if (item.Visibility == "Participants" &&
            db.CalendarEventParticipants.Any(p => p.CalendarEventId == item.Id && p.UserId == me)) return;
        throw new Volo.Abp.Authorization.AbpAuthorizationException("Calendar visibility denied.");
    }
    private static CalendarEventDto Map(CalendarEvent x, IReadOnlyList<Guid> people) => new(x.Id, x.Title, x.Description,
        x.StartTime, x.EndTime, x.AllDay, x.EventType, x.Location, x.RelatedType, x.RelatedId, x.Visibility, people);
    private static string Correlation() => System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
}

public sealed class SurveyAppService(WorkManagementDbContext db, WorkRecordAuthorization access) : ITransientDependency
{
    public async Task<List<SurveyCriteriaDto>> GetCriteriaAsync(CancellationToken ct) => await db.SurveyCriteria.AsNoTracking()
        .OrderBy(x => x.SortOrder).Select(x => new SurveyCriteriaDto(x.Id, x.Code, x.Name, x.SortOrder, x.IsActive, x.LocationId, x.Image)).ToListAsync(ct);
    public async Task<SurveyCriteriaDto> CreateCriteriaAsync(CreateSurveyCriteriaDto input, CancellationToken ct)
    {
        if (await db.SurveyCriteria.AnyAsync(x => x.Code == input.Code, ct)) throw new BusinessException("Work:DuplicateSurveyCriteria");
        var x = new SurveyCriteria(Guid.NewGuid(), input.Code, input.Name, input.SortOrder, input.LocationId, input.Image); db.SurveyCriteria.Add(x); await db.SaveChangesAsync(ct);
        return new(x.Id, x.Code, x.Name, x.SortOrder, x.IsActive, x.LocationId, x.Image);
    }
    public async Task<SurveyCriteriaDto> UpdateCriteriaAsync(Guid id, UpdateSurveyCriteriaDto input, CancellationToken ct)
    {
        var x = await db.SurveyCriteria.SingleOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new EntityNotFoundException(typeof(SurveyCriteria), id);
        x.Change(input.Name, input.SortOrder, input.IsActive, input.LocationId, input.Image);
        await db.SaveChangesAsync(ct);
        return new(x.Id, x.Code, x.Name, x.SortOrder, x.IsActive, x.LocationId, x.Image);
    }
    public async Task DeleteCriteriaAsync(Guid id, CancellationToken ct)
    {
        if (await db.SurveyResults.AnyAsync(x => x.CriteriaId == id, ct)) throw new BusinessException("Work:SurveyCriteriaInUse");
        var x = await db.SurveyCriteria.SingleOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new EntityNotFoundException(typeof(SurveyCriteria), id);
        db.SurveyCriteria.Remove(x);
        await db.SaveChangesAsync(ct);
    }
    public async Task<List<SurveyLocationDto>> GetLocationsAsync(CancellationToken ct) => await db.SurveyLocations.AsNoTracking()
        .OrderBy(x => x.Code).Select(x => new SurveyLocationDto(x.Id, x.Code, x.Name, x.OrganizationUnitId, x.IsActive, x.Description)).ToListAsync(ct);
    public async Task<SurveyLocationDto> CreateLocationAsync(CreateSurveyLocationDto input, CancellationToken ct)
    {
        if (await db.SurveyLocations.AnyAsync(x => x.Code == input.Code, ct)) throw new BusinessException("Work:DuplicateSurveyLocation");
        var x = new SurveyLocation(Guid.NewGuid(), input.Code, input.Name, input.OrganizationUnitId, input.Description); db.SurveyLocations.Add(x); await db.SaveChangesAsync(ct);
        return new(x.Id, x.Code, x.Name, x.OrganizationUnitId, x.IsActive, x.Description);
    }
    public async Task<SurveyLocationDto> UpdateLocationAsync(Guid id, UpdateSurveyLocationDto input, CancellationToken ct)
    {
        var x = await db.SurveyLocations.SingleOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new EntityNotFoundException(typeof(SurveyLocation), id);
        x.Change(input.Name, input.OrganizationUnitId, input.IsActive, input.Description);
        await db.SaveChangesAsync(ct);
        return new(x.Id, x.Code, x.Name, x.OrganizationUnitId, x.IsActive, x.Description);
    }
    public async Task DeleteLocationAsync(Guid id, CancellationToken ct)
    {
        if (await db.SurveySessions.AnyAsync(x => x.LocationId == id, ct)) throw new BusinessException("Work:SurveyLocationInUse");
        var x = await db.SurveyLocations.SingleOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new EntityNotFoundException(typeof(SurveyLocation), id);
        db.SurveyLocations.Remove(x);
        await db.SaveChangesAsync(ct);
    }
    public async Task<List<SurveySessionDto>> GetSessionsAsync(Guid? locationId, CancellationToken ct)
    {
        var query = db.SurveySessions.AsNoTracking().AsQueryable();
        if (locationId.HasValue) query = query.Where(x => x.LocationId == locationId);
        var sessions = await query.OrderByDescending(x => x.StartsAt).ToListAsync(ct);
        return sessions.Select(MapSession).ToList();
    }
    public async Task<SurveySessionDto> CreateSessionAsync(CreateSurveySessionDto input, CancellationToken ct)
    {
        if (await db.SurveySessions.AnyAsync(x => x.Code == input.Code, ct)) throw new BusinessException("Work:DuplicateSurveySession");
        if (input.LocationId.HasValue && !await db.SurveyLocations.AnyAsync(x => x.Id == input.LocationId, ct))
            throw new EntityNotFoundException(typeof(SurveyLocation), input.LocationId);
        var x = new SurveySession(Guid.NewGuid(), input.Code, input.Name, input.StartsAt, input.EndsAt, input.LocationId, access.UserId);
        db.SurveySessions.Add(x); db.OutboxMessages.Add(WorkOutbox.Create(new SurveySessionChangedEto(Guid.NewGuid(), DateTime.UtcNow, x.Id, "Created", x.Status), Correlation()));
        await db.SaveChangesAsync(ct); return MapSession(x);
    }
    public async Task<SurveySessionDto> UpdateSessionAsync(Guid id, UpdateSurveySessionDto input, CancellationToken ct)
    {
        await access.DemandSurveyOwnerAsync(id, ct);
        var x = await db.SurveySessions.SingleOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new EntityNotFoundException(typeof(SurveySession), id);
        if (input.LocationId.HasValue && !await db.SurveyLocations.AnyAsync(l => l.Id == input.LocationId, ct))
            throw new EntityNotFoundException(typeof(SurveyLocation), input.LocationId);
        x.Change(input.Name, input.StartsAt, input.EndsAt, input.LocationId);
        db.OutboxMessages.Add(WorkOutbox.Create(new SurveySessionChangedEto(Guid.NewGuid(), DateTime.UtcNow, x.Id, "Updated", x.Status), Correlation()));
        await db.SaveChangesAsync(ct);
        return MapSession(x);
    }
    public async Task<SurveySessionDto> ChangeSessionStatusAsync(Guid id, ChangeSurveySessionStatusDto input, CancellationToken ct)
    {
        await access.DemandSurveyOwnerAsync(id, ct);
        var x = await db.SurveySessions.SingleOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new EntityNotFoundException(typeof(SurveySession), id);
        x.ChangeStatus(input.Status);
        db.OutboxMessages.Add(WorkOutbox.Create(new SurveySessionChangedEto(Guid.NewGuid(), DateTime.UtcNow, x.Id, "StatusChanged", x.Status), Correlation()));
        await db.SaveChangesAsync(ct);
        return MapSession(x);
    }
    public async Task DeleteSessionAsync(Guid id, CancellationToken ct)
    {
        await access.DemandSurveyOwnerAsync(id, ct);
        var x = await db.SurveySessions.SingleOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new EntityNotFoundException(typeof(SurveySession), id);
        if (await db.SurveyResults.AnyAsync(r => r.SessionId == id, ct)) throw new BusinessException("Work:SurveySessionHasResults");
        var files = await db.SurveyFiles.Where(f => f.SessionId == id).ToListAsync(ct);
        db.SurveyFiles.RemoveRange(files);
        db.SurveySessions.Remove(x);
        db.OutboxMessages.Add(WorkOutbox.Create(new SurveySessionChangedEto(Guid.NewGuid(), DateTime.UtcNow, id, "Deleted", x.Status), Correlation()));
        await db.SaveChangesAsync(ct);
    }
    public async Task<List<SurveyFileReferenceDto>> GetSessionFilesAsync(Guid sessionId, CancellationToken ct)
    {
        await access.DemandSurveyOwnerAsync(sessionId, ct);
        return await db.SurveyFiles.AsNoTracking().Where(x => x.SessionId == sessionId)
            .Select(x => new SurveyFileReferenceDto(x.Id, x.SessionId, x.FileName, x.ContentType, x.Size))
            .ToListAsync(ct);
    }
    public async Task<List<SurveyResultDto>> GetResultsAsync(Guid sessionId, CancellationToken ct)
    {
        await access.DemandSurveyOwnerAsync(sessionId, ct);
        return await db.SurveyResults.AsNoTracking().Where(x => x.SessionId == sessionId)
            .OrderByDescending(x => x.CreationTime)
            .Select(x => new SurveyResultDto(x.Id, x.SessionId, x.CriteriaId, x.RespondentUserId, x.Score, x.Comment))
            .ToListAsync(ct);
    }
    public async Task<SurveyResultDto> SubmitAsync(Guid sessionId, SubmitSurveyResultDto input, CancellationToken ct)
    {
        await access.DemandSurveyAudienceAsync(sessionId, DateTime.UtcNow, ct);
        if (!await db.SurveySessions.AnyAsync(x => x.Id == sessionId, ct)) throw new EntityNotFoundException(typeof(SurveySession), sessionId);
        if (!await db.SurveyCriteria.AnyAsync(x => x.Id == input.CriteriaId && x.IsActive, ct)) throw new EntityNotFoundException(typeof(SurveyCriteria), input.CriteriaId);
        var x = new SurveyResult(Guid.NewGuid(), sessionId, input.CriteriaId,
            SurveySubmissionIdentity.Resolve(access.UserId, input.RespondentUserId), input.Score, input.Comment);
        db.SurveyResults.Add(x); await db.SaveChangesAsync(ct);
        return new(x.Id, x.SessionId, x.CriteriaId, x.RespondentUserId, x.Score, x.Comment);
    }

    public async Task<SurveyLocationDto> GetPublicLocationAsync(Guid locationId, CancellationToken ct)
    {
        var location = await db.SurveyLocations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == locationId && x.IsActive, ct)
            ?? throw new EntityNotFoundException(typeof(SurveyLocation), locationId);
        return MapLocation(location);
    }

    public async Task<List<SurveyCriteriaDto>> GetPublicCriteriaAsync(Guid locationId, CancellationToken ct)
    {
        _ = await GetPublicLocationAsync(locationId, ct);
        return await db.SurveyCriteria.AsNoTracking()
            .Where(x => x.IsActive && (x.LocationId == null || x.LocationId == locationId))
            .OrderBy(x => x.SortOrder)
            .Select(x => new SurveyCriteriaDto(x.Id, x.Code, x.Name, x.SortOrder, x.IsActive, x.LocationId, x.Image))
            .ToListAsync(ct);
    }

    public async Task<SurveySessionDto> CreatePublicSessionAsync(CreatePublicSurveySessionDto input, CancellationToken ct)
    {
        var fullName = Check.NotNullOrWhiteSpace(input.FullName, nameof(input.FullName), WorkConsts.NameLength).Trim();
        var phoneNumber = Check.NotNullOrWhiteSpace(input.PhoneNumber, nameof(input.PhoneNumber), 64).Trim();
        var location = await db.SurveyLocations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == input.LocationId && x.IsActive, ct)
            ?? throw new EntityNotFoundException(typeof(SurveyLocation), input.LocationId);
        var surveyTime = WorkTimestamps.ToUtc(input.SurveyTime);
        var code = $"PUBLIC-{Guid.NewGuid():N}";
        var sessionDisplay = string.IsNullOrWhiteSpace(input.SessionDisplay)
            ? $"{fullName}_{phoneNumber}_{input.LocationId}_{surveyTime:ddMMyyyyHHmm}"
            : input.SessionDisplay.Trim();
        var session = SurveySession.CreatePublic(Guid.NewGuid(), code, location.Name,
            surveyTime.AddMinutes(-1), surveyTime.AddDays(1), input.LocationId,
            fullName, phoneNumber, input.PatientCode?.Trim(), surveyTime,
            input.DeviceType?.Trim(), input.Note?.Trim(), sessionDisplay);
        db.SurveySessions.Add(session);
        db.OutboxMessages.Add(WorkOutbox.Create(new SurveySessionChangedEto(Guid.NewGuid(), DateTime.UtcNow,
            session.Id, "Created", session.Status), Correlation()));
        await db.SaveChangesAsync(ct);
        return MapSession(session);
    }

    public async Task<List<SurveyResultDto>> SubmitPublicResultsAsync(Guid sessionId,
        IReadOnlyList<SubmitSurveyResultDto> inputs, CancellationToken ct)
    {
        if (inputs.Count == 0) throw new BusinessException("Work:SurveyResultsRequired");
        var session = await db.SurveySessions.SingleOrDefaultAsync(x => x.Id == sessionId && x.IsPublic, ct)
            ?? throw new EntityNotFoundException(typeof(SurveySession), sessionId);
        if (session.StartsAt > DateTime.UtcNow || session.EndsAt < DateTime.UtcNow)
            throw new BusinessException("Work:SurveyOutsideActiveWindow");
        var criteriaIds = inputs.Select(x => x.CriteriaId).Distinct().ToList();
        var criteria = await db.SurveyCriteria.Where(x => criteriaIds.Contains(x.Id) && x.IsActive &&
                (x.LocationId == null || x.LocationId == session.LocationId)).ToListAsync(ct);
        if (criteria.Count != criteriaIds.Count) throw new BusinessException("Work:InvalidSurveyCriteria");
        var results = new List<SurveyResultDto>();
        foreach (var input in inputs)
        {
            var result = await db.SurveyResults.SingleOrDefaultAsync(x => x.SessionId == sessionId &&
                x.CriteriaId == input.CriteriaId, ct);
            if (result is null)
            {
                result = new SurveyResult(Guid.NewGuid(), sessionId, input.CriteriaId, null, input.Score, input.Comment);
                db.SurveyResults.Add(result);
            }
            else result.Change(input.Score, input.Comment);
            results.Add(new(result.Id, result.SessionId, result.CriteriaId, result.RespondentUserId, result.Score, result.Comment));
        }
        await db.SaveChangesAsync(ct);
        return results;
    }

    public async Task<SurveyResultStatisticsDto> GetStatisticsAsync(Guid? locationId, CancellationToken ct)
    {
        var query = from result in db.SurveyResults.AsNoTracking()
                    join session in db.SurveySessions.AsNoTracking() on result.SessionId equals session.Id
                    join criteria in db.SurveyCriteria.AsNoTracking() on result.CriteriaId equals criteria.Id
                    where !locationId.HasValue || session.LocationId == locationId
                    select new { result.Score, CriteriaId = criteria.Id, CriteriaName = criteria.Name, CriteriaCode = criteria.Code };
        var rows = await query.ToListAsync(ct);
        var distribution = Enumerable.Range(0, 6).ToDictionary(star => star, _ => 0);
        foreach (var row in rows)
        {
            var star = row.Score <= 0 ? 0 : Math.Clamp((int)Math.Round(row.Score / 20m, MidpointRounding.AwayFromZero), 1, 5);
            distribution[star]++;
        }
        var averages = rows.GroupBy(x => new { x.CriteriaId, x.CriteriaName, x.CriteriaCode })
            .Select(x => new
            {
                Name = x.Key.CriteriaName,
                Code = x.Key.CriteriaCode,
                Average = x.Average(y => y.Score)
            })
            .GroupBy(x => x.Name)
            .SelectMany(group => group.Select(item => new
            {
                Name = group.Count() > 1 && !string.IsNullOrWhiteSpace(item.Code)
                    ? $"{item.Name} ({item.Code})"
                    : item.Name,
                item.Average
            }))
            .ToDictionary(x => x.Name, x => Math.Round(x.Average, 1));
        return new(rows.Count, distribution, averages);
    }

    public async Task<PagedWorkDto<SurveyResultSessionSummaryDto>> GetResultSummariesAsync(Guid? locationId,
        int skip, int take, CancellationToken ct)
    {
        skip = Math.Max(skip, 0); take = Math.Clamp(take, 1, 100);
        var query = from result in db.SurveyResults.AsNoTracking()
                    join session in db.SurveySessions.AsNoTracking() on result.SessionId equals session.Id
                    join criteria in db.SurveyCriteria.AsNoTracking() on result.CriteriaId equals criteria.Id
                    where !locationId.HasValue || session.LocationId == locationId
                    orderby session.SurveyTime descending, criteria.Name
                    select new SurveyResultSessionSummaryDto(result.Id, session.Id, criteria.Id, criteria.Name,
                        result.Score, session.FullName, session.PhoneNumber, session.PatientCode,
                        session.Note, session.SurveyTime ?? session.StartsAt);
        return new(await query.LongCountAsync(ct), await query.Skip(skip).Take(take).ToListAsync(ct));
    }

    public async Task<List<SurveyResultSessionDetailDto>> GetResultDetailsAsync(Guid sessionId, Guid? locationId,
        CancellationToken ct)
    {
        var validSession = await db.SurveySessions.AnyAsync(x => x.Id == sessionId &&
            (!locationId.HasValue || x.LocationId == locationId), ct);
        if (!validSession) throw new EntityNotFoundException(typeof(SurveySession), sessionId);
        return await (from result in db.SurveyResults.AsNoTracking()
                      join criteria in db.SurveyCriteria.AsNoTracking() on result.CriteriaId equals criteria.Id
                      where result.SessionId == sessionId
                      orderby criteria.Name
                      select new SurveyResultSessionDetailDto(result.Id, result.SessionId, result.CriteriaId,
                          criteria.Name, result.Score, result.Comment)).ToListAsync(ct);
    }

    private static SurveySessionDto MapSession(SurveySession x) => new(x.Id, x.Code, x.Name, x.StartsAt, x.EndsAt,
        x.Status, x.LocationId, x.FullName, x.PhoneNumber, x.PatientCode, x.SurveyTime, x.DeviceType, x.Note, x.SessionDisplay);
    private static SurveyLocationDto MapLocation(SurveyLocation x) =>
        new(x.Id, x.Code, x.Name, x.OrganizationUnitId, x.IsActive, x.Description);
    private static string Correlation() => System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
}

public sealed class ReadModelAppService(WorkManagementDbContext db) : ITransientDependency
{
    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return new(await db.Projects.CountAsync(x => x.Status != "Completed" && x.Status != "Cancelled", ct),
            await db.ProjectTasks.CountAsync(x => x.Status != "Completed" && x.Status != "Cancelled", ct),
            await db.ProjectTasks.CountAsync(x => x.DueDate < now && x.Status != "Completed" && x.Status != "Cancelled", ct),
            await db.SurveySessions.CountAsync(x => x.Status == "Active", ct), now);
    }
    public Task<List<ReportRowDto>> GetReportAsync(string? dimension, CancellationToken ct)
    {
        var query = db.ReportReadModels.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(dimension)) query = query.Where(x => x.Dimension == dimension);
        return query.OrderBy(x => x.Dimension).ThenBy(x => x.Key)
            .Select(x => new ReportRowDto(x.Dimension, x.Key, x.Label, x.Value, x.RefreshedAt)).ToListAsync(ct);
    }
}
