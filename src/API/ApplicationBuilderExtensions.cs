using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace SQLScriptFlatten.API;

public static class ApplicationBuilderExtensions
{
    public static void UseCustomErrors(this IApplicationBuilder app, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
            app.Use(WriteDevelopmentResponse);

        app.Use(WriteProductionResponse);
    }

    private static Task WriteDevelopmentResponse(HttpContext httpContext, Func<Task> next)
        => WriteResponse(httpContext, includeDetails: true);

    private static Task WriteProductionResponse(HttpContext httpContext, Func<Task> next)
        => WriteResponse(httpContext, includeDetails: false);

    private static async Task WriteResponse(HttpContext httpContext, bool includeDetails)
    {
        var exceptionDetails = httpContext.Features.Get<IExceptionHandlerFeature>();
        var ex = exceptionDetails?.Error;

        // Should always exist, but best to be safe!
        if (ex != null)
        {
            Log.Logger.Error(ex, "Request: {RequestPath}", httpContext.Request.Path);

            var problem = CreateAProblem(ex, includeDetails, httpContext);

            httpContext.Response.ContentType =
                "application/problem+json";

            await JsonSerializer.SerializeAsync(httpContext.Response.Body, problem);
        }
    }

    private static ProblemDetails CreateAProblem(Exception ex, bool includeDetails, HttpContext httpContext)
    {
        var problem = new ProblemDetails
        {
            Status = 500,
            Title = includeDetails ? $"An error occured: {ex.Message}" : "An error occured",
            Detail = includeDetails ? ex.ToString() : null
        };

        var traceId = httpContext?.TraceIdentifier ?? Activity.Current?.Id;
        if (traceId != null)
        {
            problem.Extensions["traceId"] = traceId;
        }

        return problem;
    }
}