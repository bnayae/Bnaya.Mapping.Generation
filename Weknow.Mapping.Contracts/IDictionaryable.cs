using System.Collections.Immutable;

namespace Weknow.Mapping
{
    /// <summary>
    /// <![CDATA[Cast-able to IDictionary<string, object> or ImmutableDictionary<string, object>]]>
    /// </summary>
    public interface IDictionaryable
    {
        /// <summary>
        /// Converts to dictionary.
        /// </summary>
        /// <returns></returns>
        Dictionary<string, object?> ToDictionary();

        /// <summary>
        /// Converts to imutable dictionary.
        /// </summary>
        /// <returns></returns>
        ImmutableDictionary<string, object?> ToImmutableDictionary();
    }
}
