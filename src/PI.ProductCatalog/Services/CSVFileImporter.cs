using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;
using PI.ProductCatalog.Models;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Services;
using static PI.Shared.Services.Flattener;

namespace PI.ProductCatalog.Services;

public class CSVFileImporter
{
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _objectTypeService;
    private ISpreadsheetFileParser _csv;

    public IEntityContext Context { get; private set; }
    public IFormFile File { get; private set; }
    public XLSCatalogFeed CatalogFeed { get; private set; }
    public EmailInbox Inbox { get; private set; }
    public EmailReceived EmailReceived { get; private set; }
    public Spreadsheet Spreadsheet { get; set; }
    public int[] ColOrder { get; private set; }
    public CSVFileConfig Config { get; private set; }

    public CSVFileImporter(
        MongoConnection connection,
        ObjectTypeService objectTypeService)
    {
        _connection = connection;
        _objectTypeService = objectTypeService;
    }

    public async Task<Result<Spreadsheet>> UploadFileAsync(IEntityContext context, IFormFile file, Guid catalogFeedId, Guid? formatId = null)
    {
        Context = context;
        File = file;

        CatalogFeed = await _connection.Filter<CatalogFeed, XLSCatalogFeed>(Context, false)
            .Eq(x => x.Id, catalogFeedId)
            .FirstOrDefaultAsync();

        if (CatalogFeed == null) return Result<Spreadsheet>.Error("Catalog feed not found");

        try
        {
            _csv = SpreadsheetFileParser.Create(file);
            if (_csv == null)
            {
                return Result<Spreadsheet>.Error("Unsupported file type");
            }

            if (!await GetInboxAsync(_csv.ColumnNames))
            {
                return Result<Spreadsheet>.Error("Format not recognized");
            }

            await CreateSpreadsheet();
            await ProcessAsync();
        }
        catch (Exception ex)
        {
            return Result<Spreadsheet>.Error($"Failed to load CSV file: {ex.Message}");
        }
        finally
        {
            _csv.Dispose();
            _csv = null;
        }

        // save 
        await _connection.InsertAsync(Spreadsheet);
        await _connection.InsertAsync(EmailReceived);

        // fire events
        await _objectTypeService.FireCreateEventAsync(Context, EmailReceived, x =>
        {
            x.Description ??= $"{x.ObjectType} Created";
            x.Action ??= "ObjectCreated";
            x.TryAddMetaValue(nameof(CatalogFeed), CatalogFeed.Name);
            x.AddRefValue(Inbox);
            x.AddRefValue(CatalogFeed);
        });

        await _objectTypeService.FireCreateEventAsync(Context, Spreadsheet, x =>
        {
            x.Description ??= $"{x.ObjectType} Created";
            x.Action ??= "ObjectCreated";
            x.TryAddMetaValue(nameof(CatalogFeed), CatalogFeed.Name);
            x.AddRefValue(nameof(EmailReceived), Spreadsheet.ParentId);
            x.AddRefValue(Inbox);
            x.AddRefValue(CatalogFeed);
        });

        return Result.Success(Spreadsheet);
    }

