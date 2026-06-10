using System;
using System.Net.Http;
using AutoMapper;
using Crochik.Messaging;
using Crochik.NET.APM;
using Crochik.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PI.Zoom
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpClient(Microsoft.Extensions.Options.Options.DefaultName).
                ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    UseCookies = false
                });
            
            services
                .AddDataProtection(Configuration)
                ;

            var config = new MapperConfiguration(cfg =>
            {
            });
            var mapper = config.CreateMapper();
            
            services.AddSingleton<IMessageBroker, RabbitMessageBroker>();
            // services.AddSingleton<IAPMService, ElastAPMService>();
            services.AddSingleton<IMapper>(mapper);

            // services.AddSingleton<ZoomService>();

            throw new NotImplementedException();
            // services.AddSqlAdapters();

            services.AddMvc();

            // services.AddSwaggerGen(c =>
            // {
            //     c.SwaggerDoc("v1", new Info { Title = "ProgramInterface", Version = "v1" });

            //     // c.OperationFilter<SecurityRequirementsOperationFilter>();
            //     // c.OperationFilter<OperationIdFilter>();

            //     // var scheme = new OAuth2Scheme
            //     // {
            //     //     Type = "oauth2",
            //     //     AuthorizationUrl = $"{_config.Authority}/connect/authorize", // ?acr_values=idp:InspireNet
            //     //     Flow = "implicit",
            //     //     TokenUrl = $"{_config.Authority}/connect/token",
            //     //     Scopes = new Dictionary<string, string> {
            //     //         { _config.APIName, "ProgramInterface API" }
            //     //     }
            //     // };

            //     // c.AddSecurityDefinition("oauth2", scheme);
            //     // c.CustomSchemaIds(x => x.FullName);
            // });            
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(
            IApplicationBuilder app, 
            IHostingEnvironment env
            )
        {
            // app.UseAPM(Configuration);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // app.UseHsts();
                // app.UseHttpsRedirection();
            }

            app.UseMvc();

            // app.UseSwagger();
            // app.UseSwaggerUI(c =>
            // {
            //     c.SwaggerEndpoint("/swagger/v1/swagger.json", "ProgramInterface");
            // });

            // app.ApplicationServices.GetRequiredService<ZoomService>().Start();
        }
    }
}
