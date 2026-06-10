using System;
using System.Threading.Tasks;

namespace PI.ProductCatalog.Style
{
    public class Reference : FixedColsParser
    {
        private readonly int _length;
        private readonly ValueSetter _setter;

        public Reference(int length, ValueSetter setter = null)
        {
            this._length = length;
            this._setter = setter;
        }

        public override string Element => "REF";

        public override Token[] Tokens => new Token[]
        {
            Token.ID("Reference",2),
            Token.AN("Value", 1, _length, setter: _setter)
        };
    }

    /// <summary>
    /// Flexivle Reference
    /// The spec says that for REF*19 the value should be in REF[2]
    /// Emser has the value in REF[1]
    /// </summary>
    public class Reference3 : FixedColsParser
    {
        private readonly int _length;
        private readonly Action<string, CatalogParserContext> _setter;

        public Reference3(int length, Action<string,CatalogParserContext> setter)
        {
            this._length = length;
            this._setter = setter;
        }

        public override string Element => "REF";

        public override Token[] Tokens => new Token[]
        {
            Token.ID("Reference",2),
            Token.AN("Value1", 1, _length, false),
            Token.AN("Value2", 1, _length, false),
        };

        protected override LineResult Convert(CatalogParserContext context)
        {
            var result = base.Convert(context);
            if (_setter==null) return result;

            var value = context.Values[1] ?? context.Values[2];
            if (value!=null) _setter(value.ToString(), context);

            return result;
        }
    }    
}
