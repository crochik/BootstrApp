namespace PI.ProductCatalog
{
    public class FunctionalGroupHeader : FixedColsParser
    {
        public override string Element => "GS";
        public override Token[] Tokens => new Token[]
        {
            Token.Const("Functional Identifier Code = Price/Sales Catalog", "SC"),

            Token.AN("Group Sender's Code", 2, 15,
                setter: (value, context) => context.CatalogUpdate.GroupSenderCode = value?.ToString()
            ),

            Token.AN("Group Receiver's Code", 2, 15,
                setter: (value, context) => context.CatalogUpdate.GroupReceiverCode = value?.ToString()
            ),

            Token.Date("Date",  null, "yyyyMMdd"), //YYYYMMDD
            Token.Date("Time", null, "HHmmss", "HHmm"), //HHMM[SS]

            Token.N0("Group Control Number", 1, 9,
                setter: (value, context) => context.CatalogUpdate.GroupControlNumber = (int)value
            ),

            Token.Const("Responsible Agency Code = Accredited Standards Committee X12", "X"),
            Token.Const("Version/Release/Industry Identifier Code", "004010") // Token.AN("Version/Release/Industry Identifier Code", 1, 12)
        };     
    }
}
