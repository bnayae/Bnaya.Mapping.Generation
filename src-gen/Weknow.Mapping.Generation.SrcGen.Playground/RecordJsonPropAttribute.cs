using System.Text.Json.Serialization;

using Weknow.Mapping;

namespace Weknow.Generation.SrcGen.Playground
{
    [Dictionaryable(Flavor=Flavor.Neo4j)]
    public partial record RecordJsonPropAttribute(string Id)
    {
        [JsonPropertyName("creation-date")]
        public required DateTimeOffset CreatedAt { get; init; }
        [JsonPropertyName("modification-date")]
        public DateTimeOffset? ModifiedAt { get; init; }
    };
}
