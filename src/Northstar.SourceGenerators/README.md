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
[GenerateInterface] public class Person       : PersonBase<Addr>,       IPerson { }
[GenerateInterface] public class DomainPerson : PersonBase<DomainAddr>, IDomainPerson { }

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

A generated interface refers to another **mirrored** type by its interface, not
its concrete class. The class keeps the concrete member, and the generator writes
an explicit implementation to join them — so `System.Text.Json`, which ignores
explicit implementations, still round-trips the concrete property with no custom
converter.

```csharp
[GenerateInterface]
public partial class Order : IOrder    // partial: it gets a generated bridge
{
    public Addr ShipTo { get; set; } = new();   // concrete on the class
}

// generated
public interface IOrder { IAddr ShipTo { get; set; } }
public partial class Order
{
    IAddr IOrder.ShipTo { get => ShipTo; set => ShipTo = (Addr)value; }
}
```

The same applies to a generic base's type arguments: `Person : PersonBase<Addr>`
mirrors to `IPerson : IPersonBase<IAddr>`.

**Where it stops, and why.**

| Position | Substituted? | Reason |
|---|---|---|
| Property type | yes | bridged by an explicit implementation |
| Method return type | yes | covariant — the bridge only widens |
| Method parameter type | **no** | contravariant: `ShipsTo(IAddr)` would promise the class accepts any `IAddr` when it accepts only `Addr` |
| Inside a covariant generic (`IReadOnlyList<out T>`) | yes | the widening is safe |
| Inside an invariant generic (`List<T>`) | **no** (`NSGEN004`) | `List<Addr>` and `List<IAddr>` have no conversion either way, so the bridge would not compile |
| Indexers, generic methods, events | no | a cast cannot express the mapping |

The one cost over closing a generic base over concrete types: writing a *foreign*
implementation through the interface is a compile error there and an
`InvalidCastException` here. Only code that writes through the interface can
reach it.

Diagnostics: `NSGEN001` nested class, `NSGEN002` static class, `NSGEN003` a class
needing a bridge is not `partial`, `NSGEN004` a generic base kept concrete type
arguments.

## Consuming it

```xml
<PackageReference Include="Northstar.SourceGenerators" Version="x.y.z"
                  PrivateAssets="all" />
```

Analyzer-only: nothing is added to your output assembly.
