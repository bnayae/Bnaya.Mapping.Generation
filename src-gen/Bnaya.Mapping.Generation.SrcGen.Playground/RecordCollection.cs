using Bnaya.Mapping;


namespace Bnaya.Generation.SrcGen.Playground
{
    [Dictionaryable]
    public partial record RecordCollection(IEnumerable<int> ids)
    {
        public required string Id { get; init; }
        public required string[] Tags { get; init; }
        public required List<RecordCollection?> Children { get; init; }
        public RecordCollection? Parent { get; init; }
    };
}
