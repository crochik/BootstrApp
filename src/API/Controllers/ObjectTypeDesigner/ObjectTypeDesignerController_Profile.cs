using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Designer;
using PI.Shared.Models.Expressions;
using PI.Shared.Requests;

namespace Controllers;

[Authorize("admin")]
public partial class ObjectTypeDesignerController
{
    /// <summary>
    /// Add profile access to object type
    /// </summary>
    [HttpGet("/api/v1/[controller]({objectTypeDraftId})/Profile/Add/DataForm")]
    public async Task<Form> GetAddProfileFormAsync([FromRoute] Guid objectTypeDraftId)
    {
        await CheckPermission(ObjectTypeDraft.ObjectTypeFullName, ObjectTypePermission.Read);
        
        var draft = await GetDraftAsync(objectTypeDraftId);

        return new Form
        {
            Name = "AddProfile",
            Title = "Profile Access",
            // ObjectType = "Field",
            Fields =
            [
                new ReferenceField
                {
                    Name = "ProfileId",
                    Label = "Profile",
                    ReferenceFieldOptions = new ReferenceFieldOptions
                    {
                        ObjectType = "AppProfile",
                        ForeignFieldName = Model.IdFieldName,
                        Items = new Dictionary<string, string>
                        {
                            { nameof(EntityRoleId.Admin), $"[{nameof(EntityRoleId.Admin)}]" },
                            { nameof(EntityRoleId.Account), $"[{nameof(EntityRoleId.Account)}]" },
                            { nameof(EntityRoleId.Manager), $"[{nameof(EntityRoleId.Manager)}]" },
                            { nameof(EntityRoleId.Organization), $"[{nameof(EntityRoleId.Organization)}]" },
                            { nameof(EntityRoleId.User), $"[{nameof(EntityRoleId.User)}]" }
                        }
                    },
                    IsRequired = true,
                },
                new ReferenceField
                {
                    Name = "CopyFrom",
                    Label = "Copy From",
                    ReferenceFieldOptions = new ReferenceFieldOptions
                    {
                        ObjectType = "AppProfile",
                        Criteria =
                        [
                            Condition.In(Model.IdFieldName, draft.ObjectType.RBAC.Permissions.Keys.Where(x => Guid.TryParse((string)x, out _)).ToArray()),
                        ],
                        ForeignFieldName = Model.IdFieldName,
                        Items = new Dictionary<string, string>
                        {
                            { nameof(EntityRoleId.Admin), $"[{nameof(EntityRoleId.Admin)}]" },
                            { nameof(EntityRoleId.Account), $"[{nameof(EntityRoleId.Account)}]" },
                            { nameof(EntityRoleId.Manager), $"[{nameof(EntityRoleId.Manager)}]" },
                            { nameof(EntityRoleId.Organization), $"[{nameof(EntityRoleId.Organization)}]" },
                            { nameof(EntityRoleId.User), $"[{nameof(EntityRoleId.User)}]" }
                        }
                    },
                },
                new BitwiseFlagField
                {
                    Name = nameof(ObjectTypePermission),
                    Label = "Object Permissions",
                    BitwiseFlagFieldOptions = new BitwiseFlagFieldOptions
                    {
                        Items = new Dictionary<string, string>
                        {
                            { $"{(int)ObjectTypePermission.Read}", $"{nameof(ObjectTypePermission.Read)}" },
                            { $"{(int)ObjectTypePermission.Update}", $"{nameof(ObjectTypePermission.Update)}" },
                            { $"{(int)ObjectTypePermission.Create}", $"{nameof(ObjectTypePermission.Create)}" },
                            { $"{(int)ObjectTypePermission.Delete}", $"{nameof(ObjectTypePermission.Delete)}" },
                            { $"{(int)ObjectTypePermission.Import}", $"{nameof(ObjectTypePermission.Import)}" },
                            { $"{(int)ObjectTypePermission.Export}", $"{nameof(ObjectTypePermission.Export)}" },
                            { $"{(int)ObjectTypePermission.BulkDelete}", "Delete in Bulk" },
                            { $"{(int)ObjectTypePermission.BulkTag}", "Tag in Bulk" },
                            { $"{(int)ObjectTypePermission.Customize}", "Customize ?" },
                        },
                    },
                    // DefaultValue = (int)ObjectTypePermission.Read,
                },
                new BitwiseFlagField
                {
                    Name = nameof(FieldPermission),
                    Label = "Default Field Permissions",
                    BitwiseFlagFieldOptions = new BitwiseFlagFieldOptions
                    {
                        Items = new Dictionary<string, string>
                        {
                            { $"{(int)FieldPermission.Read}", $"{nameof(FieldPermission.Read)}" },
                            { $"{(int)FieldPermission.Update}", $"{nameof(FieldPermission.Update)}" },
                            { $"{(int)FieldPermission.SetOnCreate}", "Create" },
                            { $"{(int)FieldPermission.Reset}", "Reset" },
                            { $"{(int)FieldPermission.CreateOnDemand}", "Create On Demand" },
                        },
                    },
                    // DefaultValue = (int)FieldPermission.Read,
                },
                new BitwiseFlagField
                {
                    Name = nameof(RelatedObjectTypePermission),
                    Label = "Default Relation Permissions",
                    BitwiseFlagFieldOptions = new BitwiseFlagFieldOptions
                    {
                        Items = new Dictionary<string, string>
                        {
                            { $"{(int)RelatedObjectTypePermission.Read}", $"{nameof(RelatedObjectTypePermission.Read)}" },
                        },
                    },
                    // DefaultValue = (int)RelatedObjectTypePermission.Read,
                },
            ],
            Actions =
            [
                new FormAction
                {
                    Name = "Grant",
                    Label = "Grant Access",
                    Enable =
                    [
                        Form.RequiredFieldsName
                    ]
                }
            ]
        };
    }

