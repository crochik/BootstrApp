## FCI Objects 
## Lead

```
+--------------------------------------+--------------------------------+-----------+-----+-----+-----+-----+-----+
|                 NAME                 |             LABEL              |   TYPE    | CST | CLC | ADD | UPD | REQ |
+--------------------------------------+--------------------------------+-----------+-----+-----+-----+-----+-----+
| Additional_Email__c                  | Additional Email               | string    |  C  |     |  A  |  U  |     |
| Additional_Email_2__c                | Additional Email 2             | string    |  C  |     |  A  |  U  |     |
| Additional_Phone__c                  | Additional Phone               | phone     |  C  |     |  A  |  U  |     |
| Additional_Phone_2__c                | Additional Phone 2             | phone     |  C  |     |  A  |  U  |     |
| Address                              | Address                        | address   |     |     |     |     |     |
| Address_Complement__c                | Address Complement             | string    |  C  |     |  A  |  U  |     |
| Address_Complement_2__c              | Address Complement 2           | string    |  C  |     |  A  |  U  |     |
| Address_Description__c               | Address Description            | string    |  C  |     |  A  |  U  |     |
| Address_Description_2__c             | Address Description 2          | string    |  C  |     |  A  |  U  |     |
| AnnualRevenue                        | Annual Revenue                 | currency  |     |     |  A  |  U  |     |
| Booking_Link__c                      | Book Appointment in PI         | string    |  C  |  F  |     |     |     |
| Branch_Opt_Out_of_Call_Center__c     | Branch Opt-Out of Call Center  | boolean   |  C  |  F  |     |     |  R  |
| Branch_Opt_Out_of_Lead_Conversion__c | Branch Opt-Out of Lead         | boolean   |  C  |  F  |     |     |  R  |
|                                      | Conversion                     |           |     |     |     |     |     |
| Call_Center_Lead_Exception__c        | Call Center Lead Exception     | picklist  |  C  |     |  A  |  U  |     |
| Call_Instructions__c                 | Call Instructions              | picklist  |  C  |     |  A  |  U  |     |
| City                                 | City                           | string    |     |     |  A  |  U  |     |
| City_2__c                            | City 2                         | string    |  C  |     |  A  |  U  |     |
| Company                              | Company                        | string    |     |     |  A  |  U  |  R  |
| Contact__c                           | Contact                        | reference |  C  |     |  A  |  U  |     |
| ConvertedAccountId                   | Converted Customer ID          | reference |     |     |     |     |     |
| ConvertedContactId                   | Converted Contact ID           | reference |     |     |     |     |     |
| ConvertedDate                        | Converted Date                 | date      |     |     |     |     |     |
| ConvertedOpportunityId               | Converted Opportunity ID       | reference |     |     |     |     |     |
| Country                              | Country                        | string    |     |     |  A  |  U  |     |
| Country_2__c                         | Country 2                      | string    |  C  |     |  A  |  U  |     |
| CreatedById                          | Created By ID                  | reference |     |     |     |     |  R  |
| CreatedDate                          | Created Date                   | datetime  |     |     |     |     |  R  |
| DB_Created_Date_without_Time__c      | DB Created Date without Time   | date      |  C  |  F  |     |     |     |
| DB_Lead_Age__c                       | DB Lead Age                    | double    |  C  |  F  |     |     |     |
| Description                          | Description                    | textarea  |     |     |  A  |  U  |     |
| Description_Additional_Email__c      | Description Additional Email   | string    |  C  |     |  A  |  U  |     |
| Description_Additional_Email_2__c    | Description Additional Email 2 | string    |  C  |     |  A  |  U  |     |
| Description_Additional_Phone__c      | Description Additional Phone   | string    |  C  |     |  A  |  U  |     |
| Description_Additional_Phone_2__c    | Description Additional Phone 2 | string    |  C  |     |  A  |  U  |     |
| Description_Email__c                 | Description (Email)            | string    |  C  |     |  A  |  U  |     |
| Description_Email_2__c               | Description (Email 2)          | string    |  C  |     |  A  |  U  |     |
| Description_Email_3__c               | Description (Email 3)          | string    |  C  |     |  A  |  U  |     |
| Description_Email_4__c               | Description (Email 4)          | string    |  C  |     |  A  |  U  |     |
| Description_Mobile_Phone__c          | Description (Mobile Phone)     | string    |  C  |     |  A  |  U  |     |
| Description_Phone__c                 | Description (Phone)            | string    |  C  |     |  A  |  U  |     |
| Design_Associate__c                  | Design Associate               | reference |  C  |     |  A  |  U  |     |
| DoNotCall                            | Do Not Call                    | boolean   |     |     |  A  |  U  |  R  |
| Dynamic_Session_Link__c              | Dynamic Session Link           | string    |  C  |  F  |     |     |     |
| E_mail_Instructions__c               | E-mail Instructions            | picklist  |  C  |     |  A  |  U  |     |
| E_mail_Instructions_Id__c            | E-mail Instructions Id         | string    |  C  |     |  A  |  U  |     |
| Email                                | Email                          | email     |     |     |  A  |  U  |     |
| Email_2__c                           | Email 2                        | string    |  C  |     |  A  |  U  |     |
| Email_3__c                           | Email 3                        | string    |  C  |     |  A  |  U  |     |
| Email_4__c                           | Email 4                        | string    |  C  |     |  A  |  U  |     |
| Email_Description__c                 | Email Description              | string    |  C  |     |  A  |  U  |     |
| Email_to_acc__c                      | Email to acc                   | string    |  C  |  F  |     |     |     |
| EmailBouncedDate                     | Email Bounced Date             | datetime  |     |     |     |  U  |     |
| EmailBouncedReason                   | Email Bounced Reason           | string    |     |     |     |  U  |     |
| Estimated_number_of_rooms__c         | Estimated number of rooms      | double    |  C  |     |  A  |  U  |     |
| et4ae5__HasOptedOutOfMobile__c       | Mobile Opt Out                 | boolean   |  C  |     |  A  |  U  |  R  |
| et4ae5__Mobile_Country_Code__c       | Mobile Country Code            | picklist  |  C  |     |  A  |  U  |     |
| Exception_Reason__c                  | Exception Reason               | picklist  |  C  |     |  A  |  U  |     |
| External_Lead__c                     | External Lead                  | boolean   |  C  |     |  A  |  U  |  R  |
| Fax                                  | Fax                            | phone     |     |     |  A  |  U  |     |
| FirstName                            | First Name                     | string    |     |     |  A  |  U  |     |
| GeocodeAccuracy                      | Geocode Accuracy               | picklist  |     |     |  A  |  U  |     |
| Guid__c                              | Guid                           | string    |  C  |     |  A  |  U  |     |
| HasOptedOutOfEmail                   | Email Opt Out                  | boolean   |     |     |  A  |  U  |  R  |
| HasOptedOutOfFax                     | Fax Opt Out                    | boolean   |     |     |  A  |  U  |  R  |
| How_did_you_hear_about_us__c         | How did you hear about us?     | string    |  C  |     |  A  |  U  |     |
| Id                                   | Lead ID                        | id        |     |     |     |     |  R  |
| IndividualId                         | Individual ID                  | reference |     |     |  A  |  U  |     |
| Industry                             | Industry                       | picklist  |     |     |  A  |  U  |     |
| INET_BRANCHCODE__c                   | INET BRANCHCODE                | string    |  C  |     |  A  |  U  |     |
| INET_BRANCHID__c                     | INET_BRANCHID                  | string    |  C  |     |  A  |  U  |     |
| INET_Call_Instructions_Id__c         | Call Instructions Id           | string    |  C  |     |  A  |  U  |     |
| INET_CONSUMER_NOTES__c               | Customer Notes                 | textarea  |  C  |     |  A  |  U  |     |
| INET_CONSUMERID__c                   | INET CONSUMERID                | string    |  C  |     |  A  |  U  |     |
| Inet_CustomerID__c                   | INET CustomerID                | string    |  C  |     |  A  |  U  |     |
| INET_LeadSourceUID__c                | Lead Source                    | string    |  C  |     |  A  |  U  |     |
| INET_NPS_Instructions_Id__c          | NPS Instructions Id            | string    |  C  |     |  A  |  U  |     |
| INET_SELECTION__c                    | Project Name                   | string    |  C  |     |  A  |  U  |     |
| INET_SELECTION_NOTES__c              | Project Notes                  | textarea  |  C  |     |  A  |  U  |     |
| INET_Service_Territory__c            | Branch                         | reference |  C  |     |  A  |  U  |     |
| INET_STATUSID__c                     | INET STATUSID                  | string    |  C  |     |  A  |  U  |     |
| INET_USERID__c                       | INET USERID                    | string    |  C  |     |  A  |  U  |     |
| INET_USERLOGIN__c                    | INET USERLOGIN                 | string    |  C  |     |  A  |  U  |     |
| InspireNET_Status__c                 | InspireNET Status              | picklist  |  C  |     |  A  |  U  |     |
| Is_Imported__c                       | Is Imported                    | boolean   |  C  |     |  A  |  U  |  R  |
| IsConverted                          | Converted                      | boolean   |     |     |  A  |     |  R  |
| IsDeleted                            | Deleted                        | boolean   |     |     |     |     |  R  |
| IsUnreadByOwner                      | Unread By Owner                | boolean   |     |     |  A  |  U  |  R  |
| Jigsaw                               | Data.com Key                   | string    |     |     |  A  |  U  |     |
| JigsawContactId                      | Jigsaw Contact ID              | string    |     |     |     |     |     |
| LastActivityDate                     | Last Activity                  | date      |     |     |     |     |     |
| LastModifiedById                     | Last Modified By ID            | reference |     |     |     |     |  R  |
| LastModifiedDate                     | Last Modified Date             | datetime  |     |     |     |     |  R  |
| LastName                             | Last Name                      | string    |     |     |  A  |  U  |  R  |
| LastReferencedDate                   | Last Referenced Date           | datetime  |     |     |     |     |     |
| LastTransferDate                     | Last Transfer Date             | date      |     |     |     |     |     |
| LastViewedDate                       | Last Viewed Date               | datetime  |     |     |     |     |     |
| Latitude                             | Latitude                       | double    |     |     |  A  |  U  |     |
| Lead_changed_to_Dead__c              | Lead changed to Dead           | datetime  |  C  |     |  A  |  U  |     |
| LeadSource                           | Lead Source                    | picklist  |     |     |  A  |  U  |     |
| Longitude                            | Longitude                      | double    |     |     |  A  |  U  |     |
| Marketing_ID__c                      | Marketing ID                   | string    |  C  |  F  |     |     |     |
| MasterRecordId                       | Master Record ID               | reference |     |     |     |     |     |
| MobilePhone                          | Mobile Phone                   | phone     |     |     |  A  |  U  |     |
| Name                                 | Full Name                      | string    |     |     |     |     |  R  |
| Name__c                              | Name                           | string    |  C  |     |  A  |  U  |     |
| Notes__c                             | Notes                          | textarea  |  C  |     |  A  |  U  |     |
| NPS_Instructions__c                  | NPS Instructions               | picklist  |  C  |     |  A  |  U  |     |
| NumberOfEmployees                    | Employees                      | int       |     |     |  A  |  U  |     |
| OwnerId                              | Owner ID                       | reference |     |     |  A  |  U  |  R  |
| Phone                                | Phone                          | phone     |     |     |  A  |  U  |     |
| Phone_Description__c                 | Phone Description              | string    |  C  |     |  A  |  U  |     |
| PhotoUrl                             | Photo URL                      | url       |     |     |     |     |     |
| PIId__c                              | PIId                           | string    |  C  |     |  A  |  U  |     |
| PostalCode                           | Zip/Postal Code                | string    |     |     |  A  |  U  |     |
| Preferred_Date__c                    | Preferred Date                 | date      |  C  |     |  A  |  U  |     |
| Preferred_Hour_Range__c              | Preferred Hour Range           | string    |  C  |     |  A  |  U  |     |
| Project_Notes__c                     | Project Notes                  | textarea  |  C  |     |  A  |  U  |     |
| ProjectID__c                         | ProjectID                      | string    |  C  |     |  A  |  U  |     |
| Rating                               | Rating                         | picklist  |     |     |  A  |  U  |     |
| Referred_by__c                       | Referred by                    | string    |  C  |     |  A  |  U  |     |
| Referred_by_Contact__c               | Referred by                    | reference |  C  |     |  A  |  U  |     |
| Salutation                           | Salutation                     | picklist  |     |     |  A  |  U  |     |
| SelectionId__c                       | SelectionId                    | string    |  C  |     |  A  |  U  |     |
| State                                | State/Province                 | string    |     |     |  A  |  U  |     |
| State_Province_2__c                  | State/Province	2                | string    |  C  |     |  A  |  U  |     |
| Status                               | Status                         | picklist  |     |     |  A  |  U  |  R  |
| Street                               | Street                         | textarea  |     |     |  A  |  U  |     |
| Street_2__c                          | Street 2                       | string    |  C  |     |  A  |  U  |     |
| SystemModstamp                       | System Modstamp                | datetime  |     |     |     |     |  R  |
| Test_Marketing_Cloud__c              | Test Marketing Cloud           | boolean   |  C  |     |  A  |  U  |  R  |
| Title                                | Title                          | string    |     |     |  A  |  U  |     |
| Web_Abandoned__c                     | Web Abandoned                  | boolean   |  C  |     |  A  |  U  |  R  |
| Website                              | Website                        | url       |     |     |  A  |  U  |     |
| Zip_Postal_Code_2__c                 | Zip/Postal Code 2              | string    |  C  |     |  A  |  U  |     |
| ZipCode__c                           | ZipCode                        | reference |  C  |     |  A  |  U  |     |
+--------------------------------------+--------------------------------+-----------+-----+-----+-----+-----+-----+
```

