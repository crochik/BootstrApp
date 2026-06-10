using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocuSeal.Api.Models;

/// <summary>
/// Serializer for CreateTemplateFromDocxResponse - handles serialization and deserialization
/// </summary>
public static class CreateTemplateFromDocxResponseSerializer
{

    /// <summary>
    /// Deserializes a JsonDocument into a CreateTemplateFromDocxResponse instance
    /// </summary>
    /// <param name="document">The JsonDocument to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromDocxResponse? DeserializeFromDocument(JsonDocument document)
    {
        if (document == null)
            return null;

        return DeserializeFromElementCore(document.RootElement);
    }

    /// <summary>
    /// Deserializes a JsonElement into a CreateTemplateFromDocxResponse instance
    /// </summary>
    /// <param name="element">The JsonElement to deserialize</param>
    /// <returns>The deserialized instance</returns>
    public static CreateTemplateFromDocxResponse? DeserializeFromElement(JsonElement element)
    {
        if (BaseSerializer.IsNullOrUndefined(element))
            return null;

        return DeserializeFromElementCore(element);
    }

    /// <summary>
    /// Core deserialization logic - parses JSON and creates instance with object initializer
    /// </summary>
    public static CreateTemplateFromDocxResponse DeserializeFromElementCore(JsonElement element)
    {
        // Parse all properties into local variables first
        int id = default!;
        string slug = default!;
        string name = default!;
        object preferences = default!;
        List<CreateTemplateFromDocxResponseSchemaItem>? schema = null;
        List<CreateTemplateFromDocxResponseFieldsItem>? fields = null;
        List<CreateTemplateFromDocxResponseSubmittersItem>? submitters = null;
        int authorId = default!;
        string archivedAt = default!;
        string createdAt = default!;
        string updatedAt = default!;
        string source = default!;
        string externalId = default!;
        int folderId = default!;
        string folderName = default!;
        bool? sharedLink = default;
        CreateTemplateFromDocxResponseAuthor author = default!;
        List<CreateTemplateFromDocxResponseDocumentsItem>? documents = null;

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
                        schema = new List<CreateTemplateFromDocxResponseSchemaItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = CreateTemplateFromDocxResponseSchemaItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                schema.Add(_itemValue);
                        }
                    }
                    break;
                case "fields":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        fields = new List<CreateTemplateFromDocxResponseFieldsItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = CreateTemplateFromDocxResponseFieldsItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                fields.Add(_itemValue);
                        }
                    }
                    break;
                case "submitters":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        submitters = new List<CreateTemplateFromDocxResponseSubmittersItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = CreateTemplateFromDocxResponseSubmittersItemSerializer.DeserializeFromElementCore(_item);
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
                    author = CreateTemplateFromDocxResponseAuthorSerializer.DeserializeFromElementCore(property.Value);
                    break;
                case "documents":
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        documents = new List<CreateTemplateFromDocxResponseDocumentsItem>();
                        foreach (var _item in property.Value.EnumerateArray())
                        {
                            var _itemValue = CreateTemplateFromDocxResponseDocumentsItemSerializer.DeserializeFromElementCore(_item);
                            if (_itemValue != null)
                                documents.Add(_itemValue);
                        }
                    }
                    break;
            }
        }

        // Create instance with all properties set at once
        return new CreateTemplateFromDocxResponse
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
    /// Deserializes a JsonDocument into a list of CreateTemplateFromDocxResponse instances
    /// </summary>
    /// <param name="document">The JsonDocument containing an array</param>
    /// <returns>A list of deserialized instances</returns>
    public static List<CreateTemplateFromDocxResponse> DeserializeListFromDocument(JsonDocument document)
    {
        if (document == null)
            return new List<CreateTemplateFromDocxResponse>();

        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected JSON array");

        var result = new List<CreateTemplateFromDocxResponse>();

        foreach (var element in root.EnumerateArray())
        {
            var item = DeserializeFromElementCore(element);
            if (item != null)
                result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Serializes a CreateTemplateFromDocxResponse instance to a JSON string
    /// </summary>
    public static string SerializeToJson(CreateTemplateFromDocxResponse instance)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(instance, instance.GetType(), options);
    }

    /// <summary>
    /// Serializes a list of CreateTemplateFromDocxResponse instances to a JSON string
    /// </summary>
    public static string SerializeListToJson(List<CreateTemplateFromDocxResponse> items)
    {
        var options = BaseSerializer.GetDefaultSerializerOptions();
        return JsonSerializer.Serialize(items, options);
    }
}
