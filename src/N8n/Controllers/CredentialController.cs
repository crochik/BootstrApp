using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;

namespace N8n.Controllers;

/// <summary>
/// Credential test endpoint. The n8n credential's <c>test</c> request hits
/// <c>GET /n8n/v1/me</c> after the user enters their token: a 200 confirms the JWT is
/// accepted (the <c>n8n</c> policy has already rejected bad/insufficient tokens with a
/// 401/403) and returns the caller's identity to label the connection.
/// </summary>
[Authorize("n8n")]
[Route("/n8n/v1")]
public class CredentialController : APIController
{
    [HttpGet("me")]
    public IActionResult Me() => Ok(new
    {
        id = Context.UserId,
        account = Context.AccountId,
        organization = Context.OrganizationId,
        role = Context.Role.ToString(),
    });
}
