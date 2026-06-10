using System;
using PI.Shared.Constants;

namespace Messages.Flow
{
    public class SyncCatalogFeedActionOptions : ActionOptions
    {
        public const string SyncedEvent = "synced";

        public Guid? SyncedEventId { get; set; }
        public Guid? ConfigurationId { get; set; }

        public override ActionOutput[] Output { get; set; }
    }

    public class SyncCatalogFeedAction : FlowAction<SyncCatalogFeedActionOptions, SyncCatalogFeedAction.Message>
    {
        public override Guid Id => ActionIds.RunCatalogFeedSync;
        public override string IconName => null;

        public class Message : SimpleActionMessage<SyncCatalogFeedActionOptions>
        {
            public Message() { }
            public Message(FlowEvent evt, IActionOptions options) : base(evt, options) { }
        }
    }
}