using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using OpenAI.Chat;
using OpenAI.Embeddings;
using Spectre.Console;
using System.Text;
using System.Text.Json;

namespace _02_HybridRAG
{
    public class HybridRagExample
    {
        private readonly EmbeddingClient _embeddingClient;
        private readonly ChatClient _chatClient;
        private readonly SearchClient _searchClient;
        private readonly SearchService _searchService;

        public HybridRagExample()
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
            _searchService = new SearchService(_searchClient, _embeddingClient);
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
                            "Vector (Overview)",
                            "Hybrid: FullText (BM25) + Vector (Overview)",
                            "Vector (Overview) + Vector (Notes)",
                            "Hybrid: FullText (BM25) + Vector (Overview) + Vector (Notes)"
                        ]));

                AnsiConsole.MarkupLineInterpolated($"Selected search method:\n [bold italic blue]{selectedSearchMethod}[/]\n");

                var question = AnsiConsole.Ask<string>("Provide a [bold blue]question[/]:");

                var searchOptions = new SearchOptions()
                {
                    QueryType = SearchQueryType.Simple, // Full > Lucene: wildcard, fuzzy, regex
                    SearchMode = SearchMode.Any, // All -> AND logic, Any -> OR logic
                    //SearchFields = { nameof(StarshipSearchDocument.Category) },
                    Size = 3,
                    Select =
                    {
                        nameof(StarshipSearchDocumentResult.Id),
                        nameof(StarshipSearchDocumentResult.Title),
                        nameof(StarshipSearchDocumentResult.ProductId),
                        nameof(StarshipSearchDocumentResult.Category),
                        nameof(StarshipSearchDocumentResult.Overview),
                        nameof(StarshipSearchDocumentResult.TopSpeed),
                        nameof(StarshipSearchDocumentResult.Fuel),
                        nameof(StarshipSearchDocumentResult.Seats),
                        nameof(StarshipSearchDocumentResult.ArtificialGravity),
                        nameof(StarshipSearchDocumentResult.Features),
                        nameof(StarshipSearchDocumentResult.Notes)
                    }
                };

                AnsiConsole.WriteLine("Running the query...\n");

                var result = selectedSearchMethod switch
                {
                    "Vector (Overview)" => await _searchService.InvokeVectorSearchAsync(question, searchOptions),
                    "Hybrid: FullText (BM25) + Vector (Overview)" => await _searchService.InvokeHybridFullTextVectorAsync(question, searchOptions),
                    "Vector (Overview) + Vector (Notes)" => await _searchService.InvokeVectorVectorAsync(question, searchOptions),
                    "Hybrid: FullText (BM25) + Vector (Overview) + Vector (Notes)" => await _searchService.InvokeHybridFullTextVectorVectorAsync(question, searchOptions),
                    _ => await _searchService.InvokeFullTextSearchAsync(question, searchOptions)
                };

                DisplayResultsPlain(result);

                var answer = await GetAnswerAsync(question, result);
                AnsiConsole.MarkupLineInterpolated($"[bold green]Chat response:[/]");
                AnsiConsole.WriteLine(answer);

                AnsiConsole.WriteLine();
                if (AnsiConsole.Confirm("Continue?")) { AnsiConsole.Clear(); } else break;
            }
        }

        private async Task<string> GetAnswerAsync(string question, IReadOnlyList<StarshipSearchDocumentResult> documents)
        {
            ChatCompletion chatCompletion = await _chatClient.CompleteChatAsync(new List<ChatMessage>
            {
                new SystemChatMessage(GetSystemPrompt()),
                new UserChatMessage(CreateUserPrompt(question, documents))
            });

            return chatCompletion.Content[0].Text ?? "Empty response";
        }

        private static string CreateUserPrompt(string question, IEnumerable<StarshipSearchDocumentResult> documents)
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
                builder.AppendLine($"ProductId: {kvp.Doc.ProductId}");
                builder.AppendLine($"Category: {kvp.Doc.Category}");
                builder.AppendLine($"Overview: {kvp.Doc.Overview}");
                builder.AppendLine($"TopSpeed: {kvp.Doc.TopSpeed}");
                builder.AppendLine($"Fuel: {kvp.Doc.Fuel}");
                builder.AppendLine($"Seats: {kvp.Doc.Seats}");
                builder.AppendLine($"ArtificialGravity: {kvp.Doc.ArtificialGravity}");
                builder.AppendLine($"Features: {string.Join(", ", kvp.Doc.Features)}");
                builder.AppendLine($"Notes: {kvp.Doc.Notes}");
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

        private static void DisplayResultsPlain(IReadOnlyList<StarshipSearchDocumentResult> documents)
        {
            if (documents.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No results found.[/]");
                return;
            }

            for (var i = 0; i < documents.Count; i++)
            {
                var doc = documents[i];

                AnsiConsole.MarkupLineInterpolated($"[bold]Result {i + 1}[/]");
                AnsiConsole.MarkupLineInterpolated($"  Score: {doc.Score:0.####}");
                AnsiConsole.MarkupLineInterpolated($"  Id: {Markup.Escape(doc.Id ?? "")}");
                AnsiConsole.MarkupLineInterpolated($"  Title: {Markup.Escape(doc.Title ?? "")}");
                AnsiConsole.MarkupLineInterpolated($"  ProductId: {Markup.Escape(doc.ProductId ?? "")}");
                AnsiConsole.MarkupLineInterpolated($"  Category: {Markup.Escape(doc.Category ?? "")}");
                AnsiConsole.MarkupLineInterpolated($"  Overview: {Markup.Escape(doc.Overview ?? "")}");
                AnsiConsole.MarkupLineInterpolated($"  TopSpeed: {Markup.Escape(doc.TopSpeed ?? "")}");
                AnsiConsole.MarkupLineInterpolated($"  Fuel: {Markup.Escape(doc.Fuel ?? "")}");
                AnsiConsole.MarkupLineInterpolated($"  Seats: {doc.Seats}");
                AnsiConsole.MarkupLineInterpolated($"  ArtificialGravity: {(doc.ArtificialGravity ? "Yes" : "No")}");
                AnsiConsole.MarkupLineInterpolated($"  Features: {Markup.Escape(string.Join(", ", doc.Features))}");
                AnsiConsole.MarkupLineInterpolated($"  Notes: {Markup.Escape(doc.Notes ?? "")}");
                AnsiConsole.WriteLine();
            }
        }

        public async Task IndexStarshipsAsync(IEnumerable<Starship> starships)
        {
            var documents = new List<StarshipSearchDocument>();

            foreach (var starship in starships)
            {
                var overviewEmbedding = await _embeddingClient.GenerateEmbeddingAsync(starship.Overview);
                var notesEmbedding = await _embeddingClient.GenerateEmbeddingAsync(starship.Notes);

                documents.Add(new StarshipSearchDocument
                {
                    Id = starship.Id,
                    Title = starship.Title,
                    ProductId = starship.ProductId,
                    Category = starship.Category,
                    Overview = starship.Overview,
                    TopSpeed = starship.Specifications.TopSpeed,
                    Fuel = starship.Specifications.Fuel,
                    Seats = starship.Specifications.Seats,
                    ArtificialGravity = starship.Specifications.ArtificialGravity,
                    Features = starship.Features,
                    Notes = starship.Notes,
                    OverviewVector = overviewEmbedding.Value.ToFloats().ToArray(),
                    NotesVector = notesEmbedding.Value.ToFloats().ToArray()
                });
            }

            await _searchClient.IndexDocumentsAsync(IndexDocumentsBatch.Upload(documents));
        }

        public static async Task<IReadOnlyList<Starship>> ReadStarshipsFromJsonAsync()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "starships.json");
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<List<Starship>>(json)!;
        }
    }
}
