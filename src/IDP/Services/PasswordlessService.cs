using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.Shared.Exceptions;
using PI.Shared.Models;
using PI.Shared.Services;

namespace Services;

public class PasswordlessService(
    ILogger<PasswordlessService> logger,
    MongoConnection connection,
    ObjectTypeService objectTypeService
)
{
    public const string ProviderKey = "Passwordless";
    public const string GrantType = "passwordless";
    private const int PinTtlMinutes = 10;

    public async Task<IResult> RequestPinAsync(StartRequest request)
    {
        var normalizedEmail = Lead.GetNormalizedEmail(request.Email);
        var normalizedPhone = Lead.GetNormalizedPhoneNumber(request.Phone);
        if (normalizedEmail == null && normalizedPhone == null)
        {
            return Result.Error("invalid_request");
        }

        if (string.IsNullOrWhiteSpace(request.CodeChallenge))
            return Result.Error("invalid_request");

        var codeChallengeMethod = string.IsNullOrWhiteSpace(request.CodeChallengeMethod) ? "plain" : request.CodeChallengeMethod;
        if (codeChallengeMethod != "S256" && codeChallengeMethod != "plain")
            return Result.Error("invalid_request");

        if (string.IsNullOrWhiteSpace(request.ClientId))
            return Result.Error("invalid_client");

        var query = connection.Filter<PasswordlessLoginCode>()
            .Eq(x => x.ClientId, request.ClientId)
            .Gt(x => x.ExpiresAt, DateTime.UtcNow)
            .Eq(x => x.IsActive, true);

        if (normalizedEmail != null) query.Eq(x => x.Email, normalizedEmail);
        else query.Eq(x => x.Phone, normalizedPhone);

        var existing = await query.FirstOrDefaultAsync();
        if (existing != null)
        {
            return Result.Unknown("Has requested it already");
        }

        var client = await connection.Filter<AppClient>()
            .Eq(x => x.ClientId, request.ClientId)
            .Ne(x => x.Enabled, false)
            .Ne(x => x.AuthenticationProviders[ProviderKey], null)
            .Ne(x => x.AccountId, null)
            .FirstOrDefaultAsync();

        if (client == null)
        {
            logger.LogError("Passwordless not enabled for {ClientId}", request.ClientId);
            return Result.Error("invalid_client");
        }

        var userQuery = connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, client.AccountId.Value)
            .Eq(x => x.UserRoleId, nameof(EntityRoleId.Profile))
            .Ne(x => x.AppProfiles[client.ProfileKey ?? client.ClientId], default(Guid?))
            .Ne(x => x.IsActive, false);

        if (normalizedEmail != null) userQuery.Eq(x => x.Email, normalizedEmail);
        else userQuery.Eq(x => x.Phone, normalizedPhone);

        var user = await userQuery.FirstOrDefaultAsync();
        if (user == null)
        {
            logger.LogInformation("Passwordless requested for unknown {Identifier} on {ClientId}",
                normalizedEmail ?? normalizedPhone, request.ClientId);
            return Result.Unknown("User not found");
        }

        var objectType = await objectTypeService.GetAsync(new AccountContext(user.AccountId), PasswordlessLoginCode.ObjectTypeFullName);
        if (objectType == null) throw NotFoundException.New(PasswordlessLoginCode.ObjectTypeFullName);

        var row = new PasswordlessLoginCode
        {
            Id = Guid.CreateVersion7(),
            Name = user.Name,
            Description = $"{user.Name} @ {client.ClientName}",
            CreatedOn = DateTime.UtcNow,
            AccountId = client.AccountId.Value,
            EntityId = user.Id,
            ClientId = request.ClientId,
            Email = normalizedEmail ?? user.Email,
            Phone = normalizedPhone ?? user.Phone,
            Pin = GeneratePin(),
            CodeChallenge = request.CodeChallenge,
            CodeChallengeMethod = codeChallengeMethod,
            ExpiresAt = DateTime.UtcNow.AddMinutes(PinTtlMinutes),
            IsActive = true,
            FlowId = objectType.InitialFlowId,
            ObjectStatusId = objectType.InitialObjectStatusId,
            RequestedSMS = normalizedEmail == null,
        };

        await objectTypeService.InsertAsync(user.Context, row);

        return Result.Success(row);
    }

    public async Task<Result<Guid?>> ValidateAndConsumeAsync(
        string clientId,
        string email,
        string phone,
        string codeVerifier,
        string pin)
    {
        var normalizedEmail = Lead.GetNormalizedEmail(email);
        var normalizedPhone = Lead.GetNormalizedPhoneNumber(phone);
        if (normalizedEmail == null && normalizedPhone == null || string.IsNullOrWhiteSpace(codeVerifier) || string.IsNullOrWhiteSpace(pin) || string.IsNullOrWhiteSpace(clientId))
        {
            return Result.Error<Guid?>("invalid_request");
        }

        var rowQuery = connection.Filter<PasswordlessLoginCode>()
            .Eq(x => x.ClientId, clientId)
            .Eq(x => x.Pin, pin)
            .Ne(x => x.IsActive, false)
            .Gte(x => x.ExpiresAt, DateTime.UtcNow);

        if (normalizedEmail != null) rowQuery.Eq(x => x.Email, normalizedEmail);
        else rowQuery.Eq(x => x.Phone, normalizedPhone);

        var row = await rowQuery.FirstOrDefaultAsync();
        if (row == null)
        {
            logger.LogError("Passwordless pin not found / expired / consumed for {ClientId}", clientId);
            return Result.Error<Guid?>("invalid_grant");
        }

        if (!VerifyCodeChallenge(codeVerifier, row.CodeChallenge, row.CodeChallengeMethod))
        {
            logger.LogError("Passwordless code_verifier mismatch for {ClientId}", clientId);
            return Result.Error<Guid?>("invalid_grant");
        }

        var consumed = await connection.Filter<PasswordlessLoginCode>()
            .Eq(x => x.Id, row.Id)
            .Ne(x => x.IsActive, false)
            .Update
            .Set(x => x.IsActive, false)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .UpdateAndGetOneAsync();

        if (consumed == null)
        {
            logger.LogError("Passwordless pin consumption race lost for {RowId}", row.Id);
            return Result.Error<Guid?>("invalid_grant");
        }

        return Result.Success<Guid?>(consumed.EntityId);
    }

    private static string GeneratePin()
    {
        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    }

    private static bool VerifyCodeChallenge(string verifier, string challenge, string method)
    {
        if (string.IsNullOrEmpty(verifier) || string.IsNullOrEmpty(challenge)) return false;

        string actual;
        if (method == "plain")
        {
            actual = verifier;
        }
        else
        {
            var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
            actual = Base64UrlEncode(hash);
        }

        var a = Encoding.ASCII.GetBytes(actual);
        var b = Encoding.ASCII.GetBytes(challenge);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public class StartRequest
    {
        public string ClientId { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string CodeChallenge { get; set; }
        public string CodeChallengeMethod { get; set; }
    }
}