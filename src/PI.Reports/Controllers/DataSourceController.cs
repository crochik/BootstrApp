using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Dipper;
using Crochik.Mongo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using NpgsqlTypes;
using PI.Shared.Controllers;
using PI.Shared.Exceptions;
using PI.Shared.Form.Models;
using PI.Shared.Models;
using PI.Shared.Models.Dashboards;
using PI.Shared.Requests;
using PI.Shared.Services;
using ValueType = PI.Shared.Models.ValueType;

namespace Reports.Controllers;

[Authorize("admin")]
[Route("/reports/v1/DataSource")]
public class DataSourceController : APIController
{
    private readonly ILogger<DataSourceController> _logger;
    private readonly MongoConnection _connection;
    private readonly ObjectTypeService _service;

    public DataSourceController(ILogger<DataSourceController> logger, MongoConnection connection, ObjectTypeService service)
    {
        _logger = logger;
        _connection = connection;
        _service = service;
    }

    [HttpGet("ObjectType({objectTypeNameOrId})/Postgres/DataForm")]
    public async Task<Form> GetBuildDataSourceForObjectTypeAsync([FromRoute] string objectTypeNameOrId)
    {
        var objectType = Guid.TryParse(objectTypeNameOrId, out var objectTypeId) ? await _service.GetAsync(Context, objectTypeId) : await _service.GetAsync(Context, objectTypeNameOrId);

        if (objectType == null) throw NotFoundException.New(nameof(ObjectType));
        if (objectType.IsEmbedded) throw new BadRequestException("Can't create datasource for embedded object type");

        // TODO: add options
        // ...

        return new Form
        {
            Name = "Create",
            Title = "Create Data Source",
            Fields = new FormField[]
            {
                new HiddenField
                {
                    Name = nameof(ObjectType),
                    DefaultValue = objectType.FullName,
                },
                new LabelField
                {
                    Name = "Message",
                    Label = $"Create Datasource for {objectType.Description}?"
                }
            },
            Actions = new[]
            {
                new FormAction
                {
                    Name = "Create"
                }
            }
        };
    }

    [HttpPost("ObjectType({objectTypeNameOrId})/Postgres/DataForm")]
    public async Task<DataFormActionResponse> BuildDataSourceForObjectTypeAsync([FromRoute] string objectTypeNameOrId, [FromBody] DataFormActionRequest request)
    {
        var objectType = Guid.TryParse(objectTypeNameOrId, out var objectTypeId) ? await _service.GetAsync(Context, objectTypeId) : await _service.GetAsync(Context, objectTypeNameOrId);

        if (objectType == null) throw NotFoundException.New(nameof(ObjectType));
        if (objectType.IsEmbedded) throw new BadRequestException("Can't create datasource for embedded object type");

        var fields = GetValidFields(Context, objectType).ToArray();
        var ds = await BuildPostgresDataSourceForObjectTypeAsync(Context, objectType, fields);
        var result = Result.Success(ds);

        // TODO: next url to edit datasource 
        // ...

        return !result.IsSuccess ? new DataFormActionResponse(request, result.Status) : new DataFormActionResponse(request, "Data source created", true);
    }


    [HttpGet("AppDataView({id})/Postgres/DataForm")]
    public async Task<Form> GetBuildDataSourceForAppDataViewAsync([FromRoute] Guid id)
    {
        var appDataView = await _connection.Filter<AppDataView>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, id)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (appDataView == null) throw NotFoundException.New<AppDataView>(id);

        // TODO: add options
        // ...

