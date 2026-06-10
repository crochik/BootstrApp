using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PI.Shared.App;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;

namespace PI.Shared.Services.DataProtection;

public class DataProtectionService : ILifetimeService
{
    private static DataProtectionService _instance = null;
    public static DataProtectionService Get() => _instance;

    private readonly ILogger<DataProtectionService> _logger;
    private readonly Dictionary<string, IDataProtectionServiceProvider> _providers;

    public DataProtectionService(ILogger<DataProtectionService> logger, IEnumerable<IDataProtectionServiceProvider> providers)
    {
        if (_instance != null) throw new Exception("Can't reinitialize service");
        _instance = this;
        
        _logger = logger;
        _providers = providers.ToDictionary(x => x.ProviderName);
    }
    
    public void Start()
    {
        _logger.LogInformation("Starting Data Protection Service");
    }

    public void Stop()
    {
        _logger.LogInformation("Stopping Data Protection Service");
    }

    private IDataProtectionServiceProvider GetProvider(string name)
    {
        if (_providers.TryGetValue(name, out var provider))
        {
            return provider;
        }

        throw new NotFoundException($"{name} provider not found");
    }
    
    public ValueTask<string> ProtectAsync(IEntityContext context, DataProtectionConfig dataProtection, string plainText)
    {
        return GetProvider(dataProtection.ProviderName).ProtectAsync(context, dataProtection, plainText);
    }

    public ValueTask<string> UnprotectAsync(IEntityContext context, DataProtectionConfig dataProtection, string encrypted)
    {
        return GetProvider(dataProtection.ProviderName).UnprotectAsync(context, dataProtection, encrypted);
    }
}

public interface IDataProtectionServiceProvider
{
    string ProviderName { get; }
    ValueTask<string> ProtectAsync(IEntityContext context, DataProtectionConfig dataProtection, string plainText);
    ValueTask<string> UnprotectAsync(IEntityContext context, DataProtectionConfig dataProtection, string encrypted);
}