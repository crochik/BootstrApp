using System;
using System.Collections.Generic;
using System.Linq;
using LMS.Models;
using Messages.Flow;
using PI.Shared.Constants;
using PI.Shared.Data.Models;
using PI.Shared.Models;
using PI.Shared.Models.Expressions;

namespace LMS.Controllers;

[Obsolete("the idea was to create a flow dynamicallu")]
public class LeadTypeFlowBuilder
{
    private const string BAD_REQUEST = "BAD_REQUEST";
    private const string REJECTED = "REJECTED";
    private const string OUT_OF_TERRITORY = "OUT_OF_TERRITORY";

    private List<FlowStep> steps = new();
    private readonly LeadType leadType;
    private readonly bool rejectLeads;
    private readonly string contentType = "application/json";

    public LeadTypeFlowBuilder(LeadType leadType, bool rejectLeads)
    {
        this.leadType = leadType;
        this.rejectLeads = rejectLeads;
    }

    private Guid?[] MapProperties(Guid triggerEventId)
    {
        var step = new FlowStep
        {
            EventIdTrigger = triggerEventId,
            CurrentStatusId = leadType.TransactionObjectStatusId,
            Description = "Map Input",
            ActionId = ActionIds.UpdateObject,
            Options = new GenericActionOptions(
                new UpdateObjectActionOptions
                {
                    ObjectType = Transaction.ObjectTypeName,
                    ObjectId = null, // current object
                    Mapping = new Dictionary<string, object>(getParsedInputMapping()),
                }
            )
            {
                Output = new[]
                {
                    new ActionOutput
                    {
                        EventId = Guid.NewGuid(),
                        Name = UpdateObjectActionOptions.ObjectUpdatedEvent,
                        Description = "Request Properties Mapped"
                    },
                    new ActionOutput
                    {
                        EventId = Guid.NewGuid(),
                        Name = UpdateObjectActionOptions.FailedToUpdateObjectEvent,
                        Description = "Error mapping request properties"
                    }
                }
            },
            // IconName = 
        };

        steps.Add(step);

        SetResponse(step.Options.Output[1].EventId.Value, BAD_REQUEST, reject: true);
        setStatus(step.Options.Output[1].EventId.Value, "Update Status to Error");

        return step.Options.Output.Select(x => x.EventId).ToArray();

        IEnumerable<KeyValuePair<string, object>> getParsedInputMapping()
        {
            return leadType.Settings.Fields
                .Where(x => x.Source != null && !x.Source.StartsWith("="))
                .Select(map);

            KeyValuePair<string, object> map(FieldMapperConfig x)
            {
                var source = x.Name switch
                {
                    "Properties|hdyhau" when x.DefaultValue != null => x.DefaultValue,
                    "Properties|leadsource" when x.DefaultValue != null => x.DefaultValue,
                    _ => "{{InitialObject.Request|Payload." + x.Source + "}}",
                };

                return new KeyValuePair<string, object>($"ParsedInput.{x.Name.Replace('|', '.')}", source);
            }
        }
    }

