using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Exceptions;
using Services;

namespace IDP.Controllers;

[Route("[controller]/[action]")]
[Authorize("admin", AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MagicCodeController(ILogger<MagicCodeController> logger, MongoConnection connection) : ControllerBase
{
    [HttpPost("{id}")]
    public async Task<IActionResult> Sign([FromRoute] Guid id, [FromForm] string privateKey, [FromServices] MagicCodeService service)
    {
        var code = await connection.Filter<MagicAuthenticationCode>()
            .Eq(x => x.Id, id)
            .Ne(x => x.IsActive, false)
            .Eq(x => x.Signature, null)
            .FirstOrDefaultAsync();

        if (code == null) throw NotFoundException.New<MagicAuthenticationCode>(id);
        
        var result = service.Sign(code, privateKey);
        if (!result.IsSuccess) throw new BadRequestException(result.Status);

        await connection.Filter<MagicAuthenticationCode>()
            .Eq(x => x.Id, id)
            .Ne(x => x.IsActive, false)
            .Eq(x => x.Signature, null)
            .Update
            .Set(x => x.Signature, result.Value)
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, null)
            .UpdateOneAsync();

        return Ok();
    }
    
    [HttpPost("rsa/generate")]
    public IActionResult GenerateRsaKeyPair()
    {
        using var rsa = RSA.Create(2048);

        return Ok(new
        {
            PublicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey()),
            PrivateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey()),
        });
    }

    [HttpPost("ecdsa/generate")]
    public IActionResult GenerateEcdsaKeyPair()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return Ok(new
        {
            PublicKey = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo()),
            PrivateKey = Convert.ToBase64String(ecdsa.ExportECPrivateKey()),
        });
    }

    [HttpPost("rsa/sign")]
    public IActionResult SignRsaString(string message, string privateKey)
    {
        using var rsa = RSA.Create();
        rsa.ImportRSAPrivateKey(Convert.FromBase64String(privateKey), out _);

        var data = Encoding.UTF8.GetBytes(message);
        var signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);

        return Ok(new
        {
            SignedMessage = $"{message}:{Convert.ToBase64String(signature)}"
        });
    }

    [HttpPost("ecdsa/sign")]
    public IActionResult SignEcdsaString(string message, string privateKey)
    {
        using var ecdsa = ECDsa.Create();

        // Import the private key (assumes PKCS#8 format from ssh-keygen)
        ecdsa.ImportECPrivateKey(Convert.FromBase64String(privateKey), out _);

        var data = Encoding.UTF8.GetBytes(message);

        // This will output the signature in DER (ASN.1) format by default
        var signature = ecdsa.SignData(data, HashAlgorithmName.SHA256);

        return Ok(new
        {
            SignedMessage = $"{message}:{Convert.ToBase64String(signature)}"
        });
    }

    [HttpPost("rsa/verify")]
    public IActionResult VerifyRsaString(string signedString, string publicKey)
    {
        try
        {
            var lastColon = signedString.LastIndexOf(':');
            var message = signedString.Substring(0, lastColon);
            var sigBase64 = signedString.Substring(lastColon + 1);

            using var rsa = RSA.Create();
            rsa.ImportRSAPublicKey(Convert.FromBase64String(publicKey), out _);

            var data = Encoding.UTF8.GetBytes(message);
            var signature = Convert.FromBase64String(sigBase64);

            var result = rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            if (result) return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating signature");
        }

        return BadRequest("Invalid signature");
    }

    [HttpPost("ecdsa/verify")]
    public IActionResult VerifyEcdsaString(string signedString, string publicKey)
    {
        try
        {
            var lastColon = signedString.LastIndexOf(':');
            var message = signedString.Substring(0, lastColon);
            var sigBase64 = signedString.Substring(lastColon + 1);

            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);

            var data = Encoding.UTF8.GetBytes(message);
            var signature = Convert.FromBase64String(sigBase64);

            var result = ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256);
            if (result) return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating signature");
        }

        return BadRequest("Invalid signature");
    }
}
