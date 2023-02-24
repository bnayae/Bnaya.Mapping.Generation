using Weknow.Mapping;

namespace Weknow.Generation.SrcGen.Playground
{
    [Dictionaryable(Flavor = Flavor.Neo4j)]
    public partial record RecordCollectionN4j(IEnumerable<int> ids)
    {
        public required string[] Tags { get; init; }
        public required List<RecordCollectionN4j?> Children { get; init; }
    };
}
