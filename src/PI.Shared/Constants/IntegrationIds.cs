using System;
using System.Collections.Generic;
using PI.Shared.Models;

namespace PI.Shared.Constants
{
    public static class IntegrationIds
    {
        public static readonly Guid AutoScheduler = Guid.Parse("59009734-8740-45f9-be04-fac1d592b42e");
        public static readonly Guid Convertros = Guid.Parse("e76ff99b-9d02-4e45-a70a-102ea4f4b8ec");
        public static readonly Guid Google = Guid.Parse("3415e2ac-acc5-443c-9b8a-9d44e9f21bcd");
        public static readonly Guid GotoMeeting = Guid.Parse("c0bb2cfb-644a-4b53-9cfd-144a63c89360");
        public static readonly Guid IME = Guid.Parse("e937d9cf-1581-4dce-839b-eff4d4ab9eb6");
        public static readonly Guid InspireNet = Guid.Parse("FC100000-0000-0000-0000-000000000000");
        public static readonly Guid Lumin = Guid.Parse("30d3973d-4204-4395-93c0-d3be21d0e279");
        public static readonly Guid Office365 = Guid.Parse("03650000-0000-0000-0000-000000000000");
        public static readonly Guid Salesforce = Guid.Parse("a2a0b3d8-ae75-47a9-8f10-0bb20af9802f");
        public static readonly Guid SendGrid = Guid.Parse("6BDBC730-8608-4B9F-A2CB-4066D77A6579");
        public static readonly Guid Slack = Guid.Parse("902784BD-26D0-4918-95A2-C934D8398DEF");
        public static readonly Guid Twilio = Guid.Parse("0817244d-5e09-4ea5-9858-3f3175f5c61d");
        public static readonly Guid TypeForm = Guid.Parse("27d9a7a4-2d31-4c05-b226-1a7d529b042f");
        public static readonly Guid OpenAI = Guid.Parse("1da3422f-80e5-44ed-ad54-7d9ed3108a11");
        public static readonly Guid Verse = Guid.Parse("e8fe3377-afa8-48cb-bdc1-3e174b21b69d");
        public static readonly Guid Zoom = Guid.Parse("ffceef81-aa32-4672-b6e0-3e843d81f0dd");
        public static readonly Guid MarketingCloud = Guid.Parse("7660d3fd-1304-4c63-a002-8062aecf5991");
        public static readonly Guid OpenPhone = Guid.Parse("ef9bde7e-fd4b-4fbb-a61b-f42d824c735d");
        public static readonly Guid CompanyCam = Guid.Parse("a384a5bf-fc39-4805-b52c-cdd8b46283ce");
        public static readonly Guid QuickBooks = Guid.Parse("f27931c3-bd79-49d6-9ae1-4a0027f23bc9");
        public static readonly Guid GitHub = Guid.Parse("e471586a-296f-4e02-8a59-f8af7c6380da");
        public static readonly Guid Claude = Guid.Parse("f0871446-3e09-4ee1-866e-52be04b1afdb");
        public static readonly Guid Gemini = Guid.Parse("371ec6da-7be9-4154-9548-03803afa6836");
        public static readonly Guid DocuSeal = Guid.Parse("4d215477-7be5-44c3-8eaa-65e648630e77");

        // TODO: should move the other ones here too
        // Stripe
        // Singer

        public static readonly IReadOnlyDictionary<Guid, string> All = new Dictionary<Guid, string> {
            { AutoScheduler, nameof(AutoScheduler) },
            { Convertros, nameof(Convertros) },
            { GotoMeeting, nameof(GotoMeeting) },
            { IME, nameof(IME) },
            { InspireNet, nameof(InspireNet) },
            { Lumin, nameof(Lumin) },
            { Office365, nameof(Office365) },
            { Salesforce, nameof(Salesforce) },
            { SendGrid, nameof(SendGrid) },
            { Slack, nameof(Slack) },
            { Twilio, nameof(Twilio) },
            { TypeForm, nameof(TypeForm) },
            { Verse, nameof(Verse) },
            { Zoom, nameof(Zoom) },
            { MarketingCloud, nameof(MarketingCloud) },
            { OpenAI, nameof(OpenAI) },
            { CompanyCam, nameof(CompanyCam) },
            { QuickBooks, nameof(QuickBooks) },
            { GitHub, nameof(GitHub) },
        };

        public static string GetName(Guid integrationId)
        {
            if (!All.TryGetValue(integrationId, out var name)) name = "Unknown";
            return name;
        }

        /// <summary>
        /// Route to publish/subscribe for integration/account events
        /// </summary>
        public static string GetActionRoute(IEntityContext context, Guid integrationId) => $"{GetActionRoutePrefix(integrationId)}.{context.AccountId:N}";

        /// <summary>
        /// Route to subscribe for all events for the integration
        /// </summary>
        public static string GetActionRouteForAllAccounts(Guid integrationId) => $"{GetActionRoutePrefix(integrationId)}.#";

        public static string GetActionRoutePrefix(Guid integrationId) => $"integration.{integrationId:N}";
    }

    public class FlowIds
    {
        public static readonly Guid InspireNet = IntegrationIds.InspireNet;
        public static readonly Guid Salesforce = IntegrationIds.Salesforce;
        public static readonly Guid AutoFlow = Guid.Parse("c7825521-2b6b-4c0a-ad5f-a841580386ef");
        public static readonly Guid Default = Guid.Parse("728268da-dd07-49c7-be58-5527c7e92d2e");
        public static readonly Guid Thumbtack = Guid.Parse("1e0c6fa8-8641-4206-b2ff-973a8baa11c7");
        public static readonly Guid Billing = Guid.Parse("3be7799e-6141-4796-baf8-9eb19e88601b");
    }

    public class AppConfigIds
    {
        public static Guid WebScheduler = Guid.Parse("9a034e46-d4d4-4106-8887-4b38181c4956");
    }

    // ????
    // public static readonly Guid TriggerEvent = Guid.Parse("57bea8af-d89d-47cf-a3fa-6b7b9fc3f181");
}