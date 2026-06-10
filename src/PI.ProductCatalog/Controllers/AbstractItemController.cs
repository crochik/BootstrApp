using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AutoMapper;
using Controllers.Models;
using Crochik.Dipper;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using PI.ProductCatalog.Models;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Extensions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;
using Crochik.Extensions;

namespace Controllers
{
    public class AbstractItemController : APIController
    {
        protected readonly ILogger<AbstractItemController> _logger;
        protected readonly IMapper _mapper;
        protected readonly MongoConnection _connection;

        protected Guid EntityId => Context.Role switch
        {
            EntityRoleId.Admin => Context.AccountId.Value,
            EntityRoleId.Manager => Context.OrganizationId.Value,
            EntityRoleId.User => Context.OrganizationId.Value,
            _ => throw new ForbiddenException(Context, "Invalid Context")
        };

        protected AbstractItemController(
            ILogger<AbstractItemController> logger,
            IMapper mapper,
            MongoConnection connection
        )
        {
            this._logger = logger;
            this._mapper = mapper;
            this._connection = connection;
        }

        protected async Task<Breadcrumb> LoadAsync(Guid entityId, Guid parentId)
        {
            var parent = await _connection.Filter<Breadcrumb>()
                .Eq(x => x.AccountId, Context.AccountId.Value)
                .Eq(x => x.EntityId, entityId)
                .Eq(x => x.Id, parentId)
                .FirstOrDefaultAsync();

            if (parent == null) throw new NotFoundException(nameof(Breadcrumb), parentId);
            return parent;
        }

