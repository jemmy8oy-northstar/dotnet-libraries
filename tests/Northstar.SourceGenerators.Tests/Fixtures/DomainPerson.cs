using Northstar.SourceGenerators;

namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>
/// The domain model. Reached through <see cref="IDomainPerson"/>, its Address
/// is an <see cref="IDomainAddr"/> — the narrowing that a plain base class
/// cannot express, because a property type cannot be overridden.
/// </summary>
/// <remarks>
/// The SAME template as <see cref="Person"/>, closed one layer down. Both
/// classes are ordinary and non-partial; the only difference between the contract
/// and the domain is which interface goes in the angle brackets.
/// </remarks>
[GenerateInterface]
public class DomainPerson : PersonBase<IDomainAddr>, IDomainPerson
{
}
