using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace HCS.OrganizationService.Domain;

public abstract class CodedAggregate : AuditedAggregateRoot<Guid>
{
    public string Code { get; protected set; } = string.Empty;
    public string Name { get; protected set; } = string.Empty;
    public int SortOrder { get; protected set; }
    public bool IsActive { get; protected set; }

    protected CodedAggregate() { }

    protected CodedAggregate(Guid id) : base(id) { }

    protected void SetCommon(string code, string name, int sortOrder, bool isActive)
    {
        Code = Check.NotNullOrWhiteSpace(code, nameof(code), OrganizationConsts.MaxCodeLength).Trim();
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), OrganizationConsts.MaxNameLength).Trim();
        if (sortOrder is < 0 or > OrganizationConsts.MaxSortOrder)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), sortOrder,
                $"Sort order must be between 0 and {OrganizationConsts.MaxSortOrder}.");
        }

        SortOrder = sortOrder;
        IsActive = isActive;
    }
}

public sealed class Department : CodedAggregate
{
    public Guid? ParentId { get; private set; }

    private Department() { }

    public Department(Guid id, string code, string name, Guid? parentId, int sortOrder, bool isActive = true)
        : base(id) => Update(code, name, parentId, sortOrder, isActive);

    public void Update(string code, string name, Guid? parentId, int sortOrder, bool isActive)
    {
        if (parentId == Id)
        {
            throw new BusinessException(OrganizationErrorCodes.DepartmentCannotBeOwnParent);
        }

        SetCommon(code, name, sortOrder, isActive);
        ParentId = parentId;
    }
}

public sealed class Unit : CodedAggregate
{
    public Guid DepartmentId { get; private set; }

    private Unit() { }

    public Unit(Guid id, Guid departmentId, string code, string name, int sortOrder, bool isActive = true)
        : base(id) => Update(departmentId, code, name, sortOrder, isActive);

    public void Update(Guid departmentId, string code, string name, int sortOrder, bool isActive)
    {
        DepartmentId = Check.NotDefaultOrNull<Guid>(departmentId, nameof(departmentId));
        SetCommon(code, name, sortOrder, isActive);
    }
}

public sealed class Position : CodedAggregate
{
    public int SignOrder { get; private set; }

    private Position() { }

    public Position(Guid id, string code, string name, int signOrder, int sortOrder, bool isActive = true)
        : base(id) => Update(code, name, signOrder, sortOrder, isActive);

    public void Update(string code, string name, int signOrder, int sortOrder, bool isActive)
    {
        if (signOrder is < 0 or > OrganizationConsts.MaxSignOrder)
        {
            throw new ArgumentOutOfRangeException(nameof(signOrder), signOrder,
                $"Sign order must be between 0 and {OrganizationConsts.MaxSignOrder}.");
        }

        SetCommon(code, name, sortOrder, isActive);
        SignOrder = signOrder;
    }
}

public sealed class MasterDataItem : CodedAggregate
{
    public string Type { get; private set; } = string.Empty;

    private MasterDataItem() { }

    public MasterDataItem(Guid id, string type, string code, string name, int sortOrder, bool isActive = true)
        : base(id) => Update(type, code, name, sortOrder, isActive);

    public void Update(string type, string code, string name, int sortOrder, bool isActive)
    {
        Type = Check.NotNullOrWhiteSpace(type, nameof(type), OrganizationConsts.MaxTypeLength).Trim();
        SetCommon(code, name, sortOrder, isActive);
    }
}

public sealed class UserOrganizationMapping : AuditedAggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public Guid DepartmentId { get; private set; }
    public Guid? UnitId { get; private set; }
    public Guid? PositionId { get; private set; }
    public bool IsPrimary { get; private set; }

    private UserOrganizationMapping() { }

    public UserOrganizationMapping(
        Guid id, Guid userId, Guid departmentId, Guid? unitId, Guid? positionId, bool isPrimary) : base(id)
        => Update(userId, departmentId, unitId, positionId, isPrimary);

    public void Update(Guid userId, Guid departmentId, Guid? unitId, Guid? positionId, bool isPrimary)
    {
        UserId = Check.NotDefaultOrNull<Guid>(userId, nameof(userId));
        DepartmentId = Check.NotDefaultOrNull<Guid>(departmentId, nameof(departmentId));
        UnitId = unitId;
        PositionId = positionId;
        IsPrimary = isPrimary;
    }
}
