; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NSGEN001 | Northstar.SourceGenerators | Warning | [GenerateInterface] does not support nested classes
NSGEN002 | Northstar.SourceGenerators | Warning | [GenerateInterface] cannot be applied to a static class
NSGEN003 | Northstar.SourceGenerators | Error | [GenerateInterface] needs this class to be partial to generate its interface bridge
NSGEN004 | Northstar.SourceGenerators | Info | [GenerateInterface] kept concrete type arguments on a generic base whose members are not bridgeable
