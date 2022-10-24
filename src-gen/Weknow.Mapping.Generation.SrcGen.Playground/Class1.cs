namespace Weknow.Mapping.Generation.SrcGen.Playground
{
    [Dictionaryable]
    public partial class Class1: IEquatable<Class1>
    {
        public int A { get; init; }
        public int B { get; init; }

        bool IEquatable<Class1>.Equals(Class1? other) => A == other?.A && B == other?.B;
    }

}