## Account

```
+----------------------------------------+--------------------------------+-----------+-----+-----+-----+-----+-----+
|                  NAME                  |             LABEL              |   TYPE    | CST | CLC | ADD | UPD | REQ |
+----------------------------------------+--------------------------------+-----------+-----+-----+-----+-----+-----+
| AccountNumber                          | Customer Number                | string    |     |     |  A  |  U  |     |
| AccountSource                          | Customer Source                | picklist  |     |     |  A  |  U  |     |
| Active__c                              | Active                         | boolean   |  C  |     |  A  |  U  |  R  |
| Additional_Phone2__c                   | Additional Phone 2             | phone     |  C  |     |  A  |  U  |     |
| Address_description__c                 | Address Description            | textarea  |  C  |     |  A  |  U  |     |
| AnnualRevenue                          | Annual Revenue                 | currency  |     |     |  A  |  U  |     |
| Best_time_to_reach_you__c              | Best time to reach you         | picklist  |  C  |     |  A  |  U  |     |
| BillingAddress                         | Billing Address                | address   |     |     |     |     |     |
| BillingCity                            | Billing City                   | string    |     |     |  A  |  U  |     |
| BillingCountry                         | Billing Country                | string    |     |     |  A  |  U  |     |
| BillingGeocodeAccuracy                 | Billing Geocode Accuracy       | picklist  |     |     |  A  |  U  |     |
| BillingLatitude                        | Billing Latitude               | double    |     |     |  A  |  U  |     |
| BillingLongitude                       | Billing Longitude              | double    |     |     |  A  |  U  |     |
| BillingPostalCode                      | Billing Zip/Postal Code        | string    |     |     |  A  |  U  |     |
| BillingState                           | Billing State/Province         | string    |     |     |  A  |  U  |     |
| BillingStreet                          | Billing Street                 | textarea  |     |     |  A  |  U  |     |
| Branch__c                              | Branch                         | string    |  C  |     |  A  |  U  |     |
| BranchID__c                            | BranchID                       | reference |  C  |     |  A  |  U  |     |
| Call_Instructions__c                   | Call Instructions              | picklist  |  C  |     |  A  |  U  |     |
| Contact__c                             | Contact                        | reference |  C  |     |  A  |  U  |     |
| CreatedById                            | Created By ID                  | reference |     |     |     |     |  R  |
| CreatedDate                            | Created Date                   | datetime  |     |     |     |     |  R  |
| Description                            | Customer Description           | textarea  |     |     |  A  |  U  |     |
| Design_Associate__c                    | Design Associate               | reference |  C  |     |  A  |  U  |     |
| E_mail_Instructions__c                 | E-mail Instructions            | picklist  |  C  |     |  A  |  U  |     |
| Email2__c                              | Additional Email               | email     |  C  |     |  A  |  U  |     |
| Email3__c                              | Additional Email 2             | email     |  C  |     |  A  |  U  |     |
| Exclude_From_Reports__c                | Exclude From Reports           | boolean   |  C  |     |  A  |  U  |  R  |
| Fax                                    | Customer Fax                   | phone     |     |     |  A  |  U  |     |
| Home_Assessment__c                     | Appointment                    | reference |  C  |     |  A  |  U  |     |
| HomeAdvisor_ServiceMagic_Exceptions__c | HomeAdvisor/ServiceMagic       | picklist  |  C  |     |  A  |  U  |     |
|                                        | Exceptions                     |           |     |     |     |     |     |
| Id                                     | Customer ID                    | id        |     |     |     |     |  R  |
| Industry                               | Industry                       | picklist  |     |     |  A  |  U  |     |
| INET_Customer_Id__c                    | Customer_Id                    | string    |  C  |     |  A  |  U  |     |
| IsDeleted                              | Deleted                        | boolean   |     |     |     |     |  R  |
| IsTraining__c                          | IsTraining                     | boolean   |  C  |     |  A  |  U  |  R  |
| Jigsaw                                 | Data.com Key                   | string    |     |     |  A  |  U  |     |
| JigsawCompanyId                        | Jigsaw Company ID              | string    |     |     |     |     |     |
| LastActivityDate                       | Last Activity                  | date      |     |     |     |     |     |
| LastModifiedById                       | Last Modified By ID            | reference |     |     |     |     |  R  |
| LastModifiedDate                       | Last Modified Date             | datetime  |     |     |     |     |  R  |
| LastReferencedDate                     | Last Referenced Date           | datetime  |     |     |     |     |     |
| LastViewedDate                         | Last Viewed Date               | datetime  |     |     |     |     |     |
| Lead__c                                | Lead                           | reference |  C  |     |  A  |  U  |     |
| Lead_Marketing_ID__c                   | Lead Marketing ID              | string    |  C  |     |  A  |  U  |     |
| Mail_Gift_Instructions__c              | Mail/Gift Instructions         | picklist  |  C  |     |  A  |  U  |     |
| Main_Email__c                          | Main Email                     | email     |  C  |     |  A  |  U  |     |
| Marketing_ID__c                        | Marketing ID                   | string    |  C  |  F  |     |     |     |
| MasterRecordId                         | Master Record ID               | reference |     |     |     |     |     |
| Match_Phone__c                         | Match Phone                    | string    |  C  |  F  |     |     |     |
| Name                                   | Customer Name                  | string    |     |     |  A  |  U  |  R  |
| Notes__c                               | Notes                          | textarea  |  C  |     |  A  |  U  |     |
| NPS_Instructions__c                    | NPS Instructions               | picklist  |  C  |     |  A  |  U  |     |
| NumberOfEmployees                      | Employees                      | int       |     |     |  A  |  U  |     |
| OperatingHoursId                       | Operating Hour ID              | reference |     |     |  A  |  U  |     |
| OwnerId                                | Customer Owner                 | reference |     |     |  A  |  U  |  R  |
| Ownership                              | Ownership                      | picklist  |     |     |  A  |  U  |     |
| ParentId                               | Parent Customer                | reference |     |     |  A  |  U  |     |
| Phone                                  | Customer Phone                 | phone     |     |     |  A  |  U  |     |
| Phone2__c                              | Mobile                         | phone     |  C  |     |  A  |  U  |     |
| Phone3__c                              | Additional Phone               | phone     |  C  |     |  A  |  U  |     |
| PhotoUrl                               | Photo URL                      | url       |     |     |     |     |     |
| Preferred_Date__c                      | Preferred Date                 | date      |  C  |     |  A  |  U  |     |
| Preferred_Hour_Range__c                | Preferred Hour Range           | string    |  C  |     |  A  |  U  |     |
| Preferred_method_of_contact__c         | Preferred method of contact    | picklist  |  C  |     |  A  |  U  |     |
| Primary_Contactid__c                   | Primary Contact ID             | string    |  C  |     |  A  |  U  |     |
| Project_Notes__c                       | Project Notes                  | textarea  |  C  |     |  A  |  U  |     |
| Rating                                 | Customer Rating                | picklist  |     |     |  A  |  U  |     |
| RecordTypeId                           | Customer Record Type           | reference |     |     |  A  |  U  |     |
| Share_with_all_users__c                | Share with all users           | boolean   |  C  |     |  A  |  U  |  R  |
| ShippingAddress                        | Shipping Address               | address   |     |     |     |     |     |
| ShippingCity                           | Shipping City                  | string    |     |     |  A  |  U  |     |
| ShippingCountry                        | Shipping Country               | string    |     |     |  A  |  U  |     |
| ShippingGeocodeAccuracy                | Shipping Geocode Accuracy      | picklist  |     |     |  A  |  U  |     |
| ShippingLatitude                       | Shipping Latitude              | double    |     |     |  A  |  U  |     |
| ShippingLongitude                      | Shipping Longitude             | double    |     |     |  A  |  U  |     |
| ShippingPostalCode                     | Shipping Zip/Postal Code       | string    |     |     |  A  |  U  |     |
| ShippingState                          | Shipping State/Province        | string    |     |     |  A  |  U  |     |
| ShippingStreet                         | Shipping Street                | textarea  |     |     |  A  |  U  |     |
| Sic                                    | SIC Code                       | string    |     |     |  A  |  U  |     |
| SicDesc                                | SIC Description                | string    |     |     |  A  |  U  |     |
| Site                                   | Customer Site                  | string    |     |     |  A  |  U  |     |
| SystemModstamp                         | System Modstamp                | datetime  |     |     |     |     |  R  |
| Test_Marketing_Cloud__c                | Test Marketing Cloud           | boolean   |  C  |     |  A  |  U  |  R  |
| TickerSymbol                           | Ticker Symbol                  | string    |     |     |  A  |  U  |     |
| Type                                   | Customer Type                  | picklist  |     |     |  A  |  U  |     |
| Website                                | Website                        | url       |     |     |  A  |  U  |     |
+----------------------------------------+--------------------------------+-----------+-----+-----+-----+-----+-----+
```

