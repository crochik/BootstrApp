using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Services;
using DataView = PI.Shared.Form.Models.DataView;

namespace Controllers;

public partial class ObjectTypeController
{
    /// <summary>
    /// Introspect object 
    /// </summary>
    [Authorize("admin")]
    [HttpGet("/api/v1/[controller]({objectTypeName})/Introspect")]
    public async Task<IActionResult> InstrospectAsync(string objectTypeName, [FromServices] ObjectTypeIntrospector introspector)
    {
        introspector.Context = Context;
        await introspector.IntrospectAsync(objectTypeName);
        var result = introspector.IndexedFieldsRecursively;
        return Ok(result);
    }

    /// <summary>
    /// get object types data view with calculated column for validation errors
    /// </summary>
    [Authorize("admin")]
    [HttpPost("DataView")]
    [ProducesResponseType(typeof(DataViewResponse), 200)]
    [Produces("text/csv", "application/json")]
    public async Task<DataViewResponse> GetValidationErrorsDataViewAsync([FromBody] DataViewRequest request)
    {
        Prepare(request);

        var objectType = await _objectTypeService.GetAsync(Context, nameof(ObjectType));
        if (objectType == null) throw new NotFoundException($"{nameof(ObjectType)} not found");

        var result = await _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, Context.AccountId)
            .SortAsc(x => x.Name)
            .FindAsync();

        var objDict = new Dictionary<string, ObjectType>();
        foreach (var ot in result)
        {
            if (!objDict.TryAdd(ot.FullName, ot))
            {
                _logger.LogError("Duplicated {ObjectType}", ot.FullName);
            }
        }

        foreach (var ot in result)
        {
            merge(ot);
        }

        var records = result
            .Select(x => new
            {
                id = x.Id,
                name = x.FullName,
                description = x.Description,
                collectionName = x.CollectionName,
                errors = _objectTypeService.ValidateObjectType(x),
                baseObjectType = x.BaseObjectType,
                isAbstract = x.IsAbstract,
                isEmbedded = x.IsEmbedded,
                baseObjectTypeId = x.LoadedBaseObjectType?.Id,
                tags = x.Tags,
            })
            .ToArray();

        var fields = new FormField[]
        {
            new TextField
            {
                Name = "id",
                Label = "ID",
            },
            new TextField
            {
                Name = "name",
                Label = "Name",
                TextFieldOptions = new TextFieldOptions
                {
                    LinkUrl = "dataForm://api/v1/CustomObject/ObjectType({{id}})/View"
                }
            },
            new TextField
            {
                Name = "description",
                Label = "Description"
            },
            new TextField
            {
                Name = "collectionName",
                Label = "Collection Name"
            },
            new CheckboxField
            {
                Name = "isAbstract",
                Label = "Abstract"
            },
            new CheckboxField
            {
                Name = "isEmbedded",
                Label = "Embedded"
            },
            new TextField
            {
                Name = "baseObjectType",
                Label = "Base Type",
                TextFieldOptions = new TextFieldOptions
                {
                    LinkUrl = "dataForm://api/v1/CustomObject/ObjectType({{baseObjectTypeId}})/View"
                }
            },
            new TextField
            {
                Name = "errors",
                Label = "Errors"
            },
            new TagsField
            {
                Name = "tags",
                Label = "Tags"
            }
        };

        if (request.Fields?.IsEmpty() ?? true)
        {
            request.Fields = fields.Select(x => x.Name).ToArray();
        }
        else
        {
            foreach (var f in fields)
            {
                if (!request.Fields.Contains(f.Name))
                {
                    f.Visible = new[] { "false" };
                }
            }
        }

        return new DataViewResponse
        {
            Request = request,
            View = new DataView
            {
                Name = "ObjectType_Validate",
                Title = "Object Types (Validate)",
                DefaultSort = "name",
                KeyField = "id",
                Fields = fields,
                IsFilterableLocally = true,
                Filter = new[]
                {
                    "collectionName",
                    "isAbstract",
                    "isEmbedded",
                    "baseObjectType",
                    "tags"
                },
            },
            Result = records,
        };
        // var response = await _objectTypeService.GetDataViewAsync(Context, objectType, request);
        // return response;

