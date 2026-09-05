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
/// that builds, so every diagnostic in the generator had no coverage at all — a
/// mutation disabling one survived every fixture test. These tests exist to kill
/// exactly that, and NSGEN005 needs them more than its predecessors did: a
/// diagnostic that never fires looks identical to a codebase that obeys the rule.
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


    // ------------------------------------------------------------- NSGEN005
    // The rule is enforced, not performed: the generator mirrors what the class
    // declares, so a concrete in an interface is the author's to fix and the
    // diagnostic's to name.

    [Fact]
    public void A_member_declared_at_a_mirrored_concrete_is_reported()
    {
        var diagnostic = Find(Probe("""
            [GenerateInterface] public class Addr { public string Line1 { get; set; } = ""; }
            [GenerateInterface] public class Order { public Addr ShipTo { get; set; } = new(); }
            """), "NSGEN005");

        Assert.NotNull(diagnostic);

        // The message has to name the member AND the interface to write, or the
        // fix is a guess — "this is wrong" on a class with twenty properties
        // sends the reader looking at the wrong one.
        Assert.Contains("Order", diagnostic.GetMessage());
        Assert.Contains("ShipTo", diagnostic.GetMessage());
        Assert.Contains("IAddr", diagnostic.GetMessage());
    }

    [Fact]
    public void Declaring_the_member_at_the_interface_clears_it()
    {
        // The control. Without it the test above would pass on a generator that
        // reports NSGEN005 for every marked class.
        Assert.Null(Find(Probe("""
            [GenerateInterface] public class Addr { public string Line1 { get; set; } = ""; }
            [GenerateInterface] public class Order { public IAddr ShipTo { get; set; } = new Addr(); }
            """), "NSGEN005"));
    }

    [Fact]
    public void A_class_with_no_mirrored_types_in_its_surface_is_never_reported()
    {
        Assert.Null(Find(Probe("""
            [GenerateInterface] public class Addr { public string Line1 { get; set; } = ""; }
            """), "NSGEN005"));
    }

    [Fact]
    public void A_method_parameter_is_reported_as_well_as_a_return()
    {
        // The half the substituting design could not reach: a bridge could widen a
        // return but never a parameter, so "interfaces only in interfaces" was
        // enforced on the return half of every signature and silent on the other.
        var source = Probe("""
            [GenerateInterface] public class Addr { public string Line1 { get; set; } = ""; }
            [GenerateInterface] public class Order
            {
                public bool ShipsTo(Addr candidate) => true;
            }
            """);

        var diagnostic = Find(source, "NSGEN005");

        Assert.NotNull(diagnostic);
        Assert.Contains("ShipsTo", diagnostic.GetMessage());
    }

    [Fact]
    public void A_concrete_inside_an_invariant_generic_is_reported_with_the_interface_form()
    {
        // The case the old design had to REFUSE (NSGEN004): rewriting List<Addr>
        // to List<IAddr> produced a bridge that would not compile, so the
        // generator kept the concrete and said so. Nothing is rewritten now, so
        // the answer is not a refusal but an instruction — and the suggestion is
        // computed, not hand-waved, which is the whole value of enforcing a rule
        // rather than performing it.
        var diagnostic = Find(Probe("""
            [GenerateInterface] public class Addr { public string Line1 { get; set; } = ""; }
            [GenerateInterface] public class Manifest
            {
                public System.Collections.Generic.List<Addr> Crates { get; set; } = new();
            }
            """), "NSGEN005");

        Assert.NotNull(diagnostic);
        Assert.Contains("List<Probe.IAddr>", diagnostic.GetMessage());

        // …and `global::` is stripped, because this is prose in an error list,
        // not generated code. Everything else stays fully qualified: the value of
        // the message is that the suggestion can be copied out of it verbatim.
        Assert.DoesNotContain("global::", diagnostic.GetMessage());
    }

    [Fact]
    public void A_generic_base_closed_over_a_concrete_is_reported()
    {
        // Nothing in the class's own member list mentions Addr, so a check that
        // only walked members would let `ICrate : ICrateBase<Addr>` through with
        // every member looking clean.
        var diagnostic = Find(Probe("""
            [GenerateInterface] public class Addr { public string Line1 { get; set; } = ""; }
            [GenerateInterface] public abstract class CrateBase<T>
            {
                public System.Collections.Generic.List<T> Items { get; set; } = new();
            }
            [GenerateInterface] public class Crate : CrateBase<Addr> { }
            """), "NSGEN005");

        Assert.NotNull(diagnostic);
        Assert.Contains("Crate", diagnostic.GetMessage());
        Assert.Contains("IAddr", diagnostic.GetMessage());
    }

    [Fact]
    public void A_generic_base_closed_over_an_interface_is_not_reported()
    {
        // And this is the case the old design could not produce AT ALL: an
        // invariant List<IAddr> in a generated interface.
        Assert.Null(Find(Probe("""
            [GenerateInterface] public class Addr { public string Line1 { get; set; } = ""; }
            [GenerateInterface] public abstract class CrateBase<T>
            {
                public System.Collections.Generic.List<T> Items { get; set; } = new();
            }
            [GenerateInterface] public class Crate : CrateBase<IAddr> { }
            """), "NSGEN005"));
    }

    [Fact]
    public void A_signature_fixed_by_a_referenced_interface_is_exempt()
    {
        // IEquatable<Coin> requires exactly Equals(Coin?). Widening it does not
        // satisfy the interface, it stops implementing it — so reporting here
        // would make the rule unusable on any type with value semantics.
        Assert.Null(Find(Probe("""
            [GenerateInterface] public class Coin : System.IEquatable<Coin>
            {
                public int Pence { get; set; }
                public bool Equals(Coin? other) => other?.Pence == Pence;
            }
            """), "NSGEN005"));
    }

    [Fact]
    public void The_exemption_does_not_extend_to_the_generators_own_mirrors()
    {
        // The boundary that makes the exemption safe, and the one that would
        // silently disable the whole diagnostic if it moved. Every marked class
        // declares `: I{Name}` — an interface in the consumer's own compilation —
        // so an exemption for "implements a declared interface" would exempt every
        // member of every marked class and NSGEN005 would never fire again.
        var diagnostic = Find(Probe("""
            [GenerateInterface] public class Addr : IAddr { public string Line1 { get; set; } = ""; }
            [GenerateInterface] public class Order : IOrder { public Addr ShipTo { get; set; } = new(); }
            """), "NSGEN005");

        Assert.NotNull(diagnostic);
        Assert.Contains("ShipTo", diagnostic.GetMessage());
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
