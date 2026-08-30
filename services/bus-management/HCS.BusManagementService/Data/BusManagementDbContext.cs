using HCS.BusManagementService.Domain;
using HCS.BusManagementService.Integration;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace HCS.BusManagementService.Data;

[ConnectionStringName(ConnectionStringName)]
public sealed class BusManagementDbContext(DbContextOptions<BusManagementDbContext> options)
    : AbpDbContext<BusManagementDbContext>(options)
{
    public const string ConnectionStringName = "BusManagement";
    public const string Schema = "hcs_bus_management";

    public DbSet<BusStation> BusStations => Set<BusStation>();
    public DbSet<UserStationAssignment> UserStationAssignments => Set<UserStationAssignment>();
    public DbSet<TransportOperator> TransportOperators => Set<TransportOperator>();
    public DbSet<FixedRoute> FixedRoutes => Set<FixedRoute>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<VehicleLegalDocument> VehicleLegalDocuments => Set<VehicleLegalDocument>();
    public DbSet<CarrierContract> CarrierContracts => Set<CarrierContract>();
    public DbSet<DepartureTrip> DepartureTrips => Set<DepartureTrip>();
    public DbSet<DepartureCheck> DepartureChecks => Set<DepartureCheck>();
    public DbSet<Tariff> Tariffs => Set<Tariff>();
    public DbSet<ParkingTariff> ParkingTariffs => Set<ParkingTariff>();
    public DbSet<ParkingSpot> ParkingSpots => Set<ParkingSpot>();
    public DbSet<ParkingReservation> ParkingReservations => Set<ParkingReservation>();
    public DbSet<ParkingSession> ParkingSessions => Set<ParkingSession>();
    public DbSet<RevenueReceipt> RevenueReceipts => Set<RevenueReceipt>();
    public DbSet<RevenueLine> RevenueLines => Set<RevenueLine>();
    public DbSet<ExpenseEntry> ExpenseEntries => Set<ExpenseEntry>();
    public DbSet<PremisesUnit> PremisesUnits => Set<PremisesUnit>();
    public DbSet<LeaseContract> LeaseContracts => Set<LeaseContract>();
    public DbSet<ShiftSettlement> ShiftSettlements => Set<ShiftSettlement>();
    public DbSet<DailyClose> DailyCloses => Set<DailyClose>();
    public DbSet<AdjustmentEntry> AdjustmentEntries => Set<AdjustmentEntry>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);

        builder.Entity<BusStation>(b =>
        {
            b.ToTable("BusStations"); b.ConfigureByConvention();
            b.Property(x => x.Code).HasMaxLength(BusConsts.CodeLength).IsRequired();
            b.Property(x => x.Name).HasMaxLength(BusConsts.NameLength).IsRequired();
            b.Property(x => x.Address).HasMaxLength(500);
            b.Property(x => x.TimeZone).HasMaxLength(64).IsRequired();
            b.HasIndex(x => x.Code).IsUnique();
        });
        builder.Entity<UserStationAssignment>(b =>
        {
            b.ToTable("UserStationAssignments"); b.ConfigureByConvention();
            b.HasIndex(x => new { x.UserId, x.StationId }).IsUnique();
            b.HasIndex(x => x.UserId).HasFilter("\"IsPrimary\" = TRUE AND \"IsActive\" = TRUE").IsUnique();
            b.HasIndex(x => new { x.UserId, x.IsActive });
            b.HasOne<BusStation>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<TransportOperator>(b =>
        {
            ConfigureCoded(b, "TransportOperators");
            b.HasIndex(x => x.StationId);
            b.HasOne<BusStation>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<FixedRoute>(b =>
        {
            ConfigureCoded(b, "FixedRoutes");
            b.HasIndex(x => x.OperatorId);
            b.HasIndex(x => x.StationId);
            b.HasOne<TransportOperator>().WithMany().HasForeignKey(x => x.OperatorId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<BusStation>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Vehicle>(b =>
        {
            b.ToTable("Vehicles"); b.ConfigureByConvention();
            b.Property(x => x.PlateNumber).HasMaxLength(BusConsts.CodeLength).IsRequired();
            b.Property(x => x.VehicleType).HasMaxLength(BusConsts.TypeLength).IsRequired();
            b.HasIndex(x => x.PlateNumber).IsUnique(); b.HasIndex(x => x.OperatorId); b.HasIndex(x => x.StationId);
            b.HasOne<TransportOperator>().WithMany().HasForeignKey(x => x.OperatorId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<BusStation>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Driver>(b =>
        {
            b.ToTable("Drivers"); b.ConfigureByConvention();
            b.Property(x => x.FullName).HasMaxLength(BusConsts.NameLength).IsRequired();
            b.Property(x => x.LicenseNumber).HasMaxLength(BusConsts.CodeLength).IsRequired();
            b.HasIndex(x => x.LicenseNumber).IsUnique(); b.HasIndex(x => x.StationId);
            b.HasOne<BusStation>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<VehicleLegalDocument>(b =>
        {
            b.ToTable("VehicleLegalDocuments"); b.ConfigureByConvention();
            b.Property(x => x.DocumentType).HasMaxLength(BusConsts.TypeLength).IsRequired();
            b.Property(x => x.ExpiresOn).HasColumnType("date");
            b.HasIndex(x => new { x.VehicleId, x.DocumentType }).IsUnique();
            b.HasIndex(x => new { x.StationId, x.ExpiresOn, x.IsActive });
            b.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<BusStation>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<CarrierContract>(b =>
        {
            b.ToTable("CarrierContracts"); b.ConfigureByConvention();
            b.Property(x => x.ContractNumber).HasMaxLength(BusConsts.CodeLength).IsRequired();
            b.Property(x => x.StartDate).HasColumnType("date"); b.Property(x => x.EndDate).HasColumnType("date");
            b.HasIndex(x => x.ContractNumber).IsUnique(); b.HasIndex(x => new { x.StationId, x.OperatorId, x.EndDate });
            b.HasOne<BusStation>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TransportOperator>().WithMany().HasForeignKey(x => x.OperatorId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<DepartureTrip>(b =>
        {
            b.ToTable("DepartureTrips"); b.ConfigureByConvention();
            b.Property(x => x.BusinessDate).HasColumnType("date");
            b.Property(x => x.ShiftCode).HasMaxLength(BusConsts.CodeLength).IsRequired();
            b.Property(x => x.Status).HasMaxLength(BusConsts.StatusLength).IsRequired();
            b.HasIndex(x => new { x.StationId, x.BusinessDate, x.ShiftCode });
            b.HasIndex(x => new { x.Status, x.ScheduledDepartureUtc });
            b.HasOne<BusStation>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TransportOperator>().WithMany().HasForeignKey(x => x.OperatorId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<FixedRoute>().WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Driver>().WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<DepartureCheck>(b =>
        {
            b.ToTable("DepartureChecks"); b.ConfigureByConvention();
            b.Property(x => x.CheckType).HasMaxLength(BusConsts.TypeLength).IsRequired();
            b.Property(x => x.Note).HasMaxLength(BusConsts.DescriptionLength);
            b.HasIndex(x => new { x.DepartureId, x.CheckType }).IsUnique();
            b.HasOne<DepartureTrip>().WithMany().HasForeignKey(x => x.DepartureId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<Tariff>(b =>
        {
            b.ToTable("Tariffs"); b.ConfigureByConvention();
            b.Property(x => x.VehicleType).HasMaxLength(BusConsts.TypeLength).IsRequired();
            b.Property(x => x.FeeType).HasMaxLength(BusConsts.TypeLength).IsRequired();
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.EffectiveFrom).HasColumnType("date"); b.Property(x => x.EffectiveTo).HasColumnType("date");
            b.HasIndex(x => new { x.StationId, x.RouteId, x.VehicleType, x.FeeType, x.EffectiveFrom }).IsUnique();
            b.HasOne<BusStation>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<FixedRoute>().WithMany().HasForeignKey(x => x.RouteId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ParkingTariff>(b =>
        {
            b.ToTable("ParkingTariffs"); b.ConfigureByConvention();
            b.Property(x => x.VehicleType).HasMaxLength(BusConsts.TypeLength).IsRequired();
            b.Property(x => x.Description).HasMaxLength(BusConsts.DescriptionLength).IsRequired();
            b.Property(x => x.RatePerUnit).HasPrecision(18, 2);
            b.Property(x => x.MinimumCharge).HasPrecision(18, 2);
            b.Property(x => x.EffectiveFrom).HasColumnType("date"); b.Property(x => x.EffectiveTo).HasColumnType("date");
            b.HasIndex(x => new { x.StationId, x.VehicleType, x.EffectiveFrom }).IsUnique();
            b.HasIndex(x => new { x.StationId, x.VehicleType, x.EffectiveFrom, x.EffectiveTo, x.IsActive });
            b.HasOne<BusStation>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ParkingSpot>(b =>
        {
            b.ToTable("ParkingSpots"); b.ConfigureByConvention();
            b.Property(x => x.Code).HasMaxLength(BusConsts.CodeLength).IsRequired();
            b.Property(x => x.Name).HasMaxLength(BusConsts.NameLength).IsRequired();
            b.Property(x => x.VehicleType).HasMaxLength(BusConsts.TypeLength);
            b.HasIndex(x => new { x.StationId, x.Code }).IsUnique();
            b.HasIndex(x => new { x.StationId, x.IsActive });
            b.HasAlternateKey(x => new { x.Id, x.StationId });
            b.HasOne<BusStation>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ParkingReservation>(b =>
        {
            b.ToTable("ParkingReservations"); b.ConfigureByConvention();
            b.Property(x => x.VehiclePlateNumber).HasMaxLength(BusConsts.CodeLength).IsRequired();
            b.Property(x => x.VehicleType).HasMaxLength(BusConsts.TypeLength).IsRequired();
            b.Property(x => x.Note).HasMaxLength(BusConsts.DescriptionLength);
            b.Property(x => x.Status).HasMaxLength(BusConsts.StatusLength).IsRequired();
            b.Property(x => x.StartUtc).HasColumnType("timestamp with time zone");
            b.Property(x => x.EndUtc).HasColumnType("timestamp with time zone");
            b.HasIndex(x => new { x.StationId, x.StartUtc, x.EndUtc });
            b.HasIndex(x => new { x.ParkingSpotId, x.StartUtc, x.Status });
            b.HasIndex(x => new { x.StationId, x.VehiclePlateNumber, x.StartUtc, x.Status });
            b.HasAlternateKey(x => new { x.Id, x.StationId });
            b.HasOne<BusStation>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ParkingSpot>().WithMany().HasForeignKey(x => new { x.ParkingSpotId, x.StationId })
                .HasPrincipalKey(x => new { x.Id, x.StationId }).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ParkingSession>(b =>
        {
            b.ToTable("ParkingSessions"); b.ConfigureByConvention();
            b.Property(x => x.BusinessDate).HasColumnType("date");
            b.Property(x => x.ShiftCode).HasMaxLength(BusConsts.CodeLength).IsRequired();
            b.Property(x => x.VehiclePlateNumber).HasMaxLength(BusConsts.CodeLength).IsRequired();
            b.Property(x => x.VehicleType).HasMaxLength(BusConsts.TypeLength).IsRequired();
            b.Property(x => x.ArrivalUtc).HasColumnType("timestamp with time zone");
            b.Property(x => x.ExitUtc).HasColumnType("timestamp with time zone");
            b.Property(x => x.RatePerUnit).HasPrecision(18, 2);
            b.Property(x => x.MinimumCharge).HasPrecision(18, 2);
            b.Property(x => x.TariffDescription).HasMaxLength(BusConsts.DescriptionLength).IsRequired();
            b.Property(x => x.ChargedAmount).HasPrecision(18, 2);
            b.Property(x => x.Status).HasMaxLength(BusConsts.StatusLength).IsRequired();
            b.Property(x => x.CancellationReason).HasMaxLength(BusConsts.DescriptionLength);
            b.HasIndex(x => x.ParkingReservationId).IsUnique().HasFilter("\"ParkingReservationId\" IS NOT NULL");
            b.HasIndex(x => new { x.StationId, x.ParkingSpotId }).IsUnique()
                .HasFilter("\"Status\" = 'Open' AND \"ParkingSpotId\" IS NOT NULL");
            b.HasIndex(x => new { x.StationId, x.BusinessDate, x.Status });
            b.HasIndex(x => new { x.StationId, x.BusinessDate, x.VehiclePlateNumber })
                .HasFilter("\"Status\" = 'Open'").IsUnique();
            b.HasAlternateKey(x => new { x.Id, x.StationId });
            b.HasOne<BusStation>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ParkingTariff>().WithMany().HasForeignKey(x => x.ParkingTariffId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ParkingSpot>().WithMany().HasForeignKey(x => new { x.ParkingSpotId, x.StationId })
                .HasPrincipalKey(x => new { x.Id, x.StationId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ParkingReservation>().WithMany().HasForeignKey(x => new { x.ParkingReservationId, x.StationId })
                .HasPrincipalKey(x => new { x.Id, x.StationId }).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<RevenueReceipt>(b =>
        {
            b.ToTable("RevenueReceipts", table => table.HasCheckConstraint(
                "CK_RevenueReceipt_SourceType",
                "\"SourceType\" IN ('FixedRoute', 'VisitingVehicle', 'PublicBus', 'Parking', 'Premises', 'Other') AND (\"SourceType\" <> 'Premises' OR \"PremisesUnitId\" IS NOT NULL) AND ((\"SourceType\" = 'Parking' AND ((\"ParkingSessionId\" IS NOT NULL AND \"IsLegacyParking\" = FALSE) OR (\"ParkingSessionId\" IS NULL AND \"IsLegacyParking\" = TRUE))) OR (\"SourceType\" <> 'Parking' AND \"ParkingSessionId\" IS NULL AND \"IsLegacyParking\" = FALSE))"));
            b.ConfigureByConvention();
            b.Property(x => x.ReceiptNumber).HasMaxLength(BusConsts.CodeLength).IsRequired();
            b.Property(x => x.ShiftCode).HasMaxLength(BusConsts.CodeLength).IsRequired();
            b.Property(x => x.SourceType).HasMaxLength(BusConsts.TypeLength).IsRequired();
            b.Property(x => x.IdempotencyKey).HasMaxLength(128);
            b.Property(x => x.SourceReference).HasMaxLength(256);
            b.Property(x => x.VehiclePlateNumber).HasMaxLength(BusConsts.CodeLength);
            b.Property(x => x.IsLegacyParking).IsRequired().HasDefaultValue(false);
            b.HasIndex(x => x.ParkingSessionId).IsUnique().HasFilter("\"ParkingSessionId\" IS NOT NULL");
            b.Property(x => x.TotalAmount).HasPrecision(18, 2);
            b.Property(x => x.BusinessDate).HasColumnType("date"); b.Property(x => x.Status).HasMaxLength(BusConsts.StatusLength).IsRequired();
            b.HasIndex(x => x.ReceiptNumber).IsUnique();
            b.HasIndex(x => new { x.StationId, x.BusinessDate, x.ShiftCode });
            b.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
            b.Ignore(x => x.Lines);
            b.HasOne<BusStation>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<DepartureTrip>().WithMany().HasForeignKey(x => x.DepartureId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<TransportOperator>().WithMany().HasForeignKey(x => x.OperatorId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PremisesUnit>().WithMany().HasForeignKey(x => new { x.PremisesUnitId, x.StationId })
                .HasPrincipalKey(x => new { x.Id, x.StationId }).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ParkingSession>().WithMany().HasForeignKey(x => new { x.ParkingSessionId, x.StationId })
                .HasPrincipalKey(x => new { x.Id, x.StationId }).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<RevenueLine>(b =>
        {
            b.ToTable("RevenueLines"); b.ConfigureByConvention();
            b.Property(x => x.Description).HasMaxLength(BusConsts.DescriptionLength).IsRequired();
            b.Property(x => x.Quantity).HasPrecision(18, 2); b.Property(x => x.UnitAmount).HasPrecision(18, 2); b.Property(x => x.LineTotal).HasPrecision(18, 2);
            b.HasIndex(x => x.ReceiptId);
            b.HasOne<RevenueReceipt>().WithMany().HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<Tariff>().WithMany().HasForeignKey(x => x.TariffId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ExpenseEntry>(b =>
        {
            b.ToTable("ExpenseEntries"); b.ConfigureByConvention();
            b.Property(x => x.BusinessDate).HasColumnType("date"); b.Property(x => x.ShiftCode).HasMaxLength(BusConsts.CodeLength).IsRequired();
            b.Property(x => x.Category).HasMaxLength(BusConsts.TypeLength).IsRequired(); b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.Description).HasMaxLength(BusConsts.DescriptionLength).IsRequired(); b.Property(x => x.Status).HasMaxLength(BusConsts.StatusLength).IsRequired();
            b.HasIndex(x => new { x.StationId, x.BusinessDate, x.ShiftCode });
            b.HasOne<BusStation>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PremisesUnit>(b =>
        {
            b.ToTable("PremisesUnits"); b.ConfigureByConvention();
            b.Property(x => x.Code).HasMaxLength(BusConsts.CodeLength).IsRequired(); b.Property(x => x.Name).HasMaxLength(BusConsts.NameLength).IsRequired();
            b.Property(x => x.AreaSquareMeters).HasPrecision(18, 2); b.Property(x => x.Location).HasMaxLength(500);
            b.HasIndex(x => new { x.StationId, x.Code }).IsUnique();
            b.HasAlternateKey(x => new { x.Id, x.StationId });
            b.HasOne<BusStation>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<LeaseContract>(b =>
        {
            b.ToTable("LeaseContracts"); b.ConfigureByConvention();
            b.Property(x => x.TenantName).HasMaxLength(BusConsts.NameLength).IsRequired(); b.Property(x => x.RentAmount).HasPrecision(18, 2);
            b.Property(x => x.RentPeriod).HasMaxLength(BusConsts.TypeLength).IsRequired(); b.Property(x => x.Status).HasMaxLength(BusConsts.StatusLength).IsRequired();
            b.Property(x => x.StartDate).HasColumnType("date"); b.Property(x => x.EndDate).HasColumnType("date");
            b.HasIndex(x => new { x.StationId, x.EndDate });
            b.HasOne<BusStation>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<PremisesUnit>().WithMany().HasForeignKey(x => x.PremisesUnitId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ShiftSettlement>(b =>
        {
            b.ToTable("ShiftSettlements"); b.ConfigureByConvention();
            b.Property(x => x.BusinessDate).HasColumnType("date"); b.Property(x => x.ShiftCode).HasMaxLength(BusConsts.CodeLength).IsRequired();
            b.Property(x => x.TotalRevenue).HasPrecision(18, 2); b.Property(x => x.TotalExpense).HasPrecision(18, 2); b.Property(x => x.Status).HasMaxLength(BusConsts.StatusLength).IsRequired();
            b.HasIndex(x => new { x.StationId, x.BusinessDate, x.ShiftCode }).IsUnique();
            b.HasOne<BusStation>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<DailyClose>(b =>
        {
            b.ToTable("DailyCloses"); b.ConfigureByConvention();
            b.Property(x => x.BusinessDate).HasColumnType("date"); b.Property(x => x.TotalRevenue).HasPrecision(18, 2); b.Property(x => x.TotalExpense).HasPrecision(18, 2); b.Property(x => x.Status).HasMaxLength(BusConsts.StatusLength).IsRequired();
            b.HasIndex(x => new { x.StationId, x.BusinessDate }).IsUnique();
            b.HasOne<BusStation>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<AdjustmentEntry>(b =>
        {
            b.ToTable("AdjustmentEntries", table => table.HasCheckConstraint(
                "CK_AdjustmentEntry_ExactlyOneTarget",
                "(\"ReceiptId\" IS NOT NULL) <> (\"ExpenseId\" IS NOT NULL) AND \"Amount\" <> 0"));
            b.ConfigureByConvention();
            b.Property(x => x.Amount).HasPrecision(18, 2); b.Property(x => x.Reason).HasMaxLength(BusConsts.DescriptionLength).IsRequired(); b.Property(x => x.Status).HasMaxLength(BusConsts.StatusLength).IsRequired();
            b.Property(x => x.ApprovedAtUtc).HasColumnType("timestamp with time zone");
            b.HasIndex(x => new { x.StationId, x.CreationTime });
            b.HasIndex(x => new { x.StationId, x.Status });
            b.HasOne<BusStation>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<RevenueReceipt>().WithMany().HasForeignKey(x => x.ReceiptId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ExpenseEntry>().WithMany().HasForeignKey(x => x.ExpenseId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("OutboxMessages"); b.HasKey(x => x.Id);
            b.Property(x => x.EventName).HasMaxLength(512).IsRequired(); b.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
            b.Property(x => x.CorrelationId).HasMaxLength(128).IsRequired(); b.Property(x => x.LastError).HasMaxLength(1000);
            b.HasIndex(x => new { x.PublishedAt, x.DeadLetteredAt, x.LeaseUntil, x.CreationTime });
        });
    }

    private static void ConfigureCoded<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> b, string table)
        where TEntity : class
    {
        b.ToTable(table); b.ConfigureByConvention();
        b.Property("Code").HasMaxLength(BusConsts.CodeLength).IsRequired();
        b.Property("Name").HasMaxLength(BusConsts.NameLength).IsRequired();
        b.HasIndex("Code").IsUnique();
    }
}
