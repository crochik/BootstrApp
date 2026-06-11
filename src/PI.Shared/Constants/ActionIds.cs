using System;

namespace PI.Shared.Constants;

public class ActionIds
{
    public static readonly Guid AssignFlow = Guid.Parse("c72d736e-4cad-4917-8f82-2d2fd4e6be21");
    public static readonly Guid AssignLead = Guid.Parse("9994c188-7719-4322-bf75-165414d25ff1");
    public static readonly Guid AssignLeadOnAppointment = Guid.Parse("e4a642bb-3ffc-42fa-b7a9-f2f6e8125c27");
    public static readonly Guid ExportAppointmentToInspireNet = Guid.Parse("e45de252-b487-4966-83ae-8ae249496502");
    
    [Obsolete("use O365ExportAppointment")]
    public static readonly Guid ExportAppointmentToOffice365 = IntegrationIds.Office365;
    
    public static readonly Guid ExportLeadToInspireNet = IntegrationIds.InspireNet;
    public static readonly Guid ExportLeadToLumin = IntegrationIds.Lumin;
    
    public static readonly Guid ExportToIntegration = Guid.Parse("98659fb2-7751-4d16-b6d5-4297234e100a");
    public static readonly Guid ExportToSalesforce = IntegrationIds.Salesforce;
    public static readonly Guid ExportLeadToSendGrid = Guid.Parse("4d236429-19bc-4401-a34b-06339cde969d");
    public static readonly Guid PostLeadToSlackChannel = IntegrationIds.Slack;
    public static readonly Guid ScheduleGotoMeetingAppointment = IntegrationIds.GotoMeeting;
    public static readonly Guid ScheduleZoomAppointment = IntegrationIds.Zoom;
    public static readonly Guid SendLeadEmailMarketingCloud = Guid.Parse("01c22db4-722f-4890-9e51-f10aa816b702");
    public static readonly Guid SendLeadEmailSendgrid = Guid.Parse("923d00fd-2e80-45f9-9aff-8fe33b649bba");
    public static readonly Guid GooglePlacesSearch = Guid.Parse("79117459-7ca7-483a-91cf-49aa2adffd86");
    
    public static readonly Guid CompanyCamCreateProject = Guid.Parse("b42ae26a-d364-4871-824d-59ec36906f23");
    public static readonly Guid CompanyCamAddDocument = Guid.Parse("3e31414b-d4cc-4176-aa54-ebe23a935a6f");
    
    /// <summary>
    /// Get availability
    /// </summary>
    public static readonly Guid SchedulerAvailability = Guid.Parse("893c9488-9748-4217-9b76-1104bc96a993");
    
    /// <summary>
    /// New version of export appointment to o365
    /// </summary>
    public static readonly Guid O365ExportAppointment = Guid.Parse("6d094cf9-66f9-479c-b44f-f0d6ed4921dd");
    public static readonly Guid O365CancelAppointment = Guid.Parse("abcd6f11-cd8d-47a9-95a7-ba3c43698f74");
    
    // Lead and LMSTransaction
    public static readonly Guid DuplicatedLeadCheck = Guid.Parse("eb9fa352-528f-4b7e-a50f-984258e84923");

    [Obsolete("the integration exporting the data should update the object directly")]
    public static readonly Guid UpdateIntegrationForAppointment = Guid.Parse("5345c2b7-727a-4ead-a8ac-d53dbf1f9476");

    public static readonly Guid UpdateIntegrationForLead = Guid.Parse("5f6e1492-4639-4bef-bdcf-fba8f48e8d76");
    public static readonly Guid LoadAppointment = Guid.Parse("52f00484-9488-496d-b2a6-1592332a146c");
    public static readonly Guid WeightLoadBalance = Guid.Parse("137507c9-e303-4e36-adea-8c56d137196b");
    public static readonly Guid AutoRefillBalance = Guid.Parse("165201bd-a4a7-4664-8635-2cd198724556");
    public static readonly Guid RunSingerSync = Guid.Parse("ce65106f-2efd-454e-8180-162b0a7d1b1a");
    public static readonly Guid RunCatalogFeedSync = Guid.Parse("787f8e01-354a-4f31-8f0c-68d6235941d6");

    /// <summary>
    /// Transform Spreadsheet into Catalog Feed 
    /// </summary>
    public static readonly Guid SpreadsheetToCatalog = Guid.Parse("aea93a5b-115d-4f33-92dc-2cb4e59f3f33");

    /// <summary>
    /// Copy feeds from account to org
    /// </summary>
    public static readonly Guid BootstrapProductCatalog = Guid.Parse("f6040466-a998-4a82-8fc0-8f936f5d78f1");
    
    public static readonly Guid SendSMS = Guid.Parse("362d8c4d-1b31-410f-bd99-f65cc5f7c782");
        
    public static readonly Guid CreateInvoice = Guid.Parse("491f6c2d-548a-4cfb-8bbc-36476da0f618");
        
    // create ical (only for appointment as now)
    public static readonly Guid CreateICal = Guid.Parse("fd65182c-0f37-4017-a675-cd29129fbc1d");
    
    public static readonly Guid TakeSnapshot = Guid.Parse("480d00d7-2ac8-4979-9d41-074cc5a8f6c1");
    public static readonly Guid SendgridBulkEmail = Guid.Parse("2f8d8732-6427-45bf-9946-e971dbf21f51");
    
    // salesforce.WorkOrder 
    public static readonly Guid GenerateQbFile = Guid.Parse("897aa867-7e1a-48e2-a506-dbf65af468f2");
    
