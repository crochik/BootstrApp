using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Services;

public class ObjectTypeIntrospector
{
    private readonly ObjectTypeService _objectTypeService;

    private GetObjectOptions GetObjectOptions { get; } = new()
    {
        Cache = new GetObjectCache(),
    };

    public IEntityContext Context { get; set; }

    public ObjectType ObjectType { get; private set; }

    private Dictionary<string, ReadableField> _childObjects;
    public Dictionary<string, ReadableField> ChildObjects => _childObjects;

    private Dictionary<string, ReadableField> _readableFields;
    public Dictionary<string, ReadableField> ReadableFieldsRecursively => _readableFields ??= GetReadableFields();

    private Dictionary<string, ReadableField> _indexedFields;
    public Dictionary<string, ReadableField> IndexedFieldsRecursively => _indexedFields ??= GetIndexedFields();

    public Dictionary<string, ReadableField> RelationalFieldsRecursively { get; private set; }

    public ObjectTypeIntrospector(ObjectTypeService objectTypeService)
    {
        _objectTypeService = objectTypeService;
    }

    public async Task IntrospectAsync(string objectTypeName)
    {
        var objectType = await GetObjectTypeAsync(objectTypeName);
        await IntrospectAsync(objectType);
    }

    public bool TryGetObjectTypeFromCache(string objectTypeName, out ObjectType objectType)
    {
        objectType = GetObjectOptions?.Cache?.GetFromCache(objectTypeName);
        return objectType != null;
    }

    private void Reset(ObjectType objectType)
    {
        ObjectType = objectType;

        _childObjects = null;
        _readableFields = null;
        _indexedFields = null;
        _readableFields = null;
        RelationalFieldsRecursively = null;
    }

    public async Task IntrospectAsync(ObjectType objectType)
    {
        Reset(objectType);

        _childObjects = new Dictionary<string, ReadableField>();
        await RecursivelyAddObjectsAsync(ObjectType);

        RelationalFieldsRecursively = new Dictionary<string, ReadableField>();
        await AddRelationalFieldsAsync(ObjectType, RelationalFieldsRecursively);
    }

    private async Task RecursivelyAddObjectsAsync(ObjectType objectType, ReadableField parent = null)
    {
        var constraints = objectType.GetConditions(Context)?.Where(x => x.Operator == Operator.Exists).ToArray();
        if (constraints?.Length > 0)
        {
            foreach (var constraint in constraints)
            {
                var relation = objectType.RelatedObjectTypes?.FirstOrDefault(x => x.Name == constraint.FieldName);
                if (relation == null || !relation.RBAC.CanRead(Context)) throw new ForbiddenException($"Can't enforce exists constraint: {constraint.FieldName} on {objectType.FullName}");
                if (!TryGetObjectTypeFromCache(relation.ObjectType, out var related))
                {
                    related = await GetObjectTypeAsync(relation.ObjectType);
                }

                if (related == null) throw new ForbiddenException($"Can't enforce exists constraint on {objectType.FullName}: {relation.ObjectType} not found");
            }
        }

        foreach (var kvp in objectType.Fields)
        {
            switch (kvp.Value.Field)
            {
                case ObjectField field:
                    await AddChildObjectAsync(kvp.Value, field.ObjectFieldOptions?.ObjectType, parent);
                    break;

                case ChildrenField field:
                    if (field.ChildrenFieldOptions.KeyType == ChildrenFieldOptions.IndexKeyType)
                    {
                        // only arrays for now
                        await AddChildObjectAsync(kvp.Value, field.ChildrenFieldOptions?.ObjectType, parent);
                    }

                    break;

                case ReferenceField referenceField:
                    // await AddChildObjectAsync(path, referenceField, kvp.Value);
                    break;
            }
        }
    }

    private async Task AddChildObjectAsync(FieldTemplate fieldTemplate, string objectTypeName, ReadableField parent = null)
    {
        if (objectTypeName == null || objectTypeName == "*") return;
        var childObjectField = await GetObjectTypeAsync(objectTypeName);
        if (childObjectField == null || !childObjectField.CanRead(Context)) return;

        var readableField = new ReadableField(parent)
        {
            // FieldPath = parent?.FieldPath == null ? field.Name : $"{parent.FieldPath}|{field.Name}",
            // ApiName = parent?.ApiName == null ? field.ApiName ?? field.Name : $"{parent.ApiName}|{field.ApiName ?? field.Name}",
            ObjectType = childObjectField,
            FieldTemplate = fieldTemplate,
            Visibility = parent?.Visibility switch
            {
                FieldVisibility.RelationalField => FieldVisibility.EmbeddedInRelationField,
                FieldVisibility.EmbeddedInRelationField => FieldVisibility.EmbeddedInRelationField,
                _ => FieldVisibility.ObjectField,
            },
        };

        _childObjects.Add(readableField.AbsolutePath, readableField);

        await RecursivelyAddObjectsAsync(childObjectField, readableField);
    }

