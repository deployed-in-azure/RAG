using Azure.Search.Documents.Models;

namespace _03_ReRankingRAG
{
    public record SemanticSearchCaption
    {
        public string? Text { get; init; }
        public string? Highlights { get; init; }

        public static SemanticSearchCaption FromQueryCaptionResult(QueryCaptionResult caption) => new()
        {
            Text = caption.Text,
            Highlights = caption.Highlights
        };
    }
}
