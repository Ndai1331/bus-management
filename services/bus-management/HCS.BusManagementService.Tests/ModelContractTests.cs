using HCS.BusManagementService.Data;
using HCS.BusManagementService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HCS.BusManagementService.Tests;

public sealed class ModelContractTests
{
    [Fact]
    public void All_bus_entities_use_the_bounded_context_schema()
    {
        var options = new DbContextOptionsBuilder<BusManagementDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=postgres")
            .Options;

        using var db = new BusManagementDbContext(options);
        var entityTypes = db.Model.GetEntityTypes()
            .Where(entity => entity.ClrType.Namespace == typeof(BusStation).Namespace)
            .ToArray();

        Assert.NotEmpty(entityTypes);
        Assert.All(entityTypes, entity => Assert.Equal(BusManagementDbContext.Schema, entity.GetSchema()));
    }

    [Fact]
    public void Business_keys_are_unique_in_the_ef_model()
    {
        var options = new DbContextOptionsBuilder<BusManagementDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=postgres")
            .Options;

        using var db = new BusManagementDbContext(options);
        var assignment = db.Model.FindEntityType(typeof(UserStationAssignment))!;
        var station = db.Model.FindEntityType(typeof(BusStation))!;
        var settlement = db.Model.FindEntityType(typeof(ShiftSettlement))!;
        var revenueLine = db.Model.FindEntityType(typeof(RevenueLine))!;
        var adjustment = db.Model.FindEntityType(typeof(AdjustmentEntry))!;
        var vehicleDocument = db.Model.FindEntityType(typeof(VehicleLegalDocument))!;
        var receipt = db.Model.FindEntityType(typeof(RevenueReceipt))!;
        var operatorEntity = db.Model.FindEntityType(typeof(TransportOperator))!;
        var route = db.Model.FindEntityType(typeof(FixedRoute))!;
        var vehicle = db.Model.FindEntityType(typeof(Vehicle))!;
        var driver = db.Model.FindEntityType(typeof(Driver))!;
        var parkingTariff = db.Model.FindEntityType(typeof(ParkingTariff))!;
        var parkingSpot = db.Model.FindEntityType(typeof(ParkingSpot))!;
        var parkingReservation = db.Model.FindEntityType(typeof(ParkingReservation))!;
        var parkingSession = db.Model.FindEntityType(typeof(ParkingSession))!;
        var designAssignment = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(UserStationAssignment))!;
        var assignmentValidFrom = designAssignment.FindProperty(nameof(UserStationAssignment.ValidFrom))!;
        var assignmentValidTo = designAssignment.FindProperty(nameof(UserStationAssignment.ValidTo))!;

        Assert.Equal("timestamp with time zone", assignmentValidFrom.GetColumnType());
        Assert.Equal("timestamp with time zone", assignmentValidTo.GetColumnType());

        Assert.Contains(assignment.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(UserStationAssignment.UserId), nameof(UserStationAssignment.StationId)]));
        Assert.Contains(station.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(BusStation.Code)]));
        Assert.Contains(settlement.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(ShiftSettlement.StationId), nameof(ShiftSettlement.BusinessDate), nameof(ShiftSettlement.ShiftCode)]));
        Assert.DoesNotContain(revenueLine.GetProperties(), property => property.Name == "RevenueReceiptId");
        Assert.Contains(revenueLine.GetForeignKeys(), foreignKey => foreignKey.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(RevenueLine.ReceiptId)]) && foreignKey.PrincipalEntityType.ClrType == typeof(RevenueReceipt));
        Assert.Contains(vehicleDocument.GetProperties(), property => property.Name == nameof(VehicleLegalDocument.StationId));
        var designAdjustment = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(AdjustmentEntry))!;
        Assert.Contains(designAdjustment.GetCheckConstraints(), constraint => constraint.Name == "CK_AdjustmentEntry_ExactlyOneTarget");
        Assert.Contains(receipt.GetProperties(), property => property.Name == nameof(RevenueReceipt.SourceReference));
        Assert.Contains(receipt.GetProperties(), property => property.Name == nameof(RevenueReceipt.VehiclePlateNumber));
        Assert.Contains(receipt.GetProperties(), property => property.Name == nameof(RevenueReceipt.PremisesUnitId));
        Assert.All(new[] { operatorEntity, route, vehicle, driver }, entity =>
            Assert.Contains(entity.GetProperties(), property => property.Name == nameof(TransportOperator.StationId)));
        var designReceipt = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(RevenueReceipt))!;
        Assert.Contains(designReceipt.GetCheckConstraints(), constraint => constraint.Name == "CK_RevenueReceipt_SourceType");
        Assert.Contains(receipt.GetForeignKeys(), foreignKey => foreignKey.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(RevenueReceipt.PremisesUnitId), nameof(RevenueReceipt.StationId)]) &&
            foreignKey.PrincipalEntityType.ClrType == typeof(PremisesUnit));
        Assert.Contains(parkingTariff.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(ParkingTariff.StationId), nameof(ParkingTariff.VehicleType), nameof(ParkingTariff.EffectiveFrom)]));
        Assert.Contains(parkingSpot.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(ParkingSpot.StationId), nameof(ParkingSpot.Code)]));
        Assert.Contains(parkingReservation.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(ParkingReservation.ParkingSpotId), nameof(ParkingReservation.StartUtc), nameof(ParkingReservation.Status)]));
        Assert.Contains(parkingReservation.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([
                nameof(ParkingReservation.ParkingSpotId), nameof(ParkingReservation.StationId)]) &&
            foreignKey.PrincipalEntityType.ClrType == typeof(ParkingSpot));
        Assert.Contains(parkingSession.GetIndexes(), index => index.IsUnique &&
            index.GetFilter() == "\"Status\" = 'Open' AND \"ParkingSpotId\" IS NOT NULL" &&
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(ParkingSession.StationId), nameof(ParkingSession.ParkingSpotId)]));
        Assert.Contains(parkingSession.GetIndexes(), index => index.IsUnique && index.GetFilter() == "\"Status\" = 'Open'" &&
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(ParkingSession.StationId), nameof(ParkingSession.BusinessDate), nameof(ParkingSession.VehiclePlateNumber)]));
        Assert.Contains(parkingSession.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([
                nameof(ParkingSession.ParkingSpotId), nameof(ParkingSession.StationId)]) &&
            foreignKey.PrincipalEntityType.ClrType == typeof(ParkingSpot));
        Assert.Contains(parkingSession.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([
                nameof(ParkingSession.ParkingReservationId), nameof(ParkingSession.StationId)]) &&
            foreignKey.PrincipalEntityType.ClrType == typeof(ParkingReservation));
        Assert.Contains(receipt.GetProperties(), property => property.Name == nameof(RevenueReceipt.ParkingSessionId));
        Assert.Contains(receipt.GetProperties(), property => property.Name == nameof(RevenueReceipt.IsLegacyParking));
        Assert.Contains(receipt.GetForeignKeys(), foreignKey => foreignKey.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(RevenueReceipt.ParkingSessionId), nameof(RevenueReceipt.StationId)]) &&
            foreignKey.PrincipalEntityType.ClrType == typeof(ParkingSession));
        Assert.Contains(designReceipt.GetCheckConstraints(), constraint => constraint.Name == "CK_RevenueReceipt_SourceType" &&
            constraint.Sql!.Contains("ParkingSessionId", StringComparison.Ordinal) &&
            constraint.Sql.Contains("IsLegacyParking", StringComparison.Ordinal));
    }
}
