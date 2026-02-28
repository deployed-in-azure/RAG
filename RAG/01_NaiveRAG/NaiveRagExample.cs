using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;
using OpenAI.Embeddings;
using Shared;
using Spectre.Console;
using System.Text;

namespace _01_NaiveRAG
{
    public class NaiveRagExample
    {
        private readonly EmbeddingClient _embeddingClient;
        private readonly ChatClient _chatClient;        
        private readonly InMemoryVectorDb _inMemoryVectorDb = new();

        private readonly IReadOnlyCollection<(string ShipName, string FileName)> _dataSource = [
            ("Aurora Class Shuttle", "aurora-class.md"),
            ("Ion‑Drive Clipper", "ion-drive-clipper.md"),
            ("Nebula-X Cruiser", "nebula-class.md"),
            ("Quantum-Fold Starliner", "quantum-fold-starliner.md"),
            ("Starlance Explorer", "starlance-explorer.md")
        ];

        public NaiveRagExample()
        {
            var openAiClient = new AzureOpenAIClient(
                new Uri(Environment.GetEnvironmentVariable("AZURE_OPEN_AI_CLIENT_URI")!),
                new DefaultAzureCredential());

            _embeddingClient = openAiClient.GetEmbeddingClient(Environment.GetEnvironmentVariable("AZURE_OPEN_AI_EMBEDDING_CLIENT_DEPLOYMENT_NAME")!);
            _chatClient = openAiClient.GetChatClient(Environment.GetEnvironmentVariable("AZURE_OPEN_AI_EMBEDDING_CHAT_CLIENT_DEPLOYMENT_NAME")!);
        }

        public async Task RunAsync()
        {
            var embeddings = await GenerateEmbeddingsAsync();
            SaveEmbeddingsInVectorDb(embeddings.ToList());

            while (true)
            {
                var selectedSystemPrompt = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select a [bold blue]system prompt[/]:")
                        .AddChoices(GetAllSystemPrompts()));

                AnsiConsole.MarkupLineInterpolated($"Selected system prompt:\n [bold italic blue]{selectedSystemPrompt}[/]\n");

                var selectedQuestion = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select a [bold blue]question[/]:")
                        .AddChoices([.. GetAllQuestions(), "Custom"]));

                if (selectedQuestion == "Custom")
                {
                    selectedQuestion = AnsiConsole.Ask<string>("Provide a [bold blue]custom[/] question:");
                }

                AnsiConsole.MarkupLineInterpolated($"Selected question:\n [bold italic blue]{selectedQuestion}[/]\n");

                var answer = await GetAnswerAsync(selectedSystemPrompt, selectedQuestion);
                AnsiConsole.MarkupLineInterpolated($"[bold green]Chat response:[/]");
                AnsiConsole.WriteLine(answer);

                AnsiConsole.WriteLine();
                if (AnsiConsole.Confirm("Continue?")) { AnsiConsole.Clear(); } else break;           
            }
        }

        private async Task<IReadOnlyCollection<VectorSearchRecord>> GenerateEmbeddingsAsync()
        {
            AnsiConsole.MarkupLine("*** Source data vectorization started ***");

            var result = new List<VectorSearchRecord>();

            foreach (var kvp in _dataSource)
            {
                var filePath = GetFilePath(kvp.FileName);
                var text = await File.ReadAllTextAsync(filePath);

                var embedding = await _embeddingClient.GenerateEmbeddingAsync(text);

                var vectorSearchRecord = new VectorSearchRecord
                {
                    Id = kvp.ShipName,
                    Vector = embedding.Value.ToFloats().ToArray(),
                    Data = new Dictionary<string, string>()
                    {
                        ["Text"] = text
                    }
                };

                result.Add(vectorSearchRecord);
            }

            AnsiConsole.MarkupLineInterpolated($"*** [bold green]{result.Count} text chunks were vectorized successfully[/] ***");

            return result;
        }

        private void SaveEmbeddingsInVectorDb(IReadOnlyCollection<VectorSearchRecord> vectorSearchRecords)
        {
            AnsiConsole.MarkupLine("*** Saving vectors in the In-Memory Vector DB started ***");

            foreach (var vectorSearchRecord in vectorSearchRecords)
            {
                _inMemoryVectorDb.Index(vectorSearchRecord);
            }

            AnsiConsole.MarkupLineInterpolated($"*** [bold green]{vectorSearchRecords.Count} vectors were saved successfully in the In-Memory Vector DB[/] ***\n");
        }

        private async Task<string> GetAnswerAsync(string selectedSystemPrompt, string selectedQuestion, int topK = 3)
        {
            var queryVector = (await _embeddingClient.GenerateEmbeddingAsync(selectedQuestion)).Value.ToFloats().ToArray();
            var topNSimilarResults = _inMemoryVectorDb.Search(queryVector, topK);

            AnsiConsole.MarkupLine($"*** Top {topK} most similar vectors were found ***");
            foreach (var result in topNSimilarResults)
            {
                AnsiConsole.MarkupLineInterpolated($"Similarity score: [bold blue]{result.Similarity:0.00}[/], Id: {result.Id}");
            }
            AnsiConsole.WriteLine();

            ChatCompletion chatCompletion = await _chatClient.CompleteChatAsync(new List<ChatMessage>
            {
                new SystemChatMessage(selectedSystemPrompt),
                new UserChatMessage(CreateUserPrompt(selectedQuestion, topNSimilarResults))
            });

            return chatCompletion.Content[0].Text ?? "Empty response";
        }

        private string CreateUserPrompt(string question, IEnumerable<VectorSearchResult> topNSimilarResults)
        {
            var builder = new StringBuilder();

            builder.AppendLine("Here are the documents related to the question.");
            builder.AppendLine("Use only the information in these documents when answering.");
            builder.AppendLine();

            builder.AppendLine("=== Retrieved Documents ===");

            foreach (var kvp in topNSimilarResults.Select((result, index) => (Result: result, Index: index)))
            {
                builder.AppendLine();
                builder.AppendLine($"[Document {kvp.Index + 1}]");
                builder.AppendLine(kvp.Result.Data["Text"]);
            }

            builder.AppendLine();
            builder.AppendLine("=== User Question ===");
            builder.AppendLine(question);

            return builder.ToString();
        }

        private IReadOnlyCollection<string> GetAllSystemPrompts() => [GetDefaultSystemPrompt(), GetKidFriendlySystemPrompt(), GetMarketingSystemPrompt()];

        private string GetDefaultSystemPrompt()
        {
            return """
            You are a helpful assistant for the Galactic Voyages travel agency.
            Answer questions using only the information provided in the documents.
            Keep your answers clear and friendly.
            """;
        }

        private string GetKidFriendlySystemPrompt() 
        { 
            return """
            You explain things the way a kid would understand. 
            Keep answers short, simple, and fun. 
            Use playful analogies, like toys, snacks, or pets. 
            """; 
        }

        private string GetMarketingSystemPrompt()
        {
            return """
            You are a friendly travel agent for Galactic Voyages.
            Highlight the positive features of each ship.
            Keep the tone upbeat and reassuring.
            """;
        }

        private static string GetFilePath(string fileName)
        {
            return Path.Combine(AppContext.BaseDirectory, "Data", fileName);
        }

        private IReadOnlyCollection<string> GetAllQuestions() => [
            "How fast is the Nebula-X Cruiser?",
            "What fuel does the Ion-Drive Clipper use?",
            "Does the Aurora-Class Shuttle have artificial gravity?",
            "Which ship can travel the farthest without refueling?"
        ];
    }
}
