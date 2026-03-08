using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using OpenAI.Embeddings;

namespace _02_HybridRAG
{
    public class SearchService(SearchClient searchClient, EmbeddingClient embeddingClient)
    {
        public async Task<IReadOnlyList<StarshipSearchDocumentResult>> InvokeFullTextSearchAsync(string question, SearchOptions searchOptions)
        {
            var response = await searchClient.SearchAsync<StarshipSearchDocumentResult>(question, searchOptions);
            return await CollectDocumentsAsync(response.Value);
        }

        public async Task<IReadOnlyList<StarshipSearchDocumentResult>> InvokeVectorSearchAsync(string question, SearchOptions searchOptions)
        {
            var embedding = await embeddingClient.GenerateEmbeddingAsync(question);
            var queryVector = embedding.Value.ToFloats().ToArray();

            searchOptions.VectorSearch = new VectorSearchOptions
            {
                Queries =
                {
                    new VectorizedQuery(queryVector)
                    {
                        KNearestNeighborsCount = searchOptions.Size,
                        Fields = { nameof(StarshipSearchDocument.OverviewVector) }
                    }
                }
            };

            var response = await searchClient.SearchAsync<StarshipSearchDocumentResult>(searchText: null, searchOptions);
            return await CollectDocumentsAsync(response.Value);
        }

        public async Task<IReadOnlyList<StarshipSearchDocumentResult>> InvokeHybridFullTextVectorAsync(string question, SearchOptions searchOptions)
        {
            var embedding = await embeddingClient.GenerateEmbeddingAsync(question);
            var queryVector = embedding.Value.ToFloats().ToArray();

            searchOptions.VectorSearch = new VectorSearchOptions
            {
                Queries =
                {
                    new VectorizedQuery(queryVector)
                    {
                        KNearestNeighborsCount = searchOptions.Size,
                        Fields = { nameof(StarshipSearchDocument.OverviewVector) },
                        Weight = 2.0f
                    }
                }
            };

            var response = await searchClient.SearchAsync<StarshipSearchDocumentResult>(question, searchOptions);
            return await CollectDocumentsAsync(response.Value);
        }

        public async Task<IReadOnlyList<StarshipSearchDocumentResult>> InvokeVectorVectorAsync(string question, SearchOptions searchOptions)
        {
            var embedding = await embeddingClient.GenerateEmbeddingAsync(question);
            var queryVector = embedding.Value.ToFloats().ToArray();

            searchOptions.VectorSearch = BuildVectorSearchOptions(
                queryVector,
                searchOptions.Size ?? 3,
                nameof(StarshipSearchDocument.OverviewVector),
                nameof(StarshipSearchDocument.NotesVector));

            var response = await searchClient.SearchAsync<StarshipSearchDocumentResult>(searchText: null, searchOptions);
            return await CollectDocumentsAsync(response.Value);
        }

        public async Task<IReadOnlyList<StarshipSearchDocumentResult>> InvokeHybridFullTextVectorVectorAsync(string question, SearchOptions searchOptions)
        {
            var embedding = await embeddingClient.GenerateEmbeddingAsync(question);
            var queryVector = embedding.Value.ToFloats().ToArray();

            searchOptions.VectorSearch = BuildVectorSearchOptions(
                queryVector,
                searchOptions.Size ?? 3,
                nameof(StarshipSearchDocument.OverviewVector),
                nameof(StarshipSearchDocument.NotesVector));

            var response = await searchClient.SearchAsync<StarshipSearchDocumentResult>(question, searchOptions);
            return await CollectDocumentsAsync(response.Value);
        }

        private static VectorSearchOptions BuildVectorSearchOptions(float[] queryVector, int k, params string[] fields)
        {
            var vectorSearchOptions = new VectorSearchOptions();

            foreach (var field in fields)
            {
                vectorSearchOptions.Queries.Add(new VectorizedQuery(queryVector)
                {
                    KNearestNeighborsCount = k,
                    Fields = { field },
                    // check how Weight changes the final relevance
                    //Weight = field == nameof(StarshipSearchDocument.OverviewVector) ? 3 : 1
                });
            }

            return vectorSearchOptions;
        }

        private static async Task<IReadOnlyList<StarshipSearchDocumentResult>> CollectDocumentsAsync(SearchResults<StarshipSearchDocumentResult> results)
        {
            var documents = new List<StarshipSearchDocumentResult>();

            await foreach (var result in results.GetResultsAsync())
            {
                documents.Add(result.Document with { Score = result.Score.GetValueOrDefault() });
            }

            return documents;
        }
    }
}
