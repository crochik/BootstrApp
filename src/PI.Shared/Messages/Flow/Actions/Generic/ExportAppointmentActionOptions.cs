namespace Messages.Flow;

public class ExportAppointmentActionOptions : ActionOptions
{
    public const string CreatedEventOutputName = "CreatedEvent";
    public const string FailedToCreateOutputName = "FailedToCreateEvent";
    
    /// <summary>
    /// User Id, can be expression
    /// </summary>
    public string UserId { get; set; }
    
    /// <summary>
    /// body content type (html or text)
    /// </summary>
    public string ContentType { get; set; }
    
    /// <summary>
    /// Body content (handlebars template)
    /// </summary>
    public string Content { get; set; }
    
    /// <summary>
    /// Appointment Subject
    /// </summary>
    public string Subject { get; set; }
    
    /// <summary>
    /// Appointment Start
    /// </summary>
    public string Start { get; set; }
    
    /// <summary>
    /// Appointment End
    /// </summary>
    public string End { get; set; }
    
    /// <summary>
    /// Location Address
    /// </summary>
    public string Address { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string PostalCode { get; set; }
    public string Country { get; set; }
    
    /// <summary>
    /// Not used (yet)
    /// </summary>
    public string LinkUrl { get; set; }
    
    /// <summary>
    /// Alias to be used when adding the object to the flow run
    /// </summary>
    public string Alias { get; set; }
}