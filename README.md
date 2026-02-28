# RAG
This repository explains various enterprise RAG patterns and techniques using real C# examples and various Azure services.

## Blog Posts

### 1. Naive RAG Explained: The Core Pattern

Learn how to build a Naive RAG system using C# and Microsoft Foundry. Ground LLMs in private markdown data for accurate FAQ bots.

To run the examples, set the following environment variables:
- `AZURE_OPEN_AI_CLIENT_URI`: Your Microsoft Foundry endpoint URL.
- `AZURE_OPEN_AI_EMBEDDING_CLIENT_DEPLOYMENT_NAME`: Your embedding model deployment name e.g. `text-embedding-ada-002`
- `AZURE_OPEN_AI_EMBEDDING_CHAT_CLIENT_DEPLOYMENT_NAME`: Your LLM deployment name e.g. `gpt-4.1-mini`

Ensure that your identity has:
- the `Azure AI User` RBAC role assigned to access the Microsoft Foundry resource

[Read the blog post to find more details](https://deployedinazure.com/naive-rag-explained/)
