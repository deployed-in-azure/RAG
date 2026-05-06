using Azure.Identity;
using Azure.Search.Documents.KnowledgeBases;
using Azure.Search.Documents.KnowledgeBases.Models;

namespace _09_FoundryIQ
{
    public class FoundryIQExample
    {
        public async Task RunAsync()
        {
            var userQuery = "What are the requirements for the CTO position in Contoso and what is the data retention policy for the low business value data in Contoso and can I visit a dentist for free?";

            var kb = new KnowledgeBaseRetrievalClient(
                new Uri(Environment.GetEnvironmentVariable("AZURE_AI_SEARCH_URI")!), 
                knowledgeBaseName: Environment.GetEnvironmentVariable("AZURE_AI_SEARCH_KNOWLEDGE_BASE")!, 
                new DefaultAzureCredential());

            var kbRetrievalRequest = new KnowledgeBaseRetrievalRequest()
            {
                IncludeActivity = true,
                //Intents =
                //{
                //    new KnowledgeRetrievalSemanticIntent(userQuery)
                //},
                KnowledgeSourceParams =
                {
                    new AzureBlobKnowledgeSourceParams("knowledge-source-contoso-cloud")
                    {
                        AlwaysQuerySource = false,
                        //IncludeReferences = true,
                        //IncludeReferenceSourceData = true,
                        //RerankerThreshold = 3.0f
                    },
                    new AzureBlobKnowledgeSourceParams("knowledge-source-health-plans")
                    {
                        AlwaysQuerySource = false,
                        //IncludeReferences = true,
                        //IncludeReferenceSourceData = true,
                        //RerankerThreshold = 3.0f
                    },
                    new AzureBlobKnowledgeSourceParams("knowledge-source-job-roles")
                    {
                        AlwaysQuerySource = false,
                        //IncludeReferences = true,
                        //IncludeReferenceSourceData = true,
                        //RerankerThreshold = 3.0f
                    },
                    //new IndexedOneLakeKnowledgeSourceParams("ksourceName")
                    //{
                    //    AlwaysQuerySource = true,
                    //    IncludeReferences = true,
                    //    IncludeReferenceSourceData = true,
                    //    RerankerThreshold = 0.70f
                    //},
                    //new IndexedSharePointKnowledgeSourceParams("ksourceName")
                    //{
                    //    AlwaysQuerySource = true,
                    //    IncludeReferences = true,
                    //    IncludeReferenceSourceData = true,
                    //    RerankerThreshold = 0.70f
                    //},
                    //new RemoteSharePointKnowledgeSourceParams("kssourceName")
                    //{
                    //    AlwaysQuerySource = true,
                    //    IncludeReferences = true,
                    //    IncludeReferenceSourceData = true,
                    //    RerankerThreshold = 0.70f,

                    //    FilterExpressionAddOn = "KeywordQueryLanguage filter"
                    //},
                    //new SearchIndexKnowledgeSourceParams("ksourceName")
                    //{
                    //    AlwaysQuerySource = true,
                    //    IncludeReferences = true,
                    //    IncludeReferenceSourceData = true,
                    //    RerankerThreshold = 0.70f,

                    //    FilterAddOn = "Location eq 'Warsaw'"
                    //},
                    //new WebKnowledgeSourceParams("ksourceName")
                    //{
                    //    AlwaysQuerySource = true,
                    //    IncludeReferences = true,
                    //    IncludeReferenceSourceData = true,
                    //    RerankerThreshold = 0.70f,

                    //    Count = 55,
                    //    Freshness = "freshness",
                    //    Language = "pl",
                    //    Market = "pl",
                    //}
                },
                //MaxOutputSize = 5, // > 5000
                //MaxRuntimeInSeconds = 10, // 10 - 600
                Messages =
                {
                    new KnowledgeBaseMessage([new KnowledgeBaseMessageTextContent(userQuery)])
                    {
                        Role = "user"
                    }
                },
                OutputMode = KnowledgeRetrievalOutputMode.AnswerSynthesis, // AnswerSynthesis
                RetrievalReasoningEffort = new KnowledgeRetrievalMediumReasoningEffort(),
            };

            var result = await kb.RetrieveAsync(kbRetrievalRequest);
            var json = result.GetRawResponse().Content.ToString();
            var text = (result.Value.Response[0].Content[0] as KnowledgeBaseMessageTextContent)!.Text;

            Console.WriteLine(text);
        }
    }
}
