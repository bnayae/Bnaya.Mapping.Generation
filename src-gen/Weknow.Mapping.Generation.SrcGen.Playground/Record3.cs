using Weknow.Mapping;

namespace Weknow.Generation.SrcGen.Playground
{
    [Dictionaryable]
    public partial record Record3(string x, double? y, float q = 2)
    {
        public int Z { get; init; }
        public int? W { get; init; }
        public static Record3 Factory(IDictionary<string, object> @source)
        {
            Record3 result = new Record3((string)@source["x"],
                @source.ContainsKey("y")
                        ? Convert.ToDouble(@source["y"])
                        : default(double?),
                Convert.ToSingle(@source["q"]))
            {
                Z = (int)@source["Z"],
                W = @source.ContainsKey("W")
                           ? (int?)@source["W"]
                           : default(int?)
            };
            return result;
        }

    };
}