    /// <summary>
    /// Add profile access to object type
    /// - if specified, it will copy access from other profile
    /// </summary>
    [HttpPost("/api/v1/[controller]({objectTypeDraftId})/Profile/Add/DataForm")]
    public async Task<DataFormActionResponse> AddProfileFormAsync([FromRoute] Guid objectTypeDraftId, [FromBody] DataFormActionRequest request)
    {
        await CheckPermission(ObjectTypeDraft.ObjectTypeFullName, ObjectTypePermission.Update | ObjectTypePermission.Read);
        
        if (!request.TryGetStrParam("ProfileId", out var roleOrProfileId))
        {
            return DataFormActionResponse.Error(request, "Missing required profile ID.");
        }

        if (!request.TryGetStrParam("CopyFrom", out var copyFrom))
        {
            copyFrom = null;
        }

        if (!request.TryGetParam(nameof(ObjectTypePermission), out var objPermissionObj) || objPermissionObj is not long defaultObjectPermissions)
        {
            defaultObjectPermissions = 0;
        }

        if (!request.TryGetParam(nameof(FieldPermission), out var fieldPermissionObj) || fieldPermissionObj is not long defaultFieldPermissions)
        {
            defaultFieldPermissions = 0;
        }

        if (!request.TryGetParam(nameof(RelatedObjectTypePermission), out var relationPermissionObj) || relationPermissionObj is not long defaultRelationPermissions)
        {
            defaultRelationPermissions = 0;
        }

        var draft = await GetDraftAsync(objectTypeDraftId);
        if (draft == null) return DataFormActionResponse.Error(request, "Draft not found");

        var updateQuery = UpdateQuery(objectTypeDraftId);

        if (copyFrom == null || !draft.ObjectType.RBAC.Permissions.TryGetValue(copyFrom, out var objectPermissions))
        {
            objectPermissions = (ObjectTypePermission)defaultObjectPermissions;
        }

        updateQuery.Set($"{nameof(ObjectTypeDraft.ObjectType)}.{nameof(ObjectType.RBAC)}.{nameof(ObjectTypeRBAC.Permissions)}.{roleOrProfileId}", objectPermissions);

        foreach (var kvp in draft.ObjectType.Fields)
        {
            if (copyFrom == null || !kvp.Value.RBAC.Permissions.TryGetValue(copyFrom, out var fieldPermissions))
            {
                fieldPermissions = (FieldPermission)defaultFieldPermissions;
            }

            updateQuery.Set($"{nameof(ObjectTypeDraft.ObjectType)}.{nameof(ObjectType.Fields)}.{kvp.Key}.{nameof(FieldTemplate.RBAC)}.{nameof(FieldRBAC.Permissions)}.{roleOrProfileId}", fieldPermissions);
        }

        if (draft.ObjectType.RelatedObjectTypes?.Length > 0)
        {
            for (var c = 0; c < draft.ObjectType.RelatedObjectTypes.Length; c++)
            {
                if (copyFrom == null || !draft.ObjectType.RelatedObjectTypes[c].RBAC.Permissions.TryGetValue(copyFrom, out var relationPermission))
                {
                    relationPermission = (RelatedObjectTypePermission)defaultRelationPermissions;
                }

                updateQuery.Set($"{nameof(ObjectTypeDraft.ObjectType)}.{nameof(ObjectType.RelatedObjectTypes)}.{c}.{nameof(RelatedObjectType.RBAC)}.{nameof(FieldRBAC.Permissions)}.{roleOrProfileId}", relationPermission);
            }
        }

        var result = await updateQuery.UpdateOneAsync();
        if (result.MatchedCount == 0) return DataFormActionResponse.Error(request, "Draft not found");
        return new DataFormActionResponse(request, success: true);
    }

