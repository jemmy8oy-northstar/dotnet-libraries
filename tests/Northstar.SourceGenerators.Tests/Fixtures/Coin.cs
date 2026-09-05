using System;
using Northstar.SourceGenerators;

namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>
/// The one place "declare it at the interface" is not the author's to obey: a
/// BCL interface fixes the signature at the concrete type.
/// </summary>
/// <remarks>
/// <c>IEquatable&lt;Coin&gt;</c> requires exactly <c>Equals(Coin?)</c>. Widening
/// it to <c>Equals(ICoin?)</c> does not satisfy the interface — it stops
/// implementing it. Reporting NSGEN005 here would make the rule unusable on any
/// type with value semantics, which is not a judgement about this type; it is
/// the BCL's signature, and the author cannot change it.
/// <para>
/// The exemption is deliberately limited to interfaces from REFERENCED
/// ASSEMBLIES. <c>ICoin</c> — this generator's own output, and every mirror like
/// it — is not metadata, so declaring <c>: ICoin</c> does not exempt a class
/// from the rule it exists to enforce. <see cref="Order"/> is that control:
/// same shape, source interface, and it obeys the rule.
/// </para>
/// </remarks>
[GenerateInterface]
public class Coin : ICoin, IEquatable<Coin>
{
    public int Pence { get; set; }

    public bool Equals(Coin? other) => other?.Pence == Pence;

    public override bool Equals(object? obj) => Equals(obj as Coin);

    public override int GetHashCode() => Pence;
}
