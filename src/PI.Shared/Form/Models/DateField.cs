using System;
using PI.Shared.Models;

namespace PI.Shared.Form.Models;

public class DateField : FormField
{
    public override object AutoConvert(object value)
    {
        if (value is string strValue)
        {
            return DateTime.Parse(strValue);
        }

        // TBD
        return value;
    }

    public override BackingType GetBackingType() => BackingType.DateTime;
}