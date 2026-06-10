namespace PI.Shared.Form.Models
{
    public class DataQuery
    {
        public bool? ForceReload { get; set; }
        public object Query { get; set; }
        public object[] Args { get; set; }
    }
}