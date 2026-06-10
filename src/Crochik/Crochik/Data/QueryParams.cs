namespace Crochik.Data
{
    public interface IQueryParams
    {
        int Top { get; set; }
        string OrderBy { get; set; }
        int Skip { get; set; }
    }

    public class QueryParams : IQueryParams
    {
        public static readonly IQueryParams All = new QueryParams();

        public int Top { get; set; } = 0;
        public string OrderBy { get; set; } = null;
        public int Skip { get; set; } = 0;

        public QueryParams() { }

        public QueryParams(int top = 0, string orderBy = null, int skip = 0)
        {
            Top = top;
            OrderBy = orderBy;
            Skip = skip;
        }
    }
}