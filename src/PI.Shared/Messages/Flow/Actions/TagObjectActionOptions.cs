using System;

namespace Messages.Flow;

public class TagObjectActionOptions : ActionOptions, IActionOptionsForRunner
{
    /// <summary>
    /// Object name in "Objects.{...}"
    /// If empty, will be the target of the flow. 
    /// </summary>
    public string ObjectPath { get; set; }
    
    /// <summary>
    /// Always fire the next event (even when no tag was added)
    /// only implemented on the runner as now
    /// </summary>
    public bool AlwaysFireNextEvent { get; set; }
    
    /// <summary>
    /// Tag (only for TagsField)
    /// (for runner) it can be a template ...that can optionally point at a string[]
    /// </summary>
    public string Tag { get; set; }

    /// <summary>
    /// Field Name to tag (TagsField or CheckboxField) 
    /// </summary>
    public string FieldName { get; set; }

    /// <summary>
    /// Event to fire if object is tagged 
    /// </summary>
    public Guid? NextEventId { get; set; }

    /// <summary>
    /// Event to fire when the object is not tagged
    /// </summary>
    public Guid? AlreadyTaggedEventId { get; set; }

    public override ActionOutput[] Output { get; set; }
}