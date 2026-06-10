using System;

namespace PI.ProductCatalog
{
    public class DateReference : FixedColsParser
    {
        private readonly Action<DateTime, CatalogParserContext> _setter;

        public DateReference(Action<DateTime, CatalogParserContext> setter = null)
        {
            this._setter = setter;
        }

        public override string Element => "DTM";
        public override Token[] Tokens => new Token[]
        {
            // 007 = Effective 
            // 162 = Pending
            // 197 = Dropped
            // 433 – Error / Remove (Sent in error)            
            Token.ID("Qualifier", 3),
            Token.Date("Date", (v,c)=>_setter?.Invoke((DateTime)v, c), "yyyyMMdd"),
        };
    }
}