    /// <summary>
    /// Remove profile access to object type
    /// </summary>
    [HttpGet("/api/v1/[controller]({objectTypeDraftId})/Profile/Remove/DataForm")]
    public async Task<Form> GetRemoveProfileFormAsync([FromRoute] Guid objectTypeDraftId, [FromQuery] string profileId)
    {
        await CheckPermission(ObjectTypeDraft.ObjectTypeFullName, ObjectTypePermission.Read);
        
        var draft = await GetDraftAsync(objectTypeDraftId);

        return new Form
        {
            Name = "RemoveProfile",
            Title = "Profile Access",
            // ObjectType = "Field",
            Fields =
            [
                new ReferenceField
                {
                    Name = "ProfileId",
                    Label = "Profile/Role",
                    ReferenceFieldOptions = new ReferenceFieldOptions
                    {
                        ObjectType = "AppProfile",
                        Criteria =
                        [
                            Condition.In(Model.IdFieldName, draft.ObjectType.RBAC.Permissions.Keys.Where(x => Guid.TryParse(x, out _)).ToArray()),
                        ],
                        ForeignFieldName = Model.IdFieldName,
                        Items = new Dictionary<string, string>
                        {
                            { nameof(EntityRoleId.Admin), $"[{nameof(EntityRoleId.Admin)}]" },
                            { nameof(EntityRoleId.Account), $"[{nameof(EntityRoleId.Account)}]" },
                            { nameof(EntityRoleId.Manager), $"[{nameof(EntityRoleId.Manager)}]" },
                            { nameof(EntityRoleId.Organization), $"[{nameof(EntityRoleId.Organization)}]" },
                            { nameof(EntityRoleId.User), $"[{nameof(EntityRoleId.User)}]" }
                        }
                    },
                    DefaultValue = profileId,
                    IsRequired = true,
                }
            ],
            Actions =
            [
                new FormAction
                {
                    Name = "Revoke",
                    Label = "Revoke Access",
                    Enable =
                    [
                        Form.RequiredFieldsName
                    ]
                }
            ]
        };
    }

    /// <summary>
    /// Remove profile access to object type
    /// </summary>
    [HttpPost("/api/v1/[controller]({objectTypeDraftId})/Profile/Remove/DataForm")]
    public async Task<DataFormActionResponse> RemoveProfileFormAsync([FromRoute] Guid objectTypeDraftId, [FromBody] DataFormActionRequest request)
    {
        await CheckPermission(ObjectTypeDraft.ObjectTypeFullName, ObjectTypePermission.Update | ObjectTypePermission.Read);
        
        if (!request.TryGetStrParam("ProfileId", out var roleOrProfileId))
        {
            return DataFormActionResponse.Error(request, "Missing required profile ID.");
        }

        var draft = await GetDraftAsync(objectTypeDraftId);
        if (draft == null) return DataFormActionResponse.Error(request, "Draft not found");

        var updateQuery = UpdateQuery(objectTypeDraftId);

        updateQuery.Unset($"{nameof(ObjectTypeDraft.ObjectType)}.{nameof(ObjectType.RBAC)}.{nameof(ObjectTypeRBAC.Permissions)}.{roleOrProfileId}");

        foreach (var kvp in draft.ObjectType.Fields)
        {
            updateQuery.Unset($"{nameof(ObjectTypeDraft.ObjectType)}.{nameof(ObjectType.Fields)}.{kvp.Key}.{nameof(FieldTemplate.RBAC)}.{nameof(FieldRBAC.Permissions)}.{roleOrProfileId}");
        }

        if (draft.ObjectType.RelatedObjectTypes?.Length > 0)
        {
            for (var c = 0; c < draft.ObjectType.RelatedObjectTypes.Length; c++)
            {
                updateQuery.Unset($"{nameof(ObjectTypeDraft.ObjectType)}.{nameof(ObjectType.RelatedObjectTypes)}.{c}.{nameof(RelatedObjectType.RBAC)}.{nameof(FieldRBAC.Permissions)}.{roleOrProfileId}");
            }
        }

        var result = await updateQuery.UpdateOneAsync();
        if (result.MatchedCount == 0) return DataFormActionResponse.Error(request, "Draft not found");
        return new DataFormActionResponse(request, success: true);
    }
}