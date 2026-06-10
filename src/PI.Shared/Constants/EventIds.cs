using System;

namespace PI.Shared.Constants
{
    public class EventIds
    {
        public const string AllRoute = "flow.*.event";
        public const string ErrorRoute = "flow.*.error";

        public static readonly Guid OnAppointmentCanceled = Guid.Parse("739b2f32-328c-4a7d-9e3d-af14437367c7");
        public static readonly Guid OnAppointmentExportedToInspirenet = ActionIds.ExportAppointmentToInspireNet;
        public static readonly Guid OnAppointmentExportedToOffice365 = ActionIds.ExportAppointmentToOffice365;
        
        [Obsolete("use object created instead")]
        public static readonly Guid OnAppointmentScheduled = Guid.Parse("ba066c97-d623-4acd-890f-bfadbedb2ae9");

        public static readonly Guid OnAppointmentImported = Guid.Parse("49ed2669-12f5-4308-b072-bfb5291b499b");
        public static readonly Guid OnAssignedEntityIdChanged = ActionIds.AssignLead;
        public static readonly Guid OnDroppedDead = Guid.Parse("38e975ff-99af-4d1a-99b0-1bfbed5ba940");
        public static readonly Guid OnEntityUnassigned = Guid.Parse("42a80470-c2db-4734-bf56-c07f6411fda0");
        public static readonly Guid OnError = Guid.Parse("3c856716-a13d-4bce-a277-637fb8c4c3f7");
        public static readonly Guid OnFlowAssigned = Guid.Parse("24313793-3946-4f2f-9fb4-08451a894255");

        [Obsolete("the integration exporting the data should update the object directly")]
        public static readonly Guid OnIntegrationForAppointmentUpdated = ActionIds.UpdateIntegrationForAppointment;

        // TODO: move away from these using generic onCreate/onUpdate events
        // dangerous changes :)
        // ...
        public static readonly Guid OnLeadCreated = Guid.Parse("ed51c9ce-945d-47cb-af2f-6d4535f7ae11");
        public static readonly Guid OnLeadImported = Guid.Parse("9067e887-e26c-414e-bc7e-4adaa63b591f");
        public static readonly Guid OnLeadUpdated = Guid.Parse("0d0eed75-400d-4138-9670-c22f82ad3646");

        public static readonly Guid OnIntegrationForLeadUpdated = ActionIds.UpdateIntegrationForLead;
        public static readonly Guid OnLeadExportedToInspireNet = ActionIds.ExportLeadToInspireNet;
        public static readonly Guid OnLeadExportedToLumin = ActionIds.ExportLeadToLumin;
        public static readonly Guid OnLeadExportedToSalesforce = ActionIds.ExportToSalesforce;
        
        public static readonly Guid OnMarkedToFollowUp = Guid.Parse("d32ae172-98cb-4dc4-8616-df46fe976312");
        public static readonly Guid OnCreateInvoice = Guid.Parse("cee7223f-d6ae-44e6-9bad-a0a0999a3f18");
        public static readonly Guid OnPendingPayment = Guid.Parse("43df36c2-b2dc-4c42-a656-dc696fc1bc74");
        public static readonly Guid OnPayment = Guid.Parse("b9e47fa3-f5d3-4907-b60f-f79104d8efa8");
        public static readonly Guid OnFailedPayment = Guid.Parse("1f43b2b1-0a2f-48a8-9ec2-70cd3a760597");
        public static readonly Guid OnBalanceUpdated = Guid.Parse("5ea6e5f1-2fc8-40d9-af5d-f59f97893729");
        public static readonly Guid OnDisputeCreated = Guid.Parse("2ce36389-d298-49e4-8a7c-e47f6c5b57c5");
        public static readonly Guid OnDisputeRejected = Guid.Parse("ecc3c4c8-ede8-4477-97cb-5b1b27745535");
        public static readonly Guid OnDisputeApproved = Guid.Parse("4049e805-9c6a-48c6-9813-74e92439116f");
        public static readonly Guid OnBalanceAdjusted = Guid.Parse("6128053b-a83a-4ef3-bdd6-930f96c8cdae");
        public static readonly Guid OnAutoRefillSettingsUpdated = Guid.Parse("e537014e-c7a8-45a8-acf3-a83f3120bce3");
        public static readonly Guid OnPaymentMethodAdded = Guid.Parse("3be20346-3979-4c3a-b442-075f3c923b39");
        
        // TODO: make this become the "OnEnterToStatus" event
        // used to be called "OnLeadStatusChanged"
        // ...
        public static readonly Guid OnStatusEntered = ActionIds.SetObjectStatus; // reuses the same id for no good reason

        /// <summary>
        /// System/Generic SMS message received from the "contact" (Lead, Entity, ...) 
        /// </summary>
        public static readonly Guid OnSMSReceived = Guid.Parse("4d61e085-872c-4b5e-acaf-9cf05c51b54a");

        /// <summary>
        /// System/Generic event fired when the object is created
        /// </summary>
        public static readonly Guid OnObjectCreated = Guid.Parse("5a8eb855-ecb2-42d5-8705-9d3d887106fb");

        /// <summary>
        /// System/Generic event fired when the object is updated
        /// </summary>
        public static readonly Guid OnObjectUpdated = Guid.Parse("37299525-b651-49c8-b871-bca94dc5c954");
        
        /// <summary>
        /// System/generic event fired when an object is accessed by an user
        /// </summary>
        public static readonly Guid OnObjectLoaded = Guid.Parse("a2533da7-cb45-4bd2-aec2-b17983eda3df");

        /// <summary>
        /// System/Generic Sync event, sync started
        /// </summary>
        public static readonly Guid OnSyncStarted = Guid.Parse("9247506f-3fca-4fb2-92f0-e19d20667508");

        /// <summary>
        /// System/Generic Sync event, sync finsihed successfully (possibly with errors)
        /// </summary>
        public static readonly Guid OnSyncFinished = Guid.Parse("94cd3c79-150c-4eb8-b7cb-183c3c007e21");

        /// <summary>
        /// System/Generic Sync event, sync failed (couldn't be completed)
        /// </summary>
        public static readonly Guid OnSyncFailed = Guid.Parse("acd9b5af-e4c9-4c0a-b798-33564d296e64");

        /// <summary>
        ///  System/Generic A push notification was successfully sent to user
        /// </summary>
        public static readonly Guid OnNotificationSent = Guid.Parse("cd713463-2a5c-4e0d-a367-9107ab4dbd19");
        
        /// <summary>
        ///  System/Generic No devices found or notification failed 
        /// </summary>
        public static readonly Guid OnNotificationFailed = Guid.Parse("c69af058-ffcd-4d65-b4ee-0f070da5da9d");
        
        
        public static readonly Guid OnBulkEmailDone = Guid.Parse("cbdee217-afb6-4ca4-a91f-f83ed545fdca");
        
        public static string GetRoute(Guid eventId, bool error = false)
            => $"flow.{eventId:N}.{(error ? "error" : "event")}";
    }
}