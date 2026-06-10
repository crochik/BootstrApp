using Crochik.Mongo;
using McpServer.Tools.Attributes;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;

namespace McpServer.Tools;

public class TerritoryTools(ILogger<TerritoryTools> logger, MongoConnection connection)
{
    [McpTool(Name = "find_organization_for_postal_code", Description = "Find the Organization that serves postal code",
        ExamplePrompts = new[] { "Which organization services zip code 90210?", "Find the org for postal code 33101" })]
    public async Task<string> GetUserProfileAsync(
        IEntityContext context, 
        [McpParameter(Description = "Postal Code", Required = true)] string postalCode
    )
    {
        postalCode = PI.Shared.Models.Lead.GetPostalCodeForLookup(postalCode);
        if (postalCode == null)
        {
            return "Error: Invalid postal code";
        }

        var query = connection.Filter<CustomObject>()
                .Eq(x => x.AccountId, context.AccountId)
                .Eq(x => x.ObjectType, "ZeeTerritory")
                .Eq(x => x.ExternalId, postalCode)
                .Ne(x => x.IsActive, false)
            ;
        
        var match = await query.FirstOrDefaultAsync();

        if (match == null)
        {
            logger.LogInformation("{PostalCode} not found", postalCode);
            return "Postal code is not serviced by any organization";
        }

        var orgQuery = connection.Filter<Entity, Organization>()
            .Eq(x => x.Id, match.EntityId)
            .Ne(x => x.IsActive, false);
        
        var organization = await orgQuery.FirstOrDefaultAsync();
        if (organization == null)
        {
            logger.LogInformation("{OrganizationId} not found/inactive", match.EntityId);
            return "Postal code is not serviced by any organization";
        }

        return
            $"""
             Postal Code {postalCode} is serviced by {organization.Name}.
             Phone Number: {organization.Phone}
             Email: {organization.Email}
             Id: {organization.Id}
             """;
    }

    [McpTool(Name = "get_territory_for_organization", Description = "Get Territory serviced by Organization",
        ExamplePrompts = new[] { "What postal codes does this organization serve?", "Show me the territory for an organization" })]
    public async Task<string> GetTerritoryAsync(
        IEntityContext context,
        [McpParameter(Description = "Organization Id", Required = true)]
        string organizationId
    )
    {
        if (!Guid.TryParse(organizationId, out var id))
        {
            return "Error: Invalid Organization ID";   
        }
        
        var orgQuery = connection.Filter<Entity, Organization>()
            .Eq(x => x.Id, id)
            .Ne(x => x.IsActive, false);
        
        var organization = await orgQuery.FirstOrDefaultAsync();
        if (organization == null)
        {
            logger.LogInformation("{OrganizationId} not found/inactive", id);
            return "Error: Organization not available.";
        }

        var zips = await connection.Filter<CustomObject>()
            .Eq(x => x.AccountId, context.AccountId)
            .Eq(x => x.ObjectType, "ZeeTerritory")
            .Eq(x => x.EntityId, id)
            .Ne(x => x.IsActive, false)
            .IncludeFields(x => x.ExternalId, x => x.Properties["Type"])
            .FindAsync();

        return string.Join("\n", Enumerable.Empty<string>()
            .Append("| Postal Code | Type |")
            .Append("| -- | -- |")
            .Concat(zips.Select(x=>$"| {x.ExternalId} | {x.Properties["Type"]} |"))
            .Append("| -- | -- |")
        );
    }
}