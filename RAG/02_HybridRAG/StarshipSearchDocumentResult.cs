namespace _02_HybridRAG
{
    public record StarshipSearchDocumentResult
    {
        public string? Id { get; init; }
        public string? Title { get; init; }
        public string? ProductId { get; init; }
        public string? Category { get; init; }
        public string? Overview { get; init; }
        public string? TopSpeed { get; init; }
        public string? Fuel { get; init; }
        public int Seats { get; init; }
        public bool ArtificialGravity { get; init; }
        public IReadOnlyCollection<string> Features { get; init; } = [];
        public string? Notes { get; init; }
        public double Score { get; init; }
    }
}
