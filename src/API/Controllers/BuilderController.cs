// using System;
// using System.Collections.Generic;
// using System.Linq;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using PI.Shared.Controllers;
// using PI.Shared.Exceptions;
// using PI.Shared.Form.Models;
// using PI.Shared.Models;
// using PI.Shared.Models.Layout;
//
// namespace Controllers;
//
// [Route("/api/v1/[controller]")]
// [Authorize("default")]
// public class BuilderController : APIController
// {
//     /// <summary>
//     /// Get list of field types to be added to the object 
//     /// </summary>
//     [Authorize("admin")]
//     [HttpPost("/api/v1/[controller]/{objectTypeName}/Add/DataView")]
//     public DataViewResponse GetUserAvailabilityAsync([FromRoute] string objectTypeName, [FromBody] DataViewRequest request)
//     {
//         return new DataViewResponse
//         {
//             Request = request,
//             ObjectType = objectTypeName,
//             View = new DataView
//             {
//                 Name = "Fields",
//                 Fields = new FormField[]
//                 {
//                     new TextField
//                     {
//                         Name = Model.IdFieldName,
//                         Label = "Type"
//                     },
//                     new TextField
//                     {
//                         Name = "name",
//                         Label = "Name"
//                     },
//                     new TextField
//                     {
//                         Name = "description",
//                         Label = "Description"
//                     },
//                 },
//                 Detail = new DataViewDetail
//                 {
//                     Page = "dataform://api/v1/Builder/" + objectTypeName + "/Add/{{id}}",
//                 },
//                 KeyField = Model.IdFieldName,
//                 IsSelectable = false,
//                 DefaultSort = "name",
//                 Filter = new[] { "name" }
//             },
//             Options = request.Breakpoint switch
//             {
//                 ScreenBreakpoint.ExtraSmall or ScreenBreakpoint.Small => CardDataViewOptions.Default,
//                 _ =>DataViewOptions.Default, 
//             }, 
//             Result = new[]
//             {
//                 new { _id = "text", Name = "Text", Description = "Any combination of characters, numbers and/or symbols" },
//                 new { _id = "url", Name = "URL", Description = "A valid URL (e.g. https://wwww.programinterface.com, mailto:crochik@crochik.com)" },
//                 new { _id = "number", Name = "Number", Description = "A number with an optional maximum number of decimal places" },
//                 new { _id = "checkbox", Name = "Checkbox", Description = "A two state value (e.g. true/false, check/unchecked, on/off, ...) with different visual representations" },
//                 new { _id = "select", Name = "Select", Description = "List of preset named values" },
//             }
//         };
//     }
//
//     /// <summary>
//     /// Get form to add a new field of the type
//     /// </summary>
//     [Authorize("admin")]
//     [HttpGet("/api/v1/[controller]/{objectTypeName}/Add/{fieldType}/DataForm")]
//     public Form GetAddFieldDataForm([FromRoute] string objectTypeName, [FromRoute] string fieldType)
//     {
//         var fields = UIElementFields()
//                 .Concat(FormFieldFields())
//             ;
//
//         fields = fieldType switch
//         {
//             "text" => fields.Concat(TextFieldFields()),
//             "url" => fields.Concat(TextFieldFields()),
//             "number" => fields.Concat(NumberFieldFields()),
//             "checkbox" => fields.Concat(CheckboxFieldFields()),
//             "select" => fields.Concat(SelectFieldFields()),
//             _ => throw new BadRequestException("Unknown field type"),
//         };
//
//         return new Form
//         {
//             Name = fieldType,
//             Fields = fields.ToArray(),
//             Actions = new[]
//             {
//                 new FormAction
//                 {
//                     Name = FormAction.Add,
//                 }
//             },
//         };
//     }
//
//     private FormField OptionsLink = new TextField
//     {
//         Name = $"{nameof(FormField.Options)}|{nameof(TextFieldOptions.LinkUrl)}",
//         Label = "Link",
//     };
//
//     private IEnumerable<FormField> SelectFieldFields()
//     {
//         yield return new TextField
//         {
//             Name = nameof(FormField.DefaultValue),
//             Label = "Default",
//         };
//         
//         yield return OptionsLink;
//
//         // TODO: add property defining the "type" for value 
//         // ...
//
//         yield return new DictionaryField
//         {
//             Name = $"{nameof(FormField.Options)}|{nameof(SelectFieldOptions.Items)}",
//             Label = "Items",
//             DictionaryFieldOptions = new DictionaryFieldOptions
//             {
//                 KeyFieldName = "itemKey",
//                 ValueFieldName = "itemValue",
//             }
//         };
//
//         yield return new TextField
//         {
//             Name = "itemKey",
//             Label = "Value",
//             Visible = new string[] { "false" },
//         };
//
//         yield return new TextField
//         {
//             Name = "itemValue",
//             Label = "Name",
//             Visible = new string[] { "false" },
//         };
//     }
//
//     private IEnumerable<FormField> CheckboxFieldFields()
//     {
//         yield return new CheckboxField
//         {
//             Name = nameof(FormField.DefaultValue),
//             Label = "Default",
//             CheckboxFieldOptions = new CheckboxFieldOptions
//             {
//                 Style = CheckboxFieldOptionsStyle.Dropdown,
//             }
//         };
//
//         yield return OptionsLink;
//
//         yield return new SelectField
//         {
//             Name = $"{nameof(FormField.Options)}|{nameof(CheckboxFieldOptions.Style)}",
//             Label = "Style",
//             SelectFieldOptions = new SelectFieldOptions
//             {
//                 Items = new Dictionary<string, string>
//                 {
//                     { nameof(CheckboxFieldOptionsStyle.Default), "Checkbox" },
//                     { nameof(CheckboxFieldOptionsStyle.Toggle), "Toggle" },
//                     { nameof(CheckboxFieldOptionsStyle.Button), "Button" },
//                     { nameof(CheckboxFieldOptionsStyle.Dropdown), "Dropdown" },
//                 }
//             }
//         };
//     }
//
//     private IEnumerable<FormField> NumberFieldFields()
//     {
//         yield return new NumberField
//         {
//             Name = nameof(FormField.DefaultValue),
//             Label = "Default",
//         };
//
//         yield return OptionsLink;
//
//         yield return new NumberField
//         {
//             Name = nameof(NumberFieldOptions.DecimalPlaces),
//             Label = "Decimal Places",
//             DefaultValue = 0,
//         };
//
//         yield return new SelectField
//         {
//             Name = $"{nameof(FormField.Options)}|{nameof(NumberFieldOptions.Style)}",
//             Label = "Style",
//             SelectFieldOptions = new SelectFieldOptions
//             {
//                 Items = new Dictionary<string, string>
//                 {
//                     { nameof(NumberFieldOptionsStyle.Currency), "Currency" },
//                     { nameof(NumberFieldOptionsStyle.Rating), "Rating" },
//                     { nameof(NumberFieldOptionsStyle.Price), "Price" },
//                 }
//             }
//         };
//     }
//
//     private IEnumerable<FormField> TextFieldFields()
//     {
//         yield return new TextField
//         {
//             Name = nameof(FormField.DefaultValue),
//             Label = "Default",
//         };
//
//         yield return OptionsLink;
//         
//         yield return new CheckboxField
//         {
//             Name = $"{nameof(FormField.Options)}|{nameof(TextFieldOptions.Multline)}",
//             Label = "Multiline",
//         };
//
//         yield return new SelectField
//         {
//             Name = $"{nameof(FormField.Options)}|{nameof(TextFieldOptions.ContentType)}",
//             Label = "Content Type",
//             SelectFieldOptions = new SelectFieldOptions
//             {
//                 Items = new Dictionary<string, string>
//                 {
//                     { "text/plain", "Plain" },
//                     { "text/html", "HTML" },
//                 }
//             }
//         };
//     }
//
//     private IEnumerable<FormField> FormFieldFields()
//     {
//         yield return new CheckboxField
//         {
//             Name = nameof(FormField.IsRequired),
//             Label = "Required?",
//         };
//     }
//
//     private IEnumerable<FormField> UIElementFields()
//     {
//         yield return new TextField
//         {
//             Name = nameof(UIElement.Name),
//             IsRequired = true,
//         };
//
//         yield return new TextField
//         {
//             Name = nameof(UIElement.Label),
//             IsRequired = false,
//         };
//
//         // enable
//
//         // visible 
//     }
// }