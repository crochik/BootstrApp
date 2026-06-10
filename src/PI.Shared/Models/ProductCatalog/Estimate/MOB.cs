using System.ComponentModel;
using System.Reflection;

namespace PI.ProductCatalog.Models;

public enum MOB
{
    [Description("Appliance Move")] MOBAM,
    [Description("Basic 21 Underlayment")] MOBB21U,
    [Description("Basic Refinishing")] MOBBF,

    [Description("Berber or Pattern Carpet Installation")]
    MOBBPCI,
    [Description("Basic Resurfacing")] MOBBS,

    [Description("Boxed Stair Carpet Install")]
    MOBBSCI,
    [Description("Basic Subfloor Prep")] MOBBSF,

    [Description("Boxed Stair Wood Install")]
    MOBBSWI,
    [Description("Cove Base")] MOBCB,

    [Description("Memory Foam Rebond Carpet Cushion with Moisture Barrier")]
    MOBCHCC,

    [Description("Memory Foam Carpet Cushion")]
    MOBCOCC,

    [Description("Rebond Carpet Cushion with Moisture Barrier")]
    MOBCOHC,

    [Description("Post Installation Disinfection Service")]
    MOBDIS,

    [Description("Double Upholstered Carpet Stair Install")]
    MOBDUCSI,

    [Description("Double Upholstered Wood Stair Install")]
    MOBDUWSI,
    [Description("Embossing Leveler")] MOBEL,
    [Description("Fiber Cushion")] MOBFCC,

    [Description("Straight Floating Vinyl TilePlank Installation")]
    MOBFCVTPI,

    [Description("Floating Floor Installation")]
    MOBFLI,

    [Description("Floor Tile With backer board Installation")]
    MOBFTBI,

    [Description("Floor Tile on Concrete Installation")]
    MOBFTCI,

    [Description("Refinishing with Dust Minimization Process")]
    MOBFWDMP,
    [Description("Tile Grout")] MOBG,

    [Description("Glue Down Berber or Pattern Carpet Installation")]
    MOBGDBPCI,

    [Description("Glue Down Carpet Installation")]
    MOBGDCI,

    [Description("Specialty Glue Down Carpet Installation")]
    MOBGDSCI,

    [Description("Grouted Pattern Vinyl TilePlank Installation")]
    MOBGPVTPI,

    [Description("Grouted Vinyl TilePlank Installation")]
    MOBGVTPI,

    [Description("Half Inch Backer Board")]
    MOBHBB,
    [Description("Heavy Furniture Move")] MOBHFM,
    [Description("Heavy Items to Move")] MOBHITM,

    [Description("Significant Subfloor Prep")]
    MOBHSF,
    [Description("Hard Surface Freight - By Area")] MOBHSFT,
    [Description("Hard Surface Freight - Drop Charge")] MOBHSFRT,
    [Description("Hardwood Adhesive")] MOBHWA,

    [Description("Hardwood Glue Down Installation")]
    MOBHWGDI,

    [Description("Hardwood Glue Down Pattern Installation")]
    MOBHWGDIP,

    [Description("Hardwood NailStaple Installation")]
    MOBHWNI,

    [Description("Hardwood NailStaple Pattern Installation")]
    MOBHWNIP,
    [Description("Install Cove Base")] MOBICB,

    [Description("Remove and reinstall existing baseboards labor")]
    MOBIEB,

    [Description("Remove and Reinstall Existing Finished Trim Labor")]
    MOBIEQ,

    [Description("Remove Existing and install new baseboards labor")]
    MOBINB,

    [Description("Remove Existing and install new quarter round labor")]
    MOBINQ,

    [Description("Install quarter round to existing base labor")]
    MOBIQEB,

    [Description("Install Quarter inch Plywood")]
    MOBIQIP,
    [Description("Install Tile Base")] MOBITB,
    [Description("Install Transition")] MOBITS,

    [Description("Loose Lay Sheet Vinyl Installation")]
    MOBLLSVI,
    [Description("New Baseboards")] MOBNBB,
    [Description("New Quarter Round")] MOBNQR,
    [Description("Oak Stair Treads")] MOBOST,

