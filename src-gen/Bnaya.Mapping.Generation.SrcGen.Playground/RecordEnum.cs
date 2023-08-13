using Bnaya.Mapping;

namespace Bnaya.Generation.SrcGen.Playground
{
    [Dictionaryable(Flavor = Flavor.Neo4j)]
    public partial record RecordEnum(string Id, ConsoleColor Color)
    {
        public ConsoleColor Background { get; init; }
        public string Name { get; init; } = string.Empty;
    };

}
