using PI.DocuSeal.Models;
using PI.Shared.Models;

namespace Providers;

public interface ITemplateProvider
{
    TemplateEngine SupportedEngine { get; }
    Task<string?> RenderTemplateAsync(IEntityContext context, DocumentTemplate config, IDictionary<string, object> objectContext);
}