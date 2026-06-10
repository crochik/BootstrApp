using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using PI.Shared.Controllers;
using PI.Shared.Form.Models;

namespace Controllers;

[Route("/api/v1/[controller]")]
public class FieldController : APIController
{
    [HttpGet]
    public IActionResult GetFieldTypes()
    {
        var list = typeof(FormField)
            .GetCustomAttributes(typeof(JsonSubTypes.JsonSubtypes.KnownSubTypeAttribute), true)
            .OfType<JsonSubTypes.JsonSubtypes.KnownSubTypeAttribute>()
            .Select(x => x.AssociatedValue);

        return Ok(list);
    }

    [HttpGet("/api/v1/[controller]({type})")]
    public IActionResult GetFieldType(string type)
    {
        var found = typeof(FormField)
            .GetCustomAttributes(typeof(JsonSubTypes.JsonSubtypes.KnownSubTypeAttribute), true)
            .OfType<JsonSubTypes.JsonSubtypes.KnownSubTypeAttribute>()
            .FirstOrDefault(x => x.AssociatedValue.ToString() == type);

        var form = new PI.Shared.Form.Models.Form
        {
            Name = found.SubType.Name,
            Title = found.SubType.Name,
            Fields = getFields().ToArray(),
        };

        return Ok(form);

        IEnumerable<FormField> getFields()
        {
            yield return new TextField
            {
                Name = nameof(UIElement.Name),
                IsRequired = true,
            };
            yield return new TextField
            {
                Name = nameof(UIElement.Label),
            };
            // Enable
            // Visible

            // Type (RO)
            // DefaultValue (Object?)
            // Style (Object?)
            yield return new CheckboxField
            {
                Name = nameof(FormField.IsRequired),
            };

            // TODO: use object
            // ...
            yield return new ObjectField
            {
                Name = nameof(FormField.Options),
                ObjectFieldOptions = new ObjectFieldOptions
                {
                    ObjectType = $"{found.SubType.Name}Options",
                },
                IsRequired = true,
            };

            // Options
            yield return new UrlField
            {
                Name = $"{nameof(FormField.Options)}|{nameof(FieldOptions.LinkUrl)}",
            };

            var optionsType = $"{found.SubType.Name}Options";
            switch (optionsType)
            {
                case nameof(TextFieldOptions):
                    yield return new CheckboxField
                    {
                        Name = nameof(TextFieldOptions.Multline),
                    };
                    break;

                case nameof(LabelFieldOptions):
                    yield return new CheckboxField
                    {
                        Name = nameof(TextFieldOptions.Multline),
                    };
                    yield return new SelectField
                    {
                        Name = nameof(LabelFieldOptions.Style),
                        SelectFieldOptions = new SelectFieldOptions
                        {
                            Items = new Dictionary<string, string>
                            {
                                {nameof(LabelStyle.Normal), nameof(LabelStyle.Normal)},
                                {nameof(LabelStyle.Header), nameof(LabelStyle.Header)},
                                {nameof(LabelStyle.Subheader), "Sub Header"},
                                {nameof(LabelStyle.Subheader2), "Sub Header2"},
                                {nameof(LabelStyle.HTML), "HTML"},
                            },
                        },
                    };
                    yield return new SelectField
                    {
                        Name = nameof(LabelFieldOptions.Color),
                        SelectFieldOptions = new SelectFieldOptions
                        {
                            Items = new Dictionary<string, string>
                            {
                                {nameof(PalletColor.Default), nameof(PalletColor.Default)},
                                {nameof(PalletColor.Error), nameof(PalletColor.Error)},
                                {nameof(PalletColor.Primary), nameof(PalletColor.Primary)},
                                {nameof(PalletColor.Secondary), nameof(PalletColor.Secondary)},
                                {nameof(PalletColor.TextPrimary), nameof(PalletColor.TextPrimary)},
                                {nameof(PalletColor.TextSecondary), nameof(PalletColor.TextSecondary)},                                
                            },
                        },
                    };
                    break;
            }
        }
    }
}