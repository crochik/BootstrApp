namespace PI.ProductCatalog.Style
{
    public class WarrantyDescription : FixedColsParser
    {
        public override string Element => "PID";

        public override Token[] Tokens => new Token[]
        {
            Token.Const("Free Form", "F"),
            Token.Const("Warranty Description", "WD"),
            null,
            Token.AN("Warranty Duration",1,12),
            Token.AN("Warranty Description", 1, 80),
            null,
            null,
            Token.AN("Yes/No Condition or Response Code", 1), // Y = Yes, N = No
        };

        protected override LineResult Convert(CatalogParserContext context)
        {
            var result = base.Convert(context);

            context.Section.Warranty = new Models.Warranty
            {
                Duration = context.Values[3].ToString(),
                Description = context.Values[4].ToString(),
                Condition = Equals(context.Values[7], "Y"),
            };

            return result;
        }
    }
}
