using System.Collections.Generic;

namespace Messages.Flow;

public class TrustedFormCertActionOptions : ActionOptions, IActionOptionsForRunner
{
    public const string TAG = "TrustedForm";
    public const string TAG_NOT_PROVIDED = "TrustedForm: Not Provided";
    public const string TAG_NOT_FOUND = "TrustedForm: Not Found";
    public const string TAG_INVALID = "TrustedForm: Invalid";
    public const string TAG_DUPLICATE = "TrustedForm: Duplicate";
    public const string TAG_RETAINED = "TrustedForm: Retained";
    public const string TAG_NO_INSIGHTS = "TrustedForm: No Insights";
    
    public const string TAG_FRESH = "TrustedForm: Fresh";
    public const string TAG_5MINUTES = "TrustedForm: 5m";
    public const string TAG_10MINUTES = "TrustedForm: 10m";
    public const string TAG_30MINUTES = "TrustedForm: 30m";
    public const string TAG_1HOUR = "TrustedForm: 1h";
    public const string TAG_12HOURS = "TrustedForm: 12h";
    public const string TAG_24HOURS = "TrustedForm: 24h";
    public const string TAG_STALE = "TrustedForm: Stale";
    public const string TAG_BOT = "TrustedForm: BOT";
    
    public const string SuccessEvent = nameof(SuccessEvent);
    // public const string ErrorEvent = nameof(ErrorEvent);
    // public const string DuplicateEvent = nameof(DuplicateEvent);
    
    public const string AgeSeconds = "age_seconds";
    public const string ApproxIpGeo = "approx_ip_geo";
    public const string Domain = "domain";
    public const string FormInputMethod = "form_input_method";
    public const string Ip = "ip";
    public const string SecondsOnPage = "seconds_on_page";
    public const string BotDetected = "bot_detected";

    /// <summary>
    /// Template path to certificate url (e.g. {{...}}) 
    /// </summary>
    public string Certificate { get; set; } = "{{Object.ParsedInput.TrustedFormCert}}";

    /// <summary>
    /// Template path to phone property 
    /// </summary>
    public string Phone { get; set; } = "{{Object.ParsedInput.Phone}}";

    /// <summary>
    /// Template path to email property
    /// </summary>
    public string Email { get; set; } = "{{Object.ParsedInput.Email}}";

    /// <summary>
    /// Vendor to be used (when not provided will default to LeadsPiper.com)
    /// </summary>
    public string Vendor { get; set; } = "LeadsPiper.com";

    /// <summary>
    /// Template path to Vendor id to be used to retain (e.g. "our id")  
    /// </summary>
    public string VendorId { get; set; }

    /// <summary>
    /// Whether to retain or not certificate 
    /// </summary>
    public bool Retain { get; set; }

    /// <summary>
    /// list of insights to request and where to store (key=path in run, value=insight) 
    /// </summary>
    public Dictionary<string, string> Insights { get; set; }

