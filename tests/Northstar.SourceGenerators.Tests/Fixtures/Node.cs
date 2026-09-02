using Northstar.SourceGenerators;

namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>A tree node whose children are nodes of the same layer.</summary>
[GenerateInterface]
public partial class Node : NodeBase<Node>, INode
{
}
