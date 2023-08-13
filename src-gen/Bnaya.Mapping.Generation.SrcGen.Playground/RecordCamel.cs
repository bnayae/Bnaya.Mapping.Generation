using Bnaya.Mapping;

namespace Bnaya.Generation.SrcGen.Playground
{
    [Dictionaryable(CaseConvention = CaseConvention.camelCase)]
    public partial record RecordCamel(string WallOfChina)
    {
        public string NothingNew { get; init; } = string.Empty;
    };

}
