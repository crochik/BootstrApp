using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace PI.Shared.OpenAPI;

public class DefaultGroupNameConvention : IControllerModelConvention
{
    private readonly string _defaultGroupName;

    public DefaultGroupNameConvention(string defaultGroupName)
    {
        _defaultGroupName = defaultGroupName;
    }
        
    public void Apply(ControllerModel controller)
    {
        controller.ApiExplorer.GroupName ??= _defaultGroupName;
    }
}