using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PI.QuickBooks.Services;

public class QuickBooksGraphQL
{
    private const string QuickBooksApiBaseUrl = "https://qb.api.intuit.com"; // For production. Use "https://sandbox-qb.api.intuit.com" for sandbox.

    public async Task<Project> CreateProjectAsync(string accessToken, string companyId, string customerId)
    {
        string newProjectName = $"New Website Build - {DateTime.Now:yyyy-MM-dd}";
        DateTime projectStartDate = DateTime.UtcNow;
        DateTime projectEndDate = projectStartDate.AddMonths(3);

        Console.WriteLine($"Attempting to create project '{newProjectName}' for customer ID '{customerId}'...");

        // 4. Call the function to create the project.
        return await CreateQuickBooksProjectAsync(
            accessToken,
            companyId,
            customerId,
            newProjectName,
            projectStartDate,
            projectEndDate
        );
    }

    /// <summary>
    ///* Creates a project for a specified customer in QuickBooks Online.
    /// </summary>
    public static async Task<Project> CreateQuickBooksProjectAsync(string accessToken, string accountId, string customerId, string projectName, DateTime startDate, DateTime endDate)
    {
        // The GraphQL endpoint for QuickBooks Online API.
        var requestUrl = $"{QuickBooksApiBaseUrl}/graphql";

        // The GraphQL mutation. This defines the operation we want to perform.
        // It uses variables (prefixed with $) to pass in dynamic data.
        var graphQLMutation = @"
                mutation CreateProject($customer: ID!, $name: String!, $startDate: Date, $endDate: Date) {
                  projectManagementCreateProject(
                    input: {
                      customerId: $customer
                      name: $name
                      startDate: $startDate
                      endDate: $endDate
                      status: NOT_STARTED
                    }
                  ) {
                    project {
                      id
                      name
                      status
                    }
                  }
                }";

        // Create the request payload object which includes the query and variables.
        var requestBody = new
        {
            query = graphQLMutation,
            variables = new
            {
                customer = customerId,
                name = projectName,
                startDate = startDate.ToString("yyyy-MM-dd"),
                endDate = endDate.ToString("yyyy-MM-dd")
            }
        };

        // Serialize the C# object into a JSON string.
        var jsonRequestBody = JsonConvert.SerializeObject(requestBody);

        // Configure the HttpClient.
        using (var client = new HttpClient())
        {
            // Set the necessary headers for the API request.
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Create the HTTP request message.
            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new StringContent(jsonRequestBody, Encoding.UTF8, "application/json")
            };

            // Add the realmId to the request headers. This is crucial for QBO to identify the company.
            request.Headers.Add("intuit-companyid", accountId);


            // Send the request to the QuickBooks API endpoint.
            var response = await client.SendAsync(request);

            // Read the response content.
            var responseString = await response.Content.ReadAsStringAsync();

            // Check if the request was successful.
            if (response.IsSuccessStatusCode)
            {
                // Deserialize the JSON response into our C# helper classes.
                var qboResponse = JsonConvert.DeserializeObject<GraphQLResponse>(responseString);

                // Check for GraphQL-specific errors returned in the response body.
                if (qboResponse?.Errors != null && qboResponse.Errors.Length > 0)
                {
                    var errorMessages = string.Join("; ", Array.ConvertAll(qboResponse.Errors, e => e.Message));
                    throw new Exception($"GraphQL API returned errors: {errorMessages}");
                }

                if (qboResponse?.Data?.ProjectManagementCreateProject?.Project == null)
                {
                    throw new Exception("Failed to create project. The response did not contain the expected project data.");
                }

                // Return the created project details.
                return qboResponse.Data.ProjectManagementCreateProject.Project;
            }
            else
            {
                // If the HTTP request itself failed, throw an exception with the status code and response.
                throw new HttpRequestException($"Error calling QuickBooks API. Status: {response.StatusCode}. Response: {responseString}");
            }
        }
    }
}

// These classes are structured to match the JSON response from the QuickBooks GraphQL API.
// This makes it easy to deserialize the response using Newtonsoft.Json.

public class GraphQLResponse
{
    [JsonProperty("data")] public ResponseData Data { get; set; }

    [JsonProperty("errors")] public GraphQLError[] Errors { get; set; }
}

public class ResponseData
{
    [JsonProperty("projectManagementCreateProject")]
    public CreateProjectPayload ProjectManagementCreateProject { get; set; }
}

public class CreateProjectPayload
{
    [JsonProperty("project")] public Project Project { get; set; }
}

public class Project
{
    [JsonProperty("id")] public string Id { get; set; }

    [JsonProperty("name")] public string Name { get; set; }

    [JsonProperty("status")] public string Status { get; set; }
}

public class GraphQLError
{
    [JsonProperty("message")] public string Message { get; set; }
}