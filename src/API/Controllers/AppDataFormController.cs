using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Controllers;

[Route("/api/v1/[controller]")]
public class AppDataFormController : APIController
{
    private readonly MongoConnection _connection;

    public AppDataFormController(MongoConnection connection)
    {
        _connection = connection;
    }

    [Authorize("default")]
    [HttpGet("/api/v1/[controller]/{name}/DataForm")]
    public async Task<Form> GetDataFormAsync([FromRoute] string name)
    {
        var form = await _connection.GetProfileElementAsync<AppForm>(Context, name);
        if (form == null) throw new NotFoundException($"{name} not found");

        return form.Form;
    }
}