using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Services;

namespace Controllers;

[Authorize("rest")]
// [ApiExplorerSettings(IgnoreApi = true)]
[ApiExplorerSettings(GroupName = "rest")]
[Route("/app/api/Object")]
public class ApiUserActionController(
    ILogger<AbstractAppUserActionController> logger, 
    MongoConnection connection, 
    UserActionService service, 
    ObjectTypeService objectTypeService
    ) : AbstractAppUserActionController(logger, connection, service, objectTypeService)
{
}