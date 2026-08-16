using Northstar.SourceGenerators;

namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>
/// The generic layering James described on #83: a type with a custom-typed
/// member becomes generic in it, abstract, and constrained by the interface
/// that member's layers share.
/// </summary>
/// <remarks>
/// The contract (<see cref="Person"/>) and the domain model
/// (<see cref="DomainPerson"/>) both close this base over their own types.
/// </remarks>
[GenerateInterface]
public abstract class PersonBase<TAddress> : IPersonBase<TAddress>
    where TAddress : IAddr
{
    public string Name { get; set; } = "";

    public TAddress Address { get; set; } = default!;
}
