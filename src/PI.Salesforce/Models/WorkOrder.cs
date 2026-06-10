using PI.Shared.Salesforce.Models;

namespace PI.Salesforce.Models;

public class WorkOrder : SfWorkOrder
{
    public SfFloorPlan Floorplan { get; set; }   
}