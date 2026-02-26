using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Http.BatchFormatters;
using Zuto.SerilogExtensions;

namespace SQLScriptFlatten.API;

public class Program
{
    public static void Main(string[] args)
    {
        var loggerConfiguration = CreateLoggerConfig();
        Log.Logger = loggerConfiguration.CreateLogger();

        try
        {
            Log.Information("Starting web host");
            CreateHostBuilder(args).Build().Run();
        }
        catch (Exception e)
        {
            Log.Fatal(e,"Host terminated unexpectedly");
            Log.CloseAndFlush();
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());

    private static LoggerConfiguration CreateLoggerConfig()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var isDevelopment = string.Equals(Environments.Development, environment, StringComparison.OrdinalIgnoreCase);

        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("CorrelationId", LogEventLevel.Warning)
            .Filter.ByExcluding("RequestPath = '/health'")
            .Enrich.WithProperty("type", "demo-api")
            .Enrich.FromLogContext()
            .WriteTo.Console();

        var logstashUrl = Environment.GetEnvironmentVariable("LogstashUrl") ?? "http://logstash.ecs.dev.zuto.cloud:8903";

        return isDevelopment
            ? loggerConfiguration
            : loggerConfiguration.WriteTo.Http(
                logstashUrl,
                textFormatter: new LogstashJsonFormatter(),
                batchFormatter: new ArrayBatchFormatter());
    }
}