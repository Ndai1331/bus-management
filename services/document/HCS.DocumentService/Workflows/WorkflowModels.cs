using System.Text.Json;

namespace HCS.DocumentService.Workflows;

public static class WorkflowStepTypes
{
    public const string Process = "PROCESS";
    public const string Sign = "SIGN";
    public const string View = "VIEW";

    public static string Normalize(string? type)
    {
        var value = string.IsNullOrWhiteSpace(type) ? Process : type.Trim().ToUpperInvariant();
        if (value is not (Process or Sign or View))
        {
            throw new ArgumentException("Invalid workflow step type.");
        }
        return value;
    }

    public static bool IsBlocking(string type) => Normalize(type) != View;
}

public sealed class WorkflowKind
{
    private WorkflowKind() { }
    public WorkflowKind(Guid id, string code, string name, string? description, bool isActive, DateTime now)
    {
        Id = id;
        Code = Required(code, 64);
        Name = Required(name, 256);
        Description = Optional(description, 2000);
        IsActive = isActive;
        CreationTime = now;
    }
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreationTime { get; private set; }
    public void Update(string name, string? description, bool isActive)
    {
        Name = Required(name, 256);
        Description = Optional(description, 2000);
        IsActive = isActive;
    }
    private static string Required(string value, int max)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result) || result.Length > max) throw new ArgumentException("Invalid workflow value.");
        return result;
    }
    private static string? Optional(string? value, int max)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result)) return null;
        if (result.Length > max) throw new ArgumentException("Invalid workflow value.");
        return result;
    }
}

public sealed class WorkflowDefinition
{
    private readonly List<WorkflowStep> _steps = [];
    private WorkflowDefinition() { }
    public WorkflowDefinition(Guid id, string code, string name, IEnumerable<WorkflowStepInput> steps, DateTime now,
        Guid? kindId = null, string? description = null, bool isActive = true, string? signMode = null)
    {
        Id = id;
        Code = Required(code, 64);
        Name = Required(name, 256);
        KindId = kindId;
        Description = Optional(description, 2000);
        IsActive = isActive;
        SignMode = WorkflowSignModes.Normalize(signMode);
        CreationTime = now;
        ReplaceSteps(steps);
    }
    public Guid Id { get; private set; }
    public Guid? KindId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string SignMode { get; private set; } = WorkflowSignModes.Sequential;
    public DateTime CreationTime { get; private set; }
    public IReadOnlyCollection<WorkflowStep> Steps => _steps;
    public void EnsureStartable()
    {
        if (_steps.Count == 0)
            throw new InvalidOperationException("Workflow definition has no steps. Add at least one step before starting.");
        if (!_steps.Any(x => x.IsBlocking))
            throw new InvalidOperationException("Workflow definition has no blocking step. Add at least one process or sign step before starting.");
    }
    public void Rename(string name) => Name = Required(name, 256);
    public void SetMetadata(Guid? kindId, string? description, bool isActive, string? signMode = null)
    {
        KindId = kindId;
        Description = Optional(description, 2000);
        IsActive = isActive;
        SignMode = WorkflowSignModes.Normalize(signMode);
    }
    public void ReplaceSteps(IEnumerable<WorkflowStepInput> steps)
    {
        var normalized = steps.OrderBy(x => x.Order).ToList();
        if (normalized.Count == 0)
        {
            _steps.Clear();
            return;
        }
        if (normalized.Select(x => x.Code.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalized.Count)
            throw new InvalidOperationException("Workflow step codes must be unique.");
        if (normalized.Select(x => x.Order).Distinct().Count() != normalized.Count)
            throw new InvalidOperationException("Workflow step orders must be unique.");
        _steps.Clear();
        _steps.AddRange(normalized.Select(x => new WorkflowStep(Guid.NewGuid(), Id,
            Required(x.Code, 64), Required(x.Name, 256), x.Order, DefaultPermission(x.RequiredPermission),
            WorkflowStepTypes.Normalize(x.Type), x.AssigneeUserId, x.AssigneeType, x.RoleId, x.UserIds, x.DepartmentIds,
            x.SlaDays, x.AllowReturn)));
    }
    internal static string Required(string value, int max)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result) || result.Length > max) throw new ArgumentException("Invalid workflow value.");
        return result;
    }

    internal static string DefaultPermission(string? value)
    {
        var result = value?.Trim();
        return string.IsNullOrWhiteSpace(result) ? "Documents.Workflow.Decide" : Required(result, 128);
    }
    internal static string? Optional(string? value, int max)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result)) return null;
        if (result.Length > max) throw new ArgumentException("Invalid workflow value.");
        return result;
    }
}

