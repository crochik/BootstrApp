using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using Crochik.Mongo;
using DevExpress.DashboardCommon;
using DevExpress.DashboardCommon.Native;
using DevExpress.DashboardWeb;
using DevExpress.Data;
using DevExpress.DataAccess.ConnectionParameters;
using DevExpress.DataAccess.Json;
using DevExpress.DataAccess.MongoDB;
using DevExpress.DataAccess.Sql;
using DevExpress.DataAccess.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PI.ProductCatalog.Postgres;
using PI.Shared.Models;
using PI.Shared.Models.Dashboards;
using DataSource = PI.Shared.Models.Dashboards.DataSource;

namespace Reports.Dashboards;

public interface IDashboardService
{
    DashboardConfigurator GetConfigurator();
}

public class DashboardService : IDashboardService, IDataSourceStorage, IEditableDashboardStorage, IDataSourceWizardConnectionStringsProvider, IObjectDataSourceCustomFillService // , IDBSchemaProvider
{
    private const string DashboardDataSourceId = "Dashboard";
    private const string UserDataSourceId = "User";

    private HttpContext HttpContext => _contextAccessor.HttpContext;
    private IContextWithActor Context => HttpContext.GetContextWithActor();
    private readonly ILogger<DashboardService> _logger;
    private readonly IHttpContextAccessor _contextAccessor;
    private readonly MongoConnection _mongoConnection;
    private readonly PostgresConnection _postgresConnection;
    private readonly DashboardConfigurator _configurator;

    public DashboardService(
        ILogger<DashboardService> logger,
        IHttpContextAccessor contextAccessor,
        MongoConnection mongoConnection,
        PostgresConnection postgresConnection)
    {
        _logger = logger;
        _contextAccessor = contextAccessor;
        _mongoConnection = mongoConnection;
        _postgresConnection = postgresConnection;

        _configurator = new Configurator();
        _configurator.SetDashboardStorage(this);
        _configurator.SetDataSourceStorage(this);
        // _configurator.SetConnectionStringsProvider(this);
        _configurator.SetObjectDataSourceCustomFillService(this);
        // _configurator.SetDBSchemaProvider(this); //sql only?

        _configurator.ConfigureDataConnection += ConfigureDataConnection;
        // _configurator.CustomParameters += CustomParameters;
        _configurator.DataLoading += DataLoading;
        // _configurator.CustomFilterExpression += CustomFilterExpression;
    }

    public DashboardConfigurator GetConfigurator()
    {
        return _configurator;
    }

    private void ConfigureDataConnection(object sender, ConfigureDataConnectionWebEventArgs e)
    {
        _logger.LogInformation("ConfigureDataConnection");

        switch (e.ConnectionName)
        {
            case "MongoDbPI":
            {
                e.ConnectionParameters = new MongoDBCustomConnectionParameters(_mongoConnection.ConnectionString);
                return;
            }

            case "PostgresPI":
            {
                var config = _postgresConnection.Configuration.GetParameters();
                e.ConnectionParameters = new PostgreSqlConnectionParameters
                {
                    ServerName = config[PostgresConnection.PostgresConfiguration.HostParameter],
                    DatabaseName = config[PostgresConnection.PostgresConfiguration.DatabaseParameter],
                    UserName = config[PostgresConnection.PostgresConfiguration.UsernameParameter],
                    Password = config[PostgresConnection.PostgresConfiguration.PasswordParameter],
                };
                return;
            }
        }

        switch (e.DataSourceName)
        {
            case "[JSON]":
            {
                var remoteUri = new Uri("http://localhost:8080/api/v1/test");
                var jsonSource = new UriJsonSource(remoteUri);
                // if (userName == "User") {
                //     jsonSource.QueryParameters.AddRange(new[] {
                //         // "CountryPattern" is a dashboard parameter whose value is used for the "CountryStartsWith" query parameter
                //         new QueryParameter("CountryStartsWith", typeof(Expression), new Expression("Parameters.CountryPattern"))
                //     });
                // } else if (userName != "Admin") {
                //     throw new ApplicationException("You are not authorized to access JSON data.");
                // }
                ((JsonSourceConnectionParameters)e.ConnectionParameters).JsonSource = jsonSource;
                return;
            }

            case "[Mongo]":
            {
                e.ConnectionParameters = new MongoDBCustomConnectionParameters(_mongoConnection.ConnectionString);
                return;
            }

            case "[Postgres]":
            {
                var config = _postgresConnection.Configuration.GetParameters();
                e.ConnectionParameters = new PostgreSqlConnectionParameters
                {
                    ServerName = config[PostgresConnection.PostgresConfiguration.HostParameter],
                    DatabaseName = config[PostgresConnection.PostgresConfiguration.DatabaseParameter],
                    UserName = config[PostgresConnection.PostgresConfiguration.UsernameParameter],
                    Password = config[PostgresConnection.PostgresConfiguration.PasswordParameter],
                };
                return;
            }
        }

        _logger.LogError("Unexpected connection: {ConnectionName} {DataSourceName}", e.ConnectionName, e.DataSourceName);
    }

