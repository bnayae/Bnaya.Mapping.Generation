using System.Collections.Immutable;

namespace Bnaya.Mapping
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
        /// Converts to immutable dictionary.
        /// </summary>
        /// <returns></returns>
        ImmutableDictionary<string, object?> ToImmutableDictionary();
    }

    /// <summary>
    /// <![CDATA[Cast-able to IDictionary<string, object> or ImmutableDictionary<string, object>]]>
    /// </summary>
    public interface IDictionaryable<T>
    {
        /// <summary>
        /// Create instance.
        /// </summary>
        /// <returns></returns>
        abstract static T FromDictionary(IDictionary<string, object?> data);
        /// <summary>
        /// Create instance.
        /// </summary>
        /// <returns></returns>
        abstract static T FromReadOnlyDictionary(IReadOnlyDictionary<string, object?> data);
        /// <summary>
        /// Create instance.
        /// </summary>
        /// <returns></returns>
        abstract static T FromImmutableDictionary(IImmutableDictionary<string, object?> data);
    }
}
