using System;
using API.Services;
using CorrelationId.HttpClient;
using Data.Dapper;
using Data.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SQLScriptFlatten.API.Ioc;

public static class ServiceModule
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {

        services.AddScoped<IScriptService, ScriptService>();

        return services;
    }

    public static IServiceCollection AddProxies(this IServiceCollection services, IConfiguration configuration)
    {

        
        return services;
    }
    
    public static IServiceCollection AddRepositories(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IScriptRepository, ScriptRepository>();
        return services;
    }
}