using Weknow.Mapping;

namespace Weknow.Generation.SrcGen.Playground
{
    [Dictionaryable]
    public partial record Record3(string x, double? y, float q = 2)
    {
        public required int Z { get; init; }
        public int? W { get; init; }
        public string? S { get; init; }
    };
}
