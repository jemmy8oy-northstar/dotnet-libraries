using System.Collections.Generic;
using Northstar.SourceGenerators;

namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>
/// A generic base that holds its type parameter inside an INVARIANT generic.
/// <c>NodeBase</c> is the covariant twin of this.
/// </summary>
/// <remarks>
/// Under the substituting design this was the case the generator had to REFUSE:
/// <c>List&lt;Addr&gt;</c> and <c>List&lt;IAddr&gt;</c> are unrelated types, so
/// an interface declaring the second could not be bridged from a class holding
/// the first, and NSGEN004 existed to say so out loud. Nothing is bridged now —
/// <see cref="Crate"/> closes this over <c>IAddr</c> and the mirror copies
/// it — so the refusal, the diagnostic, and the variance analysis behind them
/// are all gone. This fixture stays to prove the case now simply works.
/// </remarks>
[GenerateInterface]
public abstract class CrateBase<TItem> : ICrateBase<TItem>
    where TItem : IAddr
{
    public List<TItem> Items { get; set; } = new();
}
