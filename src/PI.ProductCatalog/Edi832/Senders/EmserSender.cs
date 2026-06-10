using System;
using System.Collections.Generic;
using PI.ProductCatalog.Models;

namespace PI.ProductCatalog.Edi832
{
    public class EmserSender : CatalogFormat2_2_02 
    {
        public override string SenderId => "049351117";
        public override string Name => "Emser Tile";

        // ftp://ediftp.emser.com/inbox
        public override Uri Url => new("ftp://fciedi.emser.com/inbox");

        public override IEnumerable<string> IgnoreColorProperties => new string[]
        {
            nameof(SLN.StyleName),
            nameof(SLN.StyleNumber),
            nameof(SLN.ManufacturerStyleName),
            nameof(SLN.ManufacturerStyleNumber),
        };
    }
}