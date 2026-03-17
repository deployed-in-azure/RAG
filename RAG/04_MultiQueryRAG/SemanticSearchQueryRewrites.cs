using Azure.Search.Documents.Models;

namespace _04_MultiQueryRAG
{
    public record SemanticSearchQueryRewrites
    {
        public SemanticSearchQueryRewrite? Text { get; init; }
        public SemanticSearchQueryRewrite? Vectors { get; init; }
    }

    public record SemanticSearchQueryRewrite
    {
        public string? InputQuery { get; init; }
        public IReadOnlyCollection<string> Rewrites { get; init; } = [];

        public static SemanticSearchQueryRewrite? Create(QueryRewritesValuesDebugInfo? queryRewritesValuesDebugInfo)
        {
            if (queryRewritesValuesDebugInfo is null)
            {
                return null;
            }

            return new SemanticSearchQueryRewrite
            {
                InputQuery = queryRewritesValuesDebugInfo.InputQuery,
                Rewrites = queryRewritesValuesDebugInfo.Rewrites
            };
        }
    }
}
