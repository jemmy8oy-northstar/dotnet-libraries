using System.Collections.Generic;
using Northstar.SourceGenerators;

namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>
/// A generic base that holds its type parameter inside an INVARIANT generic.
/// <c>NodeBase</c> is the covariant twin of this: it exposes
/// <c>IReadOnlyList&lt;TNode&gt;</c>, so its interface can close over the mirror.
/// </summary>
/// <remarks>
/// This one cannot. <c>List&lt;Addr&gt;</c> and <c>List&lt;IAddr&gt;</c> are
/// unrelated types with no conversion in either direction, so an interface
/// declaring <c>List&lt;IAddr&gt;</c> could not be bridged — the explicit
/// implementation would not compile. The generator therefore keeps the concrete
/// type argument here and says so with NSGEN004, rather than emitting generated
/// code that does not build.
/// </remarks>
[GenerateInterface]
public abstract class CrateBase<TItem> : ICrateBase<TItem>
    where TItem : IAddr
{
    public List<TItem> Items { get; set; } = new();
}
