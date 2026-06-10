using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson.Serialization.Attributes;

namespace PI.Shared.Models;

public class Lead : FlowObjectModel, IContact, IIndexedProperties, ITaggable
{
    private static readonly ContactContext _contactContext = new();

    public const string PropertyName_Address = "address";
    public const string PropertyName_City = "city";
    public const string PropertyName_Country = "country";
    public const string PropertyName_Email = "email";
    public const string PropertyName_FirstName = "firstName";
    public const string PropertyName_LastName = "lastName";

    [Obsolete("moving away from it ... we should only need/use hdyhau")]
    // the salesforce one uses leadSource and it is the string representation? :(
    public const string PropertyName_LeadSource = "leadsource";

    public const string PropertyName_HowDidYouHearAboutUs = "hdyhau";

    public const string PropertyName_Name = "name";
    public const string PropertyName_OptedOutOfEmail = "hasOptedOutOfEmail";
    public const string PropertyName_OptedOutOfFax = "hasOptedOutOfFax";
    public const string PropertyName_OptedOutOfMobile = "hasOptedOutOfMobile";
    public const string PropertyName_OptedOutOfPhone = "doNotCall";
    public const string PropertyName_Phone = "phone";
    public const string PropertyName_PostalCode = "postalCode";
    public const string PropertyName_State = "state";
    public const string PropertyName_Notes = "notes";
    public const string PropertyName_LeadFee = "leadFee";

    public Guid LeadTypeId { get; set; }
    public Guid? AssignedEntityId { get; set; }
    public Dictionary<string, object> Properties { get; set; }

    /// <summary>
    /// New property to track lead communication preferences (using updates from integrations)
    /// </summary>
    public Dictionary<string, string> CommunicationPreferences { get; set; }

    /// <summary>
    /// Date lead was converted into a prospect
    /// (e.g. Lead => Account in Sf)
    /// </summary>
    public DateTime? ConvertedOn { get; set; }

    /// <summary>
    /// track what we *think* it is the next appointment
    /// - being null, right now doesn't mean anything
    /// - it may be in the past (until we have some job automatically update it)
    /// - as now it may be cancelled (shouldn't)
    /// - there may be other active appointments for this lead
    /// - eventually we can have a job update with the next active appointment automatically
    /// </summary>
    public Guid? NextAppointmentId { get; set; }

    public IEnumerable<IIntegrationLead> GetIntegrations() => Integrations ?? Enumerable.Empty<IIntegrationLead>();
    public LeadIntegration[] Integrations { get; set; }

    /// list of entities with access to this lead
    [Obsolete]
    public Guid[] EntityIds { get; set; }

    [BsonIgnore] public IEntityContext Context => _contactContext;

    public Guid[] GroupMembership { get; set; }

    public Guid? ReplacedById { get; set; }

    public string TimeZoneId { get; set; }
    
    public decimal? LeadFee { get; set; }
    public string ExternalId { get; set; }
    public string TrustedFormCert { get; set; }
    // public Guid? JornayaId { get; set; }

    /// <summary>
    /// Tags
    /// </summary>
    public string[] Tags { get; set; }
    
    /// <summary>
    /// Lead is in timeout (e.g. Manually flagged to suppress other leads from being included)
    /// </summary>
    public bool? IsSuppressed { get; set; }

    // TODO: make properties into mutable/first class
    // ... 
    [BsonElement] public string Address => this[PropertyName_Address];

    [BsonElement] public string City => this[PropertyName_City];

    [BsonElement] public string Country => this[PropertyName_Country];

    [BsonElement] public string Email => this[PropertyName_Email];

    [BsonElement] public string Phone => this[PropertyName_Phone];

    [BsonElement] public string PostalCode => this[PropertyName_PostalCode];

    [BsonElement] public string State => this[PropertyName_State];
    
    [BsonElement] public GeoJSON.Point Location { get; set; }

    [BsonIgnore] private string _firstName;
    public string FirstName
    {
        get => _firstName ?? GetFirstName();
        set
        {
            _firstName = value;
            if (_firstName != null) SetValue(PropertyName_FirstName, _firstName);
        }
    }

    [BsonIgnore] private string _lastName;
    public string LastName
    {
        get => _lastName ?? GetLastName();
        set
        {
            _lastName = value;
            if (_lastName != null) SetValue(PropertyName_LastName, _lastName);
        }
    }
    
