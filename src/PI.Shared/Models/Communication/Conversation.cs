namespace PI.Shared.Models;

public class Conversation : FlowObjectModel
{
    public string CommunicationChannel { get; set; }
    public CommunicationDirection Direction { get; set; }
    public string[] Parties { get; set; }
}