using System;
using System.Linq;
using System.Threading.Tasks;
using Crochik.Mongo;
using Microsoft.Extensions.Logging;
using Octokit;
using PI.Shared.Constants;
using PI.Shared.Models;
using PI.Shared.Services;

namespace PI.OpenAPI.Services;

public class GitHubService
{
    private readonly ILogger<GitHubService> _logger;
    private readonly MongoConnection _connection;
    private readonly IntegrationAuthService _integrationAuthService;

    public GitHubService(ILogger<GitHubService> logger, MongoConnection connection, IntegrationAuthService integrationAuthService)
    {
        _logger = logger;
        _connection = connection;
        _integrationAuthService = integrationAuthService;
    }

    public async Task<GitHubClient> GetClientAsync(IEntityContext context)
    {
        var config = await _integrationAuthService.GetIntegrationConfigurationAsync(context, IntegrationIds.GitHub);
        if (!config.IsSuccess)
        {
            _logger.LogError("Failed to get configuration");
            return null;
        }

        var accessToken = await _integrationAuthService.GetAccessTokenAsync(context, config.Value);

        var github = new GitHubClient(new ProductHeaderValue("ProgramInterface.com"));
        github.Credentials = new Credentials(accessToken.Value);

        return github;
    }

    public async Task<Repository> GetOrCreateRepositoryAsync(IEntityContext context, string name, string owner = null, string template = null, string templateOwner = null)
    {
        var client = await GetClientAsync(context);
        return await client.GetOrCreateRepositoryAsync(name, owner, template, templateOwner);
    }

    public async Task<Reference> CreateBranchAsync(IEntityContext context, Repository repo, string newBranchName, string fromBranchName = null)
    {
        var client = await GetClientAsync(context);
        return await client.CreateBranchAsync(repo, newBranchName, fromBranchName);
    }

    
    // get file
    // try
    // {
    //     var content = await client.Repository.Content.GetRawContentByRef(repo.Owner.Login, repo.Name, request.Path, branch.Object.Sha);
    //
    //     // exists
    //     // ...
    //
    //     return;
    // }
    // catch (NotFoundException ex)
    // {
    //     // ...             
    // }
}

public static class GitHubClientExtensions
{
    public static async Task<Repository> GetOrCreateRepositoryAsync(this GitHubClient client, string name, string owner = null, string template = null, string templateOwner = null)
    {
        if (string.IsNullOrEmpty(owner))
        {
            throw new NotImplementedException("Can't handle it yet");
        }

        try
        {
            var repo = await client.Repository.Get(owner, name);
            return repo;
        }
        catch (NotFoundException ex)
        {
            // ...
        }

        if (!string.IsNullOrEmpty(template))
        {
            return await client.Repository.Generate(templateOwner, template, new NewRepositoryFromTemplate(name)
            {
                Owner = owner,
                Description = $"{name} Created by ProgramInterface.com",
                Private = true,
            });
        }

        return await client.Repository.Create(
            // owner,
            new NewRepository(name)
            {
                Private = true,
                Visibility = RepositoryVisibility.Private,
                Description = $"{name} Created by ProgramInterface.com",
                Homepage = "https://programinterface.com",
            }
        );
    }
    
    public static async Task<Reference> GetOrCreateBranchAsync(this GitHubClient client, Repository repo, string newBranchName, string fromBranchName=null)
    {
        try
        {
            return await client.Git.Reference.Get(repo.Owner.Login, repo.Name, $"refs/heads/{newBranchName}");
        }
        catch (NotFoundException)
        {
            // ...
        }
        
        return await client.CreateBranchAsync(repo, newBranchName, fromBranchName);
    }
    
    public static async Task<Reference> CreateBranchAsync(this GitHubClient client, Repository repo, string newBranchName, string fromBranchName=null)
    {
        var branchReference = await client.Git.Reference.Get(repo.Owner.Login, repo.Name, $"refs/heads/{fromBranchName ?? repo.DefaultBranch}");

        // Create a new branch reference
        var sha = branchReference.Object.Sha;
        var newBranchReference = new NewReference($"refs/heads/{newBranchName}", sha);

        return await client.Git.Reference.Create(repo.Id, newBranchReference);
    }

    public static async Task<RepositoryContentChangeSet> UpsertFileAsync(this GitHubClient client, Repository repo, Reference branch, UpsertFileRequest request)
    {
        // var tree = await client.Git.Tree.Get(repo.Owner.Login, repo.Name, branch.Object.Sha);
        var tree = await client.Git.Tree.GetRecursive(repo.Owner.Login, repo.Name, branch.Object.Sha);
        
        var branchName = branch.Ref.Split('/')[^1];
        
        var existing = tree.Tree.FirstOrDefault(x => x.Path == request.Path);
        if (existing != null)
        {
            return await client.Repository.Content.UpdateFile(repo.Owner.Login,
                repo.Name,
                request.Path,
                new UpdateFileRequest(request.Message, request.Content, existing.Sha, branchName)
            );
        }
        
        return await client.Repository.Content.CreateFile(
            repo.Owner.Login, 
            repo.Name,
            request.Path,
            new CreateFileRequest(
                request.Message,
                request.Content,
                branchName
            )
        );
    }   
}

public class UpsertFileRequest
{
    public string Path { get; init;  }
    public string Content { get; init;  }
    public string Message { get; init;  }
}
