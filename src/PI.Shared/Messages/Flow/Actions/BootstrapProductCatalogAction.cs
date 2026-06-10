using System;
using PI.Shared.Constants;

namespace Messages.Flow
{
    public class BootstrapProductCatalogActionOptions : SimpleActionOptions
    {
    }

    public class BootstrapProductCatalogAction : FlowAction<BootstrapProductCatalogActionOptions, BootstrapProductCatalogAction.Message>
    {
        public override Guid Id => ActionIds.BootstrapProductCatalog;
        public override string IconName => null;

        public class Message : SimpleActionMessage<BootstrapProductCatalogActionOptions>
        {
            public Message() { }
            public Message(FlowEvent evt, IActionOptions options) : base(evt, options) { }
        }
    }
}