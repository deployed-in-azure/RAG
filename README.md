# RAG
This repository explains various enterprise RAG patterns and techniques using real C# examples and various Azure services.

## Blog Posts

### 1. Naive RAG Explained: The Core Pattern

Learn how to build a Naive RAG system using C# and Microsoft Foundry. Ground LLMs in private markdown data for accurate FAQ bots.

To run the example, set the following environment variables:
- `AZURE_OPEN_AI_CLIENT_URI`: Your Microsoft Foundry endpoint URL.
- `AZURE_OPEN_AI_EMBEDDING_CLIENT_DEPLOYMENT_NAME`: Your embedding model deployment name e.g. `text-embedding-ada-002`
- `AZURE_OPEN_AI_EMBEDDING_CHAT_CLIENT_DEPLOYMENT_NAME`: Your LLM deployment name e.g. `gpt-4.1-mini`

Ensure that your identity has:
- the `Azure AI User` RBAC role assigned to access the Microsoft Foundry resource

[Read the blog post to find more details](https://deployedinazure.com/naive-rag-explained/)

### 2. Hybrid Search in RAG: BM25 + Vectors for Better Retrieval

Learn how to implement Hybrid Search in RAG using C#. Combine BM25 precision with Vector semantics in Azure AI Search for better retrieval.

To run the example, set the following environment variables:
- `AZURE_OPEN_AI_CLIENT_URI`: Your Microsoft Foundry endpoint URL.
- `AZURE_OPEN_AI_EMBEDDING_CLIENT_DEPLOYMENT_NAME`: Your embedding model deployment name e.g. `text-embedding-ada-002`
- `AZURE_OPEN_AI_EMBEDDING_CHAT_CLIENT_DEPLOYMENT_NAME`: Your LLM deployment name e.g. `gpt-4.1-mini`
- `AZURE_AI_SEARCH_URI`: Your Azure AI Search instance URL.
- `AZURE_AI_SEARCH_INDEX`: Your Azure AI Search index name (check the `Data/index_definition.json` file).

Ensure that your identity has:
- the `Azure AI User` RBAC role assigned to access the Microsoft Foundry resource
- the `Search Index Data Contributor` RBAC role assigned to access the Azure AI Search resource

[Read the blog post to find more details](https://deployedinazure.com/hybrid-search-in-rag-azure-ai-search/)

### 3. Semantic Ranking in Azure AI Search: How Cross-Encoders Improve RAG Retrieval

Semantic Ranking in Azure AI Search explained with cross‑encoders that boost RAG accuracy and improve retrieval relevance.

To run the example, set the following environment variables:
- `AZURE_OPEN_AI_CLIENT_URI`: Your Microsoft Foundry endpoint URL.
- `AZURE_OPEN_AI_EMBEDDING_CLIENT_DEPLOYMENT_NAME`: Your embedding model deployment name e.g. `text-embedding-ada-002`
- `AZURE_OPEN_AI_EMBEDDING_CHAT_CLIENT_DEPLOYMENT_NAME`: Your LLM deployment name e.g. `gpt-4.1-mini`
- `AZURE_AI_SEARCH_URI`: Your Azure AI Search instance URL.
- `AZURE_AI_SEARCH_INDEX`: Your Azure AI Search index name (check the `Data/index_definition.json` file).

Ensure that your identity has:
- the `Azure AI User` RBAC role assigned to access the Microsoft Foundry resource
- the `Search Index Data Contributor` RBAC role assigned to access the Azure AI Search resource

[Read the blog post to find more details](https://deployedinazure.com/semantic-ranking-in-azure-ai-search/)
