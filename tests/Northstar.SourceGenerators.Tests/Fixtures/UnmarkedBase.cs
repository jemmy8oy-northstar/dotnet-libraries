namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>
/// Deliberately NOT marked with [GenerateInterface]: there is no interface for
/// a derived mirror to inherit, so its members have to be folded in instead.
/// </summary>
public class UnmarkedBase
{
    public string Inherited { get; set; } = "";

    /// <summary>
    /// Virtual so <see cref="DerivedFromUnmarked"/> can override it — the only
    /// non-object override in the suite, and what makes
    /// <c>OverridesObjectMember</c>'s default arm reachable at all.
    /// </summary>
    public virtual string Describe() => Inherited;
}
