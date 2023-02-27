using System;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Weknow.Mapping.HelperExtensions;

namespace Weknow.Mapping;

[Generator]
public class DictionaryableGenerator : IIncrementalGenerator
{
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
        }

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

    #region ConvertProp

    private static string ConvertProp(
        IPropertySymbol p, string propConvention, bool immutable)
    {
        bool isEnum = p.Type.IsEnum();
        string suffix = isEnum ? ".ToString()" : string.Empty;
        if (!TryGetJsonProperty(p, out string name))
            name = p.Name.ToPropNameConvention(propConvention);
        var sb = new StringBuilder();
        if (p.NullableAnnotation == NullableAnnotation.Annotated)
        {
            sb.AppendLine($"\t\tif(this.{p.Name} != null)");
            sb.Append("\t");
        }
        sb.Append("\t\t");
        if (immutable)
            sb.Append("result = ");
        sb.AppendLine($"result.Add(\"{name}\", this.{p.Name}{suffix});");

        return sb.ToString();
    }

    #endregion // ConvertProp

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
        var publicProps = props.Where(m => m.DeclaredAccessibility == Accessibility.Public && m.SetMethod != null).ToArray();
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
    #region Operator overloads

    /// <summary>
    /// Performs an implicit conversion.
    /// </summary>
    /// <param name=""source"">The source</param>
    /// <returns>
    /// The result of the conversion.
    /// </returns>
    public static implicit operator {cls}(Dictionary<string, object?> source) => FromDictionary(source);
    /// <summary>
    /// Performs an implicit conversion/>.
    /// </summary>
    /// <param name=""source"">The source</param>
    /// <returns>
    /// The result of the conversion.
    /// </returns>
    public static implicit operator {cls}(ImmutableDictionary<string, object?> source) => FromImmutableDictionary(source);

    #endregion // Operator overloads

    #region From Dictionary
 
    /// <summary>
    /// Converts source dictionary.
    /// </summary>
    /// <param name=""source"">source of the data.</param>
    /// <returns></returns>
    public static {cls} FromDictionary(IDictionary<string, object?> source)
    {{
        {cls} result = new {cls}({string.Join($",{NEW_LINE}\t\t\t\t",
        parameters
               .Select(p => $"Cast_{p.Name}(source.TryGetValue)"))})
        {{
{string.Join($",{NEW_LINE}",
        publicProps.Where(m => !parameters.Any(p => p.Name == m.Name))
               .Select(p => $"\t\t\t{p.Name} = Cast_{p.Name}(source.TryGetValue)"))}
        }};
        return result;
    }}

    /// <summary>
    /// Converts source dictionary.
    /// </summary>
    /// <param name=""source"">source of the data.</param>
    /// <returns></returns>
    public static {cls} FromReadOnlyDictionary(IReadOnlyDictionary<string, object?> source)
    {{
        {cls} result = new {cls}({string.Join($",{NEW_LINE}\t\t\t\t",
        parameters
               .Select(p => $"Cast_{p.Name}(source.TryGetValue)"))})
        {{
{string.Join($",{NEW_LINE}",
        publicProps.Where(m => !parameters.Any(p => p.Name == m.Name))
                   .Select(p => $"\t\t\t{p.Name} = Cast_{p.Name}(source.TryGetValue)"))}
        }};
        return result;
    }}

    /// <summary>
    /// Converts source immutable dictionary.
    /// </summary>
    /// <param name=""source"">source of the data.</param>
    /// <returns></returns>
    public static {cls} FromImmutableDictionary(IImmutableDictionary<string, object?> source)
    {{
        {cls} result = new {cls}({string.Join($",{NEW_LINE}\t\t\t\t",
    parameters
               .Select(p => $"Cast_{p.Name}(source.TryGetValue)"))})
        {{
{string.Join($",{NEW_LINE}",
        publicProps.Where(m => !parameters.Any(p => p.Name == m.Name))
               .Select(p => $"\t\t\t{p.Name} = Cast_{p.Name}(source.TryGetValue)"))}
        }};
        return result;
    }}

    #endregion // From Dictionary

    #region To Dictionary

    /// <summary>
    /// Converts to dictionary.
    /// </summary>
    /// <returns></returns>
    public Dictionary<string, object?> ToDictionary()
    {{
        var result = new Dictionary<string, object?>();
{string.Join(NEW_LINE,
            publicProps
                   .Select(m => ConvertProp(m, propConvention, false)))}
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
            publicProps
                   .Select(m => ConvertProp(m, propConvention, true)))}
            return result;
    }}

    #endregion // To Dictionary

