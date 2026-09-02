using System.CodeDom.Compiler;
using System.Reflection;
using System.Text.Json;
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
        // generator already substitutes a constructed base's type arguments.
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

        Assert.Empty(handWritten);
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
        // Equals(object?) is excluded; Equals(Basic?) is a real member.
        // Filtering by NAME rather than by what the method overrides would
        // silently drop this from every model that defines one.
        var equals = typeof(IBasic).GetMethod("Equals", new[] { typeof(Basic) });
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
        Assert.True(typeof(IPersonBase<Addr>).IsAssignableFrom(typeof(Person)));
        Assert.True(typeof(IPersonBase<DomainAddr>).IsAssignableFrom(typeof(DomainPerson)));
    }

    [Fact]
    public void One_generic_consumer_serves_both_layers()
    {
        // The whole point of the generics: this method is written once.
        static string NameOf<TPerson, TAddress>(TPerson person)
            where TPerson : IPersonBase<TAddress>
            where TAddress : IAddr
            => person.Name;

        Assert.Equal("contract", NameOf<Person, Addr>(new Person { Name = "contract" }));
        Assert.Equal("domain", NameOf<DomainPerson, DomainAddr>(new DomainPerson { Name = "domain" }));
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
        Assert.True(typeof(PersonBase<Addr>).IsAbstract);
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
            Children = new List<Node> { new() { Label = "child" } },
        };

        Assert.Equal("child", root.Children[0].Label);
    }

    // ------------------------------------------ interfaces only in interfaces
    // James, web-template#89: "why concrete addr here / Interfaces only in
    // interfaces. The concretes will also just have interfaces as the attribute
    // types. But in the service they can define the concrete which obviously
    // inherits the interface."

    [Fact]
    public void A_property_at_a_mirrored_type_is_declared_at_the_interface_type()
    {
        Assert.Equal(typeof(IAddr), typeof(IOrder).GetProperty(nameof(Order.ShipTo))!.PropertyType);
    }

    [Fact]
    public void The_class_keeps_the_concrete_property_so_it_still_deserialises()
    {
        // The whole reason for a bridge rather than an interface-typed field:
        // System.Text.Json cannot pick a concrete type for an interface-typed
        // property, but it ignores explicit implementations.
        Assert.Equal(typeof(Addr), typeof(Order).GetProperty(nameof(Order.ShipTo))!.PropertyType);

        var order = JsonSerializer.Deserialize<Order>(
            """{"Reference":"A1","ShipTo":{"Line1":"12 High St"}}""")!;

        Assert.Equal("12 High St", order.ShipTo.Line1);
        Assert.Equal("""{"Reference":"A1","ShipTo":{"Line1":"12 High St"}}""", JsonSerializer.Serialize(order));
    }

    [Fact]
    public void Reading_through_the_interface_hands_back_the_same_instance()
    {
        var order = new Order { ShipTo = new Addr { Line1 = "1 A St" } };
        Assert.Same(order.ShipTo, ((IOrder)order).ShipTo);
    }

    [Fact]
    public void Writing_a_foreign_implementation_through_the_interface_fails_loudly()
    {
        // The one real cost of this shape over closing the generic base over
        // concrete types, where the same mistake is a compile error. Loud either
        // way, and only code that WRITES through the interface can reach it.
        IOrder order = new Order();
        Assert.Throws<InvalidCastException>(() => order.ShipTo = new ForeignAddr());
    }

    [Fact]
    public void A_method_return_is_widened_but_a_parameter_is_not()
    {
        // Asymmetric on purpose. A return is covariant, so the bridge only
        // upcasts and cannot fail. A parameter is contravariant: declaring
        // ShipsTo(IAddr) would promise Order accepts any IAddr when it accepts
        // only Addr, and the bridge's narrowing cast would throw on every call
        // from another implementation. The first build with parameters
        // substituted turned Basic.Equals(Basic?) into Equals(IBasic?).
        Assert.Equal(typeof(IAddr), typeof(IOrder).GetMethod(nameof(Order.Primary))!.ReturnType);
        Assert.Equal(
            typeof(Addr),
            typeof(IOrder).GetMethod(nameof(Order.ShipsTo))!.GetParameters()[0].ParameterType);
        Assert.Equal(
            typeof(Basic),
            typeof(IBasic).GetMethod(nameof(Basic.Equals), [typeof(Basic)])!.GetParameters()[0].ParameterType);
    }

    [Fact]
    public void A_generic_base_is_closed_over_the_mirror_not_the_concrete()
    {
        // The shape James asked for: IPerson : IPersonBase<IAddr>.
        Assert.Contains(typeof(IPersonBase<IAddr>), typeof(IPerson).GetInterfaces());
        Assert.Contains(typeof(IPersonBase<IDomainAddr>), typeof(IDomainPerson).GetInterfaces());

        // …and the layering still works through it, over each layer's own mirror.
        IPersonBase<IDomainAddr> domain = new DomainPerson { Address = new DomainAddr { Line1 = "high st" } };
        Assert.Equal("HIGH ST", domain.Address.Normalised());
    }

    [Fact]
    public void A_covariant_generic_is_substituted_inside()
    {
        // IReadOnlyList<TNode> is covariant, so INodeBase can close over INode
        // and the bridge's getter is a widening that cannot fail.
        Assert.Equal(
            typeof(IReadOnlyList<INode>),
            typeof(INodeBase<INode>).GetProperty(nameof(Node.Children))!.PropertyType);
    }

    [Fact]
    public void Substitution_inside_a_generic_follows_that_generic_s_variance()
    {
        // Both on ONE class, so this is a statement about the rule and not about
        // where the type happened to come from. Covariant widens; invariant does
        // not, because List<Addr> and List<IAddr> have no conversion in either
        // direction and the bridge would not compile.
        Assert.Equal(
            typeof(IReadOnlyList<IAddr>),
            typeof(IManifest).GetProperty(nameof(Manifest.Stops))!.PropertyType);
        Assert.Equal(
            typeof(List<Addr>),
            typeof(IManifest).GetProperty(nameof(Manifest.Crates))!.PropertyType);
    }

    [Fact]
    public void An_invariant_generic_keeps_the_concrete_type_argument()
    {
        // The limit of "interfaces only in interfaces", and the reason it is a
        // measured fallback rather than a silent one (NSGEN004). List<Addr> and
        // List<IAddr> are unrelated types, so an interface declaring
        // List<IAddr> could not be bridged — the generated explicit
        // implementation would not compile.
        Assert.Contains(typeof(ICrateBase<Addr>), typeof(ICrate).GetInterfaces());
        Assert.DoesNotContain(
            typeof(ICrateBase<IAddr>),
            typeof(ICrate).GetInterfaces().Where(i => i.IsGenericType));

        // And with nothing substituted there is nothing to bridge, so the class
        // does not have to be partial — the cost is only paid where it buys
        // something. An explicit implementation is private; every member here
        // maps to the public one, so no bridge was emitted.
        Assert.All(
            typeof(Crate).GetInterfaceMap(typeof(ICrateBase<Addr>)).TargetMethods,
            m => Assert.True(m.IsPublic));

        // The control: Order DOES get a bridge, so the same check comes out the
        // other way. Without this the assertion above would pass on a generator
        // that never emits a bridge at all.
        Assert.Contains(
            typeof(Order).GetInterfaceMap(typeof(IOrder)).TargetMethods,
            m => m.IsPrivate);
    }
}

/// <summary>Another IAddr the bridge has never seen — the foreign implementation
/// that makes the write-through-the-interface cost observable.</summary>
public sealed class ForeignAddr : IAddr
{
    public string Line1 { get; set; } = "";
}
