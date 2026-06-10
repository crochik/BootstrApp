using System;

namespace PI.ProductCatalog
{
    public class DataElementParserException : Exception
    {
        public string ErrorMessage { get; }
        public string DataElement { get; }
        public int Index { get; }        
        
        public DataElementParserException(string errorMessage, string dataElement, int index) :
            base($"{errorMessage}: '{dataElement}' on position {index}")
        {
            ErrorMessage = errorMessage;
            DataElement = dataElement;
            Index = index;
        }
    }
}