        return new Form
        {
            Name = "Create",
            Title = "Create Data Source",
            Fields = new FormField[]
            {
                new LabelField
                {
                    Name = "Message",
                    Label = $"Create Datasource for {appDataView.Description ?? appDataView.Name}?"
                }
            },
            Actions = new[]
            {
                new FormAction
                {
                    Name = "Create"
                }
            }
        };
    }

    [HttpPost("AppDataView({id})/Postgres/DataForm")]
    public async Task<DataFormActionResponse> BuildDataSourceForAppDataViewAsync([FromRoute] Guid id, [FromBody] DataFormActionRequest request)
    {
        var appDataView = await _connection.Filter<AppDataView>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, id)
            .Ne(x => x.IsActive, false)
            .FirstOrDefaultAsync();

        if (appDataView == null) throw NotFoundException.New<AppDataView>(id);

        var ds = await BuildDataSourceForAppDataViewAsync(appDataView);

        return !ds.IsSuccess ? new DataFormActionResponse(request, ds.Status) : new DataFormActionResponse(request, "Data source created", true);
    }

    private async Task<Result<PostgresDataSource>> BuildDataSourceForAppDataViewAsync(AppDataView appDataView)
    {
        if (appDataView.StoredProcedure == null)
        {
            // view created for an object
            var objectType = await _service.GetAsync(Context, appDataView.ObjectType);
            var fields = appDataView.Fields
                .Select(x => objectType.Fields.TryGetValue(x, out var field) && field.RBAC.CanRead(Context) ? field.Field : null)
                .Where(x => x != null)
                .ToArray();

            var source = await BuildPostgresDataSource(Context, objectType, fields);
            await _connection.InsertAsync(source);
            return Result.Success(source);
        }

        appDataView.DataView.Fields = FilterValidFieldsForSql(Context, appDataView.DataView.Fields).ToArray();

        var postgres = new PostgresDataSource
        {
            Id = Guid.NewGuid(),
            AccountId = Context.AccountId.Value,
            Name = appDataView.Name,
            Description = appDataView.Description,
            TableName = appDataView.Name,
            Columns = appDataView.DataView.Fields
                // .Select(x => (x.Name, x.IsRequired ? $"{GetType(x)} NOT NULL" : GetType(x)))
                .Select(x => (x.Name, GetSqlColumn(x)))
                .ToDictionary(x => x.Name, x => x.Item2),
            IsActive = false,
            LoadSource = new MongoDbLoadSource
            {
                StoredProcedure = appDataView.StoredProcedure,
            }
        };

        await _connection.InsertAsync(postgres);

        return Result.Success(postgres);
    }

    private async Task<PostgresDataSource> BuildPostgresDataSourceForObjectTypeAsync(IEntityContext context, ObjectType objectType, FormField[] fields, bool expandReferences = false)
    {
        var allFields = !expandReferences
            ? fields
            : fields
                .Concat(
                    fields
                        .Where(x => x is ReferenceField)
                        .Select(x => new TextField
                        {
                            Name = $"{x.Name}|Name",
                            Label = x.Label ?? x.Name,
                        }))
                .ToArray();

        var postgres = await BuildPostgresDataSource(context, objectType, allFields);

        await _connection.InsertAsync(postgres);

        return postgres;
    }

    private async Task<PostgresDataSource> BuildPostgresDataSource(IEntityContext context, ObjectType objectType, FormField[] selectedFields)
    {
        var dataView = new DataView
        {
            Name = objectType.Name,
            KeyField = Model.IdFieldName,
            // DefaultSort = Model.IdFieldName,
            Fields = selectedFields,
        };

        var appDataView = new AppDataView
        {
            AccountId = context.AccountId.Value,
            Id = Guid.NewGuid(),
            CreatedOn = DateTime.UtcNow,
            ObjectType = objectType.Name,
            DataView = dataView,
        };

        var builder = AppDataViewPipelineBuilder
                .New(
                    _connection,
                    context,
                    appDataView,
                    objectType
                )
            // .WithExpandedReferences()
            ;

        var stages = builder.BuildPipeline();

        var storedProcedure = new AggregateStoredProcedure
        {
            Operation = AggregationOperation.Find,
            Collection = objectType.CollectionName,
            DatabaseName = objectType.DatabaseName,
            Name = objectType.Name,
            Description = $"{objectType.Name} (Autogenerated)",
            Pipeline = stages.Select(x => x.ToJson()).ToArray(),
        };

        var postgres = new PostgresDataSource
        {
            Id = Guid.NewGuid(),
            AccountId = context.AccountId.Value,
            Name = objectType.Name,
            Description = $"Copy of {objectType.Description}",
            TableName = objectType.Name,
            Columns = appDataView.DataView.Fields
                // .Select(x => (x.Name, x.IsRequired ? $"{GetType(x)} NOT NULL" : GetType(x)))
                .Select(x => (x.Name, GetSqlColumn(x)))
                .ToDictionary(x => x.Name, x => x.Item2),
            IsActive = false,
            LoadSource = new MongoDbLoadSource
            {
                StoredProcedure = storedProcedure,
                // DataView = dataView,
                // Mapping = appDataView.DataView.Fields
                //     .ToDictionary(x => x.Name.Replace("||", "__"), x => x.Name),
            }
        };

        return postgres;
    }

    private SqlColumn GetSqlColumn(FormField field)
    {
        // _id
        if (field.Name == Model.IdFieldName)
        {
            return new SqlColumn
            {
                Type = NpgsqlDbType.Varchar.ToString(),
                Size = 36,
                NotNull = true,
                Resolved = "VARCHAR(36) NOT NULL PRIMARY KEY",
            };
        }

        switch (field)
        {
            case ReferenceField:
                // for now assume "UUID"
                // TODO: check if it is name, GUID, ObjectId, ...
                // ...
                return new SqlColumn
                {
                    Type = NpgsqlDbType.Varchar.ToString(),
                    Size = 36,
                    NotNull = field.IsRequired,
                    Resolved = "VARCHAR(36)", // TODO: add "NOT NULL" conditionally
                };

            case SelectField:
                // big assumption here but ...
                return new SqlColumn
                {
                    Type = NpgsqlDbType.Varchar.ToString(),
                    Size = 255,
                    NotNull = field.IsRequired,
                    Resolved = "VARCHAR(255)", // TODO: add "NOT NULL" conditionally
                };

            case TextField textField:
            {
                if (textField.TextFieldOptions?.MaxLength != null)
                {
                    return new SqlColumn
                    {
                        Type = NpgsqlDbType.Varchar.ToString(),
                        Size = textField.TextFieldOptions.MaxLength,
                        NotNull = field.IsRequired,
                        Resolved = $"VARCHAR({textField.TextFieldOptions.MaxLength})", // TODO: add "NOT NULL" conditionally
                    };
                }

                break;
            }

            case ArrayField arrayField:
            {
                // TODO: check value field backing type
                // ...
                return new SqlColumn
                {
                    Type = NpgsqlDbType.Array.ToString(),
                    NotNull = field.IsRequired,
                    Resolved = "text ARRAY", // ???
                };
            }

            case TagsField:
                return new SqlColumn
                {
                    Type = NpgsqlDbType.Array.ToString(),
                    NotNull = field.IsRequired,
                    Resolved = "text ARRAY",
                };
        }

        var backingType = field.GetBackingType();
        if (backingType.IsArray || backingType.IsDictionary)
        {
            _logger.LogInformation("Array or dictionary, skip {Field}", field.Name);
            return null;
        }

        var type = backingType.ValueType switch
        {
            ValueType.String => backingType.Length.HasValue ? NpgsqlDbType.Varchar : NpgsqlDbType.Text,
            ValueType.DateTime => NpgsqlDbType.TimestampTz,
            ValueType.Boolean => NpgsqlDbType.Bit,
            ValueType.UUID => NpgsqlDbType.Varchar,
            ValueType.ObjectId => NpgsqlDbType.Varchar,
            ValueType.Decimal => NpgsqlDbType.Numeric,
            ValueType.Int => NpgsqlDbType.Integer,
            _ => NpgsqlDbType.Text,
        };

        var resolved = backingType.ValueType switch
        {
            ValueType.String => backingType.Length.HasValue ? $"VARCHAR({backingType.Length})" : "TEXT",
            ValueType.DateTime => "TIMESTAMP WITH TIME ZONE",
            ValueType.Boolean => "BIT(1)",
            ValueType.UUID => "VARCHAR(36)",
            ValueType.ObjectId => "VARCHAR(36)",
            ValueType.Decimal => "NUMERIC",
            ValueType.Int => "INTEGER",
            _ => "TEXT",
        };

        return new SqlColumn
        {
            Type = type.ToString(),
            NotNull = field.IsRequired,
            Resolved = resolved, // TODO: add "NOT NULL" conditionally
        };
    }

    private IEnumerable<FormField> FilterValidFieldsForSql(IContextWithActor context, IEnumerable<FormField> fields)
    {
        foreach (var field in fields)
        {
            if (GetSqlColumn(field) != null)
            {
                yield return field;
                continue;
            }

            _logger.LogInformation("Unsupported {Field}", field.Name);
        }
    }

    private IEnumerable<FormField> GetValidFields(IEntityContext context, ObjectType objectType)
    {
        foreach (var field in objectType.Fields.Values.Where(x => x.RBAC.CanRead(context)).Select(x => x.Field))
        {
            if (GetSqlColumn(field) != null)
            {
                yield return field;
                continue;
            }

            _logger.LogInformation("Unsupported {Field}", field.Name);
        }
    }
}