    public Task<ObjectType> GetObjectTypeAsync(string objectTypeName) => _objectTypeService.GetAsync(Context, objectTypeName, GetObjectOptions);

    private Dictionary<string, ReadableField> GetIndexedFields()
    {
        return ReadableFieldsRecursively.Where(x => x.Value.FieldTemplate.Indexed)
            .ToDictionary(x => x.Key, x => x.Value);
    }

    private async Task AddRelationalFieldsAsync(ObjectType objectType, Dictionary<string, ReadableField> fields)
    {
        if (objectType.RelatedObjectTypes == null) return;

        var relations = objectType.RelatedObjectTypes
            .Where(x => x.RelationType is RelationType.OneToOne && x.RBAC.CanRead(Context)) // or RelationType.OneToMany
            .ToArray();

        foreach (var relation in relations)
        {
            var childObjectType = await GetObjectTypeAsync(relation.ObjectType);
            if (childObjectType == null || !childObjectType.CanRead(Context)) continue;

            FormField field = relation.RelationType switch
            {
                RelationType.OneToOne => new ObjectField
                {
                    ObjectFieldOptions = new ObjectFieldOptions
                    {
                        ObjectType = relation.ObjectType,
                    }
                },
                RelationType.OneToMany => new ChildrenField
                {
                    ChildrenFieldOptions = new ChildrenFieldOptions
                    {
                        KeyType = ChildrenFieldOptions.IndexKeyType,
                        ObjectType = relation.ObjectType,
                    }
                },
                _ => null,
            };

            if (field == null) continue;

            // create placeholder field for relation
            field.Name = relation.Name;
            field.Label = relation.Label ?? relation.Name;
            field.ApiName = relation.ApiName ?? relation.Name;
            field.Description = relation.RelationType switch
            {
                RelationType.OneToOne => $"Related {childObjectType.Label ?? childObjectType.Name}",
                RelationType.OneToMany => $"Related {childObjectType.LabelPlural ?? childObjectType.Label ?? childObjectType.Name}",
                _ => null,
            };

            var readableField = new ReadableField(null)
            {
                // FieldPath = $"{objectType.Name}|{field.Name}",
                // ApiName = $"{objectType.ApiName ?? objectType.Name}|{field.ApiName ?? objectType.Name}",

                Visibility = FieldVisibility.RelationalField,
                RelatedObjectType = relation,
                ObjectType = childObjectType,
                FieldTemplate = new FieldTemplate
                {
                    RBAC = new FieldRBAC
                    {
                    },
                    Field = field,
                    Indexed = false,
                }
            };

            if (Context.ProfileId.HasValue)
            {
                readableField.FieldTemplate.RBAC[Context.ProfileId.Value] = FieldPermission.Read;
            }

            ChildObjects.Add(readableField.AbsolutePath, readableField);

            await RecursivelyAddObjectsAsync(readableField.ObjectType, readableField);
        }
    }

