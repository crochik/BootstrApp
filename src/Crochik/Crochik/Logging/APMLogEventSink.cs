// using Elastic.Apm;
// using Serilog.Core;
// using Serilog.Events;
//
// namespace Crochik.Logging;
//
// public class APMLogEventSink : ILogEventSink
// {
//     public void Emit(LogEvent logEvent)
//     {
//         if (logEvent.Exception == null || logEvent.Level < LogEventLevel.Error)
//         {
//             return;
//         }
//
//         if (Agent.Tracer.CurrentTransaction == null)
//         {
//             return;
//         }
//
//         Agent.Tracer.CurrentTransaction.CaptureException(logEvent.Exception);
//     }
// }