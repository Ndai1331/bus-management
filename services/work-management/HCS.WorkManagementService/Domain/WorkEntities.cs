using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;

namespace HCS.WorkManagementService.Domain;

public static class WorkConsts
{
    public const int CodeLength = 64;
    public const int NameLength = 256;
    public const int StatusLength = 32;
    public const int TypeLength = 64;
    public const string CompletedStatus = "Completed";
}

public sealed class Project : FullAuditedAggregateRoot<Guid>
{
    private Project() { }
    public Project(Guid id, string code, string name, DateTime startDate, DateTime endDate, string status,
        Guid? ownerDepartmentId, Guid ownerUserId, string? description = null) : base(id)
    {
        Code = Check.NotNullOrWhiteSpace(code, nameof(code), WorkConsts.CodeLength);
        OwnerDepartmentId = ownerDepartmentId;
        OwnerUserId = ownerUserId == Guid.Empty ? throw new BusinessException("Work:OwnerRequired") : ownerUserId;
        Change(name, description, startDate, endDate, status);
    }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public Guid? OwnerDepartmentId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public void Change(string name, string? description, DateTime startDate, DateTime endDate, string status)
    {
        startDate = WorkTimestamps.ToUtc(startDate);
        endDate = WorkTimestamps.ToUtc(endDate);
        if (endDate < startDate) throw new BusinessException("Work:ProjectDateRange");
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), WorkConsts.NameLength);
        Description = description;
        StartDate = startDate;
        EndDate = endDate;
        Status = Check.NotNullOrWhiteSpace(status, nameof(status), WorkConsts.StatusLength);
    }
}

public sealed class ProjectMember : Entity<Guid>
{
    private ProjectMember() { }
    public ProjectMember(Guid id, Guid projectId, Guid userId, string role) : base(id)
        => (ProjectId, UserId, Role, IsActive) = (projectId, userId,
            Check.NotNullOrWhiteSpace(role, nameof(role), WorkConsts.TypeLength), true);
    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public void Deactivate() => IsActive = false;
}

