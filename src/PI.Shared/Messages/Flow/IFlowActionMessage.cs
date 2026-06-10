using System;

namespace Messages.Flow
{
    public interface IFlowActionMessage
    {
        Guid Id { get; }
        ActionOptions ActionOptions { get; }
        string IconName { get; }
    }

    public abstract class FlowAction<T, TMessage> : IFlowActionMessage
        where T : ActionOptions, new()
        where TMessage : ActionMessage<T>, new()
    {
        public ActionOptions ActionOptions => Options;
        public T Options { get; set; } = new T();
        public virtual string IconName { get; }
        public abstract Guid Id { get; }
    }
}