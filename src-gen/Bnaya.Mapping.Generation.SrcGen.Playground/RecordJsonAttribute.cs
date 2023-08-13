using System.Text.Json.Serialization;

using Bnaya.Mapping;

namespace Bnaya.Generation.SrcGen.Playground
{
    [Dictionaryable]
    public partial record struct RecordJsonAttribute
    {
        [JsonPropertyName("Testing_Me")]
        public int TestMe { get; init; }
        public int B { get; init; }
    }
}
