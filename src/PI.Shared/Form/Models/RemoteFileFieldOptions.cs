using System.Collections.Generic;
using Newtonsoft.Json;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Models.Files;

namespace PI.Shared.Form.Models;

public class RemoteFileFieldOptions : ReferenceFieldOptions
{    
    /// <summary>
    /// Upload file options (not sent to the client?)
    /// </summary>
    [JsonIgnore]
    public UploadFileOptions UploadFileOptions { get; set; }

    /// <summary>
    /// Acceptable Content Type(s)
    /// </summary>
    public string[] ContentTypes { get; set; }
    
    /// <summary>
    /// Relative path to post file (will automatically add "/RemoteFile/Upload" Suffix)
    /// </summary>
    public string Url { get; set; }

    public override void FillPlaceHolders(IEntityContext context, IDictionary<string, object> objectContext)
    {
        base.FillPlaceHolders(context, objectContext);

        if (ExpressionEvaluatorService.TryResolve(context, objectContext, Url, out var result) && result is string url)
        {
            Url = url;
        }
    }
}