        protected async Task<Form> GetEditFormAsync<T>(Guid entityId, Guid id)
            where T : CatalogItem
        {
            // var result = await _objectTypeService.GetDataFormAsync(Context, objectType, id);
            // if (result == null) throw new NotFoundException();

            // return result;

            var showCosts = Context.Role switch
            {
                EntityRoleId.Account => true,
                EntityRoleId.Admin => true,
                EntityRoleId.Manager => true,
                EntityRoleId.Organization => true,
                _ => false,
            };

            var query = _connection.Filter<T>()
                .Eq(x => x.AccountId, Context.AccountId.Value);

            if (Context.Role != EntityRoleId.Admin) query.Eq(x => x.EntityId, entityId);

            var item = await query
                .Eq(x => x.Id, id)
                .FirstOrDefaultAsync();

            if (item == null) throw new NotFoundException(nameof(CatalogItem), id);

            item.Update();

            return new Form
            {
                Name = "CatalogItem",
                Title = item.Name,
                Fields = createFields().Where(x => x != null).ToArray(),
                IsReadOnly = true,
            };

            IEnumerable<FormField> createFields()
            {
                yield return new HiddenField
                {
                    Name = nameof(CatalogItem.Id),
                    DefaultValue = item.Id,
                };
                yield return createField(nameof(CatalogItem.Name), item.Name);
                yield return createField(nameof(CatalogItem.Description), item.Description);
                yield return createField(nameof(CatalogItem.SKU), item.SKU);
                yield return createField(nameof(CatalogItem.AbbreviatedProductName), item.AbbreviatedProductName, "Abbreviated Name");

                yield return createField(nameof(CatalogItem.StyleNumber), item.StyleNumber, "Style Number");
                yield return createField(nameof(CatalogItem.StyleName), item.StyleName, "Style Name");
                yield return createField(nameof(CatalogItem.CollectionName), item.CollectionName, "Collection Name");

                yield return createField(nameof(CatalogItem.ColorNumber), item.ColorNumber, "Color Number");
                yield return createField(nameof(CatalogItem.ColorName), item.ColorName, "Color Name");

                yield return createField("MaterialType", item.Material?.Type.GetDescription(), "Material Type");
                yield return createField("MaterialSubType", item.Material?.SubType.GetDescription(), "Material Sub Type");

                yield return createField("MaterialApplication", item.Material?.Application, "Material Application");
                yield return createField(nameof(CatalogItem.ProductType), item.ProductType, "Product Type");
                yield return createField(nameof(CatalogItem.Composition), item.Composition);

                yield return createField(nameof(CatalogItem.SellingCompany), item.SellingCompany, "Selling Company");

                yield return createField(nameof(CatalogItem.Manufacturer), item.Manufacturer);
                yield return createField(nameof(CatalogItem.ManufacturerStyleNumber), item.ManufacturerStyleNumber, "Manufacturer Style Number");
                yield return createField(nameof(CatalogItem.ManufacturerStyleName), item.ManufacturerStyleName, "Manufacturer Style Name");
                yield return createField(nameof(CatalogItem.ManufacturerSKU), item.ManufacturerSKU, "Manufacturer SKU");

                yield return createField(nameof(CatalogItem.PricingGroup), item.PricingGroup, "Pricing Group");
                yield return createField(nameof(CatalogItem.BuyingGroup), item.BuyingGroup, "Buying Group");
                yield return createField(nameof(CatalogItem.Contract), item.Contract);

                yield return createField(nameof(CatalogItem.PrimaryComponent), item.PrimaryComponent, "Primary Component");
                yield return createField(nameof(CatalogItem.Warranty), item.Warranty?.ToString());
                yield return createField(nameof(CatalogItem.BuilderProgram), item.BuilderProgram?.ToString(), "Builder Program");
                yield return createField(nameof(CatalogItem.ProductSpecification), item.ProductSpecification, "Product Specification");
                // yield return createField(nameof(CatalogItem.UniqueStyleCode), item.UniqueStyleCode);
                // yield return createField(nameof(CatalogItem.UniqueColorCode), item.UniqueColorCode);

                yield return createField(nameof(CatalogItem.Backing), item.Backing);
                yield return createField(nameof(CatalogItem.SizeCode), item.SizeCode);

                yield return createField(nameof(CatalogItem.RollLength), item.RollLength?.ToString(), "Roll Length");
                yield return createField(nameof(CatalogItem.RollWidth), item.RollWidth?.ToString(), "Roll Width");

                yield return createField(nameof(CatalogItem.NominalWidth), item.NominalWidth?.ToString(), "Nominal Width");
                yield return createField(nameof(CatalogItem.NominalLength), item.NominalLength?.ToString(), "Nominal Length");
                yield return createField(nameof(CatalogItem.ActualWidth), item.ActualWidth?.ToString(), "Actual Width");
                yield return createField(nameof(CatalogItem.ActualLength), item.ActualLength?.ToString(), "Actual Length");
                yield return createField(nameof(CatalogItem.Height), item.Height?.ToString(), "Height");

                yield return createField(nameof(CatalogItem.PatternWidth), item.PatternWidth?.ToString(), "Pattern Width");
                yield return createField(nameof(CatalogItem.PatternLength), item.PatternLength?.ToString(), "Pattern Length");
                yield return createField(nameof(CatalogItem.PatternRepeat), item.PatternRepeat?.ToString(), "Pattern Repeat");
                yield return createField(nameof(CatalogItem.PatternDrop), item.PatternDrop?.ToString(), "Pattern Drop");

                yield return createField(nameof(CatalogItem.FaceWeight), item.FaceWeight?.ToString(), "Face Weight");
                yield return createField(nameof(CatalogItem.ShippingWeight), item.ShippingWeight?.ToString(), "Shipping Weight");

                yield return createField(nameof(CatalogItem.SellingUnit), item.SellingUnit?.ToString(), "Selling Unit");

                yield return createField(nameof(CatalogItem.PromotionalStart), item.PromotionalStart, "Promotional Start");
                yield return createField(nameof(CatalogItem.PromotionalEnd), item.PromotionalEnd, "Promotional End");
                yield return createField(nameof(CatalogItem.DroppedDate), item.DroppedDate, "Dropped Date");

                yield return createField(nameof(CatalogItem.LastModifiedOn), item.LastModifiedOn, "Last Modified");

                // packages
                yield return new LabelField
                {
                    Name = "PacakesLbl",
                    Label = "All Packages",
                    LabelFieldOptions =
                    {
                        Color = PalletColor.Primary,
                        Style = LabelStyle.Subheader,
                    }
                };

                var packaging = item.GetAllPackaging();
                if (packaging != null)
                {
                    var index = 0;
                    foreach (var package in packaging)
                    {
                        yield return createField($"{nameof(CatalogItem.Packaging)}_{package.UOM}{++index}", package.Measurement?.ToString(), package.UOM.ToString());
                    }
                }

                // costs
                if (showCosts && item.Costs != null)
                {
                    yield return new LabelField
                    {
                        Name = "PacakesLbl",
                        Label = "All Costs",
                        LabelFieldOptions =
                        {
                            Color = PalletColor.Primary,
                            Style = LabelStyle.Subheader,
                        }
                    };

                    for (var i = 0; i < item.Costs.Length; i++)
                    {
                        var cost = item.Costs[i];
                        yield return createField($"{nameof(CatalogItem.Packaging)}_{i + 1}", cost.ToString(), $"Cost #{i + 1}");
                    }
                }

                // salesforce
                yield return new LabelField
                {
                    Name = "PacakesLbl",
                    Label = "Salesforce",
                    LabelFieldOptions =
                    {
                        Color = PalletColor.Primary,
                        Style = LabelStyle.Subheader,
                    }
                };

                yield return createField(nameof(CatalogItem.Package), item.Package?.ToString(), "Default Package");
                yield return createField(nameof(CatalogItem.PackagesPerPallet), item.PackagesPerPallet?.ToString(), "Pallet (Packages)");

                if (showCosts)
                {
                    if (item.Material?.IsRollGoods ?? false)
                    {
                        yield return createField(nameof(CatalogItem.CutCost), item.CutCost?.ToString(), "Cut Cost");
                        yield return createField(nameof(CatalogItem.StandardCost), item.StandardCost?.ToString(), "Standard/Roll Cost");
                    }
                    else
                    {
                        yield return createField(nameof(CatalogItem.StandardCost), item.StandardCost?.ToString(), "Standard Cost");
                        yield return createField(nameof(CatalogItem.PalletCost), item.CutCost?.ToString(), "Pallet Cost");
                    }
                }

                if (!string.IsNullOrEmpty(item.Salesforce?.Url))
                {
                    if (!string.IsNullOrEmpty(item.Salesforce.Pricebook2))
                    {
                        yield return new UrlField
                        {
                            Name = nameof(CatalogItem.ExternalId),
                            Label = "Salesforce Pricebook Entry",
                            DefaultValue = $"{item.Salesforce.Url}/{item.Salesforce.Pricebook2}",
                            Enable = new[] { "false" },
                        };
                    }
                }

                yield return createField(nameof(CatalogItem.Salesforce.LastSyncedOn), item.Salesforce?.LastSyncedOn, "Last Synced");

                if (Context.Role == EntityRoleId.Admin) yield return createField(nameof(CatalogItem.Id), $"ObjectId(\"{item.Id.ToObjectId()}\")", "Id");
            }

            static FormField createField(string name, object value, string label = null)
                => value != null
                    ? new TextField
                    {
                        Name = name,
                        Label = label,
                        DefaultValue = value,
                        Enable = new[] { "false" },
                    }
                    : null;
        }

