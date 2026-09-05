using Northstar.SourceGenerators;

namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>
/// A DIRECT member at a mirrored type — the case James actually pointed at on
/// web-template#89 (<i>"why concrete addr here / Interfaces only in
/// interfaces"</i>). Every other fixture reaches a mirrored type through a
/// generic base's type argument.
/// </summary>
/// <remarks>
/// Not <c>partial</c>, and nothing is generated into it: the members are already
/// declared at the interface types, so the mirror can copy them verbatim.
/// <para>
/// <c>ShipsTo</c> is the member the previous design could not express. A
/// generated bridge could widen a return but never a PARAMETER — declaring
/// <c>ShipsTo(IAddr)</c> for a class that accepted only <c>Addr</c> promised
/// something its narrowing cast had to break at runtime. Written by hand at the
/// interface type it is simply true, so James's rule reaches the whole signature
/// rather than the return half of it.
/// </para>
/// </remarks>
[GenerateInterface]
public class Order : IOrder
{
    public string Reference { get; set; } = "";

    /// <summary>Declared at the interface, which is what removes the bridge.</summary>
    public IAddr ShipTo { get; set; } = new Addr();

    /// <summary>A method RETURN at a mirrored type.</summary>
    public IAddr Primary() => ShipTo;

    /// <summary>A method PARAMETER at a mirrored type.</summary>
    public bool ShipsTo(IAddr candidate) => candidate.Line1 == ShipTo.Line1;
}
