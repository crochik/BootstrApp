using PI.Shared.Form.Models;
using PI.Shared.Models;
using Xunit;

namespace UnitTests.Expressions;

public class ProjectAsSubDocument
{
    [Fact]
    public void Test()
    {
        var fieldNames = new[]
        {
            "User|First",
            "User|Last",
            "User2",
            "One|Two|Tree",
            "Child|ReferenceFieldId"
        };

        var referenceFields = new[]
        {
            new ReferenceField
            {
                Name = "Child|ReferenceFieldId",
                ReferenceFieldOptions = new ReferenceFieldOptions
                {

                },
            }
        };

        var result = AppDataViewPipelineBuilder.GetProjectionAsSubDocuments(fieldNames, referenceFields);
        var str = result.ToString();

    }
}