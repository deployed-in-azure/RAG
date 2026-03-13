using Azure.Search.Documents.Models;

namespace _03_ReRankingRAG
{
    public record SemanticSearchAnswer
    {
        public string? Key { get; init; }
        public string? Text { get; init; }
        public string? Highlights { get; init; }
        public double? Score { get; init; }

        public static SemanticSearchAnswer FromQueryAnswerResult(QueryAnswerResult answer) => new()
        {
            Key = answer.Key,
            Text = answer.Text,
            Highlights = answer.Highlights,
            Score = answer.Score
        };
    }
}