    private Guid?[] SetResponse(Guid eventId, string reason = "{{NULL}}", string message = "{{Event.Description}}", bool reject = false, bool accept = false)
    {
        var updateOptions = new UpdateObjectActionOptions
        {
            ObjectType = Transaction.ObjectTypeName,
            ObjectId = null, // current object
            Mapping = new Dictionary<string, object>
            {
                { "Response|Message", message },
                { "Response|Reason", reason },
                { "Response|ContentType", contentType },
                { "Response|Body", "{{NULL}}" },
                { "EntityId", "{{Objects.Organization._id}}" }
            }
        };

        if (reject)
        {
            updateOptions.Mapping.Add("AcceptedCost", (decimal)0);
            updateOptions.Mapping.Add("RejectedCost", "{{Object.ParsedInput.LeadFee}}");
            updateOptions.Mapping.Add("Response|Success", "{{FALSE}}");
        }
        else if (accept)
        {
            updateOptions.Mapping.Add("AcceptedCost", "{{Object.ParsedInput.LeadFee}}");
            updateOptions.Mapping.Add("RejectedCost", (decimal)0);
            updateOptions.Mapping.Add("Response|Lead|_id", "{{Event.MetaValues.Action|Output|LMSLeadId}}");
            updateOptions.Mapping.Add("Response|Success", "{{TRUE}}");
        }

        var step = new FlowStep
        {
            EventIdTrigger = eventId,
            CurrentStatusId = leadType.TransactionObjectStatusId,
            Description = "Set Response",
            ActionId = ActionIds.UpdateObject,
            Options = new GenericActionOptions(updateOptions)
            {
                Output = new[]
                {
                    new ActionOutput
                    {
                        EventId = Guid.NewGuid(),
                        Name = UpdateObjectActionOptions.ObjectUpdatedEvent,
                        Description = "Response Set",
                    },
                    new ActionOutput
                    {
                        EventId = Guid.NewGuid(),
                        Name = UpdateObjectActionOptions.FailedToUpdateObjectEvent,
                        Description = "Failed to update Response"
                    }
                }
            },
            // IconName = 
        };

        steps.Add(step);

        return step.Options.Output.Select(x => x.EventId).ToArray();
    }

    private Guid?[] setStatus(Guid eventId, string description)
    {
        var step = new FlowStep
        {
            EventIdTrigger = eventId,
            CurrentStatusId = leadType.TransactionObjectStatusId,
            Description = description,
            ActionId = ActionIds.SetObjectStatus,
            Options = new SetObjectStatusActionOptions
            {
                ObjectStatusId = default(Guid?), // will have to be set manually 
                // Output = // ???
            },
            // IconName = 
        };

        return add(step);
    }

    private Guid?[] LookupUpPostalCode(Guid triggerEventId)
    {
        var step = new FlowStep
        {
            EventIdTrigger = triggerEventId,
            CurrentStatusId = leadType.TransactionObjectStatusId,
            Description = "Lookup Postal Code",
            ActionId = ActionIds.LookupObject,
            Options = new GenericActionOptions(
                new LookupObjectActionOptions
                {
                    ObjectType = "ZeeTerritory",
                    Criteria = new Criteria
                    {
                        Conditions = new[]
                        {
                            Condition.Eq(nameof(CustomObject.ExternalId), "{{postalCodeLookup Object.ParsedInput.PostalCode}}"),
                            Condition.Eq(nameof(CustomObject.IsActive), true),
                        }
                    },
                }
            )
            {
                Output = new[]
                {
                    new ActionOutput
                    {
                        EventId = Guid.NewGuid(),
                        Name = LookupObjectActionOptions.ObjectFoundEvent,
                        Description = "Postal Code is Active"
                    },
                    new ActionOutput
                    {
                        EventId = Guid.NewGuid(),
                        Name = LookupObjectActionOptions.ObjectNotFoundEvent,
                        Description = "Postal code is out of territory"
                    },
                    // new ActionOutput
                    // {
                    //     EventId = Guid.NewGuid(),
                    //     Name = LookupObjectActionOptions.MoreThanOneObjectFoundEvent,
                    //     Description = "Postal code is in more than one territory"
                    // }
                }
            },
            // IconName = 
        };

        SetResponse(step.Options.Output[1].EventId.Value, OUT_OF_TERRITORY, reject: true);
        setStatus(step.Options.Output[1].EventId.Value, "Update Status to Out of Territory");

        return add(step);
    }

