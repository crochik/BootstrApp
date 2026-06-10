using System;

namespace PI.Shared.Constants
{
    public class ObjectTypeIds
    {
        public static Guid Lead = Guid.Parse("fc02e583-a6af-4613-ac19-ffc030a34013");
        public static Guid ObjectStatus = Guid.Parse("77d7604d-df9e-42c3-b9a2-5744a7774ead");
        public static Guid ObjectType = Guid.Parse("e95a5e18-f7b7-40cd-baea-3a64dbb9105f");
        public static Guid Appointment = Guid.Parse("ef775f7d-fa26-4902-87b7-17df205d0735");

        public static Guid Account = Guid.Parse("83bb62dd-4e0b-4f82-bc2c-8d579b195a39");
        public static Guid Organization = Guid.Parse("7c3ffa58-53b0-4e59-ba4f-53f692621f9f");
        public static Guid User = Guid.Parse("c0e2b0ac-0816-4bb7-9417-40bc1579a636");

        public static Guid LeadType = Guid.Parse("4f637eda-dc7e-41a7-8b47-d679020ccfd5");

        // not first class?
        public static Guid UserAction = Guid.Parse("1cc97873-50d4-443e-a81b-4dc8acec40e1");
    }
}