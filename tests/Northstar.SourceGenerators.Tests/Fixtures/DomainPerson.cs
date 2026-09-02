using Northstar.SourceGenerators;

namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>
/// The domain model. Reached through <see cref="IDomainPerson"/>, its Address
/// is a <see cref="DomainAddr"/> — the narrowing that a plain base class
/// cannot express, because a property type cannot be overridden.
/// </summary>
[GenerateInterface]
public partial class DomainPerson : PersonBase<DomainAddr>, IDomainPerson
{
}
