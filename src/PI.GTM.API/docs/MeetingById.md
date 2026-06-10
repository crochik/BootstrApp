# PI.GTM.API.Model.MeetingById
## Properties

Name | Type | Description | Notes
------------ | ------------- | ------------- | -------------
**CreateTime** | **DateTime?** | DEPRECATED. Returns an empty string &#39;&#39; | 
**PasswordRequired** | **bool?** | Indicates whether a password is required to join the meeting | 
**Status** | **MeetingStatus** | The meeting status, i.e whether the meeting is running or not | 
**Subject** | **string** | The subject of the meeting | 
**EndTime** | **DateTime?** | The ending time of the meeting | 
**ConferenceCallInfo** | **string** | Audio options of the meeting | 
**StartTime** | **DateTime?** | The meeting starting time | 
**Duration** | **int?** | The duration of the meeting in minutes | 
**MaxParticipants** | **int?** | The maximum number of participants allowed at the meeting | 
**MeetingId** | **long?** | The meeting ID | 
**MeetingKey** | **long?** | The meeting ID. Field retained for backwards compatibility reasons | 
**MeetingType** | **MeetingType** | The meeting type | 
**UniqueMeetingId** | **long?** | The meeting ID. Field retained for backwards compatibility reasons | 
**CoorganizerKeys** | **List&lt;string&gt;** | The co-organizer keys of users that also can host the meeting. | [optional] 

[[Back to Model list]](../README.md#documentation-for-models) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to README]](../README.md)

