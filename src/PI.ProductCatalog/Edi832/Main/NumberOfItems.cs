namespace PI.ProductCatalog
{
    public class NumberOfItems : FixedColsParser
    {
        public override string Element => "CTT";

        public override Token[] Tokens => new Token[]
        {
            Token.N0("Number of Line Items", 1, 6)
        };

        public override LineResult ParseLine(CatalogParserContext context)
        {
            // push null section so last becomes ready to be popped 
            context.Section = null;
            
            // back to main loop
            context.Loop = Loop.Main;
            
            return base.ParseLine(context);
        }
    }

    public class TransactionTrailer : FixedColsParser
    {
        public override string Element => "SE";

        public override Token[] Tokens => new Token[]
        {
            Token.N0("Number of Included Segments", 1, 10),
            Token.AN("Transaction Set Control Number", 4, 9),
        };
    }

    public class FunctionalGroupTrailer : FixedColsParser
    {
        public override string Element => "GE";

        public override Token[] Tokens => new Token[]
        {
            Token.N0("Number of Transaction Sets Included", 1, 6),
            Token.N0("Group Control Number", 1, 9),
        };
    }

    public class InterchangeTrailer : FixedColsParser
    {
        public override string Element => "IEA";

        public override Token[] Tokens => new Token[]
        {
            Token.N0("Number of Included Functional Groups", 1, 5),
            Token.N0("Interchange Control Number", 9),
        };
    }
}
