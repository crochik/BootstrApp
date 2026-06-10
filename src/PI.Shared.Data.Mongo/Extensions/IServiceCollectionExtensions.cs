using Crochik.Mongo;
using PI.Shared.Data.Adapters;
using PI.Shared.Data.Mongo.Adapters;
using PI.Shared.Data.Mongo;

namespace Microsoft.Extensions.DependencyInjection
{
    public static partial class IServiceCollectionExtensions
    {
        public static IServiceCollection AddMongoConnection(this IServiceCollection services)
        {
            services.AddSingleton<MongoConnection.IRegisterClassMap, MapperInitializer>();
            services.AddSingleton<MongoConnection>();

            return services;
        }

        public static IServiceCollection AddMongoIdentityAdapters(this IServiceCollection services)
        {
            services.AddTransient<IEntityIdentityAdapter, EntityIdentityAdapter>();
            services.AddTransient<IUserAdapter, UserAdapter>();
            services.AddTransient<IOrganizationAdapter, OrganizationAdapter>();
            services.AddTransient<IAccountAdapter, AccountAdapter>();

            return services;
        }

        public static IServiceCollection AddMongoAdapters(this IServiceCollection services)
        {
            services.AddMongoIdentityAdapters();

            services.AddTransient<IAppointmentTypeAdapter, AppointmentTypeAdapter>();
            
            services.AddTransient<AppointmentAdapter>();
            services.AddTransient<ILeadTypeAdapter, LeadTypeAdapter>();
            services.AddTransient<ILeadAdapter, LeadAdapter>();

            services.AddTransient<IEventTypeAdapter, EventTypeAdapter>();
            services.AddTransient<IFlowAdapter, FlowAdapter>();
            // services.AddTransient<IFlowTransitionAdapter, FlowAdapter>();
            services.AddTransient<ILeadStatusAdapter, LeadStatusAdapter>();

            services.AddTransient<IIntegrationAdapter, IntegrationAdapter>();
            services.AddTransient<IEntityIntegrationAdapter, EntityIntegrationAdapter>();
            services.AddTransient<IIntegrationLeadAdapter, IntegrationLeadAdapter>();
            services.AddTransient<IIntegrationAppointmentAdapter, IntegrationAppointmentAdapter>();
            services.AddTransient<IAppointmentTypeIntegrationAdapter, AppointmentTypeIntegrationAdapter>();
            services.AddTransient<ILeadTypeIntegrationAdapter, LeadTypeIntegrationAdapter>();

            services.AddTransient<IEntityMetadataAdapter, EntityMetadataAdapter>();
            services.AddTransient<AvailabilityAdapter>();

            services.AddTransient<IPostalCodeAdapter, PostalCodeAdapter>();
            
            return services;
        }
    }
}
