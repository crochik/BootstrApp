# PI.GTM.API.Api.MeetingsApi

All URIs are relative to *https://api.getgo.com/G2M/rest*

Method | HTTP request | Description
------------- | ------------- | -------------
[**CreateMeeting**](MeetingsApi.md#createmeeting) | **POST** /meetings | Create meeting
[**DeleteMeeting**](MeetingsApi.md#deletemeeting) | **DELETE** /meetings/{meetingId} | Delete meeting
[**GetAttendeesByMeetings**](MeetingsApi.md#getattendeesbymeetings) | **GET** /meetings/{meetingId}/attendees | Get attendees by meeting
[**GetHistoricalMeetings**](MeetingsApi.md#gethistoricalmeetings) | **GET** /historicalMeetings | Get historical meetings
[**GetHistoryMeetings**](MeetingsApi.md#gethistorymeetings) | **GET** /meetings | DEPRECATED: Get historical meetings
[**GetMeeting**](MeetingsApi.md#getmeeting) | **GET** /meetings/{meetingId} | Get meeting
[**GetUpcomingMeetings**](MeetingsApi.md#getupcomingmeetings) | **GET** /upcomingMeetings | Get upcoming meetings
[**StartMeeting**](MeetingsApi.md#startmeeting) | **GET** /meetings/{meetingId}/start | Start meeting
[**UpdateMeeting**](MeetingsApi.md#updatemeeting) | **PUT** /meetings/{meetingId} | Update meeting


<a name="createmeeting"></a>
# **CreateMeeting**
> List<MeetingCreated> CreateMeeting (string authorization, MeetingReqCreate body)

Create meeting

Create a new meeting based on the parameters specified.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class CreateMeetingExample
    {
        public void main()
        {
            var apiInstance = new MeetingsApi();
            var authorization = authorization_example;  // string | Access token
            var body = new MeetingReqCreate(); // MeetingReqCreate | The meeting details

            try
            {
                // Create meeting
                List&lt;MeetingCreated&gt; result = apiInstance.CreateMeeting(authorization, body);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling MeetingsApi.CreateMeeting: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **body** | [**MeetingReqCreate**](MeetingReqCreate.md)| The meeting details | 

### Return type

[**List<MeetingCreated>**](MeetingCreated.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="deletemeeting"></a>
# **DeleteMeeting**
> void DeleteMeeting (string authorization, long? meetingId)

Delete meeting

Deletes the meeting identified by the meetingId.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class DeleteMeetingExample
    {
        public void main()
        {
            var apiInstance = new MeetingsApi();
            var authorization = authorization_example;  // string | Access token
            var meetingId = 789;  // long? | The meeting ID

            try
            {
                // Delete meeting
                apiInstance.DeleteMeeting(authorization, meetingId);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling MeetingsApi.DeleteMeeting: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **meetingId** | **long?**| The meeting ID | 

### Return type

void (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="getattendeesbymeetings"></a>
# **GetAttendeesByMeetings**
> List<AttendeeByMeeting> GetAttendeesByMeetings (string authorization, long? meetingId)

Get attendees by meeting

List all attendees for specified meetingId of historical meeting. The historical meetings can be fetched using 'Get historical meetings', 'Get historical meetings by organizer', and 'Get historical meetings by group'. For users with the admin role this call returns attendees for any meeting. For any other user the call will return attendees for meetings on which the user is a valid organizer.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class GetAttendeesByMeetingsExample
    {
        public void main()
        {
            var apiInstance = new MeetingsApi();
            var authorization = authorization_example;  // string | Access token
            var meetingId = 789;  // long? | The meeting ID

            try
            {
                // Get attendees by meeting
                List&lt;AttendeeByMeeting&gt; result = apiInstance.GetAttendeesByMeetings(authorization, meetingId);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling MeetingsApi.GetAttendeesByMeetings: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **meetingId** | **long?**| The meeting ID | 

### Return type

[**List<AttendeeByMeeting>**](AttendeeByMeeting.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="gethistoricalmeetings"></a>
# **GetHistoricalMeetings**
> List<HistoricalMeeting> GetHistoricalMeetings (string authorization, DateTime? startDate, DateTime? endDate)

Get historical meetings

Get historical meetings for the currently authenticated organizer that started within the specified date/time range. Remark: Meetings which are still ongoing at the time of the request are NOT contained in the result array. A maximum of 25 meetings is returned.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class GetHistoricalMeetingsExample
    {
        public void main()
        {
            var apiInstance = new MeetingsApi();
            var authorization = authorization_example;  // string | Access token
            var startDate = 2013-10-20T19:20:30+01:00;  // DateTime? | Required start of date range, in ISO8601 UTC format, e.g. 2015-07-01T22:00:00Z
            var endDate = 2013-10-20T19:20:30+01:00;  // DateTime? | Required end of date range, in ISO8601 UTC format, e.g. 2015-07-01T23:00:00Z

            try
            {
                // Get historical meetings
                List&lt;HistoricalMeeting&gt; result = apiInstance.GetHistoricalMeetings(authorization, startDate, endDate);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling MeetingsApi.GetHistoricalMeetings: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
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

<a name="gethistorymeetings"></a>
# **GetHistoryMeetings**
> List<MeetingHistory> GetHistoryMeetings (string authorization, bool? history = null, DateTime? startDate = null, DateTime? endDate = null)

DEPRECATED: Get historical meetings

DEPRECATED: Please use the new API calls 'Get historical meetings' and 'Get upcoming meetings'.  Gets historical meetings for the current authenticated organizer. Requires date range for filtering results to only meetings within specified dates. History searches will contain the parameter 'meetingInstanceKey' which is used in conjunction with the call 'Get Attendees by Meeting' to get attendee information for a past meeting.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class GetHistoryMeetingsExample
    {
        public void main()
        {
            var apiInstance = new MeetingsApi();
            var authorization = authorization_example;  // string | Access token
            var history = true;  // bool? | When 'true', returns all past meetings within date range (optional) 
            var startDate = 2013-10-20T19:20:30+01:00;  // DateTime? | If history=true, required start of date range, in ISO8601 UTC format, e.g. 2015-07-01T22:00:00Z (optional) 
            var endDate = 2013-10-20T19:20:30+01:00;  // DateTime? | If history=true, required end of date range, in ISO8601 UTC format, e.g. 2015-07-01T23:00:00Z (optional) 

            try
            {
                // DEPRECATED: Get historical meetings
                List&lt;MeetingHistory&gt; result = apiInstance.GetHistoryMeetings(authorization, history, startDate, endDate);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling MeetingsApi.GetHistoryMeetings: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **history** | **bool?**| When &#39;true&#39;, returns all past meetings within date range | [optional] 
 **startDate** | **DateTime?**| If history&#x3D;true, required start of date range, in ISO8601 UTC format, e.g. 2015-07-01T22:00:00Z | [optional] 
 **endDate** | **DateTime?**| If history&#x3D;true, required end of date range, in ISO8601 UTC format, e.g. 2015-07-01T23:00:00Z | [optional] 

### Return type

[**List<MeetingHistory>**](MeetingHistory.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="getmeeting"></a>
# **GetMeeting**
> List<MeetingById> GetMeeting (string authorization, long? meetingId)

Get meeting

Returns the meeting details for the specified meeting.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class GetMeetingExample
    {
        public void main()
        {
            var apiInstance = new MeetingsApi();
            var authorization = authorization_example;  // string | Access token
            var meetingId = 789;  // long? | The meeting ID

            try
            {
                // Get meeting
                List&lt;MeetingById&gt; result = apiInstance.GetMeeting(authorization, meetingId);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling MeetingsApi.GetMeeting: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **meetingId** | **long?**| The meeting ID | 

### Return type

[**List<MeetingById>**](MeetingById.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="getupcomingmeetings"></a>
# **GetUpcomingMeetings**
> List<UpcomingMeeting> GetUpcomingMeetings (string authorization)

Get upcoming meetings

Gets upcoming meetings for the current authenticated organizer.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class GetUpcomingMeetingsExample
    {
        public void main()
        {
            var apiInstance = new MeetingsApi();
            var authorization = authorization_example;  // string | Access token

            try
            {
                // Get upcoming meetings
                List&lt;UpcomingMeeting&gt; result = apiInstance.GetUpcomingMeetings(authorization);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling MeetingsApi.GetUpcomingMeetings: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 

### Return type

[**List<UpcomingMeeting>**](UpcomingMeeting.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="startmeeting"></a>
# **StartMeeting**
> StartUrl StartMeeting (string authorization, long? meetingId)

Start meeting

Returns a host URL that can be used to start a meeting. When this URL is opened in a web browser, the GoToMeeting client will be downloaded and launched and the meeting will start. The end user is not required to login to a client.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class StartMeetingExample
    {
        public void main()
        {
            var apiInstance = new MeetingsApi();
            var authorization = authorization_example;  // string | Access token
            var meetingId = 789;  // long? | The meeting ID

            try
            {
                // Start meeting
                StartUrl result = apiInstance.StartMeeting(authorization, meetingId);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling MeetingsApi.StartMeeting: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **meetingId** | **long?**| The meeting ID | 

### Return type

[**StartUrl**](StartUrl.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="updatemeeting"></a>
# **UpdateMeeting**
> void UpdateMeeting (string authorization, long? meetingId, MeetingReqUpdate body)

Update meeting

Updates an existing meeting specified by meetingId.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class UpdateMeetingExample
    {
        public void main()
        {
            var apiInstance = new MeetingsApi();
            var authorization = authorization_example;  // string | Access token
            var meetingId = 789;  // long? | The meeting ID
            var body = new MeetingReqUpdate(); // MeetingReqUpdate | The meeting details

            try
            {
                // Update meeting
                apiInstance.UpdateMeeting(authorization, meetingId, body);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling MeetingsApi.UpdateMeeting: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **meetingId** | **long?**| The meeting ID | 
 **body** | [**MeetingReqUpdate**](MeetingReqUpdate.md)| The meeting details | 

### Return type

void (empty response body)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

