using System;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;

namespace PI.Shared.Models;

public static class MongoConnectionExtensions
{
    public static async Task<Lead> CreateAsync(this MongoConnection connection, IEntityContext context, Lead lead)
    {
        var entityIds = context.GetEntityIds().ToArray();
        if (entityIds.Length < 1)
        {
            return null;
        }

        lead.EntityIds = entityIds;
        lead.AccountId = context.AccountId.Value;
        lead.LastModifiedOn = DateTime.UtcNow;
        lead.LastActor = context.Actor();
        lead.Name = lead[Lead.PropertyName_Name];
        
        lead.AddIfMissing(Lead.PropertyName_FirstName, lead.GetFirstName());
        lead.AddIfMissing(Lead.PropertyName_LastName, lead.GetLastName());
        
        return await connection.InsertAsync(lead);
    }

    /// <summary>
    /// Reset first class properties with Properties and update them (and the Properties) in the database
    /// </summary>
    public static async Task<Lead> UpdatePropertiesAsync(this MongoConnection connection, IEntityContext context, Lead lead)
    {
        var query = connection.Filter<Lead>()
            .Eq(x => x.Id, lead.Id)
            .Update
            .Set(x => x.LastModifiedOn, DateTime.UtcNow)
            .Set(x => x.LastActor, context.Actor())
            .ResetUsingProperties(lead)
        ;
        
        return await query.UpdateAndGetOneAsync();
    }

    /// <summary>
    /// Reset first class properties using Properties
    /// TODO: to be replaced with explicit set of those properties when needed
    /// ... 
    /// </summary>
    public static UpdateQuery<Lead> ResetUsingProperties(this UpdateQuery<Lead> update, Lead lead)
    {
        // update independent properties using values from Properties
        lead.Name = lead[Lead.PropertyName_Name];
        lead.FirstName = lead.GetFirstName();
        lead.LastName = lead.GetLastName();

        lead.NormalizedPhoneNumber = Lead.GetNormalizedPhoneNumber(lead[Lead.PropertyName_Phone]);
        lead.NormalizedEmail = Lead.GetNormalizedEmail(lead[Lead.PropertyName_Email]);
        
        // do not reset since it should have been set only during the creation
        // lead.Notes = lead[Lead.PropertyName_Notes]; 
        
        return update
            .SetOrUnset(x => x.Properties, lead.Properties)
            // these are (still) just proxies to the  Properties[?]
            .SetOrUnset(x => x.Address, lead.Address)
            .SetOrUnset(x => x.City, lead.City)
            .SetOrUnset(x => x.Country, lead.Country)
            .SetOrUnset(x => x.Email, lead.Email)
            .SetOrUnset(x => x.Phone, lead.Phone)
            .SetOrUnset(x => x.PostalCode, lead.PostalCode)
            .SetOrUnset(x => x.State, lead.State)
            // these are already independent 
            .SetOrUnset(x => x.Name, lead.Name)
            .SetOrUnset(x => x.FirstName, lead.FirstName)
            .SetOrUnset(x => x.LastName, lead.LastName)
            .SetOrUnset(x => x.NormalizedPhoneNumber, lead.NormalizedPhoneNumber)
            .SetOrUnset(x => x.NormalizedEmail, lead.NormalizedEmail)
            .SetOrUnset(x => x.Notes, lead.Notes)
        ;
    }
}