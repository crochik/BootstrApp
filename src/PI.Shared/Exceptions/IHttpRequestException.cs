namespace PI.Shared.Exceptions
{
    public interface IHttpRequestException
    {
        int? StatusCode { get; }
    }
}