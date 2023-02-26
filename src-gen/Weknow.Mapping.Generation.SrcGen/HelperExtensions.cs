using System.Text.RegularExpressions;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Weknow.Mapping;

public static class HelperExtensions
{
    private static readonly Regex IS_LIST = new (@"IList\<\w*\>");

    public static bool IsEnum(this ITypeSymbol type) => type.BaseType?.Name == "Enum";
    public static bool IsNullable(this ITypeSymbol symbol) => symbol.NullableAnnotation == NullableAnnotation.Annotated;
    public static bool IsNullable(this IPropertySymbol symbol) => symbol.NullableAnnotation == NullableAnnotation.Annotated;

    public static bool IsIEnumerable(this IPropertySymbol p)
    {
        if(p.Type.Name == "IEnumerable")
            return true;
        return p.Type.AllInterfaces.Any(m => m.Name == "IEnumerable");
    }

    public static bool IsICollection(this IPropertySymbol p)
    {
        return p.Type.AllInterfaces.Any(m => m.Name == "ICollection");
    }

    public static bool IsIList(this IPropertySymbol p)
    {
        if(IS_LIST.IsMatch(p.Type.Name))
            return true;
        return p.Type.AllInterfaces.Any(m => IS_LIST.IsMatch(m.Name));
    }

    public static string GetCollectionType(this ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol arr)
            return arr.ElementType.Name;
        var parts = type.ToDisplayParts(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var args = parts
                        .SkipWhile(m => m.Kind != SymbolDisplayPartKind.Punctuation && m.Symbol?.Name != "<")
                        .Skip(1)
                        .TakeWhile(m => m.Kind != SymbolDisplayPartKind.Punctuation ||
                                    m.ToString() switch { "?" => true, "," => true, _ => false })
                        .Aggregate(new List<string>(), (acc, sym) =>
                        {
                            if (sym.Kind == SymbolDisplayPartKind.Punctuation)
                            {
                                int idx = acc.Count - 1;
                                string sign = sym.ToString();
                                if(sign == "?")
                                    acc[idx] = $"{acc[idx]}{sym}";
                            }
                            else
                            {
                                acc.Add(sym.Symbol.Name);
                            }
                            return acc;
                        }).ToArray();
        var result = args?.Length switch
        {
            1 => args[0],
            2 => $"KeyValuePair.Create({args[0]}, {args[1]})",
            null => throw new NotSupportedException($"Cannot fetch the argument of [{type.Name}]"),
            0 => throw new NotSupportedException($"Cannot fetch the argument of [{type.Name}]"),
            _ => $"({ string.Join(", ", args)})"
        }; 
        return result;
    }

    public static string ToPropNameConvention(this IPropertySymbol? p, string propConvention)
    { 
        string name = p?.Name?? throw new ArgumentNullException(nameof(IPropertySymbol));
        return name.ToPropNameConvention(propConvention);
    }

    public static string ToPropNameConvention(this IParameterSymbol? p, string propConvention)
    { 
        string name = p?.Name?? throw new ArgumentNullException(nameof(IParameterSymbol));
        return name.ToPropNameConvention(propConvention);
    }

    public static string ToPropNameConvention(this string name, string propConvention)
    { 
        if (propConvention == "camelCase")
            return name.ToCamelCase();
        if (propConvention == "PascalCase")
            return name.ToPascalCase();
        if (propConvention == "SCREAMING_CASE")
            return name.ToSCREAMING();
        if (propConvention == "dash_case")
            return name.ToDash();
        return name;
    }
}
