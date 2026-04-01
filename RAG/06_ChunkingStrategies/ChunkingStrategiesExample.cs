using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.ML.Tokenizers;
using Spectre.Console;

namespace _06_ChunkingStrategies
{
    public class ChunkingStrategiesExample
    {
        private readonly FixedSizeChunker _fixedSizeChunker;
        private readonly SemanticChunker _semanticChunker;
        private readonly HierarchicalChunker _hierarchicalChunker;

        public ChunkingStrategiesExample()
        {
            var openAiClient = new AzureOpenAIClient(
                new Uri(Environment.GetEnvironmentVariable("AZURE_OPEN_AI_CLIENT_URI")!),
                new DefaultAzureCredential());

            var embeddingClient = openAiClient.GetEmbeddingClient(
                Environment.GetEnvironmentVariable("AZURE_OPEN_AI_EMBEDDING_CLIENT_DEPLOYMENT_NAME")!);

            var tokenizer = TiktokenTokenizer.CreateForModel("text-embedding-ada-002");

            _fixedSizeChunker = new FixedSizeChunker(tokenizer);
            _semanticChunker = new SemanticChunker(embeddingClient, tokenizer);
            _hierarchicalChunker = new HierarchicalChunker(embeddingClient, tokenizer);
        }

        public async Task RunAsync()
        {
            const int chunkSize = 512;
            const double overlapPercentage = 25.0;

            var markdown = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Data", "grounding-data-design.md"));

            while (true)
            {
                var selectedStrategy = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select a [bold blue]chunking strategy[/]:")
                        .AddChoices([
                            "Fixed-size with overlap",
                            "Semantic chunking",
                            "Hierarchical (Parent-Child)"
                        ]));

                AnsiConsole.MarkupLineInterpolated($"Selected strategy:\n [bold italic blue]{selectedStrategy}[/]\n");

                switch (selectedStrategy)
                {
                    case "Fixed-size with overlap":
                        RenderSettingsTable(chunkSize, overlapPercentage);
                        RenderChunks(_fixedSizeChunker.CreateChunks(markdown, chunkSize, overlapPercentage));
                        break;

                    case "Semantic chunking":
                        RenderChunks(await _semanticChunker.CreateChunksAsync(markdown, similarityThreshold: 0.75, maxTokensPerChunk: 512));
                        break;

                    case "Hierarchical (Parent-Child)":
                        RenderChunks(await _hierarchicalChunker.CreateParentChildChunksAsync(markdown));
                        break;
                }

                AnsiConsole.WriteLine();
                if (AnsiConsole.Confirm("Continue?")) { AnsiConsole.Clear(); } else break;
            }
        }

        private static void RenderSettingsTable(int chunkSize, double overlapPercentage)
        {
            int overlapTokens = (int)(chunkSize * overlapPercentage / 100.0);
            int stride = chunkSize - overlapTokens;

            var table = new Table()
                .Centered()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .AddColumn(new TableColumn("[bold]Parameter[/]"))
                .AddColumn(new TableColumn("[bold]Value[/]").RightAligned());

            table.AddRow("Chunk size",   $"[green]{chunkSize} tokens[/]");
            table.AddRow("Overlap",      $"[green]{overlapPercentage}%[/]");
            table.AddRow("Overlap size", $"[green]{overlapTokens} tokens[/]");
            table.AddRow("Stride",       $"[green]{stride} tokens[/]");

            AnsiConsole.Write(new Rule("[bold yellow]Chunking Settings[/]").RuleStyle("grey50"));
            AnsiConsole.Write(table);
        }

        private static void RenderChunks(IEnumerable<string> chunks)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[bold yellow]Chunks[/]").RuleStyle("grey50"));
            AnsiConsole.WriteLine();

            int count = 0;
            foreach (var chunk in chunks)
            {
                count++;
                AnsiConsole.Write(
                    new Panel(Markup.Escape(chunk))
                        .Header($"[bold steelblue1] Chunk {count} [/]")
                        .BorderColor(Color.SteelBlue1)
                        .Padding(1, 0, 1, 0));
            }

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule($"[grey]Total: {count} chunk(s)[/]").RuleStyle("grey50"));
        }

        private static void RenderChunks(IEnumerable<(string Parent, string Child)> chunks)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[bold yellow]Chunks[/]").RuleStyle("grey50"));
            AnsiConsole.WriteLine();

            int parentCount = 0;
            int totalChildCount = 0;
            string? currentParent = null;
            int childIndexInParent = 0;

            foreach (var (parent, child) in chunks)
            {
                if (parent != currentParent)
                {
                    parentCount++;
                    childIndexInParent = 0;
                    currentParent = parent;

                    AnsiConsole.WriteLine();
                    AnsiConsole.Write(
                        new Panel(Markup.Escape(parent))
                            .Header($"[bold yellow] Parent {parentCount} [/]")
                            .BorderColor(Color.Yellow)
                            .Padding(1, 0, 1, 0));
                }

                childIndexInParent++;
                totalChildCount++;

                AnsiConsole.Write(
                    new Panel(Markup.Escape(child))
                        .Header($"[bold steelblue1] Child {parentCount}.{childIndexInParent} [/]")
                        .BorderColor(Color.SteelBlue1)
                        .Padding(1, 0, 1, 0));
            }

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule($"[grey]Total: {parentCount} parent(s), {totalChildCount} child(ren)[/]").RuleStyle("grey50"));
        }
    }
}
