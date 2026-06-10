namespace PI.Shared.Form.Models;

public class PhoneFieldOptions : FieldOptions
{
    public enum AutoFormatOption
    {
        None, 
        National,
        International,
    }    
    
    public AutoFormatOption AutoFormat { get; set; }
}