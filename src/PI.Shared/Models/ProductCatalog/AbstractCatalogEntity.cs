using System;
using System.Collections.Generic;
using System.Linq;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Serializers;
using PI.Shared.Models;

namespace PI.ProductCatalog.Models;

[UseObjectId]
public class AbstractCatalogEntity : EntityOwnedModel, ITaggable
{
    public const string FAVORITE_TAG = "Favorite";
    public const string PRODUCT_SELECTOR = "Product Selector";

    [BsonSerializer(typeof(ObjectIdAsGuidSerializer))]
    public Guid CatalogFeedId { get; set; }

    /// <summary>
    /// List of parents (may be something to be moved to EntityOwnedModel)
    /// </summary>
    public Dictionary<string, Guid> Parents { get; set; }

    /// <summary>
    /// Calculated list of parent ids
    /// </summary>
    [BsonElement]
    [BsonSerializer(typeof(ArraySerializer<Guid>))]
    [BsonArraySerializationOptions(typeof(ObjectIdAsGuidSerializer))]
    public Guid[] ParentIds => Parents?.Values.ToArray();

    public decimal? Margin { get; set; }

    /// <summary>
    /// ???? is this being ussd at all (don't think so)
    /// </summary>
    public bool? IsHidden { get; set; }

    /// <summary>
    /// list of tag (names)
    /// </summary>
    public string[] Tags { get; set; }

    /// <summary>
    /// ?????
    /// </summary>
    [BsonIgnore]
    public bool IsFavorite
    {
        get => Tags!=null && Tags.Contains(FAVORITE_TAG);
        set => Tags = Set(Tags, FAVORITE_TAG, value);
    }
        
    private static string[] Remove(string[] tags, string tag)
    {
        if (tags != null)
        {
            tags = tags.Where(x => !string.Equals(x, tag)).ToArray();
            if (tags.Length == 0) tags = null;
        }

        return tags;
    }
    
    private static string[] Set(string[] tags, string tag, bool set = true)
    {
        if (!set)
        {
            return Remove(tags, tag);
        }

        return (tags ?? Enumerable.Empty<string>()).Append(tag).Distinct().ToArray();
    }
    
}