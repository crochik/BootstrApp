# PI.Zoom.API.Model.MeetingSettings
## Properties

Name | Type | Description | Notes
------------ | ------------- | ------------- | -------------
**HostVideo** | **bool?** | Start video when host joins meeting | [optional] 
**ParticipantVideo** | **bool?** | Start video when participants join meeting | [optional] 
**CnMeeting** | **bool?** | Host meeting in China | [optional] [default to false]
**InMeeting** | **bool?** | Host meeting in India | [optional] [default to false]
**JoinBeforeHost** | **bool?** | Allow participants to join the meeting before the host starts the meeting. Only used for scheduled or recurring meetings. | [optional] [default to false]
**MuteUponEntry** | **bool?** | Mute participants upon entry | [optional] [default to false]
**Watermark** | **bool?** | Add watermark when viewing shared screen | [optional] [default to false]
**UsePmi** | **bool?** | Use Personal Meeting ID. Only used for scheduled meetings and recurring meetings with no fixed time. | [optional] [default to false]
**ApprovalType** | **int?** |  | [optional] 
**RegistrationType** | **int?** | Registration type. Used for recurring meeting with fixed time only. | [optional] 
**Audio** | **string** | Determine how participants can join the audio portion of the meeting | [optional] [default to AudioEnum.Both]
**AutoRecording** | **string** |  | [optional] [default to AutoRecordingEnum.None]
**EnforceLogin** | **bool?** | Only signed-in users can join this meeting | [optional] 
**EnforceLoginDomains** | **string** | Only signed-in users with specified domains can join meetings | [optional] 
**AlternativeHosts** | **string** | Alternative hosts emails or IDs. Multiple value separated by comma. | [optional] 
**CloseRegistration** | **bool?** | Close registration after event date | [optional] [default to false]
**WaitingRoom** | **bool?** | Enable waiting room | [optional] [default to false]

[[Back to Model list]](../README.md#documentation-for-models) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to README]](../README.md)

