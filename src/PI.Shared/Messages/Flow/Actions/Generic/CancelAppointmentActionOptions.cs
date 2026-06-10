using PI.Shared.Models;

namespace Messages.Flow;

public class CancelAppointmentActionOptions : ActionOptions
{
    public enum OperationOptions
    {
        Cancel,
        Delete
    }
    
    public OperationOptions Operation { get; set; } 
    public Criteria Criteria { get; set; }
    public bool AllowMultiple { get; set; }
}