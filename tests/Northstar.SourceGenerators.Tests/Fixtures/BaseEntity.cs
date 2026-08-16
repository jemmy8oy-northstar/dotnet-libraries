using Northstar.SourceGenerators;

namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>A mirrored base class — see <see cref="DerivedEntity"/>.</summary>
[GenerateInterface]
public class BaseEntity : IBaseEntity
{
    public string Id { get; set; } = "";
}