public sealed class ProjectTask : FullAuditedAggregateRoot<Guid>
{
    private ProjectTask() { }
    public ProjectTask(Guid id, Guid projectId, Guid? parentTaskId, string code, string title, string? description,
        DateTime startDate, DateTime dueDate, string priority, string status, int progressPercent) : base(id)
    {
        ProjectId = projectId;
        ParentTaskId = parentTaskId;
        Code = Check.NotNullOrWhiteSpace(code, nameof(code), WorkConsts.CodeLength);
        Change(title, description, startDate, dueDate, priority, status, progressPercent);
    }
    public Guid ProjectId { get; private set; }
    public Guid? ParentTaskId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime DueDate { get; private set; }
    public string Priority { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public int ProgressPercent { get; private set; }
    public void Change(string title, string? description, DateTime startDate, DateTime dueDate, string priority,
        string status, int progressPercent)
    {
        startDate = WorkTimestamps.ToUtc(startDate);
        dueDate = WorkTimestamps.ToUtc(dueDate);
        if (dueDate < startDate) throw new BusinessException("Work:TaskDateRange");
        if (progressPercent is < 0 or > 100) throw new BusinessException("Work:TaskProgressRange");
        if (progressPercent == 100 || string.Equals(status, WorkConsts.CompletedStatus, StringComparison.OrdinalIgnoreCase))
        {
            progressPercent = 100;
            status = WorkConsts.CompletedStatus;
        }
        Title = Check.NotNullOrWhiteSpace(title, nameof(title), WorkConsts.NameLength);
        Description = description;
        StartDate = startDate;
        DueDate = dueDate;
        Priority = Check.NotNullOrWhiteSpace(priority, nameof(priority), WorkConsts.StatusLength);
        Status = Check.NotNullOrWhiteSpace(status, nameof(status), WorkConsts.StatusLength);
        ProgressPercent = progressPercent;
    }
}

public sealed class ProjectTaskAssignment : Entity<Guid>
{
    private ProjectTaskAssignment() { }
    public ProjectTaskAssignment(Guid id, Guid projectTaskId, Guid userId, string assignmentType) : base(id)
        => (ProjectTaskId, UserId, AssignmentType) = (projectTaskId, userId,
            Check.NotNullOrWhiteSpace(assignmentType, nameof(assignmentType), WorkConsts.TypeLength));
    public Guid ProjectTaskId { get; private set; }
    public Guid UserId { get; private set; }
    public string AssignmentType { get; private set; } = string.Empty;
}

public sealed class ProjectTaskDocument : Entity<Guid>
{
    private ProjectTaskDocument() { }
    public ProjectTaskDocument(Guid id, Guid taskId, Guid documentId, string? documentCode) : base(id)
        => (ProjectTaskId, DocumentId, DocumentCode) = (taskId, documentId, documentCode);
    public Guid ProjectTaskId { get; private set; }
    public Guid DocumentId { get; private set; }
    public string? DocumentCode { get; private set; }
}

public sealed class CalendarEvent : FullAuditedAggregateRoot<Guid>
{
    private CalendarEvent() { }
    public CalendarEvent(Guid id, string title, string? description, DateTime startTime, DateTime endTime,
        bool allDay, string eventType, string? location, string relatedType, string? relatedId, string visibility,
        Guid ownerUserId) : base(id)
    {
        startTime = WorkTimestamps.ToUtc(startTime);
        endTime = WorkTimestamps.ToUtc(endTime);
        if (endTime < startTime) throw new BusinessException("Work:CalendarDateRange");
        Title = Check.NotNullOrWhiteSpace(title, nameof(title), WorkConsts.NameLength);
        Description = description; StartTime = startTime; EndTime = endTime; AllDay = allDay;
        EventType = Check.NotNullOrWhiteSpace(eventType, nameof(eventType), WorkConsts.TypeLength);
        Location = location; RelatedType = relatedType; RelatedId = relatedId;
        Visibility = Check.NotNullOrWhiteSpace(visibility, nameof(visibility), WorkConsts.StatusLength);
        OwnerUserId = ownerUserId == Guid.Empty ? throw new BusinessException("Work:OwnerRequired") : ownerUserId;
    }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime StartTime { get; private set; }
    public DateTime EndTime { get; private set; }
    public bool AllDay { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string? Location { get; private set; }
    public string RelatedType { get; private set; } = string.Empty;
    public string? RelatedId { get; private set; }
    public string Visibility { get; private set; } = string.Empty;
    public Guid OwnerUserId { get; private set; }
    public void Change(string title, string? description, DateTime startTime, DateTime endTime, bool allDay,
        string eventType, string? location, string relatedType, string? relatedId, string visibility)
    {
        startTime = WorkTimestamps.ToUtc(startTime);
        endTime = WorkTimestamps.ToUtc(endTime);
        if (endTime < startTime) throw new BusinessException("Work:CalendarDateRange");
        Title = Check.NotNullOrWhiteSpace(title, nameof(title), WorkConsts.NameLength);
        Description = description;
        StartTime = startTime;
        EndTime = endTime;
        AllDay = allDay;
        EventType = Check.NotNullOrWhiteSpace(eventType, nameof(eventType), WorkConsts.TypeLength);
        Location = location;
        RelatedType = relatedType;
        RelatedId = relatedId;
        Visibility = Check.NotNullOrWhiteSpace(visibility, nameof(visibility), WorkConsts.StatusLength);
    }
}

public sealed class CalendarEventParticipant : Entity<Guid>
{
    private CalendarEventParticipant() { }
    public CalendarEventParticipant(Guid id, Guid eventId, Guid userId) : base(id)
        => (CalendarEventId, UserId) = (eventId, userId);
    public Guid CalendarEventId { get; private set; }
    public Guid UserId { get; private set; }
}

public sealed class SurveyCriteria : FullAuditedAggregateRoot<Guid>
{
    private SurveyCriteria() { }
    public SurveyCriteria(Guid id, string code, string name, int sortOrder, Guid? locationId = null, string? image = null) : base(id)
    {
        Code = Check.NotNullOrWhiteSpace(code, nameof(code), WorkConsts.CodeLength);
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), WorkConsts.NameLength);
        SortOrder = sortOrder; IsActive = true; LocationId = locationId; Image = image;
    }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }
    public Guid? LocationId { get; private set; }
    public string? Image { get; private set; }
    public void Change(string name, int sortOrder, bool isActive, Guid? locationId = null, string? image = null)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), WorkConsts.NameLength);
        SortOrder = sortOrder;
        IsActive = isActive;
        LocationId = locationId;
        Image = image;
    }
}

