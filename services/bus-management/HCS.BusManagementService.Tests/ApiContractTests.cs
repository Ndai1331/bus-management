using HCS.BusManagementService.Contracts;
using HCS.BusManagementService.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCS.BusManagementService.Tests;

public sealed class ApiContractTests
{
    [Fact]
    public void Parking_operations_stay_under_the_revenue_bff_route()
    {
        var route = typeof(RevenueController).GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>().Single().Template;

        Assert.Equal("api/bus-management/revenue", route);
        Assert.NotNull(typeof(RevenueController).GetMethod(nameof(RevenueController.GetParkingSpots)));
        Assert.NotNull(typeof(RevenueController).GetMethod(nameof(RevenueController.UpdateParkingSpot)));
        Assert.NotNull(typeof(RevenueController).GetMethod(nameof(RevenueController.CreateParkingReservation)));
        Assert.NotNull(typeof(RevenueController).GetMethod(nameof(RevenueController.CheckInParkingReservation)));
        Assert.NotNull(typeof(RevenueController).GetMethod(nameof(RevenueController.CancelParkingReservation)));
        Assert.NotNull(typeof(RevenueController).GetMethod(nameof(RevenueController.CompleteParkingReservation)));
    }

    [Theory]
    [InlineData(nameof(RevenueController.GetParkingSpots), BusPermissions.RevenueParking)]
    [InlineData(nameof(RevenueController.CreateParkingSpot), BusPermissions.RevenueParkingCreate)]
    [InlineData(nameof(RevenueController.UpdateParkingSpot), BusPermissions.RevenueParkingUpdate)]
    [InlineData(nameof(RevenueController.GetParkingReservations), BusPermissions.RevenueParking)]
    [InlineData(nameof(RevenueController.CreateParkingReservation), BusPermissions.RevenueParkingCreate)]
    [InlineData(nameof(RevenueController.CheckInParkingReservation), BusPermissions.RevenueParkingUpdate)]
    [InlineData(nameof(RevenueController.CancelParkingReservation), BusPermissions.RevenueParkingUpdate)]
    [InlineData(nameof(RevenueController.CompleteParkingReservation), BusPermissions.RevenueParkingUpdate)]
    public void Parking_operations_have_explicit_permission_boundaries(string methodName, string permission)
    {
        var method = typeof(RevenueController).GetMethod(methodName)!;
        var authorize = method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>().Single();

        Assert.Equal(permission, authorize.Policy);
    }
}