        protected async Task<List<CatalogItemView>> GetResultAsync<T>(Guid entityId, DataViewResponse resp, Guid? spreadsheetId = default) where T : CatalogItem
        {
            var reverseSort = resp.Request?.OrderBy?.StartsWith("-") ?? false;
            var sort = reverseSort ? resp.Request.OrderBy.Substring(1) : resp.Request?.OrderBy;
            sort = sort?.ToLower() switch
            {
                "sku" => "SKU",
                _ => "Name",
            };

            var collection = _connection.GetCollectionName<T>();

            var sp = new AggregateStoredProcedure
            {
                Collection = collection,
                Pipeline = new[]
                {
                    "{ \"$sort\": { \"" + sort + "\": " + (reverseSort ? "-1" : "1") + " } }",
                }
            };

            var query = _connection.Filter<T>();

            await FilterAsync(entityId, query, resp.Request.Criteria, spreadsheetId);

            if (resp.Request.Top > 0)
            {
                query.Skip(resp.Request.Skip).Limit(resp.Request.Top);
            }
            else
            {
                query.Limit(resp.View.PageSize);
            }

            var result = await query.DipperAsync<CatalogItemView>(sp);
            return result;
        }

        protected async Task<IEnumerable<BreadcrumbReferenceValue>> LookupAsync<T>(Guid entityId, string objectType, DataViewRequest request, Guid? spreadsheetId = null)
            where T : CatalogItem
        {
            if (request.Criteria.TryGetEqCondition(Condition.LookupId, out var condition))
            {
                // hack for first load
                return new BreadcrumbReferenceValue
                {
                    Id = condition.Value.ToString(),
                }.AsEnumerable();
            }

            // limit fields to whitelited
            // ...

            var fieldName = objectType switch
            {
                "MaterialType" => "Material.Type",
                "MaterialSubType" => "Material.SubType",
                _ => $"{objectType}",
            };

            var collection = _connection.GetCollectionName<T>();

            var sp = new AggregateStoredProcedure
            {
                Collection = collection,
                Pipeline = new[]
                {
                    "{ \"$group\" : { \"_id\" : \"$" + fieldName + "\", \"Count\": { \"$sum\": 1 } } }",
                    "{ \"$sort\": { \"_id\": 1 } }",
                    "{ \"$limit\": 100 }",
                }
            };

            if (sp == null) throw new NotFoundException($"{objectType} lookup not found");


            var query = _connection.Filter<T>(sp.Collection);
            await FilterAsync(entityId, query, request.Criteria, spreadsheetId, fieldName);

            var result = await query.DipperAsync<BreadcrumbReferenceValue>(sp);
            return result;
        }

