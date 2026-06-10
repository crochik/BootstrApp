using System.Security.Cryptography;
using System.Text;
using Ingress.Configuration;
using Ingress.Validation;
using Xunit;

namespace Ingress.Tests;

public class EcdsaSignatureValidatorTests
{
    private const string Timestamp = "1600000000";
    private const string Body = "[{\"event\":\"delivered\",\"email\":\"a@b.com\"}]";

    private static (string PublicKey, string Signature) SignWithFreshKey(string timestamp, string body)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signed = Encoding.UTF8.GetBytes(timestamp + body);
        var signature = ecdsa.SignData(signed, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        return (Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo()), Convert.ToBase64String(signature));
    }

    private static AuthConfig Config(string publicKey) => new()
    {
        Type = "ecdsa",
        Header = "X-Sig",
        TimestampHeader = "X-Ts",
        PublicKey = publicKey
    };

    [Fact]
    public void Accepts_valid_ecdsa_signature()
    {
        var (publicKey, signature) = SignWithFreshKey(Timestamp, Body);
        var context = TestContextFactory.Create(
            new WebhookDefinition(), body: Body,
            headers: new Dictionary<string, string> { ["X-Sig"] = signature, ["X-Ts"] = Timestamp });

        Assert.True(new EcdsaSignatureValidator().Validate(context, Config(publicKey)).Succeeded);
    }

    [Fact]
    public void Rejects_tampered_body()
    {
        var (publicKey, signature) = SignWithFreshKey(Timestamp, Body);
        var context = TestContextFactory.Create(
            new WebhookDefinition(), body: Body + "tampered",
            headers: new Dictionary<string, string> { ["X-Sig"] = signature, ["X-Ts"] = Timestamp });

        Assert.False(new EcdsaSignatureValidator().Validate(context, Config(publicKey)).Succeeded);
    }

    [Fact]
    public void Rejects_wrong_timestamp()
    {
        var (publicKey, signature) = SignWithFreshKey(Timestamp, Body);
        var context = TestContextFactory.Create(
            new WebhookDefinition(), body: Body,
            headers: new Dictionary<string, string> { ["X-Sig"] = signature, ["X-Ts"] = "1699999999" });

        Assert.False(new EcdsaSignatureValidator().Validate(context, Config(publicKey)).Succeeded);
    }

    [Fact]
    public void Rejects_missing_signature_header()
    {
        var (publicKey, _) = SignWithFreshKey(Timestamp, Body);
        var context = TestContextFactory.Create(
            new WebhookDefinition(), body: Body,
            headers: new Dictionary<string, string> { ["X-Ts"] = Timestamp });

        Assert.False(new EcdsaSignatureValidator().Validate(context, Config(publicKey)).Succeeded);
    }
}
