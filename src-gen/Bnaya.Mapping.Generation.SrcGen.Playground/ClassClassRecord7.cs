using Bnaya.Mapping;

namespace Bnaya.Generation.SrcGen.Playground
{
    public partial class Class7BaseBase
    {
        public partial class Class7Base
        {
            [Dictionaryable(Flavor = Flavor.Neo4j)]
            private partial record Record7(string x, double? y, float q = 2)
            {
                public int Z { get; init; }
                public int? W { get; init; }
            };
        }
    }
}
