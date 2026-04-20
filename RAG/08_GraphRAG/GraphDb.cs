using Neo4j.Driver;

namespace _08_GraphRAG
{
    public class GraphDb : IAsyncDisposable
    {
        private const string VectorIndexName = "entity_embeddings";
        private const int EmbeddingDimensions = 1536;

        private readonly IDriver _driver;

        public GraphDb()
        {
            _driver = GraphDatabase.Driver(
                Environment.GetEnvironmentVariable("NEO4J_URI")!,
                AuthTokens.Basic(
                    Environment.GetEnvironmentVariable("NEO4J_USERNAME")!,
                    Environment.GetEnvironmentVariable("NEO4J_PASSWORD")!));
        }

        public async Task EnsureVectorIndexAsync()
        {
            await _driver.ExecutableQuery($$"""
                CREATE VECTOR INDEX {{VectorIndexName}} IF NOT EXISTS
                FOR (e:Entity)
                ON e.embedding
                OPTIONS {
                    indexConfig: {
                        `vector.dimensions`: {{EmbeddingDimensions}},
                        `vector.similarity_function`: 'cosine'
                    }
                }
                """)
                .ExecuteAsync();
        }

        public async Task IngestGraphAsync(KnowledgeGraph graph)
        {
            // 1. Ingest Entities
            foreach (var entity in graph.Entities)
            {
                await _driver.ExecutableQuery($@"
                    MERGE (e:{entity.Type} {{name: $name}})
                    ON CREATE SET e:Entity, e.description = $description, e.embedding = $embedding
                    ON MATCH SET e:Entity, e.description = $description, e.embedding = $embedding")
                    .WithParameters(new
                    {
                        name = entity.Name,
                        description = entity.Description,
                        embedding = entity.Embedding
                    })
                    .ExecuteAsync();
            }

            // 2. Ingest Relationships
            foreach (var rel in graph.Relationships)
            {
                await _driver.ExecutableQuery($@"
                    MATCH (s {{name: $source}}), (t {{name: $target}})
                    MERGE (s)-[r:{rel.Label.ToUpper()}]->(t)
                    ON CREATE SET r.description = $description, r.weight = $weight
                    ON MATCH  SET r.description = $description, r.weight = (r.weight + $weight) / 2.0")
                    .WithParameters(new
                    {
                        source = rel.Source,
                        target = rel.Target,
                        description = rel.Description,
                        weight = rel.Weight
                    })
                    .ExecuteAsync();
            }
        }

        public async Task PrintAllConnectionsAsync()
        {
            var result = await _driver.ExecutableQuery($"""
                MATCH (s)-[r]->(t)
                RETURN s.name AS sName, type(r) AS rName, t.name AS tName, coalesce(r.weight, 0.5) AS weight
                """)
                .ExecuteAsync();

            Console.WriteLine("\n--- CURRENT KNOWLEDGE GRAPH CONNECTIONS ---");
            foreach (var record in result.Result)
            {
                var source = record["sName"].As<string>();
                var relationship = record["rName"].As<string>();
                var target = record["tName"].As<string>();
                var weight = record["weight"].As<double>();

                Console.WriteLine($"  ({source}) --[{relationship}]--> ({target})  [weight: {weight:F2}]");
            }
            Console.WriteLine("-------------------------------------------\n");
        }

        public async Task<GraphSearchResult> GetTopEntitiesAsync(float[] queryVector, int topK = 5, int traversalDepth = 2, float minPathScore = 0.1f)
        {
            var result = await _driver.ExecutableQuery($$"""
                // Phase 1: vector ANN search — find seed nodes closest to the query
                CALL db.index.vector.queryNodes('{{VectorIndexName}}', $topK, $queryVector)
                YIELD node AS seedNode, score
                WITH collect(seedNode) AS seedNodes

                // Phase 2: optional variable-depth traversal — seed nodes are kept even when no paths qualify
                UNWIND seedNodes AS seedNode
                OPTIONAL MATCH path = (seedNode)-[rels*1..{{traversalDepth}}]-(endNode)
                WITH seedNodes,
                     collect(DISTINCT CASE
                         WHEN rels IS NOT NULL
                              AND reduce(score = 1.0, r IN rels | score * coalesce(r.weight, 0.5)) >= $minPathScore
                         THEN {nodes: nodes(path), rels: rels}
                         ELSE null
                     END) AS qualifiedPaths

                // Phase 3: flatten qualified paths into node and relationship lists
                WITH seedNodes,
                     [p IN qualifiedPaths WHERE p IS NOT NULL | p.nodes] AS allPathNodes,
                     [p IN qualifiedPaths WHERE p IS NOT NULL | p.rels]  AS allPathRels
                WITH seedNodes,
                     reduce(acc = [], ns IN allPathNodes | acc + ns) AS flatNodes,
                     reduce(acc = [], rs IN allPathRels  | acc + rs) AS flatRels

                RETURN
                    [n IN seedNodes | {Name: n.name, Type: [l IN labels(n) WHERE l <> 'Entity'][0], Description: n.description}]                                                          AS Seeds,
                    [n IN flatNodes | {Name: n.name, Type: [l IN labels(n) WHERE l <> 'Entity'][0], Description: n.description}]                                                          AS Traversed,
                    [r IN flatRels  | {Source: startNode(r).name, Target: endNode(r).name, Label: type(r), Description: coalesce(r.description, ''), Weight: coalesce(r.weight, 0.5)}]    AS Relationships
                """)
                .WithParameters(new { queryVector, topK, minPathScore })
                .ExecuteAsync();

            var row = result.Result.FirstOrDefault();
            if (row is null)
                return new GraphSearchResult
                {
                    SeedEntities = [],
                    TraversedEntities = [],
                    Relationships = [],
                    TraversalDepth = traversalDepth
                };

            return new GraphSearchResult
            {
                SeedEntities    = MapEntities(row["Seeds"]),
                TraversedEntities = MapEntities(row["Traversed"]),
                Relationships   = MapRelationships(row["Relationships"]),
                TraversalDepth  = traversalDepth
            };
        }

        private static IReadOnlyList<KnowledgeGraphEntity> MapEntities(object raw) =>
            ((IEnumerable<object>)raw)
                .Cast<IDictionary<string, object>>()
                .Select(m => new KnowledgeGraphEntity
                {
                    Name        = m["Name"]?.ToString() ?? "",
                    Type        = m["Type"]?.ToString() ?? "",
                    Description = m["Description"]?.ToString() ?? ""
                })
                .ToList();

        private static IReadOnlyList<KnowledgeGraphRelationship> MapRelationships(object raw) =>
            ((IEnumerable<object>)raw)
                .Cast<IDictionary<string, object>>()
                .Select(m => new KnowledgeGraphRelationship
                {
                    Source      = m["Source"]?.ToString() ?? "",
                    Target      = m["Target"]?.ToString() ?? "",
                    Label       = m["Label"]?.ToString() ?? "",
                    Description = m["Description"]?.ToString() ?? "",
                    Weight      = m.TryGetValue("Weight", out var w) && w is not null ? Convert.ToSingle(w) : 0.5f
                })
                .ToList();

        public async ValueTask DisposeAsync()
        {
            await _driver.DisposeAsync();
        }
    }
}
