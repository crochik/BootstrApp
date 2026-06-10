using Newtonsoft.Json;
using PI.Shared.Salesforce.Models;

namespace PI.Salesforce.Models;

public class Room : SfRoom
{
    public string FurnitureToMove => InstallationMapLoader.GetSetting(FurnitureToMoveId);
    public string RoomType => InstallationMapLoader.GetSetting(RoomTypeId);
    public SfRoomDrawInfo Info => RoomDraw != null ? JsonConvert.DeserializeObject<SfRoomDrawInfo>(RoomDraw) : null;
    public SfExternalLink[] ExternalLinks { get; set; }

}