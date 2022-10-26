using Weknow.Mapping;

namespace Weknow.Generation.SrcGen.Playground
{
    [Dictionaryable]
    public partial record Record3(string x, int y)
    {
        public int Z { get; init; }
    };
}
