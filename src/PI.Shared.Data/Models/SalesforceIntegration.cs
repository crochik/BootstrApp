using System;

namespace PI.Shared.Data.Models;

// TODO: "replace with a first class SalesforceEntityIntegration that will have first class properties"
// ... it can break the world so be careful! :)
/// <summary>
/// Not used for serialization (does not extend EntityIntegration)
/// It is just a container for Data 
/// </summary>
[Obsolete]
public abstract class SalesforceIntegration
{
    public class Data
    {
        public Guid? OverrideEntityId { get; set; }
    }

    public class Authentication
    {
    }
}