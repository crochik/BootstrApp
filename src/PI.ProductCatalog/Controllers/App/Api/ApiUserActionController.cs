using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Controllers;
using PI.Shared.Services;

namespace Controllers;

[Authorize("rest")]
// [ApiExplorerSettings(IgnoreApi = true)]
[ApiExplorerSettings(GroupName = "rest")]
[Route("/productcatalog/api/Object")]
public class ApiUserActionController(
    ILogger<AbstractAppUserActionController> logger, 
    MongoConnection connection, 
    UserActionService service, 
    ObjectTypeService objectTypeService
    ) : AbstractAppUserActionController(logger, connection, service, objectTypeService)
{
}