namespace PI.Shared.Models.Interfaces;

public interface IFile : IWithParent
{
    
}

public interface IImageFile : IFile, IWithLocation
{
    
}
