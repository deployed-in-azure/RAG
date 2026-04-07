using Microsoft.ML.Tokenizers;
using OpenAI.Embeddings;

namespace _06_ChunkingStrategies
{
    internal sealed class SemanticChunker(EmbeddingClient embeddingClient, Tokenizer tokenizer)
        : SemanticChunkerBase(embeddingClient, tokenizer)
    {
        public async Task<IReadOnlyList<string>> CreateChunksAsync(
            string text,
            double similarityThreshold = 0.75,
            int maxTokensPerChunk = 1024)
        {
            IReadOnlyList<string> sentences = SplitIntoSentences(text);

            if (sentences.Count <= 1)
                return sentences;

            float[][] embeddings = (await EmbeddingClient.GenerateEmbeddingsAsync(sentences))
                .Value
                .Select(e => e.ToFloats().ToArray())
                .ToArray();

            var chunks = new List<string>();
            var currentChunk = new List<string> { sentences[0] };
            int currentTokenCount = Tokenizer.CountTokens(sentences[0]);

            for (int i = 1; i < sentences.Count; i++)
            {
                int sentenceTokens = Tokenizer.CountTokens(sentences[i]);
                float similarity = CosineSimilarity(embeddings[i - 1], embeddings[i]);

                if (similarity < (float)similarityThreshold || currentTokenCount + sentenceTokens > maxTokensPerChunk)
                {
                    chunks.Add(string.Join(" ", currentChunk));
                    currentChunk = [];
                    currentTokenCount = 0;
                }

                currentChunk.Add(sentences[i]);
                currentTokenCount += sentenceTokens;
            }

            if (currentChunk.Count > 0)
                chunks.Add(string.Join(" ", currentChunk));

            return chunks;
        }
    }
}
