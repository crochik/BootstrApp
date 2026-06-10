using System;

namespace PI.ProductCatalog.Subline
{
    public class Message : FixedColsParser
    {
        private readonly string _name;
        private readonly string _qualifier;
        private readonly Action<string, CatalogParserContext> _setter;

        public Message(string name, string qualifier, Action<string, CatalogParserContext> setter = null)
        {
            this._name = name;
            this._qualifier = qualifier;
            this._setter = setter;
        }

        public override string Element => _qualifier;

        public override Token[] Tokens => new Token[] {
            Token.Const("Note Reference Code", _qualifier),
            Token.AN(_name, 1, 4096, setter: (v,c)=>_setter?.Invoke(v.ToString(), c)),
        };
    }
}
