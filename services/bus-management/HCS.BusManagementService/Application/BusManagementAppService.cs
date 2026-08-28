using System.Security.Claims;
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
                (!x.ValidTo.HasValue || x.ValidTo.Value >= DateTime.UtcNow.Date))
            .Select(x => x.StationId).ToListAsync(cancellationToken)).ToHashSet();
    }

    public async Task EnsureStationAsync(Guid stationId, CancellationToken cancellationToken = default)
    {
        if (stationId == Guid.Empty) throw new BusinessException("Bus:StationRequired");
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
        var query = db.TransportOperators.AsNoTracking().OrderBy(x => x.Code);
        var total = await query.LongCountAsync(ct); var items = await query.Skip(Math.Max(skip, 0)).Take(Math.Clamp(take, 1, 100))
            .Select(x => new OperatorDto(x.Id, x.Code, x.Name, x.IsActive)).ToListAsync(ct); return new(total, items);
    }

    public async Task<OperatorDto> CreateOperatorAsync(CreateOperatorDto input, CancellationToken ct)
    {
        var entity = new TransportOperator(Guid.NewGuid(), input.Code, input.Name); db.TransportOperators.Add(entity); await db.SaveChangesAsync(ct); return new(entity.Id, entity.Code, entity.Name, entity.IsActive);
    }

    public async Task<PagedBusDto<RouteDto>> GetRoutesAsync(int skip, int take, CancellationToken ct)
    {
        var query = db.FixedRoutes.AsNoTracking().OrderBy(x => x.Code); var total = await query.LongCountAsync(ct);
        var items = await query.Skip(Math.Max(skip, 0)).Take(Math.Clamp(take, 1, 100)).Select(x => new RouteDto(x.Id, x.Code, x.Name, x.OperatorId, x.IsActive)).ToListAsync(ct); return new(total, items);
    }

    public async Task<RouteDto> CreateRouteAsync(CreateRouteDto input, CancellationToken ct)
    {
        if (!await db.TransportOperators.AnyAsync(x => x.Id == input.OperatorId && x.IsActive, ct)) throw new BusinessException("Bus:OperatorNotFound");
        var entity = new FixedRoute(Guid.NewGuid(), input.Code, input.Name, input.OperatorId); db.FixedRoutes.Add(entity); await db.SaveChangesAsync(ct); return new(entity.Id, entity.Code, entity.Name, entity.OperatorId, entity.IsActive);
    }

    public async Task<PagedBusDto<VehicleDto>> GetVehiclesAsync(int skip, int take, CancellationToken ct)
    {
        var query = db.Vehicles.AsNoTracking().OrderBy(x => x.PlateNumber); var total = await query.LongCountAsync(ct);
        var items = await query.Skip(Math.Max(skip, 0)).Take(Math.Clamp(take, 1, 100)).Select(x => new VehicleDto(x.Id, x.PlateNumber, x.VehicleType, x.OperatorId, x.IsActive)).ToListAsync(ct); return new(total, items);
    }

    public async Task<VehicleDto> CreateVehicleAsync(CreateVehicleDto input, CancellationToken ct)
    {
        if (!await db.TransportOperators.AnyAsync(x => x.Id == input.OperatorId && x.IsActive, ct)) throw new BusinessException("Bus:OperatorNotFound");
        var entity = new Vehicle(Guid.NewGuid(), input.PlateNumber, input.VehicleType, input.OperatorId); db.Vehicles.Add(entity); await db.SaveChangesAsync(ct); return new(entity.Id, entity.PlateNumber, entity.VehicleType, entity.OperatorId, entity.IsActive);
    }

    public async Task<PagedBusDto<DriverDto>> GetDriversAsync(int skip, int take, CancellationToken ct)
    {
        var query = db.Drivers.AsNoTracking().OrderBy(x => x.FullName); var total = await query.LongCountAsync(ct);
        var items = await query.Skip(Math.Max(skip, 0)).Take(Math.Clamp(take, 1, 100)).Select(x => new DriverDto(x.Id, x.FullName, x.LicenseNumber, x.IsActive)).ToListAsync(ct); return new(total, items);
    }

    public async Task<DriverDto> CreateDriverAsync(CreateDriverDto input, CancellationToken ct)
    {
        var entity = new Driver(Guid.NewGuid(), input.FullName, input.LicenseNumber); db.Drivers.Add(entity); await db.SaveChangesAsync(ct); return new(entity.Id, entity.FullName, entity.LicenseNumber, entity.IsActive);
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
        if (!await db.TransportOperators.AnyAsync(x => x.Id == input.OperatorId && x.IsActive, ct)) throw new BusinessException("Bus:OperatorNotFound");
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
            var scopedVehicles = db.DepartureTrips.Where(x => stationIds.Contains(x.StationId)).Select(x => x.VehicleId);
            query = query.Where(x => scopedVehicles.Contains(x.VehicleId));
        }
        if (vehicleId.HasValue) query = query.Where(x => x.VehicleId == vehicleId);
        if (expiringBefore.HasValue) query = query.Where(x => x.ExpiresOn <= expiringBefore.Value.Date);
        var total = await query.LongCountAsync(ct);
        var items = await query.OrderBy(x => x.ExpiresOn).Skip(Math.Max(skip, 0)).Take(Math.Clamp(take, 1, 100)).ToListAsync(ct);
        return new(total, items.Select(ToDto).ToList());
    }

    public async Task<VehicleLegalDocumentDto> CreateVehicleLegalDocumentAsync(CreateVehicleLegalDocumentDto input, CancellationToken ct)
    {
        scope.EnsureGlobal();
        if (!await db.Vehicles.AnyAsync(x => x.Id == input.VehicleId && x.IsActive, ct)) throw new BusinessException("Bus:VehicleNotFound");
        var document = new VehicleLegalDocument(Guid.NewGuid(), input.VehicleId, input.DocumentType, input.ExpiresOn, input.DocumentId);
        db.VehicleLegalDocuments.Add(document); await db.SaveChangesAsync(ct); return ToDto(document);
    }

    public async Task<DepartureDto> CreateDepartureAsync(CreateDepartureDto input, CancellationToken ct)
    {
        await scope.EnsureStationAsync(input.StationId, ct);
        await RequireStationAsync(input.StationId, ct);
        await EnsureOpenDayAsync(input.StationId, input.BusinessDate, ct);
        if (!await db.TransportOperators.AnyAsync(x => x.Id == input.OperatorId && x.IsActive, ct)) throw new BusinessException("Bus:OperatorNotFound");
        if (!await db.FixedRoutes.AnyAsync(x => x.Id == input.RouteId && x.OperatorId == input.OperatorId && x.IsActive, ct)) throw new BusinessException("Bus:RouteNotFound");
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(x => x.Id == input.VehicleId && x.OperatorId == input.OperatorId && x.IsActive, ct) ?? throw new BusinessException("Bus:VehicleNotFound");
        var driver = await db.Drivers.SingleOrDefaultAsync(x => x.Id == input.DriverId && x.IsActive, ct) ?? throw new BusinessException("Bus:DriverNotFound");
        var businessDate = input.BusinessDate.Date;
        var inspectionValid = input.InspectionValid && await HasVehicleDocumentAsync(input.VehicleId, ["Inspection", "DangKiem"], businessDate, ct);
        var routeBadgeValid = input.RouteBadgeValid && await HasVehicleDocumentAsync(input.VehicleId, ["RouteBadge", "PhuHieu"], businessDate, ct);
        var insuranceValid = input.InsuranceValid && await HasVehicleDocumentAsync(input.VehicleId, ["Insurance", "BaoHiem"], businessDate, ct);
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
        var tariff = new Tariff(Guid.NewGuid(), input.StationId, input.RouteId, input.VehicleType, input.FeeType, input.Amount, input.EffectiveFrom, input.EffectiveTo);
        db.Tariffs.Add(tariff); await db.SaveChangesAsync(ct); return ToDto(tariff);
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
        DepartureTrip? departure = null;
        if (input.DepartureId.HasValue)
        {
            departure = await db.DepartureTrips.SingleOrDefaultAsync(x => x.Id == input.DepartureId && x.StationId == input.StationId, ct)
                ?? throw new BusinessException("Bus:DepartureNotFound");
        }
        var receipt = new RevenueReceipt(Guid.NewGuid(), MakeReceiptNumber(input.BusinessDate), input.StationId, input.BusinessDate,
            input.ShiftCode, input.SourceType, input.DepartureId, input.OperatorId, UserId, idempotencyKey);
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
        await scope.EnsureStationAsync(expense.StationId, ct); expense.Submit();
        db.OutboxMessages.Add(BusOutbox.Create(new BusExpenseChangedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, null,
            expense.Id, expense.StationId, expense.Amount, expense.Status), Guid.NewGuid().ToString("N")));
        AddMutationAudit("Expense.Submit", expense.Id, nameof(ExpenseEntry), expense.StationId);
        await db.SaveChangesAsync(ct); return ToDto(expense);
    }

    public async Task<ExpenseDto> ApproveExpenseAsync(Guid id, CancellationToken ct)
    {
        var expense = await db.ExpenseEntries.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Bus:ExpenseNotFound");
        await scope.EnsureStationAsync(expense.StationId, ct); expense.Approve(UserId);
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
        await scope.EnsureStationAsync(input.StationId, ct);
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
        var date = BusDates.BusinessDate(input.BusinessDate);
        await EnsureOpenDayAsync(input.StationId, date, ct);
        var revenue = await db.RevenueReceipts.Where(x => x.StationId == input.StationId && x.BusinessDate == date && x.ShiftCode == input.ShiftCode && x.Status == BusStatuses.Issued).SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0;
        var expense = await db.ExpenseEntries.Where(x => x.StationId == input.StationId && x.BusinessDate == date && x.ShiftCode == input.ShiftCode && x.Status == BusStatuses.Approved).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        var settlement = new ShiftSettlement(Guid.NewGuid(), input.StationId, date, input.ShiftCode, revenue, expense, UserId);
        db.ShiftSettlements.Add(settlement); AddMutationAudit("ShiftSettlement.Create", settlement.Id, nameof(ShiftSettlement), settlement.StationId);
        await db.SaveChangesAsync(ct); return ToDto(settlement);
    }

    public async Task<SettlementDto> TransitionSettlementAsync(Guid id, string action, CancellationToken ct)
    {
        var settlement = await db.ShiftSettlements.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Bus:SettlementNotFound");
        await scope.EnsureStationAsync(settlement.StationId, ct); var user = UserId;
        switch (action.Trim().ToLowerInvariant())
        {
            case "submit": settlement.Submit(user); break;
            case "check": settlement.Check(user); break;
            case "approve": settlement.Approve(user); break;
            case "close": settlement.Close(); break;
            default: throw new BusinessException("Bus:UnsupportedSettlementAction");
        }
        AddMutationAudit($"ShiftSettlement.{action.Trim().ToUpperInvariant()}", settlement.Id, nameof(ShiftSettlement), settlement.StationId);
        await db.SaveChangesAsync(ct); return ToDto(settlement);
    }

    public async Task<DailyCloseDto> CloseDailyAsync(CloseDailyDto input, CancellationToken ct)
    {
        await scope.EnsureStationAsync(input.StationId, ct); var date = BusDates.BusinessDate(input.BusinessDate);
        if (await db.ExpenseEntries.AnyAsync(x => x.StationId == input.StationId && x.BusinessDate == date && x.Status != BusStatuses.Approved, ct))
            throw new BusinessException("Bus:ExpensesNotApproved");
        var shifts = await db.ShiftSettlements.Where(x => x.StationId == input.StationId && x.BusinessDate == date).ToListAsync(ct);
        if (shifts.Count == 0 || shifts.Any(x => x.Status != BusStatuses.Approved && x.Status != BusStatuses.Closed)) throw new BusinessException("Bus:ShiftsNotApproved");
        var close = await db.DailyCloses.SingleOrDefaultAsync(x => x.StationId == input.StationId && x.BusinessDate == date, ct);
        if (close is null)
        {
            close = new DailyClose(Guid.NewGuid(), input.StationId, date, shifts.Sum(x => x.TotalRevenue), shifts.Sum(x => x.TotalExpense), shifts.Count); db.DailyCloses.Add(close);
        }
        close.Close(UserId, DateTime.UtcNow);
        db.OutboxMessages.Add(BusOutbox.Create(new BusReconciliationClosedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, null,
            close.Id, close.StationId, close.BusinessDate), Guid.NewGuid().ToString("N")));
        AddMutationAudit("DailyClose.Close", close.Id, nameof(DailyClose), close.StationId);
        await db.SaveChangesAsync(ct);
        return new DailyCloseDto(close.Id, close.StationId, close.BusinessDate, close.TotalRevenue, close.TotalExpense,
            close.ShiftCount, close.Status, close.ClosedByUserId, close.ClosedAtUtc);
    }

    public async Task<DashboardSummaryDto> GetDashboardAsync(DateTime from, DateTime to, Guid? stationId, CancellationToken ct)
    {
        var stationIds = await scope.GetStationIdsAsync(ct); var start = BusDates.BusinessDate(from); var end = BusDates.BusinessDate(to);
        var receipts = db.RevenueReceipts.AsNoTracking().Where(x => x.BusinessDate >= start && x.BusinessDate <= end && x.Status == BusStatuses.Issued);
        var expenses = db.ExpenseEntries.AsNoTracking().Where(x => x.BusinessDate >= start && x.BusinessDate <= end && x.Status == BusStatuses.Approved);
        var trips = db.DepartureTrips.AsNoTracking().Where(x => x.BusinessDate >= start && x.BusinessDate <= end);
        var shifts = db.ShiftSettlements.AsNoTracking().Where(x => x.BusinessDate >= start && x.BusinessDate <= end && x.Status != BusStatuses.Closed);
        var docs = db.VehicleLegalDocuments.AsNoTracking().Where(x => x.IsActive && x.ExpiresOn <= DateTime.UtcNow.Date.AddDays(30));
        if (stationIds is not null)
        {
            receipts = receipts.Where(x => stationIds.Contains(x.StationId));
            expenses = expenses.Where(x => stationIds.Contains(x.StationId));
            trips = trips.Where(x => stationIds.Contains(x.StationId));
            shifts = shifts.Where(x => stationIds.Contains(x.StationId));
            var scopedVehicleIds = db.DepartureTrips.Where(x => stationIds.Contains(x.StationId)).Select(x => x.VehicleId);
            docs = docs.Where(x => scopedVehicleIds.Contains(x.VehicleId));
        }
        if (stationId.HasValue)
        {
            await scope.EnsureStationAsync(stationId.Value, ct);
            receipts = receipts.Where(x => x.StationId == stationId); expenses = expenses.Where(x => x.StationId == stationId);
            trips = trips.Where(x => x.StationId == stationId); shifts = shifts.Where(x => x.StationId == stationId);
        }
        IQueryable<DepartureTrip> dashboardTrips = db.DepartureTrips.AsNoTracking();
        if (stationIds is not null) dashboardTrips = dashboardTrips.Where(x => stationIds.Contains(x.StationId));
        if (stationId.HasValue) dashboardTrips = dashboardTrips.Where(x => x.StationId == stationId);
        var dashboardVehicleIds = dashboardTrips.Select(x => x.VehicleId);
        docs = docs.Where(x => dashboardVehicleIds.Contains(x.VehicleId));
        return new(from, to, await receipts.SumAsync(x => (decimal?)x.TotalAmount, ct) ?? 0, await expenses.SumAsync(x => (decimal?)x.Amount, ct) ?? 0,
            await trips.CountAsync(ct), await receipts.CountAsync(ct), await shifts.CountAsync(ct), await docs.CountAsync(ct), DateTime.UtcNow);
    }

    public async Task<IReadOnlyList<RevenueReportRowDto>> GetRevenueReportAsync(DateTime from, DateTime to, Guid? stationId, CancellationToken ct)
    {
        var stationIds = await scope.GetStationIdsAsync(ct); var query = db.RevenueReceipts.AsNoTracking().Where(x => x.BusinessDate >= from.Date && x.BusinessDate <= to.Date && x.Status == BusStatuses.Issued);
        if (stationIds is not null) query = query.Where(x => stationIds.Contains(x.StationId));
        if (stationId.HasValue) { await scope.EnsureStationAsync(stationId.Value, ct); query = query.Where(x => x.StationId == stationId); }
        return await query.GroupBy(x => new { x.StationId, x.SourceType }).Select(g => new RevenueReportRowDto(g.Key.StationId, g.Key.SourceType, g.Sum(x => x.TotalAmount), g.Count())).OrderBy(x => x.StationId).ThenBy(x => x.SourceType).ToListAsync(ct);
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
        var stationIds = await scope.GetStationIdsAsync(ct);
        if (stationId.HasValue) await scope.EnsureStationAsync(stationId.Value, ct);
        var query = db.ShiftSettlements.AsNoTracking().Where(x => x.BusinessDate >= from.Date && x.BusinessDate <= to.Date);
        if (stationIds is not null) query = query.Where(x => stationIds.Contains(x.StationId));
        if (stationId.HasValue) query = query.Where(x => x.StationId == stationId);
        return await query.OrderBy(x => x.BusinessDate).ThenBy(x => x.StationId).ThenBy(x => x.ShiftCode)
            .Select(x => new ReconciliationReportRowDto(x.StationId, x.BusinessDate, x.ShiftCode, x.Status, x.TotalRevenue, x.TotalExpense))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ComplianceReportRowDto>> GetComplianceReportAsync(Guid? stationId, CancellationToken ct)
    {
        var stationIds = await scope.GetStationIdsAsync(ct);
        if (stationId.HasValue) await scope.EnsureStationAsync(stationId.Value, ct);
        var threshold = DateTime.UtcNow.Date.AddDays(30);
        var query = from document in db.VehicleLegalDocuments.AsNoTracking()
                    join departure in db.DepartureTrips.AsNoTracking() on document.VehicleId equals departure.VehicleId
                    where document.IsActive && document.ExpiresOn <= threshold
                    select new { departure.StationId, document.Id };
        if (stationIds is not null) query = query.Where(x => stationIds.Contains(x.StationId));
        if (stationId.HasValue) query = query.Where(x => x.StationId == stationId);
        return await query.Distinct().GroupBy(x => x.StationId)
            .Select(g => new ComplianceReportRowDto(g.Key, g.Count()))
            .OrderBy(x => x.StationId).ToListAsync(ct);
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
    private async Task EnsureOpenDayAsync(Guid stationId, DateTime businessDate, CancellationToken ct)
    {
        var date = BusDates.BusinessDate(businessDate);
        if (await db.DailyCloses.AnyAsync(x => x.StationId == stationId && x.BusinessDate == date && x.Status == BusStatuses.Closed, ct))
            throw new BusinessException("Bus:DailyAlreadyClosed");
    }

    private async Task<bool> HasCurrentReadinessAsync(DepartureTrip trip, CancellationToken ct)
    {
        var checks = await db.DepartureChecks.AsNoTracking().Where(x => x.DepartureId == trip.Id).ToListAsync(ct);
        if (checks.Count < 8 || checks.Any(x => !x.IsPassed)) return false;
        var vehicle = await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == trip.VehicleId && x.IsActive, ct);
        var driver = await db.Drivers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == trip.DriverId && x.IsActive, ct);
        if (vehicle is null || driver is null || string.IsNullOrWhiteSpace(driver.LicenseNumber)) return false;
        var date = trip.BusinessDate.Date;
        return await HasVehicleDocumentAsync(trip.VehicleId, ["Inspection", "DangKiem"], date, ct)
            && await HasVehicleDocumentAsync(trip.VehicleId, ["RouteBadge", "PhuHieu"], date, ct)
            && await HasVehicleDocumentAsync(trip.VehicleId, ["Insurance", "BaoHiem"], date, ct)
            && await db.CarrierContracts.AsNoTracking().AnyAsync(x => x.StationId == trip.StationId && x.OperatorId == trip.OperatorId &&
                x.IsActive && x.StartDate <= date && x.EndDate >= date, ct)
            && await db.Tariffs.AsNoTracking().AnyAsync(x => x.StationId == trip.StationId && (x.RouteId == null || x.RouteId == trip.RouteId) &&
                x.VehicleType == vehicle.VehicleType && x.IsActive && x.EffectiveFrom <= date && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= date), ct);
    }
    private async Task<bool> HasVehicleDocumentAsync(Guid vehicleId, string[] documentTypes, DateTime date, CancellationToken ct) =>
        await db.VehicleLegalDocuments.AnyAsync(x => x.VehicleId == vehicleId && x.IsActive && documentTypes.Contains(x.DocumentType) && x.ExpiresOn >= date, ct);

    private async Task EnsureReceiptIdempotencyMatchesAsync(RevenueReceipt existing, CreateRevenueReceiptDto input, CancellationToken ct)
    {
        if (existing.StationId != input.StationId || existing.BusinessDate != BusDates.BusinessDate(input.BusinessDate) ||
            !string.Equals(existing.ShiftCode, input.ShiftCode, StringComparison.Ordinal) ||
            !string.Equals(existing.SourceType, input.SourceType, StringComparison.Ordinal) || existing.DepartureId != input.DepartureId ||
            existing.OperatorId != input.OperatorId)
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
    private static RevenueReceiptDto ToDto(RevenueReceipt x, IEnumerable<RevenueLine> lines) => new(x.Id, x.ReceiptNumber, x.StationId, x.BusinessDate, x.ShiftCode, x.SourceType, x.DepartureId, x.OperatorId, x.TotalAmount, x.Status, x.IssuedAtUtc, lines.Select(l => new RevenueLineDto(l.Id, l.Description, l.Quantity, l.UnitAmount, l.LineTotal, l.TariffId)).ToList());
    private static ExpenseDto ToDto(ExpenseEntry x) => new(x.Id, x.StationId, x.BusinessDate, x.ShiftCode, x.Category, x.Amount, x.Description, x.DocumentId, x.Status);
    private static PremisesUnitDto ToDto(PremisesUnit x) => new(x.Id, x.StationId, x.Code, x.Name, x.AreaSquareMeters, x.Location, x.IsActive);
    private static LeaseContractDto ToDto(LeaseContract x) => new(x.Id, x.StationId, x.PremisesUnitId, x.TenantName, x.StartDate, x.EndDate, x.RentAmount, x.RentPeriod, x.Status);
    private static CarrierContractDto ToDto(CarrierContract x) => new(x.Id, x.StationId, x.OperatorId, x.ContractNumber, x.StartDate, x.EndDate, x.DocumentId, x.IsActive);
    private static VehicleLegalDocumentDto ToDto(VehicleLegalDocument x) => new(x.Id, x.VehicleId, x.DocumentType, x.ExpiresOn, x.DocumentId, x.IsActive);
    private static SettlementDto ToDto(ShiftSettlement x) => new(x.Id, x.StationId, x.BusinessDate, x.ShiftCode, x.TotalRevenue, x.TotalExpense, x.Status, x.SubmittedByUserId, x.CheckedByUserId, x.ApprovedByUserId);

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
