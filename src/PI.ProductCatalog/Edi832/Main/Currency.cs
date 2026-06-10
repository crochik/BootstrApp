namespace PI.ProductCatalog
{
    public class Currency : FixedColsParser
    {
        public override string Element => "CUR";

        public override Token[] Tokens => new Token[] {
            Token.Const("Entity Identifier = Selling Entity", "SE"),

            // USD
            // CAN
            Token.ID("Currency Code", 3, 
                setter: (value, context) => context.CatalogUpdate.Currency = string.Equals(value, "CAN") ? Models.Currency.CAN : Models.Currency.USD
            ),
        };
    }
}
