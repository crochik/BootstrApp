using System;
using PI.Shared.Models;

namespace PI.Shared.Services
{
    public static class NameFromParts
    {
        public static object Calculate(FieldMapperConfig config, object body, IIndexedProperties lead)
        {
            var firstName = lead.GetFirstName();
            var lastName = lead.GetLastName();

            return firstName == null ? lastName :
                (lastName == null ? firstName :
                $"{firstName} {lastName}");
        }

        private static string GetFirstName(this IIndexedProperties lead)
        {
            var name = lead[Lead.PropertyName_FirstName];
            if (!string.IsNullOrEmpty(name)) return name;

            name = lead[Lead.PropertyName_Name];
            return GetFirstName(name);
        }

        private static string GetLastName(this IIndexedProperties lead)
        {
            var name = lead[Lead.PropertyName_LastName];
            if (!string.IsNullOrEmpty(name)) return name;

            name = lead[Lead.PropertyName_Name];
            
            return GetLastName(name);
        }

        public static string GetFirstName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var parts = name.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            // TODO: Mr, Mrs,  ...
            // ...
            return parts.Length > 0 ? parts[0] : null;
        }

        public static string GetLastName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var parts = name.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            // TODO: Jr, Sr, I, II, ...
            // ...
            return parts.Length > 1 ? parts[^1] : null;
        }
    }
}
