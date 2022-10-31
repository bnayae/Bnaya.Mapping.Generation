using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Weknow.Mapping;

// TODO: [bnaya 2022-10-24] Add ctor level attribue to select the From ctor
// TODO: [bnaya 2022-10-24] Add conventions (camel / Pascal)

[Generator]
public class DictionaryableGenerator : IIncrementalGenerator
{
    private const string TARGET_ATTRIBUTE = nameof(DictionaryableAttribute);
    private static readonly string TARGET_SHORT_ATTRIBUTE = nameof(DictionaryableAttribute).Replace("Attribute", "");

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
        var symbol = item.Symbol;
        TypeDeclarationSyntax syntax = item.Syntax;

        var prd = predicate ?? AttributePredicate;
        var args = syntax.AttributeLists.Where(m => m.Attributes.Any(m1 =>
                                                        prd(m1.Name.ToString())))
                                        .Single()
                                        .Attributes.Single(m => prd(m.Name.ToString())).ArgumentList?.Arguments;

        var cls = syntax.Identifier.Text;
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
        IPropertySymbol?[] props = symbol.GetMembers().Select(m => m as IPropertySymbol).Where(m => m != null).ToArray();
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
partial {typeKind} {cls}: IDictionaryable
{{
        public static implicit operator {cls}(Dictionary<string, object> @source) => FromDictionary(@source);
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
                   .Select(FormatParameter))})
            {{
{string.Join($",{Environment.NewLine}",
            props.Where(m => m.DeclaredAccessibility == Accessibility.Public && m.SetMethod != null && !parameters.Any(p => p.Name == m.Name))
                   .Select(FormatProperty))}
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
                   .Select(FormatParameter))})
            {{
{string.Join($",{Environment.NewLine}",
            props.Where(m => m.DeclaredAccessibility == Accessibility.Public && m.SetMethod != null && !parameters.Any(p => p.Name == m.Name))
                   .Select(FormatProperty))}
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
                   .Select(FormatParameter))})
            {{
{string.Join($",{Environment.NewLine}",
            props.Where(m => m.DeclaredAccessibility == Accessibility.Public && m.SetMethod != null && !parameters.Any(p => p.Name == m.Name))
                   .Select(FormatProperty))}
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
                   .Select(m => $@"            result.Add(""{m.Name}"", this.{m.Name});"))}
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
                   .Select(m => $@"            result = result.Add(""{m.Name}"", this.{m.Name});"))}
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
            sb.Insert(0,"\t");
            sb.Insert(0, @$"
partial class {pcls.Identifier.Text}
{{");
            sb.AppendLine(@"}
");
            parent = parent?.Parent;
        }
        sb.Insert(0,
            @$"using System.Collections.Immutable;
using Weknow.Mapping;
{ns}

[System.CodeDom.Compiler.GeneratedCode(""Weknow.Mapping.Generation"", ""1.0.0"")]");
        spc.AddSource($"{parents}{cls}.Mapper.cs", sb.ToString());
    }

    #endregion // GenerateMapper

    #region FormatSymbol

    private static string FormatSymbol(string displayType, string name, string? defaultValue)
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

        if (convertTo == null)
        {
            if (defaultValue == null)
            {
                return @$"({displayType})@source[""{name}""]";
            }

            return @$"@source.ContainsKey(""{name}"") && @source[""{name}""] != null 
                            ? ({displayType})@source[""{name}""]
                            : {defaultValue}";
        }

        if (defaultValue == null)
        {
            return @$"Convert.{convertTo}(@source[""{name}""])";
        }

        return @$"@source.ContainsKey(""{name}"") && @source[""{name}""] != null 
                        ? Convert.{convertTo}(@source[""{name}""])
                        : {defaultValue}";
    }

    #endregion // FormatSymbol

    #region FormatParameter(IParameterSymbol p)

    private static string FormatParameter(IParameterSymbol p)
    {
        string? defaultValue = p.HasExplicitDefaultValue ? p.ExplicitDefaultValue?.ToString() : null;
        string displayType = p.Type.ToDisplayString();
        bool isNullable = p.NullableAnnotation == NullableAnnotation.Annotated;
        defaultValue = defaultValue ?? (isNullable ? $"default({displayType})" : null);

        string result = FormatSymbol(displayType, p.Name, defaultValue);
        return result;
    }

    #endregion // FormatParameter(IParameterSymbol p)

    #region FormatProperty(IPropertySymbol p)

    private static string FormatProperty(IPropertySymbol? p)
    {
        string displayType = p.Type.ToDisplayString();
        bool isNullable = p.NullableAnnotation == NullableAnnotation.Annotated;
        string? defaultValue = isNullable ? $"default({displayType})" : null;


        string result = FormatSymbol(displayType, p.Name, defaultValue);
        return $"\t\t\t{p.Name} = {result}";
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
}