namespace _05_HyDERAG
{
    public record StarshipSemanticSearchDocumentResult
    {
        public string? Id { get; init; }
        public string? Title { get; init; }
        public string? Category { get; init; }
        public string? Overview { get; init; }
        public IReadOnlyCollection<string> Features { get; init; } = [];
        public double Score { get; init; }
        public double ReRankerScore { get; init; }
    }
}
