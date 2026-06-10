using System.Net.Http;
using System.Net.Http.Headers;

namespace PI.CompanyCam;

public partial class CompanyCamClient
{
    private readonly string _accessToken;

    public CompanyCamClient(HttpClient client, string accessToken)
        : this(client)
    {
        _accessToken = accessToken;
    }
    
    partial void PrepareRequest(HttpClient client, HttpRequestMessage request, string url)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    // partial void PrepareRequest(System.Net.Http.HttpClient client, System.Net.Http.HttpRequestMessage request, System.Text.StringBuilder urlBuilder)
    // {
    //     request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
    // }
    //
    // partial void ProcessResponse(System.Net.Http.HttpClient client, System.Net.Http.HttpResponseMessage response)
    // {
    //     Console.WriteLine("x");   
    // }
}