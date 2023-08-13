using Bnaya.Mapping;

namespace Bnaya.Generation.SrcGen.Playground
{
    [Dictionaryable]
    public partial record RecordInheritance: Record4
    {
        public int C { get; init; }
    }
}
