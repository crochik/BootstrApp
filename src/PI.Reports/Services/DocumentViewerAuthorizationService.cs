using System;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.UI;
using DevExpress.XtraReports.Web.ClientControls;
using DevExpress.XtraReports.Web.WebDocumentViewer;
using Microsoft.AspNetCore.Http;
using PI.Shared.Models;

namespace Reports.Services;

public class DocumentViewerAuthorizationService : WebDocumentViewerOperationLogger, IWebDocumentViewerAuthorizationService, IExportingAuthorizationService
{
    private readonly IHttpContextAccessor _contextAccessor;

    private IEntityContext Context => _contextAccessor.HttpContext.GetContextWithActor();

    public DocumentViewerAuthorizationService(
        IHttpContextAccessor contextAccessor
    )
    {
        _contextAccessor = contextAccessor;
    }

    public bool CanCreateDocument() => Context != null;
    public bool CanCreateReport() => Context != null;

    public override void ReportOpening(string reportId, string documentId, XtraReport report)
    {
        var context = Context;
        base.ReportOpening(reportId, documentId, report);
    }

    public override void BuildStarted(string reportId, string documentId, ReportBuildProperties buildProperties)
    {
        var context = Context;
        base.BuildStarted(reportId, documentId, buildProperties);
    }

    public override ExportedDocument ExportDocumentStarting(string documentId, string asyncExportOperationId, string format, ExportOptions options, PrintingSystemBase printingSystem, Func<ExportedDocument> doExportSynchronously)
    {
        var context = Context;
        return base.ExportDocumentStarting(documentId, asyncExportOperationId, format, options, printingSystem, doExportSynchronously);
    }

    public bool CanReadDocument(string documentId)
    {
        var context = Context;
        return context != null;
    }

    public bool CanReadReport(string reportId)
    {
        var context = Context;
        return context != null;
    }

    public bool CanReleaseDocument(string documentId)
    {
        return true;
    }

    public bool CanReleaseReport(string reportId)
    {
        return true;
    }

    public bool CanReadExportedDocument(string exportedDocumentId)
    {
        return Context != null;
    }
}