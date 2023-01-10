using System.Text.RegularExpressions;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Weknow.Mapping;

public static class HelperExtensions
{
    public static bool IsEnum(this ITypeSymbol type) => type.BaseType?.Name == "Enum";
}
