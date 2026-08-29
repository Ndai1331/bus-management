using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HCS.BusManagementService.Contracts;
using HCS.BusManagementService.Data;
using HCS.BusManagementService.Domain;
using HCS.BusManagementService.Integration;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;
using HCS.IntegrationEvents.Auditing;

namespace HCS.BusManagementService.Application;

public sealed class BusAccessScope(BusManagementDbContext db, ICurrentUser currentUser) : ITransientDependency
{
    public Guid UserId => currentUser.Id ?? throw new AbpAuthorizationException("Authenticated user required.");

    public bool IsGlobal => currentUser.IsInRole("admin") || currentUser.IsInRole("lanhdao");

    public async Task<IReadOnlySet<Guid>?> GetStationIdsAsync(CancellationToken cancellationToken = default)
    {
        if (IsGlobal) return null;
        return (await db.UserStationAssignments.AsNoTracking()
            .Where(x => x.UserId == UserId && x.IsActive &&
                (!x.ValidFrom.HasValue || x.ValidFrom.Value <= DateTime.UtcNow.Date) &&
                (!x.ValidTo.HasValue || x.ValidTo.Value >= DateTime.UtcNow.Date) &&
                db.BusStations.Any(station => station.Id == x.StationId && station.IsActive))
            .Select(x => x.StationId).ToListAsync(cancellationToken)).ToHashSet();
    }

    public async Task EnsureStationAsync(Guid stationId, CancellationToken cancellationToken = default)
    {
        if (stationId == Guid.Empty) throw new BusinessException("Bus:StationRequired");
        if (!await db.BusStations.AsNoTracking().AnyAsync(x => x.Id == stationId && x.IsActive, cancellationToken))
            throw new BusinessException("Bus:StationNotFound");
        var stationIds = await GetStationIdsAsync(cancellationToken);
        if (stationIds is not null && !stationIds.Contains(stationId))
            throw new AbpAuthorizationException("The station is outside the current user's assignment.");
    }

    public void EnsureGlobal()
    {
        if (!IsGlobal) throw new AbpAuthorizationException("A global station scope is required.");
    }
}

