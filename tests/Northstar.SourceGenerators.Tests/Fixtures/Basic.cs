using Northstar.SourceGenerators;

namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>
/// Fixtures for InterfaceFromConcreteGenerator (#88).
/// </summary>
/// <remarks>
/// Every class in this folder declares <c>: I{Name}</c> — an interface that
/// exists nowhere in source. If the generator emits a mirror that does not
/// match the class, THIS PROJECT DOES NOT COMPILE. That makes compilation the
/// primary assertion; GenerateInterfaceTests.cs covers what a successful
/// compile cannot see (what was deliberately left out, and where inherited
/// members ended up).
///
/// This type exercises the ordinary surface: properties, accessors, methods,
/// and the members that must NOT be mirrored.
/// </remarks>
[GenerateInterface]
public class Basic : IBasic
{
    public string Name { get; set; } = "";

    public int Count { get; init; }

    public DateTime? OptionalAt { get; set; }

    public IReadOnlyList<string> Tags { get; } = new List<string>();

    public static string StaticThing => "excluded: static";

    internal string Internal { get; set; } = "excluded: non-public";

    private string Private { get; set; } = "excluded: non-public";

    public string Describe(string prefix, int repeat = 2, bool loud = false)
        => string.Concat(Enumerable.Repeat(prefix, repeat)) + (loud ? "!" : "");

    public bool TryParse(string input, out int value) => int.TryParse(input, out value);

    public TItem? FirstOrNull<TItem>(IEnumerable<TItem> items)
        where TItem : class
        => items.FirstOrDefault();

    /// <summary>A user-defined overload that shares a name with an object virtual,
    /// declared at the interface type like every other member here.</summary>
    /// <remarks>
    /// This must survive. Excluding object's virtuals by NAME would silently
    /// drop it from every model that defines one.
    /// </remarks>
    public bool Equals(IBasic? other) => other?.Name == Name;

    public override string ToString() => Name;

    public override bool Equals(object? obj) => Equals(obj as IBasic);

    public override int GetHashCode() => Name.GetHashCode();
}
