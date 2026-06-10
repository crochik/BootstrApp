using System;
using AutoMapper;
using Controllers.Models;
using Microsoft.Extensions.Logging;
using PI.Shared.Data.Adapters;
using PI.Shared.Models;

namespace Controllers;

public abstract class AbstractNewModelController<TAdapter, TModel, TApi> :
    AbstractModelController<TAdapter, TModel, TApi>
    where TAdapter : IModelAdapter<TModel>
    where TModel : IEntityOwnedModel
    where TApi : ApiEntityOwnedModel, TModel
{
    protected AbstractNewModelController(ILogger logger, IMapper mapper, TAdapter adapter) :
        base(logger, mapper, adapter)
    {
    }

    protected override TModel Convert(IEntityContext context, TApi api, TModel current = default)
    {
        if (current != null)
        {
            // update
            if (!current.EntityId.Equals(api.EntityId))
            {
                // TODO: allow reassigning? 
                // ...
                return default;
            }

            if (current.Id != api.Id)
            {
                // don't allow changing id
                return default;
            }

            return api;
        }

        // create
        api.Id = Guid.NewGuid();

        switch (context.Role)
        {
            case EntityRoleId.Account:
            case EntityRoleId.Admin:
                if (!context.AccountId.HasValue) return default;
                api.EntityId = context.AccountId.Value;
                break;

            case EntityRoleId.Organization:
            case EntityRoleId.Manager:
                if (!context.OrganizationId.HasValue) return default;
                api.EntityId = context.OrganizationId.Value;
                break;

            case EntityRoleId.User:
                if (!context.UserId.HasValue) return default;
                api.EntityId = context.UserId.Value;
                break;

            default:
                // not allowed
                return default;
        }

        return api;
    }

    protected override bool CanRead(IEntityContext context, TModel row)
    {
        switch (context.Role)
        {
            case EntityRoleId.Account:
                return row.EntityId == context.AccountId.Value;

            case EntityRoleId.Organization:
                return row.EntityId == context.OrganizationId.Value;

            case EntityRoleId.Admin:
                // TODO: should allow to see all users/orgs as well
                // ...
                return row.EntityId == context.AccountId.Value;

            case EntityRoleId.Manager:
            // TODO: should allow to see all users as well
            // ...
            case EntityRoleId.User:
                return row.EntityId == context.AccountId.Value ||
                       row.EntityId == context.OrganizationId.Value ||
                       row.EntityId == context.UserId.Value;

            default:
                return false;
        }
    }

    protected override bool CanUpdate(IEntityContext context, TModel row)
    {
        switch (context.Role)
        {
            case EntityRoleId.Account:
                return row.EntityId == context.AccountId.Value;

            case EntityRoleId.Organization:
                return row.EntityId == context.OrganizationId.Value;

            case EntityRoleId.Admin:
                // TODO: should allow to update from all users/orgs as well
                // ...
                return row.EntityId == context.AccountId.Value;

            case EntityRoleId.Manager:
                // TODO: should allow to from all users as well
                // ...
                return row.EntityId == context.OrganizationId.Value ||
                       row.EntityId == context.UserId.Value;

            case EntityRoleId.User:
                return row.EntityId == context.UserId.Value;

            default:
                return false;
        }
    }

    protected override bool CanDelete(IEntityContext context, TModel row) =>
        CanUpdate(context, row);
}