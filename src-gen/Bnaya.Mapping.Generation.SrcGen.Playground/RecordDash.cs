using Bnaya.Mapping;

namespace Bnaya.Generation.SrcGen.Playground
{
    [Dictionaryable(CaseConvention = CaseConvention.dash_case)]
    public partial record RecordDash(string WallOfChina)
    {
        public string NothingNew { get; init; } = string.Empty;
    };

}
