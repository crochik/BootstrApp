using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Ingress.Configuration;
using Ingress.Engine;
using Ingress.Validation;
using Xunit;

namespace Ingress.Tests;

public class ClientCertValidatorTests
{
    private static X509Certificate2 SelfSigned(string subject)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private static WebhookContext WithCert(X509Certificate2? cert) => new()
    {
        Definition = new WebhookDefinition(),
        Method = "POST",
        RawBody = Array.Empty<byte>(),
        Headers = new Dictionary<string, string>(),
        Query = new Dictionary<string, string>(),
        ClientCertificate = cert
    };

    [Fact]
    public void Accepts_allowed_thumbprint()
    {
        using var cert = SelfSigned("CN=apiserver");
        var config = new AuthConfig { Type = "clientCert", Thumbprints = { cert.Thumbprint } };

        Assert.True(new ClientCertValidator().Validate(WithCert(cert), config).Succeeded);
    }

    [Fact]
    public void Rejects_unknown_thumbprint()
    {
        using var cert = SelfSigned("CN=apiserver");
        var config = new AuthConfig { Type = "clientCert", Thumbprints = { "AA:BB:CC" } };

        Assert.False(new ClientCertValidator().Validate(WithCert(cert), config).Succeeded);
    }

    [Fact]
    public void Fails_closed_when_no_certificate_presented()
    {
        var config = new AuthConfig { Type = "clientCert", Thumbprints = { "AABBCC" } };

        Assert.False(new ClientCertValidator().Validate(WithCert(null), config).Succeeded);
    }
}
