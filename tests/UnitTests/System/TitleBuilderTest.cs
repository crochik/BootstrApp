using FluentAssertions;
using Xunit;
using PI.Shared;

namespace UnitTests
{
    public class TitleBuilderTest
    {
        [Fact]
        public void MinLength()
        {
            new TitleBuilder("test")
                .WithoutFileExtension()
                .WithMaxLengthOf(5)
                .Build()
                .Length
                .Should().Be(4);
        }

        [Fact]
        public void MinLengthWithExtension()
        {
            new TitleBuilder("test.iso")
                .WithoutFileExtension()
                .WithMaxLengthOf(5)
                .Build()
                .Length
                .Should().Be(4);
        } 

        [Fact]
        public void LongNameWithExtension()
        {
            var result = new TitleBuilder("longnamewithextension.txt")
                .WithoutFileExtension()
                .WithMaxLengthOf(5)
                .Build();

            result.Should().Be("lo...");
        } 

        [Fact]
        public void LongNameWithExtensionInTheMiddle()
        {
            var result = new TitleBuilder("longnamewithextension.txt")
                .WithoutFileExtension()
                .WithMaxLengthOf(5, true)
                .Build();

            result.Should().Be("l...n");
        } 

        [Fact]
        public void LongNameInTheMiddle()
        {
            var result = new TitleBuilder("longnamewithextension.txt")
                .WithMaxLengthOf(9, true)
                .Build();

            result.Should().Be("lon...txt");
        }
    }
}