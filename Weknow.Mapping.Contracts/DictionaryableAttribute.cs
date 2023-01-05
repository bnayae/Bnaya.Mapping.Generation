namespace Weknow.Mapping;

/// <summary>
/// Code generation decoration of Mapping to and from dictionary
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public class DictionaryableAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the generation compatibility.
    /// </summary>
    public Flavor Flavor { get; set; } = Flavor.OpenCypher;
}
