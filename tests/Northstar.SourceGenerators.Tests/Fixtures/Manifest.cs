using System.Collections.Generic;
using Northstar.SourceGenerators;

namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>
/// A class holding mirrored types inside generics DIRECTLY, rather than through
/// a generic base. One covariant, one invariant, so the two halves of the
/// substitution rule are visible side by side on one class.
/// </summary>
/// <remarks>
/// This fixture exists because a mutation survived without it: deleting the
/// variance guard in <c>RenderForInterface</c> changed no test, since the only
/// invariant case in the suite was already stopped one level up by
/// <c>CanCloseOverMirrors</c>. Without the guard, <c>Crates</c> would be
/// declared <c>List&lt;IAddr&gt;</c> and the generated bridge would not compile
/// — so this fixture's mere existence is the assertion, and the test below
/// pins which way round each one went.
/// </remarks>
[GenerateInterface]
public partial class Manifest : IManifest
{
    /// <summary>Covariant: IReadOnlyList&lt;out T&gt; widens safely, so the
    /// interface declares IReadOnlyList&lt;IAddr&gt;.</summary>
    public IReadOnlyList<Addr> Stops { get; set; } = [];

    /// <summary>Invariant: List&lt;Addr&gt; and List&lt;IAddr&gt; are unrelated,
    /// so the interface keeps List&lt;Addr&gt;.</summary>
    public List<Addr> Crates { get; set; } = [];
}
