using Weknow.Mapping;

namespace Weknow.Generation.SrcGen.Playground
{
    [Dictionaryable]
    public partial record RecordInheritance: Record4
    {
        public int C { get; init; }
    }
}