    private async Task ProcessAsync()
    {
        // TODO: reorder columns to match format
        // ...

        Spreadsheet.Columns = new Dictionary<string, string>();
        // for (var i = 0; i < _csv.ColumnNames.Length; i++)
        // {
        //     var key = "C" + i.ToString("000");
        //     Spreadsheet.Columns.Add(key, _csv.ColumnNames[i]);
        // }
        for (var i = 0; i < ColOrder.Length; i++)
        {
            var key = "C" + i.ToString("000");
            Spreadsheet.Columns.Add(key, Config.Fields[i].Config.Name);
        }

        var queue = new List<InsertOneModel<SpreadsheetRow>>();
        var count = 0;
        var index = 0;

        var rows = await _csv.GetRowsAsync();
        await foreach (var record in rows.ReadAllAsync())
        {
            index++;

            var row = new SpreadsheetRow
            {
                Id = Model.NewObjectId(),
                AccountId = Spreadsheet.AccountId,
                EntityId = Spreadsheet.EntityId,
                ParentId = Spreadsheet.Id,
                Columns = new Dictionary<string, object>(),
                CreatedOn = DateTime.UtcNow,
                Row = ++count,
                LastActor = Context.Actor(),
            };

            for (var i = 0; i < ColOrder.Length; i++)
            {
                int srcColIndex = ColOrder[i];
                if (srcColIndex < 0)
                {
                    // column is not required and was not provided
                    continue;
                }

                var key = "C" + i.ToString("000");
                row.Columns.Add(key, record[srcColIndex]);
            }

            queue.Add(new InsertOneModel<SpreadsheetRow>(row));
            if (queue.Count > 100)
            {
                var batch = await _connection.BulkWriteAsync(queue);
                Spreadsheet.RowsCount += (int)batch.InsertedCount;
                queue.Clear();
            }
        }

        if (queue.Count > 0)
        {
            var batch = await _connection.BulkWriteAsync(queue);
            Spreadsheet.RowsCount += (int)batch.InsertedCount;
            queue.Clear();
        }
    }

    private async Task CreateSpreadsheet()
    {
        EmailReceived = await _objectTypeService.CreateObjectAsync<EmailReceived>(Context);
        EmailReceived.Name = File.FileName;
        EmailReceived.AccountId = CatalogFeed.AccountId;
        EmailReceived.EntityId = CatalogFeed.EntityId;
        EmailReceived.ParentId = Inbox.Id;
        EmailReceived.LastActor = Context.Actor();
        if (Inbox.InitialObjectFlowId.TryGetValue(nameof(Shared.Models.EmailReceived), out var flowId)) EmailReceived.FlowId = flowId;
        if (Inbox.InitialObjectStatusId.TryGetValue(nameof(Shared.Models.EmailReceived), out var statusId)) EmailReceived.ObjectStatusId = statusId;

        Spreadsheet = await _objectTypeService.CreateObjectAsync<Spreadsheet>(Context);
        Spreadsheet.EntityId = EmailReceived.EntityId;
        Spreadsheet.AccountId = EmailReceived.AccountId;
        Spreadsheet.Name = EmailReceived.Name;
        Spreadsheet.ParentId = EmailReceived.Id;
        Spreadsheet.LastActor = Context.Actor();
        if (Inbox.InitialObjectFlowId.TryGetValue(nameof(Shared.Models.Spreadsheet), out flowId)) Spreadsheet.FlowId = flowId;
        if (Inbox.InitialObjectStatusId.TryGetValue(nameof(Shared.Models.Spreadsheet), out statusId)) Spreadsheet.ObjectStatusId = statusId;
    }

    private async Task<bool> GetInboxAsync(string[] columnNames)
    {
        Inbox = await _connection.Filter<EmailInbox>()
            .Eq(x => x.Id, CatalogFeed.EmailInboxId)
            .FirstOrDefaultAsync();

        if (Inbox == null)
        {
            Inbox = await CreateInboxAsync();
            if (Inbox == null) return false;
        }

        // TODO: try to find format based on columns
        if (!FindConfig(columnNames))
        {
            Inbox = null;
            return false;
        }

        // override flow/status
        Inbox.InitialObjectStatusId = Config.Inbox.InitialObjectStatusId;
        Inbox.InitialObjectFlowId = Config.Inbox.InitialObjectFlowId;

        return true;
    }

