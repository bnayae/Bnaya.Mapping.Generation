using System.Text.RegularExpressions;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Weknow.Mapping;

public static class HelperExtensions
{
    public static bool IsEnum(this ITypeSymbol type) => type.BaseType?.Name == "Enum";
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
