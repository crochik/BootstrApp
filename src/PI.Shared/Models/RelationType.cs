namespace PI.Shared.Models;

public enum RelationType
{
    /// <summary>
    /// Explicitly defined relation from one object to other object(s)
    /// e.g. using the criteria it will resolve to zero, one or multiple objects
    /// </summary>
    OneToMany,
    
    /// <summary>
    /// Explicitly defined relation from one object to other object
    /// e.g. using the criteria, it will resolve to zero or one object
    /// (should really be called ManyToOne, don't know why it started as OneToOne) 
    /// </summary>
    OneToOne,
    // ManyToOne,

    /// <summary>
    /// Implicit relation added to the base object that is extended by
    /// another object
    /// </summary>
    Extended,
    
    /// <summary>
    /// Implicit relation added to the object that is referenced by
    /// another object *ReferenceField
    /// </summary>
    Referenced,
    
    /// <summary>
    /// Implicit relation added to the object when it is embedded by
    /// another object
    /// </summary>
    Embedded,
    
    // ManyToMany,
}