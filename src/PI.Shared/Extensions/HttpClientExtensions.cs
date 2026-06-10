using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.OAuth;
using Newtonsoft.Json;
using PI.Shared.Models;
using PI.Shared.Models.Http;

namespace PI.Shared.Extensions;

public class RequestException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public RequestException(HttpStatusCode statusCode)
    {
        StatusCode = statusCode;
    }

    public RequestException(HttpStatusCode statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public RequestException(HttpStatusCode statusCode, string message, Exception innerException) : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}

public static class HttpClientExtensions
{
    public static async Task<Token> GetAccessTokenAsync(this HttpClient client, OAuthOptions Options, string code)
    {
        var tokenRequestParameters = new Dictionary<string, string>()
        {
            { "client_id", Options.ClientId },
            { "client_secret", Options.ClientSecret },
            { "grant_type", "code" },
            { "code", code }
        };
        
        return await GetTokenAsync(client, Options, tokenRequestParameters);
    }

    public static async Task<Token> RefreshTokenAsync(this HttpClient client, OAuthOptions Options, string refreshToken)
    {
        var tokenRequestParameters = new Dictionary<string, string>()
        {
            { "client_id", Options.ClientId },
            { "client_secret", Options.ClientSecret },
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken }
        };

        return await GetTokenAsync(client, Options, tokenRequestParameters);
    }

    public static async Task<Token> GetTokenAsync(this HttpClient client, OAuthOptions Options, Dictionary<string, string> tokenRequestParameters)
    {
        var requestContent = new FormUrlEncodedContent(tokenRequestParameters);
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, Options.TokenEndpoint);
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        requestMessage.Content = requestContent;
        var response = await client.SendAsync(requestMessage);
        if (response.IsSuccessStatusCode)
        {
            var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            // var payload = JObject.Parse(await response.Content.ReadAsStringAsync());
            return new Token(OAuthTokenResponse.Success(payload));
        }
        else
        {
            var error = "OAuth token endpoint failure: " + await Display(response);
            throw new Exception(error);
        }
    }

    public static async Task<T> GetAsync<T>(this HttpClient client, string url, string bearerToken = null)
        where T : class
    {
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (bearerToken != null) requestMessage.Headers.Add("Authorization", $"Bearer {bearerToken}");

        return await SendAsync<T>(client, requestMessage);
    }

    public static async Task<T> PostAsync<T>(this HttpClient client, string url, object body, string bearerToken = null)
        where T : class
    {
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (bearerToken != null) requestMessage.Headers.Add("Authorization", $"Bearer {bearerToken}");

        var json = JsonConvert.SerializeObject(body);
        requestMessage.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return await SendAsync<T>(client, requestMessage);
    }

    public static async Task<T> SendAsync<T>(this HttpClient client, HttpRequestMessage requestMessage)
        where T : class
    {
        var response = await client.SendAsync(requestMessage);
        var body = await response.Content.ReadAsStringAsync();
        if (response.IsSuccessStatusCode)
        {
            if (typeof(T) == typeof(string)) return body as T;
            return JsonConvert.DeserializeObject<T>(body);
        }

        throw new RequestException(response.StatusCode, body);
    }

    private static async Task<string> Display(HttpResponseMessage response)
    {
        var output = new StringBuilder();
        output.Append("Status: " + response.StatusCode + ";");
        output.Append("Headers: " + response.Headers.ToString() + ";");
        output.Append("Body: " + await response.Content.ReadAsStringAsync() + ";");
        return output.ToString();
    }

    public static async Task<Response> SendAsync(this HttpClient client, HttpCallOut callout)
    {
        var request = new HttpRequestMessage
        {
            Method = callout.Request.Method switch
            {
                Method.Get => HttpMethod.Get,
                Method.Post => HttpMethod.Post,
                Method.Put => HttpMethod.Put,
                Method.Delete => HttpMethod.Delete,
                Method.Patch => HttpMethod.Patch,
                Method.Options => HttpMethod.Options,
                Method.Head => HttpMethod.Head,
                Method.Trace => HttpMethod.Trace,
                _ => throw new ArgumentOutOfRangeException($"{callout.Request.Method} is invalid")
            },
            RequestUri = new Uri(callout.Request.Url),
        };

        request.Content = callout.Request.Body != null ? new ByteArrayContent(Encoding.UTF8.GetBytes(callout.Request.Body)) : new ByteArrayContent(Array.Empty<byte>());

        foreach (var header in callout.Request.Headers)
        {
            switch (header.Key)
            {
                case "Accept":
                    request.Headers.Add(header.Key, header.Value);
                    break;

                case "Authorization":
                {
                    var parts = header.Value.FirstOrDefault()?.Split(" ");
                    if (parts?.Length == 2)
                    {
                        request.Headers.Authorization = new AuthenticationHeaderValue(parts[0], parts[1]);
                    }

                    break;
                }
                default:
                    request.Content.Headers.Add(header.Key, header.Value);
                    break;
            }
        }

        var httpResponse = await client.SendAsync(request);
        var response = new Response
        {
            StatusCode = httpResponse.StatusCode,
            Succeeded = httpResponse.IsSuccessStatusCode,
            Body = await httpResponse.Content.ReadAsStringAsync(),
            Headers = new Dictionary<string, string[]>(
                httpResponse.Headers.Select((x => new KeyValuePair<string, string[]>(x.Key, x.Value.ToArray())))
            )
        };

        foreach (var header in httpResponse.Content.Headers)
        {
            response.Headers.TryAdd(header.Key, header.Value.ToArray());
        }

        return response;
    }
}