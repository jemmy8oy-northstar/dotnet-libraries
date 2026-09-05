# Northstar.SourceGenerators

`[GenerateInterface]` on a concrete class emits `I{ClassName}` beside it, mirroring
the public surface. You write `: IMyClass` yourself, so the implements-relationship
stays visible in the source you read.

```csharp
using Northstar.SourceGenerators;

[GenerateInterface]
public class Status : IStatus          // IStatus exists nowhere in source
{
    public string Name { get; set; } = "";
    public bool IsHealthy() => true;
}
```

The attribute comes with the package — there is nothing else to reference.

## Why

A contract type on a route and a domain model of the same thing want to share a
shape without one depending on the other. Mirror both onto one generic base and a
single consumer serves both layers:

```csharp
[GenerateInterface] public abstract class PersonBase<TAddress> where TAddress : IAddr { ... }
[GenerateInterface] public class Person       : PersonBase<IAddr>,       IPerson { }
[GenerateInterface] public class DomainPerson : PersonBase<IDomainAddr>, IDomainPerson { }

static string NameOf<TPerson, TAddress>(TPerson p)
    where TPerson : IPersonBase<TAddress> where TAddress : IAddr => p.Name;
```

## What is mirrored

Public instance properties (accessors preserved, including `init`), methods
(optional/`out`/`params`/generic with constraints), events, indexers, nullable
annotations, and generic type parameters with their constraints. A base class that
also carries the attribute becomes interface inheritance, so shared members are
declared once.

Excluded: static and non-public members, constructors, operators, explicit
interface implementations, and overrides of `object`'s virtuals — but *not* a
user-defined overload that merely shares a name, e.g. `Equals(MyType)`.

## Interfaces only in interfaces

A generated interface refers to another **mirrored** type by its interface. That
is a rule about what *you* declare, not a rewrite the generator performs: the
mirror is a verbatim copy of the class's public surface, so nothing is ever
generated *into* your class and no class has to be `partial`.

```csharp
[GenerateInterface]
public class Order : IOrder
{
    public IAddr ShipTo { get; set; } = new Addr();      // the interface, on the class
    public bool ShipsTo(IAddr candidate) => true;        // parameters too
}

// generated — a copy, with a converter (see below)
public interface IOrder { IAddr ShipTo { get; set; } bool ShipsTo(IAddr candidate); }
```

The same goes for a generic base: close it over the interface, and the mirror
follows. This is the layering the whole thing exists for — one abstract template,
closed once per layer:

```csharp
[GenerateInterface] public abstract class PersonBase<TAddress> where TAddress : IAddr { ... }
[GenerateInterface] public class Person       : PersonBase<IAddr>,       IPerson { }
[GenerateInterface] public class DomainPerson : PersonBase<IDomainAddr>, IDomainPerson { }
```

`Person.Address` is literally an `IAddr`, so `Person` satisfies `IPerson` on its
own. A caller never needs the concrete type — the interface has all of the
properties and methods — and services return concretes that callers receive as
interfaces.

**`NSGEN005` is how the rule is enforced.** Declare a member (or close a base) at
a mirrored *concrete* type and it names the member and the interface form to
write instead, including inside generics:

```
'Manifest.Crates' is declared at 'List<Addr>', which has its own generated
interface; declare it at 'List<IAddr>' so the interface this class implements
references only interfaces
```

It is a **warning**, because the code compiles either way — every repo in this
org sets `TreatWarningsAsErrors`, which is what makes it structural. The one
exemption: a signature fixed by an interface from a *referenced assembly*, such
as `IEquatable<T>`'s `Equals(T?)`, which you cannot widen without ceasing to
implement it. Interfaces in your own source — including this generator's own
mirrors — earn no exemption.

### JSON

Declaring a property at an interface costs exactly one thing: `System.Text.Json`
cannot pick a concrete type for it. The generator made the interface *from* a
concrete, so it emits the converter naming it and puts it on the interface —
nothing to register, no `JsonSerializerOptions` to thread through.

```csharp
// generated
[JsonConverter(typeof(IAddrJsonConverter))]
public interface IAddr { ... }
```

Reading builds the mirrored concrete. Writing serialises the **runtime** type
rather than casting, so a foreign implementation held in an interface-typed
property serialises correctly instead of throwing. Skipped when the compilation
has no `System.Text.Json`, and for abstract or generic classes (the templates —
you deserialise the closed type, not the template).

### What this replaced

The first design had the generator *rewrite* member types into interfaces and
then write an explicit implementation — a "bridge" — into the class to join the
two, which is why classes had to be `partial`. James rejected it
([#3](https://github.com/jemmy8oy-northstar/dotnet-libraries/pull/3)), and
declaring the interfaces on the class turns out to be strictly more capable:

| | bridge (old) | declared (now) |
|---|---|---|
| Method **parameters** | concrete — a widened parameter promised something the narrowing cast broke at runtime | interface |
| Inside an **invariant** generic (`List<T>`) | impossible; refused with `NSGEN004` | `List<IAddr>`, unremarkable |
| Writing a **foreign** implementation through the interface | `InvalidCastException` | an assignment |
| Class must be `partial` | when bridged | never |
| Deserialising an interface-typed property | n/a — the class kept a concrete | generated converter |

`NSGEN003` and `NSGEN004` existed only to describe the bridge's limits and are
gone with it.

Diagnostics: `NSGEN001` nested class, `NSGEN002` static class, `NSGEN005` a
concrete type in a generated interface.

## Consuming it

```xml
<PackageReference Include="Northstar.SourceGenerators" Version="x.y.z"
                  PrivateAssets="all" />
```

Analyzer-only: nothing is added to your output assembly.
