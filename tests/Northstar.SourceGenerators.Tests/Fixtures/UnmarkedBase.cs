namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>
/// Deliberately NOT marked with [GenerateInterface]: there is no interface for
/// a derived mirror to inherit, so its members have to be folded in instead.
/// </summary>
public class UnmarkedBase
{
    public string Inherited { get; set; } = "";
}
