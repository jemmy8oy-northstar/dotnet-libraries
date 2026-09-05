using Northstar.SourceGenerators;

namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>
/// Closes the invariant base over the INTERFACE, so <c>ICrate</c> is
/// <c>ICrateBase&lt;IAddr&gt;</c> and <c>Items</c> is a <c>List&lt;IAddr&gt;</c>
/// — the type the previous design could not produce at all.
/// </summary>
[GenerateInterface]
public class Crate : CrateBase<IAddr>, ICrate
{
}
