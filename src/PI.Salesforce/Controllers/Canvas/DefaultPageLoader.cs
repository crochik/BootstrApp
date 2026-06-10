using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;
using PI.Shared.Salesforce.Models.Canvas;
using PI.Shared.Services;
using User = PI.Shared.Models.User;

namespace Controllers;

public class DefaultPageLoader : AbstractPageLoader
{
    public DefaultPageLoader(ILogger<DefaultPageLoader> logger, MongoConnection connection, AuthorizationService authorizationService) : base(logger, connection, authorizationService)
    {
    }

    public override async Task<Result<string>> LoadAsync(SignedRequest signedRequest, User user, AppClient client, Record record, string page, int? height)
    {
        var error = await InitAsync(signedRequest, user, client, record, page, height);
        if (error!=null) return Result<string>.Error(error);

        return GetRedirection();
    }
}