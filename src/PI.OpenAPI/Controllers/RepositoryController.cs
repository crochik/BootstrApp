using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Octokit;
using PI.OpenAPI.Services;
using PI.Shared.Controllers;

namespace PI.OpenAPI.Controllers;

[Route("/openapi/v1/[controller]")]
public class RepositoryController : APIController
{
    private readonly GitHubService _githubService;
    private readonly IHttpClientFactory _httpClientFactory;
    private HttpClient Client => _httpClientFactory.CreateClient(nameof(RepositoryController));

    public RepositoryController(GitHubService githubService, IHttpClientFactory httpClientFactory)
    {
        _githubService = githubService;
        _httpClientFactory = httpClientFactory;
    }

    [Authorize("admin")]
    [HttpGet]
    public async Task<IEnumerable<Repository>> Get()
    {
        var github = await _githubService.GetClientAsync(Context);

        // TODO: get user from identity?
        // ...
        return await github.Repository.GetAllForUser("crochik", new ApiOptions
        {
            PageSize = 10,
        });
    }

    [Authorize("admin")]
    [HttpPost]
    public async Task<Repository> Post(string name)
    {
        var owner = "crochik";
        
        var client = await _githubService.GetClientAsync(Context);
        var repo = await client.GetOrCreateRepositoryAsync(name, owner);

        
        // var commitMessage = "Initial";

        // await client.Repository.Content.CreateFile(
        //     owner,
        //     name,
        //     "README.md",
        //     new CreateFileRequest(
        //         commitMessage,
        //         "# ProgramInterface.com\nThis is a test",
        //         "main"
        //     )
        // );
        //
        // await client.Repository.Content.CreateFile(
        //     owner,
        //     name,
        //     "test/app/README.md",
        //     new CreateFileRequest(
        //         commitMessage,
        //         "# ProgramInterface.com\nThis is a test",
        //         "main"
        //     )
        // );

        return repo;
    }

    [Authorize("admin")]
    [HttpPost("Client")]
    public async Task<Repository> CreateClientRepositoryAsync([FromBody] CreateClientRepoRequest request)
    {
        var client = await _githubService.GetClientAsync(Context);
        var repo = await client.GetOrCreateRepositoryAsync(request.RepositoryName, request.RepositoryOwner, templateOwner: request.TemplateRepositoryOwner, template: request.TemplateRepositoryName);

        var specContent = await Client.GetStringAsync(request.SourceUrl);

        var result = OpenApiDocument.Parse(specContent);
        var doc = result.Document;
        var diagnostic = result.Diagnostic;
        var serializedDoc = await doc.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_0);
        
        // use hashcode to detect changes?
        // ...
        // doc.HashCode

        var branchName = $"{doc.Info.Version}";
        
        var branch = await client.GetOrCreateBranchAsync(repo, branchName);
        
        await client.UpsertFileAsync(repo, branch, 
            new UpsertFileRequest
            {
                Path    = "swagger.json",
                Message = request.SourceUrl,
                Content = serializedDoc,
            }
        ); 
        
        return repo;
    }
}

public class CreateClientRepoRequest
{
    public string SourceUrl { get; set; } 
    public string RepositoryName { get; set; }
    public string RepositoryOwner { get; set; }

    public string TemplateRepositoryName { get; set; } = "template-openapitools-dart";
    public string TemplateRepositoryOwner { get; set; } = "crochik";
}