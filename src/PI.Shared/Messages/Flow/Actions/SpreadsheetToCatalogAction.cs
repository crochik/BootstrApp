using System;
using PI.Shared.Constants;

namespace Messages.Flow
{
    public class SpreadsheetToCatalogActionOptions : ActionOptions
    {
        public const string SuccessEventName = "success";
        public const string WithErrorsEventName = "withErrors";
        public const string FailedEventName = "failed";
        
        public Guid? SuccessEventId { get; set; }
        public Guid? WithErrorsEventId { get; set; }
        public Guid? FailedEventId { get; set; }
        public string StoredProcedure { get; set; }
        public bool IsToProduction { get; set; }
        public override ActionOutput[] Output { get; set; }
    }

    public class SpreadsheetToCatalogAction : FlowAction<SpreadsheetToCatalogActionOptions, SpreadsheetToCatalogAction.Message>
    {
        public override Guid Id => ActionIds.SpreadsheetToCatalog;
        public override string IconName => null;

        public class Message : SimpleActionMessage<SpreadsheetToCatalogActionOptions>
        {
            public Message() { }
            public Message(FlowEvent evt, IActionOptions options) : base(evt, options) { }
        }
    }
}