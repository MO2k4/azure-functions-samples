using Azure.Provisioning;
using Azure.Provisioning.Primitives;

namespace AspireDemo.AppHost;

// Stamps billing tags on every taggable Azure resource Aspire generates. An
// InfrastructureResolver runs over each Azure.Provisioning construct at Bicep
// generation time, so one resolver covers resources that have no first-class
// customisation hook (the per-function identities, the container app modules,
// the registry) without editing generated Bicep. Not every construct exposes
// Tags (role assignments don't), hence the reflection probe.
internal sealed class AzureTagResolver : InfrastructureResolver
{
    // The four tags the MWMS management-group policy "[MW] Require Resource Tags"
    // denies deployments without. Values must come from the policy's allowed lists.
    private static readonly Dictionary<string, string> RequiredTags = new()
    {
        ["cost-center"] = "GAZE",
        ["owner"] = "AZE",
        ["environment"] = "learning",
        ["project"] = "aspire-demo",
    };

    public override void ResolveProperties(ProvisionableConstruct construct, ProvisioningBuildOptions options)
    {
        base.ResolveProperties(construct, options);

        if (construct is not ProvisionableResource resource ||
            resource.GetType().GetProperty("Tags")?.GetValue(resource) is not BicepDictionary<string> tags)
        {
            return;
        }

        // Tags bound to a bicep expression (e.g. the container app environment's
        // `tags` parameter) reject item assignment; those are handled in
        // ConfigureInfrastructure instead (see AppHost.cs).
        var bicepValue = (IBicepValue)tags;
        if (bicepValue.IsOutput || bicepValue.Expression is not null || bicepValue.Kind == BicepValueKind.Expression)
        {
            return;
        }

        foreach (var (key, value) in RequiredTags)
        {
            // ResolveProperties runs until the construct graph stabilises;
            // re-assigning an existing entry keeps it dirty and never converges.
            if (!tags.ContainsKey(key))
            {
                tags[key] = value;
            }
        }
    }
}
