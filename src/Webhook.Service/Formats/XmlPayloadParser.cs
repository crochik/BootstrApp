using System.Xml.Linq;
using Webhook.Service.Engine;

namespace Webhook.Service.Formats;

/// <summary>Parses the body as XML into an <see cref="XDocument"/>.</summary>
public sealed class XmlPayloadParser : IPayloadParser
{
    public string Format => "xml";

    public object? Parse(WebhookContext context)
    {
        if (context.RawBody.Length == 0)
        {
            return null;
        }

        return XDocument.Parse(context.BodyText);
    }
}
