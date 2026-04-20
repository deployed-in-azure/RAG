namespace _08_GraphRAG
{
    public record KnowledgeGraph
    {
        public required IReadOnlyList<KnowledgeGraphEntity> Entities { get; init; }
        public required IReadOnlyList<KnowledgeGraphRelationship> Relationships { get; init; }
    }

    public record KnowledgeGraphEntity
    {
        public required string Name { get; init; }
        public required string Type { get; init; }
        public required string Description { get; init; }
        public float[] Embedding { get; init; } = [];
    }

    public record KnowledgeGraphRelationship
    {
        public required string Source { get; init; }
        public required string Target { get; init; }
        public required string Label { get; init; }
        public required string Description { get; init; }
        public float Weight { get; init; } = 0.5f;
    }

    public record GraphSearchResult
    {
        public required IReadOnlyList<KnowledgeGraphEntity> SeedEntities { get; init; }
        public required IReadOnlyList<KnowledgeGraphEntity> TraversedEntities { get; init; }
        public required IReadOnlyList<KnowledgeGraphRelationship> Relationships { get; init; }
        public required int TraversalDepth { get; init; }

        public IReadOnlyList<KnowledgeGraphEntity> AllEntities =>
            SeedEntities.Concat(TraversedEntities).DistinctBy(e => e.Name).ToList();
    }
}