{GetterFn(publicProps)}
{CastProperties(publicProps, flavor)}}}
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

    #region CastProperty(IPropertySymbol p)

    private static string CastProperty(
        StringBuilder builder,
        string compatibility,
        IPropertySymbol p)
    {
        ITypeSymbol type = p.Type;
        bool isList = p.IsIList();
        bool isArray = type is IArrayTypeSymbol;
        bool isEnumerable = type.Name != "String" && (isList || isArray || p.IsICollection() || type.Name == "IEnumerable");

        string name = p.Name;

        string indent = "\t";
        string indent1 = "\t\t";
        string indent2 = "\t\t\t";
        string indent3 = "\t\t\t\t";
        string displayType = type.ToDisplayString();
        string typeOfDisplay = displayType.EndsWith("?") ? displayType.Substring(0, displayType.Length - 1) : displayType;
        string? defaultValue = $"default /* ({displayType}) */";

        if (isEnumerable)
        {
            var itemType = type.GetCollectionType();
            bool isDictionaryableCollection = type.IsCollectionDictionariable();
            string typeOfItem = itemType.EndsWith("?") ? itemType.Substring(0, itemType.Length - 1) : itemType;

            // COLLECTION

            builder.AppendLine($"{indent}#region Cast_{name}");
            builder.AppendLine();
            builder.AppendLine($"{indent}private static {displayType} Cast_{name}(TryGetValue tryGet)");
            builder.AppendLine($"{indent}{{");
            builder.AppendLine($"{indent1}if (TryGetValueOf_{name}(tryGet, out var val))");
            builder.AppendLine($"{indent1}{{");

            if (type.Name != "IEnumerable")
            {
                builder.AppendLine($"{indent2}if(val is {typeOfDisplay} same)");
                builder.AppendLine($"{indent2}{{");
                builder.AppendLine($"{indent2}\treturn same;");
                builder.AppendLine($"{indent2}}}");
            }
            builder.AppendLine($"{indent2}IEnumerable<{itemType}> candidate = val switch");
            builder.AppendLine($"{indent2}{{");
            if (isDictionaryableCollection)
            {
                builder.AppendLine($"{indent2}\tIEnumerable<IDictionary<string, object?>> v => v");
                builder.AppendLine($"{indent2}\t\t\t.Select(v => {typeOfItem}.FromDictionary(v)),");
            }
            builder.AppendLine($"{indent2}\tIEnumerable<{itemType}> v => v,");
            builder.AppendLine($"{indent2}\tIEnumerable<object?> v => v.Where(m => m != null)");
            builder.AppendLine($"{indent2}\t\t\t.Select(Cast_{name}_Item),");
            builder.AppendLine($"{indent2}\t_ => throw new NotSupportedException()");
            builder.AppendLine($"{indent2}}};");
            if (isArray)
                builder.AppendLine($"{indent2}return candidate.ToArray();");
            else if (isList)
                builder.AppendLine($"{indent2}return candidate.ToList();");
            else if (type.Name == "IEnumerable")
            {
                builder.AppendLine($"{indent2}foreach (var x in candidate)");
                builder.AppendLine($"{indent2}{{");
                builder.AppendLine($"{indent3}yield return x;");
                builder.AppendLine($"{indent2}}}");
            }
            else
                builder.AppendLine($"{indent2}return new {displayType}(candidate);");

            builder.AppendLine($"{indent1}}}");
            builder.AppendLine($"{indent1}else");
            builder.AppendLine($"{indent1}{{");
            if (isArray)
                builder.AppendLine($"{indent2}return Array.Empty<{itemType}>();");
            else if (isList)
                builder.AppendLine($"{indent2}return new List<{itemType}>();");
            else if (type.Name == "IEnumerable")
                builder.AppendLine($"{indent2}yield break;");
            else
                builder.AppendLine($"{indent2}return new {displayType}();");
            builder.AppendLine($"{indent1}}}");

            builder.AppendLine($"{indent}}}");
            builder.AppendLine();
            builder.AppendLine($"{indent}#endregion // Cast_{name}");
            builder.AppendLine();


            // COLLECTION ITEM

            builder.AppendLine($"{indent}#region Cast_{name}_Item");
            builder.AppendLine();

            builder.AppendLine($"{indent}private static {itemType} Cast_{name}_Item(object? item)");
            builder.AppendLine($"{indent}{{");
            if (isDictionaryableCollection)
            {
                builder.AppendLine($"{indent1}if (item is IDictionary<string, object?> d)");
                builder.AppendLine($"{indent1}{{");
                builder.AppendLine($"{indent2}var val = {typeOfItem}.FromDictionary(d);");
                builder.AppendLine($"{indent2}return val;");
                builder.AppendLine($"{indent1}}}");
            }
            switch (itemType)
            {
                case "Neo4j":
                    FormatSymbolNeo4JResult(builder, itemType, "item", indent1);
                    break;
                default:
                    string? convertTo = ConvertTo(itemType);
                    if (convertTo == null)
                    {
                        builder.AppendLine($"{indent1}return ({itemType})item;");
                    }
                    else
                    {
                        builder.AppendLine(@$"{indent1}return Convert.{convertTo}(item);");
                    }
                    break;
            }
            builder.AppendLine($"{indent}}}");
            builder.AppendLine();
            builder.AppendLine($"{indent}#endregion // Cast_{name}_Item");
        }
        else
        {
            builder.AppendLine($"{indent}#region Cast_{name}");
            builder.AppendLine();

            builder.AppendLine($"{indent}private static {displayType} Cast_{name}(TryGetValue tryGet)");
            builder.AppendLine($"{indent}{{");


            bool isDictionaryable = type.IsDictionariable();
            if (isDictionaryable)
            {
                builder.AppendLine($"{indent1}if(TryGetValueOf_{name}(tryGet, out var value) && value is IDictionary<string, object?> d)");
                builder.AppendLine($"{indent1}{{");
                builder.AppendLine($"{indent2}var val = {type.Name}.FromDictionary(d);");
                builder.AppendLine($"{indent2}return val;");
                builder.AppendLine($"{indent1}}}");
            }

            FormatSymbol(builder, compatibility, displayType, name, defaultValue, indent1);
            builder.AppendLine($"{indent}}}");
            builder.AppendLine();
            builder.AppendLine($"{indent}#endregion // Cast_{name}");

        }

        return builder.ToString();
    }

    #endregion // CastProperty(IPropertySymbol p)

    #region CastProperties

    private static string CastProperties(
                    IEnumerable<IPropertySymbol> props,
                    string propConvention)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"\t#region Cast_...");
        builder.AppendLine();
        foreach (var p in props.Where(m => m.DeclaredAccessibility == Accessibility.Public))
        {
            CastProperty(builder, propConvention, p);
            builder.AppendLine();
        }
        builder.AppendLine();
        builder.AppendLine($"\t#endregion // Cast_...");
        return builder.ToString();
    }

    #endregion // CastProperties

    #region GetterFn

    private static string GetterFn(
                    IEnumerable<IPropertySymbol> props)
    {
        ConcurrentDictionary<string, object?> getterFnExists = new ConcurrentDictionary<string, object?>();

        var builder = new StringBuilder();
        builder.AppendLine($"\t#region GetValueOf_...");
        builder.AppendLine();
        foreach (var item in props)
        {
            string gen = GetterFn(getterFnExists, item);
            if (!string.IsNullOrEmpty(gen))
                builder.AppendLine(gen);
        }
        builder.AppendLine();
        builder.AppendLine($"#\tendregion // GetValueOf_...");
        return builder.ToString();
    }

    private static string GetterFn(
                    ConcurrentDictionary<string, object?> getterFnExists,
                    IParameterSymbol p)
    {
        string? defaultValue = p.HasExplicitDefaultValue ? p.ExplicitDefaultValue?.ToString() : null;
        string displayType = p.Type.ToDisplayString();
        bool isEnum = p.Type.IsEnum();
        //bool isNullable = p.NullableAnnotation == NullableAnnotation.Annotated;
        string defaultValue1 = defaultValue ?? $"default /* ({displayType}) */";
        string name = p.Name;
        return GetterFn(getterFnExists, name, defaultValue1, isEnum, displayType);
    }

    private static string GetterFn(
                    ConcurrentDictionary<string, object?> getterFnExists,
                    IPropertySymbol p)
    {
        string displayType = p.Type.ToDisplayString();
        //bool isNullable = p.NullableAnnotation == NullableAnnotation.Annotated;
        string defaultValue = $"default /* ({displayType}) */";
        bool isEnum = p.Type.IsEnum();
        string name = p.Name;
        TryGetJsonProperty(p, out string jsonName);
        return GetterFn(getterFnExists, name, defaultValue, isEnum, displayType, jsonName);
    }


    private static string GetterFn(
        ConcurrentDictionary<string, object?> getterFnExists,
        string name, string defaultValue, bool isEnum, string displayType, string? jsonName = null)
    {
        if (!getterFnExists.TryAdd(name, null))
            return string.Empty;

        var builder = new StringBuilder();
        var set = new ConcurrentDictionary<string, int>();
        if (jsonName != null)
            set.TryAdd(jsonName, 0);
        set.TryAdd(name, 1);
        set.TryAdd(name.ToCamelCase(), 2);
        set.TryAdd(name.ToDash(), 3);
        set.TryAdd(name.ToPascalCase(), 4);
        set.TryAdd(name.ToLower(), 5);
        set.TryAdd(name.ToSCREAMING(), 6);
        string indent = "\t";
        builder.AppendLine($"{indent}#region GetValueOf_{name}");
        builder.AppendLine();
        builder.AppendLine($"{indent}private static object? GetValueOf_{name}(TryGetValue tryGet)");
        builder.AppendLine($"{indent}{{");
        builder.AppendLine($"{indent}\tif(TryGetValueOf_{name}(tryGet, out var value))");
        builder.AppendLine($"{indent}\t\treturn value;");
        builder.AppendLine($"{indent}\treturn {defaultValue};");
        builder.AppendLine($"{indent}}}");
        builder.AppendLine($"{indent}private static bool TryGetValueOf_{name}(TryGetValue tryGet, out object? value)");
        builder.AppendLine($"{indent}{{");
        builder.AppendLine($"{indent}\tvalue = {defaultValue};");
        if (isEnum)
            builder.AppendLine($"{indent}\t{displayType} e = {defaultValue};");
        foreach (var k in set
                              .Select(p => p.Key)
                              .Where(m => !string.IsNullOrEmpty(m))
                              .OrderBy(m => m))
        {
            builder.AppendLine($@"{indent}    if(tryGet(""{k}"", out value))");
            builder.AppendLine($@"{indent}    {{");
            if (isEnum)
            {
                builder.AppendLine($@"{indent}        Enum.TryParse<{displayType}>((string)(value), true, out e);");
                builder.AppendLine($@"{indent}        value = e;");
            }
            builder.AppendLine($@"{indent}        return true;");
            builder.AppendLine($@"{indent}    }}");
        }
        builder.AppendLine($"{indent}\treturn false;");
        builder.AppendLine($"{indent}}}");
        builder.AppendLine();
        builder.AppendLine($"{indent}#endregion // GetValueOf_{name}");
        return builder.ToString();
    }

    #endregion // GetterFn

    #region FormatSymbol

    private static void FormatSymbol(
                            StringBuilder builder,
                            string compatibility,
                            string displayType,
                            string name,
                            string? defaultValue,
                            string indent)
    {
        if (compatibility == "Neo4j")
            FormatSymbolNeo4j(builder, displayType, name, defaultValue, indent);
        else
            FormatSymbolDefault(builder, displayType, name, defaultValue, indent);
    }

    private static void FormatSymbolNeo4j(
                            StringBuilder builder,
                            string displayType,
                            string name,
                            string? defaultValue,
                            string indent)
    {
        string indent1 = $"{indent}\t";
        string indent2 = $"{indent1}\t";

        //builder.AppendLine($"{indent}{displayType} result;");
        if (defaultValue == null)
        {
            builder.AppendLine($"{indent}var __{name}__ = GetValueOf_{name}(tryGet);");
            FormatSymbolNeo4JResult(builder, displayType, $"__{name}__", indent);
        }
        else
        {
            builder.AppendLine($"{indent}if(TryGetValueOf_{name}(tryGet, out var __{name}__))");
            builder.AppendLine($"{indent}{{");
            FormatSymbolNeo4JResult(builder, displayType, $"__{name}__", indent1);
            builder.AppendLine($"{indent}}}");
            builder.AppendLine($"{indent}return {defaultValue};");
        }
    }

    private static void FormatSymbolNeo4JResult(
        StringBuilder builder,
        string displayType,
        string propName,
        string indent)
    {
        string indent1 = $"{indent}\t";
        if (displayType == "System.TimeSpan")
        {
            builder.AppendLine($"{indent}if({propName}?.GetType() == typeof(Neo4j.Driver.LocalTime))");
            builder.AppendLine($"{indent1}return {propName}.As<{displayType}>();");
            builder.AppendLine($"{indent}else");
            builder.AppendLine($"{indent1}return ConvertToTimeSpan({propName}.As<Neo4j.Driver.OffsetTime>()).As<{displayType}>();");
        }
        else
        {
            builder.AppendLine(@$"{indent}return {propName}.As<{displayType}>();");
        }
    }

    private static void FormatSymbolDefault(
                            StringBuilder builder,
                            string displayType,
                            string name,
                            string? defaultValue,
                            string indent)
    {
        string? convertTo = ConvertTo(displayType);

        if (defaultValue == string.Empty)
            defaultValue = "string.Empty";
        if (defaultValue == null)
            defaultValue = $"null /* default({displayType}) */";

        string indent1 = $"{indent}\t";

        if (defaultValue == null)
        {
            builder.AppendLine($"{indent}var __{name}__ = GetValueOf_{name}(tryGet);");
            if (convertTo == null)
            {
                builder.AppendLine($"{indent}return ({displayType})__{name}__;");
            }
            else
            {
                builder.AppendLine(@$"{indent}return Convert.{convertTo}(__{name}__)"); ;
            }
        }
        else
        {
            builder.AppendLine($"{indent}if(TryGetValueOf_{name}(tryGet, out var __{name}__))");
            builder.AppendLine($"{indent}{{");
            if (convertTo == null)
            {
                builder.AppendLine($"{indent1}return ({displayType})__{name}__;");
            }
            else
            {
                builder.AppendLine(@$"{indent1}return Convert.{convertTo}(__{name}__);"); ;
            }
            builder.AppendLine($"{indent}}}");
            builder.AppendLine($"{indent}return {defaultValue};");
        }
    }

    #endregion // FormatSymbol

    #region ConvertTo

    private static string? ConvertTo(string displayType)
    {
        #region string? convertTo = displayType switch {...}

        return displayType switch
        {
            "float" => nameof(Convert.ToSingle),
            "float?" => nameof(Convert.ToSingle),
            nameof(Single) => nameof(Convert.ToSingle),
            $"{nameof(Single)}?" => nameof(Convert.ToSingle),

            "double" => nameof(Convert.ToDouble),
            "double?" => nameof(Convert.ToDouble),
            nameof(Double) => nameof(Convert.ToDouble),
            $"{nameof(Double)}?" => nameof(Convert.ToDouble),

            "ushort" => nameof(Convert.ToUInt16),
            "ushort?" => nameof(Convert.ToUInt16),
            nameof(UInt16) => nameof(Convert.ToUInt16),
            $"{nameof(UInt16)}?" => nameof(Convert.ToUInt16),

            "short" => nameof(Convert.ToInt16),
            "short?" => nameof(Convert.ToInt16),
            nameof(Int16) => nameof(Convert.ToInt16),
            $"{nameof(Int16)}?" => nameof(Convert.ToInt16),

            "int" => nameof(Convert.ToInt32),
            "int?" => nameof(Convert.ToInt32),
            nameof(Int32) => nameof(Convert.ToInt32),
            $"{nameof(Int32)}?" => nameof(Convert.ToInt32),

            "uint" => nameof(Convert.ToUInt32),
            "uint?" => nameof(Convert.ToUInt32),
            nameof(UInt32) => nameof(Convert.ToUInt32),
            $"{nameof(UInt32)}?" => nameof(Convert.ToUInt32),

            "ulong" => nameof(Convert.ToUInt64),
            "ulong?" => nameof(Convert.ToUInt64),
            nameof(UInt64) => nameof(Convert.ToUInt64),
            $"{nameof(UInt64)}?" => nameof(Convert.ToUInt64),

            "long" => nameof(Convert.ToInt64),
            "long?" => nameof(Convert.ToInt64),
            nameof(Int64) => nameof(Convert.ToInt64),
            $"{nameof(Int64)}?" => nameof(Convert.ToInt64),

            "sbyte" => nameof(Convert.ToSByte),
            "sbyte?" => nameof(Convert.ToSByte),
            nameof(SByte) => nameof(Convert.ToSByte),
            $"{nameof(SByte)}?" => nameof(Convert.ToSByte),

            "bool" => nameof(Convert.ToBoolean),
            "bool?" => nameof(Convert.ToBoolean),
            nameof(Boolean) => nameof(Convert.ToBoolean),
            $"{nameof(Boolean)}?" => nameof(Convert.ToBoolean),

            "DateTime" => nameof(Convert.ToDateTime),
            "DateTime?" => nameof(Convert.ToDateTime),

            "char" => nameof(Convert.ToChar),
            "char?" => nameof(Convert.ToChar),
            nameof(Char) => nameof(Convert.ToChar),
            $"{nameof(Char)}?" => nameof(Convert.ToChar),

            "byte" => nameof(Convert.ToByte),
            "byte?" => nameof(Convert.ToByte),
            nameof(Byte) => nameof(Convert.ToByte),
            $"{nameof(Byte)}?" => nameof(Convert.ToByte),

            _ => null
        };

        #endregion // string? convertTo = displayType switch {...}
    }

    #endregion // ConvertTo

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
        if (TryGetJsonProperty(atts, out string name))
            return name;
        return p.Name;
    }

    private static bool TryGetJsonProperty(IPropertySymbol p, out string name)
    {
        var atts = p.GetAttributes();
        return TryGetJsonProperty(atts, out name);
    }

    private static bool TryGetJsonProperty(ImmutableArray<AttributeData> atts, out string name)
    {
        name = string.Empty;
        if (atts.Length == 0)
            return false;

        var att = atts.Where(m => m.AttributeClass?.Name == "JsonPropertyNameAttribute")
                      .FirstOrDefault();
        TypedConstant? arg = att.ConstructorArguments.FirstOrDefault();
        if (arg == null)
            return false;

        string? val = arg.Value.Value?.ToString();
        if (val != null)
        {
            name = val;
            return true;
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
    #region ConvertToTimeSpan

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

    #endregion // ConvertToTimeSpan
";
    }

    #endregion // ConvertNeo4jDate
}