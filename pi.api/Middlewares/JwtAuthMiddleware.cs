using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace pi.api.Middlewares;

public class JwtAuthMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var req = await context.GetHttpRequestDataAsync();

        if (req == null)
        {
            await next(context);
            return;
        }

        var httpContext = context.GetHttpContext();
        if (httpContext == null)
        {
            await next(context);
            return;
        }

        //Sprawdzenie czy funkcja ma atrybut [Authorize]
        var requiresAuth = context.FunctionDefinition
            .InputBindings.Values
            .Any(b => b.Type == "httpTrigger")
            && HasAuthorizeAttribute(context);

        if (!requiresAuth)
        {
            await next(context);
            return;
        }

        var authService = context.InstanceServices
            .GetRequiredService<IAuthenticationService>();

        var result = await authService.AuthenticateAsync(httpContext, null);

        if (result.Succeeded)
        {
            httpContext.User = result.Principal!;
            await next(context);
            return;
        }

        var response = req.CreateResponse(HttpStatusCode.Unauthorized);
        await response.WriteStringAsync("Unauthorized");
        context.GetInvocationResult().Value = response;
    }

    private static bool HasAuthorizeAttribute(FunctionContext context)
    {
        var entryPoint = context.FunctionDefinition.EntryPoint;

        //entryPoint to "Namespace.KlasaFunkcji.NazwaMetody"
        var assemblyName = context.FunctionDefinition.PathToAssembly;
        var assembly = System.Reflection.Assembly.LoadFrom(assemblyName);

        var typeName = entryPoint[..entryPoint.LastIndexOf('.')];
        var methodName = entryPoint[(entryPoint.LastIndexOf('.') + 1)..];

        var type = assembly.GetType(typeName);
        var method = type?.GetMethod(methodName);

        return (method?.GetCustomAttributes(typeof(AuthorizeAttribute), true).Any() ?? false)
            || (type?.GetCustomAttributes(typeof(AuthorizeAttribute), true).Any() ?? false);
    }

}