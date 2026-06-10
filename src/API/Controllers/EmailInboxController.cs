using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Controllers;

[Route("/api/v1/[controller]")]
public class EmailInboxController : AbstractObjectTypeController<EmailInbox>
{
    public EmailInboxController(
        ObjectTypeService objectTypeService
    ) : base(objectTypeService)
    {
    }
}