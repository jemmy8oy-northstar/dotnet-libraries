using Northstar.SourceGenerators;

namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>
/// Closes the invariant base. Its interface is expected to be
/// <c>ICrateBase&lt;Addr&gt;</c> — the CONCRETE argument — and the class stays
/// non-partial, because with no substitution there is nothing to bridge.
/// That pair of facts is what makes the fallback observable rather than a claim.
/// </summary>
[GenerateInterface]
public class Crate : CrateBase<Addr>, ICrate
{
}
