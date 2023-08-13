using Bnaya.Mapping;

namespace Bnaya.Generation.SrcGen.Playground
{
    [Dictionaryable]
    public partial record struct RecordHierarchic
    {
        public int Id { get; init; }
        public string[] SimpleArray { get; init; }
        public Record4[] ComplexArray { get; init; }
        public Record3[]? OptionalComplexArray { get; init; }
    }
}
