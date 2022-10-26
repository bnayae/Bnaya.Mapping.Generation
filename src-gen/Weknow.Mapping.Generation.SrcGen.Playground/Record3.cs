using Weknow.Mapping;

namespace Weknow.Generation.SrcGen.Playground
{
    [Dictionaryable]
    public partial record Record3(string x, double? y, float q = 2)
    {
        public int Z { get; init; }
        public int? W { get; init; }

        /// <summary>
        /// Converts source dictionary.
        /// </summary>
        /// <param name="source">source of the data.</param>
        /// <returns></returns>
        public static Record3 Factory(IDictionary<string, object> @source)
        {
            Record3 result = new Record3((string)@source["x"],
                @source.ContainsKey("y")
                           ? (double?)@source["y"]
                           : default(double?),
                @source.ContainsKey("q")
                           ? (float)@source["q"]
                           : default(float))
            {
                //Z = @source.ContainsKey("Z") ? (Int32)(long)@source["Z"] : default(Int32)
            };
            return result;
        }

        /// <summary>
        /// Converts source dictionary.
        /// </summary>
        /// <param name="source">source of the data.</param>
        /// <returns></returns>
        public static Record3 Factory1(IDictionary<string, object> @source)
        {
            Record3 result = new Record3((string)@source["x"],
                Convert.ToInt32(@source["y"]),
                Convert.ToInt32(@source["q"]))
            {
                Z = Convert.ToInt32(@source["Z"]),
                W = Convert.ToInt32(@source["W"])
                
            };
            return result;
        }
    };
}
