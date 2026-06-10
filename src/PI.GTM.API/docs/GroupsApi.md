# PI.GTM.API.Api.GroupsApi

All URIs are relative to *https://api.getgo.com/G2M/rest*

Method | HTTP request | Description
------------- | ------------- | -------------
[**CreateOrganizerInGroup**](GroupsApi.md#createorganizeringroup) | **POST** /groups/{groupKey}/organizers | Create organizer in group
[**GetAttendeesByGroup**](GroupsApi.md#getattendeesbygroup) | **GET** /groups/{groupKey}/attendees | Get attendees by group
[**GetGroups**](GroupsApi.md#getgroups) | **GET** /groups | Get groups
[**GetHistoricalMeetingsByGroup**](GroupsApi.md#gethistoricalmeetingsbygroup) | **GET** /groups/{groupKey}/historicalMeetings | Get historical meetings by group
[**GetHistoryMeetingsByGroup**](GroupsApi.md#gethistorymeetingsbygroup) | **GET** /groups/{groupKey}/meetings | DEPRECATED: Get historical meetings by group
[**GetOrganizersByGroup**](GroupsApi.md#getorganizersbygroup) | **GET** /groups/{groupKey}/organizers | Get organizers by group
[**GetUpcomingMeetingsByGroup**](GroupsApi.md#getupcomingmeetingsbygroup) | **GET** /groups/{groupKey}/upcomingMeetings | Get upcoming meetings by group


<a name="createorganizeringroup"></a>
# **CreateOrganizerInGroup**
> List<OrganizerShort> CreateOrganizerInGroup (string authorization, long? groupKey, OrganizerReq body)

Create organizer in group

Creates a new organizer and sends an email to the email address defined in request. This API call is only available to users with the admin role. You may also pass 'G2W' or 'G2T' or 'OPENVOICE' as productType variables, creating organizers for those products. A G2W or G2T organizer will also have access to G2M.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class CreateOrganizerInGroupExample
    {
        public void main()
        {
            var apiInstance = new GroupsApi();
            var authorization = authorization_example;  // string | Access token
            var groupKey = 789;  // long? | The key of the group
            var body = new OrganizerReq(); // OrganizerReq | The details of the organizer to be created

            try
            {
                // Create organizer in group
                List&lt;OrganizerShort&gt; result = apiInstance.CreateOrganizerInGroup(authorization, groupKey, body);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling GroupsApi.CreateOrganizerInGroup: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **groupKey** | **long?**| The key of the group | 
 **body** | [**OrganizerReq**](OrganizerReq.md)| The details of the organizer to be created | 

### Return type

[**List<OrganizerShort>**](OrganizerShort.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="getattendeesbygroup"></a>
# **GetAttendeesByGroup**
> List<AttendeeByGroup> GetAttendeesByGroup (string authorization, long? groupKey, DateTime? startDate = null, DateTime? endDate = null)

Get attendees by group

Returns all attendees for all meetings within specified date range held by organizers within the specified group. This API call is only available to users with the admin role. This API call can be used only for groups with maximum 50 organizers.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class GetAttendeesByGroupExample
    {
        public void main()
        {
            var apiInstance = new GroupsApi();
            var authorization = authorization_example;  // string | Access token
            var groupKey = 789;  // long? | The key of the group
            var startDate = 2013-10-20T19:20:30+01:00;  // DateTime? | Start of date range, in ISO8601 UTC format, e.g. 2015-07-01T22:00:00Z (optional) 
            var endDate = 2013-10-20T19:20:30+01:00;  // DateTime? | End of date range, in ISO8601 UTC format, e.g. 2015-07-01T23:00:00Z (optional) 

            try
            {
                // Get attendees by group
                List&lt;AttendeeByGroup&gt; result = apiInstance.GetAttendeesByGroup(authorization, groupKey, startDate, endDate);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling GroupsApi.GetAttendeesByGroup: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **groupKey** | **long?**| The key of the group | 
 **startDate** | **DateTime?**| Start of date range, in ISO8601 UTC format, e.g. 2015-07-01T22:00:00Z | [optional] 
 **endDate** | **DateTime?**| End of date range, in ISO8601 UTC format, e.g. 2015-07-01T23:00:00Z | [optional] 

### Return type

[**List<AttendeeByGroup>**](AttendeeByGroup.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="getgroups"></a>
# **GetGroups**
> List<Group> GetGroups (string authorization)

Get groups

List all groups for an account. This API call is only available to users with the admin role.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class GetGroupsExample
    {
        public void main()
        {
            var apiInstance = new GroupsApi();
            var authorization = authorization_example;  // string | Access token

            try
            {
                // Get groups
                List&lt;Group&gt; result = apiInstance.GetGroups(authorization);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling GroupsApi.GetGroups: " + e.Message );
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

[**List<Group>**](Group.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="gethistoricalmeetingsbygroup"></a>
# **GetHistoricalMeetingsByGroup**
> List<HistoricalMeetingByGroup> GetHistoricalMeetingsByGroup (string authorization, long? groupKey, DateTime? startDate, DateTime? endDate)

Get historical meetings by group

Get historical meetings for the specified group that started within the specified date/time range. This API call is only available to users with the admin role. This API call is restricted to groups with a maximum of 50 organizers. Remark: Meetings which are still ongoing at the time of the request are NOT contained in the result array.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class GetHistoricalMeetingsByGroupExample
    {
        public void main()
        {
            var apiInstance = new GroupsApi();
            var authorization = authorization_example;  // string | Access token
            var groupKey = 789;  // long? | The key of the group
            var startDate = 2013-10-20T19:20:30+01:00;  // DateTime? | Required start of date range, in ISO8601 UTC format, e.g. 2015-07-01T22:00:00Z
            var endDate = 2013-10-20T19:20:30+01:00;  // DateTime? | Required end of date range, in ISO8601 UTC format, e.g. 2015-07-01T23:00:00Z

            try
            {
                // Get historical meetings by group
                List&lt;HistoricalMeetingByGroup&gt; result = apiInstance.GetHistoricalMeetingsByGroup(authorization, groupKey, startDate, endDate);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling GroupsApi.GetHistoricalMeetingsByGroup: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **groupKey** | **long?**| The key of the group | 
 **startDate** | **DateTime?**| Required start of date range, in ISO8601 UTC format, e.g. 2015-07-01T22:00:00Z | 
 **endDate** | **DateTime?**| Required end of date range, in ISO8601 UTC format, e.g. 2015-07-01T23:00:00Z | 

### Return type

[**List<HistoricalMeetingByGroup>**](HistoricalMeetingByGroup.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="gethistorymeetingsbygroup"></a>
# **GetHistoryMeetingsByGroup**
> List<HistoryMeetingByGroup> GetHistoryMeetingsByGroup (string authorization, long? groupKey, bool? history, DateTime? startDate, DateTime? endDate)

DEPRECATED: Get historical meetings by group

DEPRECATED: Please use the new API calls 'Get historical meetings by group' and 'Get upcoming meetings by group'. Get meetings for a specified group. Additional filters can be used to view only meetings within a specified date range. This API call is only available to users with the admin role.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class GetHistoryMeetingsByGroupExample
    {
        public void main()
        {
            var apiInstance = new GroupsApi();
            var authorization = authorization_example;  // string | Access token
            var groupKey = 789;  // long? | The key of the group
            var history = true;  // bool? | When 'true', returns all past meetings within date range
            var startDate = 2013-10-20T19:20:30+01:00;  // DateTime? | If history=true, required start of date range, in ISO8601 UTC format, e.g. 2015-07-01T22:00:00Z
            var endDate = 2013-10-20T19:20:30+01:00;  // DateTime? | If history=true, required end of date range, in ISO8601 UTC format, e.g. 2015-07-01T23:00:00Z

            try
            {
                // DEPRECATED: Get historical meetings by group
                List&lt;HistoryMeetingByGroup&gt; result = apiInstance.GetHistoryMeetingsByGroup(authorization, groupKey, history, startDate, endDate);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling GroupsApi.GetHistoryMeetingsByGroup: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **groupKey** | **long?**| The key of the group | 
 **history** | **bool?**| When &#39;true&#39;, returns all past meetings within date range | 
 **startDate** | **DateTime?**| If history&#x3D;true, required start of date range, in ISO8601 UTC format, e.g. 2015-07-01T22:00:00Z | 
 **endDate** | **DateTime?**| If history&#x3D;true, required end of date range, in ISO8601 UTC format, e.g. 2015-07-01T23:00:00Z | 

### Return type

[**List<HistoryMeetingByGroup>**](HistoryMeetingByGroup.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="getorganizersbygroup"></a>
# **GetOrganizersByGroup**
> List<OrganizerByGroup> GetOrganizersByGroup (string authorization, long? groupKey)

Get organizers by group

Returns all the organizers within a specific group. This API call is only available to users with the admin role.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class GetOrganizersByGroupExample
    {
        public void main()
        {
            var apiInstance = new GroupsApi();
            var authorization = authorization_example;  // string | Access token
            var groupKey = 789;  // long? | The key of the group

            try
            {
                // Get organizers by group
                List&lt;OrganizerByGroup&gt; result = apiInstance.GetOrganizersByGroup(authorization, groupKey);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling GroupsApi.GetOrganizersByGroup: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **groupKey** | **long?**| The key of the group | 

### Return type

[**List<OrganizerByGroup>**](OrganizerByGroup.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

<a name="getupcomingmeetingsbygroup"></a>
# **GetUpcomingMeetingsByGroup**
> List<UpcomingMeetingByGroup> GetUpcomingMeetingsByGroup (string authorization, long? groupKey)

Get upcoming meetings by group

Get upcoming meetings for a specified group. This API call is only available to users with the admin role. This API call can be used only for groups with maximum 50 organizers.

### Example
```csharp
using System;
using System.Diagnostics;
using PI.GTM.API.Api;
using PI.GTM.API.Client;
using PI.GTM.API.Model;

namespace Example
{
    public class GetUpcomingMeetingsByGroupExample
    {
        public void main()
        {
            var apiInstance = new GroupsApi();
            var authorization = authorization_example;  // string | Access token
            var groupKey = 789;  // long? | The key of the group

            try
            {
                // Get upcoming meetings by group
                List&lt;UpcomingMeetingByGroup&gt; result = apiInstance.GetUpcomingMeetingsByGroup(authorization, groupKey);
                Debug.WriteLine(result);
            }
            catch (Exception e)
            {
                Debug.Print("Exception when calling GroupsApi.GetUpcomingMeetingsByGroup: " + e.Message );
            }
        }
    }
}
```

### Parameters

Name | Type | Description  | Notes
------------- | ------------- | ------------- | -------------
 **authorization** | **string**| Access token | 
 **groupKey** | **long?**| The key of the group | 

### Return type

[**List<UpcomingMeetingByGroup>**](UpcomingMeetingByGroup.md)

### Authorization

No authorization required

### HTTP request headers

 - **Content-Type**: application/json
 - **Accept**: application/json

[[Back to top]](#) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to Model list]](../README.md#documentation-for-models) [[Back to README]](../README.md)

