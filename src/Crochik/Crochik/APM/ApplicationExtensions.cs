// using System;
// using System.Collections.Generic;
// using Elastic.Apm;
// using Elastic.Apm.Api;
// using Elastic.Apm.AspNetCore;
// using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
//
namespace Crochik.NET.APM;
//
// public interface IAPMTransaction : IDisposable
// {
//     object Context { set; }
// }
//
// public interface IAPMService
// {
//     IAPMTransaction StartTransaction(string type, string name, string subType = null, string action = null);
//     void SetResult(string result);
// }
//
// public class Noop : IAPMTransaction
// {
//     public object Context { set { } }
//
//     public void Dispose() { }
// }
//
// public class Transaction : IAPMTransaction
// {
//     public bool AutoEnd { get; }
//     public ITransaction ElasticTransaction { get; }
//     public ISpan ElasticSpan { get; }
//
//     public object Context
//     {
//         set
//         {
//             if (value == null) return;
//
//             if (value is IDictionary<string, object> dict)
//             {
//                 foreach (var item in dict)
//                 {
//                     if (item.Value == null) continue;
//
//                     if (ElasticSpan == null && ElasticTransaction != null && string.Equals(item.Key, "Result", StringComparison.InvariantCultureIgnoreCase))
//                     {
//                         ElasticTransaction.Result = item.Value.ToString();
//                         continue;
//                     }
//
//                     ElasticSpan?.SetLabel(item.Key, item.Value.ToString());
//                     ElasticSpan?.SetLabel(item.Key, item.Value.ToString());
//                 }
//                 return;
//             }
//
//             foreach (var prop in value.GetType().GetProperties())
//             {
//                 var propValue = prop.GetValue(value);
//                 if (propValue == null) continue;
//
//                 if (ElasticSpan == null && ElasticTransaction != null && string.Equals(prop.Name, "Result", StringComparison.InvariantCultureIgnoreCase))
//                 {
//                     ElasticTransaction.Result = propValue.ToString();
//                     continue;
//                 }
//
//                 ElasticSpan?.SetLabel(prop.Name, propValue.ToString());
//                 ElasticSpan?.SetLabel(prop.Name, propValue.ToString());
//             }
//         }
//     }
//
//     public Transaction(string transactionType, string transactionName, string subType, string action)
//     {
//         AutoEnd = Agent.Tracer.CurrentTransaction == null;
//         if (AutoEnd)
//         {
//             ElasticTransaction = Agent.Tracer.StartTransaction(transactionName, transactionType);
//         }
//         else
//         {
//             ElasticSpan = Agent.Tracer.CurrentTransaction.StartSpan(transactionName, transactionType, subType, action);
//         }
//     }
//
//     public void Dispose()
//     {
//         ElasticSpan?.End();
//
//         if (AutoEnd)
//         {
//             ElasticTransaction?.End();
//         }
//     }
// }
//
// public class NOOPApmService : IAPMService
// {
//     private readonly IAPMTransaction _noop = new Noop();
//
//     public void SetResult(string result) { }
//
//     public IAPMTransaction StartTransaction(string type, string name, string subType = null, string action = null) => _noop;
// }
//
// public class ElastAPMService : IAPMService
// {
//     private readonly bool _enabled;
//     private readonly IAPMTransaction _noop = new Noop();
//
//     public ElastAPMService(IConfiguration configuration)
//     {
//         _enabled = configuration.IsApmEnabled();
//     }
//
//     public void SetResult(string result)
//     {
//         if (_enabled && Agent.Tracer.CurrentTransaction != null)
//         {
//             Agent.Tracer.CurrentTransaction.Result = result;
//         }
//     }
//
//     public IAPMTransaction StartTransaction(string type, string name, string subType = null, string action = null)
//     {
//         return _enabled ? new Transaction(type, name, subType, action) : _noop;
//     }
// }
//
// public static class ApplicationExtensions
// {
//     public static IApplicationBuilder UseAPM(this IApplicationBuilder app, IConfiguration configuration)
//     {
//         if (!configuration.IsApmEnabled())
//         {
//             System.Console.WriteLine("Elastic APM not configured...");
//         }
//         else
//         {
//             System.Console.WriteLine($"Use Elastic APM");
//             app.UseElasticApm(configuration, new Elastic.Apm.DiagnosticSource.HttpDiagnosticsSubscriber());
//         }
//
//         return app;
//     }
// }

public static class APMConfigurationExtensions
{
    public static bool IsApmEnabled(this IConfiguration configuration) => false;
    // public static bool IsApmEnabled(this IConfiguration configuration)
    // {
    //     var secretToken = configuration.GetValue<string>("ElasticApm:SecretToken", null);
    //     var serverUrls = configuration.GetValue<string>("ElasticApm:ServerUrls", null);
    //     return serverUrls != null && secretToken != null;
    // }
}