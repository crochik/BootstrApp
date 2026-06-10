namespace PI.Shared.Models.Interfaces;

public interface IWithLocation
{
    public GeoJSON.Point Location { get; set; }
}