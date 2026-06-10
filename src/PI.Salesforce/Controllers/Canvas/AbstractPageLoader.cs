using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Crochik.Mongo;
using IdentityModel;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PI.Shared.Models;
using PI.Shared.Salesforce.Models.Canvas;
using PI.Shared.Services;
using User = PI.Shared.Models.User;

namespace Controllers;

public interface IObjectLoader
{
    Task<Result<string>> LoadAsync(SignedRequest signedRequest, User user, AppClient client, Record record, string page, int? height);
}

public abstract class AbstractPageLoader : IObjectLoader
{
    protected readonly ILogger<AbstractPageLoader> _logger;
    protected readonly MongoConnection _connection;
    protected readonly AuthorizationService _authorizationService;

    private SignedRequest SignedRequest { get; set; }
    protected User User { get; set; }
    protected AppClient Client { get; set; }
    protected Record Record { get; set; }
    protected string Page { get; set; }
    protected Guid? ProfileId { get; set; }

    protected string Url { get; set; }
    protected List<Claim> Claims { get; set; }
    private int? Height { get; set; }
    
    protected AbstractPageLoader(
        ILogger<AbstractPageLoader> logger, 
        MongoConnection connection,
        AuthorizationService authorizationService
        )
    {
        _logger = logger;
        _connection = connection;
        _authorizationService = authorizationService;
    }

    public abstract Task<Result<string>> LoadAsync(SignedRequest signedRequest, User user, AppClient client, Record record, string page, int? height);
    
    protected async Task<string> InitAsync(SignedRequest signedRequest, User user, AppClient client, Record record, string page, int? height)
    {
        var profile = await _authorizationService.GetProfileAsync(user, client);
        
        ProfileId = profile?.Id;
        if (!ProfileId.HasValue)
        {
            _logger.LogError("User has no profile for {ClientId}", client.ClientId);
            return "No Profile";
        }

        SignedRequest = signedRequest;
        User = user;
        Client = client;
        Record = record;
        Claims = await _authorizationService.GetAllClaimsAsync(user, profile, client);
        Url = client.ClientUri;
        Page = page;
        Height = height;

        return null;
    }

    protected Result<string> GetRedirection()
    {
        if (Height < 300 || Height > 2000) Height = null;

        foreach (var param in SignedRequest.Context.Environment.Parameters)
        {
            Claims.Add(new Claim($"client_param_{param.Key}", param.Value));
        }

        if (Height.HasValue)
        {
            Claims.Add(new Claim("client_embedded_height", $"{Height}"));
        }

        var jwt = _authorizationService.GenerateJwtToken(Claims, TimeSpan.FromHours(4));
        if (!jwt)
        {
            _logger.LogError("Failed to generate Jwt Token for {UserId}", User.Id);
            return Result.Error<string>(jwt.Status);
        }

        var clientJson = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(SignedRequest.Client)));

        return Result.Success($"{Url}#{jwt.Value}#{clientJson}");
    }
}