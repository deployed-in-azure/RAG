namespace _08_GraphRAG
{
    public static class JsonSchemas
    {
        public const string KnowledgeGraph = """
            {
                "type": "object",
                "properties": {
                    "entities": {
                        "type": "array",
                        "items": {
                            "type": "object",
                            "properties": {
                                "name":        { "type": "string" },
                                "type":        { "type": "string" },
                                "description": { "type": "string" }
                            },
                            "required": ["name", "type", "description"],
                            "additionalProperties": false
                        }
                    },
                    "relationships": {
                        "type": "array",
                        "items": {
                            "type": "object",
                            "properties": {
                                "source":      { "type": "string" },
                                "target":      { "type": "string" },
                                "label":       { "type": "string" },
                                "description": { "type": "string" }
                            },
                            "required": ["source", "target", "label", "description"],
                            "additionalProperties": false
                        }
                    }
                },
                "required": ["entities", "relationships"],
                "additionalProperties": false
            }
            """;
    }
}
