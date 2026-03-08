namespace _02_HybridRAG
{
    public record Starship
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public required string ProductId { get; init; }
        public required string Category { get; init; }
        public required string Overview { get; init; }
        public StarshipSpecifications Specifications { get; init; } = default!;
        public IReadOnlyCollection<string> Features { get; init; } = [];
        public string Notes { get; init; } = string.Empty;
    }
}
