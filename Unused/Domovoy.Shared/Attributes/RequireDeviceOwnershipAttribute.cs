using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Domovoy.Shared.Attributes;

/// <summary>
/// Атрибут для сверки владения устройством (JWT claim DeviceId / Subject vs роут/параметр)
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class RequireDeviceOwnershipAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _parameterName;

    public RequireDeviceOwnershipAttribute(string parameterName = "id")
    {
        _parameterName = parameterName;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        var tokenDeviceId = user.FindFirstValue("DeviceId")
                          ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(tokenDeviceId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (context.ActionArguments.TryGetValue(_parameterName, out var routeVal) && routeVal is string routeDeviceId)
        {
            if (!string.Equals(tokenDeviceId, routeDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new ForbidResult();
                return;
            }
        }

        await next();
    }
}
