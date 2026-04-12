namespace _07_ContextualRetrieval
{
    public record SearchDocumentModel
    {
        public string? id { get; init; }
        public string? EnrichedChunk { get; init; }
        public IReadOnlyCollection<float> EnrichedChunkVector { get; init; } = [];
    }
}