    private void CustomParameters(object sender, CustomParametersWebEventArgs e)
    {
        _logger.LogInformation("CustomParameters");
    }

    public object GetData(DashboardObjectDataSource dataSource, ObjectDataSourceFillParameters fillParameters)
    {
        _logger.LogInformation("GetData");

        var data = _mongoConnection.Filter<Lead>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Gt(x => x.CreatedOn, DateTime.UtcNow.AddDays(-180))
            .IncludeFields(
                x => x.Name,
                x => x.CreatedOn,
                x => x.LeadTypeId,
                x => x.ConvertedOn
            )
            .FindAsync()
            .Result;

        return data.Select(x => new Dictionary<string, object>()
        {
            { "Name", x.Name },
            { "CreatedOn", x.CreatedOn },
            // {}x.LeadTypeId,
            // Converted = x.ConvertedOn.HasValue
        });
    }

    private void DataLoading(object sender, DataLoadingWebEventArgs e)
    {
        _logger.LogInformation("DataLoading");

        var data = _mongoConnection.Filter<ExpandoObject>("ObjectStatus")
            .Eq("AccountId", Context.AccountId)
            .IncludeField("Name")
            .IncludeField("Description")
            .FindAsync()
            .Result;

        var list = new DynamicList("ObjectStatus");
        list.AddRange(data);

        e.Data = list;
    }

    private void DataLoading2(object sender, DataLoadingWebEventArgs e)
    {
        _logger.LogInformation("DataLoading");

        var data = _mongoConnection.Filter<Lead>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Gt(x => x.CreatedOn, DateTime.UtcNow.AddDays(-180))
            .IncludeFields(
                x => x.Name,
                x => x.CreatedOn,
                x => x.LeadTypeId,
                x => x.ConvertedOn
            )
            .FindAsync()
            .Result;

        e.Data = data.Select(x => new
        {
            x.Name,
            x.CreatedOn,
            x.LeadTypeId,
            Converted = x.ConvertedOn.HasValue
        });
    }

    private void CustomFilterExpression(object sender, CustomFilterExpressionWebEventArgs e)
    {
        _logger.LogInformation("CustomFilterExpression");
    }

    /// <summary>
    /// Wizard
    /// </summary>
    /// <returns></returns>
    public Dictionary<string, string> GetConnectionDescriptions()
    {
        _logger.LogInformation("GetConnectionDescriptions");

        return new Dictionary<string, string>
        {
            { "[Mongo]", "Mongo" },
            { "[Postgres]", "Postgres" },
        };
    }

    /// <summary>
    /// Wizard
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public DataConnectionParametersBase GetDataConnectionParameters(string name)
    {
        _logger.LogInformation("GetDataConnectionParameters: {Name}", name);
        // return new MongoDBCustomConnectionParameters();
        return null;
    }

    public IEnumerable<string> GetDataSourcesID()
    {
        // if ((bool)HttpContext.Request.Query?.ContainsKey("id"))
        // {
        //     // loading a specific dashboard
        //     yield return DashboardDataSourceId;
        //     yield break;
        // }

        // yield return "[Mongo]";
        yield return "[Postgres]";
        // yield return "[JSON]";
        // yield return UserDataSourceId;

        // var list = _mongoConnection.Filter<DataSource>()
        //     .Eq(x => x.AccountId, Context.AccountId)
        //     .Eq(x => x.IsActive, true)
        //     .FindAsync()
        //     .Result;
        //
        // foreach (var ds in list.Select(x => $"{x.GetType().Name}:{x.Group}").Distinct())
        // {
        //     yield return ds;
        // }
    }

