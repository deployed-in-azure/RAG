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

                // 2. Merge Relationships — deduplicate by (Source, Target, Label), averaging Weight
                foreach (var rel in graph.Relationships)
                {
                    var existing = globalRelationships.FirstOrDefault(r =>
                        string.Equals(r.Source, rel.Source, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(r.Target, rel.Target, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(r.Label,  rel.Label,  StringComparison.OrdinalIgnoreCase));

                    if (existing is not null)
                    {
                        var index = globalRelationships.IndexOf(existing);
                        globalRelationships[index] = existing with { Weight = (existing.Weight + rel.Weight) / 2f };
                    }
                    else
                    {
                        globalRelationships.Add(rel);
                    }
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
