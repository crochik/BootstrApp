using MCP.Services;
using McpServer.Services;
using McpServer.Tools;
using PI.Shared.Services.OpenApiGenerator;
using Serilog;

namespace MCP;

public class Program : AbstractMCPServer
{
    protected override string Name => "MCP";

    public static async Task<int> Main(string[] args)
    {
        Serilog.Debugging.SelfLog.Enable(Console.Error);

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Starting...");
            await new Program().RunWebApplication(args);
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
            await Console.Error.WriteLineAsync(ex.Message);
            Console.WriteLine(ex.ToString());
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    protected override void AddServices(IServiceCollection services)
    {
        base.AddServices(services);

        services
            .AddSingleton<SingleUseFileAccessService>()
            .AddSingleton<AccountManagementService>()
            .AddSingleton<BootstrAppService>()
            // ...
            .AddTransient<OpenApiParser>()
            .AddTransient<OpenApiSpecGenerator>()
            .AddTransient<ObjectTypeScriptPlugin>()
            ;
        
        services.AddMcpTools(tools =>
        {
            tools.AddToolType<ManageObjectTypeTools>();
            tools.AddToolType<OpenApiSpecTool>();
            tools.AddToolType<AppTools>();
        })
        .AddMcpResources()
        ;
    }
}