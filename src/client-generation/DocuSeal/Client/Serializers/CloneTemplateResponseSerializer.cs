using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CloneTemplateResponse - handles serialization and deserialization
/// </summary>
public static class CloneTemplateResponseSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CloneTemplateResponse instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CloneTemplateResponse? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CloneTemplateResponse instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CloneTemplateResponse? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CloneTemplateResponse DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        int id = default!;
        string slug = default!;
        string name = default!;
        object preferences = default!;
        List<CloneTemplateResponseSchemaItem>? schema = null;
        List<CloneTemplateResponseFieldsItem>? fields = null;
        List<CloneTemplateResponseSubmittersItem>? submitters = null;
        int authorId = default!;
        string archivedAt = default!;
        string createdAt = default!;
        string updatedAt = default!;
        string source = default!;
        string externalId = default!;
        int folderId = default!;
        string folderName = default!;
        bool? sharedLink = default;
        CloneTemplateResponseAuthor author = default!;
        List<CloneTemplateResponseDocumentsItem>? documents = null;

        // Single iteration over JSON properties
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "id":
                    id = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "slug":
                    slug = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "name":
                    name = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "preferences":
                    preferences = BaseSerializer.ParsePrimitiveobject(property.Value);
                    break;
                case "schema":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        schema = new List<CloneTemplateResponseSchemaItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = CloneTemplateResponseSchemaItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                schema.Add(_itemValue);
                        }
                    }
                    break;
                case "fields":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        fields = new List<CloneTemplateResponseFieldsItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = CloneTemplateResponseFieldsItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                fields.Add(_itemValue);
                        }
                    }
                    break;
                case "submitters":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        submitters = new List<CloneTemplateResponseSubmittersItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = CloneTemplateResponseSubmittersItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                submitters.Add(_itemValue);
                        }
                    }
                    break;
                case "author_id":
                    authorId = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "archived_at":
                    archivedAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "created_at":
                    createdAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "updated_at":
                    updatedAt = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "source":
                    source = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "external_id":
                    externalId = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "folder_id":
                    folderId = BaseSerializer.ParsePrimitiveint(property.Value);
                    break;
                case "folder_name":
                    folderName = BaseSerializer.ParsePrimitivestring(property.Value);
                    break;
                case "shared_link":
                    sharedLink = BaseSerializer.ParsePrimitivebool(property.Value);
                    break;
                case "author":
                    author = CloneTemplateResponseAuthorSerializer.DeserializeFromElementCore(property.Value);
                    break;
                case "documents":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        documents = new List<CloneTemplateResponseDocumentsItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = CloneTemplateResponseDocumentsItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                documents.Add(_itemValue);
                        }
                    }
                    break;
            }
        }

        // Create instance with all properties set at once
        return new CloneTemplateResponse
        {
            Id = id!,
            Slug = slug!,
            Name = name!,
            Preferences = preferences!,
            Schema = schema!,
            Fields = fields!,
            Submitters = submitters!,
            AuthorId = authorId!,
            ArchivedAt = archivedAt!,
            CreatedAt = createdAt!,
            UpdatedAt = updatedAt!,
            Source = source!,
            ExternalId = externalId!,
            FolderId = folderId!,
            FolderName = folderName!,
            SharedLink = sharedLink,
            Author = author!,
            Documents = documents!
        };
    }


    /// <summary>
    /// Deserializes a JsonDocument into a list of CloneTemplateResponse instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CloneTemplateResponse> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CloneTemplateResponse>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CloneTemplateResponse>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CloneTemplateResponse instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CloneTemplateResponse instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CloneTemplateResponse instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CloneTemplateResponse> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
