using System.Collections.Generic;
using Northstar.SourceGenerators;

namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>
/// Mirrored types held inside generics DIRECTLY — one covariant, one invariant,
/// side by side on one class.
/// </summary>
/// <remarks>
/// This fixture is the clearest evidence that James's shape is not a weaker
/// version of the rule. Under the substituting design <c>Crates</c> could only
/// ever be <c>List&lt;Addr&gt;</c> in the interface: rewriting an INVARIANT
/// generic to <c>List&lt;IAddr&gt;</c> produced a bridge that would not compile,
/// so the generator refused and said so with NSGEN004. Written at the interface
/// type by hand, <c>List&lt;IAddr&gt;</c> is unremarkable — the variance question
/// only ever existed because something was being rewritten.
/// <para>
/// Both members are therefore interfaces here, and NSGEN004 no longer exists.
/// </para>
/// </remarks>
[GenerateInterface]
public class Manifest : IManifest
{
    /// <summary>Covariant: IReadOnlyList&lt;out T&gt;.</summary>
    public IReadOnlyList<IAddr> Stops { get; set; } = [];

    /// <summary>Invariant: List&lt;T&gt;. Unreachable under the old design.</summary>
    public List<IAddr> Crates { get; set; } = [];
}
