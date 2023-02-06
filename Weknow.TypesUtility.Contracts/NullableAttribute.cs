namespace Weknow.TypesUtility;

/// <summary>
/// Code generation decoration of Mapping to and from dictionary
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public class NullableAttribute : Attribute
{
    /// <summary>
    /// Override the modifier (public, private, internal).
    /// </summary>
    public string? Modifier { get; set; }

    public string? Suffix { get; set; } 
}
