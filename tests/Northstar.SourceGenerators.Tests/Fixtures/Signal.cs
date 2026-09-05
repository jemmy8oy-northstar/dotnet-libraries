using System;
using Northstar.SourceGenerators;

namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>
/// The member kinds no other fixture has, each of which was a rule the suite
/// asserted and a mutation walked straight through.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>An event.</b> The README says events are mirrored; nothing checked
/// it, and a mutation making <c>ShouldMirror</c> refuse every event survived.</item>
/// <item><b>An explicit interface implementation.</b> These must NOT be
/// mirrored — the member is already tied to another interface. This used to be
/// covered by accident, because the generated bridge produced one on several
/// fixtures; deleting the bridge left the rule with nothing exercising it.</item>
/// <item><b>A property whose GETTER is not public.</b> "Mirrored if at least one
/// accessor is public" is two clauses, and every fixture satisfied the first,
/// so the second was never the reason for an answer.</item>
/// <item><b>Abstract and non-generic.</b> <see cref="AbstractSignal"/> below —
/// the combination that tells the converter rule's two conditions apart.</item>
/// </list>
/// </remarks>
[GenerateInterface]
public class Signal : ISignal, IProbe
{
    public event EventHandler? Fired;

    /// <summary>Public setter, non-public getter: mirrored on the SECOND clause.</summary>
    public string Token { private get; set; } = "";

    /// <summary>Explicitly implemented, so it is IProbe's member and not this
    /// class's public surface. It must not appear on ISignal.</summary>
    void IProbe.Probe() => Fired?.Invoke(this, EventArgs.Empty);

    public void Raise() => Fired?.Invoke(this, EventArgs.Empty);

    public string Read() => Token;
}

/// <summary>Hand-written on purpose: <see cref="Signal"/> needs an interface to
/// implement EXPLICITLY, and a generated mirror is never implemented that way.</summary>
public interface IProbe
{
    void Probe();
}

/// <summary>
/// Abstract and non-generic — the shape that separates the converter rule's two
/// conditions.
/// </summary>
/// <remarks>
/// The rule is "not abstract AND not generic". Every abstract fixture was also
/// generic and every generic one also abstract, so <c>&amp;&amp;</c> and
/// <c>||</c> gave the same answer everywhere and a mutation between them
/// survived. There is nothing to construct here, so no converter may be emitted.
/// </remarks>
[GenerateInterface]
public abstract class AbstractSignal : IAbstractSignal
{
    public string Label { get; set; } = "";
}
