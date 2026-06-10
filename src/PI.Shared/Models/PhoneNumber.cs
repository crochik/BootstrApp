using PhoneNumbers;

namespace PI.Shared.Models;

public class PhoneNumber
{
    private static PhoneNumberUtil Helper => PhoneNumberUtil.GetInstance();
    
    public string Raw { get; private init; }
    public string Display { get; private init; }
    public string International { get; private init; }
    
    public static bool TryParse(string rawPhoneNumber, out PhoneNumber phoneNumber)
    {
        if (string.IsNullOrEmpty(rawPhoneNumber))
        {
            phoneNumber = null;
            return false;
        }

        try
        {
            var parsed = Helper.Parse(rawPhoneNumber, "US");
            phoneNumber = new PhoneNumber
            {
                Raw = rawPhoneNumber,
                Display = Helper.Format(parsed, PhoneNumberFormat.NATIONAL),
                International = Helper.Format(parsed, PhoneNumberFormat.INTERNATIONAL), // Helper.FormatNumberForMobileDialing(parsed, "US", true),
            };
            
            return true;
        }
        catch (NumberParseException)
        {
            // invalid phone number, keep original
            phoneNumber = null;
            return false;
        }
    }
}