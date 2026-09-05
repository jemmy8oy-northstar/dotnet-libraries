using System.CodeDom.Compiler;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Northstar.SourceGenerators.Tests.Fixtures;

namespace Northstar.SourceGenerators.Tests;

/// <summary>
/// Covers InterfaceFromConcreteGenerator (#88).
/// </summary>
/// <remarks>
/// The fixtures in GenerateInterfaceFixtures.cs already prove the happy path by
/// compiling — each class declares an interface that only the generator
/// supplies. These tests cover what a successful compile cannot see: members
/// deliberately excluded, where inherited members ended up, and the generic
/// layering behaving at runtime.
/// </remarks>
public class GenerateInterfaceTests
{
    private static string[] MemberNames<T>() =>
        typeof(T).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToArray();

    // ------------------------------------------------------------ it exists
    [Fact]
    public void Generates_an_interface_the_class_implements()
    {
        Assert.True(typeof(IBasic).IsInterface);
        Assert.True(typeof(IBasic).IsAssignableFrom(typeof(Basic)));
    }

    [Fact]
    public void Generated_interface_lives_beside_its_class()
    {
        // Placement is decision (1) on #88. Asserted so that changing it is a
        // deliberate edit to this test, not a silent drift.
        Assert.Equal(typeof(Basic).Namespace, typeof(IBasic).Namespace);
        Assert.Equal(typeof(Basic).Assembly, typeof(IBasic).Assembly);
    }

    // --------------------------------------------- nothing is hand-written
    [Fact]
    public void Every_interface_in_the_fixtures_is_generated_not_hand_written()
    {
        // James's review on #89: "there are a lot of handwritten interfaces".
        // The closing interfaces (IPerson, IDomainPerson, INode) and the
        // constraint (IAddr) were hand-written and did not need to be — the
        // generator emits the mirror for every closing interface too.
        // This asserts the fixtures never regain a hand-written interface: the
        // generator stamps [GeneratedCode], so "is this generated?" is a
        // runtime question rather than a question about the file list.
        var interfaces = typeof(Basic).Assembly
            .GetTypes()
            .Where(t => t.IsInterface && t.Namespace == typeof(Basic).Namespace)
            .ToArray();

        // Guard against the assertion passing vacuously if the namespace moves.
        Assert.NotEmpty(interfaces);

        var handWritten = interfaces
            .Where(t => t.GetCustomAttribute<GeneratedCodeAttribute>() is null)
            .Select(t => t.Name)
            .OrderBy(n => n)
            .ToArray();

        // IProbe is the one deliberate exception, listed rather than filtered out
        // by a rule: Signal needs an interface to implement EXPLICITLY, and a
        // generated mirror is never implemented that way. Naming it here means
        // adding a second hand-written interface fails this test and has to be
        // argued for, which is the point of the assertion.
        Assert.Equal(["IProbe"], handWritten);
    }

    // ----------------------------------------------------------- what's in
    [Fact]
    public void Mirrors_public_properties_with_their_accessors()
    {
        var name = typeof(IBasic).GetProperty(nameof(Basic.Name))!;
        Assert.True(name.CanRead);
        Assert.True(name.CanWrite);

        // A get-only property must not gain a setter.
        var tags = typeof(IBasic).GetProperty(nameof(Basic.Tags))!;
        Assert.True(tags.CanRead);
        Assert.False(tags.CanWrite);
    }

    [Fact]
    public void Preserves_init_only_accessors()
    {
        var setter = typeof(IBasic).GetProperty(nameof(Basic.Count))!.SetMethod!;

        // `init` shows up as a required modifier on the setter. If the
        // generator emitted `set`, callers could mutate an init-only contract.
        Assert.Contains(
            setter.ReturnParameter.GetRequiredCustomModifiers(),
            m => m.Name == "IsExternalInit");
    }

    [Fact]
    public void Preserves_nullable_reference_annotations()
    {
        var prop = typeof(IBasic).GetProperty(nameof(Basic.OptionalAt))!;
        Assert.Equal(typeof(DateTime?), prop.PropertyType);
    }

    [Fact]
    public void Mirrors_methods_with_optional_and_out_parameters()
    {
        var describe = typeof(IBasic).GetMethod(nameof(Basic.Describe))!;
        var parameters = describe.GetParameters();
        Assert.Equal(3, parameters.Length);
        Assert.True(parameters[1].HasDefaultValue);
        Assert.Equal(2, parameters[1].DefaultValue);
        Assert.Equal(false, parameters[2].DefaultValue);

        var tryParse = typeof(IBasic).GetMethod(nameof(Basic.TryParse))!;
        Assert.True(tryParse.GetParameters()[1].IsOut);
    }