    public static readonly Guid CopyFile = Guid.Parse("19fe6347-d94d-45fe-a295-caf597f04e49");
    
    // import
    public static readonly Guid ImportObjects = Guid.Parse("23ab04c0-0cb4-418e-8882-b9f57834f323");
    
    // generic: any object type?
    public static readonly Guid Conditional = Guid.Parse("f6be0a00-90f2-412a-9030-86d1ebcc48fd");
    public static readonly Guid DelayEvent = Guid.Parse("33228cf5-d532-44b7-b72d-e5cbc962bec0");
    public static readonly Guid LoadRelatedObject = Guid.Parse("99bd74ae-56aa-4315-a141-1883a0f6ecfb");
    public static readonly Guid IterateView = Guid.Parse("334f988c-c1eb-4790-83fe-413ee53ae15c");
    public static readonly Guid TagObject = Guid.Parse("20f47aac-f325-4f81-82d1-ec3ed4980756");
    public static readonly Guid StartFlow = Guid.Parse("0b2e4278-dd0f-483d-952d-9e8e45d45311");
    public static readonly Guid GetUserInput = Guid.Parse("43c779aa-3707-460a-9246-2bac710f1622");
    public static readonly Guid SendNotification = Guid.Parse("b981d6a6-c1c3-46e0-9e7b-ca77a65d10de");
    public static readonly Guid SendEmailSendgrid = Guid.Parse("06bc39de-be3d-4c74-86ce-100e0339a514");
    public static readonly Guid PostToSlackChannel = Guid.Parse("05fe82f7-2322-4dbb-be55-764578f94821");
    public static readonly Guid PostToGoogleChat = Guid.Parse("f7c9fad6-b982-4855-a151-3234ddf0335a");
    public static readonly Guid HttpCallOut = Guid.Parse("9fb2162d-e50d-4d6f-b0b4-0d127d73ed3f");
    public static readonly Guid ExtractDataToFile = Guid.Parse("a9c70aaf-4980-4393-a9e3-088befdfa285");
    public static readonly Guid SetObjectStatus = Guid.Parse("eded7e06-8bba-4968-9e5e-284702c5a099");
    public static readonly Guid RunStoredProcedure = Guid.Parse("1de42019-eaf8-4c77-9d06-74d8daae4604");
    public static readonly Guid CreateObject = Guid.Parse("b9047b78-cae3-427d-a1b0-4317f05b2443");
    public static readonly Guid UpdateObject = Guid.Parse("40bec386-71f9-4fc7-81bf-9de3aea40a9d");
    public static readonly Guid LookupObject = Guid.Parse("217eb167-dfc1-4ffe-98a4-3ed50f431f54");
    public static readonly Guid Switch = Guid.Parse("be6440ff-81a9-4fdf-ba4d-13d424635c9c");
    public static readonly Guid OpenApiOperation = Guid.Parse("76b83284-8644-4349-aec6-cbbaf985422d");
    public static readonly Guid CreateObjectUsingForm = Guid.Parse("6c3c92f4-d1d3-4bb6-ab20-52a35d8f002e");
    public static readonly Guid ExportToQuickbooks = Guid.Parse("1d1430a0-a6c8-4dbc-b073-aef699b0b36a");
    public static readonly Guid Compose = Guid.Parse("ce1b6a5b-f528-4cd8-ab63-90ed0444d2b7");
    public static readonly Guid FireEvent = Guid.Parse("9003b678-fb9d-421f-97cd-09f5d2869637");
    public static readonly Guid FireWebhook = Guid.Parse("dc672599-64a4-403d-8cec-b7413d86cf6d");
    
    // salesforce
    public static readonly Guid CreateSalesforceObject = Guid.Parse("1d5f47f3-1880-4f3c-8297-5e1f60e40cd6");
    public static readonly Guid UpdateSalesforceObject = Guid.Parse("7aa67a07-1cf6-4a98-b377-28a1deeb6bc2");
    
    // ... to be implemented
    public static readonly Guid DeleteObject = Guid.Parse("5959880e-566f-4381-91f5-292a9cabc787");

    // files
    public static readonly Guid GetPresignedUrl = Guid.Parse("162ec353-036f-4865-8410-2464d083f3dc");

    // LMS
    public static readonly Guid LeadTypeServiceUsage = Guid.Parse("5f7eef70-6392-46d7-993b-29e76afb93ac");
    public static readonly Guid LeadTypeTimeOfDay = Guid.Parse("cddfe7be-f81c-4766-9127-83a7e214033f");
    public static readonly Guid TrustedFormCert = Guid.Parse("3b9aab03-a8b4-4a2e-825e-1032203d9c03");
    
    // generic actions 
    public static readonly Guid MarketingCloudDataExtension = Guid.Parse("523912d9-52b9-4e7c-866e-d156dac7627c");
    public static readonly Guid GenerativeAICompletion = Guid.Parse("189795a3-4f6b-447d-b034-88cd87b397da");
    public static readonly Guid RunScript = Guid.Parse("c060fccd-822e-4539-b651-cc1d2f43502c");
    
    // docuseal
    public static readonly Guid CreateDocuSealSubmission = Guid.Parse("76a86a8c-de4c-4045-94ad-16861603ea08");
    
    // measure square
    public static readonly Guid CalculateSeams = Guid.Parse("f8325623-dce7-4432-97e5-60d27c4cb31d");
    
    public static string GetRoute(Guid actionId) => $"flow.{actionId:N}.action";
}