using System.Collections.Generic;
using System.Dynamic;
using PI.Shared.Extensions;
using PI.Shared.Models;

namespace PI.Openphone.Models;

public class OpenPhoneEventData
{
    public ExpandoObject Object { get; set; }

    public IDictionary<string, object> ObjectProperties => Object;
    public string From => ObjectProperties.TryGetStrParam("from", out var value) ? value : "?";
    public string To => ObjectProperties.TryGetStrParam("to", out var value) ? value : "?";

    public string DisplayTo => GetDisplay(To);
    public string DisplayFrom => GetDisplay(From);

    public string GetDisplay(string phoneNumber)
    {
        return phoneNumber != null && PhoneNumber.TryParse(phoneNumber, out var phone) ? phone.Display : phoneNumber;
    }

    public string Body => ObjectProperties.TryGetStrParam("body", out var value) ? value : null;
    public string FirstName => ObjectProperties.TryGetStrParam("firstName", out var value) ? value : null;
    public string LastName => ObjectProperties.TryGetStrParam("lastName", out var value) ? value : null;
    public string Company => ObjectProperties.TryGetStrParam("company", out var value) ? value : null;
    public string Status => ObjectProperties.TryGetStrParam("status", out var value) ? value : null;

    public IEnumerable<string> NameParts
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(FirstName)) yield return FirstName;
            if (!string.IsNullOrWhiteSpace(LastName)) yield return LastName;
        }
    }

    public string FullName => string.Join(' ', NameParts);

    public string FullNameWithCompany
    {
        get
        {
            var fullName = FullName;
            var company = Company;
            if (string.IsNullOrWhiteSpace(company)) return fullName;
            if (string.IsNullOrWhiteSpace(fullName)) return company;
            return $"{fullName} ({company})";
        }
    }

    public string Direction => ObjectProperties.TryGetStrParam("direction", out var value)
        ? value switch
        {
            "incoming" => "Incoming",
            "outgoing" => "Outgoing",
            _ => "?"
        }
        : null;
}