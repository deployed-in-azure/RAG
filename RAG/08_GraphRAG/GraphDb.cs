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
                    ON CREATE SET r.description = $description")
                    .WithParameters(new
                    {
                        source = rel.Source,
                        target = rel.Target,
                        description = rel.Description
                    })
                    .ExecuteAsync();
            }
        }

        public async Task PrintAllConnectionsAsync()
        {
            var result = await _driver.ExecutableQuery($"""
                MATCH (s)-[r]->(t)
                RETURN s.name AS sName, type(r) AS rName, t.name AS tName
                """)
                .ExecuteAsync();

            Console.WriteLine("\n--- CURRENT KNOWLEDGE GRAPH CONNECTIONS ---");
            foreach (var record in result.Result)
            {
                var source = record["sName"].As<string>();
                var relationship = record["rName"].As<string>();
                var target = record["tName"].As<string>();

                Console.WriteLine($"  ({source}) --[{relationship}]--> ({target})");
            }
            Console.WriteLine("-------------------------------------------\n");
        }

        public async Task<GraphSearchResult> GetTopEntitiesAsync(float[] queryVector, int topK = 5, int traversalDepth = 1)
        {
            var result = await _driver.ExecutableQuery($$"""
                // Phase 1: vector ANN search — find seed nodes closest to the query
                CALL db.index.vector.queryNodes('{{VectorIndexName}}', $topK, $queryVector)
                YIELD node AS seedNode, score
                WITH collect(seedNode) AS seedNodes

                // Phase 2: variable-depth traversal from every seed node
                UNWIND seedNodes AS seedNode
                MATCH path = (seedNode)-[r*1..{{traversalDepth}}]-(neighbor)
                WITH seedNodes,
                     collect(DISTINCT neighbor) AS traversedNodes,
                     collect(DISTINCT r)         AS relPaths

                // Phase 3: flatten relationship collections and return
                UNWIND relPaths AS relList
                UNWIND relList  AS rel
                WITH seedNodes, traversedNodes, collect(DISTINCT rel) AS rels

                RETURN
                    [n IN seedNodes     | {Name: n.name, Type: labels(n)[0], Description: n.description}] AS Seeds,
                    [n IN traversedNodes | {Name: n.name, Type: labels(n)[0], Description: n.description}] AS Traversed,
                    [r IN rels          | {Source: startNode(r).name, Target: endNode(r).name, Label: type(r), Description: coalesce(r.description, '')}] AS Relationships
                """)
                .WithParameters(new { queryVector, topK }) 
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
                    Description = m["Description"]?.ToString() ?? ""
                })
                .ToList();

        public async ValueTask DisposeAsync()
        {
            await _driver.DisposeAsync();
        }
    }
}
