using System;

namespace PI.ProductCatalog.Models;

public enum DoorType {
    /// Gap in the wall with no door leaf.
    Opening,

    /// Single door hinged on the left (start) side of the opening.
    LeftHinge,

    /// Single door hinged on the right (end) side of the opening.
    RightHinge,

    /// Double door — two leaves, one hinged on each side.
    DoubleDoor,
}

public class Door
{
    public string Name { get; set; }
    
    /// Width of the door in world coordinates.
    public Measurement Width { get; set; }
    
    public bool AddTransition { get; set; }
    public bool TrimDoor { get; set; }

    /// The segment (wall) index where the door is placed.
    /// Segment i connects vertices[i] to vertices[(i+1) % n].
    public int WallIndex { get; set; }

    /// Distance from the start vertex (vertices[wallIndex]) to the
    /// near edge of the door, measured along the wall.
    public decimal? Offset { get; set; }

    public DoorType? Type { get; set; }
    
    /// Whether the door opens inward (true) or outward (false).
    /// Only relevant when [type] is not [DoorType.opening].
    public bool? OpensInward { get; set; }
    
    [Obsolete("Use type instead")]
    public bool? IsOpening { get; set; }
    
    /// <summary>
    /// 2 vertices shape
    /// </summary>
    public ShapePoint[] Shape { get; set; }
}