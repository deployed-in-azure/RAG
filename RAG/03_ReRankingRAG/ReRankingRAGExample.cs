using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using OpenAI.Chat;
using OpenAI.Embeddings;
using Spectre.Console;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace _03_ReRankingRAG
{
    public class ReRankingRAGExample
    {
        private readonly EmbeddingClient _embeddingClient;
        private readonly ChatClient _chatClient;
        private readonly SearchClient _searchClient;

        public ReRankingRAGExample()
        {
            var credential = new DefaultAzureCredential();

            _searchClient = new SearchClient(
                new Uri(Environment.GetEnvironmentVariable("AZURE_AI_SEARCH_URI")!),
                indexName: Environment.GetEnvironmentVariable("AZURE_AI_SEARCH_INDEX")!,
                credential);

            var openAiClient = new AzureOpenAIClient(
                new Uri(Environment.GetEnvironmentVariable("AZURE_OPEN_AI_CLIENT_URI")!),
                credential);

            _embeddingClient = openAiClient.GetEmbeddingClient(Environment.GetEnvironmentVariable("AZURE_OPEN_AI_EMBEDDING_CLIENT_DEPLOYMENT_NAME")!);
            _chatClient = openAiClient.GetChatClient(Environment.GetEnvironmentVariable("AZURE_OPEN_AI_EMBEDDING_CHAT_CLIENT_DEPLOYMENT_NAME")!);
        }

        public async Task RunAsync()
        {
            var starships = await ReadStarshipsFromJsonAsync();
            await IndexStarshipsAsync(starships);

            while (true)
            {
                var selectedSearchMethod = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select a [bold blue]search method[/]:")
                        .AddChoices([
                            "FullText (BM25)",
                            "Hybrid: FullText (BM25) + Vector",
                            "Hybrid + Semantic",
                            "Hybrid + Semantic + Scoring Profile",
                        ]));

                AnsiConsole.MarkupLineInterpolated($"Selected search method:\n [bold italic blue]{selectedSearchMethod}[/]\n");

                var question = AnsiConsole.Ask<string>("Provide a [bold blue]question[/]:");

                var searchOptions = new SearchOptions()
                {
                    QueryType = selectedSearchMethod.Contains("Semantic") ? SearchQueryType.Semantic : SearchQueryType.Simple,
                    Size = 10,
                    IncludeTotalCount = true,
                    Select =
                    {
                        nameof(StarshipSemanticSearchDocumentResult.Id),
                        nameof(StarshipSemanticSearchDocumentResult.Title),
                        nameof(StarshipSemanticSearchDocumentResult.Category),
                        nameof(StarshipSemanticSearchDocumentResult.Overview),
                        nameof(StarshipSemanticSearchDocumentResult.Features)
                    }
                };

                if (selectedSearchMethod.Contains("Semantic"))
                {
                    searchOptions.SemanticSearch = new SemanticSearchOptions
                    {
                        SemanticQuery = question,
                        QueryAnswer = new QueryAnswer(QueryAnswerType.Extractive),
                        QueryCaption = new QueryCaption(QueryCaptionType.Extractive),
                    };
                }
                
                if (selectedSearchMethod.Contains("Hybrid"))
                {
                    var embedding = await _embeddingClient.GenerateEmbeddingAsync(question);
                    var queryVector = embedding.Value.ToFloats().ToArray();

                    searchOptions.VectorSearch = new VectorSearchOptions
                    {
                        Queries =
                        {
                            new VectorizedQuery(queryVector)
                            {
                                KNearestNeighborsCount = searchOptions.Size ?? 10,
                                Fields = { nameof(StarshipSearchDocument.OverviewVector) }
                            }
                        }
                    };
                }

                if (selectedSearchMethod.Contains("Scoring Profile"))
                {
                    searchOptions.ScoringProfile = "boost_category_field";
                    searchOptions.ScoringParameters.Add("tagBoostCategory-Luxury");
                }

                var semanticSearchResult = await SearchAsync(question, searchOptions);

                DisplayResultsPlain(semanticSearchResult);

                var answer = await GetAnswerAsync(question, semanticSearchResult);
                AnsiConsole.MarkupLineInterpolated($"[bold green]Chat response:[/]");
                AnsiConsole.WriteLine(answer);

                AnsiConsole.WriteLine();
                if (AnsiConsole.Confirm("Continue?")) { AnsiConsole.Clear(); } else break;
            }
        }

        public async Task<SemanticSearchResult> SearchAsync(string question, SearchOptions searchOptions)
        {
            var response = await _searchClient.SearchAsync<StarshipSemanticSearchDocumentResult>(question, searchOptions);

            var rawContent = response.GetRawResponse().Content.ToString();

            var documents = new List<StarshipSemanticSearchDocumentResult>();
            await foreach (var result in response.Value.GetResultsAsync())
            {
                documents.Add(result.Document with 
                { 
                    Score = result.Score.GetValueOrDefault(),
                    ReRankerScore = result.SemanticSearch?.RerankerScore ?? 0,
                    RerankerBoostedScore = result.SemanticSearch?.RerankerBoostedScore ?? 0,
                    Captions = result.SemanticSearch?.Captions?.Select(SemanticSearchCaption.FromQueryCaptionResult).ToList() ?? []
                });

                if (documents.Count == 3)
                {
                    break;
                }
            }

            return new SemanticSearchResult
            {
                Answers = response.Value.SemanticSearch?.Answers?.Select(SemanticSearchAnswer.FromQueryAnswerResult).ToList() ?? [],
                Documents = documents
            };
        }

        private async Task<string> GetAnswerAsync(string question, SemanticSearchResult semanticSearchResult)
        {
            ChatCompletion chatCompletion = await _chatClient.CompleteChatAsync(new List<ChatMessage>
            {
                new SystemChatMessage(GetSystemPrompt()),
                new UserChatMessage(CreateUserPrompt(question, semanticSearchResult.Documents))
            });

            return chatCompletion.Content[0].Text ?? "Empty response";
        }

        private static string CreateUserPrompt(string question, IEnumerable<StarshipSemanticSearchDocumentResult> documents)
        {
            var builder = new StringBuilder();

            builder.AppendLine("Here are the documents related to the question.");
            builder.AppendLine("Use only the information in these documents when answering.");
            builder.AppendLine();
            builder.AppendLine("=== Retrieved Documents ===");

            foreach (var kvp in documents.Select((doc, index) => (Doc: doc, Index: index)))
            {
                builder.AppendLine();
                builder.AppendLine($"[Document {kvp.Index + 1}]");
                builder.AppendLine($"Id: {kvp.Doc.Id}");
                builder.AppendLine($"Title: {kvp.Doc.Title}");
                builder.AppendLine($"Category: {kvp.Doc.Category}");
                builder.AppendLine($"Overview: {kvp.Doc.Overview}");
                builder.AppendLine($"Features: {string.Join(", ", kvp.Doc.Features)}");
            }

            builder.AppendLine();
            builder.AppendLine("=== User Question ===");
            builder.AppendLine(question);

            return builder.ToString();
        }

        private static string GetSystemPrompt()
        {
            return """
            You are a helpful assistant for the Galactic Voyages travel agency.
            Answer questions using only the information provided in the documents.
            Keep your answers clear and friendly.
            """;
        }

        private static void DisplayResultsPlain(SemanticSearchResult semanticSearchResult)
        {
            if (semanticSearchResult.Answers.Count > 0)
            {
                AnsiConsole.MarkupLine("[bold cyan]Semantic Answers[/]");
                for (var i = 0; i < semanticSearchResult.Answers.Count; i++)
                {
                    var answer = semanticSearchResult.Answers[i];
                    var citation = $"Answer: {i + 1})";

                    AnsiConsole.MarkupLineInterpolated($"  [bold]{citation}[/]\n    Score: {answer.Score.GetValueOrDefault():0.####}");
                    AnsiConsole.MarkupLineInterpolated($"    Key: {Markup.Escape(answer.Key ?? "-")}");
                    AnsiConsole.MarkupLineInterpolated($"    Text: {Markup.Escape(answer.Text ?? "-")}");

                    if (!string.IsNullOrWhiteSpace(answer.Highlights))
                    {
                        AnsiConsole.MarkupLine($"    Highlights: {ToSpectreHighlightedText(answer.Highlights)}");
                    }
                }

                AnsiConsole.WriteLine();
            }

            if (semanticSearchResult.Documents.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No results found.[/]");
                return;
            }

            AnsiConsole.MarkupLine("[bold cyan]Documents + Captions[/]");

            for (var i = 0; i < semanticSearchResult.Documents.Count; i++)
            {
                var doc = semanticSearchResult.Documents[i];

                AnsiConsole.MarkupLineInterpolated($"[bold]Result {i + 1}[/]");
                AnsiConsole.MarkupLineInterpolated($"  Score: {doc.Score:0.####}");
                AnsiConsole.MarkupLineInterpolated($"  ReRanker Score: {doc.ReRankerScore:0.####}");
                AnsiConsole.MarkupLineInterpolated($"  ReRanker Boosted Score: {doc.RerankerBoostedScore:0.####}");
                AnsiConsole.MarkupLineInterpolated($"  Id: {Markup.Escape(doc.Id ?? "")}");
                AnsiConsole.MarkupLineInterpolated($"  Title: {Markup.Escape(doc.Title ?? "")}");
                AnsiConsole.MarkupLineInterpolated($"  Category: {Markup.Escape(doc.Category ?? "")}");
                AnsiConsole.MarkupLineInterpolated($"  Overview: {Markup.Escape(doc.Overview ?? "")}");

                if (doc.Captions.Count > 0)
                {
                    AnsiConsole.MarkupLine("  [bold]Captions:[/]");
                    var captionIndex = 1;
                    foreach (var caption in doc.Captions)
                    {
                        var captionCitation = $"Caption: {captionIndex})";
                        var captionText = !string.IsNullOrWhiteSpace(caption.Highlights)
                            ? caption.Highlights
                            : caption.Text ?? string.Empty;

                        AnsiConsole.MarkupLine($"    {captionCitation} {ToSpectreHighlightedText(captionText)}");
                        captionIndex++;
                    }
                }

                AnsiConsole.WriteLine();
            }
        }

        public async Task IndexStarshipsAsync(IEnumerable<StarshipSearchDocument> starships)
        {
            var documents = new List<StarshipSearchDocument>();

            foreach (var starship in starships)
            {
                var overviewEmbedding = await _embeddingClient.GenerateEmbeddingAsync(starship.Overview);

                documents.Add(starship with
                { 
                    OverviewVector = overviewEmbedding.Value.ToFloats().ToArray()
                });
            }

            await _searchClient.IndexDocumentsAsync(IndexDocumentsBatch.Upload(documents));
        }

        public static async Task<IReadOnlyList<StarshipSearchDocument>> ReadStarshipsFromJsonAsync()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "starships.json");
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<List<StarshipSearchDocument>>(json)!;
        }

        private static string ToSpectreHighlightedText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            var currentIndex = 0;

            foreach (Match match in Regex.Matches(text, "<em>(.*?)</em>"))
            {
                if (match.Index > currentIndex)
                {
                    builder.Append(Markup.Escape(text[currentIndex..match.Index]));
                }

                var highlightedText = match.Groups[1].Value;
                builder.Append($"[bold yellow]{Markup.Escape(highlightedText)}[/]");

                currentIndex = match.Index + match.Length;
            }

            if (currentIndex < text.Length)
            {
                builder.Append(Markup.Escape(text[currentIndex..]));
            }

            return builder.ToString();
        }
    }
}