    [Description("Perimeter Bonded Sheet Vinyl Installation")]
    MOBPBSVI,
    [Description("Premium Underlayment")] MOBPU,

    [Description("Pattern Vinyl TilePlank Installation")]
    MOBPVTPI,

    [Description("Quarter Inch Backer Board")]
    MOBQBB,
    [Description("Quarter inch Plywood")] MOBQIP,
    [Description("Rebond Carpet Cushion")] MOBRCC,

    [Description("Removal and Haul Away of Carpet and Pad")]
    MOBRCP,

    [Description("Remove Tackstrip and Prep Floor for Hard Surface")]
    MOBRCPNC,

    [Description("Remove Existing and Install New Cove Base")]
    MOBREINCB,

    [Description("Remove Existing and Install New Tile Base")]
    MOBREINTB,

    [Description("Removal and Haul Away of Floating Floor")]
    MOBRFLOAT,

    [Description("Removal and Haul Away of Hardwood Nailed or Glued")]
    MOBRHW,

    [Description("Refinishing with Premium Finish")]
    MOBRPF,

    [Description("Refinishing with Premium Finish and Dust Minimization Process")]
    MOBRPFDM,

    [Description("Removal and Haul Away of Resilient")]
    MOBRSV,

    [Description("Removal and Haul Away of Resilient w Subfloor")]
    MOBRSVSF,

    [Description("Removal and Haul Away of Tile and Backer Board")]
    MOBRTBB,

    [Description("Removal and Haul Away of Tile over Concrete")]
    MOBRTC,

    [Description("Removal and Haul Away of Vinyl Tile or Plank")]
    MOBRVT,

    [Description("Removal and Haul Away of Vinyl Tile or Plank w Subfloor")]
    MOBRVTSF,

    [Description("Standard 31 Underlayment")]
    MOBS31U,

    [Description("Standard Carpet Cushion")]
    MOBSCC,

    [Description("Standard Carpet Installation")]
    MOBSCI,

    [Description("Boxed Stair Sand and Finish")]
    MOBSFBS,

    [Description("Double Upholstered Stair Sand and Finish")]
    MOBSFDUS,

    [Description("Standard Furniture Move")]
    MOBSFM,

    [Description("Single Upholstered Stair Sand and Finish")]
    MOBSFSUS,

    [Description("Standard Hardwood Underlayment")]
    MOBSHU,

    [Description("Specialty Carpet Installation")]
    MOBSPCI,
    [Description("Soft Surface Freight")] MOBSSFT,

    [Description("Fully Glued Sheet Vinyl Installation")]
    MOBSSVI,

    [Description("Single Upholstered Carpet Stair Install")]
    MOBSUCSI,

    [Description("Single Upholstered Wood Stair Install")]
    MOBSUWSI,
    [Description("Sheet Vinyl Adhesive")] MOBSVA,

    [Description("Straight Vinyl TilePlank Installation")]
    MOBSVTPI,

    [Description("Resurfacing with Dust Minimization Process")]
    MOBSWDMP,
    [Description("Thinset")] MOBT,
    [Description("Tile Base")] MOBTB,

    [Description("Trim Doors for Clearance")]
    MOBTD,

    [Description("Toilet Pedestal Sink Other Move")]
    MOBTPSM,
    [Description("Transition Strip")] MOBTS,
    [Description("Vapor Barrier")] MOBVB,
    [Description("LVT Grout")] MOBVG,

    [Description("Vinyl PlankTile Adhesive")]
    MOBVPTA,

    [Description("Wall Tile With backer board Installation")]
    MOBWTBI,

    [Description("Wall Tile on Drywall Installation")]
    MOBWTDI,

    [Description("Removal and Haul Away of Carpet Glue Down")]
    MOPRCG
}

public static class MOBExtensions
{
    public static string GetDescription(this MOB mob)
    {
        var fieldInfo = typeof(MOB).GetField(mob.ToString());
        return fieldInfo?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? mob.ToString();
    }
}
