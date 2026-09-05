using Northstar.SourceGenerators;

namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>
/// A tree node whose children are nodes of the same layer, reached through the
/// interface: <c>INode : INodeBase&lt;INode&gt;</c>, closed over itself.
/// </summary>
[GenerateInterface]
public class Node : NodeBase<INode>, INode
{
}
