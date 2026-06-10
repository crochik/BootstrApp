using Microsoft.Extensions.DependencyInjection;

namespace Services;

public static partial class IServiceCollectionExtensions
{
    public static IServiceCollection AddDataLoader(this IServiceCollection services)
    {
        services.AddScoped<AccountObjectImporter>();
        services.AddScoped<LeadObjectImporter>();
        services.AddScoped<ServiceResourceUserObjectImporter>();
        services.AddScoped<ServiceTerritoryObjectImporter>();
        services.AddScoped<ServiceTerritoryMemberObjectImporter>();
        services.AddScoped<UserObjectImporter>();
        services.AddScoped<ServiceAppointmentConverter>();
        
        return services;
    }
}
