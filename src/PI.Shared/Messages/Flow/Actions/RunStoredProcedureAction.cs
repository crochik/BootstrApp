using System;
using System.Collections.Generic;
using PI.Shared.Constants;

namespace Messages.Flow
{
    public class RunStoredProcedureActionOptions : SimpleActionOptions
    {
        public string StoredProcedure { get; set; }
        public Dictionary<string, string> Parameters { get; set; }
    }

    public class RunStoredProcedureAction : FlowAction<RunStoredProcedureActionOptions, RunStoredProcedureAction.Message>
    {
        public override Guid Id => ActionIds.RunStoredProcedure;
        public override string IconName => null;

        public class Message : SimpleActionMessage<RunStoredProcedureActionOptions>
        {
            public Message()
            {
            }

            public Message(FlowEvent evt, IActionOptions options) :
                base(evt, options)
            {
            }
        }
    }
}