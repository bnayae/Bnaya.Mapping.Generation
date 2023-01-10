using Weknow.Mapping;

namespace Weknow.Generation.SrcGen.Playground
{
    [Dictionaryable(CaseConvention = CaseConvention.SCREAMING_CASE)]
    public partial record RecordScream(string WallOfChina)
    {
        public string NothingNew { get; init; } = string.Empty;
    };

}