    // public ObjectType BuildActionOptionsObjectType(IEntityContext context)
    // {
    //     var objectType = new ObjectType
    //     {
    //         Id = Guid.NewGuid(),
    //         Name = $"{nameof(ActionIds.TrustedFormCert)}ActionOptions",
    //         Description = "Trusted Form Options",
    //         AccountId = context.AccountId.Value,
    //         EntityId = context.AccountId.Value,
    //         BaseObjectType = "ActionOptions",
    //         IsEmbedded = true,
    //         CollectionName = "*",
    //         NativeType = "?",
    //         RBAC = new ObjectTypeRBAC
    //         {
    //             [EntityRoleId.Admin] = ObjectTypePermission.Read | ObjectTypePermission.Update | ObjectTypePermission.Create | ObjectTypePermission.Delete,
    //         },
    //         Fields = getFields().Select(x => new FieldTemplate
    //         {
    //             Field = x,
    //             RBAC = new FieldRBAC()
    //             {
    //                 [EntityRoleId.Admin] = FieldPermission.Read | FieldPermission.Update | FieldPermission.SetOnCreate,
    //             }
    //         }).ToDictionary(x => x.Field.Name)
    //     };
    //
    //     return objectType;
    //
    //     IEnumerable<FormField> getFields()
    //     {
    //         yield return new TextField
    //         {
    //             Name = nameof(Certificate),
    //             Label = "Certificate (expression)",
    //             Description = "Certificate Url. It can be an Expression.",
    //             IsRequired = false,
    //             TextFieldOptions = new TextFieldOptions
    //             {
    //                 AllowExpressions = true,
    //             },
    //             DefaultValue = "{{Object.ParsedInput.TrustedFormCert}}",
    //         };
    //
    //         yield return new TextField
    //         {
    //             Name = nameof(Email),
    //             Label = "Email (expression)",
    //             Description = "Email associated with certificate. It can be an Expression.",
    //             IsRequired = false,
    //             TextFieldOptions = new TextFieldOptions
    //             {
    //                 AllowExpressions = true,
    //             },
    //             DefaultValue = "{{Object.ParsedInput.Email}}",
    //         };
    //         
    //         yield return new TextField
    //         {
    //             Name = nameof(Phone),
    //             Label = "Phone (expression)",
    //             Description = "Phone number associated with certificate. It can be an Expression.",
    //             IsRequired = false,
    //             TextFieldOptions = new TextFieldOptions
    //             {
    //                 AllowExpressions = true,
    //             },
    //             DefaultValue = "{{Object.ParsedInput.Phone}}",
    //             Visible = new [] {nameof(Retain)},
    //         };            
    //
    //         yield return new CheckboxField()
    //         {
    //             Name = nameof(Retain),
    //             Label = "Retain Certificate",
    //             Description = "Whether to retain certificate or not.",
    //             IsRequired = true,
    //             DefaultValue = false,
    //         };
    //
    //         yield return new TextField
    //         {
    //             Name = nameof(Vendor),
    //             Label = "Vendor",
    //             Description = "Vendor to be associated with retained certificate. It can be an Expression.",
    //             IsRequired = false,
    //             TextFieldOptions = new TextFieldOptions
    //             {
    //                 AllowExpressions = true,
    //             },
    //             DefaultValue = "LeadsPiper.com",
    //             Visible = new [] {nameof(Retain)},
    //         };
    //
    //         yield return new TextField
    //         {
    //             Name = nameof(VendorId),
    //             Label = "Vendor Id",
    //             Description = "Id to be associated with retained certificate. It can be an Expression.",
    //             IsRequired = false,
    //             TextFieldOptions = new TextFieldOptions
    //             {
    //                 AllowExpressions = true,
    //             },
    //             Visible = new [] {nameof(Retain)},
    //         };
    //         
    //         yield return new DictionaryField
    //         {
    //             Name = nameof(Insights),
    //             Label = "Insights",
    //             Description = "Insights to capture. Key is the path to target property in the run",
    //             DictionaryFieldOptions =
    //             {
    //                 KeyField = new TextField
    //                 {
    //                     Name = "Target",
    //                     Label = "Target Property",
    //                 },
    //                 ValueField = new SelectField
    //                 {
    //                     Name = "Insight",
    //                     Label = "Insight",
    //                     SelectFieldOptions = new SelectFieldOptions
    //                     {
    //                         Items = new Dictionary<string,string>
    //                         {
    //                             { AgeSeconds, "Age in Seconds" },
    //                             { ApproxIpGeo, "Approximate Location" },
    //                             { Domain, "Domain" },
    //                             { FormInputMethod, "Input Method" },
    //                             { Ip, "IP Address" },
    //                             { SecondsOnPage, "Seconds in Page" },
    //                         },
    //                     }
    //                 },
    //             },
    //         };                 
    //
    //         // yield return new CheckboxField
    //         // {
    //         //     Name = nameof(AlwaysFireNextEvent),
    //         //     Label = "Ignore Error(s) and Continue",
    //         //     Description = "When set it will always continue even if it can't validate/retain certificate.",
    //         //     IsRequired = false,
    //         //     DefaultValue = false,
    //         // };
    //     }
    // }
    //
    // public GenericAction BuildGenericAction(IEntityContext context)
    // {
    //     var action = new GenericAction
    //     {
    //         AccountId = context.AccountId.Value,
    //         CreatedOn = DateTime.UtcNow,
    //         Name = nameof(ActionIds.TrustedFormCert),
    //         Description = "Validate/Retain Trusted Form Certificate",
    //         ActionId = ActionIds.TrustedFormCert,
    //         InputObjectTypes = null,
    //         IconName = null,
    //         ActionOptionsObjectType = $"{nameof(ActionIds.TrustedFormCert)}ActionOptions",
    //         Role = EntityRoleId.Admin,
    //         ProfileIds = null,
    //         Outputs = new Dictionary<string, string>
    //         {
    //             { SuccessEvent, "TrustedForm Filter" },
    //             // { ErrorEvent, "Failed Certificate Operation" },
    //             // { DuplicateEvent, "Certificate has been retained before" }
    //         }
    //     };
    //
    //     return action;
    // }
}