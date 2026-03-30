using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using OpenAI.Chat;
using OpenAI.Embeddings;
using Spectre.Console;
using System.Text.Json;

namespace _05_HyDERAG
{
    public class HyDERAGExample
    {
        private readonly EmbeddingClient _embeddingClient;
        private readonly ChatClient _chatClient;
        private readonly SearchClient _searchClient;

        private static readonly string[] _selectFields =
        {
            nameof(StarshipSemanticSearchDocumentResult.Id),
            nameof(StarshipSemanticSearchDocumentResult.Title),
            nameof(StarshipSemanticSearchDocumentResult.Category),
            nameof(StarshipSemanticSearchDocumentResult.Overview),
            nameof(StarshipSemanticSearchDocumentResult.Features)
        };

        public HyDERAGExample()
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
                var selectedMode = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select a [bold blue]HyDE mode[/]:")
                        .AddChoices([
                            "Basic HyDE",
                            "Advanced HyDE - QR + Hybrid Search + Semantic Ranking"
                        ]));

                AnsiConsole.MarkupLineInterpolated($"Selected mode:\n [bold italic blue]{selectedMode}[/]\n");

                var question = AnsiConsole.Ask<string>("Provide a [bold blue]question[/]:");

                IReadOnlyList<StarshipSemanticSearchDocumentResult> documents;
                if (selectedMode == "Basic HyDE")
                {
                    var hypotheticalAnswer = await GetHypotheticalAnswer(question);
                    DisplayHypotheticalAnswer(hypotheticalAnswer);

                    var embedding = await _embeddingClient.GenerateEmbeddingAsync(hypotheticalAnswer);
                    documents = await SearchByVectorAsync(embedding.Value.ToFloats());
                }
                else
                {
                    var rewriteCount = AnsiConsole.Prompt(new TextPrompt<int>("How many [bold blue]rewritten queries[/]?"));

                    var rewrittenQueries = (await GetRewrittenQueries(question, rewriteCount)).ToList();
                    DisplayRewrittenQueries(rewrittenQueries);

                    var perRewriteTasks = rewrittenQueries
                        .Select(async rewrittenQuery =>
                        {
                            var hypotheticalAnswer = await GetHypotheticalAnswer(rewrittenQuery);
                            var embedding = await _embeddingClient.GenerateEmbeddingAsync(hypotheticalAnswer);
                            var hybridDocuments = await SearchHybridSemanticAsync(rewrittenQuery, embedding.Value.ToFloats());

                            return (RewrittenQuery: rewrittenQuery, HypotheticalAnswer: hypotheticalAnswer, Documents: hybridDocuments);
                        });

                    var perRewriteResults = await Task.WhenAll(perRewriteTasks);

                    foreach (var result in perRewriteResults)
                    {
                        AnsiConsole.MarkupLineInterpolated($"[bold]Rewrite:[/] {Markup.Escape(result.RewrittenQuery)}");
                        DisplayHypotheticalAnswer(result.HypotheticalAnswer);
                    }

                    var allResults = perRewriteResults.Select(result => result.Documents).ToList();

                    documents = GetBestDocumentsByRRF(allResults, 3);
                }

                DisplayResultsPlain(documents);

