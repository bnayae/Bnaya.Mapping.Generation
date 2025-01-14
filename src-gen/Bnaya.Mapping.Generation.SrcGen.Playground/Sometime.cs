﻿using Bnaya.Mapping;

namespace Bnaya.Generation.SrcGen.Playground;

[Dictionaryable(Flavor = Mapping.Flavor.Neo4j)]
public partial record Sometime
{
    public required string Name { get; init; }
    public required DateTimeOffset Birthday { get; init; }
    public required DateTimeOffset Local { get; init; }
    public required DateTime IssueDate { get; init; }
    public required TimeSpan At { get; init; }
    public DateTime? Might { get; init; }
}