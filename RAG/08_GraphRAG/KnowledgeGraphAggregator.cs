namespace _08_GraphRAG
{
    public class KnowledgeGraphAggregator
    {
        public static KnowledgeGraph Merge(IEnumerable<KnowledgeGraph> perChunkGraphs)
        {
            var globalEntities = new Dictionary<string, KnowledgeGraphEntity>(StringComparer.OrdinalIgnoreCase);
            var globalRelationships = new List<KnowledgeGraphRelationship>();

            foreach (var graph in perChunkGraphs)
            {
                // 1. Merge Entities
                foreach (var entity in graph.Entities)
                {
                    var id = entity.Name;
                    if (globalEntities.TryGetValue(id, out var existing))
                    {
                        // in a real implemention we could store all the descriptions and then merge them into a single concise description using LLM
                    }
                    else
                    {
                        // only the 1st description is stored in that example
                        globalEntities[id] = entity;
                    }
                }

                // 2. Merge Relationships
                foreach (var rel in graph.Relationships)
                {
                    globalRelationships.Add(rel with
                    {
                        Source = rel.Source,
                        Target = rel.Target
                    });
                }
            }

            return new KnowledgeGraph
            {
                Entities = globalEntities.Values.ToList(),
                Relationships = globalRelationships
            };
        }
    }
}
