using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Domovoy.Shared.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Domovoy.Auth.Service.Tests;

public class RequireDeviceOwnershipAttributeTests
{
    [Fact]
    public async Task OnActionExecutionAsync_MatchingDeviceId_CallsNextDelegate()
    {
        // Arrange
        var filter = new RequireDeviceOwnershipAttribute("id");
        var claims = new[] { new Claim("DeviceId", "device-123") };
        var identity = new ClaimsIdentity(claims, "Test");
        var user = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = user };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var actionArguments = new Dictionary<string, object?> { { "id", "device-123" } };

        var executingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), actionArguments, new object());
        var calledNext = false;
        ActionExecutionDelegate next = () => { calledNext = true; return Task.FromResult<ActionExecutedContext>(null!); };

        // Act
        await filter.OnActionExecutionAsync(executingContext, next);

        // Assert
        Assert.True(calledNext);
        Assert.Null(executingContext.Result);
    }

    [Fact]
    public async Task OnActionExecutionAsync_MismatchedDeviceId_SetsForbidResult()
    {
        // Arrange
        var filter = new RequireDeviceOwnershipAttribute("id");
        var claims = new[] { new Claim("DeviceId", "device-123") };
        var identity = new ClaimsIdentity(claims, "Test");
        var user = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = user };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var actionArguments = new Dictionary<string, object?> { { "id", "other-device-456" } };

        var executingContext = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), actionArguments, new object());
        var calledNext = false;
        ActionExecutionDelegate next = () => { calledNext = true; return Task.FromResult<ActionExecutedContext>(null!); };

        // Act
        await filter.OnActionExecutionAsync(executingContext, next);

        // Assert
        Assert.False(calledNext);
        Assert.IsType<ForbidResult>(executingContext.Result);
    }
}
