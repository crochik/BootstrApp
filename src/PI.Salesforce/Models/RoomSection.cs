using PI.Shared.Salesforce.Models;

namespace PI.Salesforce.Models;

public class RoomSection : SfRoomSection
{
    public Room Room { get; set; }
}