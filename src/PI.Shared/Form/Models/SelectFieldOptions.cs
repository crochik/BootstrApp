using System.Collections;
using System.Collections.Generic;
using Swashbuckle.AspNetCore.Annotations;

namespace PI.Shared.Form.Models;

[SwaggerSubType(typeof(MultiSelectFieldOptions), DiscriminatorValue = nameof(MultiSelectFieldOptions))]
[SwaggerSubType(typeof(ReferenceFieldOptions), DiscriminatorValue = nameof(ReferenceFieldOptions))]
[SwaggerSubType(typeof(MultiReferenceFieldOptions), DiscriminatorValue = nameof(MultiReferenceFieldOptions))]
[SwaggerSubType(typeof(RemoteFileFieldOptions), DiscriminatorValue = nameof(RemoteFileFieldOptions))]
[SwaggerSubType(typeof(AppointmentFieldOptions), DiscriminatorValue = nameof(AppointmentFieldOptions))]
[SwaggerSubType(typeof(ChatReferenceFieldOptions), DiscriminatorValue = nameof(ChatReferenceFieldOptions))]
[SwaggerSubType(typeof(LookupFieldOptions), DiscriminatorValue = nameof(LookupFieldOptions))]
public class SelectFieldOptions : FieldOptions
{
    public IDictionary Items { get; set; }
    
    /// <summary>
    /// Whether user can pick an unknown value (not included in the items)
    /// </summary>
    public bool? AllowUnknown { get; set; }
}

public class MultiSelectFieldOptions : SelectFieldOptions
{
}

public class BitwiseFlagFieldOptions : FieldOptions
{
    public IDictionary<string, string> Items { get; set; }
}
