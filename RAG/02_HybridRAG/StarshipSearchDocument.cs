namespace _02_HybridRAG
{
    public record StarshipSearchDocument
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public required string ProductId { get; init; }
        public required string Category { get; init; }
        public required string Overview { get; init; }
        public required string TopSpeed { get; init; }
        public required string Fuel { get; init; }
        public int Seats { get; init; }
        public bool ArtificialGravity { get; init; }
        public IReadOnlyCollection<string> Features { get; init; } = [];
        public required string Notes { get; init; }
        public IReadOnlyList<float> OverviewVector { get; init; } = [];
        public IReadOnlyList<float> NotesVector { get; init; } = [];
    }
}
