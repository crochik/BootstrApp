# PI.Zoom.API.Api.MeetingsApi

All URIs are relative to *https://api.zoom.us/v2*

Method | HTTP request | Description
------------- | ------------- | -------------
[**MeetingCreate**](MeetingsApi.md#meetingcreate) | **POST** /users/{userId}/meetings | Create a meeting
[**Meetings**](MeetingsApi.md#meetings) | **GET** /users/{userId}/meetings | List meetings


<a name="meetingcreate"></a>
# **MeetingCreate**
> MeetingInfoCreated MeetingCreate (string userId, Meeting body)

Create a meeting

Create a meeting for a user <aside>The expiration time of start_url is two hours. But for API users, the expiration time is 90 days.</aside>

### Example
```csharp
using System;
using System.Diagnostics;
using PI.Zoom.API.Api;
using PI.Zoom.API.Client;
using PI.Zoom.API.Model;

namespace Example
{
    public class MeetingCreateExample
    {
        public void main()
        {
            // Configure API key authorization: global
            Configuration.Default.AddApiKey("access_token", "YOUR_API_KEY");
            // Uncomment below to setup prefix (e.g. Bearer) for API key, if needed
            // Configuration.Default.AddApiKeyPrefix("access_token", "Bearer");

            var apiInstance = new MeetingsApi();
            var userId = userId_example;  // string | The user ID or email address
            var body = new Meeting(); // Meeting | Meeting object

            try
            {
                // Create a meeting
                MeetingInfoCreated result = apiInstance.MeetingCreate(userId, body);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling MeetingsApi.MeetingCreate: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **userId** | **string**| The user ID or email address | 
 **body** | [**Meeting**](Meeting.md)| Meeting object | 

### Return type

[**MeetingInfoCreated**](MeetingInfoCreated.md)

### Authorization

[global](../README.md#global)

### HTTP request headers

 - **Content-Type**: application/json, multipart/form-data
 - **Accept**: application/json, application/xml

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="meetings"></a>
# **Meetings**
> MeetingList Meetings (string userId, string type = null, int? pageSize = null, int? pageNumber = null)

List meetings

List meetings for a user

### Example
```csharp
using System;
using System.Diagnostics;
using PI.Zoom.API.Api;
using PI.Zoom.API.Client;
using PI.Zoom.API.Model;

namespace Example
{
    public class MeetingsExample
    {
        public void main()
        {
            // Configure API key authorization: global
            Configuration.Default.AddApiKey("access_token", "YOUR_API_KEY");
            // Uncomment below to setup prefix (e.g. Bearer) for API key, if needed
            // Configuration.Default.AddApiKeyPrefix("access_token", "Bearer");

            var apiInstance = new MeetingsApi();
            var userId = userId_example;  // string | The user ID or email address
            var type = type_example;  // string | The meeting type (optional)  (default to live)
            var pageSize = 56;  // int? | The number of records returned within a single API call (optional)  (default to 30)
            var pageNumber = 56;  // int? | Current page number of returned records (optional)  (default to 1)

            try
            {
                // List meetings
                MeetingList result = apiInstance.Meetings(userId, type, pageSize, pageNumber);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling MeetingsApi.Meetings: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **userId** | **string**| The user ID or email address | 
 **type** | **string**| The meeting type | [optional] [default to live]
 **pageSize** | **int?**| The number of records returned within a single API call | [optional] [default to 30]
 **pageNumber** | **int?**| Current page number of returned records | [optional] [default to 1]

### Return type

[**MeetingList**](MeetingList.md)

### Authorization

[global](../README.md#global)

### HTTP request headers

 - **Content-Type**: application/json, multipart/form-data
 - **Accept**: application/json, application/xml

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

