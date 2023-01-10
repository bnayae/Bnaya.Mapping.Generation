using Weknow.Mapping;

namespace Weknow.Generation.SrcGen.Playground
{
    [Dictionaryable(CaseConvention = CaseConvention.dash_case)]
    public partial record RecordDash(string WallOfChina)
    {
        public string NothingNew { get; init; } = string.Empty;
    };

}
