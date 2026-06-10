# Indices 
- db.getCollection('stripe.Customer').createIndex({EntityId: 1})
- db.getCollection('stripe.Charge').createIndex({CustomerId:1})
- db.getCollection('stripe.Charge').createIndex({CreatedOn: -1, CustomerId:1})
- db.getCollection('stripe.Charge').createIndex({ExternalId: 1}, {unique: true})