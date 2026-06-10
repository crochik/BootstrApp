# PI.GTM.API.Api.OrganizersApi

All URIs are relative to *https://api.getgo.com/G2M/rest*

Method | HTTP request | Description
------------- | ------------- | -------------
[**CreateOrganizer**](OrganizersApi.md#createorganizer) | **POST** /organizers | Create organizer
[**DeleteOrganizer**](OrganizersApi.md#deleteorganizer) | **DELETE** /organizers/{organizerKey} | Delete organizer
[**DeleteOrganizerByEmail**](OrganizersApi.md#deleteorganizerbyemail) | **DELETE** /organizers | Delete organizer by email
[**DeleteSessionAttendeesById**](OrganizersApi.md#deletesessionattendeesbyid) | **DELETE** /organizers/{organizerKey}/sessions/{sessionId}/attendees | Remove attendee list of a meeting session
[**DeleteSessionById**](OrganizersApi.md#deletesessionbyid) | **DELETE** /organizers/{organizerKey}/sessions/{sessionId} | Delete meeting session by session id
[**DeleteSessionRecordingsById**](OrganizersApi.md#deletesessionrecordingsbyid) | **DELETE** /organizers/{organizerKey}/sessions/{sessionId}/recordings | Delete recordings of a meeting session
[**GetAttendeesByOrganizer**](OrganizersApi.md#getattendeesbyorganizer) | **GET** /organizers/{organizerKey}/attendees | Get attendees by organizer
[**GetHistoricalMeetingsByOrganizer**](OrganizersApi.md#gethistoricalmeetingsbyorganizer) | **GET** /organizers/{organizerKey}/historicalMeetings | Get historical meetings by organizer
[**GetMeetingsByOrganizer**](OrganizersApi.md#getmeetingsbyorganizer) | **GET** /organizers/{organizerKey}/meetings | DEPRECATED: Get meetings by organizer
[**GetOrganizer**](OrganizersApi.md#getorganizer) | **GET** /organizers/{organizerKey} | Get organizer
[**GetOrganizersAllOrByEmail**](OrganizersApi.md#getorganizersallorbyemail) | **GET** /organizers | Get organizer by email / Get all organizers
[**GetUpcomingMeetingsByOrganizer**](OrganizersApi.md#getupcomingmeetingsbyorganizer) | **GET** /organizers/{organizerKey}/upcomingMeetings | Get upcoming meetings by organizer
[**UpdateOrganizer**](OrganizersApi.md#updateorganizer) | **PUT** /organizers/{organizerKey} | Update organizer


<a name="createorganizer"></a>
# **CreateOrganizer**
> List<OrganizerShort> CreateOrganizer (string authorization, OrganizerReq body)

Create organizer

Creates a new organizer and sends an email to the email address defined in the request. This API call is only available to users with the admin role. You may also pass 'G2W' or 'G2T' or 'OPENVOICE' as productType variables, creating organizers for those products. A G2W or G2T organizer will also have access to G2M.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class CreateOrganizerExample
    {
        public void main()
        {
            var apiInstance = new OrganizersApi();
            var authorization = authorization_example;  // string | Access token
            var body = new OrganizerReq(); // OrganizerReq | The details of the organizer to be created

            try
            {
                // Create organizer
                List&lt;OrganizerShort&gt; result = apiInstance.CreateOrganizer(authorization, body);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling OrganizersApi.CreateOrganizer: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **body** | [**OrganizerReq**](OrganizerReq.md)| The details of the organizer to be created | 

### Return type

[**List<OrganizerShort>**](OrganizerShort.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="deleteorganizer"></a>
# **DeleteOrganizer**
> void DeleteOrganizer (string authorization, long? organizerKey)

Delete organizer

Deletes the individual organizer specified by the organizer key. This API call is only available to users with the admin role.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class DeleteOrganizerExample
    {
        public void main()
        {
            var apiInstance = new OrganizersApi();
            var authorization = authorization_example;  // string | Access token
            var organizerKey = 789;  // long? | The key of the organizer

            try
            {
                // Delete organizer
                apiInstance.DeleteOrganizer(authorization, organizerKey);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling OrganizersApi.DeleteOrganizer: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **organizerKey** | **long?**| The key of the organizer | 

### Return type

void (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="deleteorganizerbyemail"></a>
# **DeleteOrganizerByEmail**
> void DeleteOrganizerByEmail (string authorization, string email)

Delete organizer by email

Deletes the individual organizer specified by the email address. This API call is only available to users with the admin role.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class DeleteOrganizerByEmailExample
    {
        public void main()
        {
            var apiInstance = new OrganizersApi();
            var authorization = authorization_example;  // string | Access token
            var email = email_example;  // string | The email address of the organizer

            try
            {
                // Delete organizer by email
                apiInstance.DeleteOrganizerByEmail(authorization, email);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling OrganizersApi.DeleteOrganizerByEmail: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **email** | **string**| The email address of the organizer | 

### Return type

void (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="deletesessionattendeesbyid"></a>
# **DeleteSessionAttendeesById**
> void DeleteSessionAttendeesById (string authorization, long? organizerKey, long? sessionId)

Remove attendee list of a meeting session

Use this call to clean up your data by deleting the attendee list for a specific session. The sessionID(s) and the number of attendees at each session is included by meeting in the Get Historical Meetings call. Each meeting held is at least one session. One meeting may be multiple sessions if the organizer stopped and restarted the meeting for any reason, such as their device of choice did not work. Recurring meetings are at least one session per event. The call removes the attendee list from the session from LogMeIn's servers. You can remove the entire session data using the Delete Session call, or delete recordings of the meeting using the Delete Session Recordings call. The meeting will still appear in your organizer's Meeting History, but will have no associated attendees. NOTE: The delete occurs at the session level because this provides the greatest granular control. To delete for a specific meetingID, you would need to accumualate all sessions for the meeting and apply the delete.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class DeleteSessionAttendeesByIdExample
    {
        public void main()
        {
            var apiInstance = new OrganizersApi();
            var authorization = authorization_example;  // string | Access token
            var organizerKey = 789;  // long? | The organizer ID
            var sessionId = 789;  // long? | The session ID

            try
            {
                // Remove attendee list of a meeting session
                apiInstance.DeleteSessionAttendeesById(authorization, organizerKey, sessionId);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling OrganizersApi.DeleteSessionAttendeesById: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **organizerKey** | **long?**| The organizer ID | 
 **sessionId** | **long?**| The session ID | 

### Return type

void (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="deletesessionbyid"></a>
# **DeleteSessionById**
> void DeleteSessionById (string authorization, long? organizerKey, long? sessionId)

Delete meeting session by session id

Use this call to clean up your data by deleting any specific session. The sessionID(s) are included by meeting in the Get Historical Meetings call. Each meeting held is at least one session. One meeting may be multiple sessions if the organizer stopped and restarted the meeting for any reason, such as their device of choice did not work. Recurring meetings are at least one session per event. The call removes the specified session(s) from LogMeIn's servers including the sessionID, the attendee list, and any recordings. The meeting will still appear in your organizer's Meeting History, but will have no associated sessions. NOTE: The delete occurs at the session level because this provides the greatest granular control. To delete for a specific meetingID, you would need to accumualate all sessions for the meeting and apply the delete.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class DeleteSessionByIdExample
    {
        public void main()
        {
            var apiInstance = new OrganizersApi();
            var authorization = authorization_example;  // string | Access token
            var organizerKey = 789;  // long? | The organizer ID
            var sessionId = 789;  // long? | The session ID

            try
            {
                // Delete meeting session by session id
                apiInstance.DeleteSessionById(authorization, organizerKey, sessionId);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling OrganizersApi.DeleteSessionById: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **organizerKey** | **long?**| The organizer ID | 
 **sessionId** | **long?**| The session ID | 

### Return type

void (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="deletesessionrecordingsbyid"></a>
# **DeleteSessionRecordingsById**
> void DeleteSessionRecordingsById (string authorization, long? organizerKey, long? sessionId)

Delete recordings of a meeting session

Use this call to clean up your data by deleting recordings associated with a specific session. The sessionID(s) are included by meeting in the Get Historical Meetings call. The call removes any recordings associated with the specified session from LogMeIn's servers. (Any session may have multiple recordings if the feature was started and stopped multiple times during the meeting.) You can remove the entire session data using the Delete Session call, or delete attendee lists for the session using the Delete Session Attendees call. The meeting will still appear in your organizer's Meeting History, but will have no associated recordings for the specified session. NOTE: The delete occurs at the session level because this provides the greatest granular control. To delete for a specific meetingID, you would need to accumualate all sessions for the meeting and apply the delete.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class DeleteSessionRecordingsByIdExample
    {
        public void main()
        {
            var apiInstance = new OrganizersApi();
            var authorization = authorization_example;  // string | Access token
            var organizerKey = 789;  // long? | The organizer ID
            var sessionId = 789;  // long? | The session ID

            try
            {
                // Delete recordings of a meeting session
                apiInstance.DeleteSessionRecordingsById(authorization, organizerKey, sessionId);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling OrganizersApi.DeleteSessionRecordingsById: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **organizerKey** | **long?**| The organizer ID | 
 **sessionId** | **long?**| The session ID | 

### Return type

void (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="getattendeesbyorganizer"></a>
# **GetAttendeesByOrganizer**
> List<AttendeeByOrganizer> GetAttendeesByOrganizer (string authorization, long? organizerKey, DateTime? startDate, DateTime? endDate)

Get attendees by organizer

Lists all attendees for all meetings within a specified date range for a specified organizer. This API call is only available to users with the admin role.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class GetAttendeesByOrganizerExample
    {
        public void main()
        {
            var apiInstance = new OrganizersApi();
            var authorization = authorization_example;  // string | Access token
            var organizerKey = 789;  // long? | The key of the organizer
            var startDate = 2013-10-20T19:20:30+01:00;  // DateTime? | A required start of date range in ISO8601 UTC format, e.g. 2015-07-01T22:00:00Z
            var endDate = 2013-10-20T19:20:30+01:00;  // DateTime? | A required end of date range in ISO8601 UTC format, e.g. 2015-07-01T23:00:00Z

            try
            {
                // Get attendees by organizer
                List&lt;AttendeeByOrganizer&gt; result = apiInstance.GetAttendeesByOrganizer(authorization, organizerKey, startDate, endDate);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling OrganizersApi.GetAttendeesByOrganizer: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **organizerKey** | **long?**| The key of the organizer | 
 **startDate** | **DateTime?**| A required start of date range in ISO8601 UTC format, e.g. 2015-07-01T22:00:00Z | 
 **endDate** | **DateTime?**| A required end of date range in ISO8601 UTC format, e.g. 2015-07-01T23:00:00Z | 

### Return type

[**List<AttendeeByOrganizer>**](AttendeeByOrganizer.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="gethistoricalmeetingsbyorganizer"></a>
# **GetHistoricalMeetingsByOrganizer**
> List<HistoricalMeeting> GetHistoricalMeetingsByOrganizer (string authorization, long? organizerKey, DateTime? startDate, DateTime? endDate)

Get historical meetings by organizer

Get historical meetings for the specified organizer that started within the specified date/time range. Remark: Meetings which are still ongoing at the time of the request are NOT contained in the result array.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class GetHistoricalMeetingsByOrganizerExample
    {
        public void main()
        {
            var apiInstance = new OrganizersApi();
            var authorization = authorization_example;  // string | Access token
            var organizerKey = 789;  // long? | The key of the organizer
            var startDate = 2013-10-20T19:20:30+01:00;  // DateTime? | Required start of date range, in ISO8601 UTC format, e.g. 2015-07-01T22:00:00Z
            var endDate = 2013-10-20T19:20:30+01:00;  // DateTime? | Required end of date range, in ISO8601 UTC format, e.g. 2015-07-01T23:00:00Z

            try
            {
                // Get historical meetings by organizer
                List&lt;HistoricalMeeting&gt; result = apiInstance.GetHistoricalMeetingsByOrganizer(authorization, organizerKey, startDate, endDate);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling OrganizersApi.GetHistoricalMeetingsByOrganizer: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **organizerKey** | **long?**| The key of the organizer | 
 **startDate** | **DateTime?**| Required start of date range, in ISO8601 UTC format, e.g. 2015-07-01T22:00:00Z | 
 **endDate** | **DateTime?**| Required end of date range, in ISO8601 UTC format, e.g. 2015-07-01T23:00:00Z | 

### Return type

[**List<HistoricalMeeting>**](HistoricalMeeting.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="getmeetingsbyorganizer"></a>
# **GetMeetingsByOrganizer**
> List<MeetingByOrganizer> GetMeetingsByOrganizer (string authorization, long? organizerKey, bool? scheduled = null, bool? history = null, DateTime? startDate = null, DateTime? endDate = null)

DEPRECATED: Get meetings by organizer

DEPRECATED: Please use the new API calls 'Get historical meetings by organizer' and 'Get upcoming meetings by organizer'. Gets future (scheduled) or past (history) meetings for a specified organizer. Include 'history=true' and the past start and end dates in the URL to retrieve past meetings. Enter 'scheduled=true' (without dates) to get scheduled meetings.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class GetMeetingsByOrganizerExample
    {
        public void main()
        {
            var apiInstance = new OrganizersApi();
            var authorization = authorization_example;  // string | Access token
            var organizerKey = 789;  // long? | The key of the organizer
            var scheduled = true;  // bool? | When 'true', returns all future meetings. Date range not supported. (optional) 
            var history = true;  // bool? | When 'true', returns all past meetings within date range (optional) 
            var startDate = 2013-10-20T19:20:30+01:00;  // DateTime? | If history is 'true', required start of date range, in ISO8601 UTC format, e.g. 2015-07-01T22:00:00Z (optional) 
            var endDate = 2013-10-20T19:20:30+01:00;  // DateTime? | If history is 'true', required end of date range, in ISO8601 UTC format, e.g. 2015-07-01T23:00:00Z (optional) 

            try
            {
                // DEPRECATED: Get meetings by organizer
                List&lt;MeetingByOrganizer&gt; result = apiInstance.GetMeetingsByOrganizer(authorization, organizerKey, scheduled, history, startDate, endDate);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling OrganizersApi.GetMeetingsByOrganizer: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **organizerKey** | **long?**| The key of the organizer | 
 **scheduled** | **bool?**| When &#39;true&#39;, returns all future meetings. Date range not supported. | [optional] 
 **history** | **bool?**| When &#39;true&#39;, returns all past meetings within date range | [optional] 
 **startDate** | **DateTime?**| If history is &#39;true&#39;, required start of date range, in ISO8601 UTC format, e.g. 2015-07-01T22:00:00Z | [optional] 
 **endDate** | **DateTime?**| If history is &#39;true&#39;, required end of date range, in ISO8601 UTC format, e.g. 2015-07-01T23:00:00Z | [optional] 

### Return type

[**List<MeetingByOrganizer>**](MeetingByOrganizer.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="getorganizer"></a>
# **GetOrganizer**
> List<Organizer> GetOrganizer (string authorization, long? organizerKey)

Get organizer

Returns the individual organizer specified by the key. This API call is only available to users with the admin role. Non-admin users can only make this call for their own organizerKey.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class GetOrganizerExample
    {
        public void main()
        {
            var apiInstance = new OrganizersApi();
            var authorization = authorization_example;  // string | Access token
            var organizerKey = 789;  // long? | The key of the organizer

            try
            {
                // Get organizer
                List&lt;Organizer&gt; result = apiInstance.GetOrganizer(authorization, organizerKey);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling OrganizersApi.GetOrganizer: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **organizerKey** | **long?**| The key of the organizer | 

### Return type

[**List<Organizer>**](Organizer.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="getorganizersallorbyemail"></a>
# **GetOrganizersAllOrByEmail**
> List<Organizer> GetOrganizersAllOrByEmail (string authorization, string email = null)

Get organizer by email / Get all organizers

Gets the individual organizer specified by the organizer's email address. If an email address is not specified, all organizers are returned. This API call is only available to users with the admin role.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class GetOrganizersAllOrByEmailExample
    {
        public void main()
        {
            var apiInstance = new OrganizersApi();
            var authorization = authorization_example;  // string | Access token
            var email = email_example;  // string | The email address of the organizer (optional) 

            try
            {
                // Get organizer by email / Get all organizers
                List&lt;Organizer&gt; result = apiInstance.GetOrganizersAllOrByEmail(authorization, email);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling OrganizersApi.GetOrganizersAllOrByEmail: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **email** | **string**| The email address of the organizer | [optional] 

### Return type

[**List<Organizer>**](Organizer.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="getupcomingmeetingsbyorganizer"></a>
# **GetUpcomingMeetingsByOrganizer**
> List<UpcomingMeeting> GetUpcomingMeetingsByOrganizer (string authorization, long? organizerKey)

Get upcoming meetings by organizer

Get upcoming meetings for a specified organizer. This API call is only available to users with the admin role.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class GetUpcomingMeetingsByOrganizerExample
    {
        public void main()
        {
            var apiInstance = new OrganizersApi();
            var authorization = authorization_example;  // string | Access token
            var organizerKey = 789;  // long? | The key of the organizer

            try
            {
                // Get upcoming meetings by organizer
                List&lt;UpcomingMeeting&gt; result = apiInstance.GetUpcomingMeetingsByOrganizer(authorization, organizerKey);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling OrganizersApi.GetUpcomingMeetingsByOrganizer: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **organizerKey** | **long?**| The key of the organizer | 

### Return type

[**List<UpcomingMeeting>**](UpcomingMeeting.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="updateorganizer"></a>
# **UpdateOrganizer**
> void UpdateOrganizer (string authorization, long? organizerKey, OrganizerStatus body)

Update organizer

Updates the products of the specified organizer. To add a product (G2M, G2W, G2T, OPENVOICE) for the organizer, the call must be sent once for each product you want to add. To remove all products for the organizer, set status to 'suspended'. The call is limited to users with the admin role.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class UpdateOrganizerExample
    {
        public void main()
        {
            var apiInstance = new OrganizersApi();
            var authorization = authorization_example;  // string | Access token
            var organizerKey = 789;  // long? | The key of the organizer
            var body = new OrganizerStatus(); // OrganizerStatus | The organizer's status

            try
            {
                // Update organizer
                apiInstance.UpdateOrganizer(authorization, organizerKey, body);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling OrganizersApi.UpdateOrganizer: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **organizerKey** | **long?**| The key of the organizer | 
 **body** | [**OrganizerStatus**](OrganizerStatus.md)| The organizer&#39;s status | 

### Return type

void (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