public sealed class SurveyLocation : FullAuditedAggregateRoot<Guid>
{
    private SurveyLocation() { }
    public SurveyLocation(Guid id, string code, string name, Guid? organizationUnitId, string? description = null) : base(id)
    {
        Code = Check.NotNullOrWhiteSpace(code, nameof(code), WorkConsts.CodeLength);
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), WorkConsts.NameLength);
        OrganizationUnitId = organizationUnitId; IsActive = true; Description = description;
    }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public Guid? OrganizationUnitId { get; private set; }
    public bool IsActive { get; private set; }
    public string? Description { get; private set; }
    public void Change(string name, Guid? organizationUnitId, bool isActive, string? description = null)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), WorkConsts.NameLength);
        OrganizationUnitId = organizationUnitId;
        IsActive = isActive;
        Description = description;
    }
}

public sealed class SurveySession : FullAuditedAggregateRoot<Guid>
{
    private SurveySession() { }
    public SurveySession(Guid id, string code, string name, DateTime startsAt, DateTime endsAt, Guid? locationId,
        Guid ownerUserId) : base(id)
    {
        startsAt = WorkTimestamps.ToUtc(startsAt);
        endsAt = WorkTimestamps.ToUtc(endsAt);
        if (endsAt < startsAt) throw new BusinessException("Work:SurveyDateRange");
        Code = Check.NotNullOrWhiteSpace(code, nameof(code), WorkConsts.CodeLength);
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), WorkConsts.NameLength);
        StartsAt = startsAt; EndsAt = endsAt; LocationId = locationId; Status = "Draft";
        OwnerUserId = ownerUserId == Guid.Empty ? throw new BusinessException("Work:OwnerRequired") : ownerUserId;
    }
    private SurveySession(Guid id, string code, string name, DateTime startsAt, DateTime endsAt, Guid? locationId,
        string? fullName, string? phoneNumber, string? patientCode, DateTime surveyTime, string? deviceType, string? note,
        string sessionDisplay) : base(id)
    {
        startsAt = WorkTimestamps.ToUtc(startsAt);
        endsAt = WorkTimestamps.ToUtc(endsAt);
        if (endsAt < startsAt) throw new BusinessException("Work:SurveyDateRange");
        Code = Check.NotNullOrWhiteSpace(code, nameof(code), WorkConsts.CodeLength);
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), WorkConsts.NameLength);
        StartsAt = startsAt; EndsAt = endsAt; LocationId = locationId; Status = "Active";
        OwnerUserId = Guid.Empty; IsPublic = true; FullName = fullName; PhoneNumber = phoneNumber;
        PatientCode = patientCode; SurveyTime = WorkTimestamps.ToUtc(surveyTime); DeviceType = deviceType;
        Note = note; SessionDisplay = Check.NotNullOrWhiteSpace(sessionDisplay, nameof(sessionDisplay), WorkConsts.NameLength);
    }
    public static SurveySession CreatePublic(Guid id, string code, string name, DateTime startsAt, DateTime endsAt,
        Guid locationId, string fullName, string phoneNumber, string? patientCode, DateTime surveyTime,
        string? deviceType, string? note, string sessionDisplay) => new(id, code, name, startsAt, endsAt, locationId,
        Check.NotNullOrWhiteSpace(fullName, nameof(fullName), WorkConsts.NameLength),
        Check.NotNullOrWhiteSpace(phoneNumber, nameof(phoneNumber), WorkConsts.CodeLength), patientCode, surveyTime,
        deviceType, note, sessionDisplay);
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public DateTime StartsAt { get; private set; }
    public DateTime EndsAt { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public Guid? LocationId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public bool IsPublic { get; private set; }
    public string? FullName { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? PatientCode { get; private set; }
    public DateTime? SurveyTime { get; private set; }
    public string? DeviceType { get; private set; }
    public string? Note { get; private set; }
    public string? SessionDisplay { get; private set; }
    public void Change(string name, DateTime startsAt, DateTime endsAt, Guid? locationId)
    {
        startsAt = WorkTimestamps.ToUtc(startsAt);
        endsAt = WorkTimestamps.ToUtc(endsAt);
        if (endsAt < startsAt) throw new BusinessException("Work:SurveyDateRange");
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), WorkConsts.NameLength);
        StartsAt = startsAt;
        EndsAt = endsAt;
        LocationId = locationId;
    }
    public void ChangeStatus(string status) => Status = Check.NotNullOrWhiteSpace(status, nameof(status), WorkConsts.StatusLength);
}

