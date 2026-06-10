using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Models.Slack
{
    public class SlackMessage
    {
        public string Text { get; set; }
        public Block[] Blocks { get; set; }
        public Attachment[] Attachments { get; set; }
    }

    public class Action
    {
        public string Type { get; set; } = "button";
        public string Text { get; set; }
        public string Url { get; set; }
    }

    public class Attachment
    {
        public string Fallback { get; set; }
        public Action[] Actions { get; set; }
    }

    public class TextObject
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum TextObjectType
        {
            [EnumMember(Value = "plain_text")]
            PlainText,

            [EnumMember(Value = "mrkdwn")]
            MarkDown
        };

        public TextObjectType Type { get; set; } = TextObjectType.PlainText;
        public string Text { get; set; }
        public bool? Emoji { get; set; }
        public bool? Verbatin { get; set; }
    }

    // https://api.slack.com/reference/block-kit/block-elements
    public abstract class Block
    {
        public abstract string Type { get; }

        [JsonProperty("block_id")]
        public string BlockId { get; set; } = Guid.NewGuid().ToString("N");
    }

    public class DividerBlock : Block
    {
        public override string Type => "divider";
    }

    public abstract class ElementObject
    {
        public abstract string Type { get; }
    }

    public class ConfirmationDialog
    {
        // plain-text only
        public TextObject Title { get; set; }

        public TextObject Text { get; set; }

        // plain-text only
        public TextObject Confirm { get; set; }

        // plain-text only
        public TextObject Deny { get; set; }
    }

    public class Button : ElementObject
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum ButtonStyle
        {
            [EnumMember(Value = "default")]
            Default,
            [EnumMember(Value = "primary")]
            Primary,
            [EnumMember(Value = "danger")]
            Danger
        }

        public override string Type => "button";
        public TextObject Text { get; set; }

        [JsonProperty("action_id")]
        public string ActionId { get; set; } = Guid.NewGuid().ToString("N");
        public string Url { get; set; }
        public string Value { get; set; }
        public ButtonStyle? Style { get; set; }
        public ConfirmationDialog Confirm { get; set; }
    }

    public class ActionsBlock : Block
    {
        public override string Type => "actions";
        public ElementObject[] Elements { get; set; }
    }

    //??? 
    public class Accessory
    {
    }

    public class SectionBlock : Block
    {
        public override string Type => "section";
        public TextObject Text { get; set; }
        public TextObject[] Fields { get; set; }
        public Accessory Accessory { get; set; }
    }

    public class ImageBlock : Block
    {
        public override string Type => "image";
        // ...
    }
}
