namespace PI.ProductCatalog.Style
{
    public class BuilderInformationRequired : FixedColsParser
    {
        public override string Element => "PID";

        public override Token[] Tokens => new Token[]
        {
            Token.Const("Free Form", "F"),
            Token.Const("Builder Information Required", "BLM"),
            null,
            null,
            Token.AN("Builder Program Name", 1, 80),
            null,
            null,
            Token.AN("Yes/No Condition or Response Code", 1), // Y = Yes, N = No
        };

        protected override LineResult Convert(CatalogParserContext context)
        {
            var result = base.Convert(context);

            context.Section.BuilderProgram = new Models.BuilderProgram
            {
                Name = context.Values[4].ToString(),
                Condition = string.Equals(context.Values[7], "Y"),
            };

            return result;
        }        
    }
}
