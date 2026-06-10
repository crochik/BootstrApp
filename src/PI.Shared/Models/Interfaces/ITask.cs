using System;

namespace PI.Shared.Models.Interfaces;

public interface ITask : INote, IWithAddress
{
    public Guid AssignedUserId { get; set; }
    public DateTime? DueDate { get; set; }
    public string Priority { get; set; }
    
    // status: cancelled, completed, open, rejected, ...

    // rules?
    //      completion requirements? 
    //          all complete, first completes, ....
    //      
}
