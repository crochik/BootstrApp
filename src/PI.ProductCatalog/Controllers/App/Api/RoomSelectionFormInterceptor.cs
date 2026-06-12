using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Extensions;
using Crochik.Mongo;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using PI.ProductCatalog.Models;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using PI.Shared.Requests;
using PI.Shared.Services;
using Services;

namespace Controllers;

public class RoomSelectionFormInterceptor(ILogger<RoomSelectionFormInterceptor> logger, MongoConnection connection, ObjectTypeService objectTypeService, EstimateService estimateService) : IInterceptAfter, IInterceptPrepareForm
{
    public string ObjectTypeName => "otg.RoomSelection";

    public string[] FormNames =>
    [
        nameof(FormName.Add),
        nameof(FormName.Edit),
    ];

    public string[] ActionNames => null;

    public async ValueTask<Result<Form>> PrepareFormAsync(IEntityContext context, string formName, Form form, HttpRequest request)
    {
        if (formName != nameof(FormName.Add))
        {
            // TODO: handle edit 
            // ...
            return Result.Success(form);
        }

        var defaultValues = form.Fields.Where(x => x.DefaultValue != null).ToDictionary(x => x.Name, x => x.DefaultValue);

        if (!defaultValues.TryGetValue(nameof(RoomSelection.RoomIds), out var roomIdsObj) || roomIdsObj is not IEnumerable<object> en)
        {
            return Result.Error<Form>("Didn't get rooms");
        }

        var roomIds = en.OfType<string>().Select(Guid.Parse).ToArray();

        defaultValues.TryGetValue(nameof(RoomSelection.ItemId), out var itemIdObj);

        var itemId = itemIdObj.TryToParseObjectId(out var uuid) ? uuid : default(Guid?);

        // if (!itemId.HasValue)
        // {
        //     return Result.Error<Form>("Didn't get item id");
        // }

        if (!defaultValues.TryGetValue(nameof(RoomSelection.SessionKey), out var sessionKeyObj) || sessionKeyObj is not string sessionKey)
        {
            return Result.Error<Form>("Didn't get session key");
        }

        RoomSelection existing = null;
        if (itemId.HasValue)
        {
            // try to load any previous for the same
            existing = await connection.Filter<RoomSelection>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.ItemId, itemId)
                .Eq(x => x.Hash, RoomSelection.CalculateHash(sessionKey, roomIds))
                .SortDesc(x => x.CreatedOn)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                if (existing.InstallationTypeId.HasValue) defaultValues[nameof(RoomSelection.InstallationTypeId)] = existing.InstallationTypeId.Value;
                if (existing.PatternTypeId.HasValue) defaultValues[nameof(RoomSelection.PatternTypeId)] = existing.PatternTypeId.Value;
                if (existing.TrimWorkId.HasValue) defaultValues[nameof(RoomSelection.TrimWorkId)] = existing.TrimWorkId.Value;
                if (existing.UnderlaymentId.HasValue) defaultValues[nameof(RoomSelection.UnderlaymentId)] = existing.UnderlaymentId.Value;
                if (existing.SubfloorPrepId.HasValue) defaultValues[nameof(RoomSelection.SubfloorPrepId)] = existing.SubfloorPrepId.Value;
                if (existing.StairsRiserFinishId.HasValue) defaultValues[nameof(RoomSelection.StairsRiserFinishId)] = existing.StairsRiserFinishId.Value;
            }
        }

        var rooms = await connection.Filter<AbstractRoom>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .In(x => x.Id, roomIds)
            .FindAsync();

        var existingSubFloorIds = rooms
            .OfType<RegularRoom>()
            .Select(x => x.ExistingSubfloorId).Distinct().ToArray();
        
        if (existingSubFloorIds.Length == 1)
        {
            defaultValues.Add(nameof(RegularRoom.ExistingSubfloorId), existingSubFloorIds.First());
        }

        // fields
        var fields = new List<FormField>();

        // if (existing != null)
        // {
        //     fields.Add(new LabelField
        //     {
        //         Name = "Message",
        //         Label = "WARNING: This operation can't be undone and will reset this estimate.",
        //         LabelFieldOptions = new LabelFieldOptions
        //         {
        //             Color = PalletColor.Error,
        //         },
        //     });
        //
        //     foreach (var layout in form.Layouts?.All.OfType<GridFormLayout>() ?? [])
        //     {
        //         layout.Rows = layout.Rows.Prepend(new GridFormRowLayout
        //         {
        //             Fields =
        //             [
        //                 new GridFormFieldLayout
        //                 {
        //                     Name = "Message",
        //                     Width = 12,
        //                 }
        //             ]
        //         }).ToArray();
        //     }
        // }

        foreach (var field in form.Fields)
        {
            var newField = await ProcessFieldAsync(context, defaultValues, field);
            if (newField == null || newField.IsUnknown) continue;
            if (newField.IsError) return newField.ConvertTo<Form>();

            if (defaultValues.TryGetValue(newField.Value.Name, out var defaultValue))
            {
                newField.Value.DefaultValue = defaultValue;
            }

            fields.Add(newField.Value);
        }

        form.Fields = fields.ToArray();
        form.Title = "New Estimate";

        // // actions
        // var addAction = form.Actions.FirstOrDefault(x => (x.Action ?? x.Name) == FormAction.Add);
        // if (addAction == null) return Result.Error<Form>("Missing Add Action");
        // addAction.Label = "Update";
        //
        // if (existing != null)
        // {
        //     form.Actions = form.Actions.Prepend(new FormAction
        //     {
        //         Name = FormAction.Client_Cancel,
        //         Label = "Cancel",
        //     }).ToArray();
        // }

        return Result.Success(form);
    }

    private async Task<Result<FormField>> ProcessFieldAsync(IEntityContext context, Dictionary<string, object> defaultValues, FormField field)
    {
        return field switch
        {
            ReferenceField referenceField => await ProcessReferenceFieldAsync(context, defaultValues, referenceField),
            SelectField s => processSelectField(s),
            _ => Result.Success(field),
        };

        Result<FormField> processSelectField(SelectField selectField)
        {
            if (selectField.SelectFieldOptions.Items.Count == 1)
            {
                return Result.Success<FormField>(new HiddenField
                {
                    Name = selectField.Name,
                    Label = selectField.Label,
                    ApiName = selectField.ApiName,
                    DefaultValue = selectField.SelectFieldOptions.Items.Keys.ToEnumerableObject().First(),
                });
            }

            return Result.Success<FormField>(selectField);
        }
    }


    private async Task<Result<FormField>> ProcessReferenceFieldAsync(IEntityContext context, Dictionary<string, object> defaultValues, ReferenceField field)
    {
        if (field.Name switch
            {
                nameof(RoomSelection.InstallationTypeId) => false,
                nameof(RoomSelection.PatternTypeId) => false,
                nameof(RoomSelection.TrimWorkId) => false,
                nameof(RoomSelection.UnderlaymentId) => false,
                nameof(RoomSelection.SubfloorPrepId) => false,
                nameof(RoomSelection.StairsRiserFinishId) => false,
                nameof(RoomSelection.RuleSetId) => false,
                _ => true,
            })
        {
            // other fields
            if (field.DefaultValue != null)
            {
                // already set, assume it can't be changed
                return Result.Success<FormField>(new HiddenField
                {
                    Name = field.Name,
                    Label = field.Label,
                    ApiName = field.ApiName,
                    DefaultValue = field.DefaultValue,
                });
            }

            return Result.Success<FormField>(field);
        }

        var conditions = new List<Condition>();
        if (field.ReferenceFieldOptions.Criteria?.Length > 0)
        {
            foreach (var criteria in field.ReferenceFieldOptions.Criteria)
            {
                var value = criteria.Value;
                if (value is string stringValue && stringValue.StartsWith("#{{"))
                {
                    value = stringValue[1..];
                }

                if (!ExpressionEvaluatorService.TryResolve(context, defaultValues, value, out var resolved))
                {
                    return Result.Error<FormField>($"Couldn't resolve: {value} for {criteria.FieldName}");
                }

                conditions.Add(Condition.New(criteria.FieldName, criteria.Operator, resolved));
            }
        }

        if (field.Name == nameof(RoomSelection.RuleSetId))
        {
            var objectType = await objectTypeService.GetAsync(context, field.ReferenceFieldOptions.ObjectType);
            var templates = await objectTypeService.FindAsync<RoomSelection>(context, objectType, conditions.ToArray());
            if (templates.Count == 1)
            {
                return Result.Success<FormField>(new HiddenField
                {
                    Name = field.Name,
                    Label = field.Label,
                    ApiName = field.ApiName,
                    DefaultValue = templates[0].Id,
                });
            }

            // convert to select field since we resolved the values
            return Result.Success<FormField>(new SelectField
            {
                Name = field.Name,
                Label = field.Label,
                ApiName = field.ApiName,
                DefaultValue = field.DefaultValue ?? (templates.IsEmpty() ? null : templates[0].Id),
                IsRequired = true,
                SelectFieldOptions = new SelectFieldOptions
                {
                    Items = templates.OrderBy(x => x.Name).ToImmutableSortedDictionary(x => x.Id, x => x.Name),
                }
            });
        }

        return await getInstallationOptionsAsync();

        async Task<Result<FormField>> getInstallationOptionsAsync()
        {
            var objectType = await objectTypeService.GetAsync(context, field.ReferenceFieldOptions.ObjectType);

            var installOptions = await objectTypeService.FindAsync<InstallationTypeOption>(context, objectType, conditions.ToArray());

            if (defaultValues.TryGetValue("ProductType", out var productType) && Enum.TryParse<ProductType>(productType.ToString(), out var productTypeCode))
            {
                var query = connection.Filter<EstimateOptionDefault>()
                    .Eq(x => x.ProductType, productTypeCode)
                    .Eq(x => x.ObjectType, field.Name switch
                    {
                        nameof(RoomSelection.InstallationTypeId) => "fcb2b.InstallationTypeOption",
                        nameof(RoomSelection.PatternTypeId) => "fcb2b.PatternTypeOption",
                        nameof(RoomSelection.TrimWorkId) => "fcb2b.TrimWorkOption",
                        nameof(RoomSelection.UnderlaymentId) => "fcb2b.UnderlaymentOption",
                        nameof(RoomSelection.SubfloorPrepId) => "fcb2b.SubfloorPrepOption",
                        nameof(RoomSelection.StairsRiserFinishId) => "fcb2b.StairsRiserFinishOption",
                        _ => throw new BadRequestException($"Unexpected Field: {field.Name}"),
                    })
                    .SortDesc(x => x.ExistingSubfloorId); // prefer exact match

                switch (field.Name)
                {
                    case nameof(RoomSelection.UnderlaymentId):
                    // case nameof(RoomSelection.SubfloorPrepId):
                    case nameof(RoomSelection.InstallationTypeId):
                    {
                        if (defaultValues.TryGetValue(nameof(RegularRoom.ExistingSubfloorId), out var existingSubFloorId))
                        {
                            query.In(x => x.ExistingSubfloorId, [null, existingSubFloorId]);
                        }

                        break;
                    }
                }

                var defaultOption = await query.FirstOrDefaultAsync();
                if (defaultOption != null)
                {
                    field.DefaultValue = defaultOption.EstimateOptionId;
                }
            }

            if (installOptions.IsEmpty()) return Result.Unknown<FormField>("No Options configured");
            
            if (installOptions.Count == 1)
            {
                return Result.Success<FormField>(new HiddenField
                {
                    Name = field.Name,
                    Label = field.Label,
                    ApiName = field.ApiName,
                    DefaultValue = installOptions[0].Id,
                });
            }

            // convert to select field since we resolved the values
            return Result.Success<FormField>(new SelectField
            {
                Name = field.Name,
                Label = field.Label,
                ApiName = field.ApiName,
                DefaultValue = field.DefaultValue, // ??  options[0].Id,
                IsRequired = true,
                SelectFieldOptions = new SelectFieldOptions
                {
                    Items = installOptions.OrderBy(x => x.Name).ToImmutableSortedDictionary(x => x.Id, x => x.Name),
                }
            });
        }
    }

    public async ValueTask<Result<DataFormActionResponse>> ProcessResponseAsync(IEntityContext context, string objectTypeName, Guid? objectId, string formName, DataFormActionRequest request, DataFormActionResponse response)
    {
        if (!response.Success) return Result.Success(response);

        if (response.Ids?.Length != 1) return Result.Error<DataFormActionResponse>("Failed to add");

        var selection = await connection.Filter<RoomSelection>()
            .Eq(x => x.AccountId, context.AccountId.Value)
            .Eq(x => x.Id, response.Ids.First())
            .FirstOrDefaultAsync();

        if (selection == null) return Result.Error<DataFormActionResponse>("Selection not found");

        if (selection.ItemId.HasValue)
        {
            // flag others for the same product / rooms as inactive
            await connection.Filter<RoomSelection>()
                .Eq(x => x.AccountId, context.AccountId.Value)
                .Eq(x => x.Hash, selection.Hash)
                .Eq(x => x.ItemId, selection.ItemId)
                .Ne(x => x.IsActive, false)
                // .Eq(x=>x.ObjectStatusId, selection.ObjectStatusId) // TODO: shouldn't change based on status?
                .Ne(x => x.Id, selection.Id)
                .Update
                .Set(x => x.IsActive, false)
                .Set(x => x.LastActor, context.Actor())
                .Set(x => x.LastModifiedOn, DateTime.UtcNow)
                .UpdateManyAsync();
        }

        var result = await estimateService.RecalculateAsync(context, selection);
        if (result.IsSuccess)
        {
            response.Message = null;
            return Result.Success(response);
        }

        return result.ConvertTo<DataFormActionResponse>();
    }

    
    // /// <summary>
    // /// Find existing selection for item
    // /// if not, try to find a selection for the same bin/product type and use it as a reference
    // /// will return error, unknow (couldn't find a reference) or success
    // /// </summary>
    // public async Task<Result<RoomSelection>> GetOrCreateAsync(IEntityContext context, RoomSelectionController.FindRoomSelectionRequest request)
    // {
    //     var roomSelection = await connection.Filter<RoomSelection>()
    //         .Eq(x => x.AccountId, context.AccountId.Value)
    //         .Eq(x => x.EntityId, context.OrganizationId.Value)
    //         .Eq(x => x.ProjectExternalId, request.ProjectExternalId)
    //         .Eq(x => x.ItemId, request.ItemId)
    //         .Eq(x => x.Hash, request.Hash)
    //         .SortDesc(x => x.CreatedOn)
    //         .FirstOrDefaultAsync();
    //
    //     if (roomSelection != null) return Result.Success(roomSelection);
    //
    //     // try to find one for the same product type
    //     var item = await connection.Filter<CatalogItem>()
    //         .Eq(x => x.AccountId, context.AccountId.Value)
    //         .Eq(x => x.EntityId, context.OrganizationId.Value)
    //         .Eq(x => x.Id, request.ItemId)
    //         .FirstOrDefaultAsync();
    //
    //     if (item == null || !ProductTypeResolver.TryResolve(item, out var productType) || !productType.HasValue)
    //     {
    //         return Result.Error<RoomSelection>("Catalog Item doesn't exist or couldn't determine product type");
    //     }
    //
    //     var reference = await connection.Filter<RoomSelection>()
    //         .Eq(x => x.AccountId, context.AccountId.Value)
    //         .Eq(x => x.EntityId, context.OrganizationId.Value)
    //         .Eq(x => x.ProjectExternalId, request.ProjectExternalId)
    //         .Eq(x => x.ProductType, productType.Value.ToString())
    //         .Eq(x => x.Hash, request.Hash)
    //         .SortDesc(x => x.CreatedOn)
    //         .FirstOrDefaultAsync();
    //
    //     if (reference == null)
    //     {
    //         return Result.Unknown<RoomSelection>("Couldn't find reference to copy settings from");
    //     }
    //
    //     // TODO: would need to validate that the original item and the new item are "compatible"
    //     // e.g. that the same settings apply ?
    //     // ....
    //
    //     roomSelection = new RoomSelection
    //     {
    //         Id = Guid.NewGuid(),
    //         AccountId = context.AccountId.Value,
    //         EntityId = context.OrganizationId.Value,
    //         CreatedOn = DateTime.UtcNow,
    //         CreatedBy = context.UserId.Value,
    //         BinId = request.BinId,
    //         ProjectExternalId = request.ProjectExternalId,
    //         ItemId = request.ItemId,
    //         ProductType = productType.ToString(),
    //         RoomIds = request.RoomIds,
    //         CatalogFeedId = item.CatalogFeedId,
    //         StyleNumber = item.StyleNumber,
    //         SessionKey = request.SessionKey,
    //         Name = request.Name,
    //         Description = request.Description,
    //         // copy settings from reference
    //         Bin = reference.Bin,
    //         FlowId = reference.FlowId,
    //         // ObjectStatusId = reference.ObjectStatusId,             // TODO: ???
    //         InstallationTypeId = reference.InstallationTypeId,
    //         PatternTypeId = reference.PatternTypeId,
    //         TrimWorkId = reference.TrimWorkId,
    //         UnderlaymentId = reference.UnderlaymentId,
    //         // TODO: anything else? 
    //         // ... 
    //     };
    //
    //     roomSelection = await objectTypeService.InsertAsync(context, roomSelection, e =>
    //     {
    //         e.Description = "Cloned from selection for same product type";
    //         e.Action = "ObjectCloned";
    //     });
    //
    //     // calculate estimate
    //     return await RecalculateAsync(context, roomSelection);
    // }
}