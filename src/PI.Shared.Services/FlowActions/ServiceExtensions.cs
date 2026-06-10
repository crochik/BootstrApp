using FlowActions;
using PI.Shared.Services.ActionRunners;

namespace Microsoft.Extensions.DependencyInjection;

public static class IServiceCollectionExtensions
{
    /// <summary>
    /// Add "standard flow action builders"
    /// - needed for the flow builder; AND...
    /// - for the ActionRunnerService in order to generate the actions events directly
    /// </summary>
    public static IServiceCollection AddFlowActionBuilders(this IServiceCollection services)
    {
        // services.AddSingleton<IFlowActionBuilder, ExportAppointmentToInspireNetActionBuilder>();
        // services.AddSingleton<IFlowActionBuilder, ExportAppointmentToOffice365ActionBuilder>();
        // services.AddSingleton<IFlowActionBuilder, ExportToInspireNetActionBuilder>();
        // services.AddSingleton<IFlowActionBuilder, ExportToLuminActionBuilder>();
        // services.AddSingleton<IFlowActionBuilder, ExportToSendGridActionBuilder>();
        // services.AddSingleton<IFlowActionBuilder, LoadAppointmentActionBuilder>();
        // services.AddSingleton<IFlowActionBuilder, PostLeadToSlackActionBuilder>();
        // services.AddSingleton<IFlowActionBuilder, SendEmailWithMarketingCloudActionBuilder>();
        // services.AddSingleton<IFlowActionBuilder, SendLeadEmailWithSendGridActionBuilder>();
        // services.AddSingleton<IFlowActionBuilder, UpdateIntegrationForAppointmentActionBuilder>();
        // services.AddSingleton<IFlowActionBuilder, UpdateIntegrationForLeadActionBuilder>();
        // services.AddSingleton<IFlowActionBuilder, WeightLoadBalanceActionBuilder>();

        // for multiple object types
        services.AddSingleton<IFlowActionBuilder, DelayActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, PostToSlackActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, PostToGoogleActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, SendSMSActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, SendEmailWithSendGridActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, SetObjectStatusActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, LoadRelatedObjectActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, ConditionalActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, SwitchActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, OpenApiOperationActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, CreateObjectUsingFormActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, GetUserInputActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, CreateInvoiceActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, SendNotificationActionBuilder>();
        // services.AddSingleton<IFlowActionBuilder, IterateViewActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, TagObjectActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, StartFlowActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, HttpCallOutActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, ExtractDataToFileActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, FireEventActionBuilder>();

        // lead
        services.AddSingleton<IFlowActionBuilder, AssignActionBuilder>();
        // services.AddSingleton<IFlowActionBuilder, AssignFlowActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, ExportToIntegrationActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, DuplicatedLeadCheckBuilder>();

        // appt
        services.AddSingleton<IFlowActionBuilder, AssignOnAppointmentActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, CreateICalActionBuilder>();

        // lead or appt
        services.AddSingleton<IFlowActionBuilder, ExportToSalesforceActionBuilder>();

        // org
        services.AddSingleton<IFlowActionBuilder, AutoRefillBalanceActionBuilder>();

        // account
        services.AddSingleton<IFlowActionBuilder, RunSingerSyncActionBuilder>();

        // catalogfeed
        services.AddSingleton<IFlowActionBuilder, SyncCatalogFeedActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, SpreadsheetToCatalogActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, BootstrapProductCatalogActionBuilder>();

        services.AddSingleton<IFlowActionBuilder, RunStoredProcedureActionBuilder>();

        // snapshot
        services.AddSingleton<IFlowActionBuilder, TakeSnapshotActionBuilder>();
        services.AddSingleton<IFlowActionBuilder, SendGridBulkEmailActionBuilder>();

        // sf_INET_Option__c
        services.AddSingleton<IFlowActionBuilder, GenerateQbFileActionBuilder>();

        // RemoteFile
        services.AddSingleton<IFlowActionBuilder, CopyFileActionBuilder>();

        // ImportObjectsJob
        services.AddSingleton<IFlowActionBuilder, ImportObjectsActionBuilder>();

        return services;
    }

    public static IServiceCollection AddRunner<T>(this IServiceCollection services) where T : class, IActionRunner
    {
        services.AddSingleton<T>();
        services.AddSingleton<IActionRunner, T>();

        return services;
    }
}