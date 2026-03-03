using FluentAssertions;
using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Tests.Engine;

public class DomainRequestInvariantsTests
{
    [Theory]
    [InlineData(typeof(TranscodeRequest))]
    [InlineData(typeof(H264TranscodeRequest))]
    public void DomainRequests_ShouldExposeReadOnlyPublicProperties(Type requestType)
    {
        var writableProperties = requestType
            .GetProperties()
            .Where(static property => property.SetMethod is not null && property.SetMethod.IsPublic)
            .Select(static property => property.Name)
            .ToArray();

        writableProperties.Should().BeEmpty();
    }
}
