namespace PI.ProductCatalog
{
    public class AccountNumber : FixedColsParser
    {
        public override string Element => "REF";

        public override Token[] Tokens => new Token[]{
            Token.Const("Reference Identification Qualifier", "11"),
            Token.AN("Account Number", 1,30,
                setter: (value, context) => context.CatalogUpdate.AccountNumber = value?.ToString()
            )
        };
    }
}
