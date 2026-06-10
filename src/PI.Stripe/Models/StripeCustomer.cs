using Crochik.Mongo;
using PI.Shared.Models;

namespace Models;

[BsonCollection("stripe.Customer")]
public class StripeCustomer : Model<string>
{
    /// <summary>
    /// This is just a "reference" holder for the identity
    /// THIS IS NOT the identity saved when the customer is created by the API
    /// </summary>
    public EntityIdentity Identity { get; set; }
}