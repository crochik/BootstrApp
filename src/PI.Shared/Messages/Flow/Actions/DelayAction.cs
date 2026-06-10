using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PI.Shared.Constants;

namespace Messages.Flow
{
    public class DelayActionOptions : ActionOptions
    {
        public const string DelayedEventName = "delayed";
        
        [JsonConverter(typeof(StringEnumConverter))]
        public enum Anchors
        {
            Appointment,
            ExecutionTime,
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum BeforeAfter
        {
            Before,
            After,
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum UnitsOfTime
        {
            Minutes,
            Hours,
            Days,
        }

        public Anchors Anchor { get; set; }
        public BeforeAfter When { get; set; }
        public UnitsOfTime Unit { get; set; }
        public int Amount { get; set; }
        public bool TruncateDate { get; set; } // TODO: truncate day (midnight), truncate week (sunday of the week), ...
        public string Tag { get; set; }
        
        public Guid? DelayedEventId { get; set; }

        public override ActionOutput[] Output { get; set; }

        private string SingularFormWhen() => Unit switch
        {
            UnitsOfTime.Minutes => "One Minute",
            UnitsOfTime.Hours => "One Hour",
            UnitsOfTime.Days => "One Day",
            _ => "Error"
        };

        private string TruncatedWhen()
        {
            if (When == BeforeAfter.After)
            {
                return Unit switch
                {
                    UnitsOfTime.Minutes => $"{(int)Amount / 60}:{Amount % 60} of the day of {Anchor}",
                    UnitsOfTime.Hours => (Amount < 12 ? $"{Amount} AM" : $"{Amount - 12} PM") + $" of the day of {Anchor}",
                    _ => $"{Amount} Days After the day of {Anchor}"
                };
            }

            return "???";
        }

        public string WhenToString() => TruncateDate ?
            TruncatedWhen() :
            (Amount != 1 ? $"{Amount} {Unit}" : SingularFormWhen()) + $" {When} {Anchor}";

        public TimeSpan GetTimeSpan()
        {
            var ammount = When == BeforeAfter.Before ? -Amount : Amount;

            return Unit switch
            {
                UnitsOfTime.Minutes => TimeSpan.FromMinutes(ammount),
                UnitsOfTime.Hours => TimeSpan.FromHours(ammount),
                UnitsOfTime.Days => TimeSpan.FromDays(ammount),
                _ => TimeSpan.Zero
            };
        }
    }

    public class DelayAction : FlowAction<DelayActionOptions, DelayAction.Message>
    {
        public override Guid Id => ActionIds.DelayEvent;
        public override string IconName => Id.ToString();

        public class Message : SimpleActionMessage<DelayActionOptions>
        {
            public Message() { }

            public Message(FlowEvent evt, IActionOptions options) : base(evt, options) { }
        }
    }
}