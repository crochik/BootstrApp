using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;

namespace PI.Shared.Services.DataProtection;

public class MicrosoftDataProtectionServiceProvider : IDataProtectionServiceProvider
{
    public string ProviderName => "MicrosoftDataProtection";

    private readonly ILogger<MicrosoftDataProtectionServiceProvider> _logger;
    private readonly IDataProtectionProvider _provider;

    public MicrosoftDataProtectionServiceProvider(
        ILogger<MicrosoftDataProtectionServiceProvider> logger,
        IDataProtectionProvider provider
        )
    {
        _logger = logger;
        _provider = provider;
    }

    private IDataProtector GetProtector(DataProtectionConfig dataProtection)
    {
        if (dataProtection is not MicrosoftDataProtectionConfig config) throw new BadRequestException("Unexpected config");

        // TODO: cache?
        // ...
        var protector = _provider.CreateProtector(config.Purpose);
        return protector;
    }

    public ValueTask<string> ProtectAsync(IEntityContext context, DataProtectionConfig dataProtection, string plainText)
    {
        var protector = GetProtector(dataProtection);
        var encrypted = protector.Protect(plainText);
        return ValueTask.FromResult(encrypted);
    }

    public ValueTask<string> UnprotectAsync(IEntityContext context, DataProtectionConfig dataProtection, string encrypted)
    {
        var protector = GetProtector(dataProtection);
        var plainText = protector.Unprotect(encrypted);
        return ValueTask.FromResult(plainText);
    }
}