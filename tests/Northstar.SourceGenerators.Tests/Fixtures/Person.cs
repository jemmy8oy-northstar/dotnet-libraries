using Northstar.SourceGenerators;

namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>
/// The concrete contract used in a route. It adds no members of its own — the
/// whole of <c>IPerson</c> is inherited from the closed generic base, which is
/// why marking it is enough and nothing here is hand-written.
/// </summary>
/// <remarks>
/// Closes the template over <see cref="IAddr"/>, the INTERFACE — James,
/// dotnet-libraries#3: <i>"the contract interface returned from the route
/// implements a contract interface with contract interfaces specified in the
/// generic"</i>. That is what makes <c>Address</c> literally an <c>IAddr</c>
/// here, so this class satisfies <c>IPerson</c> with nothing generated into it
/// and no <c>partial</c>.
/// </remarks>
[GenerateInterface]
public class Person : PersonBase<IAddr>, IPerson
{
}
