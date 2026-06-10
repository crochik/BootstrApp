using PI.DocuSeal.Models;
using PI.Shared.Models;
using Providers;
using RazorLight;

namespace PI.DocuSeal.Providers;

public class RazorLightTemplateProvider : ITemplateProvider
{
    private readonly IRazorLightEngine _razorLightEngine;

    public TemplateEngine SupportedEngine => TemplateEngine.RazorLight;

    public RazorLightTemplateProvider(IRazorLightEngine razorLightEngine)
    {
        _razorLightEngine = razorLightEngine;
    }

    public async Task<string?> RenderTemplateAsync(IEntityContext context, DocumentTemplate config, IDictionary<string, object> objectContext)
    {
        try
        {
            var templateKey = $"custom_template_{Guid.NewGuid()}";
            return await _razorLightEngine.CompileRenderStringAsync(templateKey, config.Template, objectContext);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to render RazorLight template: {ex.Message}", ex);
        }
    }
}