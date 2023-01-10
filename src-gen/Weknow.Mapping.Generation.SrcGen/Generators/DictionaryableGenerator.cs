using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Weknow.Mapping;

// TODO: [bnaya 2022-10-24] Add ctor level attribue to select the From ctor
// TODO: [bnaya 2022-10-24] Add conventions (camel / Pascal)

[Generator]
public class DictionaryableGenerator : IIncrementalGenerator
{
    private const string TARGET_ATTRIBUTE = "DictionaryableAttribute";
    private static readonly string TARGET_SHORT_ATTRIBUTE = "Dictionaryable";
    private const string FLAVOR_START = "Flavor";
    private const string PROP_CONVENSION_START = "CaseConvention";
    private readonly static Regex FLAVOR = new Regex(@"Flavor\s*=\s*[\w|.]*Flavor\.(.*)");
    private readonly static Regex PROP_CONVENSION = new Regex(@"CaseConvention\s*=\s*[\w|.]*CaseConvention\.(.*)");

    #region Initialize

    /// <summary>
    /// Called to initialize the generator and register generation steps via callbacks
    /// on the <paramref name="context" />
    /// </summary>
    /// <param name="context">The <see cref="T:Microsoft.CodeAnalysis.IncrementalGeneratorInitializationContext" /> to register callbacks on</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {

        #region var classDeclarations = ...

#pragma warning disable CS8619
        IncrementalValuesProvider<GenerationInput> classDeclarations =
                context.SyntaxProvider
                    .CreateSyntaxProvider(
                        predicate: static (s, _) => ShouldTriggerGeneration(s),
                        transform: static (ctx, _) => ToGenerationInput(ctx))
                    .Where(static m => m is not null);
#pragma warning restore CS8619

        #endregion // var classDeclarations = ...

        #region ShouldTriggerGeneration

        /// <summary>
        /// Indicate whether the node should trigger a source generation />
        /// </summary>
        static bool ShouldTriggerGeneration(SyntaxNode node)
        {
            if (!(node is TypeDeclarationSyntax t)) return false;

            bool hasAttributes = t.AttributeLists.Any(m => m.Attributes.Any(m1 =>
                    AttributePredicate(m1.Name.ToString())));

            return hasAttributes;
        };

        #endregion // ShouldTriggerGeneration

        IncrementalValueProvider<(Compilation, ImmutableArray<GenerationInput>)> compilationAndClasses
            = context.CompilationProvider.Combine(classDeclarations.Collect());

