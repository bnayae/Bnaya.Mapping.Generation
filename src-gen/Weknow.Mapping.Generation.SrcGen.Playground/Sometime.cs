using Weknow.Mapping;

namespace Weknow.Generation.SrcGen.Playground;

[Dictionaryable(Flavor = Mapping.Flavor.Neo4j)]
internal partial record Sometime
{
    public required string Name { get; init; }
    public required DateTimeOffset Birthday { get; init; }
    public required DateTimeOffset Local { get; init; }
    public required DateTime IssueDate { get; init; }
    public required TimeSpan At { get; init; }
}