        protected async Task FilterAsync<T>(Guid entityId, Query<T> query, Condition[] criteria, Guid? spreadSheetId, string skip = null) where T : CatalogItem
        {
            foreach (var filter in criteria ?? Enumerable.Empty<Condition>())
            {
                var fieldName = filter.FieldName switch
                {
                    "MaterialType" => "Material.Type",
                    "MaterialSubType" => "Material.SubType",
                    Condition.AutoComplete => skip,
                    _ => filter.FieldName,
                };

                if (string.Equals(fieldName, skip))
                {
                    if (!string.IsNullOrWhiteSpace(filter.Value?.ToString()))
                    {
                        var regex = new BsonRegularExpression($"{Regex.Escape(filter.Value.ToString())}", "i");
                        query.Regex(fieldName, regex);
                    }

                    continue;
                }

                switch (fieldName)
                {
                    case nameof(CatalogItem.ParentIds):
                        if (filter.Value.TryParseGuid(out var parentId))
                        {
                            query.AnyEq(x => x.ParentIds, parentId);
                        }
                        else
                        {
                            throw new BadRequestException($"Invalid ParentId: {filter.Value}");
                        }

                        break;

                    case Condition.LookupId:
                    {
                        if (filter.Value.TryParseGuid(out var id))
                        {
                            // specific id
                            query.Eq(x => x.Id, id);
                        }
                        else if (filter.Value is IEnumerable<object> list)
                        {
                            // list of ids 
                            var ids = list.Select(x => PropertyValueConverter.ConvertTo<Guid>(x)).ToArray();
                            query.In(x => x.Id, ids);
                        }

                        break;
                    }

                    case Condition.FullTextSearch:
                    {
                        var value = filter.Value?.ToString();
                        if (!string.IsNullOrWhiteSpace(value)) query.Text(filter.Value.ToString());
                        break;
                    }

                    default:
                        switch (filter.Operator)
                        {
                            case Operator.Gt:
                                query.Gt(fieldName, filter.Value);
                                break;
                            case Operator.Lt:
                                query.Lt(fieldName, filter.Value);
                                break;
                            case Operator.Eq:
                                query.Eq(fieldName, filter.Value);
                                break;
                            case Operator.Ne:
                                query.Ne(fieldName, filter.Value);
                                break;
                            default:
                                throw new BadRequestException($"{filter.Operator} not supported");
                        }

                        break;
                }
            }

            if (spreadSheetId.HasValue)
            {
                query.Eq(nameof(CatalogItemStaging.ParentId), spreadSheetId.Value.AsSerializedId());

                var spreadsheet = await _connection.Filter<Spreadsheet>()
                    .Eq(x => x.AccountId, Context.AccountId.Value)
                    .Eq(x => x.Id, spreadSheetId)
                    .FirstOrDefaultAsync();

                entityId = spreadsheet.EntityId;
            }
            // else 
            // {
            //     // 2021/12/23: limit to active
            //     query.Eq(x => x.IsActive, true);
            // }

            // limit to account/entity last
            query.Eq(nameof(CatalogItem.AccountId), Context.AccountId.Value);
            query.Eq(nameof(CatalogItem.EntityId), entityId);
        }

