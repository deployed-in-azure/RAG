namespace _04_MultiQueryRAG
{
    public record StarshipSearchDocument
    {
        public string? Id { get; init; }
        public string? Title { get; init; }
        public string? Category { get; init; }
        public string? Overview { get; init; }
        public IReadOnlyCollection<float> OverviewVector { get; init; } = [];
        public IReadOnlyCollection<string> Features { get; init; } = [];
    }
}