    private Guid?[] LookupOrganization(Guid triggerEventId)
    {
        var step = new FlowStep
        {
            EventIdTrigger = triggerEventId,
            CurrentStatusId = leadType.TransactionObjectStatusId,
            Description = "Lookup Organization",
            ActionId = ActionIds.LookupObject,
            Options = new GenericActionOptions(
                new LookupObjectActionOptions
                {
                    ObjectType = "Organization",
                    Criteria = new Criteria
                    {
                        Conditions = new[]
                        {
                            Condition.Eq(Model.IdFieldName, "{{Objects.ZeeTerritory.EntityId}}"),
                            Condition.Eq(nameof(CustomObject.IsActive), true),
                        }
                    },
                }
            )
            {
                Output = new[]
                {
                    new ActionOutput
                    {
                        EventId = Guid.NewGuid(),
                        Name = LookupObjectActionOptions.ObjectFoundEvent,
                        Description = "Organization is Active"
                    },
                    new ActionOutput
                    {
                        EventId = Guid.NewGuid(),
                        Name = LookupObjectActionOptions.ObjectNotFoundEvent,
                        Description = "Organization is not Active"
                    },
                    // new ActionOutput
                    // {
                    //     EventId = Guid.NewGuid(),
                    //     Name = LookupObjectActionOptions.MoreThanOneObjectFoundEvent,
                    //     Description = "More than one Organization with the _id"
                    // }                    
                }
            },
            // IconName = 
        };


        SetResponse(step.Options.Output[1].EventId.Value, OUT_OF_TERRITORY, reject: true);
        setStatus(step.Options.Output[1].EventId.Value, "Update Status to Out of Territory");

        return add(step);
    }

    private Guid?[] CheckIfIsPpaOrganization(Guid triggerEventId)
    {
        var nextEventId = Guid.NewGuid();
        var errorEventId = Guid.NewGuid();

        var step = new FlowStep
        {
            EventIdTrigger = triggerEventId,
            CurrentStatusId = leadType.TransactionObjectStatusId,
            Description = "Is PPA?",
            ActionId = ActionIds.Conditional,
            Options = new ConditionalActionOptions
            {
                Criteria = new Criteria
                {
                    Conditions = new[]
                    {
                        Condition.Eq("{{Objects.Organization.FCI|IsPPA}}", true),
                    }
                },
                TrueEventId = nextEventId,
                FalseEventId = errorEventId,
                Output = new[]
                {
                    new ActionOutput
                    {
                        EventId = nextEventId,
                        Name = "OrganizationIsPPA",
                        Description = "Organization is in PPA"
                    },
                    new ActionOutput
                    {
                        EventId = errorEventId,
                        Name = "OrganizationIsNotPPA",
                        Description = "Organization is not PPA"
                    }
                }
            },
            // IconName = 
        };

        addTag(errorEventId, "Organization is not in PPA", "Not PPA");

        return add(step);
    }

    private Guid?[] add(FlowStep step)
    {
        steps.Add(step);

        return step.Options.Output?.Select(x => x.EventId).ToArray() ?? Array.Empty<Guid?>();
    }

    private Guid?[] addTag(Guid triggerEventId, string description, string tag)
    {
        var step = new FlowStep
        {
            EventIdTrigger = triggerEventId,
            CurrentStatusId = leadType.TransactionObjectStatusId,
            Description = description,
            ActionId = ActionIds.TagObject,
            Options = new TagObjectActionOptions
            {
                Tag = tag,
                Output = new[]
                {
                    new ActionOutput
                    {
                        Name = "Tagged",
                        Description = "Tagged",
                        EventId = Guid.NewGuid(),
                    },
                    new ActionOutput
                    {
                        Name = "AlreadyTagged",
                        Description = "Failed to Tag"
                    }
                }
            },
            // IconName = 
        };

        return add(step);
    }

