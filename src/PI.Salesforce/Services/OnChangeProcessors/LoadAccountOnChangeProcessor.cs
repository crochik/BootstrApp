using System;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public class LoadAccountOnChangeProcessor : LoadObjectOnChangeProcessor<SAccountObject>, IOnAccountChangeProcessor
{
    private readonly LoadLeadOnChangeProcessor _leadLoader;
    public override string ObjectType => "sf_Account";

    public LoadAccountOnChangeProcessor(
        ILogger<LoadAccountOnChangeProcessor> logger,
        MongoConnection connection,
        ObjectTypeService objectTypeService,
        SalesforceService salesforceService,
        LoadLeadOnChangeProcessor leadOnObjectChangeProcessor
    ) : base(logger, connection, objectTypeService, salesforceService)
    {
        _leadLoader = leadOnObjectChangeProcessor;
    }

    protected override async Task<IFlowObject> ImportObjectAsync(ImportObject options)
    {
        var lead = default(Lead);
        if (options.AdditionalContext?.LeadId.HasValue ?? false)
        {
            lead = await _connection.Filter<Lead>()
                .Eq(x => x.AccountId, options.Context.AccountId.Value)
                .Eq(x => x.Id, options.AdditionalContext.LeadId.Value)
                .FirstOrDefaultAsync();
        }

        lead ??= await _leadLoader.FindLeadBySfAccountIdAsync(options.Context, sfAccount: options.Source);

        if (lead != null)
        {
            _logger.LogInformation("Found {LeadId} for {AccountExternalId}: Update it", lead.Id, options.Source.ExternalId);
            return await _leadLoader.UpdateLeadAsync(options.Context, options.Source, lead);
        }

        _logger.LogInformation("SfLead hasn't been imported yet, could create lead with just sfaccount");
        return await _leadLoader.ImportLeadAsync(options.Context, null, options.Source);
    }
}

public class SAccountObject : SalesforceCustomObject, ILeadReference, IAssignedEntityId
{
    public Guid? LeadId { get; set; }
    public Guid? AssignedEntityId { get; set; }
}