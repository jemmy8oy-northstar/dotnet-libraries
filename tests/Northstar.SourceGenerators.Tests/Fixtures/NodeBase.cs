using Northstar.SourceGenerators;

namespace Northstar.SourceGenerators.Tests.Fixtures;

/// <summary>
/// The case that looks like it needs an unbounded type-parameter list: a model
/// that nests ITSELF.
/// </summary>
/// <remarks>
/// F-bounded polymorphism closes the loop in exactly one parameter, which is
/// what gives the transitive parameter computation a stopping condition. A
/// cycle of N mutually-nesting types needs exactly N parameters.
/// </remarks>
[GenerateInterface]
public abstract class NodeBase<TNode> : INodeBase<TNode>
    where TNode : INodeBase<TNode>
{
    public string Label { get; set; } = "";

    public IReadOnlyList<TNode> Children { get; set; } = new List<TNode>();
}
