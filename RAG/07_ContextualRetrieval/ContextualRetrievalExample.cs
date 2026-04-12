using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.ML.Tokenizers;
using OpenAI.Chat;
using OpenAI.Embeddings;
using Spectre.Console;

namespace _07_ContextualRetrieval
{
    public class ContextualRetrievalExample
    {
        private readonly ChatClient _chatClient;
        private readonly EmbeddingClient _embeddingClient;
        private readonly Tokenizer _tokenizer;

        private record GeneratedContext(string Value, int CachedInputTokensCount);
        private record EnrichedChunk(string Context, string Chunk);

        public ContextualRetrievalExample()
        {
            var openAiClient = new AzureOpenAIClient(
                new Uri(Environment.GetEnvironmentVariable("AZURE_OPEN_AI_CLIENT_URI")!),
                new DefaultAzureCredential());

            _chatClient = openAiClient.GetChatClient(Environment.GetEnvironmentVariable("AZURE_OPEN_AI_EMBEDDING_CHAT_CLIENT_DEPLOYMENT_NAME")!);
            _embeddingClient = openAiClient.GetEmbeddingClient(Environment.GetEnvironmentVariable("AZURE_OPEN_AI_EMBEDDING_CLIENT_DEPLOYMENT_NAME")!);
            _tokenizer = TiktokenTokenizer.CreateForModel("text-embedding-ada-002");
        }

        public async Task RunAsync()
        {
            var markdown = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Data", "remote_work_policy_pl.md"));

            while (true)
            {
                var chunks = CreateChunks(markdown, chunkSize: 512, overlapPercentage: 25);
                int chunkIndex = 0;

                foreach (var chunk in chunks)
                {
                    var context = await GetContext(markdown, chunk);

                    // Prepend the generated context sentence to the raw chunk — this is the core of the Contextual Retrieval pattern.
                    // The model-generated sentence situates the chunk within the broader document, making it self-contained for retrieval.
                    var enrichedChunk = new EnrichedChunk(context.Value, chunk);

                    // Embed the enriched text (context + chunk) so the vector captures full situational meaning,
                    // not just the isolated chunk's semantics.
                    var embedding = (await _embeddingClient.GenerateEmbeddingAsync($"{enrichedChunk.Context} \n {enrichedChunk.Chunk}")).Value.ToFloats();

                    // Store the enriched text and its vector. The enriched form benefits both retrieval paths:
                    // vector search (semantic similarity) and BM25 full-text search (keyword overlap).
                    var searchDocument = new SearchDocumentModel()
                    {
                        id = Guid.NewGuid().ToString(),
                        EnrichedChunk = $"{enrichedChunk.Context} \n {enrichedChunk.Chunk}",
                        EnrichedChunkVector = embedding.ToArray()
                    };

                    chunkIndex++;

                    var content = new Rows(
                        new Markup("[bold yellow]Context[/]"),
                        new Text(enrichedChunk.Context),
                        new Rule(),
                        new Markup("[bold steelblue1]Chunk[/]"),
                        new Text(enrichedChunk.Chunk),
                        new Rule(),
                        new Markup($"[dim]Cached tokens: {context.CachedInputTokensCount}[/]")
                    );

                    AnsiConsole.Write(new Panel(content)
                    {
                        Header = new PanelHeader($" [bold green]Chunk {chunkIndex}[/] "),
                        Border = BoxBorder.Rounded,
                        Padding = new Padding(1, 0),
                        Expand = true
                    });
                }

                AnsiConsole.WriteLine();
                if (AnsiConsole.Confirm("Continue?")) { AnsiConsole.Clear(); } else break;
            }
        }

        private async Task<GeneratedContext> GetContext(string documentContent, string chunkContent)
        {
            var prompt = GetContextEnrichmentPrompt(documentContent, chunkContent);

            ChatCompletion chatCompletion = await _chatClient.CompleteChatAsync(new UserChatMessage(prompt));

            return new GeneratedContext(chatCompletion.Content[0].Text ?? "", chatCompletion.Usage.InputTokenDetails.CachedTokenCount);
        }

        private string GetContextEnrichmentPrompt(string documentContent, string chunkContent)
        {
            return $"""
                ### System Instructions
                You are a retrieval-augmentation specialist. Your goal is to prepend a 1-sentence situational context to a text chunk.
                Focus on: 
                1. Who/What/Where (Entities).
                2. Document Subject (The 'Global' context).
                3. Critical IDs or Dates.
                4. Versions/References

                ### Document
                {documentContent}

                ### Chunk
                {chunkContent}

                ### Output Requirement
                Provide ONLY the 1-sentence context. Do not include 'The context is...' or any preamble. 
                Goal: Improve keyword and vector match for search.
                """;
        }

        private IEnumerable<string> CreateChunks(string text, int chunkSize, double overlapPercentage)
        {
            int overlapSize = (int)(chunkSize * overlapPercentage / 100.0);
            int stride = chunkSize - overlapSize;

            if (stride <= 0)
                throw new ArgumentException(
                    $"overlapPercentage ({overlapPercentage}%) produces a non-positive stride. Keep it below 100%.",
                    nameof(overlapPercentage));

            return ChunkIterator(text, chunkSize, stride);
        }

        private IEnumerable<string> ChunkIterator(string text, int chunkSize, int stride)
        {
            var encoded = _tokenizer.EncodeToIds(text);
            int[] allTokenIds = encoded as int[] ?? [.. encoded];

            for (int i = 0; i < allTokenIds.Length; i += stride)
            {
                int end = Math.Min(i + chunkSize, allTokenIds.Length);
                string? decoded = _tokenizer.Decode(new ArraySegment<int>(allTokenIds, i, end - i));

                if (!string.IsNullOrEmpty(decoded))
                    yield return decoded;

                if (end == allTokenIds.Length)
                    yield break;
            }
        }
    }
}
