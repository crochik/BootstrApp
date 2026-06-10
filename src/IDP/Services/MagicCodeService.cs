using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using PI.Shared.Models;

namespace Services;

public class MagicCodeService(ILogger<MagicCodeService> logger, MongoConnection connection)
{
    // TODO: should be part of the configuration
    private const string PublicKey = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEJ69u3NJGAGfGSEzF2/qQVNBFZ93BfUhN3IOx9rJAv8lppIOZQWx4tAm1gEBfiMT16lVEac6XRuKq4OGHazDCGw==";
    
    public async Task<Result<MagicCodeResult>> GetAndValidateAsync(string code, string clientId)
    {
        var magicCode = await connection.Filter<MagicAuthenticationCode>()
            .Eq(x => x.Code, code)
            .Eq(x => x.ClientId, clientId)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (magicCode == null) 
        {
            logger.LogError("Invalid {MagicAuthenticationCode}", code);
            return Result.Error<MagicCodeResult>("Invalid Code");
        }
        
        var valid = VerifySignature(magicCode, PublicKey);
        if (!valid.IsSuccess)
        {
            logger.LogError("Failed to verify signature: {Status}", valid.Status);
            return valid.ConvertTo<MagicCodeResult>();
        }
        
        var client = await connection.Filter<AppClient>()
            .Eq(x => x.ClientId, clientId)
            .Ne(x => x.Enabled, false)
            .FirstOrDefaultAsync();

        if (client == null || !client.AuthenticationProviders.ContainsKey("MagicCode"))
        {
            logger.LogError("Invalid {Client}", clientId);
            return Result.Error<MagicCodeResult>("Invalid Client");
        }

        if (client.AccountId.HasValue && client.AccountId != magicCode.AccountId)
        {
            logger.LogError("Account Mismatch");
            return Result.Error<MagicCodeResult>("Account Mismatch");
        }

        var user = await connection.Filter<Entity, User>()
            .Eq(x => x.AccountId, magicCode.AccountId)
            .Eq(x => x.Id, magicCode.EntityId)
            .Ne(x => x.IsActive, false)
            // .Eq(x => x.UserRoleId, nameof(EntityRoleId.Profile))
            .FirstOrDefaultAsync();

        if (user == null) 
        {
            logger.LogError("{UserId} not found", magicCode.EntityId);
            return Result.Error<MagicCodeResult>("User Not Found");
        }
        
        return Result.Success(new MagicCodeResult
        {
            MagicAuthenticationCode = magicCode,
            User = user,
            Client = client,
        });
    }

    private string BuildPlainMessage(MagicAuthenticationCode code)
    {
        return string.Join("|", [
            code.ClientId,
            code.Id.ToString(),
            code.AccountId.ToString(),
            code.EntityId.ToString(),
            code.ProfileId?.ToString() ?? "ANY",
            code.Code
        ]);
    }
    
    public Result<string> Sign(MagicAuthenticationCode code, string privateKey)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportECPrivateKey(Convert.FromBase64String(privateKey), out _);
        var data = Encoding.UTF8.GetBytes(BuildPlainMessage(code));

        // This will output the signature in DER (ASN.1) format by default
        var signature = ecdsa.SignData(data, HashAlgorithmName.SHA256);

        return Result.Success(Convert.ToBase64String(signature));
    }
    
    public Result<bool> VerifySignature(MagicAuthenticationCode code, string publicKey)
    {
        try
        {
            var message = BuildPlainMessage(code);
            var sigBase64 = code.Signature;

            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);

            var data = Encoding.UTF8.GetBytes(message);
            var signature = Convert.FromBase64String(sigBase64);

            var result = ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256);
            if (result) return Result.Success(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating signature");
            return Result.Error<bool>(ex.Message);
        }

        return Result.Error<bool>("Invalid Signature");
    }
}

[BsonCollection("idp.MagicAuthenticationCode")]
public class MagicAuthenticationCode : EntityOwnedModel
{
    public string Code { get; set; }
    public Guid? ProfileId { get; set; }
    public string ClientId { get; set; }
    public string Signature { get; set; }
    public bool IsActive { get; set; }
}

public class MagicCodeResult
{
    public MagicAuthenticationCode MagicAuthenticationCode { get; init; }
    public User User { get; init; }
    public AppClient Client { get; init; }
}