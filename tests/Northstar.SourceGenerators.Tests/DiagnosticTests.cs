using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Northstar.SourceGenerators.Tests;

/// <summary>
/// Drives the generator over a source string so its DIAGNOSTICS can be asserted.
/// </summary>
/// <remarks>
/// The fixture-based tests are the better assertion for anything that compiles —
/// the build itself checks them. But they can only ever feed the generator input
/// that builds, so every diagnostic in the generator had no coverage at all. A
/// mutation making <c>IsPartial</c> always return true (NSGEN003 never fires)
/// survived all 33 of them. These tests exist to kill exactly that.
/// </remarks>
public class DiagnosticTests
{
    private static ImmutableArray<Diagnostic> Run(string source)
    {
        var compilation = CSharpCompilation.Create(
            "DiagnosticProbe",
            [CSharpSyntaxTree.ParseText(source)],
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        CSharpGeneratorDriver
            .Create(new InterfaceFromConcreteGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        return diagnostics;
    }

    private static Diagnostic? Find(string source, string id) =>
        Run(source).FirstOrDefault(d => d.Id == id);

    // The probe compiles the generator's own attribute in, because
    // RegisterPostInitializationOutput output is not visible to the syntax
    // provider in this harness.
    private const string Attribute = """
        namespace Northstar.SourceGenerators
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            internal sealed class GenerateInterfaceAttribute : System.Attribute { }
        }
        """;

    private static string Probe(string body) => Attribute + "\n" +
        "namespace Probe {\n using Northstar.SourceGenerators;\n" + body + "\n}";

    [Fact]
    public void A_class_needing_a_bridge_must_be_partial()
    {
        var diagnostic = Find(Probe("""
            [GenerateInterface] public class Addr { public string Line1 { get; set; } = ""; }
            [GenerateInterface] public class Order { public Addr ShipTo { get; set; } = new(); }
            """), "NSGEN003");

        Assert.NotNull(diagnostic);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);

        // The message has to name the member that caused it, or the fix is a
        // guess — "make it partial" without "because of ShipTo" sends the reader
        // looking at the wrong property on a class with twenty.
        Assert.Contains("Order", diagnostic.GetMessage());
        Assert.Contains("ShipTo", diagnostic.GetMessage());
    }

    [Fact]
    public void Declaring_it_partial_clears_the_diagnostic()
    {
        // The control. Without it the test above would pass on a generator that
        // reports NSGEN003 for every marked class.
        Assert.Null(Find(Probe("""
            [GenerateInterface] public class Addr { public string Line1 { get; set; } = ""; }
            [GenerateInterface] public partial class Order { public Addr ShipTo { get; set; } = new(); }
            """), "NSGEN003"));
    }

    [Fact]
    public void A_class_with_nothing_to_bridge_never_has_to_be_partial()
    {
        // The cost of the new shape is paid only where it buys something.
        Assert.Null(Find(Probe("""
            [GenerateInterface] public class Addr { public string Line1 { get; set; } = ""; }
            """), "NSGEN003"));
    }

    [Fact]
    public void An_unbridgeable_generic_base_is_reported_not_silently_downgraded()
    {
        var diagnostic = Find(Probe("""
            [GenerateInterface] public class Addr { public string Line1 { get; set; } = ""; }
            [GenerateInterface] public abstract class CrateBase<T>
            {
                public System.Collections.Generic.List<T> Items { get; set; } = new();
            }
            [GenerateInterface] public class Crate : CrateBase<Addr> { }
            """), "NSGEN004");

        Assert.NotNull(diagnostic);
        Assert.Contains("Crate", diagnostic.GetMessage());
    }

    [Fact]
    public void A_bridgeable_generic_base_is_not_reported()
    {
        // IReadOnlyList<out T> is covariant, so this one CAN close over the
        // mirror and must not be flagged.
        Assert.Null(Find(Probe("""
            [GenerateInterface] public class Addr { public string Line1 { get; set; } = ""; }
            [GenerateInterface] public abstract class CrateBase<T>
            {
                public System.Collections.Generic.IReadOnlyList<T> Items { get; set; } = [];
            }
            [GenerateInterface] public partial class Crate : CrateBase<Addr> { }
            """), "NSGEN004"));
    }

    [Fact]
    public void A_nested_class_is_reported()
    {
        Assert.NotNull(Find(Probe("""
            public class Outer { [GenerateInterface] public class Inner { } }
            """), "NSGEN001"));
    }

    [Fact]
    public void A_static_class_is_reported()
    {
        Assert.NotNull(Find(Probe("""
            [GenerateInterface] public static class Helpers { }
            """), "NSGEN002"));
    }

    [Fact]
    public void Every_diagnostic_the_generator_can_raise_is_declared_in_the_release_file()
    {
        // AnalyzerReleases.Unshipped.md is what RS2000 checks against, and it is
        // hand-maintained — so a new diagnostic can ship undeclared if nobody
        // builds the analyzer project itself. This asserts the pair.
        var ids = typeof(InterfaceFromConcreteGenerator)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(DiagnosticDescriptor))
            .Select(f => ((DiagnosticDescriptor)f.GetValue(null)!).Id)
            .ToArray();

        Assert.NotEmpty(ids);
        Assert.Equal(ids.Distinct().Count(), ids.Length);

        var release = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "AnalyzerReleases.Unshipped.md"));

        // Both directions. RS2000 fails the analyzer build for a rule missing
        // from the file; nothing checked the reverse, so a deleted diagnostic
        // would leave a line documenting a rule that can never fire.
        Assert.All(ids, id => Assert.Contains(id, release));

        var declared = release
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("NSGEN", StringComparison.Ordinal))
            .Select(l => l.Split('|')[0].Trim())
            .ToArray();

        Assert.Equal(ids.OrderBy(i => i), declared.OrderBy(i => i));
    }
}
