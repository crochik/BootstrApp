using Controllers;
using Microsoft.Extensions.DependencyInjection;

namespace Services;

public static partial class IServiceCollectionExtensions
{
    public static IServiceCollection AddOnChangeProcessors(this IServiceCollection services)
    {
        services.AddScoped<LoadLeadOnChangeProcessor>();
        services.AddScoped<LoadAccountOnChangeProcessor>();
        services.AddScoped<LoadServiceAppointmentOnChangeProcessor>();

        services.AddScoped<IOnLeadChangeProcessor, LoadLeadOnChangeProcessor>();
        services.AddScoped<IOnAccountChangeProcessor, LoadAccountOnChangeProcessor>();
        services.AddScoped<IOnServiceAppointmentChangeProcessor, LoadServiceAppointmentOnChangeProcessor>();
        services.AddScoped<IOnWorkOrderChangeProcessor, LoadWorkOrderOnChangeProcessor>();

        services.AddScoped<LeadPageLoader>();
        services.AddScoped<AccountPageLoader>();
        services.AddScoped<ServiceAppointmentPageLoader>();
        services.AddScoped<WorkOrderPageLoader>();
        services.AddScoped<OptionPageLoader>();
        services.AddScoped<DefaultPageLoader>();
        
        return services;
    }
}