public sealed class WorkflowStep
{
    private WorkflowStep() { }
    internal WorkflowStep(Guid id, Guid definitionId, string code, string name, int order, string requiredPermission,
        string type, Guid? assigneeUserId, string? assigneeType, Guid? roleId, IReadOnlyList<Guid>? userIds,
        IReadOnlyList<Guid>? departmentIds, int? slaDays, bool allowReturn)
    {
        Id = id;
        DefinitionId = definitionId;
        Code = code;
        Name = name;
        Order = order;
        RequiredPermission = requiredPermission;
        Type = type;
        AssigneeType = WorkflowStepAssigneeTypes.Normalize(assigneeType);
        RoleId = roleId;
        UserIdsJson = Serialize(userIds);
        DepartmentIdsJson = Serialize(departmentIds);
        SlaDays = slaDays is < 0 ? throw new ArgumentOutOfRangeException(nameof(slaDays)) : slaDays;
        AllowReturn = allowReturn;
        AssigneeUserId = assigneeUserId ?? UserIds.FirstOrDefault();
        if (AssigneeUserId == Guid.Empty) AssigneeUserId = null;
    }
    public Guid Id { get; private set; }
    public Guid DefinitionId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public string RequiredPermission { get; private set; } = string.Empty;
    public string Type { get; private set; } = WorkflowStepTypes.Process;
    public Guid? AssigneeUserId { get; private set; }
    public string AssigneeType { get; private set; } = WorkflowStepAssigneeTypes.SpecificUser;
    public Guid? RoleId { get; private set; }
    public string UserIdsJson { get; private set; } = "[]";
    public string DepartmentIdsJson { get; private set; } = "[]";
    public int? SlaDays { get; private set; }
    public bool AllowReturn { get; private set; }
    public IReadOnlyList<Guid> UserIds => Deserialize(UserIdsJson);
    public IReadOnlyList<Guid> DepartmentIds => Deserialize(DepartmentIdsJson);
    public bool IsBlocking => WorkflowStepTypes.IsBlocking(Type);
    public Guid? ResolveAssignee(IReadOnlyDictionary<string, Guid>? overrides)
    {
        if (overrides is not null && overrides.TryGetValue(Code, out var selected)) return selected;
        if (UserIds.Count > 0) return UserIds[0];
        return AssigneeUserId;
    }
    private static string Serialize(IReadOnlyList<Guid>? ids) =>
        JsonSerializer.Serialize(ids?.Distinct().ToArray() ?? []);
    private static IReadOnlyList<Guid> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        return JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
    }
}

