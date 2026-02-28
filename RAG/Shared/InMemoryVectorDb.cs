namespace Shared
{
    public class InMemoryVectorDb
    {
        private readonly Dictionary<string, VectorSearchRecord> _vectors = new();

        private int _supportedVectorDimension;

        public InMemoryVectorDb(int supportedVectorDimension = 1536)
        {
            _supportedVectorDimension = supportedVectorDimension;
        }

        public void Index(VectorSearchRecord? vectorDocument)
        {
            ArgumentNullException.ThrowIfNull(vectorDocument);

            if (vectorDocument.Vector.Length != _supportedVectorDimension)
            {
                throw new InvalidOperationException($"Invalid vector dimension. The only supported dimension is {_supportedVectorDimension}.");
            }

            if (_vectors.ContainsKey(vectorDocument.Id))
            {
                throw new InvalidOperationException($"A document with ID '{vectorDocument.Id}' already exists.");
            }

            _vectors[vectorDocument.Id] = vectorDocument;
        }

        public IReadOnlyCollection<VectorSearchResult> Search(float[] queryVector, int topK)
        {
            if (queryVector.Length != _supportedVectorDimension)
            {
                throw new InvalidOperationException($"Invalid vector dimension. The only supported dimension is {_supportedVectorDimension}.");
            }

            if (topK <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(topK), "topK must be greater than zero.");
            }

            return _vectors.Values
                .Select(record => new
                {
                    Document = record,
                    Similarity = CosineSimilarity(queryVector, record.Vector)
                })
                .OrderByDescending(x => x.Similarity)
                .Take(topK)
                .Select(x => new VectorSearchResult
                {
                    Id = x.Document.Id,
                    Similarity = x.Similarity,
                    Data = x.Document.Data
                })
                .ToList();
        }

        private static double CosineSimilarity(float[] vectorA, float[] vectorB)
        {
            var dotProduct = 0.0;
            var sumSquaresA = 0.0;
            var sumSquaresB = 0.0;

            for (var i = 0; i < vectorA.Length; i++)
            {
                var a = vectorA[i];
                var b = vectorB[i];
                dotProduct += a * b;
                sumSquaresA += a * a;
                sumSquaresB += b * b;
            }

            var vectorALength = Math.Sqrt(sumSquaresA);
            var vectorBLength = Math.Sqrt(sumSquaresB);
            var denominator = vectorALength * vectorBLength;
            var cosine = Math.Round(denominator == 0.0 ? 0.0 : dotProduct / denominator, 2);

            return cosine;
        }
    }
}