    private Dictionary<string, ReadableField> GetReadableFields()
    {
        var result = new Dictionary<string, ReadableField>(fields().DistinctBy(x => x.Key));

        // the children fields of them have already been included 
        // because these were already in the ChildObjects
        foreach (var kvp in RelationalFieldsRecursively)
        {
            // will throw if find conflicting keys
            result.Add(kvp.Key, kvp.Value);
        }

        return result;

        IEnumerable<KeyValuePair<string, ReadableField>> fields()
        {
            foreach (var ft in ObjectType.Fields.Values)
            {
                if (!ft.RBAC.CanRead(Context)) continue;

                if (ChildObjects.ContainsKey(ft.Field.Name)) continue;

                if (ft.Field switch
                    {
                        ObjectField => true,
                        ChildrenField => true,
                        _ => false,
                    }) continue;

                yield return new KeyValuePair<string, ReadableField>(ft.Field.Name, new ReadableField(null)
                {
                    // FieldPath = ft.Field.Name,
                    // ApiName = ft.Field.ApiName ?? ft.Field.ApiName,
                    ObjectType = ObjectType,
                    FieldTemplate = ft,
                    Visibility = ft.Field switch
                    {
                        ObjectField => FieldVisibility.ObjectField,
                        _ => FieldVisibility.Normal,
                    },
                });
            }

            if (ChildObjects != null)
            {
                foreach (var kvp in ChildObjects)
                {
                    if (!kvp.Value.FieldTemplate.RBAC.CanRead(Context)) continue;
                    if (!kvp.Value.ObjectType.CanRead(Context)) continue;

                    yield return kvp;

                    var visibility = kvp.Value.Visibility switch
                    {
                        FieldVisibility.ObjectField => FieldVisibility.EmbeddedInObjectField,
                        FieldVisibility.RelationalField => FieldVisibility.EmbeddedInRelationField, // TODO: if the relation is one to many we would need to change the fields (or limit so they can not be projected)
                        FieldVisibility.EmbeddedInRelationField => FieldVisibility.EmbeddedInRelationField, // TODO: if the relation is one to many we would need to change the fields (or limit so they can not be projected)
                        // FieldVisibility.LookupField => FieldVisibility.EmbeddedInLookupField,
                        _ => default(FieldVisibility?)
                    };

                    if (!visibility.HasValue) continue;

                    foreach (var ft in kvp.Value.ObjectType.Fields.Values)
                    {
                        if (!ft.RBAC.CanRead(Context)) continue;

                        // if (ChildObjects.ContainsKey(ft.Field.Name)) continue;

                        if (ft.Field switch
                            {
                                ObjectField => true,
                                ChildrenField => true,
                                _ => false,
                            }) continue;

                        var readableField = new ReadableField(kvp.Value)
                        {
                            // FieldPath = $"{kvp.Value.FieldPath}|{ft.Field.Name}",
                            // ApiName = $"{kvp.Value.ApiName}|{ft.Field.ApiName ?? ft.Field.ApiName}",
                            ObjectType = kvp.Value.ObjectType,
                            FieldTemplate = ft,
                            Visibility = visibility.Value,
                        };

                        yield return new KeyValuePair<string, ReadableField>(readableField.AbsolutePath, readableField);
                    }
                }
            }
        }
    }

    public class ReadableField
    {
        public ReadableField Parent { get; }

        public string Name => FieldTemplate.Field.Name;
        public string ApiName => FieldTemplate.Field.ApiName ?? Name;
        public string Label => FieldTemplate.Field.Label ?? Name;

        public string Description => FieldTemplate.Field.Description;

        // Recursive (Including parent)
        public string ApiAbsoluteName => Parent != null ? $"{Parent.ApiAbsoluteName}_{ApiName}" : ApiName;
        public string AbsolutePath => Parent != null ? $"{Parent.AbsolutePath}|{Name}" : Name;
        public string ApiAbsolutePath => Parent != null ? $"{Parent.ApiAbsolutePath}.{ApiName}" : ApiName;
        public string AbsoluteLabel => Parent != null ? $"{Parent.AbsoluteLabel} > {Label}" : Label;
        public ReadableField TopLevel => Parent == null ? this : Parent.TopLevel;

        public string PathInCollection
        {
            get
            {
                if (Visibility == FieldVisibility.RelationalField) return null;
                var parentPath = Parent?.PathInCollection;
                var fieldPath = FormField.GetPathInCollection(FieldTemplate.Field.Name);
                return parentPath == null ? fieldPath : $"{parentPath}.{fieldPath}";
            }
        }

        // public string ApiName { get; init; }
        // public string FieldPath { get; set; }
        public FieldTemplate FieldTemplate { get; init; }

        public ObjectType ObjectType { get; init; }
        public RelatedObjectType RelatedObjectType { get; init; }
        public FieldVisibility Visibility { get; set; }

        public ReadableField(ReadableField parent)
        {
            Parent = parent;
        }

        public bool TryGetRecursively(FieldVisibility visibility, out ReadableField readableField)
        {
            if (Visibility == visibility)
            {
                readableField = this;
                return true;
            }

            if (Parent == null)
            {
                readableField = null;
                return false;
            }

            return Parent.TryGetRecursively(visibility, out readableField);
        }

        public IEnumerable<ReadableField> GetParents()
        {
            if (Parent == null) yield break;

            yield return Parent;

            foreach (var parent in Parent.GetParents()) yield return parent;
        }
    }

    public enum FieldVisibility
    {
        Normal,
        ObjectField,
        EmbeddedInObjectField,
        RelationalField,
        EmbeddedInRelationField,
    }
}