public sealed class WorkflowTemplate
{
    private WorkflowTemplate() { }
    public WorkflowTemplate(Guid id, string code, string name, Guid definitionId, int version, string templateJson, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 64) throw new ArgumentException("Invalid template code.");
        if (string.IsNullOrWhiteSpace(name) || name.Length > 256) throw new ArgumentException("Invalid template name.");
        if (definitionId == Guid.Empty) throw new ArgumentException("Definition is required.");
        if (version <= 0) throw new ArgumentOutOfRangeException(nameof(version));
        if (string.IsNullOrWhiteSpace(templateJson)) throw new ArgumentException("Template JSON is required.");
        using var _ = JsonDocument.Parse(templateJson);
        (Id, Code, Name, DefinitionId, Version, TemplateJson, OutputFormat, IsActive, CreationTime) =
            (id, code.Trim(), name.Trim(), definitionId, version, templateJson, "PDF", true, now);
    }
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public Guid DefinitionId { get; private set; }
    public int Version { get; private set; }
    public string TemplateJson { get; private set; } = string.Empty;
    public string OutputFormat { get; private set; } = "PDF";
    public bool IsActive { get; private set; }
    public DateTime CreationTime { get; private set; }
    public Guid? WordFileId { get; private set; }
    public string? WordFileName { get; private set; }
    public string? WordContentType { get; private set; }
    public string? WordBlobName { get; private set; }
    public Guid? PdfFileId { get; private set; }
    public string? PdfFileName { get; private set; }
    public string? PdfContentType { get; private set; }
    public string? PdfBlobName { get; private set; }
    public void SetActive(bool isActive) => IsActive = isActive;
    public void UpdateContent(string name, string templateJson, string outputFormat)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 256) throw new ArgumentException("Invalid template name.");
        if (string.IsNullOrWhiteSpace(templateJson)) throw new ArgumentException("Template JSON is required.");
        using var _ = JsonDocument.Parse(templateJson);
        Name = name.Trim();
        TemplateJson = templateJson;
        OutputFormat = string.IsNullOrWhiteSpace(outputFormat) ? "PDF" : outputFormat.Trim().ToUpperInvariant();
        if (OutputFormat.Length > 16) throw new ArgumentException("Invalid output format.");
    }
    public void AttachWord(Guid fileId, string fileName, string contentType, string blobName)
        => (WordFileId, WordFileName, WordContentType, WordBlobName) = (fileId, fileName, contentType, blobName);
    public void AttachPdf(Guid fileId, string fileName, string contentType, string blobName)
        => (PdfFileId, PdfFileName, PdfContentType, PdfBlobName) = (fileId, fileName, contentType, blobName);
}

public sealed class WorkflowInstance
{
    private readonly List<ApprovalTask> _tasks = [];
    private WorkflowInstance() { }
    public WorkflowInstance(Guid id, Guid documentId, WorkflowDefinition definition, string idempotencyKey, DateTime now,
        IReadOnlyDictionary<string, Guid>? assigneeOverrides = null, string? viewScopesJson = null)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("Idempotency key is required.");
        definition.EnsureStartable();
        var ordered = definition.Steps.OrderBy(x => x.Order).ToList();
        var first = FirstBlockingIndex(ordered);
        if (first < 0) throw new InvalidOperationException("Workflow definition has no blocking step. Add at least one process or sign step before starting.");
        Id = id;
        DocumentId = documentId;
        DefinitionId = definition.Id;
        IdempotencyKey = idempotencyKey.Trim();
        Status = WorkflowInstanceStatus.Running;
        CurrentStep = first;
        CreationTime = now;
        ViewScopesJson = viewScopesJson;
        AddTask(ordered[first], now, assigneeOverrides);
    }
    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid DefinitionId { get; private set; }
    public WorkflowInstanceStatus Status { get; private set; }
    public int CurrentStep { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string? ViewScopesJson { get; private set; }
    public DateTime CreationTime { get; private set; }
    public IReadOnlyCollection<ApprovalTask> Tasks => _tasks;

    public bool Decide(Guid taskId, bool approve, Guid actorUserId, string? comment, string commandKey,
        IReadOnlyList<WorkflowStep> orderedSteps, DateTime now, bool returnStep = false,
        IReadOnlyDictionary<string, Guid>? assigneeOverrides = null)
    {
        var task = _tasks.SingleOrDefault(x => x.Id == taskId) ?? throw new KeyNotFoundException("Approval task not found.");
        if (task.DecisionKey == commandKey) return false;
        if (Status != WorkflowInstanceStatus.Running) throw new InvalidOperationException("Workflow is not running.");
        var ordered = orderedSteps.OrderBy(x => x.Order).ToList();
        var current = ordered.ElementAtOrDefault(CurrentStep)
            ?? throw new InvalidOperationException("Workflow step configuration is missing.");
        if (returnStep)
        {
            if (!current.AllowReturn) throw new InvalidOperationException("This step does not allow return.");
            task.Return(actorUserId, comment, commandKey, now);
            Status = WorkflowInstanceStatus.Returned;
            return true;
        }
        task.Decide(approve, actorUserId, comment, commandKey, now);
        if (!approve)
        {
            Status = WorkflowInstanceStatus.Rejected;
            return true;
        }
        var next = NextBlockingIndex(ordered, CurrentStep + 1);
        if (next < 0)
        {
            Status = WorkflowInstanceStatus.Completed;
            CurrentStep = ordered.Count;
            return true;
        }
        CurrentStep = next;
        AddTask(ordered[next], now, assigneeOverrides);
        return true;
    }

    public void Resubmit(IReadOnlyList<WorkflowStep> orderedSteps, DateTime now, string commandKey,
        IReadOnlyDictionary<string, Guid>? assigneeOverrides = null)
    {
        if (string.IsNullOrWhiteSpace(commandKey)) throw new ArgumentException("Idempotency key is required.");
        if (Status != WorkflowInstanceStatus.Returned) throw new InvalidOperationException("Only returned workflows can be resubmitted.");
        var ordered = orderedSteps.OrderBy(x => x.Order).ToList();
        var first = FirstBlockingIndex(ordered);
        if (first < 0) throw new InvalidOperationException("A workflow requires at least one blocking step.");
        Status = WorkflowInstanceStatus.Running;
        CurrentStep = first;
        AddTask(ordered[first], now, assigneeOverrides);
    }

    private void AddTask(WorkflowStep step, DateTime now, IReadOnlyDictionary<string, Guid>? assigneeOverrides)
    {
        DateTime? dueAt = step.SlaDays is { } days ? now.AddDays(days) : null;
        _tasks.Add(new ApprovalTask(Guid.NewGuid(), Id, step.Code, now, step.ResolveAssignee(assigneeOverrides), dueAt));
    }

    internal static int FirstBlockingIndex(IReadOnlyList<WorkflowStep> ordered) => NextBlockingIndex(ordered, 0);
    internal static int NextBlockingIndex(IReadOnlyList<WorkflowStep> ordered, int start)
    {
        for (var i = start; i < ordered.Count; i++)
        {
            if (ordered[i].IsBlocking) return i;
        }
        return -1;
    }
}

