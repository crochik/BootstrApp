# PI.GTM.API.Model.MeetingReqCreate
## Properties

Name | Type | Description | Notes
------------ | ------------- | ------------- | -------------
**Subject** | **string** | The subject of the meeting. 100 characters maximum. The characters &#39;&amp;gt;&#39; and &#39;&amp;lt;&#39; have to be replaced with the corresponding html character code (&amp;amp;gt; for &amp;#39;&amp;gt;&amp;#39; and &amp;amp;lt; for &amp;#39;&amp;lt;&amp;#39;) | 
**Starttime** | **DateTime?** | The starting time of the meeting. Required ISO8601 UTC string, e.g. 2015-07-01T22:00:00Z | 
**Endtime** | **DateTime?** | The ending time of the meeting. Required ISO8601 UTC string, e.g. 2015-07-01T23:00:00Z | 
**Passwordrequired** | **bool?** | Indicates whether a password is required to join the meeting. Required parameter | 
**Conferencecallinfo** | **string** | A required string. Can be one of the following options: &lt;br&gt;PSTN (PSTN only), &lt;br&gt;Free (PSTN and VoIP), &lt;br&gt;Hybrid, (PSTN and VoIP), &lt;br&gt;Private (you provide numbers and access code), or &lt;br&gt;VoIP (VoIP only). &lt;br&gt;You may also enter plain text for numbers and access codes with a limit of 255 characters | 
**Timezonekey** | **string** | DEPRECATED. Must be provided and set to empty string &#39;&#39; | 
**Meetingtype** | **MeetingType** | The meeting type | 
**CoorganizerKeys** | **List&lt;string&gt;** | Co-organizer keys. Co-organizers can start the meeting on the organizers behalf. Retrieve a list of valid organizers from the \&quot;Get all organizers\&quot; call. | [optional] 

[[Back to Model list]](../README.md#documentation-for-models) [[Back to API list]](../README.md#documentation-for-api-endpoints) [[Back to README]](../README.md)

