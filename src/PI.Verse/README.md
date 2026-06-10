# Verse.io

## Send Leads to verse.io
PI will send on schedule (probably once a day) leads to verse.io that satisfy the criteria to be contacted by verse.io 

The assumption right now is that we will make a POST to https://hooks.zapier.com/hooks/catch/8069779/bk7kx8x/ with a JSON (content-type) body with the following properties:
- Id (UUID used to uniquely identify a Lead)
- Firstname
- Lastname
- Email
- Phone
- Address (Street)
- State
- City
- PostalCode (Zip)
- Product (The product the lead is interested in)
- URL (Calendarbooking link)
- JWT ()

E.g.
``` 
{
    "id": "ff2ee7c8-fa9e-4200-94dd-07cd6bae5b99",
    "firstName": "Jane",
    "lastName": "Doe",
    "email": "jane@doe.com",
    "phone": "(222) 333-4444",
    "address": "1000 Nowhere Ln",
    "city": "NYC",
    "postalCode": "12345",
    "product": "Ceramic Tile",
    "url": "https://someverylongurl.com/with/lots/of/parts",
    "jwt: "ey…………………",
}
```

## Communicate changes in (Lead) Status from PI to Verse.io
PI will communicate with verse.io to update a lead status in verse.io (whether should be contacted or not)

From the document I assume these are the two endpoints PI would call to update the lead status.

### Set up Catchook URL for End Convo Qualified
POST https://hooks.zapier.com/hooks/catch/8069779/bk7knft/

Build integration with Verse to end the Convo qualified

Fields Needed:
- Email or Phone

### Set up Catchook URL for End Convo Unqualified
POST https://hooks.zapier.com/hooks/catch/8069779/bk7k7t6/

Build integration with Verse to end the Convo unqual

Fields Needed:
- Email or Phone

### Communicate changes in (Lead) Status from Verse.io to PI

Verse.io will make calls to PI to communicate changes in status for a lead. 

The different statuses: 
- Verse Working
- Verse Qualified
- Verse Unqualified

The request may include a "notes" field (with no character limit).
In the case of "Unqualified" it may include a "reason".
In the case an Appt was scheduled, it may include an Appointment Date/Time

Verse.io will make POST calls to an endpoint to be determined and pass the JWT received with the lead as the Authorization header. 

Valid statuses for simplicity would be "working", "qualified", "unqualified".

The body will be a json object. 

Examples:

Working:
```
{
    "status": "working",
    "notes": "will call every hour for the next month"
}
```

Unqualified:
```
{
    "status": "unqualified",
    "reason": "asked to be removed from contact list",
    "notes": "got annoyed that we called too often"
}
```

Qualified:
```
{
    "status": "qualified",
    "appointmentDate": "2022-07-05T17:00:00.000Z",
    "notes": "asked for rep to bring lots of carpet samples"
}
```

