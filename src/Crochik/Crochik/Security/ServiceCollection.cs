using System;
using System.IO;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crochik.Security;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add data protection for Apps not using the MicroserviceApp
    /// </summary>
    public static IServiceCollection AddDataProtection(this IServiceCollection services, IConfiguration configuration)
    {
        var config = configuration.GetDataProtectionConfig();
        if (config == null) return services;

        if (config.UseAWS)
        {
            services.AddDefaultAWSOptions(configuration.GetAWSOptions());

            var prefix = config.KeysPath ?? "/Sched.Onl/DataProtection";
            System.Console.WriteLine($"DataProtection, using AWS Systems Manager: {prefix}");
            services.AddDataProtection()
                .PersistKeysToAWSSystemsManager(prefix)
                .SetDefaultKeyLifetime(TimeSpan.FromDays(config.KeysLifeTime))
                .SetApplicationName(config.ApplicationName);

            return services;
        }

        var keysPath = config?.KeysPath ?? Directory.GetCurrentDirectory() + "/.dataprotection-keys";
        System.Console.WriteLine($"DataProtection, using local path: {keysPath}");

        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
            .SetDefaultKeyLifetime(TimeSpan.FromDays(config.KeysLifeTime))
            .SetApplicationName(config.ApplicationName);

        return services;
    }
}

public static class ConfigurationExtensions
{
    public static DataProtectionConfig GetDataProtectionConfig(this IConfiguration configuration)
    {
        return configuration.GetSection("DataProtection").Get<DataProtectionConfig>();
    }
}

public class DataProtectionConfig
{
    public string KeysPath { get; set; }
    public string DeveloperSigningCredential { get; set; } = "PI";
    public string ApplicationName { get; set; } = "ProgramInterface";
    public int KeysLifeTime { get; set; } = 30;
    public bool UseAWS { get; set; } = true;
}