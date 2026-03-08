namespace _02_HybridRAG
{
    public record StarshipSpecifications
    {
        public required string TopSpeed { get; init; }
        public required string Fuel { get; init; }
        public int Seats { get; init; }
        public bool ArtificialGravity { get; init; }
    }
}
