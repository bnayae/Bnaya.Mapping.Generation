using Weknow.Mapping;

namespace Weknow.Generation.SrcGen.Playground
{
    [Dictionaryable(CaseConvention = CaseConvention.PascalCase)]
    public partial record RecordPascal(string WallOfChina)
    {
        public string NothingNew { get; init; } = string.Empty;
    };

}
