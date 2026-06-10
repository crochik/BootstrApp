using System;
using System.Collections.Generic;

namespace Messages.Flow;

public class LoadRelatedObjectActionOptions : ActionOptions
{
    /// <summary>
    /// Field name or RelatedObject key
    /// </summary>
    [Obsolete("use RelatedObjects instead")]
    public string RelatedObject { get; set; }

    /// <summary>
    /// what object from the run to expand
    /// </summary>
    [Obsolete("use RelatedObjects instead")]
    public string ParentObject { get; set; }
    
    /// <summary>
    /// list of related objects to load
    /// one per line
    /// in the format {{ParentObject}}.{{ReferenceOrRelatedObject}}
    /// </summary>
    public string RelatedObjects { get; set; }
    
    public Guid? NextEventId { get; set; }
    public Guid? NotFoundEventId { get; set; }

    public override ActionOutput[] Output { get; set; }

    public IEnumerable<string> GetTargetLoadedObjects(string parentObjectType)
    {
        var objectsToBeLoaded = RelatedObjects;
        objectsToBeLoaded ??= "{{" + (ParentObject ??parentObjectType) + "}}.{{" + RelatedObject + "}}";

        var list = objectsToBeLoaded.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var objectToBeLoaded in list)
        {
            var parts = objectToBeLoaded.Split("}}.{{");
            var parentObjectName = parts.Length == 1 ? parentObjectType : parts[0][2..];
            var relatedObject = parts.Length == 1 ? parts[0][2..^2] : parts[^1][..^2];
            var targetObjectName = $"{parentObjectName}|{relatedObject}";
            yield return targetObjectName;
        }
    }
}