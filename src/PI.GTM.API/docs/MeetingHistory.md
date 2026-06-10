# PI.GTM.API.Model.MeetingHistory
## Properties

Name | Type | Description | Notes
------------ | ------------- | ------------- | -------------
**LastName** | **string** | The surname of the meeting organizer | 
**GroupName** | **string** | DEPRECATED. An empty string &#39;&#39; is returned | 
**NumAttendees** | **int?** | The number of attendees at the meeting instance | 
**PasswordRequired** | **bool?** | DEPRECATED. An empty string &#39;&#39; is returned | 
**Status** | **string** | DEPRECATED. An empty string &#39;&#39; is returned | 
**Subject** | **string** | The subject of the meeting | 
**EndTime** | **DateTime?** | The time the meeting instance ended | 
**Date** | **DateTime?** | The time the meeting instance started. Field retained for backwards compatibility reasons | 
**ConferenceCallInfo** | **string** | Audio options for the meeting | 
**StartTime** | **DateTime?** | The time the meeting instance started | 
**Organizerkey** | **string** | The key of the meeting organizer. Field retained for backwards compatibility reasons | 
**MeetingInstanceKey** | **long?** | The key of the unique meeting instance | 
**NewOrganizerKey** | **string** | The key of the meeting organizer. Field introduced for compatibility reasons | 
**Duration** | **int?** | The duration of the meeting session in minutes | 
**NewMeetingId** | **string** | Formatted meeting ID | 
**SessionId** | **long?** | The ID of the meeting session | 
**Email** | **string** | The meeting organizer&#39;s email address | 
**MeetingId** | **long?** | The meeting ID | 
**OrganizerKey** | **string** | The key of the meeting organizer | 
**MeetingKey** | **long?** | The meeting ID. Field retained for backwards compatibility reasons | 
**MeetingType** | **MeetingType** | The meeting type | 
**FirstName** | **string** | The meeting organizer&#39;s first name | 
**UniqueMeetingId** | **long?** | The meeting ID. Field retained for backwards compatibility reasons | 
**Recording** | [**MeetingRecording**](MeetingRecording.md) | Information about the meeting recording | [optional] 

[[Back to Model list]](../README.md#documentation-for-models) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to README]](../README.md)