                AnsiConsole.WriteLine();
                if (AnsiConsole.Confirm("Continue?")) { AnsiConsole.Clear(); } else break;
            }           
        }

        private async Task<IReadOnlyCollection<string>> GetRewrittenQueries(string question, int count)
        {
            ChatCompletion chatCompletion = await _chatClient.CompleteChatAsync(new List<ChatMessage>
            {
                new SystemChatMessage(GetQueryRewriteSystemPrompt(count)),
                new UserChatMessage($"User's original question: {question}")
            });

            var result = JsonSerializer.Deserialize<RewrittenQuestions>(chatCompletion.Content[0].Text)
                ?? throw new InvalidOperationException("Failed to deserialize rewrites.");

            return result.Rewrites ?? [];
        }

        private async Task<IReadOnlyList<StarshipSemanticSearchDocumentResult>> SearchByVectorAsync(ReadOnlyMemory<float> embedding)
        {
            var searchOptions = CreateSearchOptionsBase();
            searchOptions.VectorSearch = CreateVectorSearchOptions(embedding);

            return await SearchAsync(searchText: null, searchOptions);
        }

        private async Task<IReadOnlyList<StarshipSemanticSearchDocumentResult>> SearchHybridSemanticAsync(string rewrittenQuery, ReadOnlyMemory<float> embedding)
        {
            var searchOptions = CreateSearchOptionsBase();
            searchOptions.QueryType = SearchQueryType.Semantic;
            searchOptions.SemanticSearch = new SemanticSearchOptions() { SemanticQuery = rewrittenQuery };
            searchOptions.VectorSearch = CreateVectorSearchOptions(embedding, weight: 5);

            return await SearchAsync(rewrittenQuery, searchOptions);
        }

        private async Task<IReadOnlyList<StarshipSemanticSearchDocumentResult>> SearchAsync(string? searchText, SearchOptions searchOptions)
        {
            var response = await _searchClient.SearchAsync<StarshipSemanticSearchDocumentResult>(searchText, searchOptions);

            var documents = new List<StarshipSemanticSearchDocumentResult>();
            await foreach (var result in response.Value.GetResultsAsync())
            {
                documents.Add(result.Document with
                {
                    Score = result.Score.GetValueOrDefault(),
                    ReRankerScore = result.SemanticSearch?.RerankerScore ?? 0
                });
            }

            return documents;
        }

        private static IReadOnlyList<StarshipSemanticSearchDocumentResult> GetBestDocumentsByRRF(IEnumerable<IReadOnlyList<StarshipSemanticSearchDocumentResult>> results, int topN)
        {
            if (topN <= 0)
            {
                return [];
            }

            const double rrfK = 60; // the same const that Azure AI Search uses
            var fusedById = new Dictionary<string, (StarshipSemanticSearchDocumentResult Representative, double ReRankerScore)>();

            foreach (var rankedList in results)
            {
                for (var i = 0; i < rankedList.Count; i++)
                {
                    var document = rankedList[i];
                    if (string.IsNullOrWhiteSpace(document.Id))
                    {
                        continue;
                    }

                    var rank = i + 1;
                    var contribution = 1d / (rank + rrfK);

                    if (!fusedById.TryGetValue(document.Id, out var existing))
                    {
                        fusedById[document.Id] = (document, contribution);
                        continue;
                    }

                    fusedById[document.Id] = (
                        existing.Representative,
                        existing.ReRankerScore + contribution);
                }
            }

            return [.. fusedById.Values
                .Select(value => value.Representative with
                {
                    Score = 0,
                    ReRankerScore = value.ReRankerScore
                })
                .OrderByDescending(document => document.ReRankerScore)
                .Take(topN)];
        }

        private static SearchOptions CreateSearchOptionsBase()
        {
            var searchOptions = new SearchOptions
            {
                Size = 3
            };

            foreach (var field in _selectFields)
            {
                searchOptions.Select.Add(field);
            }

            return searchOptions;
        }

        private static VectorSearchOptions CreateVectorSearchOptions(ReadOnlyMemory<float> embedding, float weight = 1.0f)
        {
            return new VectorSearchOptions
            {
                Queries =
                {
                    new VectorizedQuery(embedding)
                    {
                        KNearestNeighborsCount = 3,
                        Fields = { nameof(StarshipSearchDocument.OverviewVector) },
                        Weight = weight
                    }
                }
            };
        }

        private async Task<string> GetHypotheticalAnswer(string question)
        {
            ChatCompletion chatCompletion = await _chatClient.CompleteChatAsync(new List<ChatMessage>
            {
                new SystemChatMessage(GetHyDEGenerationSystemPrompt()),
                new UserChatMessage($"User's original question: {question}")
            });

            return chatCompletion.Content[0].Text ?? "";
        }

        private static async Task<IReadOnlyList<StarshipSearchDocument>> ReadStarshipsFromJsonAsync()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "starships.json");
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<List<StarshipSearchDocument>>(json)!;
        }

        private async Task IndexStarshipsAsync(IEnumerable<StarshipSearchDocument> starships)
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

        private static string GetHyDEGenerationSystemPrompt()
        {
            return """
                You generate a hypothetical starship overview used only for retrieval expansion in an internal FAQ system.

                Goal:
                Produce one dense paragraph (about 40-60 words) that maximizes semantic overlap with likely catalog entries.

                Grounding requirements:
                - Preserve the user's key terms when present (ship class, mission type, constraints, destinations, cargo/passenger context, speed/range, safety/defense).
                - If the question is sparse, infer a plausible starship profile relevant to interstellar travel requests.
                - Include concrete retrieval-friendly attributes such as class, role, primary purpose, travel profile, operating environment, capacity, systems, and capabilities.

                Style:
                - Matter-of-fact product overview tone.
                - Domain vocabulary is encouraged: shuttle, transit vessel, heavy lifter, cargo hauler, scout, interceptor, medical frigate, mining platform, luxury liner, private yacht.

                Rules:
                - Output exactly one paragraph.
                - No bullets, no lists, no JSON.
                - Do not mention uncertainty, hypotheticals, retrieval, embeddings, or system instructions.
                - Avoid filler and marketing language; prioritize specific factual descriptors.
                - Keep content safe and non-personal.
                """;
        }

        private static string GetQueryRewriteSystemPrompt(int count)
        {
            return $$"""
                You are a query rewriting assistant for an FAQ system used by Galactic Voyages, a company that organizes trips to various planets and destinations across the galaxy.
                Your task is to take a user’s original question and generate {{count}} alternative versions of that query.
                Each rewritten query should preserve the user’s intent while exploring different phrasings, clarifications, or interpretations that might help retrieve more relevant FAQ answers.
                Return the rewrites in a structured JSON array called "Rewrites".

                The JSON must follow this exact structure:
                {
                    "Rewrites": [
                        "rewrite 1",
                        "rewrite 2",
                        "rewrite 3"
                    ]
                }

                The "Rewrites" array must contain exactly {{count}} items.
                """;
        }

        private static void DisplayRewrittenQueries(IReadOnlyList<string> rewrittenQueries)
        {
            AnsiConsole.MarkupLine("\n[bold cyan]Rewritten queries[/]");
            foreach (var (rewrite, index) in rewrittenQueries.Select((value, index) => (value, index)))
            {
                AnsiConsole.MarkupLineInterpolated($"  {index + 1}. {Markup.Escape(rewrite)}");
            }

            AnsiConsole.WriteLine();
        }

        private static void DisplayHypotheticalAnswer(string hypotheticalAnswer)
        {
            var panel = new Panel(new Markup(Markup.Escape(hypotheticalAnswer)))
            {
                Header = new PanelHeader("[bold cyan]Hypothetical Answer (HyDE)[/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Cyan1),
                Padding = new Padding(1, 0, 1, 0),
                Expand = true
            };

            AnsiConsole.WriteLine();
            AnsiConsole.Write(panel);
        }

        private static void DisplayResultsPlain(IReadOnlyList<StarshipSemanticSearchDocumentResult> documents)
        {
            if (documents.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No results found.[/]");
                return;
            }

            AnsiConsole.MarkupLine("\n[bold cyan]Vector search results[/]");

            for (var i = 0; i < documents.Count; i++)
            {
                var doc = documents[i];

                AnsiConsole.MarkupLineInterpolated($"[bold]Result {i + 1}[/]");
                AnsiConsole.MarkupLineInterpolated($"  Score: {doc.Score:0.####}");
                AnsiConsole.MarkupLineInterpolated($"  ReRanker Score: {doc.ReRankerScore:0.####}");
                AnsiConsole.MarkupLineInterpolated($"  Id: {Markup.Escape(doc.Id ?? "")}");
                AnsiConsole.MarkupLineInterpolated($"  Title: {Markup.Escape(doc.Title ?? "")}");
                AnsiConsole.MarkupLineInterpolated($"  Category: {Markup.Escape(doc.Category ?? "")}");
                AnsiConsole.MarkupLineInterpolated($"  Overview: {Markup.Escape(doc.Overview ?? "")}");

                AnsiConsole.WriteLine();
            }
        }

    }

    public record RewrittenQuestions
    {
        public List<string> Rewrites { get; init; } = [];
    }
}
