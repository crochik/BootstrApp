using System;
using System.IO;
using System.Threading.Tasks;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.HangoutsChat.v1;
using Google.Apis.HangoutsChat.v1.Data;
using PI.Shared.Models;

namespace PI.Google;

public class GoogleClient
{
    private HangoutsChatService _chatService = null;

    private HangoutsChatService ChatService => _chatService ??= new HangoutsChatService();
    private GoogleClientSecrets ClientSecrets { get; set; }
    private UserCredential Credentials { get; set; }

    public static async Task<GoogleClientSecrets> GetClientConfiguration(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        return await GoogleClientSecrets.FromStreamAsync(stream);
    }

    // public static UserCredential GetCredentials(GoogleClientSecrets secrets, string accessToken, string refreshToken)
    // {
    //     var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
    //     {
    //         ClientSecrets = new ClientSecrets
    //         {
    //             ClientId = "xxx.apps.googleusercontent.com",
    //             ClientSecret = "xxxx"
    //         },
    //         Scopes = scopes,
    //         DataStore = new FileDataStore("Store")
    //     });

    //     var token = new TokenResponse
    //     {
    //         AccessToken = "[your_access_token_here]",
    //         RefreshToken = "[your_refresh_token_here]"
    //     };

    //     var credential = new UserCredential(flow, "me", token);
    // }

    /// <summary>
    /// Send message using webhooks url
    /// </summary>
    public async Task<Result<Message>> SendAsync(Uri uri, Message message)
    {
        var request = ChatService.Spaces.Messages.Create(message, "spaces/tbd");
        request.ModifyRequest = (req) =>
        {
            req.RequestUri = uri;
        };

        try
        {
            var response = await request.ExecuteAsync();
            return Result.Success(response);
        }
        catch (GoogleApiException ex)
        {
            return Result.Error<Message>(ex.Message);
        }
    }
    
    public async Task<Result<Message>> SendAsync(string spaceId, Message message, string key, string token)
    {
        var request = ChatService.Spaces.Messages.Create(message, $"spaces/{spaceId}");
        request.Key = key;
        request.ModifyRequest = (req) =>
        {
            req.RequestUri = new Uri(req.RequestUri.ToString() + $"&token={token}");
        };

        try
        {
            var response = await request.ExecuteAsync();
            return Result.Success(response);
        }
        catch (GoogleApiException ex)
        {
            return Result.Error<Message>(ex.Message);
        }
    }
}