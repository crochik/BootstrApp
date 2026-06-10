using System;
using DevExpress.DashboardAspNetCore;
using DevExpress.DashboardCommon;
using DevExpress.DashboardWeb;
using DevExpress.DataAccess.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Reports.Dashboards;

public class SampleDashboardService : IDashboardService
{
    private readonly IConfiguration _configuration;
    private readonly IFileProvider _fileProvider;
    
    public SampleDashboardService(IHostEnvironment hostEnvironment, IConfiguration configuration)
    {
        _configuration = configuration;
        _fileProvider = hostEnvironment.ContentRootFileProvider;
    }
    
    public DashboardConfigurator GetConfigurator()
    {
        var configurator = new DashboardConfigurator();
        configurator.SetDashboardStorage(new DashboardFileStorage(_fileProvider.GetFileInfo("Data/Dashboards").PhysicalPath));
        configurator.SetDataSourceStorage(CreateDataSourceStorage());
        configurator.SetConnectionStringsProvider(new DashboardConnectionStringsProvider(_configuration));
        configurator.ConfigureDataConnection += Configurator_ConfigureDataConnection;
        return configurator;
        
    }

    public void Configurator_ConfigureDataConnection(object sender, ConfigureDataConnectionWebEventArgs e)
    {
        if (e.ConnectionName == "jsonSupport")
        {
            Uri fileUri = new Uri(_fileProvider.GetFileInfo("Data/support.json").PhysicalPath, UriKind.RelativeOrAbsolute);
            JsonSourceConnectionParameters jsonParams = new JsonSourceConnectionParameters();
            jsonParams.JsonSource = new UriJsonSource(fileUri);
            e.ConnectionParameters = jsonParams;
        }

        if (e.ConnectionName == "jsonCategories")
        {
            Uri fileUri = new Uri(_fileProvider.GetFileInfo("Data/categories.json").PhysicalPath, UriKind.RelativeOrAbsolute);
            JsonSourceConnectionParameters jsonParams = new JsonSourceConnectionParameters();
            jsonParams.JsonSource = new UriJsonSource(fileUri);
            e.ConnectionParameters = jsonParams;
        }
    }

    public DataSourceInMemoryStorage CreateDataSourceStorage()
    {
        // https://docs.devexpress.com/Dashboard/402924/web-dashboard/integrate-dashboard-component/dashboard-backend/manage-multi-tenancy
        // https://docs.devexpress.com/Dashboard/403108/web-dashboard/integrate-dashboard-component/dashboard-backend/prepare-data-source-storage-for-the-aspnet-mvc-framework/mongodb-data-source
        DataSourceInMemoryStorage dataSourceStorage = new DataSourceInMemoryStorage();

        var jsonDataSourceSupport = new DashboardJsonDataSource("Support");
        jsonDataSourceSupport.ConnectionName = "jsonSupport";
        jsonDataSourceSupport.RootElement = "Employee";
        dataSourceStorage.RegisterDataSource("jsonDataSourceSupport", jsonDataSourceSupport.SaveToXml());

        var jsonDataSourceCategories = new DashboardJsonDataSource("Categories");
        jsonDataSourceCategories.ConnectionName = "jsonCategories";
        jsonDataSourceCategories.RootElement = "Products";
        dataSourceStorage.RegisterDataSource("jsonDataSourceCategories", jsonDataSourceCategories.SaveToXml());
            
        return dataSourceStorage;
    }    
}