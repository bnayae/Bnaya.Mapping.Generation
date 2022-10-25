using Weknow.Mapping;

namespace Weknow.Generation.SrcGen.Playground
{
    [Dictionaryable]
    public partial record Record3(string x)
    {
        public int Y { get; init; }
    };
}
