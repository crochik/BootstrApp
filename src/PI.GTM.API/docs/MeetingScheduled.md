# PI.GTM.API.Model.MeetingScheduled
## Properties

Name | Type | Description | Notes
------------ | ------------- | ------------- | -------------
**StartTime** | **DateTime?** | The starting time of the meeting. | 
**CreateTime** | **DateTime?** | The time the meeting was created. | 
**Meetingid** | **long?** | The meeting ID. | 
**MaxParticipants** | **int?** | The maximum number of participants allowed at the meeting. | 
**PasswordRequired** | **bool?** | Indicates whether a password is required to join the meeting. | 
**Status** | **MeetingStatus** | The meeting status, i.e whether the meeting is running or not | 
**Subject** | **string** | The subject of the meeting. | 
**MeetingType** | **MeetingType** | The meeting type | 
**EndTime** | **DateTime?** | The ending time of the meeting. | 
**UniqueMeetingId** | **long?** | The meeting ID. Field retained for backwards compatibility reasons. | 
**ConferenceCallInfo** | **string** | Audio options for the meeting. | 
**CoorganizerKeys** | **List&lt;string&gt;** | Co-organizer keys. Co-organizers can start the meeting on the organizers behalf. | [optional] 

[[Back to Model list]](../README.md#documentation-for-models) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to README]](../README.md)

