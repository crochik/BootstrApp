using System;

namespace PI.ProductCatalog
{
    public class InterchangeStartSegment : FixedColsParser
    {
        public override string Element => "ISA";

        public override Token[] Tokens { get; } = new Token[]
        {
            Token.Const("Authorization Information Qualifier", "00"),
            null, // Token.AN("Authorization Information", 10),

            Token.Const("Security Information Qualifier", "00"),
            null, // Token.AN("Security Information", 10),

            Token.ID("Sender ID Qualifier", 2),
            Token.AN("Sender ID", 15,
                setter: (value, context) =>
                {
                    var senderId = value?.ToString();
                    if (string.Equals(senderId, context.CatalogUpdate.SenderId)) return;
                    if (senderId != null && int.TryParse(senderId, out var nSenderId) && context.CatalogUpdate.SenderId != null && int.TryParse(context.CatalogUpdate.SenderId, out var nExpectedId) && nExpectedId == nSenderId) return;
                    throw new DataElementParserException($"Invalid SenderId, got '{senderId}' but expected '{context.CatalogUpdate.SenderId}'", "Sender ID", 6);
                }
            ),

            Token.ID("Receiver ID Qualifier", 2),
            Token.AN("Receiver ID", 15,
                setter: (value, context) =>
                {
                    var receiverId = value?.ToString();
                    if (string.IsNullOrEmpty(context.CatalogUpdate.ReceiverId))
                    {
                        context.CatalogUpdate.ReceiverId = receiverId;
                    }
                    else if (!string.Equals(context.CatalogUpdate.ReceiverId, receiverId))
                    {
                        throw new DataElementParserException($"Invalid ReceiverId, got '{receiverId}' but expected '{context.CatalogUpdate.ReceiverId}'", "Receiver ID", 8);
                    }
                }
            ),

            Token.Date("Interchange Date", (value, context) =>
                {
                    var date = (DateTime)value;
                    context.CatalogUpdate.InterchangeDate = context.CatalogUpdate.InterchangeDate.HasValue ? date.Date.Add(context.CatalogUpdate.InterchangeDate.Value.TimeOfDay) : date;
                },
                "yyMMdd"
            ), //YYMMDD

            Token.Date("Interchange Time", (value, context) =>
                {
                    var time = (DateTime)value;
                    context.CatalogUpdate.InterchangeDate = context.CatalogUpdate.InterchangeDate.HasValue ? context.CatalogUpdate.InterchangeDate.Value.Date.Add(time.TimeOfDay) : time;
                },
                "HHmm"
            ), // HHMM

            Token.Const("Interchange Control Standards Identifier = U.S. EDI Community of ASC X12, TDCC and UCS", "U"),

            // 832 version 
            Token.ID("Interchange Control Version Number", 5,
                // validate it didn't change?
                // ...
                setter: (value, context) => context.CatalogUpdate.Version = value.ToString()
            ), // Token.Const("00400", "Interchange Control Version Number = ANSI-X12"),

            Token.N0("Interchange Control Number", 9,
                // validate it didn't change?
                // ...
                setter: (value, context) => context.CatalogUpdate.ControlNumber = value as int?
            ),

            Token.Const("Acknowledgement Requested = No Acknowledgement Requested", "0"),

            Token.ID("Test Indicator", 1,
                // validate it didn't change?
                // ...
                // T = Test
                // P = Production
                setter: (value, context) => context.CatalogUpdate.IsTest = string.Equals(value, "T")
            ),

            Token.AN("Sub Element Separator", 1),
        };
    }
}