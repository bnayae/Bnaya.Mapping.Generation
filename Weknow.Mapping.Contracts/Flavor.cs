namespace Weknow.Mapping;

/// <summary>
/// Generation Compatibility
/// </summary>
public enum Flavor
{
    /// <summary>
    /// use compatible open cypher
    /// https://opencypher.org/
    /// </summary>
    OpenCypher,
    /// <summary>
    /// use neo4j 5 compatible cypher
    /// https://neo4j.com/docs/cypher-cheat-sheet/current/
    /// </summary>
    Neo4j
}