    public XDocument GetDataSource(string dataSourceID)
    {
        _logger.LogInformation("GetDataSource: {DataSourceID}", dataSourceID);

        var parts = dataSourceID.Split(":");

        if (parts.Length == 2)
        {
            var group = GetPostgresDataSource(parts[1]);
            return new XDocument(group.SaveToXml());
        }

        var ds = dataSourceID switch
        {
            "[JSON]" => new DashboardJsonDataSource("[JSON]")
            {
                RootElement = "Customers",
                ConnectionName = "jsonCustomers"
            },
            "[Mongo]" => new DashboardMongoDBDataSource("[Mongo]", "MongoDbPI")
            {
                Queries =
                {
                    new MongoDBQuery()
                    {
                        DatabaseName = "PI",
                        CollectionName = "ObjectStatus",
                        // Schema = new MongoDBSchemaNode
                        // {
                        //     Name = "ObjectStatus",
                        //     NodeType = MongoDBNodeType.Array,
                        //     Nodes =
                        //     {
                        //         new MongoDBSchemaNode
                        //         {
                        //             Name = "Name",
                        //             NodeType = MongoDBNodeType.Property,
                        //             Type = typeof(string)
                        //         },
                        //         new MongoDBSchemaNode
                        //         {
                        //             Name = "Description",
                        //             NodeType = MongoDBNodeType.Property,
                        //             Type = typeof(string)
                        //         }
                        //     }
                        // }
                    }
                }
            },
            "[Postgres]" => GetPostgresDataSource(),
            DashboardDataSourceId => GetDataSourceForDashboard(),
            UserDataSourceId => GetDataSourceForUser(),
            _ => throw new ApplicationException("Unexpected data source"),
        };

        return new XDocument(ds.SaveToXml());
    }

    private DashboardSqlDataSource GetPostgresDataSource2(string group = null)
    {
        var query = _mongoConnection.Filter<DataSource, PostgresDataSource>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Ne(x => x.IsActive, false);

        if (group != null)
        {
            query.Eq(x => x.Group, group)
                .SortAsc(x => x.Name)
                ;
        }
        
        var list = query.Find();

        var queries = list.Select(x =>
            SelectQueryFluentBuilder.AddTable(x.TableName).SelectAllColumnsFromTable().Build(x.Name)
        );

        var ds = new DashboardSqlDataSource(group ?? "[Postgres]", "PostgresPI");
        ds.Queries.AddRange(queries);
        // ds.Relations.Add("Organization", "User", "_id", "OrganizationId");

        var sql = "SELECT tablename FROM pg_catalog.pg_tables WHERE schemaname != 'pg_catalog' AND schemaname != 'information_schema'";            
            
        return ds;
    }

    private DashboardSqlDataSource GetPostgresDataSource(string group = null)
    {
        var queries = getTableNames().ToArray().Select(x =>
            SelectQueryFluentBuilder.AddTable(x).SelectAllColumnsFromTable().Build(x)
        );
        
        var ds = new DashboardSqlDataSource(group ?? "[Postgres]", "PostgresPI");
        ds.DataProcessingMode = DataProcessingMode.Server;
        ds.Queries.AddRange(queries);
        // ds.Relations.Add("Organization", "User", "_id", "OrganizationId");
        
        return ds;

        IEnumerable<string> getTableNames()
        {
            var sql = "SELECT tablename FROM pg_catalog.pg_tables WHERE schemaname != 'pg_catalog' AND schemaname != 'information_schema'";
            sql += " UNION ";
            sql += "select table_name from INFORMATION_SCHEMA.views WHERE table_schema = ANY (current_schemas(false))";
                
            using var rs = _postgresConnection.DataSource.CreateCommand(sql).ExecuteReader();
            while (rs.Read())
            {
                yield return rs.GetString(0);
            }
            rs.Close();
        }
    }

    private IDashboardDataSource GetDataSourceForDashboard()
    {
        return null;
    }

    private IDashboardDataSource GetDataSourceForUser()
    {
        // var ds = new DashboardObjectDataSource("PI Leads");
        // ds.DataSource = typeof(Lead);
        // // ds.Parameters.Add(new DevExpress.DataAccess.ObjectBinding.Parameter("Date", typeof(DateTime), DateTime.UtcNow.Date));
        // return new XDocument(ds.SaveToXml());

        DashboardObjectDataSource objDataSource = new DashboardObjectDataSource(UserDataSourceId); //, typeof(Lead));
        // objDataSource.DataSource = typeof(SalesPersonData);
        return objDataSource;
    }

