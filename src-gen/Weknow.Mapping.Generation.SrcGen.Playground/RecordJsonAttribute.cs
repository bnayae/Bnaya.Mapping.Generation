using System.Text.Json.Serialization;

using Weknow.Mapping;

namespace Weknow.Generation.SrcGen.Playground
{
    [Dictionaryable]
    public partial record struct RecordJsonAttribute
    {
        [JsonPropertyName("Testing_Me")]
        public int TestMe { get; init; }
        public int B { get; init; }
    }
}