    [Fact]
    public void Mirrors_generic_methods_with_their_constraints()
    {
        var method = typeof(IBasic).GetMethod(nameof(Basic.FirstOrNull))!;
        Assert.True(method.IsGenericMethodDefinition);

        var constraints = method.GetGenericArguments()[0].GenericParameterAttributes;
        Assert.True(constraints.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint));
    }

    // ---------------------------------------------------------- what's out
    [Theory]
    [InlineData("StaticThing")]   // static members have no instance surface
    [InlineData("Internal")]      // non-public
    [InlineData("Private")]       // non-public
    [InlineData("ToString")]      // object virtuals are on every type already
    [InlineData("GetHashCode")]
    public void Excludes_members_that_do_not_belong_on_an_interface(string name)
    {
        Assert.DoesNotContain(name, MemberNames<IBasic>());
    }

    [Fact]
    public void Excludes_constructors()
    {
        Assert.Empty(typeof(IBasic).GetConstructors());
    }

    [Fact]
    public void Keeps_a_user_defined_overload_that_shares_a_name_with_an_object_virtual()
    {
        // Equals(object?) is excluded; Equals(IBasic?) is a real member.
        // Filtering by NAME rather than by what the method overrides would
        // silently drop this from every model that defines one.
        var equals = typeof(IBasic).GetMethod("Equals", new[] { typeof(IBasic) });
        Assert.NotNull(equals);
        Assert.Null(typeof(IBasic).GetMethod("Equals", new[] { typeof(object) }));
    }

    // --------------------------------------------------------- inheritance
    [Fact]
    public void Mirrors_the_base_class_chain_into_interface_inheritance()
    {
        Assert.True(typeof(IBaseEntity).IsAssignableFrom(typeof(IDerivedEntity)));
    }

    [Fact]
    public void Does_not_restate_members_supplied_by_an_inherited_mirror()
    {
        // Id comes from IBaseEntity. Declaring it again on IDerivedEntity would
        // shadow rather than share, and produce a CS0108 warning in consumers.
        Assert.DoesNotContain(nameof(BaseEntity.Id), MemberNames<IDerivedEntity>());
        Assert.Contains(nameof(DerivedEntity.Extra), MemberNames<IDerivedEntity>());

        // ...but it is still reachable through the interface.
        IDerivedEntity entity = new DerivedEntity { Id = "x", Extra = "y" };
        Assert.Equal("x", entity.Id);
    }

    [Fact]
    public void Folds_in_members_of_a_base_that_is_not_itself_mirrored()
    {
        // There is no interface to inherit here, so an interface omitting
        // Inherited would be an incomplete view of the class.
        Assert.Contains("Inherited", MemberNames<IDerivedFromUnmarked>());
        Assert.Contains("Own", MemberNames<IDerivedFromUnmarked>());
    }

    // ------------------------------------------------- the generic layering
    [Fact]
    public void Generic_base_is_mirrored_with_its_type_parameters_and_constraints()
    {
        var definition = typeof(IPersonBase<>);
        Assert.True(definition.IsInterface);

        var typeParameter = definition.GetGenericArguments()[0];

        // The constraint is itself a GENERATED interface (IAddr, mirrored from
        // Addr) — there is no hand-written contract holding the layering up.
        Assert.Contains(typeof(IAddr), typeParameter.GetGenericParameterConstraints());
    }

    [Fact]
    public void Contract_and_domain_model_share_one_generic_base()
    {
        Assert.True(typeof(IPersonBase<IAddr>).IsAssignableFrom(typeof(Person)));
        Assert.True(typeof(IPersonBase<IDomainAddr>).IsAssignableFrom(typeof(DomainPerson)));
    }

    [Fact]
    public void One_generic_consumer_serves_both_layers()
    {
        // The whole point of the generics: this method is written once.
        static string NameOf<TPerson, TAddress>(TPerson person)
            where TPerson : IPersonBase<TAddress>
            where TAddress : IAddr
            => person.Name;

        Assert.Equal("contract", NameOf<Person, IAddr>(new Person { Name = "contract" }));
        Assert.Equal("domain", NameOf<DomainPerson, IDomainAddr>(new DomainPerson { Name = "domain" }));
    }

    [Fact]
    public void Domain_model_narrows_the_nested_type()
    {
        IDomainPerson person = new DomainPerson
        {
            Name = "James",
            Address = new DomainAddr { Line1 = "1 Real St" },
        };

        // Reached through the interface, the nested type is the DOMAIN one —
        // Normalised() does not exist on the contract's Addr.
        Assert.Equal("1 REAL ST", person.Address.Normalised());
    }

    [Fact]
    public void Abstract_generic_base_stays_abstract()
    {
        // James asked me to confirm this on #83.
        Assert.True(typeof(PersonBase<IAddr>).IsAbstract);
    }

    // ----------------------------------------------------- self-reference
    [Fact]
    public void Self_referential_model_closes_with_a_single_type_parameter()
    {
        // The case that looks like it needs an unbounded parameter list.
        Assert.Single(typeof(INodeBase<>).GetGenericArguments());

        INode root = new Node
        {
            Label = "root",
            Children = new List<INode> { new Node { Label = "child" } },
        };

        Assert.Equal("child", root.Children[0].Label);
    }

    // ------------------------------------------ interfaces only in interfaces
    // James, dotnet-libraries#3 (2026-09-04), rejecting the generated bridge:
    // "I imagine interfaces with interface properties and service interfaces
    //  with methods with interface args and interface results … the generic is
    //  abstract and used as a template … the domain interface can implement with
    //  domain interfaces specified in the generics … then the concrete domains
    //  implement the domain interfaces and these can be returned from the
    //  services. But obviously any caller receives the interface. Which is fine
    //  because the interface has all of the properties and methods."
    //
    // The load-bearing sentence is the last one. Because a caller never needs the
    // concrete type, the CLASS can declare its members at interface types — and
    // then the mirror is a verbatim copy, with nothing to bridge.

    [Fact]
    public void Nothing_is_generated_into_any_class()
    {
        // The structural assertion that the bridge is gone. An explicit interface
        // implementation compiles to a PRIVATE method in the interface map; a
        // class that satisfies its interface directly has only public ones.
        // Asserted across every marked fixture rather than one, so re-introducing
        // a bridge anywhere fails here [[probe-the-population-not-the-item]].
        var marked = typeof(Basic).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace == typeof(Basic).Namespace)
            .ToArray();

        Assert.NotEmpty(marked);

        var withExplicitImplementations = marked
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.Namespace == typeof(Basic).Namespace)
                // GENERATED interfaces only. A hand-written one (IProbe) may well
                // be implemented explicitly — that is a choice the author made,
                // and this test is about what the GENERATOR writes.
                .Where(i => i.GetCustomAttribute<GeneratedCodeAttribute>() is not null)
                .Where(i => t.GetInterfaceMap(i).TargetMethods.Any(m => m.IsPrivate))
                .Select(i => $"{t.Name} -> {i.Name}"))
            .ToArray();

        Assert.Empty(withExplicitImplementations);
    }

    [Fact]
    public void A_member_at_a_mirrored_type_is_the_interface_on_both_sides()
    {
        // Under the bridge design these two lines disagreed on purpose: the
        // interface said IAddr, the class said Addr, and generated code joined
        // them. Agreeing is the whole change.
        Assert.Equal(typeof(IAddr), typeof(IOrder).GetProperty(nameof(Order.ShipTo))!.PropertyType);
        Assert.Equal(typeof(IAddr), typeof(Order).GetProperty(nameof(Order.ShipTo))!.PropertyType);
    }

    [Fact]
    public void A_foreign_implementation_can_be_written_through_the_interface()
    {
        // The bridge's setter cast `(Addr)value` threw InvalidCastException here,
        // and that was documented as the shape's one real cost. It is not a cost
        // of the RULE — it was a cost of rewriting the signature. The class means
        // IAddr now, so any IAddr is simply assignable.
        IOrder order = new Order();
        var foreign = new ForeignAddr { Line1 = "elsewhere" };

        order.ShipTo = foreign;

        Assert.Same(foreign, order.ShipTo);
        Assert.Same(foreign, ((Order)order).ShipTo);
    }

    [Fact]
    public void A_method_parameter_is_an_interface_too_not_just_the_return()
    {
        // The asymmetry the substituting design could not remove. A bridge could
        // widen a RETURN (covariant, cannot fail) but never a PARAMETER: declaring
        // ShipsTo(IAddr) for a class accepting only Addr promised something the
        // narrowing cast had to break at runtime, so parameters stayed concrete
        // and James's rule was honoured on half the signature.
        Assert.Equal(typeof(IAddr), typeof(IOrder).GetMethod(nameof(Order.Primary))!.ReturnType);
        Assert.Equal(
            typeof(IAddr),
            typeof(IOrder).GetMethod(nameof(Order.ShipsTo))!.GetParameters()[0].ParameterType);
        Assert.Equal(
            typeof(IBasic),
            typeof(IBasic).GetMethod(nameof(Basic.Equals), [typeof(IBasic)])!.GetParameters()[0].ParameterType);

        // And it works from a foreign implementation, which is exactly the call
        // the bridge threw on.
        Assert.True(new Order { ShipTo = new Addr { Line1 = "1 A St" } }
            .ShipsTo(new ForeignAddr { Line1 = "1 A St" }));
    }

    [Fact]
    public void A_generic_base_closed_over_an_interface_mirrors_verbatim()
    {
        // "the domain interface can implement with domain interfaces specified in
        //  the generics" — and the concrete closes the SAME template one layer down.
        Assert.Contains(typeof(IPersonBase<IAddr>), typeof(IPerson).GetInterfaces());
        Assert.Contains(typeof(IPersonBase<IDomainAddr>), typeof(IDomainPerson).GetInterfaces());

        // The class's own base carries the same argument — that agreement is what
        // means no bridge, and it is the thing a test can see that a compile cannot.
        Assert.Equal(typeof(PersonBase<IAddr>), typeof(Person).BaseType);
        Assert.Equal(typeof(PersonBase<IDomainAddr>), typeof(DomainPerson).BaseType);

        IPersonBase<IDomainAddr> domain = new DomainPerson { Address = new DomainAddr { Line1 = "high st" } };
        Assert.Equal("HIGH ST", domain.Address.Normalised());
    }

    [Fact]
    public void An_invariant_generic_is_reachable_now_and_was_not_before()
    {
        // The strongest evidence that his shape is not a weaker version of the
        // rule. Rewriting List<Addr> to List<IAddr> could never work — they are
        // unrelated types, so the bridge would not compile — and the generator had
        // to refuse, keeping ICrateBase<Addr> and reporting NSGEN004. Written at
        // the interface type by hand it is unremarkable, and NSGEN004 is deleted.
        Assert.Contains(typeof(ICrateBase<IAddr>), typeof(ICrate).GetInterfaces());
        Assert.Equal(
            typeof(List<IAddr>),
            typeof(ICrateBase<IAddr>).GetProperty(nameof(Crate.Items))!.PropertyType);

        // Both halves of the old variance rule, on one class, now the same answer.
        Assert.Equal(
            typeof(IReadOnlyList<IAddr>),
            typeof(IManifest).GetProperty(nameof(Manifest.Stops))!.PropertyType);
        Assert.Equal(
            typeof(List<IAddr>),
            typeof(IManifest).GetProperty(nameof(Manifest.Crates))!.PropertyType);
    }

    [Fact]
    public void A_bcl_interface_that_fixes_a_signature_is_left_alone()
    {
        // IEquatable<Coin> requires exactly Equals(Coin?). The author cannot widen
        // it — widening stops implementing the interface — so NSGEN005 exempts it
        // and the mirror copies the concrete parameter. (That the fixture compiles
        // with TreatWarningsAsErrors is the assertion that it is not reported;
        // DiagnosticTests pins the exemption's boundary.)
        Assert.Equal(
            typeof(Coin),
            typeof(ICoin).GetMethod(nameof(Coin.Equals), [typeof(Coin)])!.GetParameters()[0].ParameterType);
    }

    // ------------------------------------- member kinds nothing else exercises
    // Every test below exists because a mutation survived: the rule was written,
    // asserted in prose, and had no fixture that could tell it from its opposite
    // ([[an-uncaught-mutation-is-a-finding]]).

    [Fact]
    public void An_event_is_mirrored()
    {
        // The README has claimed this since the generator was written and no
        // fixture had an event, so `case IEventSymbol: return true` could be
        // flipped to `false` with the whole suite still green.
        var evt = typeof(ISignal).GetEvent(nameof(Signal.Fired));
        Assert.NotNull(evt);
        Assert.Equal(typeof(EventHandler), evt.EventHandlerType);
    }

    [Fact]
    public void An_explicit_interface_implementation_is_not_mirrored()
    {
        // It is already tied to another interface, so restating it would put a
        // member on ISignal that Signal does not publicly have. Covered by
        // accident until the bridge was deleted — the bridge WAS an explicit
        // implementation, on several fixtures at once.
        Assert.DoesNotContain("Probe", MemberNames<ISignal>());
        Assert.Contains("Raise", MemberNames<ISignal>());
    }

    [Fact]
    public void A_property_with_a_public_setter_and_a_private_getter_is_mirrored()
    {
        // "at least one accessor is public" is two clauses, and every other
        // fixture satisfies the first — so the second had never been the reason
        // for an answer and could be inverted unnoticed.
        var token = typeof(ISignal).GetProperty(nameof(Signal.Token));
        Assert.NotNull(token);
        Assert.False(token.CanRead);
        Assert.True(token.CanWrite);
    }

    [Fact]
    public void An_abstract_class_gets_no_json_converter_and_a_concrete_one_does()
    {
        // The converter rule is "not abstract AND not generic". Every abstract
        // fixture was also generic and vice versa, so `&&` and `||` agreed
        // everywhere. AbstractSignal is abstract and NOT generic, which is the
        // only shape that tells them apart.
        Assert.Null(typeof(IAbstractSignal).GetCustomAttribute<JsonConverterAttribute>());
        Assert.NotNull(typeof(IAddr).GetCustomAttribute<JsonConverterAttribute>());

        // …and the template layer, which is both.
        Assert.Null(typeof(IPersonBase<>).GetCustomAttribute<JsonConverterAttribute>());
    }

    // ----------------------------------------------- the one cost, measured
    [Fact]
    public void A_property_at_an_interface_type_does_not_deserialise_on_its_own()
    {
        // Stated rather than discovered in a route. System.Text.Json cannot pick a
        // concrete type for an interface-typed property; the bridge design avoided
        // this by keeping the class's property concrete, which is exactly the part
        // James rejected. This test is the gap, and it is closed by the converter
        // the generator emits — HandWritten below is the permanent control that
        // System.Text.Json still cannot do it unaided.
        var ex = Assert.Throws<NotSupportedException>(() =>
            JsonSerializer.Deserialize<HandWrittenHolder>("""{"Item":{"Line1":"12 High St"}}"""));

        Assert.Contains("interface", ex.Message);
    }

    [Fact]
    public void A_generated_interface_carries_a_converter_to_the_one_concrete_it_mirrors()
    {
        // The generator made IAddr from Addr, so it knows both halves of the pair
        // and can put the converter on the interface it writes. Nothing is
        // registered on JsonSerializerOptions by the caller.
        var order = JsonSerializer.Deserialize<Order>(
            """{"Reference":"A1","ShipTo":{"Line1":"12 High St"}}""")!;

        Assert.Equal("12 High St", order.ShipTo.Line1);
        Assert.IsType<Addr>(order.ShipTo);
        Assert.Equal(
            """{"Reference":"A1","ShipTo":{"Line1":"12 High St"}}""",
            JsonSerializer.Serialize(order));
    }

    [Fact]
    public void Serialising_writes_whatever_implementation_is_actually_there()
    {
        // The converter must not cast on the way OUT. A foreign implementation is
        // a legal value of the property, and a converter that cast it to the
        // mirrored concrete would throw on serialisation — turning a read path
        // into the failure the bridge's setter used to be.
        var order = new Order { Reference = "A1", ShipTo = new ForeignAddr { Line1 = "elsewhere" } };

        Assert.Equal(
            """{"Reference":"A1","ShipTo":{"Line1":"elsewhere"}}""",
            JsonSerializer.Serialize(order));
    }

    [Fact]
    public void A_null_interface_property_round_trips()
    {
        var round = JsonSerializer.Deserialize<Order>(
            """{"Reference":"A1","ShipTo":null}""")!;

        Assert.Null(round.ShipTo);
    }
}

/// <summary>Another IAddr the generator has never seen — the foreign
/// implementation that makes "any caller receives the interface" testable.</summary>
public sealed class ForeignAddr : IAddr
{
    public string Line1 { get; set; } = "";
}

/// <summary>Hand-written, unmarked, and therefore with no generated converter:
/// the permanent control that System.Text.Json cannot deserialise an
/// interface-typed property unaided.</summary>
public interface IHandWritten
{
    string Line1 { get; set; }
}

public sealed class HandWrittenHolder
{
    public IHandWritten? Item { get; set; }
}
