using HCS.BusManagementService.Data;
using HCS.BusManagementService.Domain;
using Microsoft.EntityFrameworkCore;

namespace HCS.BusManagementService.Tests;

public sealed class ModelContractTests
{
    [Fact]
    public void All_bus_entities_use_the_bounded_context_schema()
    {
        var options = new DbContextOptionsBuilder<BusManagementDbContext>()
            .UseInMemoryDatabase(nameof(All_bus_entities_use_the_bounded_context_schema))
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
            .UseInMemoryDatabase(nameof(Business_keys_are_unique_in_the_ef_model))
            .Options;

        using var db = new BusManagementDbContext(options);
        var assignment = db.Model.FindEntityType(typeof(UserStationAssignment))!;
        var station = db.Model.FindEntityType(typeof(BusStation))!;
        var settlement = db.Model.FindEntityType(typeof(ShiftSettlement))!;
        var revenueLine = db.Model.FindEntityType(typeof(RevenueLine))!;

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
    }
}
