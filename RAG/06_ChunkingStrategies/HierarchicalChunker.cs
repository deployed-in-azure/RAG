using Microsoft.ML.Tokenizers;
using OpenAI.Embeddings;

namespace _06_ChunkingStrategies
{
    internal sealed class HierarchicalChunker(EmbeddingClient embeddingClient, Tokenizer tokenizer)
        : SemanticChunkerBase(embeddingClient, tokenizer)
    {
        public async Task<IReadOnlyList<(string Parent, string Child)>> CreateParentChildChunksAsync(
            string text,
            double similarityThreshold = 0.75,
            int maxTokensPerParentChunk = 1024,
            int maxTokensPerChildChunk = 256)
        {
            IReadOnlyList<string> sentences = SplitIntoSentences(text);

            if (sentences.Count == 0)
                return [];

            float[][] embeddings = (await EmbeddingClient.GenerateEmbeddingsAsync(sentences))
                .Value
                .Select(e => e.ToFloats().ToArray())
                .ToArray();

            // First pass: group sentence indices into parent chunks
            var parentGroups = new List<List<int>>();
            var currentParentIndices = new List<int> { 0 };
            int currentParentTokenCount = Tokenizer.CountTokens(sentences[0]);

            for (int i = 1; i < sentences.Count; i++)
            {
                int sentenceTokens = Tokenizer.CountTokens(sentences[i]);
                float similarity = CosineSimilarity(embeddings[i - 1], embeddings[i]);

                if (similarity < (float)similarityThreshold || currentParentTokenCount + sentenceTokens > maxTokensPerParentChunk)
                {
                    parentGroups.Add(currentParentIndices);
                    currentParentIndices = [];
                    currentParentTokenCount = 0;
                }

                currentParentIndices.Add(i);
                currentParentTokenCount += sentenceTokens;
            }

            if (currentParentIndices.Count > 0)
                parentGroups.Add(currentParentIndices);

            // Second pass: within each parent, sub-divide into child chunks
            var result = new List<(string Parent, string Child)>();

            foreach (var parentIndices in parentGroups)
            {
                string parentText = string.Join(" ", parentIndices.Select(i => sentences[i]));

                var currentChildIndices = new List<int> { parentIndices[0] };
                int currentChildTokenCount = Tokenizer.CountTokens(sentences[parentIndices[0]]);

                for (int j = 1; j < parentIndices.Count; j++)
                {
                    int idx = parentIndices[j];
                    int prevIdx = parentIndices[j - 1];
                    int sentenceTokens = Tokenizer.CountTokens(sentences[idx]);
                    float similarity = CosineSimilarity(embeddings[prevIdx], embeddings[idx]);

                    if (similarity < (float)similarityThreshold || currentChildTokenCount + sentenceTokens > maxTokensPerChildChunk)
                    {
                        result.Add((parentText, string.Join(" ", currentChildIndices.Select(i => sentences[i]))));
                        currentChildIndices = [];
                        currentChildTokenCount = 0;
                    }

                    currentChildIndices.Add(idx);
                    currentChildTokenCount += sentenceTokens;
                }

                if (currentChildIndices.Count > 0)
                    result.Add((parentText, string.Join(" ", currentChildIndices.Select(i => sentences[i]))));
            }

            return result;
        }
    }
}
