using System;

namespace PI.ProductCatalog.Edi832
{
    public class UnkownSender : CatalogFormat2_2_02
    {
        public override string SenderId => string.Empty;
        public override string Name => "Unknown";
        public override Uri Url => null;
    }
}