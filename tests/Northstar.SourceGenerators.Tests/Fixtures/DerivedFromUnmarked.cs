using Northstar.SourceGenerators;

namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>
/// Its base is not mirrored, so IDerivedFromUnmarked must declare the
/// inherited members too — otherwise the interface is an incomplete view of
/// the class.
/// </summary>
[GenerateInterface]
public class DerivedFromUnmarked : UnmarkedBase, IDerivedFromUnmarked
{
    public string Own { get; set; } = "";

    /// <summary>
    /// An override of a NON-object virtual. It is a real member of this class's
    /// public surface and must be mirrored — the exclusion is for
    /// <c>ToString</c>/<c>GetHashCode</c>/<c>Equals(object)</c> only.
    /// </summary>
    public override string Describe() => Own;

    /// <summary>
    /// A public FIELD. Interfaces cannot declare fields, so it must not be
    /// mirrored — the <c>default</c> arm of the member-kind switch, which every
    /// other fixture leaves unreached.
    /// </summary>
    public string Tag = "";
}
