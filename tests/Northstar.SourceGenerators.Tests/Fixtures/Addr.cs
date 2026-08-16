using Northstar.SourceGenerators;

namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>
/// A leaf: nothing about it varies between layers, so no generic base. Its
/// generated <c>IAddr</c> is what <see cref="PersonBase{TAddress}"/> constrains
/// on — the constraint the layering hangs on is itself generated.
/// </summary>
[GenerateInterface]
public class Addr : IAddr
{
    public string Line1 { get; set; } = "";
}