    private async Task<EmailInbox> CreateInboxAsync()
    {
        var template = await _connection.Filter<EmailInbox>()
            .Eq(x => x.AccountId, Context.AccountId.Value)
            .Eq(x => x.Name, "ACCOUNT_TEMPLATE")
            .Eq(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (template == null)
        {
            // _logger.LogError(request, "No template available for account");
            return null;
        }

        template.Id = Model.NewGuid();
        template.Name = CatalogFeed.Name;
        template.Description = null;
        template.CreatedOn = DateTime.UtcNow;
        template.LastActor = Context.Actor();
        template.LastModifiedOn = null;
        template.EntityId = CatalogFeed.EntityId;

        template = await _objectTypeService.InsertAsync(Context, template);

        CatalogFeed = await _connection.Filter<XLSCatalogFeed>()
            .Eq(x => x.Id, CatalogFeed.Id)
            .Update
            .Set(x => x.EmailInboxId, template.Id)
            .UpdateAndGetOneAsync();

        if (CatalogFeed == null) return null;
        return template;
    }

    private bool FindConfig(string[] columnNames)
    {
        foreach (var config in GetConfigs())
        {
            var cols = columnNames.Select(x => x.Replace(" ", "").ToLowerInvariant()).ToList();
            var colIndex = new List<int>();
            for (var c = 0; c < config.Fields.Length; c++)
            {
                var field = config.Fields[c];
                var name = (field.Config.Source ?? field.Config.Name).Replace(" ", "").ToLowerInvariant();
                var index = cols.IndexOf(name);
                if (index < 0)
                {
                    if (field.Config.IsRequired)
                    {
                        // didn't find required column -> not a valid config
                        colIndex = null;
                        break;
                    }
                }
                else
                {
                    // when the field is not required we may add -1 as the index
                    cols[index] = null;
                    colIndex.Add(index);
                }
            }

            if (colIndex != null)
            {
                // found switable config
                ColOrder = colIndex.ToArray();
                Config = config;
                return true;
            }
        }

        return false;
    }

    private IEnumerable<CSVFileConfig> GetConfigs()
    {
        yield return new CSVFileConfig
        {
            AccountId = Context.AccountId.Value,
            EntityId = Context.AccountId.Value,
            Name = "StandardFormat",
            Description = "PLM standard format",
            Fields = new[]
            {
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "PRODUCT",
                        Type = FIELDTYPE.Text,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "SUPPLIER",
                        Type = FIELDTYPE.Text,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "STYLE or SKU",
                        Type = FIELDTYPE.Text,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "STYLE or ITEM NAME",
                        Type = FIELDTYPE.Text,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "UOM",
                        Type = FIELDTYPE.Text,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "UNIT COST: CUT or CARTON",
                        Type = FIELDTYPE.Number,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "UNIT COST: ROLL or PALLET",
                        Type = FIELDTYPE.Number,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "GROSS MARGIN",
                        Type = FIELDTYPE.Number,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "ROLL LENGTH (FT)",
                        Type = FIELDTYPE.Number,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "ROLL WIDTH(FT)",
                        Type = FIELDTYPE.Number,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "UNITS PER CARTON/BOX",
                        Type = FIELDTYPE.Number,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "CARTONS PER PALLET",
                        Type = FIELDTYPE.Number,
                        IsRequired = true,
                    },
                },
            },
            Inbox = new EmailInbox
            {
                InitialObjectFlowId = new Dictionary<string, Guid>
                {
                    { nameof(EmailReceived), Guid.Parse("0f80c711-1c85-4a96-951c-64eedd6dd45a") },
                    { nameof(Spreadsheet), Guid.Parse("63347cee-af36-44eb-a8c5-9864b725871a") }
                },
                InitialObjectStatusId = new Dictionary<string, Guid>
                {
                    { nameof(EmailReceived), Guid.Parse("febd7b77-6ebf-47e9-805b-5915faa32cef") },
                    { nameof(Spreadsheet),  Guid.Parse("fb8cc92e-f9e7-4674-9360-e768bfce299b") }
                }
            }
        };

