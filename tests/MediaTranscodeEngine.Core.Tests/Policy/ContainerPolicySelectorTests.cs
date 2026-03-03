using FluentAssertions;
using MediaTranscodeEngine.Core.Policy;

namespace MediaTranscodeEngine.Core.Tests.Policy;

public class ContainerPolicySelectorTests
{
    [Fact]
    public void Select_WhenContainerRegistered_ReturnsPolicy()
    {
        var sut = CreateSut();

        var actual = sut.Select("mkv");

        actual.Should().BeOfType<MkvContainerPolicy>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Select_WhenContainerMissing_ThrowsArgumentException(string? container)
    {
        var sut = CreateSut();
        Action action = () => sut.Select(container!);

        action.Should()
            .Throw<ArgumentException>()
            .WithParameterName("container");
    }

    [Fact]
    public void Select_WhenContainerNotRegistered_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        Action action = () => sut.Select("webm");

        action.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*webm*");
    }

    [Theory]
    [InlineData(true, typeof(MkvContainerPolicy))]
    [InlineData(false, typeof(Mp4ContainerPolicy))]
    public void Select_WhenOutputMkvFlagChanges_ReturnsExpectedPolicyType(bool outputMkv, Type expectedType)
    {
        var sut = CreateSut();

        var actual = sut.Select(outputMkv);

        actual.Should().BeOfType(expectedType);
    }

    [Fact]
    public void Constructor_WhenPoliciesContainDuplicateContainer_ThrowsInvalidOperationException()
    {
        Action action = () => _ = new ContainerPolicySelector(
        [
            new MkvContainerPolicy(),
            new MkvContainerPolicy()
        ]);

        action.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*mkv*");
    }

    private static ContainerPolicySelector CreateSut()
    {
        return new ContainerPolicySelector(
        [
            new MkvContainerPolicy(),
            new Mp4ContainerPolicy()
        ]);
    }
}
