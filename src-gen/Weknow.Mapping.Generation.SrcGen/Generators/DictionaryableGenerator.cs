using System.CodeDom.Compiler;
using System.Collections.Concurrent;
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
    private readonly static Regex FLAVOR = new Regex(@"Flavor\s*=\s*[\w|.]*Flavor\.(.*)");
    private const string PROP_CONVENSION_START = "CaseConvention";
    private readonly static Regex PROP_CONVENSION = new Regex(@"CaseConvention\s*=\s*[\w|.]*CaseConvention\.(.*)");
    private const string NEW_LINE = "\n";

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
        string ns = nsCandidate != null ? $"namespace {nsCandidate};{NEW_LINE}" : "";

        var props = (IPropertySymbol[])(hierarchy
                            .SelectMany(s => s.GetMembers()
                                             .Select(m => m as IPropertySymbol)
                                            .Where(m => m != null && m.Name != "EqualityContract"))
                            .ToArray());
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
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8604 // Possible null reference argument.

[System.CodeDom.Compiler.GeneratedCode(""Weknow.Mapping.Generation"", ""1.0.0"")]
partial {typeKind} {cls}: IDictionaryable, IDictionaryable<{cls}>
{{
    private delegate bool TryGetValue(string key, out object? value);

{ConvertNeo4jDate(flavor)}
        /// <summary>
        /// Performs an implicit conversion.
        /// </summary>
        /// <param name=""source"">The source</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator {cls}(Dictionary<string, object?> @source) => FromDictionary(@source);
        /// <summary>
        /// Performs an implicit conversion/>.
        /// </summary>
        /// <param name=""source"">The source</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator {cls}(ImmutableDictionary<string, object?> @source) => FromImmutableDictionary(@source);
 
        /// <summary>
        /// Converts source dictionary.
        /// </summary>
        /// <param name=""source"">source of the data.</param>
        /// <returns></returns>
        public static {cls} FromDictionary(IDictionary<string, object?> @source)
        {{
            {cls} result = new {cls}({string.Join($",{NEW_LINE}\t\t\t\t",
            parameters
                   .Select(p => FormatParameter(flavor, p)))})
            {{
{string.Join($",{NEW_LINE}",
            props.Where(m => m.DeclaredAccessibility == Accessibility.Public && m.SetMethod != null && !parameters.Any(p => p.Name == m.Name))
                   .Select(p => FormatProperty(flavor, p)))}
            }};
            return result;
        }}

        /// <summary>
        /// Converts source dictionary.
        /// </summary>
        /// <param name=""source"">source of the data.</param>
        /// <returns></returns>
        public static {cls} FromReadOnlyDictionary(IReadOnlyDictionary<string, object?> @source)
        {{
            {cls} result = new {cls}({string.Join($",{NEW_LINE}\t\t\t\t",
            parameters
                   .Select(p => FormatParameter(flavor, p)))})
            {{
{string.Join($",{NEW_LINE}",
            props.Where(m => m.DeclaredAccessibility == Accessibility.Public && m.SetMethod != null && !parameters.Any(p => p.Name == m.Name))
                   .Select(p => FormatProperty(flavor, p)))}
            }};
            return result;
        }}

        /// <summary>
        /// Converts source immutable dictionary.
        /// </summary>
        /// <param name=""source"">source of the data.</param>
        /// <returns></returns>
        public static {cls} FromImmutableDictionary(IImmutableDictionary<string, object?> @source)
        {{
            {cls} result = new {cls}({string.Join($",{NEW_LINE}\t\t\t\t",
            parameters
                   .Select(p => FormatParameter(flavor, p)))})
            {{
{string.Join($",{NEW_LINE}",
            props.Where(m => m.DeclaredAccessibility == Accessibility.Public && m.SetMethod != null && !parameters.Any(p => p.Name == m.Name))
                   .Select(p => FormatProperty(flavor, p)))}
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
{string.Join(NEW_LINE,
            props.Where(m => m.DeclaredAccessibility == Accessibility.Public)
                   .Select(m =>
                   {
                       bool isEnum = m.Type.IsEnum();
                       string suffix = isEnum ? ".ToString()" : string.Empty;
                       string name = GetPropNameOrAlias(m)
                                        .ToPropNameConvention(propConvention);
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
{string.Join(NEW_LINE,
            props.Where(m => m.DeclaredAccessibility == Accessibility.Public)
                   .Select(m =>
                   {
                       bool isEnum = m.Type.IsEnum();
                       string suffix = isEnum ? ".ToString()" : string.Empty;
                       string name = m.Name.ToPropNameConvention(propConvention);
                       if (m.NullableAnnotation == NullableAnnotation.Annotated)
                           return $@"            if(this.{m.Name} != null) 
                result = result.Add(""{name}"", this.{m.Name}{suffix});";
                       return $@"            result = result.Add(""{name}"", this.{m.Name}{suffix});";
                   }))}
            return result;
        }}

        {GetterFn(props)}
}}
");
        StringBuilder parents = new();
        SyntaxNode? parent = syntax.Parent;
        while (parent is ClassDeclarationSyntax pcls)
        {
            parents.Insert(0, $"{pcls.Identifier.Text}.");
            sb.Replace(NEW_LINE, $"{NEW_LINE}\t");
            sb.Insert(0, "\t");
            sb.Insert(0, @$"
partial class {pcls.Identifier.Text}
{{
");
            sb.AppendLine(@"}
");
            parent = parent?.Parent;
        }

        string additionalUsing = flavor switch
        {
            "Neo4j" => $"{NEW_LINE}using Neo4j.Driver;",
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

    #region GetterFn

    private static string GetterFn(
                    //IEnumerable<IParameterSymbol> prms,
                    IEnumerable<IPropertySymbol> props)
    {
        ConcurrentDictionary<string, object?> getterFnExists = new ConcurrentDictionary<string, object?>();

        var sb = new StringBuilder();
        //foreach (var item in prms)
        //{
        //    string gen = GetterFn(getterFnExists, item);
        //    if(!string.IsNullOrEmpty(gen))
        //        sb.AppendLine(gen);
        //}
        foreach (var item in props)
        {
            string gen = GetterFn(getterFnExists, item);
            if(!string.IsNullOrEmpty(gen))
                sb.AppendLine(gen);
        }
        return sb.ToString();
    }

    private static string GetterFn(
                    ConcurrentDictionary<string, object?> getterFnExists, 
                    IParameterSymbol p)
    {
        string? defaultValue = p.HasExplicitDefaultValue ? p.ExplicitDefaultValue?.ToString() : null;
        string displayType = p.Type.ToDisplayString();
        bool isNullable = p.NullableAnnotation == NullableAnnotation.Annotated;
        defaultValue = defaultValue ?? (isNullable ? $"default({displayType})" : null);
        string name = p.Name;

        return GetterFn(getterFnExists, name, defaultValue);
    }
    private static string GetterFn(
                    ConcurrentDictionary<string, object?> getterFnExists,
                    IPropertySymbol p)
    {
        string displayType = p.Type.ToDisplayString();
        bool isNullable = p.NullableAnnotation == NullableAnnotation.Annotated;
        string? defaultValue = isNullable ? $"default({displayType})" : null;
        string name = p.Name;

        return GetterFn(getterFnExists, name, defaultValue);
    }


    private static string GetterFn(ConcurrentDictionary<string, object?> getterFnExists, string name, string? defaultValue)
    {
        if (!getterFnExists.TryAdd(name, null))
            return string.Empty;

        var sb = new StringBuilder();
        var set = new ConcurrentDictionary<string, int>();
        set.TryAdd(name, 1);
        set.TryAdd(name.ToCamelCase(), 2);
        set.TryAdd(name.ToDash(), 3);
        set.TryAdd(name.ToPascalCase(), 4);
        set.TryAdd(name.ToLower(), 5);
        set.TryAdd(name.ToSCREAMING(), 6);
        string indent = "\t\t";
        sb.AppendLine($"{indent}private static object? GetValueOf_{name}(TryGetValue tryGet)");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}\tif(TryGetValueOf_{name}(tryGet, out var value))");
        sb.AppendLine($"{indent}\t\treturn value;");
        sb.AppendLine($"{indent}\treturn default;");
        sb.AppendLine($"{indent}}}");
        sb.AppendLine($"{indent}private static bool TryGetValueOf_{name}(TryGetValue tryGet, out object? value)");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}\tvalue = {defaultValue ?? "null"};");
        foreach (var kv in set.OrderBy(m => m.Value))
        {
            string k = kv.Key;
            sb.AppendLine($@"{indent}    if(tryGet(""{k}"", out value))");
            sb.AppendLine($@"{indent}        return true;");
        }
        sb.AppendLine($"{indent}\treturn false;");
        sb.AppendLine($"{indent}}}");
        return sb.ToString();
    }

    #endregion // GetterFn

    #region FormatSymbol

    private static string FormatSymbol(string compatibility, string displayType, string name, string? defaultValue, bool isEnum)
    {
        var result = compatibility switch
        {
            "Neo4j" => FormatSymbolNeo4j(displayType, name, defaultValue, isEnum),
            _ => FormatSymbolDefault(displayType, name, defaultValue)
        };

        return result;
    }

    private static string FormatSymbolNeo4j(string displayType, string name, string? defaultValue, bool isEnum)
    {
        string getter = $"GetValueOf_{name}(@source.TryGetValue)";
        string tryGetter = $"TryGetValueOf_{name}(@source.TryGetValue, out var __{name}__)";
        string convert = @$"{getter}.As<{displayType}>()";
        if (isEnum)
        { 
            convert = $"Enum.TryParse<{displayType}>((string)({getter}), true, out var {name}Enum) ? {name}Enum : {convert}";
        }
        else if (displayType == "System.TimeSpan")
        {
            convert = $"{getter}?.GetType() == typeof(Neo4j.Driver.LocalTime) ? {convert} : ConvertToTimeSpan({getter}.As<Neo4j.Driver.OffsetTime>())";
        }
        if (defaultValue == null)
        {
            return convert;
        }

        if(defaultValue == string.Empty)
            defaultValue = "string.Empty";
        return @$"{tryGetter} 
                        ? __{name}__.As<{displayType}>()
                        : {defaultValue}";
    }

    private static string FormatSymbolDefault(string displayType, string name, string? defaultValue)
    {
        #region string? convertTo = displayType switch {...}

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

        #endregion // string? convertTo = displayType switch {...}

        string getter = $"GetValueOf_{name}(@source.TryGetValue)";
        string tryGetter = $"TryGetValueOf_{name}(@source.TryGetValue, out var __{name}__)";

        if (convertTo == null)
        {
            if (defaultValue == string.Empty)
                defaultValue = "string.Empty";
            if (defaultValue == null)
                defaultValue = "null";
            return @$"{tryGetter}
                            ? ({displayType})__{name}__
                            : {defaultValue}";
        }

        if (defaultValue == null)
        {
            return @$"Convert.{convertTo}({getter})";
        }

        if (defaultValue == string.Empty)
            defaultValue = "string.Empty";
        return @$"{tryGetter}
                        ? Convert.{convertTo}(__{name}__)
                        : {defaultValue}";
    }

    #endregion // FormatSymbol

    #region FormatParameter(IParameterSymbol p)

    private static string FormatParameter(string compatibility, IParameterSymbol p)
    {
        string? defaultValue = p.HasExplicitDefaultValue ? p.ExplicitDefaultValue?.ToString() : null;
        string displayType = p.Type.ToDisplayString();
        bool isNullable = p.NullableAnnotation == NullableAnnotation.Annotated;
        defaultValue = defaultValue ?? (isNullable ? $"default({displayType})" : null);
        bool isEnum = p.Type.IsEnum();
        string name = p.Name; 

        string result = FormatSymbol(compatibility, displayType, name, defaultValue, isEnum);
        return result;
    }

    #endregion // FormatParameter(IParameterSymbol p)

    #region FormatProperty(IPropertySymbol p)

    private static string FormatProperty(string compatibility, IPropertySymbol p)
    {
        string displayType = p.Type.ToDisplayString();
        bool isNullable = p.NullableAnnotation == NullableAnnotation.Annotated;
        string? defaultValue = isNullable ? $"default({displayType})" : null;
        bool isEnum = p.Type.IsEnum();

        string name = p.Name;
        string result = FormatSymbol(compatibility, displayType, name, defaultValue, isEnum);
        return $"\t\t\t\t{name} = {result}";
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

    #region AttributePredicate

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

    #endregion // AttributePredicate

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

            var ps = (IEnumerable<IPropertySymbol>)(symbol.GetMembers().Select(m => m as IPropertySymbol).Where(m => m != null));
            foreach (IPropertySymbol m in ps ?? Array.Empty<IPropertySymbol>())
            {
                Properties = Properties.Add(m);
            }

            base.VisitNamedType(symbol);
        }
    }

    #endregion // SymVisitor

    #region TryGetJsonProperty

    private static string GetPropNameOrAlias(IPropertySymbol p)
    {
        var atts = p.GetAttributes();
        string name = p.Name;
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

    #region ConvertNeo4jDate

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

    #endregion // ConvertNeo4jDate
}