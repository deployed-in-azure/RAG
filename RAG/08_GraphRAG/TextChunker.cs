using Microsoft.ML.Tokenizers;

namespace _08_GraphRAG
{
    public class TextChunker(Tokenizer tokenizer)
    {
        public TextChunker() : this(TiktokenTokenizer.CreateForModel("text-embedding-ada-002")) { }
        public IEnumerable<string> CreateChunks(string text, int chunkSize, double overlapPercentage)
        {
            int overlapSize = (int)(chunkSize * overlapPercentage / 100.0);
            int stride = chunkSize - overlapSize;

            if (stride <= 0)
            {
                throw new ArgumentException(
                    $"overlapPercentage ({overlapPercentage}%) produces a non-positive stride. Keep it below 100%.",
                    nameof(overlapPercentage));
            }

            return ChunkIterator(text, chunkSize, stride);
        }

        private IEnumerable<string> ChunkIterator(string text, int chunkSize, int stride)
        {
            var encoded = tokenizer.EncodeToIds(text);
            int[] allTokenIds = encoded as int[] ?? [.. encoded];

            for (int i = 0; i < allTokenIds.Length; i += stride)
            {
                int end = Math.Min(i + chunkSize, allTokenIds.Length);
                string? decoded = tokenizer.Decode(new ArraySegment<int>(allTokenIds, i, end - i));

                if (!string.IsNullOrEmpty(decoded))
                {
                    yield return decoded;
                }

                if (end == allTokenIds.Length)
                {
                    yield break;
                }
            }
        }
    }
}