    [BsonIgnore] private string _normalizedPhoneNumber;
    public string NormalizedPhoneNumber
    {
        get => _normalizedPhoneNumber ?? GetNormalizedPhoneNumber();
        set => _normalizedPhoneNumber = value;
    }

    [BsonIgnore] private string _normalizedEmail;
    public string NormalizedEmail
    {
        get => _normalizedEmail ?? GetNormalizedEmail();
        set => _normalizedEmail = value;
    }
    
    [BsonIgnore] private string _notes;
    public string Notes
    {
        get => _notes ?? this[PropertyName_Notes];
        set
        {
            _notes = value;
            if (_notes != null) SetValue(PropertyName_Notes, _notes);
        }
    }

    public Lead()
    {
    }

    public string this[string key] => Properties != null && Properties.TryGetValue(key, out var value) ? value?.ToString() : null;

    public IEnumerable<KeyValuePair<string, object>> AllProperties()
        => Properties ?? (IEnumerable<KeyValuePair<string, object>>)Array.Empty<KeyValuePair<string, object>>();

    public IEnumerable<EntityIdentity> GetIdentities() => Enumerable.Empty<EntityIdentity>();

    public bool AddIfMissing(string key, object value)
    {
        if (value == null) return false;
        Properties ??= new Dictionary<string, object>();
        return Properties.TryAdd(key, value);
    }

    public bool SetValue(string key, object value)
    {
        Properties ??= new Dictionary<string, object>();
        if (Properties.TryGetValue(key, out var current))
        {
            if (value == null)
            {
                Properties.Remove(key);
                return true;
            }

            if (value.Equals(current) ||
                ((value is string || current is string) && string.Equals(value?.ToString(), current?.ToString()))
               )
            {
                return false;
            }
        }

        if (value == null)
        {
            return false;
        }

        Properties[key] = value;
        return true;
    }

    public string GetCommunicationPreference(string channel)
    {
        if (CommunicationPreferences == null || !CommunicationPreferences.TryGetValue(channel, out var preference))
        {
            return CommunicationPreference.Unknown;
        }

        return preference;
    }

    public string GetFirstName()
    {
        var name = this[Lead.PropertyName_FirstName];
        if (!string.IsNullOrEmpty(name)) return name;

        name = this[Lead.PropertyName_Name];
        return GetFirstName(name);
    }

    public static string GetFirstName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        var parts = name.Split(" ", StringSplitOptions.RemoveEmptyEntries);
        // TODO: Mr, Mrs,  ...
        // ...
        return parts.Length > 0 ? parts[0] : null;
    }

    public string GetLastName()
    {
        var name = this[Lead.PropertyName_LastName];
        if (!string.IsNullOrEmpty(name)) return name;

        name = this[Lead.PropertyName_Name];
        return GetLastName(name);
    }

    public static string GetLastName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        var parts = name.Split(" ", StringSplitOptions.RemoveEmptyEntries);
        // TODO: Jr, Sr, I, II, ...
        // ...
        return parts.Length > 1 ? parts[^1] : null;
    }

    private string GetNormalizedPhoneNumber()
    {
        var rawPhoneNumber = this[Lead.PropertyName_Phone];
        return GetNormalizedPhoneNumber(rawPhoneNumber);
    }

    private string GetNormalizedEmail()
    {
        var rawEmail = this[Lead.PropertyName_Email];
        return GetNormalizedEmail(rawEmail);
    }

    public static string GetNormalizedPhoneNumber(string rawPhoneNumber)
    {
        return PhoneNumber.TryParse(rawPhoneNumber, out var phoneNumber) ? phoneNumber?.International : rawPhoneNumber;
    }

    public static string GetNormalizedEmail(string rawEmail)
    {
        // TODO: check if it is valid
        // ...
        return string.IsNullOrWhiteSpace(rawEmail) ? null : rawEmail.ToLowerInvariant();
    }

    public static string GetPostalCodeForLookup(string postalCode)
    {
        if (string.IsNullOrWhiteSpace(postalCode)) return null;
        if (postalCode[0] >= '0' && postalCode[0] <= '9')
        {
            switch (postalCode.Length)
            {
                case < 4: return null;
                case > 5:
                    postalCode = postalCode[..5];
                    break;
            }

            if (!int.TryParse(postalCode, out var num)) return null;
            postalCode = num.ToString();
            if (postalCode.Length < 4) return null;
            return postalCode.Length == 4 ? "0" + postalCode : postalCode;
        }

        return postalCode.Length < 3 ? null : postalCode[..3].ToUpperInvariant();
    }
}