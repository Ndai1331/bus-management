using HCS.BusManagementService.Contracts;
using HCS.BusManagementService.Domain;
using Microsoft.EntityFrameworkCore;

namespace HCS.BusManagementService.Data;

/// <summary>
/// Creates a small, repeatable data set for the local Compose environment.
/// It is deliberately opt-in and never updates or deletes user-created rows.
/// </summary>
public static class BusManagementDemoDataSeeder
{
    public const string ConfigurationKey = "BusManagement:SeedDemoData";

    private const string DemoShiftCode = "DEMO-S1";
    private const string DemoVehicleType = "Bus";
    private const string DemoFeeType = "StationFee";
    private const string DemoParkingSpotCode = "DEMO-P01";
    private static readonly string[] ReadinessChecks =
    ["Inspection", "RouteBadge", "Insurance", "DriverLicense", "TransportOrder", "Contract", "Fee", "Control"];

    // These are seed-only actor identifiers. Bus Management does not keep a FK to
    // the Identity database, so the demo records remain portable across local DBs.
    private static readonly Guid DemoOperatorUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DemoAccountantUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid DemoManagerUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private sealed record StationSeed(
        BusStation Station,
        TransportOperator Operator,
        FixedRoute Route,
        Vehicle Vehicle,
        Driver Driver,
        Tariff Tariff,
        ParkingTariff ParkingTariff,
        ParkingSpot ParkingSpot,
        PremisesUnit PremisesUnit);

    private sealed record StationDefinition(string Code, string Name, string Address, string RouteName, string PlateNumber);

    private static readonly StationDefinition[] StationDefinitions =
    [
        new("QN-01", "Bến xe Hạ Long", "Đường Cao Xanh, Hạ Long, Quảng Ninh", "Hạ Long - Hà Nội", "14B-001.01"),
        new("QN-02", "Bến xe Cẩm Phả", "Phường Cẩm Thịnh, Cẩm Phả, Quảng Ninh", "Cẩm Phả - Hà Nội", "14B-002.02"),
        new("QN-03", "Bến xe Móng Cái", "Phường Trần Phú, Móng Cái, Quảng Ninh", "Móng Cái - Hạ Long", "14B-003.03"),
        new("QN-04", "Bến xe Uông Bí", "Phường Quang Trung, Uông Bí, Quảng Ninh", "Uông Bí - Hà Nội", "14B-004.04"),
        new("QN-05", "Bến xe Vân Đồn", "Thị trấn Cái Rồng, Vân Đồn, Quảng Ninh", "Vân Đồn - Hạ Long", "14B-005.05"),
        new("QN-06", "Bến xe Đông Triều", "Phường Mạo Khê, Đông Triều, Quảng Ninh", "Đông Triều - Uông Bí", "14B-006.06"),
        new("QN-07", "Bến xe Tiên Yên", "Thị trấn Tiên Yên, Quảng Ninh", "Tiên Yên - Móng Cái", "14B-007.07"),
        new("QN-08", "Bến xe Hải Hà", "Thị trấn Quảng Hà, Hải Hà, Quảng Ninh", "Hải Hà - Móng Cái", "14B-008.08")
    ];

