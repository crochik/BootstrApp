using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Identity;
using PI.Shared.Models;

namespace Services;

public abstract class AbstractIdentityProvider : IIdentityProvider
{
    protected readonly IMapper _mapper;

    public abstract string Name { get; }

    public abstract ValueTask<ExternalIdentity> GetTenantAsync(ExternalLoginInfo loginInfo, ExternalIdentity userIdentity);

    protected AbstractIdentityProvider(IMapper mapper)
    {
        _mapper = mapper;
    }

    public virtual ValueTask<ExternalIdentity> GetIdentityAsync(ExternalLoginInfo loginInfo)
    {
        var userIdentity = _mapper.Map<ExternalIdentity>(loginInfo);
        return ValueTask.FromResult(userIdentity);
    }

    public Account BuildAccount(ExternalIdentity tenantEntity)
    {
        var entityIdentity = _mapper.Map<EntityIdentity>(tenantEntity);
        var id = Guid.NewGuid();
        var account = new Account
        {
            Id = id,
            AccountId = id,
            EntityId = id, // should be CSS?
            Name = tenantEntity.Name,
            Identities = new[]
            {
                entityIdentity
            }
        };

        return account;
    }

    public User BuildUser(Account account, ExternalIdentity userIdentity)
    {
        var tenantId = account.Identities?
            .FirstOrDefault(x => x.IdentityProviderId == userIdentity.Provider.ToString())?
            .ExternalId;

        var identity = _mapper.Map<EntityIdentity>(userIdentity);
        if (tenantId != null)
        {
            identity.Data ??= new Dictionary<string, object>();
            identity.Data["TenantId"] = Guid.Parse(tenantId);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            EntityId = account.Id,
            // UserRoleId = EntityRoleId.Admin.ToString(),
            Name = userIdentity.Name,
            IsActive = true,
            Email = userIdentity.Email,
            TimeZoneId = userIdentity.TimeZoneId,
            Identities =
            [
                identity
            ]
        };

        return user;
    }
}