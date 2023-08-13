using Bnaya.Mapping;

namespace Bnaya.Generation.SrcGen.Playground
{
    [Dictionaryable]
    public partial class ClassUnmatch : IEquatable<ClassUnmatch>
    {
        public ClassUnmatch(int x, int y)
        {
            A = x;
            B = y;
        }
    
        public int A { get;  }
        public int B { get;  }

        bool IEquatable<ClassUnmatch>.Equals(ClassUnmatch? other) => A == other?.A && B == other?.B;
    }

}