public sealed class ApprovalTask
{
    private ApprovalTask() { }
    internal ApprovalTask(Guid id, Guid instanceId, string stepCode, DateTime now, Guid? assigneeUserId, DateTime? dueAt)
        => (Id, InstanceId, StepCode, Status, CreationTime, AssigneeUserId, DueAt)
            = (id, instanceId, stepCode, ApprovalTaskStatus.Pending, now, assigneeUserId, dueAt);
    public Guid Id { get; private set; }
    public Guid InstanceId { get; private set; }
    public string StepCode { get; private set; } = string.Empty;
    public ApprovalTaskStatus Status { get; private set; }
    public Guid? DecidedBy { get; private set; }
    public Guid? AssigneeUserId { get; private set; }
    public string? Comment { get; private set; }
    public string? DecisionKey { get; private set; }
    public DateTime CreationTime { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public DateTime? DueAt { get; private set; }
    internal void ExtendDueDate(int additionalDays, DateTime now, string? reason)
    {
        if (Status != ApprovalTaskStatus.Pending) throw new InvalidOperationException("Only a pending task can be extended.");
        if (additionalDays is < 1 or > 365) throw new ArgumentOutOfRangeException(nameof(additionalDays));
        DueAt = (DueAt ?? now).AddDays(additionalDays);
        if (!string.IsNullOrWhiteSpace(reason)) Comment = reason.Trim().Length > 1000 ? reason.Trim()[..1000] : reason.Trim();
    }
    internal void Decide(bool approve, Guid actorUserId, string? comment, string key, DateTime now)
    {
        EnsurePending(key);
        Status = approve ? ApprovalTaskStatus.Approved : ApprovalTaskStatus.Rejected;
        Apply(actorUserId, comment, key, now);
    }
    internal void Return(Guid actorUserId, string? comment, string key, DateTime now)
    {
        EnsurePending(key);
        Status = ApprovalTaskStatus.Returned;
        Apply(actorUserId, comment, key, now);
    }
    private void EnsurePending(string key)
    {
        if (Status != ApprovalTaskStatus.Pending) throw new InvalidOperationException("Approval task was already decided.");
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Idempotency key is required.");
    }
    private void Apply(Guid actorUserId, string? comment, string key, DateTime now)
    {
        DecidedBy = actorUserId;
        Comment = comment?.Trim();
        DecisionKey = key.Trim();
        DecidedAt = now;
    }
}
