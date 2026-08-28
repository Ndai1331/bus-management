using HCS.OrganizationService.Contracts;
using Volo.Abp.Application.Dtos;

namespace HCS.OrganizationService.Application;

public interface IOrganizationAppService
{
    Task<PagedResultDto<DepartmentDto>> GetDepartmentsAsync(OrganizationListInput input, CancellationToken ct = default);
    Task<DepartmentDto> CreateDepartmentAsync(UpsertDepartmentDto input, CancellationToken ct = default);
    Task<DepartmentDto> UpdateDepartmentAsync(Guid id, UpsertDepartmentDto input, CancellationToken ct = default);
    Task DeleteDepartmentAsync(Guid id, CancellationToken ct = default);

    Task<PagedResultDto<UnitDto>> GetUnitsAsync(OrganizationListInput input, CancellationToken ct = default);
    Task<UnitDto> CreateUnitAsync(UpsertUnitDto input, CancellationToken ct = default);
    Task<UnitDto> UpdateUnitAsync(Guid id, UpsertUnitDto input, CancellationToken ct = default);
    Task DeleteUnitAsync(Guid id, CancellationToken ct = default);

    Task<PagedResultDto<PositionDto>> GetPositionsAsync(OrganizationListInput input, CancellationToken ct = default);
    Task<PositionDto> CreatePositionAsync(UpsertPositionDto input, CancellationToken ct = default);
    Task<PositionDto> UpdatePositionAsync(Guid id, UpsertPositionDto input, CancellationToken ct = default);
    Task DeletePositionAsync(Guid id, CancellationToken ct = default);

    Task<PagedResultDto<MasterDataItemDto>> GetMasterDataAsync(string? type, OrganizationListInput input, CancellationToken ct = default);
    Task<MasterDataItemDto> CreateMasterDataAsync(UpsertMasterDataItemDto input, CancellationToken ct = default);
    Task<MasterDataItemDto> UpdateMasterDataAsync(Guid id, UpsertMasterDataItemDto input, CancellationToken ct = default);
    Task DeleteMasterDataAsync(Guid id, CancellationToken ct = default);

    Task<PagedResultDto<UserOrganizationMappingDto>> GetUserMappingsAsync(Guid? userId, int skipCount, int maxResultCount, CancellationToken ct = default);
    Task<IReadOnlyList<UserDepartmentLookupDto>> GetUserDepartmentsAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
    Task<UserOrganizationMappingDto> CreateUserMappingAsync(UpsertUserOrganizationMappingDto input, CancellationToken ct = default);
    Task<UserOrganizationMappingDto> UpdateUserMappingAsync(Guid id, UpsertUserOrganizationMappingDto input, CancellationToken ct = default);
    Task DeleteUserMappingAsync(Guid id, CancellationToken ct = default);
}
