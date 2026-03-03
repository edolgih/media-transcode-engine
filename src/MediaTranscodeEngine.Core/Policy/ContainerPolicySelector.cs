using MediaTranscodeEngine.Core.Engine;

namespace MediaTranscodeEngine.Core.Policy;

public sealed class ContainerPolicySelector
{
    private readonly IReadOnlyDictionary<string, IContainerPolicy> _policiesByContainer;

    public ContainerPolicySelector(IEnumerable<IContainerPolicy> policies)
    {
        ArgumentNullException.ThrowIfNull(policies);

        _policiesByContainer = policies
            .GroupBy(policy => policy.Container, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    if (group.Count() > 1)
                    {
                        throw new InvalidOperationException($"Duplicate container policy registration for '{group.Key}'.");
                    }

                    return group.Single();
                },
                StringComparer.OrdinalIgnoreCase);
    }

    public IContainerPolicy Select(string container)
    {
        if (string.IsNullOrWhiteSpace(container))
        {
            throw new ArgumentException("Container is required.", nameof(container));
        }

        if (!_policiesByContainer.TryGetValue(container, out var policy))
        {
            throw new InvalidOperationException($"Container policy is not registered for '{container}'.");
        }

        return policy;
    }

    public IContainerPolicy Select(bool outputMkv)
    {
        return Select(outputMkv
            ? RequestContracts.Unified.MkvContainer
            : RequestContracts.Unified.Mp4Container);
    }
}
