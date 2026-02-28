namespace Shared
{
    public record VectorSearchResult
    {
        public required string Id { get; init; }
        public required double Similarity { get; init; }
        public required IReadOnlyDictionary<string, string> Data { get; init; }
    }
}