    private Guid?[] CheckIfIsDuplicateFilter(Guid triggerEventId)
    {
        var nextEventId = Guid.NewGuid();
        var duplicateEventId = Guid.NewGuid();

        var step = new FlowStep
        {
            EventIdTrigger = triggerEventId,
            CurrentStatusId = leadType.TransactionObjectStatusId,
            Description = "Check if is a duplicate",
            ActionId = ActionIds.DuplicatedLeadCheck,
            Options = new DuplicatedLeadCheckActionOptions
            {
                Offset = TimeSpan.FromDays(30),
                NextEventId = nextEventId,
                DuplicateLeadEventId = duplicateEventId,
                AlwaysFireNextEvent = !rejectLeads,
                Output = new[]
                {
                    new ActionOutput
                    {
                        EventId = nextEventId,
                        Name = "LeadIsNotDuplicate",
                        Description = "No duplicates found",
                    },
                    new ActionOutput
                    {
                        EventId = duplicateEventId,
                        Name = "LeadIsDuplicate",
                        Description = "Lead is a duplicate"
                    }
                }
            },
            // IconName = 
        };

        return add(step);
    }

    private Guid?[] TrustedFormFilter(Guid triggerEventId)
    {
        var step = new FlowStep
        {
            EventIdTrigger = triggerEventId,
            CurrentStatusId = leadType.TransactionObjectStatusId,
            Description = "Check TrustedForm Certificate",
            ActionId = ActionIds.TrustedFormCert,
            Options = new GenericActionOptions(
                new TrustedFormCertActionOptions
                {
                    Retain = false,
                    Insights = new Dictionary<string, string>
                    {
                        { "ParsedInput.ExtraProperties.TrustedFormAgeSeconds", TrustedFormCertActionOptions.AgeSeconds },
                        { "ParsedInput.ExtraProperties.TrustedFormSecondsOnPage", TrustedFormCertActionOptions.SecondsOnPage },
                        { "ParsedInput.ExtraProperties.TrustedFormDomain", TrustedFormCertActionOptions.Domain },
                    },
                }
            )
            {
                Output = new[]
                {
                    new ActionOutput
                    {
                        EventId = Guid.NewGuid(),
                        Name = TrustedFormCertActionOptions.SuccessEvent,
                        Description = "Validated TrustedForm Certificate",
                    }
                }
            },
            // IconName = 
        };

        return add(step);
    }

    private Guid?[] ServiceFilter(Guid triggerEventId)
    {
        var step = new FlowStep
        {
            EventIdTrigger = triggerEventId,
            CurrentStatusId = leadType.TransactionObjectStatusId,
            Description = "Check if service is active for territory",
            ActionId = ActionIds.LeadTypeServiceUsage,
            Options = new GenericActionOptions(
                new LeadTypeServiceUsageActionOptions
                {
                    Service = "{{Object.ParsedInput.Service}}",
                }
            )
            {
                Output = new[]
                {
                    new ActionOutput
                    {
                        EventId = Guid.NewGuid(),
                        Name = LeadTypeServiceUsageActionOptions.OnBudgetEvent,
                        Description = "Service is active",
                    },
                    new ActionOutput
                    {
                        EventId = Guid.NewGuid(),
                        Name = LeadTypeServiceUsageActionOptions.OverBudgetEvent,
                        Description = "Service is disabled or over budget"
                    }
                }
            },
            // IconName = 
        };

        return add(step);
    }

    private Guid?[] CheckTags(Guid triggerEventId)
    {
        var trueEventId = Guid.NewGuid();
        var falseEventId = Guid.NewGuid();

        var step = new FlowStep
        {
            EventIdTrigger = triggerEventId,
            CurrentStatusId = leadType.TransactionObjectStatusId,
            Description = "Check Tags",
            ActionId = ActionIds.Conditional,
            Options = new ConditionalActionOptions
            {
                Criteria = new Criteria
                {
                    Conditions = new[]
                    {
                        Condition.In("{{Object.Tags}}", TrustedFormCertActionOptions.TAG_NOT_PROVIDED, TrustedFormCertActionOptions.TAG_FRESH),
                        Condition.Nin("{{Object.Tags}}", DuplicatedLeadCheckActionOptions.TAG_DUPLICATE, TrustedFormCertActionOptions.TAG_INVALID, TrustedFormCertActionOptions.TAG_DUPLICATE),
                    }
                },
                TrueEventId = trueEventId,
                FalseEventId = falseEventId,
                Output = new[]
                {
                    new ActionOutput
                    {
                        EventId = trueEventId,
                        Name = "PassLead",
                        Description = "Lead Satisfies all conditions"
                    },
                    new ActionOutput
                    {
                        EventId = falseEventId,
                        Name = "FailLead",
                        Description = "Lead doesn't pass muster"
                    }
                }
            },
            // IconName = 
        };

        SetResponse(falseEventId, REJECTED, reject: true);
        setStatus(falseEventId, "Update Status to Rejected");

        return add(step);
    }

