using System;
using System.Collections.Generic;
using System.Linq;
using Crochik.Mongo;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using PI.Shared.Form.Models;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Models.Designer;

/// <summary>
/// Object Type draft (in progress changes)
/// </summary>
[BsonCollection("ObjectType.Draft")]
public class ObjectTypeDraft : EntityOwnedModel, ITaggable
{
    public const string ObjectTypeFullName = "ObjectTypeDraft";

    public ObjectType ObjectType { get; set; }
    
    [BsonIgnore]
    public ObjectType BaseObjectType { get; set; }

    [JsonIgnore] public Dictionary<string, object> Example { get; set; } = new();

    [JsonIgnore] public Dictionary<string, AppFormLayout> Layouts { get; set; } = new();
    
    public Guid? ObjectStatusId { get; set; }

    public Guid? FlowId { get; set; }

    public bool IsActive { get; set; } = true;

    public string[] Tags { get; set; }

    public ObjectTypeDraft()
    {
    }
}

public static class ObjectTypeDraftExtensions
{
    public static void UpdateRelatedObjectTypes(this ObjectTypeDraft draft)
    {
        var objectType = draft.ObjectType;

        if (objectType.Fields == null) return;
        
        // add placeholder relations
        foreach (var field in objectType.Fields)
        {
            if (field.Value.Field is ReferenceField referenceField)
            {
                if (referenceField.ReferenceFieldOptions?.ObjectType == null || referenceField.ReferenceFieldOptions.ObjectType.StartsWith("/")) continue;

                var existing = objectType.RelatedObjectTypes?
                    .FirstOrDefault(x => x.RelationType == RelationType.OneToOne &&
                                         x.ObjectType == referenceField.ReferenceFieldOptions.ObjectType &&
                                         x.Criteria?.Conditions?.Length == 1 &&
                                         x.Criteria.Conditions[0].FieldName == (referenceField.ReferenceFieldOptions.ForeignFieldName ?? Model.IdFieldName) &&
                                         x.Criteria.Conditions[0].Operator == Operator.Eq &&
                                         x.Criteria.Conditions[0].Value is string value && value == "{{" + field.Value.Field.Name + "}}"
                    );

                if (existing != null) continue;

                objectType.RelatedObjectTypes ??= [];
                objectType.RelatedObjectTypes = objectType.RelatedObjectTypes
                    .Append(new RelatedObjectType
                    {
                        RelationType = RelationType.OneToOne,
                        ObjectType = referenceField.ReferenceFieldOptions.ObjectType,
                        Name = referenceField.Name,
                        Label = referenceField.Label ?? referenceField.Name,
                        Criteria = new Criteria
                        {
                            Conditions =
                            [
                                Condition.Eq(referenceField.ReferenceFieldOptions.ForeignFieldName ?? Model.IdFieldName, "{{" + referenceField.Name + "}}"),
                            ]
                        },
                        RBAC = new RelatedObjectTypeRBAC(),
                    })
                    .ToArray();
            }
        }
    }
}