        // TODO: should use dataview builder
        // ...
        protected DataView GetDataView(DataViewRequest request, bool showMargin, string prefix)
        {
            var filters = request.Criteria?.ToDictionary(x => x.FieldName, x => x.Value) ?? new Dictionary<string, object>();

            var fields = getFields().ToArray();

            if (request.Fields?.Length > 0)
            {
                // TODO: update user settings
                // ...
                var visible = request.Fields.ToHashSet();
                foreach (var field in fields)
                {
                    if (visible.Contains(field.Name)) continue;
                    field.Visible = new[] { "false" };
                }
            }
            else
            {
                // TODO: load last settings
                // ...
                request.Fields = fields.Select(x => x.Name).ToArray();
            }

            var view = new DataView
            {
                Name = nameof(CatalogItem),
                Title = "Product Catalog",
                Fields = fields,
                DefaultSort = "Name",
                KeyField = "id",
                IsSelectable = false,
                PageSize = 100,
                Searchable = true,
                Actions = new FormAction[]
                {
                    new FormAction
                    {
                        Name = "edit",
                        Action = "dataForm://" + prefix + "({{id}})",
                        Visible = new[] { "false" },
                    }
                },
            };

            return view;

            IEnumerable<FormField> getFields()
            {
                yield return new HiddenField
                {
                    Name = "id",
                    Label = "Id",
                };

                yield return new TextField
                {
                    Name = "sku",
                    Label = "SKU",
                };

                yield return new HiddenField
                {
                    Name = "name",
                    Label = "Name",
                };

                yield return new TextField
                {
                    Name = "description",
                    Label = "Description",
                };

                yield return new TextField
                {
                    Name = "sellingUnit",
                    Label = "Selling Unit"
                };

                if (showMargin)
                {
                    yield return new TextField
                    {
                        Name = "margin",
                        Label = "Margin",
                    };

                    yield return new TextField
                    {
                        Name = "formattedCost1",
                        Label = "Cost",
                        Options = new TextFieldOptions
                        {
                            Multline = true,
                        }
                    };

                    yield return new TextField
                    {
                        Name = "formattedPrice1",
                        Label = "Price",
                    };

                    yield return new TextField
                    {
                        Name = "formattedCost2",
                        Label = "Cost 2",
                        Options = new TextFieldOptions
                        {
                            Multline = true,
                        }
                    };

                    yield return new TextField
                    {
                        Name = "formattedPrice2",
                        Label = "Price 2",
                    };

                    // yield return new CheckboxField
                    // {
                    //     Name = "isActive",
                    //     Label = "Active?",
                    // };

                    yield break;
                }

                yield return new TextField
                {
                    Name = "width",
                    Label = "Width"
                };

                yield return new TextField
                {
                    Name = "length",
                    Label = "Length"
                };

                yield return new TextField
                {
                    Name = "carton",
                    Label = "Carton",
                };

                yield return new TextField
                {
                    Name = "pallet",
                    Label = "Pallet",
                };

                // new TextField
                // {
                //     Name = "baseUnit",
                //     Label = "Base Unit"
                // },


                // if (!filters.ContainsKey("MaterialType"))
                // {
                //     fields.Add(new TextField
                //     {
                //         Name = "materialType",
                //         Label = "Material",
                //     });
                // }

                // if (!filters.ContainsKey("MaterialSubType"))
                // {
                //     fields.Add(new TextField
                //     {
                //         Name = "materialSubType",
                //         Label = "Material (Sub)",
                //     });
                // }

                // if (!filters.ContainsKey(nameof(CatalogItem.ProductType)))
                // {
                //     fields.Add(new TextField
                //     {
                //         Name = nameof(CatalogItem.ProductType).ToCamelCase(),
                //         Label = "Product Type"
                //     });
                // }

                // if (!filters.ContainsKey(nameof(CatalogItem.Manufacturer)))
                // {
                //     fields.Add(new TextField
                //     {
                //         Name = nameof(CatalogItem.Manufacturer).ToCamelCase(),
                //         Label = "Manufacturer",
                //     });
                // }

                // if (!filters.ContainsKey(nameof(CatalogItem.CollectionName)))
                // {
                //     fields.Add(new TextField
                //     {
                //         Name = nameof(CatalogItem.CollectionName).ToCamelCase(),
                //         Label = "Collection",
                //     });
                // }

                // if (!filters.ContainsKey(nameof(CatalogItem.StyleName)))
                // {
                //     fields.Add(new TextField
                //     {
                //         Name = nameof(CatalogItem.StyleName).ToCamelCase(),
                //         Label = "Style",
                //     });
                // }

                // if (!filters.ContainsKey(nameof(CatalogItem.ColorName)))
                // {
                //     fields.Add(new TextField
                //     {
                //         Name = nameof(CatalogItem.ColorName).ToCamelCase(),
                //         Label = "Color",
                //     });
                // }

                // fields.Add(new TextField
                // {
                //     Name = nameof(CatalogItem.StyleNumber).ToCamelCase(),
                //     Label = "Style #"
                // });

                // fields.Add(new TextField
                // {
                //     Name = nameof(CatalogItem.ColorNumber).ToCamelCase(),
                //     Label = "Color #"
                // });
            }
        }

