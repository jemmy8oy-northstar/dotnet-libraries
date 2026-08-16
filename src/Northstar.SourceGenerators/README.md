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

Diagnostics: `NSGEN001` nested class, `NSGEN002` static class.

## Consuming it

```xml
<PackageReference Include="Northstar.SourceGenerators" Version="x.y.z"
                  PrivateAssets="all" />
```

Analyzer-only: nothing is added to your output assembly.
