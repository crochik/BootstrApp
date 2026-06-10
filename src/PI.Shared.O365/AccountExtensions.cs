using System;
using PI.Shared.Extensions;
using PI.Shared.Models;

namespace PI.Shared.O365;

public static class AccountExtensions 
{
    public static bool TryGetMicrosoftTenantId(this Account account, out Guid tenantId)
    {
        if (account?.FirstIdentity(ExternalProvider.Microsoft) is not EntityIdentity accountIdentity)
        {
            tenantId = Guid.Empty;
            return false;
        }

        return Guid.TryParse(accountIdentity.ExternalId, out tenantId);
    }
    
    public static bool TryGetMicrosoftTenantId(this User user, out Guid tenantId)
    {
        if (user.FirstIdentity(ExternalProvider.Microsoft) is EntityIdentity userIdentity && userIdentity.Data.TryGetGuidParam("TenantId", out tenantId))
        {
            return true;
        }

        tenantId = Guid.Empty;
        return false;
    }
}