        void merge(ObjectType ot)
        {
            if (ot.BaseObjectType == null || ot.LoadedBaseObjectType != null) return;

            if (!objDict.TryGetValue(ot.BaseObjectType, out var baseOt))
            {
                // error 
                _logger.LogError("Missing {ObjectType}", ot.BaseObjectType);
                return;
            }

            merge(baseOt);
            ObjectTypeService.Merge(baseOt, ot);
        }
    }

    /// <summary>
    /// Delete (implicit) related objects 
    /// </summary>
    [Authorize("admin")]
    [HttpDelete("RelatedObjectTypes")]
    public async Task<IActionResult> DeleteCompleteRelatedObjectTypesAsync()
    {
        // var result = await _connection.Filter<ObjectType>()
        //     .Eq(x => x.AccountId, Context.AccountId)
        //     .Update
        //     .PullFilterBuilder(
        //         x => x.RelatedObjectTypes,
        //         q => q.In(x => x.RelationType, [
        //             RelationType.Embedded, RelationType.Extended, RelationType.Referenced
        //         ]))
        //     .UpdateManyAsync();

        var result = await _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Name, "nobody here")
            .Update
            .PullFilterBuilder(
                x => x.RelatedObjectTypes,
                q => q.Eq(x => x.RBAC.Permissions, new Dictionary<string, RelatedObjectTypePermission>()))
            .UpdateManyAsync();

        return Ok(result);
    }

    /// <summary>
    /// Get Objects that relate to this
    /// </summary>
    [Authorize("admin")]
    [HttpPut("RelatedObjectTypes")]
    public async Task<IActionResult> AutoCompleteRelatedObjectTypesAsync()
    {
        await _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Update
            .PullFilterBuilder(
                x => x.RelatedObjectTypes,
                q => q.In(x => x.RelationType, [
                    RelationType.Embedded, RelationType.Extended, RelationType.Referenced
                ]))
            .UpdateManyAsync();

        var objectTypes = await _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, Context.AccountId)
            // .Eq(x => x.BaseObjectType, ObjectType.GetFullName(objectType, @namespace))
            .SortAsc(x => x.Name)
            .FindAsync();

        foreach (var ot in objectTypes)
        {
            // inheritance 
            if (!string.IsNullOrEmpty(ot.BaseObjectType))
            {
                await updateObjectType(ot.BaseObjectType, new RelatedObjectType
                {
                    Name = ot.Name,
                    Label = ot.Description,
                    RelationType = RelationType.Extended,
                    ObjectType = ot.FullName,
                    RBAC = new RelatedObjectTypeRBAC(),
                });
            }

            if (ot.Fields == null) continue;

            // relations
            foreach (var kvp in ot.Fields)
            {
                var task = kvp.Value.Field switch
                {
                    ReferenceField field => string.IsNullOrWhiteSpace(field.ReferenceFieldOptions?.ObjectType)
                        ? null
                        : updateObjectType(field.ReferenceFieldOptions.ObjectType, new RelatedObjectType
                        {
                            Name = field.Name,
                            Label = field.Description,
                            RelationType = RelationType.Referenced,
                            ObjectType = ot.FullName,
                            RBAC = new RelatedObjectTypeRBAC(),
                            Criteria = new Criteria
                            {
                                Conditions = new[]
                                {
                                    PI.Shared.Models.Expressions.Condition.Eq(field.Name, "{{" + (field.ReferenceFieldOptions.ForeignFieldName ?? Model.IdFieldName) + "}}")
                                }
                            }
                        }),
                    MultiReferenceField field => string.IsNullOrWhiteSpace(field.MultiReferenceFieldOptions?.ObjectType)
                        ? null
                        : updateObjectType(field.MultiReferenceFieldOptions.ObjectType, new RelatedObjectType
                        {
                            Name = field.Name,
                            Label = field.Description,
                            RelationType = RelationType.Referenced,
                            ObjectType = ot.FullName,
                            RBAC = new RelatedObjectTypeRBAC(),
                            Criteria = new Criteria
                            {
                                Conditions = new[]
                                {
                                    PI.Shared.Models.Expressions.Condition.Eq(field.Name, "{{" + (field.MultiReferenceFieldOptions.ForeignFieldName ?? Model.IdFieldName) + "}}")
                                }
                            }
                        }),
                    ObjectField field => string.IsNullOrWhiteSpace(field.ObjectFieldOptions?.ObjectType)
                        ? null
                        : updateObjectType(field.ObjectFieldOptions.ObjectType, new RelatedObjectType
                        {
                            Name = field.Name,
                            Label = field.Description,
                            RelationType = RelationType.Embedded,
                            ObjectType = ot.FullName,
                            RBAC = new RelatedObjectTypeRBAC(),
                        }),
                    ChildrenField field => string.IsNullOrWhiteSpace(field.ChildrenFieldOptions?.ObjectType)
                        ? null
                        : updateObjectType(field.ChildrenFieldOptions.ObjectType, new RelatedObjectType
                        {
                            Name = field.Name,
                            Label = field.Description,
                            RelationType = RelationType.Embedded,
                            ObjectType = ot.FullName,
                            RBAC = new RelatedObjectTypeRBAC(),
                        }),

                    // TODO: can it handle calculated?
                    // Calculated ... 

                    _ => null,
                };

                if (task != null) await task;
            }
        }

        return Ok();

        async Task updateObjectType(string objectTypeName, RelatedObjectType relation)
        {
            var updated = await _objectTypeService.Query(Context, objectTypeName, false)
                .NotBuilder(q => q.ElemMatchBuilder(
                        f => f.RelatedObjectTypes, q =>
                            q.Eq(x => x.ObjectType, relation.ObjectType)
                                .Eq(x => x.RelationType, relation.RelationType)
                    )
                )
                .Update
                .Push(x => x.RelatedObjectTypes, relation)
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .Set(x => x.LastActor, Context.Actor)
                .UpdateAndGetOneAsync();

            if (updated != null)
            {
                // already set
                _logger.LogInformation("Add \"{RelationType}\" Relation to {ObjectType}", relation.RelationType, updated.FullName);
            }
        }
    }

    [Authorize("admin")]
    [HttpPut("Discriminators")]
    public async Task<IActionResult> FixDiscriminatorsAsync([FromQuery] FixDiscriminatorOptions options)
    {
        var objectTypes = await _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Ne(x => x.IsActive, false)
            .Ne(x => x.Discriminator, null)
            .FindAsync();

        var result = new List<object>();
        foreach (var ot in objectTypes)
        {
            await NewMethod(options, ot, result);
        }

        return Ok(result);
    }

    private async Task NewMethod(FixDiscriminatorOptions options, ObjectType objectType, List<object> result)
    {
        if (objectType.Discriminator.Count < 1) return;

        var discriminatorFieldName = default(string);
        var values = new HashSet<string>();
        var childTypes = new Dictionary<string, string>();
        foreach (var d in objectType.Discriminator)
        {
            if (d.Value.Conditions?.Length != 1)
            {
                if (options == FixDiscriminatorOptions.Check)
                {
                    result.Add(new
                    {
                        ObjectType = objectType.FullName,
                        Message = $"{d.Key}: Not single condition",
                    });
                }

                continue;
            }

            var condition = d.Value.Conditions.First();
            discriminatorFieldName ??= condition.FieldName;
            if (discriminatorFieldName != condition.FieldName)
            {
                if (options == FixDiscriminatorOptions.Check)
                {
                    result.Add(new
                    {
                        ObjectType = objectType.FullName,
                        Message = $"{d.Key}: Unexpected field name: {discriminatorFieldName} vs {condition.FieldName}",
                    });
                }

                continue;
            }

            childTypes.Add(d.Key, condition.Value.ToString());
            values.Add(condition.Value.ToString());
        }

        if (values.Count == 0) return;

        // TODO: check discriminator field
        // ...
        if (objectType.BaseObjectType != null)
        {
            objectType = await _objectTypeService.GetAsync(Context, objectType.FullName);
        }

        if (!objectType.Fields.TryGetValue(discriminatorFieldName, out var discriminatorFt))
        {
            if (options == FixDiscriminatorOptions.Check)
            {
                result.Add(new
                {
                    ObjectType = objectType.FullName,
                    Message = $"Discriminator field {discriminatorFieldName} not found",
                });
            }

            return;
        }

        if (discriminatorFt.Field is not SelectField discriminatorField || discriminatorField.Options is not SelectFieldOptions selectFieldOptions || selectFieldOptions.Items == null)
        {
            if (options == FixDiscriminatorOptions.Check)
            {
                result.Add(new
                {
                    ObjectType = objectType.FullName,
                    Message = $"Discriminator field {discriminatorFieldName} is not a select field, can't check values",
                });
            }

            return;
        }

        var missing = new HashSet<string>();
        foreach (var item in selectFieldOptions.Items.Keys)
        {
            var key = item.ToString();
            if (!values.Remove(key))
            {
                missing.Add(key);
            }
        }

        if (missing.Count > 0)
        {
            if (options == FixDiscriminatorOptions.Check)
            {
                result.Add(new
                {
                    ObjectType = objectType.FullName,
                    Message = $"Discriminators missing: {string.Join(", ", missing)}",
                });
            }
            else if (options == FixDiscriminatorOptions.AddDiscriminators)
            {
                // TODO: ...
                // ...

                result.Add(new
                {
                    ObjectType = objectType.FullName,
                    Message = $"Discriminators missing: {string.Join(", ", missing)}, added discriminators",
                });
            }
        }

        if (values.Count > 0)
        {
            if (options == FixDiscriminatorOptions.Check)
            {
                result.Add(new
                {
                    ObjectType = objectType.FullName,
                    Message = $"Discriminator field {discriminatorFieldName} is missing values: {string.Join(", ", values)}",
                });
            }
            else if (options == FixDiscriminatorOptions.AddOptions)
            {
                // TODO: ...
                // ...

                result.Add(new
                {
                    ObjectType = objectType.FullName,
                    Message = $"Discriminator field {discriminatorFieldName} is missing values: {string.Join(", ", values)}, added options",
                });
            }
        }

        // check objects exist
        var children = await _connection.Filter<ObjectType>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Ne(x => x.IsActive, false)
            .In(x => x.FullName, childTypes.Keys)
            // .Eq(x => x.BaseObjectType, objectType.FullName)
            .FindAsync();

        var notFound = new Dictionary<string, string>(childTypes);
        foreach (var child in children)
        {
            notFound.Remove(child.FullName);

            // TODO: check whether the constraints are right in the child?
            // ...
        }

        if (notFound.Count == 0) return;

        if (options == FixDiscriminatorOptions.Check)
        {
            result.Add(new
            {
                ObjectType = objectType.FullName,
                Message = $"Missing Descendants: {string.Join(", ", notFound.Keys)}",
            });
        }

        if (options != FixDiscriminatorOptions.AddObjectTypes)
        {
            return;
        }

        foreach (var kvp in notFound)
        {
            var child = CreateChildObjectType(objectType, discriminatorFieldName, kvp.Key, kvp.Value);
            if (child != null)
            {
                try
                {
                    child = await _connection.InsertAsync(child);
                }
                catch (Exception ex)
                {
                    result.Add(new
                    {
                        ObjectType = objectType.FullName,
                        Message = $"Error inserting Object Type {kvp.Key}: {ex.Message}",
                    });

                    child = null;
                }
            }

            if (child == null)
            {
                result.Add(new
                {
                    ObjectType = objectType.FullName,
                    Message = $"Failed to create missing Object Type: {kvp.Key}",
                });

                continue;
            }

            result.Add(new
            {
                ObjectType = objectType.FullName,
                Message = $"Added Missing Object Type: {child.FullName}",
            });
        }
    }

    private ObjectType CreateChildObjectType(ObjectType baseObjectType, string discriminatorFieldName, string objectTypeName, string discriminatorValue)
    {
        if (!baseObjectType.Fields.TryGetValue(discriminatorFieldName, out var discriminatorFt))
        {
            return null;
        }

        var discriminatorField = (SelectField)discriminatorFt.Field;
        var parts = objectTypeName.Split('.');

        var newOt = new ObjectType
        {
            Id = Model.NewGuid(),
            AccountId = Context.AccountId.Value,
            EntityId = Context.AccountId.Value,
            InitialFlowId = baseObjectType.InitialFlowId,
            InitialObjectStatusId = baseObjectType.InitialObjectStatusId,
            CreatedOn = DateTime.UtcNow,
            LastActor = Context.Actor,
            Tags = ["AUTOFIX"],
            BaseObjectType = baseObjectType.FullName,
            Name = parts[^1],
            Namespace = parts.Length > 1 ? string.Join(".", parts[..^1]) : null,
            RBAC = baseObjectType.RBAC,
            DatabaseName = baseObjectType.DatabaseName,
            CollectionName = baseObjectType.CollectionName,
            // ApiName =
            // Label =
            // LabelPlural =
            // Description =
            IsFullTextSearchable = baseObjectType.IsFullTextSearchable,
            IsActive = true,
            LookupFields = baseObjectType.LookupFields,
            // UniqueExternalId = // obsolete
            // NativeType = // obsolete
            Fields = new Dictionary<string, FieldTemplate>
            {
                {
                    discriminatorFieldName, new FieldTemplate
                    {
                        Field = new TextField
                        {
                            Name = discriminatorField.Name,
                            Label = discriminatorField.Label,
                            DefaultValue = discriminatorValue,
                            Enable = ["false"],
                        },
                        InitialValue = discriminatorValue,
                        RBAC = new FieldRBAC
                        {
                            Permissions = new Dictionary<string, FieldPermission>(discriminatorFt.RBAC.Permissions.Select(x => new KeyValuePair<string, FieldPermission>(x.Key, FieldPermission.Read))),
                        }
                    }
                }
            },
            IsEmbedded = baseObjectType.IsEmbedded,
            // IsAbstract =
            Constraints = new Dictionary<string, Criteria>
            {
                {
                    nameof(EntityRoleId.Account), new Criteria
                    {
                        Conditions =
                        [
                            Condition.Eq(discriminatorFieldName, discriminatorValue)
                        ]
                    }
                }
            },
        };

        return newOt;
    }

    public enum FixDiscriminatorOptions
    {
        Check,
        AddObjectTypes,
        AddDiscriminators,
        AddOptions,
    }
}