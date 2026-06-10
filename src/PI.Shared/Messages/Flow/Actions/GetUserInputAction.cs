using System;
using PI.Shared.Constants;

namespace Messages.Flow;

/// <summary>
/// Get input from user
/// right now it is only used to get input from user in the foreground (as a side effect of another event when submitting a form)
/// </summary>
public class GetUserInputActionOptions : ActionOptions
{
    public Guid? NextEventId { get; set; }
    
    public override ActionOutput[] Output { get; set; }
    
    // TODO: add property to determine if it should run in the foreground, async (e.g. request input using notification) or "any"
    // TODO: add support to process it and send the notification in the flow service (and skip if it is just "foreground"
    // ...
}

public class GetUserInputAction : FlowAction<GetUserInputActionOptions, GetUserInputAction.Message>
{
    public override Guid Id => ActionIds.GetUserInput;
    public override string IconName => Id.ToString();

    public class Message : SimpleActionMessage<GetUserInputActionOptions>
    {
        public Message() { }

        public Message(FlowEvent evt, IActionOptions options) : base(evt, options) { }
    }
}