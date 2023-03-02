using Weknow.Mapping;

namespace Weknow.Generation.SrcGen.Playground
{
    [Dictionaryable]
    public partial class ClassUnmatch : IEquatable<Class1>
    {
        public ClassUnmatch(int x, int y)
        {
            A = x;
            B = y;
        }
    
        public int A { get; init; }
        public int B { get; init; }

        bool IEquatable<Class1>.Equals(Class1? other) => A == other?.A && B == other?.B;
    }

}
