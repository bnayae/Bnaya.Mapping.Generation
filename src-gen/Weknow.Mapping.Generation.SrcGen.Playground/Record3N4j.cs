using Weknow.Mapping;

namespace Weknow.Generation.SrcGen.Playground
{
    [Dictionaryable(Flavor = Flavor.Neo4j)]
    public partial record Record3N4j(string x, double? y, float q = 2)
    {
        public int Z { get; init; }
        public int? W { get; init; }
        public string? S { get; init; }
    };
}