    public static async Task<int> SeedAsync(BusManagementDbContext db, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var created = 0;
        var demoDate = await FindExistingDemoDateAsync(db, cancellationToken) ?? DateTime.UtcNow.Date.AddDays(-1);
        var stations = new List<StationSeed>(StationDefinitions.Length);

        foreach (var definition in StationDefinitions)
        {
            var station = await db.BusStations.SingleOrDefaultAsync(x => x.Code == definition.Code, cancellationToken);
            if (station is null)
            {
                station = new BusStation(Guid.NewGuid(), definition.Code, definition.Name, definition.Address);
                db.BusStations.Add(station);
                created++;
            }

            var operatorCode = $"DEMO-{definition.Code}-OP";
            var transportOperator = await db.TransportOperators.SingleOrDefaultAsync(x => x.Code == operatorCode, cancellationToken);
            if (transportOperator is null)
            {
                transportOperator = new TransportOperator(Guid.NewGuid(), operatorCode, $"Nhà xe mẫu {definition.Name}", station.Id);
                db.TransportOperators.Add(transportOperator);
                created++;
            }

            var routeCode = $"DEMO-{definition.Code}-R1";
            var route = await db.FixedRoutes.SingleOrDefaultAsync(x => x.Code == routeCode, cancellationToken);
            if (route is null)
            {
                route = new FixedRoute(Guid.NewGuid(), routeCode, definition.RouteName, transportOperator.Id, station.Id);
                db.FixedRoutes.Add(route);
                created++;
            }

            var vehicle = await db.Vehicles.SingleOrDefaultAsync(x => x.PlateNumber == definition.PlateNumber, cancellationToken);
            if (vehicle is null)
            {
                vehicle = new Vehicle(Guid.NewGuid(), definition.PlateNumber, DemoVehicleType, transportOperator.Id, station.Id);
                db.Vehicles.Add(vehicle);
                created++;
            }

            var licenseNumber = $"DEMO-{definition.Code}-LIC";
            var driver = await db.Drivers.SingleOrDefaultAsync(x => x.LicenseNumber == licenseNumber, cancellationToken);
            if (driver is null)
            {
                driver = new Driver(Guid.NewGuid(), $"Tài xế mẫu {definition.Name}", licenseNumber, station.Id);
                db.Drivers.Add(driver);
                created++;
            }

            var documentExpiry = DateTime.UtcNow.Date.AddDays(365);
            foreach (var documentType in new[] { "Inspection", "RouteBadge", "Insurance" })
            {
                var document = await db.VehicleLegalDocuments.SingleOrDefaultAsync(
                    x => x.VehicleId == vehicle.Id && x.DocumentType == documentType, cancellationToken);
                if (document is null)
                {
                    db.VehicleLegalDocuments.Add(new VehicleLegalDocument(
                        Guid.NewGuid(), vehicle.Id, documentType, documentExpiry, null, station.Id));
                    created++;
                }
            }

            var contractNumber = $"DEMO-{definition.Code}-CARRIER";
            var carrierContract = await db.CarrierContracts.SingleOrDefaultAsync(
                x => x.ContractNumber == contractNumber, cancellationToken);
            if (carrierContract is null)
            {
                db.CarrierContracts.Add(new CarrierContract(
                    Guid.NewGuid(), station.Id, transportOperator.Id, contractNumber,
                    demoDate.AddDays(-30), DateTime.UtcNow.Date.AddDays(365)));
                created++;
            }

            var tariffStart = new DateTime(demoDate.Year, 1, 1);
            var tariff = await db.Tariffs.SingleOrDefaultAsync(x => x.StationId == station.Id &&
                x.RouteId == route.Id && x.VehicleType == DemoVehicleType && x.FeeType == DemoFeeType &&
                x.EffectiveFrom == tariffStart, cancellationToken);
            if (tariff is null)
            {
                tariff = new Tariff(Guid.NewGuid(), station.Id, route.Id, DemoVehicleType, DemoFeeType, 150_000m, tariffStart);
                db.Tariffs.Add(tariff);
                created++;
            }

            var parkingTariff = await db.ParkingTariffs.SingleOrDefaultAsync(x => x.StationId == station.Id &&
                x.VehicleType == DemoVehicleType && x.EffectiveFrom == tariffStart, cancellationToken);
            if (parkingTariff is null)
            {
                parkingTariff = new ParkingTariff(Guid.NewGuid(), station.Id, DemoVehicleType, 60,
                    50_000m, 50_000m, "Phí bãi đỗ mẫu theo giờ", tariffStart);
                db.ParkingTariffs.Add(parkingTariff);
                created++;
            }

            var parkingSpot = await db.ParkingSpots.SingleOrDefaultAsync(x => x.StationId == station.Id &&
                x.Code == DemoParkingSpotCode, cancellationToken);
            if (parkingSpot is null)
            {
                parkingSpot = new ParkingSpot(Guid.NewGuid(), station.Id, DemoParkingSpotCode,
                    "Vị trí đỗ mẫu", DemoVehicleType);
                db.ParkingSpots.Add(parkingSpot);
                created++;
            }

            var premisesCode = $"DEMO-{definition.Code}-P01";
            var premises = await db.PremisesUnits.SingleOrDefaultAsync(x => x.StationId == station.Id &&
                x.Code == premisesCode, cancellationToken);
            if (premises is null)
            {
                premises = new PremisesUnit(Guid.NewGuid(), station.Id, premisesCode,
                    "Quầy dịch vụ mẫu", 24m, "Khu thương mại tầng 1");
                db.PremisesUnits.Add(premises);
                created++;
            }

            stations.Add(new StationSeed(station, transportOperator, route, vehicle, driver,
                tariff, parkingTariff, parkingSpot, premises));
        }

        // Persist the master data first. This also makes the seeder safe when it is
        // re-run after a previous deployment was interrupted between phases.
        await db.SaveChangesAsync(cancellationToken);

        foreach (var station in stations)
        {
            created += await SeedTransactionsAsync(db, station, demoDate, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return created;
    }

    private static async Task<DateTime?> FindExistingDemoDateAsync(BusManagementDbContext db, CancellationToken cancellationToken)
    {
        return await db.DepartureTrips.AsNoTracking()
            .Where(x => x.ShiftCode == DemoShiftCode)
            .Select(x => (DateTime?)x.BusinessDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static async Task<int> SeedTransactionsAsync(
        BusManagementDbContext db, StationSeed seed, DateTime demoDate, CancellationToken cancellationToken)
    {
        var created = 0;
        var scheduledUtc = DateTime.SpecifyKind(demoDate.Date.AddHours(1), DateTimeKind.Utc);
        var actualUtc = scheduledUtc.AddMinutes(10);

        var departure = await db.DepartureTrips.SingleOrDefaultAsync(x =>
            x.OperatorId == seed.Operator.Id && x.RouteId == seed.Route.Id && x.VehicleId == seed.Vehicle.Id &&
            x.DriverId == seed.Driver.Id && x.ShiftCode == DemoShiftCode, cancellationToken);
        if (departure is null)
        {
            departure = new DepartureTrip(Guid.NewGuid(), seed.Station.Id, seed.Operator.Id, seed.Route.Id,
                seed.Vehicle.Id, seed.Driver.Id, demoDate, DemoShiftCode, scheduledUtc, 32);
            departure.Register();
            foreach (var checkType in ReadinessChecks)
            {
                db.DepartureChecks.Add(new DepartureCheck(Guid.NewGuid(), departure.Id, checkType, true, "Dữ liệu mẫu hợp lệ"));
            }
            departure.MarkReady();
            departure.MarkDeparted(actualUtc);
            departure.Complete();
            db.DepartureTrips.Add(departure);
            created++;
        }

        var fixedRouteKey = $"demo-fixed-route-{seed.Station.Code}";
        var fixedReceipt = await db.RevenueReceipts.SingleOrDefaultAsync(
            x => x.IdempotencyKey == fixedRouteKey, cancellationToken);
        if (fixedReceipt is null)
        {
            fixedReceipt = new RevenueReceipt(Guid.NewGuid(), $"DEMO-{seed.Station.Code}-FIXED",
                seed.Station.Id, demoDate, DemoShiftCode, RevenueSources.FixedRoute, departure.Id,
                seed.Operator.Id, DemoOperatorUserId, fixedRouteKey, "DEMO-FIXED-ROUTE", seed.Vehicle.PlateNumber);
            fixedReceipt.AddLine(Guid.NewGuid(), "Phí bến chuyến cố định", 1m, seed.Tariff.Amount, seed.Tariff.Id);
            fixedReceipt.Issue(DateTime.UtcNow.AddMinutes(-10));
            db.RevenueReceipts.Add(fixedReceipt);
            foreach (var line in fixedReceipt.Lines) db.RevenueLines.Add(line);
            created++;
        }

        var expenseDescription = $"Chi phí vận hành mẫu {seed.Station.Code}";
        var expense = await db.ExpenseEntries.SingleOrDefaultAsync(
            x => x.StationId == seed.Station.Id && x.Description == expenseDescription, cancellationToken);
        if (expense is null)
        {
            expense = new ExpenseEntry(Guid.NewGuid(), seed.Station.Id, demoDate, DemoShiftCode,
                "Vận hành", 50_000m, expenseDescription, null, DemoOperatorUserId);
            expense.Submit();
            expense.Approve(DemoAccountantUserId);
            db.ExpenseEntries.Add(expense);
            created++;
        }

        var leaseTenant = $"Đơn vị thuê mẫu {seed.Station.Code}";
        var lease = await db.LeaseContracts.SingleOrDefaultAsync(x => x.StationId == seed.Station.Id &&
            x.PremisesUnitId == seed.PremisesUnit.Id && x.TenantName == leaseTenant, cancellationToken);
        if (lease is null)
        {
            lease = new LeaseContract(Guid.NewGuid(), seed.Station.Id, seed.PremisesUnit.Id,
                leaseTenant, demoDate, DateTime.UtcNow.Date.AddDays(365), 8_000_000m, "Monthly");
            lease.Approve();
            db.LeaseContracts.Add(lease);
            created++;
        }

        var parkingPlate = $"DEMO-PARK-{seed.Station.Code}";
        var parkingSession = await db.ParkingSessions.SingleOrDefaultAsync(x =>
            x.StationId == seed.Station.Id && x.VehiclePlateNumber == parkingPlate, cancellationToken);
        RevenueReceipt? parkingReceipt = null;
        if (parkingSession is null)
        {
            var exitUtc = DateTime.UtcNow.AddMinutes(-30);
            var arrivalUtc = DateTime.UtcNow.AddMinutes(-90);
            parkingSession = new ParkingSession(Guid.NewGuid(), seed.Station.Id, demoDate, DemoShiftCode,
                parkingPlate, DemoVehicleType, arrivalUtc, seed.ParkingTariff.Id,
                seed.ParkingTariff.BillingUnitMinutes, seed.ParkingTariff.RatePerUnit,
                seed.ParkingTariff.MinimumCharge, seed.ParkingTariff.Description, seed.ParkingSpot.Id);
            var quote = parkingSession.Quote(exitUtc);
            parkingReceipt = RevenueReceipt.CreateParking(Guid.NewGuid(), $"DEMO-{seed.Station.Code}-PARK",
                parkingSession, DemoOperatorUserId);
            parkingReceipt.AddLine(Guid.NewGuid(), "Phí bãi đỗ mẫu", 1m, quote.Amount);
            parkingReceipt.Issue(exitUtc);
            parkingSession.Close(exitUtc);
            db.ParkingSessions.Add(parkingSession);
            db.RevenueReceipts.Add(parkingReceipt);
            foreach (var line in parkingReceipt.Lines) db.RevenueLines.Add(line);
            created += 2;
        }
        else
        {
            parkingReceipt = await db.RevenueReceipts.SingleOrDefaultAsync(
                x => x.ParkingSessionId == parkingSession.Id, cancellationToken);
        }

        var settlement = await db.ShiftSettlements.SingleOrDefaultAsync(x => x.StationId == seed.Station.Id &&
            x.ShiftCode == DemoShiftCode, cancellationToken);
        if (settlement is null)
        {
            var parkingAmount = parkingReceipt?.TotalAmount ?? 0m;
            settlement = new ShiftSettlement(Guid.NewGuid(), seed.Station.Id, demoDate, DemoShiftCode,
                fixedReceipt.TotalAmount + parkingAmount, expense.Amount, DemoOperatorUserId);
            settlement.Submit(DemoOperatorUserId);
            settlement.Check(DemoAccountantUserId);
            settlement.Approve(DemoManagerUserId);
            settlement.Close();
            db.ShiftSettlements.Add(settlement);
            created++;
        }

        var dailyClose = await db.DailyCloses.SingleOrDefaultAsync(x => x.StationId == seed.Station.Id &&
            x.BusinessDate == demoDate.Date, cancellationToken);
        if (dailyClose is null)
        {
            dailyClose = new DailyClose(Guid.NewGuid(), seed.Station.Id, demoDate,
                settlement.TotalRevenue, settlement.TotalExpense, 1);
            dailyClose.Close(DemoManagerUserId, DateTime.UtcNow.AddMinutes(-5));
            db.DailyCloses.Add(dailyClose);
            created++;
        }

        return created;
    }
}