    private Guid?[] CreateLead(Guid triggerEventId)
    {
        var successEventId = Guid.NewGuid();
        var errorEventId = Guid.NewGuid();

        var step = new FlowStep
        {
            EventIdTrigger = triggerEventId,
            CurrentStatusId = leadType.TransactionObjectStatusId,
            Description = "Create Lead",
            ActionId = ActionIds.CreateObject,
            Options = new GenericActionOptions(
                new CreateObjectActionOptions
                {
                    ObjectType = "LMSLead", // leadType.ObjectType,
                    Mapping = new Dictionary<string, object>(GetCreateMapping()),
                }
            )
            {
                Output = new[]
                {
                    new ActionOutput
                    {
                        EventId = successEventId,
                        Name = CreateObjectActionOptions.ObjectCreatedEvent,
                        Description = "Lead created",
                    },
                    new ActionOutput
                    {
                        EventId = errorEventId,
                        Name = CreateObjectActionOptions.FailedToCreateObjectEvent,
                        Description = "Failed to create Lead",
                    }
                }
            },
            // IconName = 
        };

        retainCertificate(successEventId);

        SetResponse(successEventId, message: "Lead Imported successfully", accept: true);
        setStatus(successEventId, "Update Status to Accepted");

        SetResponse(errorEventId, BAD_REQUEST, reject: true);
        setStatus(errorEventId, "Update Status to Error");

        return add(step);
    }

    private Guid?[] retainCertificate(Guid triggerEventId)
    {
        var step = new FlowStep
        {
            EventIdTrigger = triggerEventId,
            CurrentStatusId = leadType.TransactionObjectStatusId,
            Description = "Retain TrustedForm Certificate",
            ActionId = ActionIds.TrustedFormCert,
            Options = new GenericActionOptions(
                new TrustedFormCertActionOptions
                {
                    Retain = true,
                    Vendor = "LeadsPiper.com",
                    VendorId = "{{Event.Action|Output|LMSLeadId}}",
                }
            )
            {
                Output = new[]
                {
                    new ActionOutput
                    {
                        EventId = Guid.NewGuid(),
                        Name = TrustedFormCertActionOptions.SuccessEvent,
                        Description = "Retained TrustedForm Certificate",
                    }
                }
            },
            // IconName = 
        };

        return add(step);
    }

    public Flow Build()
    {
        var mapping = MapProperties(EventIds.OnStatusEntered);
        var postalCode = LookupUpPostalCode(mapping[0].Value);
        var organization = LookupOrganization(postalCode[0].Value);

        // filters
        var isppa = CheckIfIsPpaOrganization(organization[0].Value);
        var isDuplicate = CheckIfIsDuplicateFilter(organization[0].Value);
        var trustedForm = TrustedFormFilter(organization[0].Value);
        var service = ServiceFilter(organization[0].Value);
        var tags = CheckTags(organization[0].Value);

        CreateLead(tags[0].Value);

        return new Flow
        {
            Id = Guid.NewGuid(),
            AccountId = leadType.AccountId,
            EntityId = leadType.AccountId,
            Name = "LMS Flow",
            Description = "Auto-generated Flow for LeadsPiper.com",
            CreatedOn = DateTime.UtcNow,
            // LastActor = 
            ObjectType = Transaction.ObjectTypeName,
            Steps = steps.ToArray(),
        };
    }

