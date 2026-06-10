using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NetCoreForce.Client.Models;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;

namespace PI.Shared.Salesforce;

public class ObjectTypeBuilder
{
    public static ObjectType BuildWrapper(ObjectType salesforceObjectType)
    {
        var fullObjectTypeName = $"salesforce.{salesforceObjectType.Name}";

        var fields = fieldTemplates().ToArray();
        foreach (var field in fields)
        {
            field.RBAC = new FieldRBAC
            {
                [EntityRoleId.Account] = FieldPermission.Read | FieldPermission.SetOnCreate | FieldPermission.Update,
                [EntityRoleId.Admin] = FieldPermission.Read | FieldPermission.Update,
            };
        }

        var objectType = new ObjectType
        {
            Id = Guid.NewGuid(),
            AccountId = salesforceObjectType.AccountId,
            EntityId = salesforceObjectType.EntityId,
            CreatedOn = DateTime.UtcNow,
            Namespace = "salesforce",
            Name = salesforceObjectType.Name,
            BaseObjectType = nameof(EntityOwnedModel),
            Description = $"{salesforceObjectType.Label}: Synced Salesforce Object",
            Label = salesforceObjectType.Label,
            LabelPlural = salesforceObjectType.LabelPlural,
            LastActor = salesforceObjectType.LastActor,
            LastModifiedOn = DateTime.UtcNow,
            IsEmbedded = false,
            Fields = fields.ToDictionary(x => x.Field.Name),
            RBAC = new ObjectTypeRBAC()
            {
                [EntityRoleId.Account] = ObjectTypePermission.Read | ObjectTypePermission.Update | ObjectTypePermission.Create | ObjectTypePermission.Delete,
                [EntityRoleId.Admin] = ObjectTypePermission.Read | ObjectTypePermission.Update,
            },
            CollectionName = $"salesforce.{salesforceObjectType.Name}",
            // DatabaseName = 
        };

        // "childRelationships": [
        // {
        //     "cascadeDelete": false,
        //     "childSObject": "CampaignMember",
        //     "deprecatedAndHidden": false,
        //     "field": "LeadOrContactId",
        //     "junctionIdListNames": [],
        //     "junctionReferenceTo": [],
        //     "restrictedDelete": false
        // },
        // {
        //     "cascadeDelete": false,
        //     "childSObject": "CampaignMemberChangeEvent",
        //     "deprecatedAndHidden": false,
        //     "field": "LeadId",
        //     "junctionIdListNames": [],
        //     "junctionReferenceTo": [],
        //     "restrictedDelete": false
        // },

        return objectType;

        IEnumerable<FieldTemplate> fieldTemplates()
        {
            yield return new FieldTemplate
            {
                Field = new TextField
                {
                    Name = nameof(IExternalId.ExternalId),
                    Label = "Salesforce Id",
                },
            };

            yield return new FieldTemplate
            {
                Field = new CheckboxField
                {
                    Name = nameof(IFlowObject.IsActive),
                    Label = "Active?",
                },
            };

            // ideally the name of this field would not be hardcoded 
            // but right now it is (for backwards compatibility)
            yield return new FieldTemplate
            {
                Field = new ObjectField
                {
                    Name = nameof(ICustomProperties.Properties),
                    Label = $"{salesforceObjectType.Label ?? salesforceObjectType.Description ?? salesforceObjectType.Name}",
                    ObjectFieldOptions = new ObjectFieldOptions
                    {
                        ObjectType = salesforceObjectType.FullName,
                    },
                },
            };

            yield return new FieldTemplate
            {
                Field = new ReferenceField
                {
                    Name = nameof(IFlowObject.FlowId),
                    Label = "Flow",
                    ReferenceFieldOptions = new ReferenceFieldOptions
                    {
                        ObjectType = nameof(Flow),
                        Criteria =
                        [
                            Condition.Eq(nameof(Flow.ObjectType), fullObjectTypeName)
                        ]
                    },
                },
            };

            yield return new FieldTemplate
            {
                Field = new ReferenceField
                {
                    Name = nameof(IFlowObject.ObjectStatusId),
                    Label = "Object Status",
                    ReferenceFieldOptions = new ReferenceFieldOptions
                    {
                        ObjectType = nameof(ObjectStatus),
                        Criteria =
                        [
                            Condition.Eq(nameof(Flow.ObjectType), fullObjectTypeName)
                        ]
                    },
                },
            };

            // public string ObjectType { get; set; }
            //
            // public Guid ObjectTypeId { get; set; }
            //
        }
    }

