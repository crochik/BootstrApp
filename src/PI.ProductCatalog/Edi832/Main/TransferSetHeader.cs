namespace PI.ProductCatalog
{
    public class TransferSetHeader : FixedColsParser
    {
        public override string Element => "ST";

        public override Token[] Tokens => new Token[]
        {
            Token.Const("Transaction Set Identifier Code = X12 Price/Sales Catalog", "832"),
            Token.AN("Transaction Set Control Number", 4, 9,
                setter: (value, context) => {
                    if (!int.TryParse(value?.ToString(), out var transactionControlNumber))
                    {
                        throw new DataElementParserException(
                            $"Invalid Transaction Control Number, got '{transactionControlNumber}'",
                            "Transaction Set Control Number",
                            2
                        );
                    }

                    if (context.CatalogUpdate.TransactionControlNumber.HasValue && transactionControlNumber<=context.CatalogUpdate.TransactionControlNumber)
                    {
                        throw new DataElementParserException(
                            $"Invalid Transaction Control Number, got '{transactionControlNumber}' but last was '{context.CatalogUpdate.TransactionControlNumber}'",
                            "Transaction Set Control Number",
                            2
                        );
                    }

                    context.CatalogUpdate.TransactionControlNumber = transactionControlNumber;
                }
            ),
        };
    }
}
