using Messages.Flow;

namespace PI.CompanyCam.Services;

public class AbstractCompanyCamActionOptions : ActionOptions
{
    /// <summary>
    /// EntityId to be used to determine the companyCam account
    /// </summary>
    public string EntityId { get; set; }

    /// <summary>
    /// Email of the user to be used as the "creator" of the document (optional)
    /// </summary>
    public string CreatedByEmail { get; set; }

    /// <summary>
    /// Alias for the object created (optional) 
    /// </summary>
    public string Alias { get; set; }
}

public class CompanyCamAddDocumentActionOptions : AbstractCompanyCamActionOptions
{
    public enum FileType
    {
        Photo, 
        Document
    }
    
    /// <summary>
    /// Type of file
    /// </summary>
    public FileType Type { get; set; }

    /// <summary>
    /// Id of the company cam project to add the document to (expression)
    /// </summary>
    public string CompanyCamProjectId { get; set; }

    /// <summary>
    /// document/photo file name (expression)
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Unauthorized Url to get file contents (expression) 
    /// </summary>
    public string SourceUrl { get; set; }

    /// <summary>
    /// Expression/Date in string format to be used as create date
    /// </summary>
    public string CreatedDate { get; set; }
    
    /// <summary>
    /// Expression to resolve to one (string) or more tags (string[])
    /// </summary>
    public string Tags { get; set; }
}