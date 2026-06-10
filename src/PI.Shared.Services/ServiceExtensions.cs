using Microsoft.Extensions.DependencyInjection;
using PI.Shared.Salesforce;
using PI.Shared.Services.DataProtection;

namespace PI.Shared.Services;

public static class ServiceExtensions 
{
    public static IServiceCollection AddLeadBuilderService(this IServiceCollection services)
    {
        services.AddSingleton<IValueMapperService, ValueMapperService>();
        
        // TODO: make it a singleton ... can't find any reason why it would need to be transient but... 
        services.AddTransient<LeadBuilderService>();

        return services;
    }

    public static IServiceCollection AddObjectTypeService(this IServiceCollection services)
    {
        services.AddSingleton<ObjectTypeService>();

        // add data protection service as it is a hard dependency 
        // won't do anything unless providers are also added
        services.AddSingleton<DataProtectionService>();

        // used to build data views 
        services.AddTransient<ObjectTypeIntrospector>();
        services.AddTransient<ObjectDataViewBuilder>();
        
        return services;
    }

    public static IServiceCollection AddSalesforceService(this IServiceCollection services)
    {
        services.AddSingleton<NetCoreForceClient>();
        services.AddSingleton<SalesforceService>();

        return services;
    }

    public static IServiceCollection AddReportService(this IServiceCollection services)
    {
        services.AddSingleton<ReportService>();

        return services;
    }

    public static IServiceCollection AddSyncService(this IServiceCollection services)
    {
        services.AddSingleton<JobStatusService>();

        return services;
    }
}