    public IEnumerable<DashboardInfo> GetAvailableDashboardsInfo()
    {
        var model = _mongoConnection.Filter<PI.Shared.Models.Dashboards.Dashboard>()
            .Eq(x => x.AccountId, Context.AccountId)
            .FindAsync()
            .Result;

        return model.Select(x => new DashboardInfo
        {
            ID = x.Id.ToString(),
            Name = x.Name
        });
    }

    public XDocument LoadDashboard(string dashboardID)
    {
        var model = _mongoConnection.Filter<PI.Shared.Models.Dashboards.Dashboard>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, Guid.Parse(dashboardID))
            .FirstOrDefaultAsync()
            .Result;

        return XDocument.Parse(model.Xml);
    }

    public void SaveDashboard(string dashboardID, XDocument dashboard)
    {
        var title = dashboard.XPathSelectElement("/Dashboard/Title[1]")?.FirstAttribute?.Value;

        // TODO: save data source(s)
        // ...

        var model = _mongoConnection.Filter<PI.Shared.Models.Dashboards.Dashboard>()
            .Eq(x => x.AccountId, Context.AccountId)
            .Eq(x => x.Id, Guid.Parse(dashboardID))
            .Update
            .Set(x => x.Name, title)
            .Set(x => x.Xml, dashboard.ToString())
            .UpdateAndGetOneAsync()
            .Result;

        return;
    }

    public string AddDashboard(XDocument dashboard, string dashboardName)
    {
        // TODO: save data source(s)
        // ...

        var model = new PI.Shared.Models.Dashboards.Dashboard
        {
            AccountId = Context.AccountId.Value,
            EntityId = Context.UserId.Value,
            Id = Guid.NewGuid(),
            Name = dashboardName,
            CreatedOn = DateTime.UtcNow,
            Xml = dashboard.ToString(),
        };

        model = _mongoConnection.InsertAsync(model).Result;
        return model.Id.ToString();
    }

    // public DBSchema GetSchema(SqlDataConnection connection, SchemaLoadingMode schemaLoadingMode)
    // {
    //     var tb1 = new DBTable("User");
    //     var tb2 = new DBTable("Organization");
    //     return new DBSchema(new[] { tb1, tb2 }, Array.Empty<DBTable>());
    // }
    //
    // public void LoadColumns(SqlDataConnection connection, params DBTable[] tables)
    // {
    //     throw new NotImplementedException();
    // }
}

public class Configurator : DashboardConfigurator
{
    protected override DataLoadingResult RaiseDataLoading(string dashboardId, string dataSourceComponentName, string dataSourceName, string dataId, IEnumerable<IParameter> parameters)
    {
        try
        {
            var result = base.RaiseDataLoading(dashboardId, dataSourceComponentName, dataSourceName, dataId, parameters);
            return result;
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}

public class DynamicList : List<ExpandoObject>, ITypedList
{
    private readonly string _name;

    public DynamicList(string name)
    {
        _name = name;
    }

    public PropertyDescriptorCollection GetItemProperties(PropertyDescriptor[] listAccessors)
    {
        return new PropertyDescriptorCollection(properties().ToArray(), true);

        IEnumerable<PropertyDescriptor> properties()
        {
            yield return new ExpandoObjectPropertyDescriptor("Name", typeof(string));
            yield return new ExpandoObjectPropertyDescriptor("_id", typeof(string));
            yield return new ExpandoObjectPropertyDescriptor("Description", typeof(string));
        }
    }

    public string GetListName(PropertyDescriptor[] listAccessors) => _name;
}

class ExpandoObjectPropertyDescriptor : PropertyDescriptor
{
    public override Type ComponentType => null;
    public override bool IsReadOnly => true;
    public override Type PropertyType { get; }

    public ExpandoObjectPropertyDescriptor(string name, Type propertyType) : base(name, null)
    {
        PropertyType = propertyType;
    }

    public override bool CanResetValue(object component) => false;

    public override object GetValue(object component)
    {
        return component is IDictionary<string, object> dict && dict.TryGetValue(Name, out var value) ? value : null;
    }

    public override void ResetValue(object component)
    {
    }

    public override void SetValue(object component, object value)
    {
    }

    public override bool ShouldSerializeValue(object component) => true;
}