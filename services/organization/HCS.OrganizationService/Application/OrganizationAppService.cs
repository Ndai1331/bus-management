using HCS.OrganizationService.Contracts;
using HCS.OrganizationService.Data;
using HCS.OrganizationService.Domain;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace HCS.OrganizationService.Application;

[UnitOfWork(false)]
public class OrganizationAppService : ApplicationService, IOrganizationAppService
{
    private readonly OrganizationDbContext _db;
    private readonly IGuidGenerator _guidGenerator;

    public OrganizationAppService(OrganizationDbContext db, IGuidGenerator guidGenerator)
    {
        _db = db;
        _guidGenerator = guidGenerator;
    }

    public virtual async Task<PagedResultDto<DepartmentDto>> GetDepartmentsAsync(OrganizationListInput input, CancellationToken ct = default)
    {
        var query = Filter(_db.Departments.AsNoTracking(), input);
        return await PageAsync(query, input, x => new DepartmentDto(x.Id, x.Code, x.Name, x.ParentId, x.SortOrder, x.IsActive), ct);
    }

    public virtual async Task<DepartmentDto> CreateDepartmentAsync(UpsertDepartmentDto input, CancellationToken ct = default)
    {
        await EnsureUniqueCodeAsync(_db.Departments, input.Code, null, ct);
        await EnsureDepartmentParentAsync(null, input.ParentId, ct);
        var entity = new Department(_guidGenerator.Create(), input.Code, input.Name, input.ParentId, input.SortOrder, input.IsActive);
        _db.Departments.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public virtual async Task<DepartmentDto> UpdateDepartmentAsync(Guid id, UpsertDepartmentDto input, CancellationToken ct = default)
    {
        var entity = await _db.Departments.FindAsync([id], ct) ?? throw new EntityNotFoundException(typeof(Department), id);
        await EnsureUniqueCodeAsync(_db.Departments, input.Code, id, ct);
        await EnsureDepartmentParentAsync(id, input.ParentId, ct);
        entity.Update(input.Code, input.Name, input.ParentId, input.SortOrder, input.IsActive);
        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public virtual Task DeleteDepartmentAsync(Guid id, CancellationToken ct = default) => DeleteAsync(_db.Departments, id, ct);

    public virtual async Task<PagedResultDto<UnitDto>> GetUnitsAsync(OrganizationListInput input, CancellationToken ct = default)
    {
        var query = Filter(_db.Units.AsNoTracking(), input);
        return await PageAsync(query, input, x => new UnitDto(x.Id, x.DepartmentId, x.Code, x.Name, x.SortOrder, x.IsActive), ct);
    }

    public virtual async Task<UnitDto> CreateUnitAsync(UpsertUnitDto input, CancellationToken ct = default)
    {
        await EnsureDepartmentExistsAsync(input.DepartmentId, ct);
        await EnsureUniqueCodeAsync(_db.Units, input.Code, null, ct);
        var entity = new Unit(_guidGenerator.Create(), input.DepartmentId, input.Code, input.Name, input.SortOrder, input.IsActive);
        _db.Units.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public virtual async Task<UnitDto> UpdateUnitAsync(Guid id, UpsertUnitDto input, CancellationToken ct = default)
    {
        var entity = await _db.Units.FindAsync([id], ct) ?? throw new EntityNotFoundException(typeof(Unit), id);
        await EnsureDepartmentExistsAsync(input.DepartmentId, ct);
        await EnsureUniqueCodeAsync(_db.Units, input.Code, id, ct);
        entity.Update(input.DepartmentId, input.Code, input.Name, input.SortOrder, input.IsActive);
        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public virtual Task DeleteUnitAsync(Guid id, CancellationToken ct = default) => DeleteAsync(_db.Units, id, ct);

    public virtual async Task<PagedResultDto<PositionDto>> GetPositionsAsync(OrganizationListInput input, CancellationToken ct = default)
    {
        var query = Filter(_db.Positions.AsNoTracking(), input);
        return await PageAsync(query, input, x => new PositionDto(x.Id, x.Code, x.Name, x.SignOrder, x.SortOrder, x.IsActive), ct);
    }

    public virtual async Task<PositionDto> CreatePositionAsync(UpsertPositionDto input, CancellationToken ct = default)
    {
        await EnsureUniqueCodeAsync(_db.Positions, input.Code, null, ct);
        var entity = new Position(_guidGenerator.Create(), input.Code, input.Name, input.SignOrder, input.SortOrder, input.IsActive);
        _db.Positions.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public virtual async Task<PositionDto> UpdatePositionAsync(Guid id, UpsertPositionDto input, CancellationToken ct = default)
    {
        var entity = await _db.Positions.FindAsync([id], ct) ?? throw new EntityNotFoundException(typeof(Position), id);
        await EnsureUniqueCodeAsync(_db.Positions, input.Code, id, ct);
        entity.Update(input.Code, input.Name, input.SignOrder, input.SortOrder, input.IsActive);
        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public virtual Task DeletePositionAsync(Guid id, CancellationToken ct = default) => DeleteAsync(_db.Positions, id, ct);

    public virtual async Task<PagedResultDto<MasterDataItemDto>> GetMasterDataAsync(string? type, OrganizationListInput input, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(type)) EnsureMasterDataTypeAllowed(type);
        var query = Filter(_db.MasterDataItems.AsNoTracking(), input);
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(x => x.Type == type.Trim());
        return await PageAsync(query, input, x => new MasterDataItemDto(x.Id, x.Type, x.Code, x.Name, x.SortOrder, x.IsActive), ct);
    }

    public virtual async Task<MasterDataItemDto> CreateMasterDataAsync(UpsertMasterDataItemDto input, CancellationToken ct = default)
    {
        EnsureMasterDataTypeAllowed(input.Type);
        await EnsureMasterDataUniqueAsync(input.Type, input.Code, null, ct);
        var entity = new MasterDataItem(_guidGenerator.Create(), input.Type, input.Code, input.Name, input.SortOrder, input.IsActive);
        _db.MasterDataItems.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public virtual async Task<MasterDataItemDto> UpdateMasterDataAsync(Guid id, UpsertMasterDataItemDto input, CancellationToken ct = default)
    {
        var entity = await _db.MasterDataItems.FindAsync([id], ct) ?? throw new EntityNotFoundException(typeof(MasterDataItem), id);
        EnsureMasterDataTypeAllowed(input.Type);
        await EnsureMasterDataUniqueAsync(input.Type, input.Code, id, ct);
        entity.Update(input.Type, input.Code, input.Name, input.SortOrder, input.IsActive);
        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public virtual Task DeleteMasterDataAsync(Guid id, CancellationToken ct = default) => DeleteAsync(_db.MasterDataItems, id, ct);

    public virtual async Task<PagedResultDto<UserOrganizationMappingDto>> GetUserMappingsAsync(Guid? userId, int skipCount, int maxResultCount, CancellationToken ct = default)
    {
        var query = _db.UserOrganizationMappings.AsNoTracking();
        if (userId.HasValue) query = query.Where(x => x.UserId == userId.Value);
        var total = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.CreationTime)
            .Skip(Math.Max(0, skipCount)).Take(Math.Clamp(maxResultCount, 1, 1000)).Select(x => Map(x)).ToListAsync(ct);
        return new PagedResultDto<UserOrganizationMappingDto>(total, items);
    }

    public virtual async Task<IReadOnlyList<UserDepartmentLookupDto>> GetUserDepartmentsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        var ids = userIds.Where(x => x != Guid.Empty).Distinct().Take(200).ToHashSet();
        if (ids.Count == 0) return [];

        var mappings = await (from userMapping in _db.UserOrganizationMappings.AsNoTracking()
                              join department in _db.Departments.AsNoTracking()
                                  on userMapping.DepartmentId equals department.Id
                              join position in _db.Positions.AsNoTracking()
                                  on userMapping.PositionId equals (Guid?)position.Id into positionJoin
                              from position in positionJoin.DefaultIfEmpty()
                              where ids.Contains(userMapping.UserId)
                              orderby userMapping.UserId, userMapping.IsPrimary descending, userMapping.CreationTime
                              select new
                              {
                                  userMapping.UserId,
                                  userMapping.DepartmentId,
                                  DepartmentName = department.Name,
                                  userMapping.PositionId,
                                  PositionName = position == null ? null : position.Name
                              }).ToListAsync(ct);

        return ids.Select(userId => mappings.FirstOrDefault(x => x.UserId == userId) is { } mapping
                ? new UserDepartmentLookupDto(userId, mapping.DepartmentId, mapping.DepartmentName,
                    mapping.PositionId, mapping.PositionName)
                : new UserDepartmentLookupDto(userId, null))
            .ToArray();
    }

    public virtual async Task<UserOrganizationMappingDto> CreateUserMappingAsync(UpsertUserOrganizationMappingDto input, CancellationToken ct = default)
    {
        await ValidateMappingAsync(input, null, ct);
        var entity = new UserOrganizationMapping(_guidGenerator.Create(), input.UserId, input.DepartmentId, input.UnitId, input.PositionId, input.IsPrimary);
        _db.UserOrganizationMappings.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public virtual async Task<UserOrganizationMappingDto> UpdateUserMappingAsync(Guid id, UpsertUserOrganizationMappingDto input, CancellationToken ct = default)
    {
        var entity = await _db.UserOrganizationMappings.FindAsync([id], ct) ?? throw new EntityNotFoundException(typeof(UserOrganizationMapping), id);
        await ValidateMappingAsync(input, id, ct);
        entity.Update(input.UserId, input.DepartmentId, input.UnitId, input.PositionId, input.IsPrimary);
        await _db.SaveChangesAsync(ct);
        return Map(entity);
    }

    public virtual Task DeleteUserMappingAsync(Guid id, CancellationToken ct = default) => DeleteAsync(_db.UserOrganizationMappings, id, ct);

    private async Task ValidateMappingAsync(UpsertUserOrganizationMappingDto input, Guid? currentId, CancellationToken ct)
    {
        await EnsureDepartmentExistsAsync(input.DepartmentId, ct);
        if (input.UnitId.HasValue && !await _db.Units.AnyAsync(x => x.Id == input.UnitId && x.DepartmentId == input.DepartmentId, ct))
            throw new BusinessException(OrganizationErrorCodes.UnitDepartmentMismatch);
        if (input.PositionId.HasValue && !await _db.Positions.AnyAsync(x => x.Id == input.PositionId, ct))
            throw new EntityNotFoundException(typeof(Position), input.PositionId);
        if (input.IsPrimary && await _db.UserOrganizationMappings.AnyAsync(x => x.UserId == input.UserId && x.IsPrimary && x.Id != currentId, ct))
            throw new BusinessException(OrganizationErrorCodes.MultiplePrimaryMappings);
        if (await _db.UserOrganizationMappings.AnyAsync(x =>
                x.UserId == input.UserId && x.DepartmentId == input.DepartmentId &&
                x.UnitId == input.UnitId && x.PositionId == input.PositionId && x.Id != currentId, ct))
            throw new BusinessException(OrganizationErrorCodes.DuplicateUserMapping);
    }

    private async Task EnsureDepartmentParentAsync(Guid? departmentId, Guid? parentId, CancellationToken ct)
    {
        await EnsureDepartmentExistsAsync(parentId, ct);
        var cursor = parentId;
        while (cursor.HasValue)
        {
            if (cursor == departmentId)
                throw new BusinessException(OrganizationErrorCodes.DepartmentHierarchyCycle);
            cursor = await _db.Departments.Where(x => x.Id == cursor.Value).Select(x => x.ParentId).SingleAsync(ct);
        }
    }

    private async Task EnsureDepartmentExistsAsync(Guid? id, CancellationToken ct)
    {
        if (id.HasValue && !await _db.Departments.AnyAsync(x => x.Id == id.Value, ct))
            throw new BusinessException(OrganizationErrorCodes.InvalidDepartment).WithData("DepartmentId", id.Value);
    }

    private static async Task EnsureUniqueCodeAsync<TEntity>(DbSet<TEntity> set, string code, Guid? currentId, CancellationToken ct)
        where TEntity : CodedAggregate
    {
        var normalized = code.Trim();
        if (await set.AnyAsync(x => x.Code == normalized && x.Id != currentId, ct))
            throw new BusinessException(OrganizationErrorCodes.DuplicateCode).WithData("Code", normalized);
    }

    private async Task EnsureMasterDataUniqueAsync(string type, string code, Guid? currentId, CancellationToken ct)
    {
        var normalizedType = type.Trim();
        var normalizedCode = code.Trim();
        if (await _db.MasterDataItems.AnyAsync(x => x.Type == normalizedType && x.Code == normalizedCode && x.Id != currentId, ct))
            throw new BusinessException(OrganizationErrorCodes.DuplicateCode).WithData("Type", normalizedType).WithData("Code", normalizedCode);
    }

    private static void EnsureMasterDataTypeAllowed(string? type)
    {
        if (string.IsNullOrWhiteSpace(type) || !OrganizationConsts.AllowedMasterDataTypes.Contains(type.Trim()))
            throw new BusinessException(OrganizationErrorCodes.InvalidMasterDataType).WithData("Type", type ?? string.Empty);
    }

    private static IQueryable<TEntity> Filter<TEntity>(IQueryable<TEntity> query, OrganizationListInput input) where TEntity : CodedAggregate
    {
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var filter = input.Filter.Trim().ToLowerInvariant();
            query = query.Where(x => x.Code.ToLower().Contains(filter)
                                  || x.Name.ToLower().Contains(filter));
        }
        if (input.IsActive.HasValue) query = query.Where(x => x.IsActive == input.IsActive.Value);
        return query;
    }

    private static async Task<PagedResultDto<TDto>> PageAsync<TEntity, TDto>(IQueryable<TEntity> query, OrganizationListInput input, System.Linq.Expressions.Expression<Func<TEntity, TDto>> selector, CancellationToken ct)
        where TEntity : CodedAggregate
    {
        var total = await query.LongCountAsync(ct);
        var items = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Skip(Math.Max(0, input.SkipCount)).Take(Math.Clamp(input.MaxResultCount, 1, 100)).Select(selector).ToListAsync(ct);
        return new PagedResultDto<TDto>(total, items);
    }

    private async Task DeleteAsync<TEntity>(DbSet<TEntity> set, Guid id, CancellationToken ct) where TEntity : class
    {
        var entity = await set.FindAsync([id], ct) ?? throw new EntityNotFoundException(typeof(TEntity), id);
        set.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    private static DepartmentDto Map(Department x) => new(x.Id, x.Code, x.Name, x.ParentId, x.SortOrder, x.IsActive);
    private static UnitDto Map(Unit x) => new(x.Id, x.DepartmentId, x.Code, x.Name, x.SortOrder, x.IsActive);
    private static PositionDto Map(Position x) => new(x.Id, x.Code, x.Name, x.SignOrder, x.SortOrder, x.IsActive);
    private static MasterDataItemDto Map(MasterDataItem x) => new(x.Id, x.Type, x.Code, x.Name, x.SortOrder, x.IsActive);
    private static UserOrganizationMappingDto Map(UserOrganizationMapping x) => new(x.Id, x.UserId, x.DepartmentId, x.UnitId, x.PositionId, x.IsPrimary);
}
