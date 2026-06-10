using System;

namespace PI.ProductCatalog
{
    public class ProductInfo : FixedColsParser
    {
        protected readonly string _charCode;
        protected readonly int _min;
        protected readonly int _max;
        public Action<string, CatalogParserContext> Setter { get; }
        public object DefaultValue { get; }
        public bool IsMandatory { get; set; }

        public ProductInfo(string charCode, int min, int max, Action<string, CatalogParserContext> setter = null, object defaultValue = null, bool isMandatory = true)
        {
            this._charCode = charCode;
            this._min = min;
            this._max = max;

            this.Setter = setter;
            this.DefaultValue = defaultValue;
            this.IsMandatory = isMandatory;
        }

        public override string Element => "PID";

        public override Token[] Tokens => new Token[]
        {
            Token.Const("Free Form", "F"),
            Token.Const("PID", _charCode), // 2-3
            null,
            null,
            Token.AN("Value", _min, _max, IsMandatory, (v,c)=>Setter?.Invoke(v?.ToString(), c), DefaultValue),
        };
    }
}
