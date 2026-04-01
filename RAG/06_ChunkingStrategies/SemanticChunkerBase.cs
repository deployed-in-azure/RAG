using Microsoft.ML.Tokenizers;
using OpenAI.Embeddings;
using System.Numerics.Tensors;
using System.Text.RegularExpressions;

namespace _06_ChunkingStrategies
{
    internal abstract class SemanticChunkerBase(EmbeddingClient embeddingClient, Tokenizer tokenizer)
    {
        protected readonly EmbeddingClient EmbeddingClient = embeddingClient;
        protected readonly Tokenizer Tokenizer = tokenizer;

        protected static IReadOnlyList<string> SplitIntoSentences(string text)
        {
            // Primitive Pattern: Split on one or more spaces (\s+)
            // ONLY IF they are preceded by . ! or ? (?<=[.!?])
            string pattern = @"(?<=[.!?])\s+";

            return Regex.Split(text, pattern)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }

        protected static float CosineSimilarity(float[] a, float[] b) => TensorPrimitives.CosineSimilarity(a, b);
    }
}