        /*
            [Authorize("admin")]
            [HttpPost("BetaUsers")]
            public async Task<IActionResult> AddBetaUserAsync()
            {
                var emails = new[]
                {
                    // "mark.mcmurray@fci.com.fcistaging",
                    // "jason.gange@fci.com.fcistaging",
                    // "dave.paeske@fci.com.fcistaging",
                    // "robert.greenlaw@fci.com.fcistaging",
                    // "paul.deal@fci.com.fcistaging",
                    "james.brooks@fci.com.fcistaging",
                    "Jocelyn.estrada@fci.com.fcistaging",
                    "Mike.killeen@fci.com.fcistaging"
                };

                foreach (var email in emails)
                {
                    await SeedAsync(email);
                }

                return Ok();
            }

            [Authorize("admin")]
            [HttpPost("BetaUser")]
            public async Task<IActionResult> AddBetaUserAsync(string email)
            {
                await SeedAsync(email);

                return Ok();
            }

            private async Task SeedAsync(string email)
            {
                var user = await _connection.Filter<Entity, User>()
                    .Eq(x => x.AccountId, Context.AccountId)
                    .Eq(x => x.UserRoleId, nameof(EntityRoleId.Manager))
                    .ElemMatch(x => x.Identities, y => y.EqIgnoreCase("Data.username", email))
                    .FirstOrDefaultAsync();

                if (user == null) throw new NotFoundException($"User not found with username: {email}");

                await SeedAsync(user.OrganizationId.Value);
            }

            [Authorize("admin")]
            [HttpPost("Seed")]
            public async Task<IActionResult> SeedAsync(Guid organizationId)
            {
                var org = await _connection.Filter<Entity, Organization>()
                    .Eq(x => x.AccountId, Context.AccountId.Value)
                    .Eq(x => x.Id, organizationId)
                    .FirstOrDefaultAsync();

                if (org == null) throw new NotFoundException(nameof(Organization), organizationId);

                var result1 = await _connection.DipperAggregateAsync(
                    "CopyItems",
                    "productCatalog",
                    new
                    {
                        AccountId = Context.AccountId.ToString(),
                        EntityId = organizationId.ToString(),
                    });

                var result2 = await _connection.DipperAsync(
                    "AddBreadcrumbs",
                    "productCatalog",
                    new
                    {
                        AccountId = Context.AccountId.ToString(),
                        EntityId = organizationId.ToString(),
                    });

                return Ok((result1, result2));
            }
        */
    }
}