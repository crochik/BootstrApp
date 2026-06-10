using PI.Shared.Form.Models;

namespace PI.Shared.Models;

public class FieldMapperConfig // : FormField
{
    public bool IsRequired { get; set; }
    public string Name { get; set; }
    public string Label { get; set; }
    public string Source { get; set; }
    public FIELDTYPE Type { get; set; }
    public object DefaultValue { get; set; }
}