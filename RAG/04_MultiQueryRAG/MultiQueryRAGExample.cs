using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using OpenAI.Chat;
using OpenAI.Embeddings;
using Spectre.Console;
using System.Text.Json;

namespace _04_MultiQueryRAG
{
    public class MultiQueryRAGExample
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

        public MultiQueryRAGExample()
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
            //var starships = await ReadStarshipsFromJsonAsync();
            //await IndexStarshipsAsync(starships);

            while (true)
            {
                var selectedRewriter = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select a [bold blue]query rewriter type[/]:")
                        .AddChoices([
                            "None",
                            "Custom",
                            "Azure AI Search"
                        ]));

                AnsiConsole.MarkupLineInterpolated($"Selected rewriter:\n [bold italic blue]{selectedRewriter}[/]\n");

                List<string> searchMethodChoices = selectedRewriter switch
                {
                    "None" => ["FullText (BM25)", "Vector"],
                    "Custom" => ["FullText (BM25) + QR", "Vector + QR"],
                    "Azure AI Search" => ["FullText (BM25) + SR + QR", "FullText (BM25) + Vector + SR + QR"],
                    _ => ["FullText (BM25)", "Vector"]
                };

                var selectedSearchMethod = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select a [bold blue]search method[/]:")
                        .AddChoices(searchMethodChoices));

                AnsiConsole.MarkupLineInterpolated($"Selected search method:\n [bold italic blue]{selectedSearchMethod}[/]\n");
                var question = AnsiConsole.Ask<string>("Provide a [bold blue]question[/]:");

                var results = new SemanticSearchResult();
                if (selectedRewriter == "None")
                {
                    var searchOptions = CreateSearchOptionsBase();
                    if (selectedSearchMethod.Contains("Vector"))
                    {
                        var embedding = await _embeddingClient.GenerateEmbeddingAsync(question);
                        searchOptions.VectorSearch = CreateVectorSearchOptions(embedding.Value.ToFloats());
                        question = null;
                    }

                    results = await SearchAsync(question, searchOptions);
                }
                else if (selectedRewriter == "Custom")
                {
                    results = await SearchUsingCustomRewriterAsync(question, selectedSearchMethod, useDomainSpecificSystemPrompt: true);
                }
                else
                {
                    results = await SearchUsingAiSearchAsync(question, selectedSearchMethod);
                }

                DisplayResultsPlain(results);

                AnsiConsole.WriteLine();
                if (AnsiConsole.Confirm("Continue?")) { AnsiConsole.Clear(); } else break;
            }
        }

        private async Task<IReadOnlyCollection<string>> GetRewrittenQueries(string question, string systemPrompt)
        {
            ChatCompletion chatCompletion = await _chatClient.CompleteChatAsync(new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage($"User's original question: {question}")
            });

            var result = JsonSerializer.Deserialize<RewrittenQuestions>(chatCompletion.Content[0].Text) ?? throw new InvalidOperationException("Failed to deserialize rewrites.");
            return result.Rewrites ?? [];
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

        private static async Task<IReadOnlyList<StarshipSearchDocument>> ReadStarshipsFromJsonAsync()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "starships.json");
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<List<StarshipSearchDocument>>(json)!;
        }

        private async Task<SemanticSearchResult> SearchUsingCustomRewriterAsync(string question, string selectedSearchMethod, bool useDomainSpecificSystemPrompt = true)
        {
            var systemPrompt = useDomainSpecificSystemPrompt
                ? GetDomainSpecificQueryRewriteSystemPrompt(5)
                : GetGenericQueryRewriteSystemPrompt(5);

            var rewrittenQueries = (await GetRewrittenQueries(question, systemPrompt)).ToList();

            var type = useDomainSpecificSystemPrompt ? "Domain Specific" : "Generic";
            AnsiConsole.MarkupLine($"\n[bold green]{type}[/] rewritten queries");
            foreach (var kvp in rewrittenQueries.Select((value, index) => (RewrittenQuery: value, Index: index)))
            {
                AnsiConsole.WriteLine($" {kvp.Index + 1}. {kvp.RewrittenQuery}");
            }

            var searchOptionsPerQuery = new List<SearchOptions>();
            if (selectedSearchMethod.Contains("Vector"))
            {
                var embeddings = await _embeddingClient.GenerateEmbeddingsAsync(rewrittenQueries);
                searchOptionsPerQuery = [.. embeddings.Value
                    .Select(embedding =>
                    {
                        var searchOptions = CreateSearchOptionsBase();
                        searchOptions.VectorSearch = CreateVectorSearchOptions(embedding.ToFloats());

                        return searchOptions;
                    })];
            }
            else
            {
                searchOptionsPerQuery = [.. rewrittenQueries.Select(_ => CreateSearchOptionsBase())];
            }

            var result = await Task.WhenAll(rewrittenQueries
                .Select((rewrittenQuery, index) => SearchAsync(selectedSearchMethod.Contains("Vector") ? null : rewrittenQuery, searchOptionsPerQuery[index])));

            AnsiConsole.MarkupLine("\n[bold cyan]Per-query top documents[/]");
            foreach (var item in rewrittenQueries.Zip(result, (query, searchResult) => new { Query = query, SearchResult = searchResult }))
            {
                AnsiConsole.MarkupLineInterpolated($"[bold]Query:[/] {Markup.Escape(item.Query)}");

                if (item.SearchResult.Documents.Count == 0)
                {
                    AnsiConsole.MarkupLine("  [yellow]No results[/]");
                    continue;
                }

                foreach (var doc in item.SearchResult.Documents)
                {
                    AnsiConsole.MarkupLineInterpolated($"  Score: {doc.Score:0.####} | Id: {Markup.Escape(doc.Id ?? "-")} | Title: {Markup.Escape(doc.Title ?? "-")}");
                }

                AnsiConsole.WriteLine();
            }

            var topDocuments = GetBestDocumentsByMaxScore(result);

            return new SemanticSearchResult
            {
                Documents = topDocuments
            };
        }

        private static IReadOnlyList<StarshipSemanticSearchDocumentResult> GetBestDocumentsByMaxScore(SemanticSearchResult[] result, int topN = 3)
        {
            var bestDocumentsById = new Dictionary<string, StarshipSemanticSearchDocumentResult>();

            foreach (var document in result.SelectMany(searchResult => searchResult.Documents))
            {
                if (!bestDocumentsById.TryGetValue(document.Id!, out var currentBest) || document.Score > currentBest.Score)
                {
                    bestDocumentsById[document.Id!] = document;
                }
            }

            return [.. bestDocumentsById.Values.OrderByDescending(document => document.Score).Take(topN)];
        }

        private async Task<SemanticSearchResult> SearchUsingAiSearchAsync(string? question, string selectedSearchMethod)
        {
            var searchOptions = CreateSearchOptionsBase();
            searchOptions.QueryType = SearchQueryType.Semantic;
            searchOptions.QueryLanguage = "en-us";

            if (selectedSearchMethod.Contains("Vector"))
            {
                searchOptions.VectorSearch = new VectorSearchOptions()
                {
                    Queries =
                    {
                        new VectorizableTextQuery(question)
                        {
                            KNearestNeighborsCount = 3,
                            Fields = { nameof(StarshipSearchDocument.OverviewVector) }
                        }
                    }
                };
            }

            searchOptions.SemanticSearch = new SemanticSearchOptions()
            {
                QueryRewrites = new QueryRewrites(QueryRewritesType.Generative)
                {
                    Count = 5
                }
            };

            searchOptions.Debug = "queryRewrites";

            return await SearchAsync(question, searchOptions);
        }

        private static SearchOptions CreateSearchOptionsBase()
        {
            var searchOptions = new SearchOptions
            {
                Size = 3,
                QueryType = SearchQueryType.Simple
            };

            foreach (var field in _selectFields)
            {
                searchOptions.Select.Add(field);
            }

            return searchOptions;
        }

        private static VectorSearchOptions CreateVectorSearchOptions(ReadOnlyMemory<float> embedding)
        {
            return new VectorSearchOptions
            {
                Queries = 
                {
                    new VectorizedQuery(embedding)
                    {
                        KNearestNeighborsCount = 3,
                        Fields = { nameof(StarshipSearchDocument.OverviewVector) }
                    }
                }
            };
        }

        private async Task<SemanticSearchResult> SearchAsync(string? question, SearchOptions searchOptions)
        {
            var response = await _searchClient.SearchAsync<StarshipSemanticSearchDocumentResult>(question, searchOptions);

            var documents = new List<StarshipSemanticSearchDocumentResult>();
            await foreach (var result in response.Value.GetResultsAsync())
            {
                documents.Add(result.Document with
                {
                    Score = result.Score.GetValueOrDefault(),
                    ReRankerScore = result.SemanticSearch?.RerankerScore ?? 0
                });

                if (documents.Count == 3)
                {
                    break;
                }
            }

            return new SemanticSearchResult
            {
                Documents = documents,
                QueryRewrites = new SemanticSearchQueryRewrites()
                {
                    Text = SemanticSearchQueryRewrite.Create(response.Value.DebugInfo?.QueryRewrites?.Text),
                    Vectors = SemanticSearchQueryRewrite.Create(response.Value.DebugInfo?.QueryRewrites?.Vectors?.FirstOrDefault()),
                }
            };
        }

        private string GetDomainSpecificQueryRewriteSystemPrompt(int count = 3)
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

        private string GetGenericQueryRewriteSystemPrompt(int count = 3)
        {
            return $$"""
                You are a query rewriting assistant for Retrieval-Augmented Generation (RAG).
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

        private static void DisplayResultsPlain(SemanticSearchResult semanticSearchResult)
        {
            var textQueryRewrites = semanticSearchResult.QueryRewrites?.Text;
            if (textQueryRewrites is not null && textQueryRewrites.Rewrites.Count > 0)
            {
                AnsiConsole.MarkupLine("\n[bold cyan]Query Rewrites (Text)[/]");

                if (!string.IsNullOrWhiteSpace(textQueryRewrites.InputQuery))
                {
                    AnsiConsole.MarkupLineInterpolated($"  Input Query: {Markup.Escape(textQueryRewrites.InputQuery)}");
                }

                foreach (var (rewrite, index) in textQueryRewrites.Rewrites.Select((value, index) => (value, index)))
                {
                    AnsiConsole.MarkupLineInterpolated($"  {index + 1}. {Markup.Escape(rewrite)}");
                }

                AnsiConsole.WriteLine();
            }

            if (semanticSearchResult.Documents.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No results found.[/]");
                return;
            }

            AnsiConsole.WriteLine();

            for (var i = 0; i < semanticSearchResult.Documents.Count; i++)
            {
                var doc = semanticSearchResult.Documents[i];

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
