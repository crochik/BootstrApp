using System;

namespace Models;

public class LeadResponse 
{
    public Guid? Id { get; set; }
    public ConvertrosLead Lead { get; set; }
}