public sealed class SurveyResult : FullAuditedAggregateRoot<Guid>
{
    private SurveyResult() { }
    public SurveyResult(Guid id, Guid sessionId, Guid criteriaId, Guid? respondentUserId, decimal score, string? comment) : base(id)
    {
        if (score is < 0 or > 100) throw new BusinessException("Work:SurveyScoreRange");
        SessionId = sessionId; CriteriaId = criteriaId; RespondentUserId = respondentUserId; Score = score; Comment = comment;
    }
    public Guid SessionId { get; private set; }
    public Guid CriteriaId { get; private set; }
    public Guid? RespondentUserId { get; private set; }
    public decimal Score { get; private set; }
    public string? Comment { get; private set; }
    public void Change(decimal score, string? comment)
    {
        if (score is < 0 or > 100) throw new BusinessException("Work:SurveyScoreRange");
        Score = score; Comment = comment;
    }
}

public sealed class SurveyFileReference : Entity<Guid>
{
    private SurveyFileReference() { }
    public SurveyFileReference(Guid id, Guid sessionId, Guid uploadedByUserId, string blobName, string fileName, string contentType, long size) : base(id)
        => (SessionId, UploadedByUserId, BlobName, FileName, ContentType, Size) = (sessionId, uploadedByUserId, blobName, fileName, contentType, size);
    public Guid SessionId { get; private set; }
    public string BlobName { get; private set; } = string.Empty;
    public Guid UploadedByUserId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long Size { get; private set; }
}

public sealed class DashboardMetric : Entity<Guid>
{
    private DashboardMetric() { }
    public DashboardMetric(Guid id, string key, decimal value, DateTime refreshedAt) : base(id)
        => (Key, Value, RefreshedAt) = (key, value, refreshedAt);
    public string Key { get; private set; } = string.Empty;
    public decimal Value { get; private set; }
    public DateTime RefreshedAt { get; private set; }
}

public sealed class ReportReadModel : Entity<Guid>
{
    private ReportReadModel() { }
    public ReportReadModel(Guid id, string dimension, string key, string label, decimal value, DateTime refreshedAt) : base(id)
        => (Dimension, Key, Label, Value, RefreshedAt) = (dimension, key, label, value, refreshedAt);
    public string Dimension { get; private set; } = string.Empty;
    public string Key { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public decimal Value { get; private set; }
    public DateTime RefreshedAt { get; private set; }
}