    IEnumerable<KeyValuePair<string, object>> GetCreateMapping()
    {
        foreach (var kvp in leadType.Settings.Fields
                     .Where(x => x.Source != null && !x.Source.StartsWith("="))
                     .Select(getMappingForCreate)
                )
        {
            yield return kvp;
        }

        if (leadType.Settings.Fields.Any(x => x.Name == nameof(Lead.Name)))
        {
            if (leadType.Settings.Fields.All(x => x.Name != nameof(Lead.FirstName)))
            {
                yield return new KeyValuePair<string, object>("FirstName", "{{firstName Object.ParsedInput.Name}}");
            }

            if (leadType.Settings.Fields.All(x => x.Name != nameof(Lead.LastName)))
            {
                yield return new KeyValuePair<string, object>("LastName", "{{lastName Object.ParsedInput.Name}}");
            }
        }
        else
        {
            yield return new KeyValuePair<string, object>("Name", "{{concatenate Object.ParsedInput.FirstName? Object.ParsedInput.MiddleName? Object.ParsedInput.LastName?}}");
        }

        if (leadType.Settings.Fields.All(x => x.Name != nameof(Lead.NormalizedEmail)))
        {
            yield return new KeyValuePair<string, object>("NormalizedEmail", "{{normalizeEmail Object.ParsedInput.Email}}");
        }

        if (leadType.Settings.Fields.All(x => x.Name != nameof(Lead.NormalizedPhoneNumber)))
        {
            yield return new KeyValuePair<string, object>("NormalizedPhoneNumber", "{{normalizePhone Object.ParsedInput.Phone}}");
        }

        if (leadType.Settings.Fields.All(x => x.Name != nameof(Lead.EntityId) || x.Source.StartsWith("=")))
        {
            yield return new KeyValuePair<string, object>("EntityId", "{{Objects.Organization._id}}"); // TODO: coalesce to account id?
        }

        if (leadType.Settings.Fields.All(x => x.Name != nameof(Lead.ObjectStatusId)) && leadType.InitialObjectStatusId.HasValue)
        {
            yield return new KeyValuePair<string, object>("ObjectStatusId", leadType.InitialObjectStatusId.Value);
        }

        if (leadType.Settings.Fields.All(x => x.Name != nameof(Lead.FlowId)) && leadType.InitialFlowId.HasValue)
        {
            yield return new KeyValuePair<string, object>("FlowId", leadType.InitialFlowId.Value);
        }

        if (leadType.Settings.Fields.All(x => x.Name != nameof(Lead.LeadTypeId)))
        {
            yield return new KeyValuePair<string, object>("LeadTypeId", "{{Object.Request|LeadTypeId}}");
        }

        if (leadType.Settings.Fields.All(x => x.Name != nameof(Lead.Tags)))
        {
            yield return new KeyValuePair<string, object>("Tags", "{{Object.Tags}}");
        }

        if (leadType.Settings.Fields.All(x => x.Name != "ExtraProperties"))
        {
            yield return new KeyValuePair<string, object>("ExtraProperties", "{{Object.ExtraProperties}}");
        }

        KeyValuePair<string, object> getMappingForCreate(FieldMapperConfig x)
        {
            var source = x.Name switch
            {
                "Email" => "{{normalizeEmail Object.ParsedInput." + x.Name + "}}",
                "Phone" => "{{normalizePhone Object.ParsedInput." + x.Name + "}}",
                // "Properties|hdyhau" => x.DefaultValue.ToString(),
                // "Properties|leadsource" => x.DefaultValue.ToString(),
                _ => "{{Object.ParsedInput." + x.Name.Replace('|', '.') + "}}"
            };

            return new KeyValuePair<string, object>(x.Name, source);
        }
    }
}