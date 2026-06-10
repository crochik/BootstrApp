namespace PI.ProductCatalog
{
    /// <summary>
    /// Allow multiple!?!?!?
    /// </summary>
    public class VendorName : FixedColsParser
    {
        public override string Element => "N1";

        public override Token[] Tokens => new Token[]
        {
            Token.Const("Entity Identifier Code = Vendor Name", "VN"),
            Token.AN("Vendor Name", 1, 60, 
                setter: (value, context) => context.CatalogUpdate.Vendor = value?.ToString()
            ),
        };
    }
}
