using System;

namespace PI.Shared.Constants
{
    public class LeadStatusIds
    {
        public static Guid Unassigned = Guid.Parse("6186F91D-BAE3-4E5D-A7F8-0D6F957395EA");
        // public static Guid FollowUp = Guid.Parse("1B9D97AB-5F93-4636-B6D5-26E23F659168");
        public static Guid Initial = Guid.Parse("1D0D0BF0-4C72-4B79-8D68-BD11802DF9EC");
        // public static Guid Dead = Guid.Parse("77AE05AD-91F4-437D-B71C-C17145D796EA");
        public static Guid Scheduled = Guid.Parse("266CD1C2-215B-4B85-9074-DD3868196C55");
        // public static Guid Assigned = Guid.Parse("2D9E4FD1-5D19-404C-9DA8-FD3B2D34C57D");
        public static Guid ExportedToSalesforce = Guid.Parse("cb966ab3-38e1-473a-b0bd-5f362c5bca50");
    }
}