public sealed class BusManagementAppService(BusManagementDbContext db, BusAccessScope scope, ICurrentUser currentUser,
    IHttpContextAccessor httpContextAccessor)
    : ITransientDependency
{
    private Guid? UserId => currentUser.Id;

    public async Task<PagedBusDto<BusStationDto>> GetStationsAsync(string? filter, int skip, int take, CancellationToken ct)
    {
        var stationIds = await scope.GetStationIdsAsync(ct);
        var query = db.BusStations.AsNoTracking();
        if (stationIds is not null) query = query.Where(x => stationIds.Contains(x.Id));
        if (!string.IsNullOrWhiteSpace(filter)) query = query.Where(x => x.Code.Contains(filter) || x.Name.Contains(filter));
        var total = await query.LongCountAsync(ct);
        var items = await query.OrderBy(x => x.Code).Skip(Math.Max(skip, 0)).Take(Math.Clamp(take, 1, 100))
            .Select(x => new BusStationDto(x.Id, x.Code, x.Name, x.Address, x.TimeZone, x.IsActive)).ToListAsync(ct);
        return new(total, items);
    }

    public async Task<BusStationDto> CreateStationAsync(CreateBusStationDto input, CancellationToken ct)
    {
        scope.EnsureGlobal();
        var station = new BusStation(Guid.NewGuid(), input.Code, input.Name, input.Address, input.TimeZone);
        db.BusStations.Add(station); await db.SaveChangesAsync(ct); return ToDto(station);
    }

    public async Task<BusStationDto> UpdateStationAsync(Guid id, UpdateBusStationDto input, CancellationToken ct)
    {
        scope.EnsureGlobal();
        var station = await db.BusStations.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Bus:StationNotFound");
        station.Change(input.Name, input.Address, input.TimeZone, input.IsActive); await db.SaveChangesAsync(ct); return ToDto(station);
    }

    public async Task<StationAssignmentDto> AssignStationAsync(AssignStationDto input, CancellationToken ct)
    {
        scope.EnsureGlobal();
        await RequireStationAsync(input.StationId, ct);
        if (input.IsPrimary)
        {
            var currentPrimary = await db.UserStationAssignments.Where(x => x.UserId == input.UserId && x.IsPrimary && x.IsActive).ToListAsync(ct);
            foreach (var assignment in currentPrimary) assignment.Change(false, assignment.ValidFrom, assignment.ValidTo);
        }
        var existing = await db.UserStationAssignments.SingleOrDefaultAsync(x => x.UserId == input.UserId && x.StationId == input.StationId, ct);
        if (existing is not null)
        {
            existing.Change(input.IsPrimary, input.ValidFrom, input.ValidTo);
            await db.SaveChangesAsync(ct); return ToDto(existing);
        }
        var assignmentNew = new UserStationAssignment(Guid.NewGuid(), input.UserId, input.StationId, input.IsPrimary, input.ValidFrom, input.ValidTo);
        db.UserStationAssignments.Add(assignmentNew); await db.SaveChangesAsync(ct); return ToDto(assignmentNew);
    }

    public async Task<PagedBusDto<OperatorDto>> GetOperatorsAsync(int skip, int take, CancellationToken ct)
    {
        var stationIds = await scope.GetStationIdsAsync(ct);
        var query = db.TransportOperators.AsNoTracking();
        if (stationIds is not null) query = query.Where(x => x.StationId.HasValue && stationIds.Contains(x.StationId.Value));
        query = query.OrderBy(x => x.Code);
        var total = await query.LongCountAsync(ct); var items = await query.Skip(Math.Max(skip, 0)).Take(Math.Clamp(take, 1, 100))
            .Select(x => new OperatorDto(x.Id, x.Code, x.Name, x.IsActive, x.StationId)).ToListAsync(ct); return new(total, items);
    }

    public async Task<OperatorDto> CreateOperatorAsync(CreateOperatorDto input, CancellationToken ct)
    {
        var stationId = await ResolveMasterDataStationAsync(input.StationId, ct);
        var entity = new TransportOperator(Guid.NewGuid(), input.Code, input.Name, stationId); db.TransportOperators.Add(entity); await db.SaveChangesAsync(ct);
        return new(entity.Id, entity.Code, entity.Name, entity.IsActive, entity.StationId);
    }

    public async Task<PagedBusDto<RouteDto>> GetRoutesAsync(int skip, int take, CancellationToken ct)
    {
        var stationIds = await scope.GetStationIdsAsync(ct);
        var query = db.FixedRoutes.AsNoTracking();
        if (stationIds is not null) query = query.Where(x => x.StationId.HasValue && stationIds.Contains(x.StationId.Value));
        query = query.OrderBy(x => x.Code); var total = await query.LongCountAsync(ct);
        var items = await query.Skip(Math.Max(skip, 0)).Take(Math.Clamp(take, 1, 100)).Select(x => new RouteDto(x.Id, x.Code, x.Name, x.OperatorId, x.IsActive, x.StationId)).ToListAsync(ct); return new(total, items);
    }

    public async Task<RouteDto> CreateRouteAsync(CreateRouteDto input, CancellationToken ct)
    {
        var operatorEntity = await db.TransportOperators.SingleOrDefaultAsync(x => x.Id == input.OperatorId && x.IsActive, ct)
            ?? throw new BusinessException("Bus:OperatorNotFound");
        var stationId = await ResolveMasterDataStationAsync(input.StationId ?? operatorEntity.StationId, ct);
        EnsureMasterDataStationMatches(operatorEntity.StationId, stationId);
        var entity = new FixedRoute(Guid.NewGuid(), input.Code, input.Name, input.OperatorId, stationId); db.FixedRoutes.Add(entity); await db.SaveChangesAsync(ct);
        return new(entity.Id, entity.Code, entity.Name, entity.OperatorId, entity.IsActive, entity.StationId);
    }

    public async Task<PagedBusDto<VehicleDto>> GetVehiclesAsync(int skip, int take, CancellationToken ct)
    {
        var stationIds = await scope.GetStationIdsAsync(ct);
        var query = db.Vehicles.AsNoTracking();
        if (stationIds is not null) query = query.Where(x => x.StationId.HasValue && stationIds.Contains(x.StationId.Value));
        query = query.OrderBy(x => x.PlateNumber); var total = await query.LongCountAsync(ct);
        var items = await query.Skip(Math.Max(skip, 0)).Take(Math.Clamp(take, 1, 100)).Select(x => new VehicleDto(x.Id, x.PlateNumber, x.VehicleType, x.OperatorId, x.IsActive, x.StationId)).ToListAsync(ct); return new(total, items);
    }

    public async Task<VehicleDto> CreateVehicleAsync(CreateVehicleDto input, CancellationToken ct)
    {
        var operatorEntity = await db.TransportOperators.SingleOrDefaultAsync(x => x.Id == input.OperatorId && x.IsActive, ct)
            ?? throw new BusinessException("Bus:OperatorNotFound");
        var stationId = await ResolveMasterDataStationAsync(input.StationId ?? operatorEntity.StationId, ct);
        EnsureMasterDataStationMatches(operatorEntity.StationId, stationId);
        var entity = new Vehicle(Guid.NewGuid(), input.PlateNumber, input.VehicleType, input.OperatorId, stationId); db.Vehicles.Add(entity); await db.SaveChangesAsync(ct);
        return new(entity.Id, entity.PlateNumber, entity.VehicleType, entity.OperatorId, entity.IsActive, entity.StationId);
    }

    public async Task<PagedBusDto<DriverDto>> GetDriversAsync(int skip, int take, CancellationToken ct)
    {
        var stationIds = await scope.GetStationIdsAsync(ct);
        var query = db.Drivers.AsNoTracking();
        if (stationIds is not null) query = query.Where(x => x.StationId.HasValue && stationIds.Contains(x.StationId.Value));
        query = query.OrderBy(x => x.FullName); var total = await query.LongCountAsync(ct);
        var items = await query.Skip(Math.Max(skip, 0)).Take(Math.Clamp(take, 1, 100)).Select(x => new DriverDto(x.Id, x.FullName, x.LicenseNumber, x.IsActive, x.StationId)).ToListAsync(ct); return new(total, items);
    }

    public async Task<DriverDto> CreateDriverAsync(CreateDriverDto input, CancellationToken ct)
    {
        var stationId = await ResolveMasterDataStationAsync(input.StationId, ct);
        var entity = new Driver(Guid.NewGuid(), input.FullName, input.LicenseNumber, stationId); db.Drivers.Add(entity); await db.SaveChangesAsync(ct);
        return new(entity.Id, entity.FullName, entity.LicenseNumber, entity.IsActive, entity.StationId);
    }

    public async Task<PagedBusDto<CarrierContractDto>> GetCarrierContractsAsync(Guid? stationId, DateTime? onDate, int skip, int take, CancellationToken ct)
    {
        var stationIds = await scope.GetStationIdsAsync(ct);
        if (stationId.HasValue) await scope.EnsureStationAsync(stationId.Value, ct);
        var query = db.CarrierContracts.AsNoTracking();
        if (stationIds is not null) query = query.Where(x => stationIds.Contains(x.StationId));
        if (stationId.HasValue) query = query.Where(x => x.StationId == stationId);
        if (onDate.HasValue) query = query.Where(x => x.IsActive && x.StartDate <= onDate.Value.Date && x.EndDate >= onDate.Value.Date);
        var total = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(x => x.EndDate).Skip(Math.Max(skip, 0)).Take(Math.Clamp(take, 1, 100)).ToListAsync(ct);
        return new(total, items.Select(ToDto).ToList());
    }

    public async Task<CarrierContractDto> CreateCarrierContractAsync(CreateCarrierContractDto input, CancellationToken ct)
    {
        await scope.EnsureStationAsync(input.StationId, ct); await RequireStationAsync(input.StationId, ct);
        var operatorEntity = await db.TransportOperators.SingleOrDefaultAsync(x => x.Id == input.OperatorId && x.IsActive, ct)
            ?? throw new BusinessException("Bus:OperatorNotFound");
        EnsureMasterDataStationMatches(operatorEntity.StationId, input.StationId);
        var contract = new CarrierContract(Guid.NewGuid(), input.StationId, input.OperatorId, input.ContractNumber,
            input.StartDate, input.EndDate, input.DocumentId);
        db.CarrierContracts.Add(contract); await db.SaveChangesAsync(ct); return ToDto(contract);
    }

    public async Task<PagedBusDto<VehicleLegalDocumentDto>> GetVehicleLegalDocumentsAsync(Guid? vehicleId, DateTime? expiringBefore,
        int skip, int take, CancellationToken ct)
    {
        var stationIds = await scope.GetStationIdsAsync(ct);
        var query = db.VehicleLegalDocuments.AsNoTracking();
        if (stationIds is not null)
        {
            query = query.Where(x => (x.StationId.HasValue && stationIds.Contains(x.StationId.Value)) ||
                (!x.StationId.HasValue && db.DepartureTrips.Where(t => t.VehicleId == x.VehicleId).Select(t => t.StationId).Distinct().Count() == 1 &&
                    db.DepartureTrips.Any(t => stationIds.Contains(t.StationId) && t.VehicleId == x.VehicleId)));
        }
        if (vehicleId.HasValue) query = query.Where(x => x.VehicleId == vehicleId);
        if (expiringBefore.HasValue) query = query.Where(x => x.ExpiresOn <= expiringBefore.Value.Date);
        var total = await query.LongCountAsync(ct);
        var items = await query.OrderBy(x => x.ExpiresOn).Skip(Math.Max(skip, 0)).Take(Math.Clamp(take, 1, 100)).ToListAsync(ct);
        return new(total, items.Select(ToDto).ToList());
    }

    public async Task<VehicleLegalDocumentDto> CreateVehicleLegalDocumentAsync(CreateVehicleLegalDocumentDto input, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.VehicleId && x.IsActive, ct)
            ?? throw new BusinessException("Bus:VehicleNotFound");
        var stationId = await ResolveVehicleStationAsync(vehicle.Id, input.StationId, ct);
        EnsureMasterDataStationMatches(vehicle.StationId, stationId);
        await scope.EnsureStationAsync(stationId, ct);
        await RequireStationAsync(stationId, ct);
        if (await db.VehicleLegalDocuments.AnyAsync(x => x.VehicleId == vehicle.Id && x.DocumentType == input.DocumentType, ct))
            throw new BusinessException("Bus:VehicleDocumentExists");
        var document = new VehicleLegalDocument(Guid.NewGuid(), vehicle.Id, input.DocumentType, input.ExpiresOn, input.DocumentId, stationId);
        db.VehicleLegalDocuments.Add(document); await db.SaveChangesAsync(ct); return ToDto(document);
    }

    public async Task<VehicleLegalDocumentDto> UpdateVehicleLegalDocumentAsync(Guid id, UpdateVehicleLegalDocumentDto input, CancellationToken ct)
    {
        var document = await db.VehicleLegalDocuments.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new BusinessException("Bus:VehicleDocumentNotFound");
        var stationId = await ResolveVehicleStationAsync(document.VehicleId, document.StationId, ct);
        await scope.EnsureStationAsync(stationId, ct);
        document.Renew(input.ExpiresOn, input.DocumentId, input.IsActive);
        if (!document.StationId.HasValue) document.AssignStation(stationId);
        await db.SaveChangesAsync(ct);
        return ToDto(document);
    }

    public async Task<DepartureDto> CreateDepartureAsync(CreateDepartureDto input, CancellationToken ct)
    {
        await scope.EnsureStationAsync(input.StationId, ct);
        await RequireStationAsync(input.StationId, ct);
        await EnsureOpenDayAsync(input.StationId, input.BusinessDate, ct);
        if (!await db.TransportOperators.AnyAsync(x => x.Id == input.OperatorId && x.IsActive &&
            (x.StationId == input.StationId || (scope.IsGlobal && !x.StationId.HasValue)), ct)) throw new BusinessException("Bus:OperatorNotFound");
        if (!await db.FixedRoutes.AnyAsync(x => x.Id == input.RouteId && x.OperatorId == input.OperatorId && x.IsActive &&
            (x.StationId == input.StationId || (scope.IsGlobal && !x.StationId.HasValue)), ct)) throw new BusinessException("Bus:RouteNotFound");
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(x => x.Id == input.VehicleId && x.OperatorId == input.OperatorId && x.IsActive &&
            (x.StationId == input.StationId || (scope.IsGlobal && !x.StationId.HasValue)), ct) ?? throw new BusinessException("Bus:VehicleNotFound");
        var driver = await db.Drivers.SingleOrDefaultAsync(x => x.Id == input.DriverId && x.IsActive &&
            (x.StationId == input.StationId || (scope.IsGlobal && !x.StationId.HasValue)), ct) ?? throw new BusinessException("Bus:DriverNotFound");
        var businessDate = input.BusinessDate.Date;
        var inspectionValid = input.InspectionValid && await HasVehicleDocumentAsync(input.VehicleId, ["Inspection", "DangKiem"], businessDate, input.StationId, ct);
        var routeBadgeValid = input.RouteBadgeValid && await HasVehicleDocumentAsync(input.VehicleId, ["RouteBadge", "PhuHieu"], businessDate, input.StationId, ct);
        var insuranceValid = input.InsuranceValid && await HasVehicleDocumentAsync(input.VehicleId, ["Insurance", "BaoHiem"], businessDate, input.StationId, ct);
        var driverLicenseValid = input.DriverLicenseValid && !string.IsNullOrWhiteSpace(driver.LicenseNumber);
        var contractValid = input.ContractValid && await db.CarrierContracts.AnyAsync(x => x.StationId == input.StationId && x.OperatorId == input.OperatorId &&
            x.IsActive && x.StartDate <= businessDate && x.EndDate >= businessDate, ct);
        var feePaid = input.FeePaid && await db.Tariffs.AnyAsync(x => x.StationId == input.StationId && (x.RouteId == null || x.RouteId == input.RouteId) && x.VehicleType == vehicle.VehicleType && x.IsActive && x.EffectiveFrom <= businessDate && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= businessDate), ct);

        var trip = new DepartureTrip(Guid.NewGuid(), input.StationId, input.OperatorId, input.RouteId, input.VehicleId, input.DriverId,
            input.BusinessDate, input.ShiftCode, input.ScheduledDepartureUtc, input.PassengerCount);
        trip.Register();
        var checks = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["Inspection"] = inspectionValid, ["RouteBadge"] = routeBadgeValid,
            ["Insurance"] = insuranceValid, ["DriverLicense"] = driverLicenseValid,
            ["TransportOrder"] = input.TransportOrderValid, ["Contract"] = contractValid,
            ["Fee"] = feePaid, ["Control"] = input.ControlApproved
        };
        db.DepartureTrips.Add(trip);
        foreach (var check in checks) db.DepartureChecks.Add(new DepartureCheck(Guid.NewGuid(), trip.Id, check.Key, check.Value));
        if (checks.Values.All(x => x)) trip.MarkReady(); else trip.Block();
        db.OutboxMessages.Add(BusOutbox.Create(new BusDepartureChangedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, null,
            trip.Id, trip.StationId, trip.Status), Guid.NewGuid().ToString("N")));
        await db.SaveChangesAsync(ct); return await GetDepartureAsync(trip.Id, ct);
    }

    public async Task<PagedBusDto<DepartureDto>> GetDeparturesAsync(Guid? stationId, DateTime? from, DateTime? to, string? status, int skip, int take, CancellationToken ct)
    {
        if (stationId.HasValue) await scope.EnsureStationAsync(stationId.Value, ct);
        var stationIds = await scope.GetStationIdsAsync(ct); var query = db.DepartureTrips.AsNoTracking();
        if (stationIds is not null) query = query.Where(x => stationIds.Contains(x.StationId));
        if (stationId.HasValue) query = query.Where(x => x.StationId == stationId);
        if (from.HasValue) query = query.Where(x => x.BusinessDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(x => x.BusinessDate <= to.Value.Date);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var total = await query.LongCountAsync(ct); var trips = await query.OrderByDescending(x => x.BusinessDate).ThenBy(x => x.ScheduledDepartureUtc)
            .Skip(Math.Max(skip, 0)).Take(Math.Clamp(take, 1, 100)).ToListAsync(ct);
        var ids = trips.Select(x => x.Id).ToArray(); var checks = await db.DepartureChecks.AsNoTracking().Where(x => ids.Contains(x.DepartureId)).ToListAsync(ct);
        return new(total, trips.Select(x => ToDto(x, checks.Where(c => c.DepartureId == x.Id))).ToList());
    }

    public async Task<DepartureDto> UpdateDepartureChecksAsync(Guid id, UpdateDepartureChecksDto input, CancellationToken ct)
    {
        var trip = await db.DepartureTrips.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Bus:DepartureNotFound");
        await scope.EnsureStationAsync(trip.StationId, ct);
        await EnsureOpenDayAsync(trip.StationId, trip.BusinessDate, ct);
        if (trip.Status is not (BusStatuses.Registered or BusStatuses.Blocked)) throw new BusinessException("Bus:DepartureChecksLocked");
        var allowed = new HashSet<string>(["Inspection", "RouteBadge", "Insurance", "DriverLicense", "TransportOrder", "Contract", "Fee", "Control"], StringComparer.Ordinal);
        if (input.Checks is null || input.Checks.Any(x => string.IsNullOrWhiteSpace(x.CheckType))) throw new BusinessException("Bus:DepartureChecksInvalid");
        var groups = input.Checks.GroupBy(x => x.CheckType, StringComparer.Ordinal).ToArray();
        if (groups.Any(x => x.Count() != 1)) throw new BusinessException("Bus:DepartureChecksInvalid");
        var incoming = groups.ToDictionary(x => x.Key, x => x.Single(), StringComparer.Ordinal);
        if (incoming.Count != allowed.Count || incoming.Keys.Any(x => !allowed.Contains(x))) throw new BusinessException("Bus:DepartureChecksInvalid");
        var existing = await db.DepartureChecks.Where(x => x.DepartureId == id).ToListAsync(ct);
        foreach (var check in incoming.Values)
        {
            var entity = existing.SingleOrDefault(x => x.CheckType == check.CheckType);
            if (entity is null) db.DepartureChecks.Add(new DepartureCheck(Guid.NewGuid(), id, check.CheckType, check.IsPassed, check.Note));
            else entity.Record(check.IsPassed, check.Note);
        }
        AddMutationAudit("Departure.ReadinessCheck", trip.Id, nameof(DepartureCheck), trip.StationId);
        await db.SaveChangesAsync(ct); return await GetDepartureAsync(id, ct);
    }

    public async Task<DepartureDto> TransitionDepartureAsync(Guid id, string action, CancellationToken ct)
    {
        var trip = await db.DepartureTrips.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Bus:DepartureNotFound");
        await scope.EnsureStationAsync(trip.StationId, ct);
        await EnsureOpenDayAsync(trip.StationId, trip.BusinessDate, ct);
        switch (action.Trim().ToLowerInvariant())
        {
            case "ready":
                if (!await HasCurrentReadinessAsync(trip, ct)) { trip.Block(); break; }
                if (trip.Status == BusStatuses.Blocked) trip.ResetToRegistered();
                trip.MarkReady(); break;
            case "depart":
                if (!await HasCurrentReadinessAsync(trip, ct)) { trip.Block(); break; }
                trip.MarkDeparted(DateTime.UtcNow); break;
            case "complete": trip.Complete(); break;
            case "cancel": trip.Cancel(); break;
            case "noservice": trip.MarkNoService(); break;
            default: throw new BusinessException("Bus:UnsupportedDepartureAction");
        }
        db.OutboxMessages.Add(BusOutbox.Create(new BusDepartureChangedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, null,
            trip.Id, trip.StationId, trip.Status), Guid.NewGuid().ToString("N")));
        await db.SaveChangesAsync(ct); return await GetDepartureAsync(id, ct);
    }

    public async Task<TariffDto> CreateTariffAsync(CreateTariffDto input, CancellationToken ct)
    {
        await scope.EnsureStationAsync(input.StationId, ct); await RequireStationAsync(input.StationId, ct);
        if (input.RouteId.HasValue && !await db.FixedRoutes.AnyAsync(x => x.Id == input.RouteId && x.IsActive &&
            (x.StationId == input.StationId || (scope.IsGlobal && !x.StationId.HasValue)), ct))
            throw new BusinessException("Bus:RouteNotFound");
        var tariff = new Tariff(Guid.NewGuid(), input.StationId, input.RouteId, input.VehicleType, input.FeeType, input.Amount, input.EffectiveFrom, input.EffectiveTo);
        db.Tariffs.Add(tariff); await db.SaveChangesAsync(ct); return ToDto(tariff);
    }

    public async Task<PagedBusDto<ParkingTariffDto>> GetParkingTariffsAsync(Guid? stationId, string? vehicleType,
        DateTime? effectiveOn, int skip, int take, CancellationToken ct)
    {
        if (stationId.HasValue) await scope.EnsureStationAsync(stationId.Value, ct);
        var stationIds = await scope.GetStationIdsAsync(ct);
        var query = db.ParkingTariffs.AsNoTracking();
        if (stationIds is not null) query = query.Where(x => stationIds.Contains(x.StationId));
        if (stationId.HasValue) query = query.Where(x => x.StationId == stationId.Value);
        if (!string.IsNullOrWhiteSpace(vehicleType)) query = query.Where(x => x.VehicleType == vehicleType.Trim());
        if (effectiveOn.HasValue)
        {
            var date = effectiveOn.Value.Date;
            query = query.Where(x => x.IsActive && x.EffectiveFrom <= date && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= date));
        }
        var total = await query.LongCountAsync(ct);
        var items = await query.OrderBy(x => x.StationId).ThenBy(x => x.VehicleType).ThenByDescending(x => x.EffectiveFrom)
            .Skip(Math.Max(skip, 0)).Take(Math.Clamp(take, 1, 100)).ToListAsync(ct);
        return new(total, items.Select(ToDto).ToList());
    }

    public async Task<ParkingTariffDto> CreateParkingTariffAsync(CreateParkingTariffDto input, CancellationToken ct)
    {
        return await ExecuteMutationTransactionAsync(async () =>
        {
            await scope.EnsureStationAsync(input.StationId, ct); await RequireStationAsync(input.StationId, ct);
            await LockParkingTariffConfigurationAsync(input.StationId, ct);
            var vehicleType = input.VehicleType.Trim();
            var effectiveFrom = input.EffectiveFrom.Date;
            var effectiveTo = input.EffectiveTo?.Date;
            var overlap = await db.ParkingTariffs.AnyAsync(x => x.StationId == input.StationId && x.IsActive &&
                x.VehicleType == vehicleType && x.EffectiveFrom <= (effectiveTo ?? DateTime.MaxValue.Date) &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= effectiveFrom), ct);
            if (overlap) throw new BusinessException("Bus:ParkingTariffOverlap");
            var tariff = new ParkingTariff(Guid.NewGuid(), input.StationId, vehicleType, input.BillingUnitMinutes,
                input.RatePerUnit, input.MinimumCharge, input.Description, input.EffectiveFrom, input.EffectiveTo);
            db.ParkingTariffs.Add(tariff);
            return ToDto(tariff);
        }, ct);
    }

    public async Task<PagedBusDto<ParkingSessionDto>> GetParkingSessionsAsync(Guid? stationId, DateTime? from, DateTime? to,
        string? status, string? vehiclePlateNumber, int skip, int take, CancellationToken ct)
    {
        if (stationId.HasValue) await scope.EnsureStationAsync(stationId.Value, ct);
        var stationIds = await scope.GetStationIdsAsync(ct);
        var query = db.ParkingSessions.AsNoTracking();
        if (stationIds is not null) query = query.Where(x => stationIds.Contains(x.StationId));
        if (stationId.HasValue) query = query.Where(x => x.StationId == stationId.Value);
        if (from.HasValue) query = query.Where(x => x.BusinessDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(x => x.BusinessDate <= to.Value.Date);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim());
        if (!string.IsNullOrWhiteSpace(vehiclePlateNumber)) query = query.Where(x => x.VehiclePlateNumber == ParkingSession.NormalizePlate(vehiclePlateNumber));
        var total = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(x => x.BusinessDate).ThenByDescending(x => x.ArrivalUtc)
            .Skip(Math.Max(skip, 0)).Take(Math.Clamp(take, 1, 100)).ToListAsync(ct);
        var sessionIds = items.Select(x => x.Id).ToArray();
        var receiptIds = sessionIds.Length == 0
            ? new Dictionary<Guid, Guid>()
            : await db.RevenueReceipts.AsNoTracking()
                .Where(x => x.ParkingSessionId.HasValue && sessionIds.Contains(x.ParkingSessionId.Value))
                .ToDictionaryAsync(x => x.ParkingSessionId!.Value, x => x.Id, ct);
        return new(total, items.Select(x => ToDto(x, receiptIds.GetValueOrDefault(x.Id))).ToList());
    }

    public async Task<ParkingSessionDto> CreateParkingSessionAsync(CreateParkingSessionDto input, CancellationToken ct)
    {
        return await ExecuteMutationTransactionAsync(async () =>
        {
            await scope.EnsureStationAsync(input.StationId, ct); await RequireStationAsync(input.StationId, ct);
            await EnsureParkingBusinessDateAsync(input.StationId, input.BusinessDate, input.ArrivalUtc, ct);
            var shiftCode = ParkingSession.NormalizeShift(input.ShiftCode);
            await EnsureOpenDayAsync(input.StationId, input.BusinessDate, ct);
            await EnsureShiftAcceptsSourceMutationAsync(input.StationId, input.BusinessDate, shiftCode, ct);
            var plate = ParkingSession.NormalizePlate(input.VehiclePlateNumber);
            if (await db.ParkingSessions.AnyAsync(x => x.StationId == input.StationId &&
                x.BusinessDate == BusDates.BusinessDate(input.BusinessDate) && x.VehiclePlateNumber == plate &&
                x.Status == BusStatuses.ParkingOpen, ct))
                throw new BusinessException("Bus:ParkingSessionAlreadyOpen");

            var tariff = await ResolveParkingTariffAsync(input.StationId, input.VehicleType, input.BusinessDate, input.ParkingTariffId, ct);
            var session = new ParkingSession(Guid.NewGuid(), input.StationId, input.BusinessDate, shiftCode, plate,
                input.VehicleType, input.ArrivalUtc, tariff.Id, tariff.BillingUnitMinutes, tariff.RatePerUnit,
                tariff.MinimumCharge, tariff.Description);
            db.ParkingSessions.Add(session);
            AddParkingOutbox(session, null);
            AddMutationAudit("ParkingSession.Create", session.Id, nameof(ParkingSession), session.StationId);
            return ToDto(session, null);
        }, ct);
    }

    public async Task<ParkingSessionDto> CloseParkingSessionAsync(Guid id, CloseParkingSessionDto input, CancellationToken ct)
    {
        try
        {
            return await ExecuteMutationTransactionAsync(async () =>
            {
                var session = await db.ParkingSessions.SingleOrDefaultAsync(x => x.Id == id, ct)
                    ?? throw new BusinessException("Bus:ParkingSessionNotFound");
                await scope.EnsureStationAsync(session.StationId, ct);
                await LockBusinessDayAsync(session.StationId, session.BusinessDate, ct);
                await db.Entry(session).ReloadAsync(ct);
                if (session.Status == BusStatuses.ParkingClosed) return await ToParkingSessionDtoAsync(session, ct);
                if (session.Status == BusStatuses.ParkingCancelled) throw new BusinessException("Bus:ParkingSessionImmutable");
                await EnsureOpenDayAsync(session.StationId, session.BusinessDate, ct);
                await EnsureShiftAcceptsSourceMutationAsync(session.StationId, session.BusinessDate, session.ShiftCode, ct);

                var exitUtc = input.ExitUtc ?? DateTime.UtcNow;
                var quote = session.Quote(exitUtc);
                var receipt = RevenueReceipt.CreateParking(Guid.NewGuid(), MakeReceiptNumber(session.BusinessDate), session, UserId);
                receipt.AddLine(Guid.NewGuid(), $"{session.TariffDescription} - {quote.BilledUnits} đơn vị", 1, quote.Amount);
                receipt.Issue(DateTime.UtcNow);
                session.Close(exitUtc);

                db.RevenueReceipts.Add(receipt);
                foreach (var line in receipt.Lines) db.RevenueLines.Add(line);
                db.OutboxMessages.Add(BusOutbox.Create(new BusRevenueRecordedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, null,
                    receipt.Id, receipt.StationId, receipt.TotalAmount, receipt.SourceType), Guid.NewGuid().ToString("N")));
                AddParkingOutbox(session, receipt.Id);
                AddMutationAudit("ParkingSession.Close", session.Id, nameof(ParkingSession), session.StationId);
                return ToDto(session, receipt.Id);
            }, ct);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception, "IX_RevenueReceipts_ParkingSessionId"))
        {
            db.ChangeTracker.Clear();
            var committed = await db.ParkingSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
            if (committed is null || committed.Status != BusStatuses.ParkingClosed)
                throw;
            return await ToParkingSessionDtoAsync(committed, ct);
        }
    }

    public async Task<ParkingSessionDto> CancelParkingSessionAsync(Guid id, CancelParkingSessionDto input, CancellationToken ct)
    {
        return await ExecuteMutationTransactionAsync(async () =>
        {
            var session = await db.ParkingSessions.SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new BusinessException("Bus:ParkingSessionNotFound");
            await scope.EnsureStationAsync(session.StationId, ct);
            await LockBusinessDayAsync(session.StationId, session.BusinessDate, ct);
            await db.Entry(session).ReloadAsync(ct);
            await EnsureOpenDayAsync(session.StationId, session.BusinessDate, ct);
            await EnsureShiftAcceptsSourceMutationAsync(session.StationId, session.BusinessDate, session.ShiftCode, ct);
            session.Cancel(input.Reason);
            AddParkingOutbox(session, null);
            AddMutationAudit("ParkingSession.Cancel", session.Id, nameof(ParkingSession), session.StationId);
            return ToDto(session, null);
        }, ct);
    }

    public async Task<RevenueReceiptDto> CreateReceiptAsync(CreateRevenueReceiptDto input, CancellationToken ct)
    {
        await scope.EnsureStationAsync(input.StationId, ct); await RequireStationAsync(input.StationId, ct);
        if (input.Lines is null || input.Lines.Count == 0) throw new BusinessException("Bus:ReceiptLinesRequired");
        var idempotencyKey = input.IdempotencyKey?.Trim();
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var previous = await db.RevenueReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, ct);
            if (previous is not null)
            {
                await EnsureReceiptIdempotencyMatchesAsync(previous, input, ct);
                return await GetReceiptAsync(previous.Id, ct);
            }
        }
        await EnsureOpenDayAsync(input.StationId, input.BusinessDate, ct);
        await EnsureShiftAcceptsSourceMutationAsync(input.StationId, input.BusinessDate, input.ShiftCode, ct);
        await ValidateRevenueSourceAsync(input, ct);
        DepartureTrip? departure = null;
        if (input.DepartureId.HasValue)
        {
            departure = await db.DepartureTrips.SingleOrDefaultAsync(x => x.Id == input.DepartureId && x.StationId == input.StationId, ct)
                ?? throw new BusinessException("Bus:DepartureNotFound");
        }
        var receipt = new RevenueReceipt(Guid.NewGuid(), MakeReceiptNumber(input.BusinessDate), input.StationId, input.BusinessDate,
            input.ShiftCode, input.SourceType, input.DepartureId, input.OperatorId, UserId, idempotencyKey,
            input.SourceReference, input.VehiclePlateNumber, input.PremisesUnitId);
        foreach (var line in input.Lines)
        {
            if (line.TariffId.HasValue)
            {
                var businessDate = input.BusinessDate.Date;
                var tariff = await db.Tariffs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == line.TariffId &&
                    x.StationId == input.StationId && x.IsActive && x.EffectiveFrom <= businessDate &&
                    (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= businessDate), ct)
                    ?? throw new BusinessException("Bus:TariffNotEffective");
                if (departure is not null && tariff.RouteId.HasValue && tariff.RouteId != departure.RouteId)
                    throw new BusinessException("Bus:TariffRouteMismatch");
                if (line.UnitAmount != tariff.Amount)
                    throw new BusinessException("Bus:TariffSnapshotMismatch");
            }
            receipt.AddLine(Guid.NewGuid(), line.Description, line.Quantity, line.UnitAmount, line.TariffId);
        }
        receipt.Issue(DateTime.UtcNow); db.RevenueReceipts.Add(receipt);
        foreach (var line in receipt.Lines) db.RevenueLines.Add(line);
        db.OutboxMessages.Add(BusOutbox.Create(new BusRevenueRecordedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, null,
            receipt.Id, receipt.StationId, receipt.TotalAmount, receipt.SourceType), Guid.NewGuid().ToString("N")));
        AddMutationAudit("RevenueReceipt.Issue", receipt.Id, nameof(RevenueReceipt), receipt.StationId);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            // A concurrent request may win the unique idempotency-key constraint between
            // the lookup and insert. Re-read the winner and return it only when the payload
            // is identical; a reused key can never silently create a different receipt.
            db.ChangeTracker.Clear();
            var concurrent = await db.RevenueReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, ct);
            if (concurrent is null) throw;
            await EnsureReceiptIdempotencyMatchesAsync(concurrent, input, ct);
            return await GetReceiptAsync(concurrent.Id, ct);
        }
        return await GetReceiptAsync(receipt.Id, ct);
    }

    public async Task<PagedBusDto<RevenueReceiptDto>> GetReceiptsAsync(Guid? stationId, DateTime? from, DateTime? to, string? sourceType, int skip, int take, CancellationToken ct)
    {
        if (stationId.HasValue) await scope.EnsureStationAsync(stationId.Value, ct);
        var stationIds = await scope.GetStationIdsAsync(ct); var query = db.RevenueReceipts.AsNoTracking();
        if (stationIds is not null) query = query.Where(x => stationIds.Contains(x.StationId));
        if (stationId.HasValue) query = query.Where(x => x.StationId == stationId);
        if (from.HasValue) query = query.Where(x => x.BusinessDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(x => x.BusinessDate <= to.Value.Date);
        if (!string.IsNullOrWhiteSpace(sourceType)) query = query.Where(x => x.SourceType == sourceType);
        var total = await query.LongCountAsync(ct); var receipts = await query.OrderByDescending(x => x.BusinessDate).ThenByDescending(x => x.CreationTime).Skip(Math.Max(skip, 0)).Take(Math.Clamp(take, 1, 100)).ToListAsync(ct);
        var ids = receipts.Select(x => x.Id).ToArray(); var lines = await db.RevenueLines.AsNoTracking().Where(x => ids.Contains(x.ReceiptId)).ToListAsync(ct);
        return new(total, receipts.Select(x => ToDto(x, lines.Where(l => l.ReceiptId == x.Id))).ToList());
    }

    public async Task<ExpenseDto> CreateExpenseAsync(CreateExpenseDto input, CancellationToken ct)
    {
        await scope.EnsureStationAsync(input.StationId, ct); await RequireStationAsync(input.StationId, ct);
        await EnsureOpenDayAsync(input.StationId, input.BusinessDate, ct);
        await EnsureShiftAcceptsSourceMutationAsync(input.StationId, input.BusinessDate, input.ShiftCode, ct);
        var expense = new ExpenseEntry(Guid.NewGuid(), input.StationId, input.BusinessDate, input.ShiftCode, input.Category, input.Amount, input.Description, input.DocumentId, UserId);
        db.ExpenseEntries.Add(expense);
        db.OutboxMessages.Add(BusOutbox.Create(new BusExpenseChangedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, null,
            expense.Id, expense.StationId, expense.Amount, expense.Status), Guid.NewGuid().ToString("N")));
        AddMutationAudit("Expense.Create", expense.Id, nameof(ExpenseEntry), expense.StationId);
        await db.SaveChangesAsync(ct); return ToDto(expense);
    }

    public async Task<PagedBusDto<ExpenseDto>> GetExpensesAsync(Guid? stationId, DateTime? from, DateTime? to,
        string? status, int skip, int take, CancellationToken ct)
    {
        var stationIds = await scope.GetStationIdsAsync(ct);
        if (stationId.HasValue) await scope.EnsureStationAsync(stationId.Value, ct);
        var query = db.ExpenseEntries.AsNoTracking();
        if (stationIds is not null) query = query.Where(x => stationIds.Contains(x.StationId));
        if (stationId.HasValue) query = query.Where(x => x.StationId == stationId);
        if (from.HasValue) query = query.Where(x => x.BusinessDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(x => x.BusinessDate <= to.Value.Date);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var total = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(x => x.BusinessDate).ThenByDescending(x => x.CreationTime)
            .Skip(Math.Max(skip, 0)).Take(Math.Clamp(take, 1, 100)).ToListAsync(ct);
        return new(total, items.Select(ToDto).ToList());
    }

    public async Task<ExpenseDto> SubmitExpenseAsync(Guid id, CancellationToken ct)
    {
        var expense = await db.ExpenseEntries.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Bus:ExpenseNotFound");
        await scope.EnsureStationAsync(expense.StationId, ct); await EnsureOpenDayAsync(expense.StationId, expense.BusinessDate, ct); expense.Submit();
        db.OutboxMessages.Add(BusOutbox.Create(new BusExpenseChangedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, null,
            expense.Id, expense.StationId, expense.Amount, expense.Status), Guid.NewGuid().ToString("N")));
        AddMutationAudit("Expense.Submit", expense.Id, nameof(ExpenseEntry), expense.StationId);
        await db.SaveChangesAsync(ct); return ToDto(expense);
    }

    public async Task<ExpenseDto> ApproveExpenseAsync(Guid id, CancellationToken ct)
    {
        var expense = await db.ExpenseEntries.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Bus:ExpenseNotFound");
        await scope.EnsureStationAsync(expense.StationId, ct); await EnsureOpenDayAsync(expense.StationId, expense.BusinessDate, ct); expense.Approve(UserId);
        db.OutboxMessages.Add(BusOutbox.Create(new BusExpenseChangedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, null,
            expense.Id, expense.StationId, expense.Amount, expense.Status), Guid.NewGuid().ToString("N")));
        AddMutationAudit("Expense.Approve", expense.Id, nameof(ExpenseEntry), expense.StationId);
        await db.SaveChangesAsync(ct); return ToDto(expense);
    }

    public async Task<PremisesUnitDto> CreatePremisesUnitAsync(CreatePremisesUnitDto input, CancellationToken ct)
    {
        await scope.EnsureStationAsync(input.StationId, ct); await RequireStationAsync(input.StationId, ct);
        var unit = new PremisesUnit(Guid.NewGuid(), input.StationId, input.Code, input.Name, input.AreaSquareMeters, input.Location); db.PremisesUnits.Add(unit); await db.SaveChangesAsync(ct); return ToDto(unit);
    }

    public async Task<PagedBusDto<PremisesUnitDto>> GetPremisesUnitsAsync(Guid? stationId, string? filter, int skip, int take, CancellationToken ct)
    {
        var stationIds = await scope.GetStationIdsAsync(ct);
        if (stationId.HasValue) await scope.EnsureStationAsync(stationId.Value, ct);
        var query = db.PremisesUnits.AsNoTracking();
        if (stationIds is not null) query = query.Where(x => stationIds.Contains(x.StationId));
        if (stationId.HasValue) query = query.Where(x => x.StationId == stationId);
        if (!string.IsNullOrWhiteSpace(filter)) query = query.Where(x => x.Code.Contains(filter) || x.Name.Contains(filter));
        var total = await query.LongCountAsync(ct);
        var items = await query.OrderBy(x => x.Code).Skip(Math.Max(skip, 0)).Take(Math.Clamp(take, 1, 100)).ToListAsync(ct);
        return new(total, items.Select(ToDto).ToList());
    }

    public async Task<LeaseContractDto> CreateLeaseAsync(CreateLeaseContractDto input, CancellationToken ct)
    {
        await scope.EnsureStationAsync(input.StationId, ct); await RequireStationAsync(input.StationId, ct);
        if (!await db.PremisesUnits.AnyAsync(x => x.Id == input.PremisesUnitId && x.StationId == input.StationId, ct)) throw new BusinessException("Bus:PremisesNotFound");
        var lease = new LeaseContract(Guid.NewGuid(), input.StationId, input.PremisesUnitId, input.TenantName, input.StartDate, input.EndDate, input.RentAmount, input.RentPeriod); db.LeaseContracts.Add(lease); await db.SaveChangesAsync(ct); return ToDto(lease);
    }

    public async Task<PagedBusDto<LeaseContractDto>> GetLeasesAsync(Guid? stationId, DateTime? from, DateTime? to,
        string? status, int skip, int take, CancellationToken ct)
    {
        var stationIds = await scope.GetStationIdsAsync(ct);
        if (stationId.HasValue) await scope.EnsureStationAsync(stationId.Value, ct);
        var query = db.LeaseContracts.AsNoTracking();
        if (stationIds is not null) query = query.Where(x => stationIds.Contains(x.StationId));
        if (stationId.HasValue) query = query.Where(x => x.StationId == stationId);
        if (from.HasValue) query = query.Where(x => x.EndDate >= from.Value.Date);
        if (to.HasValue) query = query.Where(x => x.StartDate <= to.Value.Date);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var total = await query.LongCountAsync(ct);
        var items = await query.OrderBy(x => x.EndDate).Skip(Math.Max(skip, 0)).Take(Math.Clamp(take, 1, 100)).ToListAsync(ct);
        return new(total, items.Select(ToDto).ToList());
    }

    public async Task<SettlementDto> CreateShiftSettlementAsync(CreateShiftSettlementDto input, CancellationToken ct)
    {
        await scope.EnsureStationAsync(input.StationId, ct);
        await RequireStationAsync(input.StationId, ct);
        var date = BusDates.BusinessDate(input.BusinessDate);
        await EnsureOpenDayAsync(input.StationId, date, ct);
        var revenue = await db.RevenueReceipts.Where(x => x.StationId == input.StationId && x.BusinessDate == date && x.ShiftCode == input.ShiftCode && x.Status == BusStatuses.Issued).SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0;
        var expense = await db.ExpenseEntries.Where(x => x.StationId == input.StationId && x.BusinessDate == date && x.ShiftCode == input.ShiftCode && x.Status == BusStatuses.Approved).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        var settlement = new ShiftSettlement(Guid.NewGuid(), input.StationId, date, input.ShiftCode, revenue, expense, UserId);
        db.ShiftSettlements.Add(settlement); AddSettlementOutbox(settlement); AddMutationAudit("ShiftSettlement.Create", settlement.Id, nameof(ShiftSettlement), settlement.StationId);
        await db.SaveChangesAsync(ct); return ToDto(settlement);
    }

    public async Task<SettlementDto> TransitionSettlementAsync(Guid id, string action, CancellationToken ct)
    {
        var settlement = await db.ShiftSettlements.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Bus:SettlementNotFound");
        await scope.EnsureStationAsync(settlement.StationId, ct); await EnsureOpenDayAsync(settlement.StationId, settlement.BusinessDate, ct); var user = UserId;
        var totals = await GetShiftTotalsAsync(settlement.StationId, settlement.BusinessDate, settlement.ShiftCode, ct);
        settlement.RefreshTotals(totals.Revenue, totals.Expense);
        switch (action.Trim().ToLowerInvariant())
        {
            case "submit": settlement.Submit(user); break;
            case "check": settlement.Check(user); break;
            case "approve": settlement.Approve(user); break;
            case "close": settlement.Close(); break;
            default: throw new BusinessException("Bus:UnsupportedSettlementAction");
        }
        AddSettlementOutbox(settlement);
        AddMutationAudit($"ShiftSettlement.{action.Trim().ToUpperInvariant()}", settlement.Id, nameof(ShiftSettlement), settlement.StationId);
        await db.SaveChangesAsync(ct); return ToDto(settlement);
    }

    public async Task<DailyCloseDto> CloseDailyAsync(CloseDailyDto input, CancellationToken ct)
    {
        return await ExecuteMutationTransactionAsync(async () =>
        {
            await scope.EnsureStationAsync(input.StationId, ct); await RequireStationAsync(input.StationId, ct); var date = BusDates.BusinessDate(input.BusinessDate);
            await LockBusinessDayAsync(input.StationId, date, ct);
            if (await db.ParkingSessions.AnyAsync(x => x.StationId == input.StationId && x.BusinessDate == date &&
                x.Status == BusStatuses.ParkingOpen, ct))
                throw new BusinessException("Bus:OpenParkingSessions");
            if (await db.ExpenseEntries.AnyAsync(x => x.StationId == input.StationId && x.BusinessDate == date && x.Status != BusStatuses.Approved, ct))
                throw new BusinessException("Bus:ExpensesNotApproved");
            var shifts = await db.ShiftSettlements.Where(x => x.StationId == input.StationId && x.BusinessDate == date).ToListAsync(ct);
            if (shifts.Count == 0 || shifts.Any(x => x.Status != BusStatuses.Approved && x.Status != BusStatuses.Closed)) throw new BusinessException("Bus:ShiftsNotApproved");
            var settledShiftCodes = shifts.Where(x => x.Status is BusStatuses.Approved or BusStatuses.Closed)
                .Select(x => x.ShiftCode).ToArray();
            if (await db.RevenueReceipts.AnyAsync(x => x.StationId == input.StationId && x.BusinessDate == date &&
                x.Status == BusStatuses.Issued && !settledShiftCodes.Contains(x.ShiftCode), ct) ||
                await db.ExpenseEntries.AnyAsync(x => x.StationId == input.StationId && x.BusinessDate == date &&
                    x.Status == BusStatuses.Approved && !settledShiftCodes.Contains(x.ShiftCode), ct))
                throw new BusinessException("Bus:ShiftsNotApproved");
            var close = await db.DailyCloses.SingleOrDefaultAsync(x => x.StationId == input.StationId && x.BusinessDate == date, ct);
            if (close is null)
            {
                close = new DailyClose(Guid.NewGuid(), input.StationId, date, shifts.Sum(x => x.TotalRevenue), shifts.Sum(x => x.TotalExpense), shifts.Count); db.DailyCloses.Add(close);
            }
            close.Close(UserId, DateTime.UtcNow);
            db.OutboxMessages.Add(BusOutbox.Create(new BusReconciliationClosedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, null,
                close.Id, close.StationId, close.BusinessDate), Guid.NewGuid().ToString("N")));
            AddMutationAudit("DailyClose.Close", close.Id, nameof(DailyClose), close.StationId);
            return new DailyCloseDto(close.Id, close.StationId, close.BusinessDate, close.TotalRevenue, close.TotalExpense,
                close.ShiftCount, close.Status, close.ClosedByUserId, close.ClosedAtUtc);
        }, ct);
    }

    public async Task<AdjustmentDto> CreateAdjustmentAsync(CreateAdjustmentDto input, CancellationToken ct)
    {
        var userId = UserId ?? throw new AbpAuthorizationException("Authenticated user required.");
        await scope.EnsureStationAsync(input.StationId, ct); await RequireStationAsync(input.StationId, ct);
        if ((input.ReceiptId.HasValue) == (input.ExpenseId.HasValue)) throw new BusinessException("Bus:AdjustmentTargetInvalid");

        Guid targetStationId;
        DateTime targetDate;
        if (input.ReceiptId.HasValue)
        {
            var receipt = await db.RevenueReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.ReceiptId.Value, ct)
                ?? throw new BusinessException("Bus:ReceiptNotFound");
            if (receipt.Status != BusStatuses.Issued) throw new BusinessException("Bus:AdjustmentReceiptInvalid");
            targetStationId = receipt.StationId; targetDate = receipt.BusinessDate;
        }
        else
        {
            var expense = await db.ExpenseEntries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.ExpenseId!.Value, ct)
                ?? throw new BusinessException("Bus:ExpenseNotFound");
            if (expense.Status != BusStatuses.Approved) throw new BusinessException("Bus:AdjustmentExpenseInvalid");
            targetStationId = expense.StationId; targetDate = expense.BusinessDate;
        }

        if (targetStationId != input.StationId) throw new BusinessException("Bus:AdjustmentStationMismatch");
        await EnsureClosedDayAsync(targetStationId, targetDate, ct);
        var adjustment = new AdjustmentEntry(Guid.NewGuid(), input.StationId, input.ReceiptId, input.ExpenseId, input.Amount, input.Reason, userId);
        db.AdjustmentEntries.Add(adjustment);
        AddAdjustmentOutbox(adjustment);
        AddMutationAudit("Adjustment.Create", adjustment.Id, nameof(AdjustmentEntry), adjustment.StationId);
        await db.SaveChangesAsync(ct);
        return ToDto(adjustment);
    }

    public async Task<PagedBusDto<AdjustmentDto>> GetAdjustmentsAsync(Guid? stationId, string? status, DateTime? from,
        DateTime? to, int skip, int take, CancellationToken ct)
    {
        var stationIds = await scope.GetStationIdsAsync(ct);
        if (stationId.HasValue) await scope.EnsureStationAsync(stationId.Value, ct);
        var query = db.AdjustmentEntries.AsNoTracking();
        if (stationIds is not null) query = query.Where(x => stationIds.Contains(x.StationId));
        if (stationId.HasValue) query = query.Where(x => x.StationId == stationId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (from.HasValue) query = query.Where(x => x.CreationTime >= from.Value.Date);
        if (to.HasValue) query = query.Where(x => x.CreationTime < to.Value.Date.AddDays(1));
        var total = await query.LongCountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreationTime).Skip(Math.Max(skip, 0))
            .Take(Math.Clamp(take, 1, 100)).ToListAsync(ct);
        return new(total, items.Select(ToDto).ToList());
    }

    public async Task<AdjustmentDto> ApproveAdjustmentAsync(Guid id, CancellationToken ct)
    {
        var adjustment = await db.AdjustmentEntries.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new BusinessException("Bus:AdjustmentNotFound");
        await scope.EnsureStationAsync(adjustment.StationId, ct);
        var targetDate = adjustment.ReceiptId.HasValue
            ? await db.RevenueReceipts.Where(x => x.Id == adjustment.ReceiptId.Value).Select(x => (DateTime?)x.BusinessDate).SingleOrDefaultAsync(ct)
            : await db.ExpenseEntries.Where(x => x.Id == adjustment.ExpenseId!.Value).Select(x => (DateTime?)x.BusinessDate).SingleOrDefaultAsync(ct);
        if (!targetDate.HasValue) throw new BusinessException("Bus:AdjustmentTargetNotFound");
        await EnsureClosedDayAsync(adjustment.StationId, targetDate.Value, ct);
        adjustment.Approve(UserId ?? throw new AbpAuthorizationException("Authenticated user required."), DateTime.UtcNow);
        AddAdjustmentOutbox(adjustment);
        AddMutationAudit("Adjustment.Approve", adjustment.Id, nameof(AdjustmentEntry), adjustment.StationId);
        await db.SaveChangesAsync(ct);
        return ToDto(adjustment);
    }

    public async Task<DashboardSummaryDto> GetDashboardAsync(DateTime from, DateTime to, Guid? stationId, CancellationToken ct)
    {
        if (stationId.HasValue) await scope.EnsureStationAsync(stationId.Value, ct);
        var permitted = await scope.GetStationIdsAsync(ct);
        var dashboardStationIds = stationId.HasValue
            ? [stationId.Value]
            : permitted is not null
                ? permitted.ToArray()
                : await db.BusStations.AsNoTracking().Where(x => x.IsActive).Select(x => x.Id).ToArrayAsync(ct);
        var start = BusDates.BusinessDate(from); var end = BusDates.BusinessDate(to);
        var receipts = db.RevenueReceipts.AsNoTracking().Where(x => dashboardStationIds.Contains(x.StationId) &&
            x.BusinessDate >= start && x.BusinessDate <= end && x.Status == BusStatuses.Issued);
        var expenses = db.ExpenseEntries.AsNoTracking().Where(x => dashboardStationIds.Contains(x.StationId) &&
            x.BusinessDate >= start && x.BusinessDate <= end && x.Status == BusStatuses.Approved);
        var trips = db.DepartureTrips.AsNoTracking().Where(x => dashboardStationIds.Contains(x.StationId) &&
            x.BusinessDate >= start && x.BusinessDate <= end);
        var shifts = db.ShiftSettlements.AsNoTracking().Where(x => dashboardStationIds.Contains(x.StationId) &&
            x.BusinessDate >= start && x.BusinessDate <= end && x.Status != BusStatuses.Closed);
        var receiptAdjustments = from adjustment in db.AdjustmentEntries.AsNoTracking()
                                 join receipt in db.RevenueReceipts.AsNoTracking() on adjustment.ReceiptId equals receipt.Id
                                 where adjustment.Status == BusStatuses.Approved && dashboardStationIds.Contains(adjustment.StationId) &&
                                     receipt.BusinessDate >= start && receipt.BusinessDate <= end
                                 select adjustment;
        var expenseAdjustments = from adjustment in db.AdjustmentEntries.AsNoTracking()
                                 join expense in db.ExpenseEntries.AsNoTracking() on adjustment.ExpenseId equals expense.Id
                                 where adjustment.Status == BusStatuses.Approved && dashboardStationIds.Contains(adjustment.StationId) &&
                                     expense.BusinessDate >= start && expense.BusinessDate <= end
                                 select adjustment;

        var receiptBase = await receipts.GroupBy(x => x.StationId).Select(g => new { g.Key, Amount = g.Sum(x => x.TotalAmount), Count = g.Count() }).ToDictionaryAsync(x => x.Key, ct);
        var expenseBase = await expenses.GroupBy(x => x.StationId).Select(g => new { g.Key, Amount = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Key, ct);
        var receiptAdjustment = await receiptAdjustments.GroupBy(x => x.StationId).Select(g => new { g.Key, Amount = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Key, ct);
        var expenseAdjustment = await expenseAdjustments.GroupBy(x => x.StationId).Select(g => new { g.Key, Amount = g.Sum(x => x.Amount) }).ToDictionaryAsync(x => x.Key, ct);
        var tripCounts = await trips.GroupBy(x => x.StationId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, ct);
        var openShiftCounts = await shifts.GroupBy(x => x.StationId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, ct);
        var stationRows = dashboardStationIds.OrderBy(x => x).Select(id => new StationDashboardRowDto(id,
            receiptBase.GetValueOrDefault(id)?.Amount ?? 0, expenseBase.GetValueOrDefault(id)?.Amount ?? 0,
            tripCounts.GetValueOrDefault(id)?.Count ?? 0, receiptBase.GetValueOrDefault(id)?.Count ?? 0,
            openShiftCounts.GetValueOrDefault(id)?.Count ?? 0, receiptAdjustment.GetValueOrDefault(id)?.Amount ?? 0,
            expenseAdjustment.GetValueOrDefault(id)?.Amount ?? 0)).ToList();

        var threshold = DateTime.UtcNow.Date.AddDays(30);
        var docs = db.VehicleLegalDocuments.AsNoTracking().Where(x => x.IsActive && x.ExpiresOn <= threshold &&
            ((x.StationId.HasValue && dashboardStationIds.Contains(x.StationId.Value)) ||
             (!x.StationId.HasValue && db.DepartureTrips.Where(t => t.VehicleId == x.VehicleId).Select(t => t.StationId).Distinct().Count() == 1 &&
                 db.DepartureTrips.Any(t => dashboardStationIds.Contains(t.StationId) && t.VehicleId == x.VehicleId))));
        var contracts = db.CarrierContracts.AsNoTracking().Where(x => dashboardStationIds.Contains(x.StationId) && x.IsActive && x.EndDate <= threshold);
        var leases = db.LeaseContracts.AsNoTracking().Where(x => dashboardStationIds.Contains(x.StationId) && x.Status != BusStatuses.Closed && x.EndDate <= threshold);
        return new(from, to, receiptBase.Values.Sum(x => x.Amount), expenseBase.Values.Sum(x => x.Amount),
            tripCounts.Values.Sum(x => x.Count), receiptBase.Values.Sum(x => x.Count), openShiftCounts.Values.Sum(x => x.Count),
            await docs.CountAsync(ct), DateTime.UtcNow, receiptAdjustment.Values.Sum(x => x.Amount), expenseAdjustment.Values.Sum(x => x.Amount),
            await contracts.CountAsync(ct), await leases.CountAsync(ct), stationRows);
    }

    public async Task<IReadOnlyList<RevenueReportRowDto>> GetRevenueReportAsync(DateTime from, DateTime to, Guid? stationId, CancellationToken ct)
    {
        var startDate = from.Date; var endDate = to.Date;
        var stationIds = await scope.GetStationIdsAsync(ct); var query = db.RevenueReceipts.AsNoTracking().Where(x => x.BusinessDate >= startDate && x.BusinessDate <= endDate && x.Status == BusStatuses.Issued);
        if (stationIds is not null) query = query.Where(x => stationIds.Contains(x.StationId));
        if (stationId.HasValue) { await scope.EnsureStationAsync(stationId.Value, ct); query = query.Where(x => x.StationId == stationId); }
        var baseRows = await query.GroupBy(x => new { x.StationId, x.SourceType })
            .Select(g => new RevenueReportRowDto(g.Key.StationId, g.Key.SourceType, g.Sum(x => x.TotalAmount), g.Count()))
            .ToListAsync(ct);
        var adjustments = from adjustment in db.AdjustmentEntries.AsNoTracking()
                          join receipt in db.RevenueReceipts.AsNoTracking() on adjustment.ReceiptId equals receipt.Id
                          where adjustment.Status == BusStatuses.Approved && receipt.BusinessDate >= startDate && receipt.BusinessDate <= endDate
                          select new { adjustment.StationId, receipt.SourceType, adjustment.Amount };
        if (stationIds is not null) adjustments = adjustments.Where(x => stationIds.Contains(x.StationId));
        if (stationId.HasValue) adjustments = adjustments.Where(x => x.StationId == stationId);
        var adjustmentRows = await adjustments.GroupBy(x => new { x.StationId, x.SourceType })
            .Select(g => new { g.Key.StationId, g.Key.SourceType, Amount = g.Sum(x => x.Amount) }).ToListAsync(ct);
        var adjustmentMap = adjustmentRows.ToDictionary(x => (x.StationId, x.SourceType), x => x.Amount);
        return baseRows.Select(x => x with { AdjustmentAmount = adjustmentMap.GetValueOrDefault((x.StationId, x.SourceType)) })
            .OrderBy(x => x.StationId).ThenBy(x => x.SourceType).ToList();
    }

    public async Task<IReadOnlyList<DepartureReportRowDto>> GetDepartureReportAsync(DateTime from, DateTime to, Guid? stationId, CancellationToken ct)
    {
        var stationIds = await scope.GetStationIdsAsync(ct);
        if (stationId.HasValue) await scope.EnsureStationAsync(stationId.Value, ct);
        var query = db.DepartureTrips.AsNoTracking().Where(x => x.BusinessDate >= from.Date && x.BusinessDate <= to.Date);
        if (stationIds is not null) query = query.Where(x => stationIds.Contains(x.StationId));
        if (stationId.HasValue) query = query.Where(x => x.StationId == stationId);
        return await query.GroupBy(x => new { x.StationId, x.BusinessDate, x.Status })
            .Select(g => new DepartureReportRowDto(g.Key.StationId, g.Key.BusinessDate, g.Key.Status, g.Count(), g.Sum(x => x.PassengerCount)))
            .OrderBy(x => x.BusinessDate).ThenBy(x => x.StationId).ThenBy(x => x.Status).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ReconciliationReportRowDto>> GetReconciliationReportAsync(DateTime from, DateTime to, Guid? stationId, CancellationToken ct)
    {
        var startDate = from.Date; var endDate = to.Date;
        var stationIds = await scope.GetStationIdsAsync(ct);
        if (stationId.HasValue) await scope.EnsureStationAsync(stationId.Value, ct);
        var query = db.ShiftSettlements.AsNoTracking().Where(x => x.BusinessDate >= startDate && x.BusinessDate <= endDate);
        if (stationIds is not null) query = query.Where(x => stationIds.Contains(x.StationId));
        if (stationId.HasValue) query = query.Where(x => x.StationId == stationId);
        var rows = await query.OrderBy(x => x.BusinessDate).ThenBy(x => x.StationId).ThenBy(x => x.ShiftCode)
            .Select(x => new ReconciliationReportRowDto(x.StationId, x.BusinessDate, x.ShiftCode, x.Status, x.TotalRevenue, x.TotalExpense))
            .ToListAsync(ct);
        var revenueAdjustments = from adjustment in db.AdjustmentEntries.AsNoTracking()
                                 join receipt in db.RevenueReceipts.AsNoTracking() on adjustment.ReceiptId equals receipt.Id
                                 where adjustment.Status == BusStatuses.Approved && receipt.BusinessDate >= startDate && receipt.BusinessDate <= endDate
                                 group adjustment by new { adjustment.StationId, receipt.BusinessDate, receipt.ShiftCode } into grouped
                                 select new { grouped.Key.StationId, grouped.Key.BusinessDate, grouped.Key.ShiftCode, Amount = grouped.Sum(x => x.Amount) };
        var expenseAdjustments = from adjustment in db.AdjustmentEntries.AsNoTracking()
                                 join expense in db.ExpenseEntries.AsNoTracking() on adjustment.ExpenseId equals expense.Id
                                 where adjustment.Status == BusStatuses.Approved && expense.BusinessDate >= startDate && expense.BusinessDate <= endDate
                                 group adjustment by new { adjustment.StationId, expense.BusinessDate, expense.ShiftCode } into grouped
                                 select new { grouped.Key.StationId, grouped.Key.BusinessDate, grouped.Key.ShiftCode, Amount = grouped.Sum(x => x.Amount) };
        if (stationIds is not null)
        {
            revenueAdjustments = revenueAdjustments.Where(x => stationIds.Contains(x.StationId));
            expenseAdjustments = expenseAdjustments.Where(x => stationIds.Contains(x.StationId));
        }
        if (stationId.HasValue)
        {
            revenueAdjustments = revenueAdjustments.Where(x => x.StationId == stationId);
            expenseAdjustments = expenseAdjustments.Where(x => x.StationId == stationId);
        }
        var revenueMap = await revenueAdjustments.ToDictionaryAsync(x => (x.StationId, x.BusinessDate, x.ShiftCode), x => x.Amount, ct);
        var expenseMap = await expenseAdjustments.ToDictionaryAsync(x => (x.StationId, x.BusinessDate, x.ShiftCode), x => x.Amount, ct);
        return rows.Select(x => x with
        {
            RevenueAdjustmentAmount = revenueMap.GetValueOrDefault((x.StationId, x.BusinessDate, x.ShiftCode)),
            ExpenseAdjustmentAmount = expenseMap.GetValueOrDefault((x.StationId, x.BusinessDate, x.ShiftCode))
        }).ToList();
    }

    public async Task<IReadOnlyList<ComplianceReportRowDto>> GetComplianceReportAsync(Guid? stationId, DateTime? asOf, CancellationToken ct)
    {
        var stationIds = await scope.GetStationIdsAsync(ct);
        if (stationId.HasValue) await scope.EnsureStationAsync(stationId.Value, ct);
        var reportStationIds = stationId.HasValue
            ? [stationId.Value]
            : stationIds is not null
                ? stationIds.ToArray()
                : await db.BusStations.AsNoTracking().Where(x => x.IsActive).Select(x => x.Id).ToArrayAsync(ct);
        var threshold = (asOf?.Date ?? DateTime.UtcNow.Date).AddDays(30);
        var assignedDocuments = db.VehicleLegalDocuments.AsNoTracking()
            .Where(x => x.IsActive && x.ExpiresOn <= threshold && x.StationId.HasValue && reportStationIds.Contains(x.StationId.Value))
            .Select(x => new { StationId = x.StationId!.Value, x.Id });
        var legacyDocuments = from document in db.VehicleLegalDocuments.AsNoTracking()
                              join departure in db.DepartureTrips.AsNoTracking() on document.VehicleId equals departure.VehicleId
                              where document.IsActive && document.ExpiresOn <= threshold && !document.StationId.HasValue &&
                                  db.DepartureTrips.Where(t => t.VehicleId == document.VehicleId).Select(t => t.StationId).Distinct().Count() == 1 &&
                                  reportStationIds.Contains(departure.StationId)
                              select new { departure.StationId, document.Id };
        var documentCounts = await assignedDocuments.Concat(legacyDocuments).Distinct()
            .GroupBy(x => x.StationId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var contractCounts = await db.CarrierContracts.AsNoTracking()
            .Where(x => x.IsActive && x.EndDate <= threshold && reportStationIds.Contains(x.StationId))
            .GroupBy(x => x.StationId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var leaseCounts = await db.LeaseContracts.AsNoTracking()
            .Where(x => x.EndDate <= threshold && x.Status != BusStatuses.Closed && reportStationIds.Contains(x.StationId))
            .GroupBy(x => x.StationId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return reportStationIds.OrderBy(x => x).Select(id => new ComplianceReportRowDto(id,
            documentCounts.GetValueOrDefault(id), contractCounts.GetValueOrDefault(id), leaseCounts.GetValueOrDefault(id))).ToList();
    }

    private async Task<DepartureDto> GetDepartureAsync(Guid id, CancellationToken ct)
    {
        var trip = await db.DepartureTrips.AsNoTracking().SingleAsync(x => x.Id == id, ct); var checks = await db.DepartureChecks.AsNoTracking().Where(x => x.DepartureId == id).ToListAsync(ct); return ToDto(trip, checks);
    }
    private async Task<RevenueReceiptDto> GetReceiptAsync(Guid id, CancellationToken ct)
    {
        var receipt = await db.RevenueReceipts.AsNoTracking().SingleAsync(x => x.Id == id, ct); var lines = await db.RevenueLines.AsNoTracking().Where(x => x.ReceiptId == id).ToListAsync(ct); return ToDto(receipt, lines);
    }
    private async Task RequireStationAsync(Guid stationId, CancellationToken ct) { if (!await db.BusStations.AnyAsync(x => x.Id == stationId && x.IsActive, ct)) throw new BusinessException("Bus:StationNotFound"); }
    private async Task<Guid> ResolveVehicleStationAsync(Guid vehicleId, Guid? requestedStationId, CancellationToken ct)
    {
        if (requestedStationId.HasValue)
        {
            await scope.EnsureStationAsync(requestedStationId.Value, ct);
            return requestedStationId.Value;
        }

        var stations = await db.DepartureTrips.AsNoTracking().Where(x => x.VehicleId == vehicleId)
            .Select(x => x.StationId).Distinct().Take(2).ToListAsync(ct);
        if (stations.Count != 1) throw new BusinessException("Bus:VehicleStationRequired");
        return stations[0];
    }

    private async Task<Guid?> ResolveMasterDataStationAsync(Guid? requestedStationId, CancellationToken ct)
    {
        if (requestedStationId.HasValue)
        {
            await scope.EnsureStationAsync(requestedStationId.Value, ct);
            await RequireStationAsync(requestedStationId.Value, ct);
            return requestedStationId.Value;
        }
        if (!scope.IsGlobal) throw new BusinessException("Bus:StationRequiredForScopedMasterData");
        return null;
    }

    private static void EnsureMasterDataStationMatches(Guid? ownerStationId, Guid? requestedStationId)
    {
        if (ownerStationId.HasValue && ownerStationId != requestedStationId)
            throw new BusinessException("Bus:MasterDataStationMismatch");
    }

    private async Task ValidateRevenueSourceAsync(CreateRevenueReceiptDto input, CancellationToken ct)
    {
        var source = input.SourceType?.Trim();
        if (string.IsNullOrWhiteSpace(source) || !RevenueSources.Supported.Contains(source))
            throw new BusinessException("Bus:RevenueSourceInvalid");

        if (input.OperatorId.HasValue)
        {
            var operatorEntity = await db.TransportOperators.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == input.OperatorId && x.IsActive, ct)
                ?? throw new BusinessException("Bus:OperatorNotFound");
            if (operatorEntity.StationId.HasValue && operatorEntity.StationId != input.StationId)
                throw new BusinessException("Bus:MasterDataStationMismatch");
        }

        if (source == RevenueSources.FixedRoute)
        {
            if (!input.DepartureId.HasValue) throw new BusinessException("Bus:DepartureRequiredForFixedRoute");
            var departure = await db.DepartureTrips.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.DepartureId.Value, ct)
                ?? throw new BusinessException("Bus:DepartureNotFound");
            if (departure.StationId != input.StationId || departure.BusinessDate != BusDates.BusinessDate(input.BusinessDate) ||
                !string.Equals(departure.ShiftCode, input.ShiftCode, StringComparison.Ordinal))
                throw new BusinessException("Bus:RevenueDepartureMismatch");
            if (departure.Status is BusStatuses.Cancelled or BusStatuses.NoService)
                throw new BusinessException("Bus:RevenueDepartureUnavailable");
            if (input.OperatorId.HasValue && input.OperatorId != departure.OperatorId)
                throw new BusinessException("Bus:RevenueOperatorMismatch");
            return;
        }

        if (input.DepartureId.HasValue) throw new BusinessException("Bus:DepartureOnlyForFixedRoute");
        switch (source)
        {
            case RevenueSources.VisitingVehicle when string.IsNullOrWhiteSpace(input.VehiclePlateNumber):
                throw new BusinessException("Bus:VehiclePlateRequired");
            case RevenueSources.PublicBus when !input.OperatorId.HasValue:
                throw new BusinessException("Bus:OperatorRequiredForPublicBus");
            case RevenueSources.Parking:
                throw new BusinessException("Bus:ParkingSessionRequired");
            case RevenueSources.Premises:
                if (!input.PremisesUnitId.HasValue) throw new BusinessException("Bus:PremisesRequired");
                if (!await db.PremisesUnits.AnyAsync(x => x.Id == input.PremisesUnitId && x.StationId == input.StationId && x.IsActive, ct))
                    throw new BusinessException("Bus:PremisesNotFound");
                break;
        }
    }

    private async Task EnsureOpenDayAsync(Guid stationId, DateTime businessDate, CancellationToken ct)
    {
        var date = BusDates.BusinessDate(businessDate);
        await LockBusinessDayAsync(stationId, date, ct);
        if (await db.DailyCloses.AnyAsync(x => x.StationId == stationId && x.BusinessDate == date && x.Status == BusStatuses.Closed, ct))
            throw new BusinessException("Bus:DailyAlreadyClosed");
    }

    private async Task EnsureClosedDayAsync(Guid stationId, DateTime businessDate, CancellationToken ct)
    {
        var date = BusDates.BusinessDate(businessDate);
        await LockBusinessDayAsync(stationId, date, ct);
        if (!await db.DailyCloses.AnyAsync(x => x.StationId == stationId && x.BusinessDate == date && x.Status == BusStatuses.Closed, ct))
            throw new BusinessException("Bus:AdjustmentRequiresClosedDay");
    }

    private async Task EnsureShiftAcceptsSourceMutationAsync(Guid stationId, DateTime businessDate, string shiftCode, CancellationToken ct)
    {
        var date = BusDates.BusinessDate(businessDate);
        if (await db.ShiftSettlements.AnyAsync(x => x.StationId == stationId && x.BusinessDate == date &&
            x.ShiftCode == shiftCode && x.Status != BusStatuses.Draft, ct))
            throw new BusinessException("Bus:SettlementAlreadySubmitted");
    }

    private async Task<ParkingTariff> ResolveParkingTariffAsync(Guid stationId, string vehicleType, DateTime businessDate,
        Guid? tariffId, CancellationToken ct)
    {
        var normalizedVehicleType = vehicleType.Trim();
        var date = BusDates.BusinessDate(businessDate);
        if (tariffId.HasValue)
        {
            var explicitTariff = await db.ParkingTariffs.SingleOrDefaultAsync(x => x.Id == tariffId.Value &&
                x.StationId == stationId && x.IsActive, ct) ?? throw new BusinessException("Bus:ParkingTariffNotFound");
            if (!explicitTariff.IsEffectiveOn(date) || !string.Equals(explicitTariff.VehicleType, normalizedVehicleType, StringComparison.Ordinal))
                throw new BusinessException("Bus:ParkingTariffNotEffective");
            return explicitTariff;
        }

        var candidates = await db.ParkingTariffs.AsNoTracking().Where(x => x.StationId == stationId &&
            x.VehicleType == normalizedVehicleType && x.IsActive && x.EffectiveFrom <= date &&
            (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= date))
            .OrderByDescending(x => x.EffectiveFrom).Take(2).ToListAsync(ct);
        return candidates.Count switch
        {
            1 => candidates[0],
            0 => throw new BusinessException("Bus:ParkingTariffNotFound"),
            _ => throw new BusinessException("Bus:ParkingTariffAmbiguous")
        };
    }

    private async Task EnsureParkingBusinessDateAsync(Guid stationId, DateTime businessDate, DateTime arrivalUtc, CancellationToken ct)
    {
        if (arrivalUtc.Kind != DateTimeKind.Utc) throw new BusinessException("Bus:ParkingUtcRequired");
        var timeZoneId = await db.BusStations.Where(x => x.Id == stationId).Select(x => x.TimeZone).SingleAsync(ct);
        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new BusinessException("Bus:StationTimeZoneInvalid");
        }
        catch (InvalidTimeZoneException)
        {
            throw new BusinessException("Bus:StationTimeZoneInvalid");
        }

        var expectedBusinessDate = TimeZoneInfo.ConvertTimeFromUtc(arrivalUtc, timeZone).Date;
        if (expectedBusinessDate != businessDate.Date)
            throw new BusinessException("Bus:ParkingBusinessDateMismatch");
    }

    private async Task<T> ExecuteMutationTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        var ambientTransaction = db.Database.CurrentTransaction;
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        string? savepoint = null;
        if (db.Database.IsRelational() && ambientTransaction is null)
            transaction = await db.Database.BeginTransactionAsync(ct);
        else if (db.Database.IsRelational() && ambientTransaction is not null)
        {
            savepoint = $"bus_mutation_{Guid.NewGuid():N}";
            await ambientTransaction.CreateSavepointAsync(savepoint, ct);
        }
        try
        {
            var result = await action();
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            if (ambientTransaction is not null && savepoint is not null)
                await ambientTransaction.ReleaseSavepointAsync(savepoint, ct);
            return result;
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
            else if (ambientTransaction is not null && savepoint is not null)
                await ambientTransaction.RollbackToSavepointAsync(savepoint, CancellationToken.None);
            throw;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    private async Task<(decimal Revenue, decimal Expense)> GetShiftTotalsAsync(Guid stationId, DateTime businessDate,
        string shiftCode, CancellationToken ct)
    {
        var date = BusDates.BusinessDate(businessDate);
        var revenue = await db.RevenueReceipts.Where(x => x.StationId == stationId && x.BusinessDate == date &&
            x.ShiftCode == shiftCode && x.Status == BusStatuses.Issued).SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0;
        var expense = await db.ExpenseEntries.Where(x => x.StationId == stationId && x.BusinessDate == date &&
            x.ShiftCode == shiftCode && x.Status == BusStatuses.Approved).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        return (decimal.Round(revenue, 2), decimal.Round(expense, 2));
    }

    private async Task LockBusinessDayAsync(Guid stationId, DateTime businessDate, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return;
        var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{stationId:N}:{BusDates.BusinessDate(businessDate):yyyy-MM-dd}"));
        var lockKey = BitConverter.ToInt32(keyBytes, 0);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockKey})", ct);
    }

    private async Task LockParkingTariffConfigurationAsync(Guid stationId, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return;
        var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"parking-tariffs:{stationId:N}"));
        var lockKey = BitConverter.ToInt32(keyBytes, 0);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockKey})", ct);
    }

    private static bool IsUniqueViolation(DbUpdateException exception, string constraintName)
    {
        var inner = exception.InnerException;
        var type = inner?.GetType();
        if (type?.FullName != "Npgsql.PostgresException") return false;
        var sqlState = type.GetProperty("SqlState")?.GetValue(inner)?.ToString();
        var actualConstraint = type.GetProperty("ConstraintName")?.GetValue(inner)?.ToString();
        return sqlState == "23505" && string.Equals(actualConstraint, constraintName, StringComparison.Ordinal);
    }

    private void AddSettlementOutbox(ShiftSettlement settlement)
    {
        db.OutboxMessages.Add(BusOutbox.Create(new BusSettlementChangedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, null,
            settlement.Id, settlement.StationId, settlement.BusinessDate, settlement.ShiftCode, settlement.Status,
            settlement.TotalRevenue, settlement.TotalExpense), Guid.NewGuid().ToString("N")));
    }

    private void AddAdjustmentOutbox(AdjustmentEntry adjustment)
    {
        db.OutboxMessages.Add(BusOutbox.Create(new BusAdjustmentChangedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, null,
            adjustment.Id, adjustment.StationId, adjustment.Amount, adjustment.Status), Guid.NewGuid().ToString("N")));
    }

    private void AddParkingOutbox(ParkingSession session, Guid? receiptId)
    {
        db.OutboxMessages.Add(BusOutbox.Create(new BusParkingSessionChangedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, null,
            session.Id, session.StationId, session.BusinessDate, session.Status, session.ChargedAmount, receiptId,
            session.BillingUnitMinutes, session.CancellationReason),
            Guid.NewGuid().ToString("N")));
    }

    private async Task<bool> HasCurrentReadinessAsync(DepartureTrip trip, CancellationToken ct)
    {
        var checks = await db.DepartureChecks.AsNoTracking().Where(x => x.DepartureId == trip.Id).ToListAsync(ct);
        if (checks.Count < 8 || checks.Any(x => !x.IsPassed)) return false;
        var vehicle = await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == trip.VehicleId && x.IsActive, ct);
        var driver = await db.Drivers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == trip.DriverId && x.IsActive, ct);
        if (vehicle is null || driver is null || string.IsNullOrWhiteSpace(driver.LicenseNumber)) return false;
        var date = trip.BusinessDate.Date;
        return await HasVehicleDocumentAsync(trip.VehicleId, ["Inspection", "DangKiem"], date, trip.StationId, ct)
            && await HasVehicleDocumentAsync(trip.VehicleId, ["RouteBadge", "PhuHieu"], date, trip.StationId, ct)
            && await HasVehicleDocumentAsync(trip.VehicleId, ["Insurance", "BaoHiem"], date, trip.StationId, ct)
            && await db.CarrierContracts.AsNoTracking().AnyAsync(x => x.StationId == trip.StationId && x.OperatorId == trip.OperatorId &&
                x.IsActive && x.StartDate <= date && x.EndDate >= date, ct)
            && await db.Tariffs.AsNoTracking().AnyAsync(x => x.StationId == trip.StationId && (x.RouteId == null || x.RouteId == trip.RouteId) &&
                x.VehicleType == vehicle.VehicleType && x.IsActive && x.EffectiveFrom <= date && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= date), ct);
    }
    private async Task<bool> HasVehicleDocumentAsync(Guid vehicleId, string[] documentTypes, DateTime date, Guid stationId, CancellationToken ct) =>
        await db.VehicleLegalDocuments.AnyAsync(x => x.VehicleId == vehicleId && x.IsActive &&
            (x.StationId == stationId || (!x.StationId.HasValue &&
                db.DepartureTrips.Where(t => t.VehicleId == x.VehicleId).Select(t => t.StationId).Distinct().Count() == 1 &&
                db.DepartureTrips.Any(t => t.VehicleId == x.VehicleId && t.StationId == stationId))) &&
            documentTypes.Contains(x.DocumentType) && x.ExpiresOn >= date, ct);

    private async Task EnsureReceiptIdempotencyMatchesAsync(RevenueReceipt existing, CreateRevenueReceiptDto input, CancellationToken ct)
    {
        if (existing.StationId != input.StationId || existing.BusinessDate != BusDates.BusinessDate(input.BusinessDate) ||
            !string.Equals(existing.ShiftCode, input.ShiftCode, StringComparison.Ordinal) ||
            !string.Equals(existing.SourceType, input.SourceType?.Trim(), StringComparison.Ordinal) || existing.DepartureId != input.DepartureId ||
            existing.OperatorId != input.OperatorId ||
            !string.Equals(existing.SourceReference, input.SourceReference?.Trim(), StringComparison.Ordinal) ||
            !string.Equals(existing.VehiclePlateNumber, input.VehiclePlateNumber?.Trim(), StringComparison.Ordinal) ||
            existing.PremisesUnitId != input.PremisesUnitId)
            throw new BusinessException("Bus:IdempotencyKeyConflict");

        var existingLines = await db.RevenueLines.AsNoTracking().Where(x => x.ReceiptId == existing.Id)
            .OrderBy(x => x.Id).Select(x => new { x.Description, x.Quantity, x.UnitAmount, x.TariffId }).ToListAsync(ct);
        var requestedLines = input.Lines.OrderBy(x => x.Description, StringComparer.Ordinal).ThenBy(x => x.Quantity).ThenBy(x => x.UnitAmount)
            .Select(x => new { Description = x.Description.Trim(), x.Quantity, x.UnitAmount, x.TariffId }).ToList();
        var matches = existingLines.Count == requestedLines.Count && existingLines.OrderBy(x => x.Description, StringComparer.Ordinal).ThenBy(x => x.Quantity).ThenBy(x => x.UnitAmount)
            .Zip(requestedLines, (stored, requested) => string.Equals(stored.Description, requested.Description, StringComparison.Ordinal) &&
                stored.Quantity == decimal.Round(requested.Quantity, 2) && stored.UnitAmount == decimal.Round(requested.UnitAmount, 2) && stored.TariffId == requested.TariffId)
            .All(x => x);
        if (!matches) throw new BusinessException("Bus:IdempotencyKeyConflict");
    }
    private static string MakeReceiptNumber(DateTime date) => $"RC-{date:yyyyMMdd}-{Guid.NewGuid():N}"[..21];
    private static BusStationDto ToDto(BusStation x) => new(x.Id, x.Code, x.Name, x.Address, x.TimeZone, x.IsActive);
    private static StationAssignmentDto ToDto(UserStationAssignment x) => new(x.Id, x.UserId, x.StationId, x.IsPrimary, x.IsActive, x.ValidFrom, x.ValidTo);
    private static DepartureDto ToDto(DepartureTrip x, IEnumerable<DepartureCheck> checks) => new(x.Id, x.StationId, x.OperatorId, x.RouteId, x.VehicleId, x.DriverId, x.BusinessDate, x.ShiftCode, x.ScheduledDepartureUtc, x.ActualDepartureUtc, x.PassengerCount, x.Status, checks.Select(c => new DepartureCheckDto(c.Id, c.CheckType, c.IsPassed, c.Note)).ToList());
    private static TariffDto ToDto(Tariff x) => new(x.Id, x.StationId, x.RouteId, x.VehicleType, x.FeeType, x.Amount, x.EffectiveFrom, x.EffectiveTo);
    private static RevenueReceiptDto ToDto(RevenueReceipt x, IEnumerable<RevenueLine> lines) => new(x.Id, x.ReceiptNumber, x.StationId, x.BusinessDate, x.ShiftCode, x.SourceType, x.DepartureId, x.OperatorId, x.TotalAmount, x.Status, x.IssuedAtUtc, lines.Select(l => new RevenueLineDto(l.Id, l.Description, l.Quantity, l.UnitAmount, l.LineTotal, l.TariffId)).ToList(), x.SourceReference, x.VehiclePlateNumber, x.PremisesUnitId, x.ParkingSessionId);
    private static ExpenseDto ToDto(ExpenseEntry x) => new(x.Id, x.StationId, x.BusinessDate, x.ShiftCode, x.Category, x.Amount, x.Description, x.DocumentId, x.Status);
    private static PremisesUnitDto ToDto(PremisesUnit x) => new(x.Id, x.StationId, x.Code, x.Name, x.AreaSquareMeters, x.Location, x.IsActive);
    private static LeaseContractDto ToDto(LeaseContract x) => new(x.Id, x.StationId, x.PremisesUnitId, x.TenantName, x.StartDate, x.EndDate, x.RentAmount, x.RentPeriod, x.Status);
    private static CarrierContractDto ToDto(CarrierContract x) => new(x.Id, x.StationId, x.OperatorId, x.ContractNumber, x.StartDate, x.EndDate, x.DocumentId, x.IsActive);
    private static VehicleLegalDocumentDto ToDto(VehicleLegalDocument x) => new(x.Id, x.VehicleId, x.StationId, x.DocumentType, x.ExpiresOn, x.DocumentId, x.IsActive);
    private static AdjustmentDto ToDto(AdjustmentEntry x) => new(x.Id, x.StationId, x.ReceiptId, x.ExpenseId, x.Amount, x.Reason, x.Status, x.CreatedByUserId, x.ApprovedByUserId, x.ApprovedAtUtc);
    private static SettlementDto ToDto(ShiftSettlement x) => new(x.Id, x.StationId, x.BusinessDate, x.ShiftCode, x.TotalRevenue, x.TotalExpense, x.Status, x.SubmittedByUserId, x.CheckedByUserId, x.ApprovedByUserId);
    private static ParkingTariffDto ToDto(ParkingTariff x) => new(x.Id, x.StationId, x.VehicleType, x.BillingUnitMinutes, x.RatePerUnit, x.MinimumCharge, x.Description, x.EffectiveFrom, x.EffectiveTo, x.IsActive);
    private async Task<ParkingSessionDto> ToParkingSessionDtoAsync(ParkingSession session, CancellationToken ct)
    {
        var receiptId = await db.RevenueReceipts.AsNoTracking()
            .Where(x => x.ParkingSessionId == session.Id)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(ct);
        if (session.Status == BusStatuses.ParkingClosed && !receiptId.HasValue)
            throw new BusinessException("Bus:ParkingReceiptMissing");
        return ToDto(session, receiptId);
    }

    private static ParkingSessionDto ToDto(ParkingSession x, Guid? receiptId) => new(x.Id, x.StationId, x.BusinessDate, x.ShiftCode, x.VehiclePlateNumber, x.VehicleType,
        x.ArrivalUtc, x.ExitUtc, x.DurationMinutes, x.BilledUnits, x.BillingUnitMinutes, x.RatePerUnit, x.MinimumCharge,
        x.TariffDescription, x.ChargedAmount, x.Status, x.ParkingTariffId, receiptId, x.CancellationReason);

    private void AddMutationAudit(string action, Guid entityId, string entityType, Guid stationId)
    {
        var context = httpContextAccessor.HttpContext;
        var executionTime = DateTime.UtcNow;
        var audit = new AuditRecordCapturedEto(Guid.NewGuid(), "HCS.BusManagementService", "HCS.BusManagementService",
            UserId, context?.User.FindFirstValue(ClaimTypes.Name) ?? context?.User.FindFirstValue("preferred_username"),
            executionTime, 0, action, context?.Request.Method, context?.Request.Path, 200,
            context?.TraceIdentifier ?? Guid.NewGuid().ToString("N"), context?.Connection.RemoteIpAddress?.ToString(),
            context?.Request.Headers.UserAgent, null, $"StationId={stationId}", [],
            [new AuditEntityChangeCapturedEto(Guid.NewGuid(), executionTime, action, entityId.ToString(), entityType)]);
        db.OutboxMessages.Add(BusOutbox.CreateAudit(audit, context?.TraceIdentifier ?? Guid.NewGuid().ToString("N")));
    }
}
