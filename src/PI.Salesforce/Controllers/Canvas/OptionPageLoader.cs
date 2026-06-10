using System.Threading.Tasks;
using Crochik.Logging;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;
using PI.Shared.Salesforce.Models;
using PI.Shared.Salesforce.Models.Canvas;
using PI.Shared.Services;
using User = PI.Shared.Models.User;

namespace Controllers;

public class OptionPageLoader : AbstractPageLoader
{
    private readonly ObjectTypeService _objectTypeService;

    public OptionPageLoader(ILogger<OptionPageLoader> logger, MongoConnection connection, AuthorizationService authorizationService, ObjectTypeService objectTypeService)
        : base(logger, connection, authorizationService)
    {
        _objectTypeService = objectTypeService;
    }

    public override async Task<Result<string>> LoadAsync(SignedRequest signedRequest, User user, AppClient client, Record record, string page, int? height)
    {
        var error = await InitAsync(signedRequest, user, client, record, page, height);
        if (error != null) return Result<string>.Error(error);

        error = await LoadObjectAsync();
        if (error != null) return Result<string>.Error(error);

        return GetRedirection();
    }

    private async Task<string> LoadObjectAsync()
    {
        using var scope = _logger.AddScope(new
        {
            User.AccountId,
            ExternalId = Record.Id,
        });

        var context = new AccountContext(User.AccountId);
        var objectType = await _objectTypeService.GetAsync<SalesforceObjectType>(context, SfOption.ObjectTypeName);
        var sfObject = await _connection.Filter<SfOptionObject>(objectType.CollectionName, objectType.DatabaseName)
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.ExternalId, Record.Id)
            .FirstOrDefaultAsync();

        if (sfObject == null)
        {
            _logger.LogInformation("Object not found locally...");
            return "Proposal hasn't been loaded yet";
        }
        else
        {
            // get from salesforce to check if we have the latest?
            //  ....
        }

        Url = $"{Url}?page:/SalesforceOption?_id={sfObject.Id}";

        return null;
    }
}