    public static ObjectType Build(IEntityContext Context, SObjectDescribeFull source)
    {
        // "hasSubtypes": false,
        // "isSubtype": false,
        // "deprecatedAndHidden": false,

        var objectType = new ObjectType
        {
            Id = Guid.NewGuid(),
            AccountId = Context.AccountId.Value,
            EntityId = Context.AccountId.Value,
            CreatedOn = DateTime.UtcNow,
            Namespace = "salesforce.api",
            Name = source.Name,
            Description = $"{source.Label}: Salesforce Object",
            Label = source.Label,
            LabelPlural = source.LabelPlural,
            LastActor = Context.Actor(),
            LastModifiedOn = DateTime.UtcNow,
            IsEmbedded = true,
            Fields = new Dictionary<string, FieldTemplate>(),
            RBAC = new ObjectTypeRBAC(),
            // CollectionName = $"salesforce.{source.Name}",
            // DatabaseName = 
        };

        foreach (var srcField in source.Fields)
        {
            var field = ConvertField(srcField);
            if (field == null)
            {
                // ...
                continue;
            }

            var ft = new FieldTemplate
            {
                Field = field,
                Indexed = field switch
                {
                    SelectField or ReferenceField => true,
                    _ => false,
                },
            };

            var permissions = FieldPermission.Read;
            if (!srcField.Calculated)
            {
                if (srcField.Creatable) permissions |= FieldPermission.SetOnCreate;
                if (srcField.Updateable) permissions |= FieldPermission.Update;
            }

            ft.RBAC[EntityRoleId.Account] = permissions;
            ft.RBAC[EntityRoleId.Admin] = permissions;

            objectType.Fields[field.Name] = ft;
        }

        var objectPermissions = ObjectTypePermission.None;
        if (source.Searchable || source.Queryable || source.Retrieveable) objectPermissions |= ObjectTypePermission.Read;
        if (source.Creatable) objectPermissions |= ObjectTypePermission.Create;
        if (source.Deletable) objectPermissions |= ObjectTypePermission.Delete;
        if (source.Updateable) objectPermissions |= ObjectTypePermission.Update;
        // "activateable": false,
        // "replicateable": true,
        // "retrieveable": true,
        // "triggerable": true,
        // "undeletable": true,

        objectType.RBAC[EntityRoleId.Account] = objectPermissions;
        objectType.RBAC[EntityRoleId.Admin] = objectPermissions;

        // var sfIntegration =
        // {
        //     Type: "salesforce",
        //     Name: describe.Name,
        //     IsSubType: describe.IsSubType,
        //     HasSubtypes: describe.HasSubtypes,
        //     FieldMap: []models.FieldMap{
        //     {
        //     Source: "Id",
        //     TargetProperty: &externalIdField,
        //     SalesforceType: "id",
        //     },
        //     },
        // };

        // TODO: get layouts?
        // ...
        // "urls": {
        //     "compactLayouts": "/services/data/v50.0/sobjects/Lead/describe/compactLayouts",
        //     "rowTemplate": "/services/data/v50.0/sobjects/Lead/{ID}",
        //     "approvalLayouts": "/services/data/v50.0/sobjects/Lead/describe/approvalLayouts",
        //     "uiDetailTemplate": "https://fcifloors--fcistaging.sandbox.my.salesforce.com/{ID}",
        //     "uiEditTemplate": "https://fcifloors--fcistaging.sandbox.my.salesforce.com/{ID}/e",
        //     "listviews": "/services/data/v50.0/sobjects/Lead/listviews",
        //     "describe": "/services/data/v50.0/sobjects/Lead/describe",
        //     "uiNewRecord": "https://fcifloors--fcistaging.sandbox.my.salesforce.com/00Q/e",
        //     "quickActions": "/services/data/v50.0/sobjects/Lead/quickActions",
        //     "layouts": "/services/data/v50.0/sobjects/Lead/describe/layouts",
        //     "sobject": "/services/data/v50.0/sobjects/Lead"
        // }

        return objectType;
    }

    private static FormField ConvertField(SObjectFieldMetadata srcField)
    {
        FormField field = srcField.Type switch
        {
            "id" or "string" => new TextField
            {
                TextFieldOptions = new TextFieldOptions
                {
                    Multline = false,
                    MaxLength = srcField.Length,
                    ContentType = srcField.HtmlFormatted ? "text/html" : null,
                }
            },
            "textarea" => new TextField
            {
                TextFieldOptions = new TextFieldOptions
                {
                    Multline = true,
                    MaxLength = srcField.Length,
                    ContentType = srcField.HtmlFormatted ? "text/html" : null,
                }
            },
            "boolean" => new CheckboxField
            {
            },
            "int" => new NumberField
            {
                NumberFieldOptions = new NumberFieldOptions
                {
                    DecimalPlaces = 0,
                }
            },
            "double" or "percent" => new NumberField
            {
                NumberFieldOptions = new NumberFieldOptions
                {
                }
            },
            "currency" => new NumberField
            {
                NumberFieldOptions = new NumberFieldOptions
                {
                    Style = NumberFieldOptionsStyle.Currency,
                    DecimalPlaces = 2, // ???
                }
            },
            "date" => new DateField(),
            "datetime" => new DateTimeField(),
            "time" => new TimeField(),
            "url" => new UrlField
            {
                URLFieldOptions = new URLFieldOptions
                {
                    LinkUrl = "{{value}}", // ???
                }
            },
            "picklist" => new SelectField
            {
                SelectFieldOptions = new SelectFieldOptions
                {
                    Items = srcField.PicklistValues.ToImmutableSortedDictionary(x => x.Value, x => x.Label),
                },
            },
            "reference" => new ReferenceField
            {
                ReferenceFieldOptions = new ReferenceFieldOptions
                {
                    ObjectType = $"salesforce.{srcField.ReferenceTo[0]}",
                    ForeignFieldName = "ExternalId",
                    LinkUrl = $"dataForm://api/v1/CustomObject/salesforce.{srcField.ReferenceTo[0]}" + "({{value}})/View", // ???
                },
            },
            "phone" => new PhoneField
            {
                PhoneFieldOptions = new PhoneFieldOptions
                {
                    LinkUrl = "callto:{{value}}", // ???
                },
            },
            "email" => new EmailField
            {
                // EmailFieldOptions = new EmailFieldOptions
                // {
                //     LinkUrl = "mailto:{{value}}",                    
                // }
            },
            "base64" or "encryptedstring" => new HiddenField(), // ????

            "address" => null, // ???
            "combobox" => null, // ???
            "location" => null, // ??
            "multipicklist" => null, // ???
            _ => null,
        };

        if (field != null)
        {
            field.Name = srcField.Name;
            field.Label = srcField.Label;
            field.DefaultValue = srcField.DefaultValue;
            field.IsRequired = !srcField.Nillable;
        }

        return field;
    }
}