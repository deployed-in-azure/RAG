using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;
using OpenAI.Embeddings;
using Spectre.Console;
using System.Text.Json;

namespace _08_GraphRAG
{
    public class GraphRAGExample
    {
        private readonly ChatClient _chatClient;
        private readonly EmbeddingClient _embeddingClient;
        private readonly TextChunker _textChunker;
        private readonly GraphDb _graphDb;

        private readonly JsonSerializerOptions _jsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };

        public GraphRAGExample()
        {
            var openAiClient = new AzureOpenAIClient(
                new Uri(Environment.GetEnvironmentVariable("AZURE_OPEN_AI_CLIENT_URI")!),
                new DefaultAzureCredential());

            _chatClient = openAiClient.GetChatClient(Environment.GetEnvironmentVariable("AZURE_OPEN_AI_EMBEDDING_CHAT_CLIENT_DEPLOYMENT_NAME")!);
            _embeddingClient = openAiClient.GetEmbeddingClient(Environment.GetEnvironmentVariable("AZURE_OPEN_AI_EMBEDDING_CLIENT_DEPLOYMENT_NAME")!);
            _textChunker = new TextChunker();
            _graphDb = new GraphDb();
        }

        public async Task RunAsync()
        {
            while (true)
            {
                var action = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("What do you want to do?")
                        .AddChoices("Index", "Query", "Exit"));

                if (action == "Exit")
                {
                    break;
                }

                AnsiConsole.WriteLine();
                AnsiConsole.Clear();

                if (action == "Index")
                {
                    var markdown = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Data", "grounding-data-design.md"));

                    var perChunkKnowledgeGraphs = new List<KnowledgeGraph>();
                    foreach (var chunk in _textChunker.CreateChunks(markdown, chunkSize: 512, overlapPercentage: 10))
                    {
                        var knowledgeGraph = await GetKnowledgeGraphAsync(chunk);
                        PrintKnowledgeGraph(knowledgeGraph);
                        perChunkKnowledgeGraphs.Add(knowledgeGraph);
                    }

                    var aggregatedKnowledgeGraph = KnowledgeGraphAggregator.Merge(perChunkKnowledgeGraphs);
                    AnsiConsole.MarkupLine("[bold yellow]Aggregated Knowledge Graph[/]");
                    PrintKnowledgeGraph(aggregatedKnowledgeGraph);

                    await _graphDb.EnsureVectorIndexAsync();
                    await _graphDb.IngestGraphAsync(aggregatedKnowledgeGraph);
                    AnsiConsole.MarkupLine("[green]Indexing complete.[/]");
                }
                else
                {
                    var question = AnsiConsole.Ask<string>("Provide a [bold blue]question[/]:");
                    var topK = AnsiConsole.Ask<int>("Top K seed nodes:", 5);
                    var traversalDepth = AnsiConsole.Ask<int>("Traversal depth:", 1);
                    var minPathScore = AnsiConsole.Ask<float>("Min path score:", 0.5f);

                    var queryVector = (await _embeddingClient.GenerateEmbeddingAsync(question)).Value.ToFloats().ToArray();
                    var searchResult = await _graphDb.GetTopEntitiesAsync(queryVector, topK: topK, traversalDepth: traversalDepth, minPathScore: minPathScore);

                    AnsiConsole.MarkupLine($"[dim]Retrieved [bold]{searchResult.AllEntities.Count}[/] entities and [bold]{searchResult.Relationships.Count}[/] relationships (depth: {traversalDepth}, min score: {minPathScore:F2})[/]");
                    AnsiConsole.WriteLine();

                    AnsiConsole.Write(new Rule("[bold grey]Seed Entities[/]").RuleStyle("grey dim").LeftJustified());
                    foreach (var seed in searchResult.SeedEntities)
                    {
                        AnsiConsole.MarkupLine($"  [bold]{Markup.Escape(seed.Name)}[/] [dim]({Markup.Escape(seed.Type)})[/] — {Markup.Escape(seed.Description)}");
                    }

                    var graphContext = FormatKnowledgeGraphAsContext(searchResult);

                    AnsiConsole.Write(new Rule("[bold grey]Graph Context injected into prompt[/]").RuleStyle("grey dim").LeftJustified());
                    AnsiConsole.WriteLine(graphContext);

                    var answer = await _chatClient.CompleteChatAsync(
                    [
                        new SystemChatMessage(Prompts.GetRagSystemPrompt(graphContext)),
                        new UserChatMessage(question)
                    ]);

                    AnsiConsole.WriteLine();
                    AnsiConsole.Write(new Rule("[bold green]Answer[/]").RuleStyle("green dim").LeftJustified());
                    AnsiConsole.WriteLine(answer.Value.Content[0].Text);

                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[dim]Press [bold]Enter[/] to continue...[/]");
                    Console.ReadLine();
                    AnsiConsole.Clear();
                }
            }
        }

        private async Task<KnowledgeGraph> GetKnowledgeGraphAsync(string chunk)
        {
            ChatCompletion chatCompletion = await _chatClient.CompleteChatAsync(new List<ChatMessage>
            {
                new SystemChatMessage(Prompts.GetSystemPrompt()),
                new UserChatMessage(Prompts.GetUserPrompt(chunk))
            },
            new ChatCompletionOptions()
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    "knowledge_graph",
                    jsonSchema: BinaryData.FromString(JsonSchemas.KnowledgeGraph)),
                Temperature = 0
            });

            var result = JsonSerializer.Deserialize<KnowledgeGraph>(chatCompletion.Content[0].Text, _jsonSerializerOptions)
                ?? throw new InvalidOperationException("Failed to deserialize rewrites.");

            var embeddingInputs = result.Entities.Select(e => $"{e.Name}: {e.Description}").ToList();
            // in a real production app you should batch these embeddingInputs to not exceed some limits
            var embeddingResult = await _embeddingClient.GenerateEmbeddingsAsync(embeddingInputs);

            var entitiesWithEmbeddings = result.Entities
                .Zip(embeddingResult.Value, (entity, embedding) => entity with { Embedding = embedding.ToFloats().ToArray() })
                .ToList();

            return result with { Entities = entitiesWithEmbeddings };
        }

        private static string FormatKnowledgeGraphAsContext(GraphSearchResult searchResult)
        {
            var sb = new System.Text.StringBuilder();

            sb.AppendLine("## Entities");
            foreach (var entity in searchResult.AllEntities)
            {
                sb.AppendLine($"- {entity.Name} ({entity.Type}): {entity.Description}");
            }

            sb.AppendLine();
            sb.AppendLine("## Relationships");
            foreach (var rel in searchResult.Relationships)
            {
                sb.AppendLine($"- {rel.Source} --[{rel.Label}]--> {rel.Target} (weight: {rel.Weight:F2}): {rel.Description}");
            }

            return sb.ToString();
        }

        private void PrintKnowledgeGraph(KnowledgeGraph graph)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule($"[bold yellow]Knowledge Graph[/] [dim]({graph.Entities.Count} entities · {graph.Relationships.Count} relationships)[/]").RuleStyle("yellow dim").LeftJustified());
            AnsiConsole.WriteLine();

            var entityTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .AddColumn(new TableColumn("[bold grey]Name[/]"))
                .AddColumn(new TableColumn("[bold grey]Type[/]").NoWrap())
                .AddColumn(new TableColumn("[bold grey]Description[/]"));

            foreach (var entity in graph.Entities)
                entityTable.AddRow(
                    Markup.Escape(entity.Name),
                    Markup.Escape(entity.Type),
                    Markup.Escape(entity.Description));

            AnsiConsole.Write(new Panel(entityTable)
                .Header("[bold] Entities [/]")
                .BorderColor(Color.Grey)
                .Padding(1, 0));

            AnsiConsole.WriteLine();

            var relTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .AddColumn(new TableColumn("[bold grey]Source[/]").NoWrap())
                .AddColumn(new TableColumn("[bold grey]Label[/]").NoWrap().Centered())
                .AddColumn(new TableColumn("[bold grey]Target[/]").NoWrap())
                .AddColumn(new TableColumn("[bold grey]Weight[/]").NoWrap().Centered())
                .AddColumn(new TableColumn("[bold grey]Description[/]"));

            foreach (var rel in graph.Relationships)
                relTable.AddRow(
                    Markup.Escape(rel.Source),
                    $"[green]{Markup.Escape(rel.Label)}[/]",
                    Markup.Escape(rel.Target),
                    $"[dim]{rel.Weight:F2}[/]",
                    Markup.Escape(rel.Description));

            AnsiConsole.Write(new Panel(relTable)
                .Header("[bold] Relationships [/]")
                .BorderColor(Color.Grey)
                .Padding(1, 0));

            AnsiConsole.WriteLine();
        }
    }
}
