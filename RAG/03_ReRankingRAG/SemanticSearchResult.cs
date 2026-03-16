namespace _03_ReRankingRAG
{
    public record SemanticSearchResult
    {
        public IReadOnlyList<SemanticSearchAnswer> Answers { get; init; } = [];
        public IReadOnlyList<StarshipSemanticSearchDocumentResult> Documents { get; init; } = [];
    }
}
