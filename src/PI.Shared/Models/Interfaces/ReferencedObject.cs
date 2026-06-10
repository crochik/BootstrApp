namespace PI.Shared.Models.Interfaces;

public class ReferencedObject
{
    public string ObjectType { get; set; }
    public object ObjectId { get; set; }
}

public class TaggedReferenceObject : ReferencedObject
{
    public string Tag { get; set; }
}

public interface IWithParent
{
    /// <summary>
    /// Parent Object if any
    /// </summary>
    public ReferencedObject Parent { get; set; }
}