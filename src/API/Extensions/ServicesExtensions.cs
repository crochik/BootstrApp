using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using PI.Shared.O365.Extensions;

namespace App
{
    public static class ServicesExtensions
    {
        public static IServiceCollection AddMapper(this IServiceCollection services)
        {
            var config = new MapperConfiguration(cfg =>
            {
                cfg.AddCalendar();
                cfg.AddMaps(typeof(ServicesExtensions).Assembly);
            });

            var mapper = config.CreateMapper();

            services.AddSingleton<IMapper>(mapper);

            return services;
        }

    }
}
