using System;
using System.Data;
using System.Text.Json.Nodes;
using API.Models;
using API.Services;
using CorrelationId;
using CorrelationId.DependencyInjection;
using Data;
using Data.Dapper;
using SQLScriptFlatten.API.Ioc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Retry;
using Serilog;

namespace SQLScriptFlatten.API;

public class Startup
{
    
    
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    private IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        // Configure options
        services.Configure<ScriptExecutionOptions>(Configuration.GetSection("ScriptExecution"));
        services.Configure<TableCacheOptions>(Configuration.GetSection("TableCache"));
        
        services.AddScoped<IDbConnectionFactory, DbConnectionFactory>();
        
        // Register TableCacheService as singleton for caching table names
        services.AddSingleton<TableCacheService>();

        
        services.AddSingleton<AsyncRetryPolicy>(sp =>
            Policy.Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount:5,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(30),
                    onRetry: (ex, ts, retryCount, ctx) =>
                    {
                        var logger = sp.GetRequiredService<ILogger<Startup>>();
                        logger.LogWarning(ex, $"Retry {retryCount} after {ctx}s seconds due to error.", retryCount,ts.TotalSeconds);
                    }));
        
        services.AddScoped<IQuery,DapperQuery>();
        
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
            });
        });
        
        services.AddControllers();
        services.AddSwaggerGen(
            c => { c.SwaggerDoc("v1", new OpenApiInfo { Title = "API", Version = "v1" }); });
        services.AddHealthChecks();
        services
            .AddCorrelationId(options =>
            {
                options.UpdateTraceIdentifier = true;
                options.AddToLoggingScope = true;
            })
            .WithGuidProvider();
        
        services
            .AddServices()
            .AddProxies(Configuration)
            .AddRepositories(Configuration);
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        
        app.UseCorrelationId();

        if (env.IsDevelopment()) 
            app.UseDeveloperExceptionPage();

        app.UseCors("AllowAll");
        
        app.UseRouting();
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1"));

        var option = new RewriteOptions().AddRedirect("^$", "swagger");
        app.UseRewriter(option);
        app.UseEndpoints(builder => builder.MapControllers());
        app.UseExceptionHandler(builder => builder.UseCustomErrors(env));

        app.UseSerilogRequestLogging();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapHealthChecks("health");
        });
    }
}
