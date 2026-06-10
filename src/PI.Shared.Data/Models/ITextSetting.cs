namespace PI.Shared.Data.Models
{
    public interface ITextSetting : ISetting
    {
        string Value { get; }
        string UserValue { get; }
        string OrgValue { get; }
        string AccountValue { get; }
    }

    public class TextSetting : Setting, ITextSetting
    {
        public string Value => UserValue ?? OrgValue ?? AccountValue;
        public string UserValue { get; set; }
        public string OrgValue { get; set; }
        public string AccountValue { get; set; }
    }
}