using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PI.Shared.Services;

public static class FileStorageIServiceCollectionExtensions 
{
    public static IServiceCollection AddRemoteFileService(this IServiceCollection services)
    {
        services
            .AddSingleton<RemoteFileService>()
            .AddSingleton<IRemoteFileServiceProvider, AwsS3RemoteFileServiceProvider>()
            .AddSingleton<IRemoteFileServiceProvider, FtpRemoteFileServiceProvider>()
            .AddSingleton<IRemoteFileServiceProvider, SftpRemoteFileServiceProvider>()
            ;
        
        return services;
    }

    public static IServiceCollection AddFileStorage(this IServiceCollection services, IConfiguration configuration)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS")))
        {
            System.Console.WriteLine("> using AWS For file storage");
            AddS3FileStorageService(services, configuration);
        }
        else
        {
            System.Console.WriteLine("> using GCP For file storage");
            AddGcpFileStorageService(services);
        }

        return services;
    }

    private static void AddGcpFileStorageService(IServiceCollection services)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS")))
        {
            throw new Exception("Missing required GOOGLE_APPLICATION_CREDENTIALS environment variable");
        }
        
        services.AddSingleton<IFileStorageService, GcpFileStorageService>();
    }

    private static void AddS3FileStorageService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDefaultAWSOptions(configuration.GetAWSOptions());
        services.AddAWSService<Amazon.S3.AmazonS3Client>();
        services.AddSingleton<IFileStorageService, AwsFileStorageService>();
    }
}