using FluentAssertions;
using PI.Shared.Models;
using Xunit;

namespace UnitTests.Expressions;

public class PersonNameTests
{
    [Theory]
    [InlineData("Chris @ Unitarian Church of Barnstable", "Chris", "Barnstable")]
    public void Basic(string fullName, string firstName, string lastName)
    {
        PersonName.TryParse(fullName, out var parsed).Should().BeTrue();
        parsed.FirstName.Should().Be(firstName);
        parsed.LastName.Should().Be(lastName);
    }
}