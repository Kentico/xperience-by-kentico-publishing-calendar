using System;
using System.Net;
using System.Threading;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DancingGoat.DemoData;

/// <summary>
/// Registers the demo data refresh endpoint (<see cref="DemoDataRefreshService"/>) in the Development and CI environments.
/// Called by 'scripts/Update-DemoData.ps1', the Aspire "Refresh demo data" command, and the e2e global setup.
/// </summary>
public static class DemoDataConfiguration
{
    /// <summary>POST: refreshes the demo data and returns a <see cref="DemoDataRefreshSummary"/> as JSON.</summary>
    public const string ROUTE = "/api/demo-data/refresh";

    /// <summary>
    /// Required request header (any value). Browsers cannot send it cross-origin without a CORS preflight,
    /// so pages on other sites cannot trigger the refresh.
    /// </summary>
    public const string HEADER_NAME = "X-Demo-Data-Refresh";

    private const string CI_ENVIRONMENT = "CI";


    /// <summary>
    /// Registers the refresh service in the Development and CI environments.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    public static void AddDancingGoatDemoData(this WebApplicationBuilder builder)
    {
        if (IsEnabled(builder.Environment))
        {
            builder.Services.AddTransient<DemoDataRefreshService>();
        }
    }


    /// <summary>
    /// Maps the refresh endpoint in the Development and CI environments. Accepts loopback requests only.
    /// </summary>
    /// <param name="app">The web application.</param>
    public static void MapDancingGoatDemoData(this WebApplication app)
    {
        if (!IsEnabled(app.Environment))
        {
            return;
        }

        // Optional "today" (yyyy-MM-dd, today or later) shifts the layout, e.g. to check that a later run resets the previous one
        app.MapPost(ROUTE, async (HttpContext context, DateOnly? today, DemoDataRefreshService service, ILogger<DemoDataRefreshService> logger) =>
        {
            if (!IsLoopback(context) || !context.Request.Headers.ContainsKey(HEADER_NAME))
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            try
            {
                // Not canceled with the request: a half-applied refresh would leave mixed states until the next run
                return Results.Json(await service.Refresh(today?.ToDateTime(TimeOnly.MinValue), CancellationToken.None));
            }
            catch (ArgumentOutOfRangeException exception)
            {
                return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest, title: "Invalid demo data refresh request");
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Demo data refresh failed.");

                return Results.Problem(exception.Message, statusCode: StatusCodes.Status500InternalServerError, title: "Demo data refresh failed");
            }
        });
    }


    private static bool IsEnabled(IHostEnvironment environment) =>
        environment.IsDevelopment() || environment.IsEnvironment(CI_ENVIRONMENT);


    private static bool IsLoopback(HttpContext context) =>
        context.Connection.RemoteIpAddress is { } address && IPAddress.IsLoopback(address);
}
