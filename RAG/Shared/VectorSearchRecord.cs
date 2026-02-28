namespace Shared
{
    public record VectorSearchRecord
    {
        public required string Id { get; init; }
        public IReadOnlyDictionary<string, string> Data { get; init; } = new Dictionary<string, string>();
        public required float[] Vector { get; init; }
    }
}
