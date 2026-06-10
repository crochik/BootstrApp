using System;
using PI.Shared.Constants;

namespace Messages.Flow
{
    public class RunSingerSyncActionOptions : ActionOptions
    {
        public const string SyncedEventName = "synced";

        public Guid? SyncedEventId { get; set; }
        public Guid? ConfigurationId { get; set; }

        public override ActionOutput[] Output { get; set; }
    }

    public class RunSingerSyncAction : FlowAction<RunSingerSyncActionOptions, RunSingerSyncAction.Message>
    {
        public override Guid Id => ActionIds.RunSingerSync;
        public override string IconName => null;

        public class Message : SimpleActionMessage<RunSingerSyncActionOptions>
        {
            public Message() { }
            public Message(FlowEvent evt, IActionOptions options) : base(evt, options) { }
        }
    }
}