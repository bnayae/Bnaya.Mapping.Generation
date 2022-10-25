using System.Collections.Immutable;

using Weknow.Generation.SrcGen.Playground;

using Xunit;


namespace Weknow.Text.Json.Extensions.Tests
{
    public class MappingTests
    {

        [Fact]
        public void Class1_Test()
        {
            var c = new Class1 { A = 1, B = 2 };
            Dictionary<string, object?> d = c.ToDictionary();
            ImmutableDictionary<string, object?> di = c.ToImmutableDictionary();
            Class1 c1 = d;
            Class1 c2 = di;

            Assert.Equal(c, c1);
            Assert.Equal(c, c2);
        }

        [Fact]
        public void Record3_Test()
        {
            var c = new Record3("Hi") { Y = 2 };
            Dictionary<string, object?> d = c.ToDictionary();
            ImmutableDictionary<string, object?> di = c.ToImmutableDictionary();
            Record3 c1 = d;
            Record3 c2 = di;
            Record3 c3 = (Record3)di;

            Assert.Equal(c, c1);
            Assert.Equal(c, c2);
            Assert.Equal(c, c3);
        }

        [Fact]
        public void Record3_ReadOnly_Test()
        {
            var c = new Record3("Hi") { Y = 2 };
            IReadOnlyDictionary<string, object?> d = c.ToDictionary();
            Record3 c1 = (Record3)(Dictionary<string, object?>)d;

            Assert.Equal(c, c1);
        }

        [Fact]
        public void Record4_Test()
        {
            var c = new Record4 { A = 1, B = 2 };
            Dictionary<string, object?> d = c.ToDictionary();
            ImmutableDictionary<string, object?> di = c.ToImmutableDictionary();
            Record4 c1 = d;
            Record4 c2 = di;

            Assert.Equal(c, c1);
            Assert.Equal(c, c2);
        }

        [Fact]
        public void Record4_Null_Test()
        {
            var c = new Record4 { A = 1 };
            Dictionary<string, object?> d = c.ToDictionary();
            ImmutableDictionary<string, object?> di = c.ToImmutableDictionary();
            Record4 c1 = d;
            Record4 c2 = di;

            Assert.Equal(c, c1);
            Assert.Equal(c, c2);
        }

        [Fact]
        public void Record5_Test()
        {
            var c = new Record5 { A = 1, B = 2 };
            Dictionary<string, object?> d = c.ToDictionary();
            ImmutableDictionary<string, object?> di = c.ToImmutableDictionary();
            Record5 c1 = d;
            Record5 c2 = di;

            Assert.Equal(c, c1);
            Assert.Equal(c, c2);
        }

        [Fact]
        public void Record5_Null_Test()
        {
            var c = new Record5 { A = 1 };
            Dictionary<string, object?> d = c.ToDictionary();
            ImmutableDictionary<string, object?> di = c.ToImmutableDictionary();
            Record5 c1 = d;
            Record5 c2 = di;

            Assert.Equal(c, c1);
            Assert.Equal(c, c2);
        }

        [Fact]
        public void Struct5_Test()
        {
            var c = new Struct5 { A = 1, B = 2 };
            IReadOnlyDictionary<string, object> d = c.ToDictionary().ToDictionary(m => m.Key, m => m.Value ?? throw new Exception());
            Struct5 c1 = Struct5.FromReadOnlyDictionary(d);

            Assert.Equal(c, c1);
        }

        [Fact]
        public void Struct5_Nullable_Test()
        {
            var c = new Struct5 { A = 1, B = 2 };
            Dictionary<string, object?> d = c.ToDictionary();
            ImmutableDictionary<string, object?> di = c.ToImmutableDictionary();
            Struct5 c1 = d;
            Struct5 c2 = di;

            Assert.Equal(c, c1);
            Assert.Equal(c, c2);
        }

        [Fact]
        public void Struct5_Null_Test()
        {
            var c = new Struct5 { A = 1 };
            Dictionary<string, object?> d = c.ToDictionary();
            ImmutableDictionary<string, object?> di = c.ToImmutableDictionary();
            Struct5 c1 = d;
            Struct5 c2 = di;

            Assert.Equal(c, c1);
            Assert.Equal(c, c2);
        }
    }
}
