namespace _04_MultiQueryRAG
{
    public record SemanticSearchResult
    {
        public IReadOnlyList<StarshipSemanticSearchDocumentResult> Documents { get; init; } = [];
        public SemanticSearchQueryRewrites? QueryRewrites { get;init; }
    }
}
