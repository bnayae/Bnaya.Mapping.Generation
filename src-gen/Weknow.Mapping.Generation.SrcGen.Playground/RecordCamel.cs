using Weknow.Mapping;

namespace Weknow.Generation.SrcGen.Playground
{
    [Dictionaryable(CaseConvention = CaseConvention.camelCase)]
    public partial record RecordCamel(string WallOfChina)
    {
        public string NothingNew { get; init; } = string.Empty;
    };

}
