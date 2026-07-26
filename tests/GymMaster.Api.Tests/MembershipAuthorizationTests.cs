using System.Reflection;
using GymMaster.API.Entities;
using GymMaster.API.Features.Billing;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace GymMaster.Api.Tests;

public sealed class MembershipAuthorizationTests
{
    [Theory]
    [InlineData(nameof(MembershipsController.Sell))]
    [InlineData(nameof(MembershipsController.ConfirmPayment))]
    [InlineData(nameof(MembershipsController.Renew))]
    public void Operational_membership_endpoints_are_staff_only(string actionName)
    {
        var action = typeof(MembershipsController).GetMethod(
            actionName,
            BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(action);
        var authorize = Assert.Single(action!.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(RoleNames.Staff, authorize.Roles);
    }
}
