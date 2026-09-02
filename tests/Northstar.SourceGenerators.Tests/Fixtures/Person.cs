using Northstar.SourceGenerators;

namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>
/// The concrete contract used in a route. It adds no members of its own — the
/// whole of <c>IPerson</c> is inherited from the closed generic base, which is
/// why marking it is enough and nothing here is hand-written.
/// </summary>
[GenerateInterface]
public partial class Person : PersonBase<Addr>, IPerson
{
}
