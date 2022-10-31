using Weknow.Mapping;

namespace Weknow.Generation.SrcGen.Playground
{
    public partial class Class6
    {
        [Dictionaryable]
        private partial record Record6(string x, double? y, float q = 2)
        {
            public int Z { get; init; }
            public int? W { get; init; }
        };
    }
}
