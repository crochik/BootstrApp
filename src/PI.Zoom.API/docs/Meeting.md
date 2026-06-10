# PI.Zoom.API.Model.Meeting
## Properties

Name | Type | Description | Notes
------------ | ------------- | ------------- | -------------
**Topic** | **string** | Meeting topic | [optional] 
**Type** | **int?** | Meeting Type | [optional] 
**StartTime** | **DateTime?** | Meeting start time. When using a format like \&quot;yyyy-MM-dd&#39;T&#39;HH:mm:ss&#39;Z&#39;\&quot;, always use GMT time. When using a format like \&quot;yyyy-MM-dd&#39;T&#39;HH:mm:ss\&quot;, you should use local time and you will need to specify the time zone. Only used for scheduled meetings and recurring meetings with fixed time. | [optional] 
**Duration** | **int?** | Meeting duration (minutes). Used for scheduled meetings only | [optional] 
**Timezone** | **string** | Timezone to format start_time. For example, \&quot;America/Los_Angeles\&quot;. For scheduled meetings only. Please reference our [timezone](#timezones) list for supported timezones and their formats. | [optional] 
**Password** | **string** | Password to join the meeting. Password may only contain the following characters: [a-z A-Z 0-9 @ - _ *]. Max of 10 characters. | [optional] 
**Agenda** | **string** | Meeting description | [optional] 
**TrackingFields** | [**List&lt;MeetingInfoTrackingFields&gt;**](MeetingInfoTrackingFields.md) | Tracking fields | [optional] 
**Recurrence** | [**Recurrence**](Recurrence.md) |  | [optional] 
**Settings** | [**MeetingSettings**](MeetingSettings.md) |  | [optional] 

[[Back to Model list]](../README.md#documentation-for-models) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to README]](../README.md)

