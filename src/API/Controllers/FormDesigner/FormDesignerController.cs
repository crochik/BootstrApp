using System;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Form.Models;
using PI.Shared.Requests;
using PI.Shared.Services;

namespace Controllers.FormDesigner;

[Authorize("admin")]
[Route("/api/v1/[controller]")]
public class FormDesignerController : APIController
{
    private readonly ObjectTypeService _objectTypeService;

    public FormDesignerController(ObjectTypeService _objectTypeService)
    {
        this._objectTypeService = _objectTypeService;
    }
    
    /// <summary>
    /// Get form to add field to form 
    /// </summary>
    [HttpGet("Field({fieldObjectTypeName})/Add/DataForm")]
    public async Task<Form> GetAddFieldFormAsync([FromRoute] string fieldObjectTypeName)
    {
        var objectType = await _objectTypeService.GetAsync(Context, fieldObjectTypeName);
        if (objectType == null || objectType.GetLoadedBaseObjectTypeNames().All(x => x != "Field") || !objectType.IsEmbedded)
        {
            return Form.BuildErrorForm($"{fieldObjectTypeName} is not a valid field");
        }

        var form = await _objectTypeService.GetAddDataFormAsync(Context, objectType);

        form.Title = $"New {objectType.Label ?? objectType.Description ?? objectType.Name}";
        form.Menu = null; // remove design, ...

        return form;
    }

    [HttpPost("Field({fieldObjectTypeName})/Add/DataForm")]
    public DataFormActionResponse AddFieldAsync([FromBody] DataFormActionRequest request) => DataFormActionResponse.Error(request, "Can't add embedded object");

    /// <summary>
    /// not "standard" form builder since we need the client to send the data to be edited  
    /// </summary>
    [HttpPut("Field({fieldObjectTypeName})/Edit/DataForm")]
    public async Task<Form> GetEditFieldFormAsync([FromRoute] string fieldObjectTypeName, [FromBody] ExpandoObject field)
    {
        var objectType = await _objectTypeService.GetAsync(Context, fieldObjectTypeName);
        if (objectType == null || objectType.GetLoadedBaseObjectTypeNames().All(x => x != "Field") || !objectType.IsEmbedded)
        {
            return Form.BuildErrorForm($"{fieldObjectTypeName} is not a valid field");
        }

        var form = await _objectTypeService.GetEditDataFormAsync(Context, objectType, Guid.Empty, field);

        form.Title = $"Edit {objectType.Label ?? objectType.Description ?? objectType.Name}";
        form.Menu = null; // remove design, ...

        return form;
    }
    
    [HttpPost("Field({fieldObjectTypeName})/Edit/DataForm")]
    public DataFormActionResponse EditFieldAsync([FromBody] DataFormActionRequest request) => DataFormActionResponse.Error(request, "Can't edit embedded object");
 
    /// <summary>
    /// Get form to add action to form 
    /// </summary>
    [HttpGet("FormAction/Add/DataForm")]
    public async Task<Form> GetAddFormActionFormAsync()
    {
        var objectType = await _objectTypeService.GetAsync(Context, nameof(FormAction));
        if (objectType == null)
        {
            return Form.BuildErrorForm($"Missing Object definition");
        }

        var form = await _objectTypeService.GetAddDataFormAsync(Context, objectType);

        form.Title = $"New {objectType.Label ?? objectType.Description ?? objectType.Name}";
        form.Menu = null; // remove design, ...

        return form;
    }

    [HttpPost("FormAction/Add/DataForm")]
    public DataFormActionResponse AddFormActionAsync([FromBody] DataFormActionRequest request) => DataFormActionResponse.Error(request, "Can't add embedded object");

    /// <summary>
    /// not "standard" form builder since we need the client to send the data to be edited  
    /// </summary>
    [HttpPut("FormAction/Edit/DataForm")]
    public async Task<Form> GetEditFormActionFormAsync([FromBody] ExpandoObject formAction)
    {
        var objectType = await _objectTypeService.GetAsync(Context, nameof(FormAction));
        if (objectType == null)
        {
            return Form.BuildErrorForm($"Missing Object definition");
        }

        var form = await _objectTypeService.GetEditDataFormAsync(Context, objectType, Guid.Empty, formAction);

        form.Title = $"Edit {objectType.Label ?? objectType.Description ?? objectType.Name}";
        form.Menu = null; // remove design, ...

        return form;
    }
    
    [HttpPost("FormAction/Edit/DataForm")]
    public DataFormActionResponse EditFormActionAsync([FromBody] DataFormActionRequest request) => DataFormActionResponse.Error(request, "Can't edit embedded object");
    
    /// <summary>
    /// not "standard" form builder since we need the client to send the data to be edited  
    /// </summary>
    [HttpPut("Form/Edit/DataForm")]
    public async Task<Form> GetEditFormFormAsync([FromBody] ExpandoObject form)
    {
        // TODO: use a model without fields, actions and menu?
        // ...
        var objectType = await _objectTypeService.GetAsync(Context, nameof(Form));
        if (objectType == null)
        {
            return Form.BuildErrorForm($"Missing Object definition");
        }

        var result = await _objectTypeService.GetEditDataFormAsync(Context, objectType, Guid.Empty, form);

        result.Title = $"Edit {objectType.Label ?? objectType.Description ?? objectType.Name}";
        result.Menu = null; // remove design, ...
        result.Actions = result?.Actions.Where(x => x.Name != "Delete").ToArray();

        return result;
    }
    
    [HttpPost("Form/Edit/DataForm")]
    public DataFormActionResponse EditFormAsync([FromBody] DataFormActionRequest request) => DataFormActionResponse.Error(request, "Can't edit embedded object");
}