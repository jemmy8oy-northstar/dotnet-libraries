using Northstar.SourceGenerators;

namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>
/// A DIRECT property at a mirrored type — the case James actually pointed at on
/// web-template#89 (<i>"why concrete addr here / Interfaces only in
/// interfaces"</i>), and the one no fixture covered before this. Every other
/// fixture reaches a mirrored type through a generic base's type argument.
/// </summary>
/// <remarks>
/// <c>partial</c> because the generated <c>IOrder</c> declares <c>ShipTo</c> as
/// <c>IAddr</c> while this class keeps it as <c>Addr</c>, so the generator writes
/// an explicit implementation here to join the two. That requirement is reported
/// as NSGEN003 if it is missing — it is not left to a confusing CS0535.
/// </remarks>
[GenerateInterface]
public partial class Order : IOrder
{
    public string Reference { get; set; } = "";

    /// <summary>Concrete on the class, so System.Text.Json can still populate it.</summary>
    public Addr ShipTo { get; set; } = new();

    /// <summary>A method RETURN at a mirrored type. Safe to widen — the bridge
    /// only ever upcasts on the way out, which is why returns substitute and
    /// parameters do not.</summary>
    public Addr Primary() => ShipTo;

    /// <summary>A method PARAMETER at a mirrored type, deliberately left
    /// concrete. Declaring it as <c>IAddr</c> would promise this class accepts
    /// any IAddr when it accepts only Addr.</summary>
    public bool ShipsTo(Addr candidate) => candidate.Line1 == ShipTo.Line1;
}