        yield return new CSVFileConfig
        {
            AccountId = Context.AccountId.Value,
            EntityId = Context.AccountId.Value,
            Name = "StandardWithStyle",
            Description = "Standard with Style",
            Fields = new[]
            {
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "PRODUCT",
                        Type = FIELDTYPE.Text,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "SUPPLIER",
                        Type = FIELDTYPE.Text,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "SKU",
                        Type = FIELDTYPE.Text,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "ITEM NAME",
                        Type = FIELDTYPE.Text,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "STYLE#",
                        Type = FIELDTYPE.Text,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "STYLE NAME",
                        Type = FIELDTYPE.Text,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "UOM",
                        Type = FIELDTYPE.Text,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "UNIT COST: CUT or CARTON",
                        Type = FIELDTYPE.Number,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "UNIT COST: ROLL or PALLET",
                        Type = FIELDTYPE.Number,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "GROSS MARGIN",
                        Type = FIELDTYPE.Number,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "ROLL LENGTH (FT)",
                        Type = FIELDTYPE.Number,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "ROLL WIDTH(FT)",
                        Type = FIELDTYPE.Number,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "UNITS PER CARTON/BOX",
                        Type = FIELDTYPE.Number,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "CARTONS PER PALLET",
                        Type = FIELDTYPE.Number,
                        IsRequired = true,
                    },
                },
            },
            Inbox = new EmailInbox
            {
                InitialObjectFlowId = new Dictionary<string, Guid>
                {
                    { nameof(EmailReceived), Guid.Parse("0f80c711-1c85-4a96-951c-64eedd6dd45a") },
                    { nameof(Spreadsheet), Guid.Parse("83a577f4-bcc5-4d1d-90df-3432dcfff7e7") }
                },
                InitialObjectStatusId = new Dictionary<string, Guid>
                {
                    { nameof(EmailReceived), Guid.Parse("febd7b77-6ebf-47e9-805b-5915faa32cef") },
                    { nameof(Spreadsheet),  Guid.Parse("fb8cc92e-f9e7-4674-9360-e768bfce299b") }
                }
            }
        };

        yield return new CSVFileConfig
        {
            AccountId = Context.AccountId.Value,
            EntityId = Context.AccountId.Value,
            Name = "Drop",
            Description = "Drop any non empty",
            Fields = new[]
            {
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "STYLE or SKU",
                        Type = FIELDTYPE.Text,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "DROP",
                        Type = FIELDTYPE.Text,
                        IsRequired = true,
                    },
                }
            },
            Inbox = new EmailInbox
            {
                InitialObjectFlowId = new Dictionary<string, Guid>
                {
                    { nameof(EmailReceived), Guid.Parse("0f80c711-1c85-4a96-951c-64eedd6dd45a") },
                    { nameof(Spreadsheet), Guid.Parse("1e8f297d-c88f-46db-8fdc-9c894ab2e58b") }
                },
                InitialObjectStatusId = new Dictionary<string, Guid>
                {
                    { nameof(EmailReceived), Guid.Parse("febd7b77-6ebf-47e9-805b-5915faa32cef") },
                    { nameof(Spreadsheet),  Guid.Parse("fb8cc92e-f9e7-4674-9360-e768bfce299b") }
                }
            }
        };

        yield return new CSVFileConfig
        {
            AccountId = Context.AccountId.Value,
            EntityId = Context.AccountId.Value,
            Name = "Drop Date",
            Description = "Define drop date",
            Fields = new[]
            {
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "STYLE or SKU",
                        Type = FIELDTYPE.Text,
                        IsRequired = true,
                    },
                },
                new Field
                {
                    Config =new FieldMapperConfig
                    {
                        Name = "DROP DATE",
                        Type = FIELDTYPE.Date,
                        IsRequired = true,
                    },
                }
            },
            Inbox = new EmailInbox
            {
                InitialObjectFlowId = new Dictionary<string, Guid>
                {
                    { nameof(EmailReceived), Guid.Parse("0f80c711-1c85-4a96-951c-64eedd6dd45a") },
                    { nameof(Spreadsheet), Guid.Parse("baa08aca-3b3b-476b-8804-8cb37410c296") }
                },
                InitialObjectStatusId = new Dictionary<string, Guid>
                {
                    { nameof(EmailReceived), Guid.Parse("febd7b77-6ebf-47e9-805b-5915faa32cef") },
                    { nameof(Spreadsheet),  Guid.Parse("fb8cc92e-f9e7-4674-9360-e768bfce299b") }
                }
            }
        };
    }
}

public class CSVFileConfig : EntityOwnedModel
{
    public Field[] Fields { get; set; }
    public EmailInbox Inbox { get; set; }
}