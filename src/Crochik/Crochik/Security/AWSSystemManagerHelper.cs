using System.Threading.Tasks;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.Extensions.Configuration;

namespace Crochik.Security
{
    public class AWSSystemManagerHelper
    {
        private static IAmazonSimpleSystemsManagement GetClientWithoutDI(IConfiguration configuration)
        {
            var awsOptions = configuration.GetAWSOptions();

            return awsOptions.CreateServiceClient<AmazonSimpleSystemsManagementClient>();
        }

        public static async Task<string> GetParameterAsync(IConfiguration configuration, string name)
        {
            var client = GetClientWithoutDI(configuration);
            var parameter = await client.GetParameterAsync(new GetParameterRequest()
            {
                Name = name,
                WithDecryption = true
            });

            return parameter.Parameter.Value;
        }

        public static string GetParameter(IConfiguration configuration, string name)
            => GetParameterAsync(configuration, name).GetAwaiter().GetResult();
    }
}