        // register a code generator for the triggers
        context.RegisterSourceOutput(compilationAndClasses, Generate);
    }

    #endregion // Initialize

    #region Generate

    /// <summary>
    /// Source generates loop.
    /// </summary>
    /// <param name="spc">The SPC.</param>
    /// <param name="source">The source.</param>
    private static void Generate(
        SourceProductionContext spc,
        (Compilation compilation,
        ImmutableArray<GenerationInput> items) source)
    {
        var (compilation, items) = source;
        foreach (GenerationInput item in items)
        {
            GenerateMapper(spc, compilation, item);
        }
    }

    #endregion // Generate

    #region GenerateMapper

    /// <summary>
    /// Generates a mapper.
    /// </summary>
    /// <param name="spc">The SPC.</param>
    /// <param name="compilation">The compilation.</param>
    /// <param name="item">The item.</param>
    internal static void GenerateMapper(
        SourceProductionContext spc,
        Compilation compilation,
        GenerationInput item,
        Func<string, bool>? predicate = null)
    {
        INamedTypeSymbol symbol = item.Symbol;

        //symbol.BaseType
        TypeDeclarationSyntax syntax = item.Syntax;
        var cls = syntax.Identifier.Text;

        var hierarchy = new List<INamedTypeSymbol> { symbol };
        var s = symbol.BaseType;
        while (s != null && s.Name != "Object")
        {
            hierarchy.Add(s);
            s = s.BaseType;
        }

        var prd = predicate ?? AttributePredicate;
        var args = syntax.AttributeLists.Where(m => m.Attributes.Any(m1 =>
                                                        prd(m1.Name.ToString())))
                                        .Single()
                                        .Attributes.Single(m => prd(m.Name.ToString())).ArgumentList?.Arguments;
        var flavor = args?.Select(m => m.ToString())
                .FirstOrDefault(m => m.StartsWith(FLAVOR_START))
                ?.Trim() ?? "Default";
        flavor = FLAVOR.Replace(flavor, "$1");
        var propConvention = args?.Select(m => m.ToString())
                .FirstOrDefault(m => m.StartsWith(PROP_CONVENSION_START))
                ?.Trim() ?? "None";
        propConvention = PROP_CONVENSION.Replace(propConvention, "$1");

        SyntaxKind kind = syntax.Kind();
        string typeKind = kind switch
        {
            SyntaxKind.RecordDeclaration => "record",
            SyntaxKind.RecordStructDeclaration => "record struct",
            SyntaxKind.StructDeclaration => "struct",
            SyntaxKind.ClassDeclaration => "class",
            _ => throw new Exception($"Illegal Type [{kind}]")
        };
        string? nsCandidate = symbol.ContainingNamespace.ToString();
        string ns = nsCandidate != null ? $"namespace {nsCandidate};{Environment.NewLine}" : "";

        IPropertySymbol?[] props = hierarchy.SelectMany(s => s.GetMembers().Select(m => m as IPropertySymbol).Where(m => m != null)).ToArray();
        ImmutableArray<IParameterSymbol> parameters = symbol.Constructors
            .Where(m => !(m.Parameters.Length == 1 && m.Parameters[0].Type.Name == cls))
            .Aggregate((acc, c) =>
            {
                int cl = c.Parameters.Length;
                int accl = acc.Parameters.Length;
                if (cl > accl)
                    return c;
                return acc;
            })?.Parameters ?? ImmutableArray<IParameterSymbol>.Empty;


        StringBuilder sbMapper = new();

        StringBuilder sb = new();
        sb.AppendLine(@$"
[System.CodeDom.Compiler.GeneratedCode(""Weknow.Mapping.Generation"", ""1.0.0"")]
partial {typeKind} {cls}: IDictionaryable
{{{ConvertNeo4jDate(flavor)}
        /// <summary>
        /// Performs an implicit conversion.
        /// </summary>
        /// <param name=""source"">The source</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator {cls}(Dictionary<string, object> @source) => FromDictionary(@source);
        /// <summary>
        /// Performs an implicit conversion/>.
        /// </summary>
        /// <param name=""source"">The source</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator {cls}(ImmutableDictionary<string, object> @source) => FromImmutableDictionary(@source);
 
        /// <summary>
        /// Converts source dictionary.
        /// </summary>
        /// <param name=""source"">source of the data.</param>
        /// <returns></returns>
        public static {cls} FromDictionary(IDictionary<string, object> @source)
        {{
            {cls} result = new {cls}({string.Join($",{Environment.NewLine}\t\t\t\t",
            parameters
                   .Select(p => FormatParameter(flavor, p, propConvention)))})
            {{
{string.Join($",{Environment.NewLine}",
            props.Where(m => m.DeclaredAccessibility == Accessibility.Public && m.SetMethod != null && !parameters.Any(p => p.Name == m.Name))
                   .Select(p => FormatProperty(flavor, p, propConvention)))}
            }};
            return result;
        }}

        /// <summary>
        /// Converts source dictionary.
        /// </summary>
        /// <param name=""source"">source of the data.</param>
        /// <returns></returns>
        public static {cls} FromReadOnlyDictionary(IReadOnlyDictionary<string, object> @source)
        {{
            {cls} result = new {cls}({string.Join($",{Environment.NewLine}\t\t\t\t",
            parameters
                   .Select(p => FormatParameter(flavor, p, propConvention)))})
            {{
{string.Join($",{Environment.NewLine}",
            props.Where(m => m.DeclaredAccessibility == Accessibility.Public && m.SetMethod != null && !parameters.Any(p => p.Name == m.Name))
                   .Select(p => FormatProperty(flavor, p, propConvention)))}
            }};
            return result;
        }}

        /// <summary>
        /// Converts source immutable dictionary.
        /// </summary>
        /// <param name=""source"">source of the data.</param>
        /// <returns></returns>
        public static {cls} FromImmutableDictionary(ImmutableDictionary<string, object> @source)
        {{
            {cls} result = new {cls}({string.Join($",{Environment.NewLine}\t\t\t\t",
            parameters
                   .Select(p => FormatParameter(flavor, p, propConvention)))})
            {{
{string.Join($",{Environment.NewLine}",
            props.Where(m => m.DeclaredAccessibility == Accessibility.Public && m.SetMethod != null && !parameters.Any(p => p.Name == m.Name))
                   .Select(p => FormatProperty(flavor, p, propConvention)))}
            }};
            return result;
        }}

        /// <summary>
        /// Converts to dictionary.
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, object?> ToDictionary()
        {{
            var result = new Dictionary<string, object?>();
{string.Join(Environment.NewLine,
            props.Where(m => m.DeclaredAccessibility == Accessibility.Public)
                   .Select(m =>
                   {
                       bool isEnum = m.Type.IsEnum();
                       string suffix = isEnum ? ".ToString()" : string.Empty;
                       string name = GetPropNameOrAlias(m, propConvention);
                       if (m.NullableAnnotation == NullableAnnotation.Annotated)
                           return $@"            if(this.{m.Name} != null) 
                result.Add(""{name}"", this.{m.Name}{suffix});";
                       return $@"            result.Add(""{name}"", this.{m.Name}{suffix});";
                   }))}
            return result;
        }}

        /// <summary>
        /// Converts to immutable dictionary.
        /// </summary>
        /// <returns></returns>
        public ImmutableDictionary<string, object?> ToImmutableDictionary()
        {{
            var result = ImmutableDictionary<string, object?>.Empty;
{string.Join(Environment.NewLine,
            props.Where(m => m.DeclaredAccessibility == Accessibility.Public)
                   .Select(m =>
                   {
                       bool isEnum = m.Type.IsEnum();
                       string suffix = isEnum ? ".ToString()" : string.Empty;
                       string name = GetPropNameOrAlias(m, propConvention);
                       if (m.NullableAnnotation == NullableAnnotation.Annotated)
                           return $@"            if(this.{m.Name} != null) 
                result = result.Add(""{name}"", this.{m.Name}{suffix});";
                       return $@"            result = result.Add(""{name}"", this.{m.Name}{suffix});";
                   }))}
            return result;
        }}
}}
");
        StringBuilder parents = new();
        SyntaxNode? parent = syntax.Parent;
        while (parent is ClassDeclarationSyntax pcls)
        {
            parents.Insert(0, $"{pcls.Identifier.Text}.");
            sb.Replace(Environment.NewLine, $"{Environment.NewLine}\t");
            sb.Insert(0, "\t");
            sb.Insert(0, @$"
partial class {pcls.Identifier.Text}
{{");
            sb.AppendLine(@"}
");
            parent = parent?.Parent;
        }

        string additionalUsing = flavor switch
        {
            "Neo4j" => $"{Environment.NewLine}using Neo4j.Driver;",
            _ => string.Empty
        };
        sb.Insert(0,
            @$"using System.Collections.Immutable;
using Weknow.Mapping;{additionalUsing}
{ns}
");
        sb.Insert(0, @$"// Generated at {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}
// Flavor = {flavor}
");
        spc.AddSource($"{parents}{cls}.Mapper.cs", sb.ToString());
    }

    #endregion // GenerateMapper

    #region FormatSymbol

    private static string FormatSymbol(string compatibility, string displayType, string name, string? defaultValue, bool isEnum, string propConvention)
    {
        return compatibility switch
        {
            "Neo4j" => FormatSymbolNeo4j(displayType, name, defaultValue, isEnum, propConvention),
            _ => FormatSymbolDefault(displayType, name, defaultValue, propConvention)
        };
    }
    private static string FormatSymbolNeo4j(string displayType, string name, string? defaultValue, bool isEnum, string propConvention)
    {
        string entryName = name.ToPropNameConvention(propConvention);
        string getter = @$"@source[""{entryName}""]";
        string convert = @$"{getter}.As<{displayType}>()";
        if (isEnum)
        { 
            convert = $"Enum.TryParse<{displayType}>((string)({getter}), true, out var {name}Enum) ? {name}Enum : {convert}";
        }
        else if (displayType == "System.TimeSpan")
        {
            convert = $"{getter}.GetType() == typeof(Neo4j.Driver.LocalTime) ? {convert} : ConvertToTimeSpan({getter}.As<Neo4j.Driver.OffsetTime>())";
        }
        if (defaultValue == null)
        {
            return convert;
        }

        return @$"@source.ContainsKey(""{entryName}"") && @source[""{entryName}""] != null 
                        ? {convert}
                        : {defaultValue}";
    }

    private static string FormatSymbolDefault(string displayType, string name, string? defaultValue, string propConvention)
    {
        string? convertTo = displayType switch
        {
            "float" => "ToSingle",
            "float?" => "ToSingle",

            "double" => "ToDouble",
            "double?" => "ToDouble",

            "ushort" => "ToUInt16",
            "ushort?" => "ToUInt16",

            "short" => "ToUInt16",
            "short?" => "ToUInt16",

            "int" => "ToInt32",
            "int?" => "ToInt32",

            "uint" => "ToUInt32",
            "uint?" => "ToUInt32",

            "ulong" => "ToUInt64",
            "ulong?" => "ToUInt64",

            "long" => "ToInt64",
            "long?" => "ToInt64",

            "sbyte" => "ToSByte",
            "sbyte?" => "ToSByte",

            "bool" => "ToBoolean",
            "bool?" => "ToBoolean",

            "DateTime" => "ToDateTime",
            "DateTime?" => "ToDateTime",

            "char" => "ToChar",
            "char?" => "ToChar",

            "byte" => "ToByte",
            "byte?" => "ToByte",

            _ => null
        };

        string entryName = name.ToPropNameConvention(propConvention);

        if (convertTo == null)
        {
            string convert = @$"({displayType})@source[""{entryName}""]";
            if (defaultValue == null)
            {
                return convert;
            }

            return @$"@source.ContainsKey(""{entryName}"") && @source[""{entryName}""] != null 
                            ? {convert}
                            : {defaultValue}";
        }

        if (defaultValue == null)
        {
            return @$"Convert.{convertTo}(@source[""{entryName}""])";
        }

        return @$"@source.ContainsKey(""{entryName}"") && @source[""{entryName}""] != null 
                        ? Convert.{convertTo}(@source[""{entryName}""])
                        : {defaultValue}";
    }

    #endregion // FormatSymbol

    #region FormatParameter(IParameterSymbol p)

    private static string FormatParameter(string compatibility, IParameterSymbol p, string propConvention)
    {
        string? defaultValue = p.HasExplicitDefaultValue ? p.ExplicitDefaultValue?.ToString() : null;
        string displayType = p.Type.ToDisplayString();
        bool isNullable = p.NullableAnnotation == NullableAnnotation.Annotated;
        defaultValue = defaultValue ?? (isNullable ? $"default({displayType})" : null);
        bool isEnum = p.Type.IsEnum();
        string name = p.Name; 

        string result = FormatSymbol(compatibility, displayType, name, defaultValue, isEnum, propConvention);
        return result;
    }

    #endregion // FormatParameter(IParameterSymbol p)

    #region FormatProperty(IPropertySymbol p)

    private static string FormatProperty(string compatibility, IPropertySymbol? p, string propConvention)
    {
        string displayType = p.Type.ToDisplayString();
        bool isNullable = p.NullableAnnotation == NullableAnnotation.Annotated;
        string? defaultValue = isNullable ? $"default({displayType})" : null;
        bool isEnum = p.Type.IsEnum();

        string name = GetPropNameOrAlias(p, propConvention);
        string result = FormatSymbol(compatibility, displayType, name, defaultValue, isEnum, propConvention);
        string propName = p.Name;
        return $"\t\t\t\t{propName} = {result}";
    }

    #endregion // FormatProperty(IPropertySymbol p)

    #region ToGenerationInput

    /// <summary>
    /// Converts to generation-input.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns></returns>
    private static GenerationInput ToGenerationInput(GeneratorSyntaxContext context)
    {
        var declarationSyntax = (TypeDeclarationSyntax)context.Node;

        var symbol = context.SemanticModel.GetDeclaredSymbol(declarationSyntax);
        if (symbol == null) throw new NullReferenceException($"Code generated symbol of {nameof(declarationSyntax)} is missing");
        return new GenerationInput(declarationSyntax, symbol as INamedTypeSymbol);
    }

    #endregion // ToGenerationInput

    #region Validation


    /// <summary>
    /// The predicate whether match to the target attribute
    /// </summary>
    private static bool AttributePredicate(string candidate)
    {
        int len = candidate.LastIndexOf(".");
        if (len != -1)
            candidate = candidate.Substring(len + 1);

        return candidate == TARGET_ATTRIBUTE ||
               candidate == TARGET_SHORT_ATTRIBUTE;
    }

    #endregion // Validation    AttributePredicate

    #region SymVisitor

    /// <summary>
    /// Grab properties & constructor parameters out of a declaration
    /// </summary>
    private class SymVisitor : SymbolVisitor
    {
        public ImmutableList<IPropertySymbol> Properties { get; private set; } = ImmutableList<IPropertySymbol>.Empty;
        public ImmutableList<IParameterSymbol> Ctor { get; private set; } = ImmutableList<IParameterSymbol>.Empty;


        public override void VisitNamedType(INamedTypeSymbol symbol)
        {

            var cs = symbol.GetMembers().Where(m => m.Kind == SymbolKind.Method && m.Name == ".ctor")
                .ToArray();
            int count = 0;
            foreach (IMethodSymbol m in cs)
            {
                int c = m.Parameters.Length;
                if (count >= c) continue;
                count = c;

                foreach (IParameterSymbol p in m.Parameters)
                {
                    Ctor = Ctor.Add(p);
                }
            }

            var ps = symbol.GetMembers().Select(m => m as IPropertySymbol).Where(m => m != null);
            foreach (IPropertySymbol m in ps ?? Array.Empty<IPropertySymbol>())
            {
                Properties = Properties.Add(m);
            }

            base.VisitNamedType(symbol);
        }
    }

    #endregion // SymVisitor

    #region TryGetJsonProperty

    private static string GetPropNameOrAlias(IPropertySymbol p, string propConvention)
    {
        var atts = p.GetAttributes();
        string name = p.ToPropNameConvention(propConvention); ;
        TryGetJsonProperty(atts, ref name);
        return name;
    }

    private static bool TryGetJsonProperty(ImmutableArray<AttributeData> atts, ref string name)
    {
        if (atts.Length == 0)
            return false;

        var att = atts.Where(m => m.AttributeClass?.Name == "JsonPropertyNameAttribute")
                      .FirstOrDefault();
        TypedConstant? arg = att.ConstructorArguments.FirstOrDefault();
        if (arg != null)
        {
            string? val = arg.Value.Value?.ToString();
            if (val != null)
            {
                name = val;
                return true;
            }
        }
        return false;
    }

    #endregion // TryGetJsonProperty

    private static string ConvertNeo4jDate(string flavor)
    {
        if (flavor != "Neo4j")
            return string.Empty;

        return @"
        /// <summary>
        /// Convert To TimeSpan.
        /// </summary>
        /// <param name=""offset"">The offset</param>
        /// <returns>
        /// </returns>
        private static TimeSpan ConvertToTimeSpan(Neo4j.Driver.OffsetTime offset) => 
                new TimeSpan(0, offset.Hour, offset.Minute, offset.Second);
        /// <summary>
        /// Convert To TimeSpan.
        /// </summary>
        /// <param name=""offset"">The offset</param>
        /// <returns>
        /// </returns>
        private static TimeSpan ConvertToTimeSpan(Neo4j.Driver.LocalTime offset) => 
                new TimeSpan(0, offset.Hour, offset.Minute, offset.Second);
";
    }
}