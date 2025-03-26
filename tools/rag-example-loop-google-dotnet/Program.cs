using Google.Cloud.AIPlatform.V1;
using Google.Apis.Auth.OAuth2;
using Google.Protobuf.WellKnownTypes;
using Google.Protobuf.Collections;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Type = Google.Cloud.AIPlatform.V1.Type;
using Value = Google.Protobuf.WellKnownTypes.Value;

namespace ContosoAssistantSample
{
    // Extension method for RepeatedField to add ranges of items
    public static class ProtobufExtensions
    {
        public static void AddRange<T>(this RepeatedField<T> field, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                field.Add(item);
            }
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Contoso Assistant using Google Vertex AI...");
            var assistant = new ContosoAssistant();

            // Example queries to test the assistant
            string[] sampleQueries = new[]
            {
                "tell me about our different roles",
                "what health plans do we offer?",
                "tell me about the company values"
            };

            foreach (var query in sampleQueries)
            {
                Console.WriteLine("\n\n=============================================");
                Console.WriteLine($"USER: {query}");
                Console.WriteLine("=============================================");

                string response = await assistant.ProcessQueryAsync(query);

                Console.WriteLine("\nASSISTANT: ");
                Console.WriteLine(response);
            }
        }
    }

    public class ContosoAssistant
    {
        private readonly PredictionServiceClient _predictionServiceClient;
        private readonly string _projectId;
        private readonly string _location;
        private readonly string _publisher;
        private readonly string _model;
        private readonly DocumentRepository _documentRepository;
        private const int MAX_ITERATIONS = 10;

        public ContosoAssistant()
        {
            // Use the credential loading approach from the example
            var (projectId, serviceAccountKey) = LoadCredentials();
            if (projectId == null || serviceAccountKey == null)
            {
                throw new InvalidOperationException("Failed to load credentials.");
            }

            _projectId = projectId;
            _location = "us-central1";
            _publisher = "google";
            _model = "gemini-2.0-flash";

            // Create the client with proper credentials
            var credential = GoogleCredential.FromJson(serviceAccountKey)
                .CreateScoped("https://www.googleapis.com/auth/cloud-platform");
            _predictionServiceClient = new PredictionServiceClientBuilder
            {
                Endpoint = $"{_location}-aiplatform.googleapis.com",
                Credential = credential
            }.Build();

            // Initialize the document repository
            _documentRepository = new DocumentRepository();
        }

        private static (string projectId, string serviceAccountKey) LoadCredentials()
        {
            try
            {
                string projectRoot = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.Parent.Parent
                    .FullName;
                string apiKeysPath = Path.Combine(projectRoot, "API_KEYS.json");
                string jsonContent = File.ReadAllText(apiKeysPath);
                using JsonDocument doc = JsonDocument.Parse(jsonContent);
                var googleCloud = doc.RootElement.GetProperty("googleCloud");
                string projectId = googleCloud.GetProperty("projectId").GetString();
                string serviceAccountKey = googleCloud.GetProperty("serviceAccountKey").ToString();
                return (projectId, serviceAccountKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading credentials: {ex.Message}");
                return (null, null);
            }
        }

        public async Task<string> ProcessQueryAsync(string query)
        {
            // Define the user's prompt
            var userPromptContent = new Content
            {
                Role = "USER",
                Parts =
                {
                    new Part { Text = query }
                }
            };

            // Define the tools for function calling
            var searchDocumentsFunc = new FunctionDeclaration
            {
                Name = "search_documents",
                Description =
                    "Search for documents based on a query - ALWAYS USE THIS FIRST for company-specific questions",
                Parameters = new OpenApiSchema
                {
                    Type = Type.Object,
                    Properties =
                    {
                        ["query"] = new OpenApiSchema
                        {
                            Type = Type.String,
                            Description = "The search query"
                        }
                    },
                    Required = { "query" }
                }
            };

            var getDocumentFunc = new FunctionDeclaration
            {
                Name = "get_document_content",
                Description = "Get the content of a document by its ID - use after search_documents returns results",
                Parameters = new OpenApiSchema
                {
                    Type = Type.Object,
                    Properties =
                    {
                        ["documentId"] = new OpenApiSchema
                        {
                            Type = Type.String,
                            Description = "The ID of the document to retrieve"
                        }
                    },
                    Required = { "documentId" }
                }
            };

            // Keep all messages for the full conversation context
            List<Content> conversationHistory = new List<Content>
            {
                userPromptContent
            };

            // Define the system instructions
            string systemPrompt = @"
## System Role
You are an AI assistant designed to answer questions for Contoso Electronics, an aerospace industry company providing advanced electronic components for commercial and military aircraft. 

## IMPORTANT: Use of Tools
- You DO NOT have up-to-date information about Contoso Electronics in your training data
- For ANY company-specific information, you MUST use the search_documents tool first
- NEVER answer questions about Contoso Electronics company-specific details without first searching the documents
- This includes questions about roles, benefits, values, policies, procedures, etc.

## Tool Usage Instructions
1. For ANY question about Contoso Electronics, ALWAYS use the search_documents tool first
2. After finding relevant documents, use get_document_content to retrieve specific document content
3. Only then formulate your response based on the document content
4. If search returns no relevant documents, inform the user you couldn't find information on that topic

## Document Search Process
1. For ANY query about Contoso Electronics, use search_documents tool with relevant keywords
2. Review the search results and identify the most relevant document(s)
3. Use the get_document_content tool with the exact documentId returned from the search
4. Base your response on the document content, citing specific information

## Response Guidelines
- Always provide detailed, accurate responses based on document content
- Be transparent if you can't find relevant information
- Format your responses in clear, well-structured markdown
- Do not hallucinate or make up information about the company
- When referencing documents, include specific excerpts within your answer
";

            // Create the initial request with SystemInstruction property
            var generateContentRequest = new GenerateContentRequest
            {
                Model = $"projects/{_projectId}/locations/{_location}/publishers/{_publisher}/models/{_model}",
                GenerationConfig = new GenerationConfig
                {
                    Temperature = 0.2f
                },
                SystemInstruction = new Content
                {
                    Parts = { new Part { Text = systemPrompt } }
                },
                Tools =
                {
                    new Tool
                    {
                        FunctionDeclarations = { searchDocumentsFunc, getDocumentFunc }
                    }
                }
            };

            // Add contents from conversation history
            generateContentRequest.Contents.AddRange(conversationHistory);

            int currentIteration = 0;

            // Main conversation loop
            do
            {
                currentIteration++;
                Console.WriteLine($"[DEBUG] Iteration {currentIteration}");

                if (currentIteration > MAX_ITERATIONS)
                {
                    throw new InvalidOperationException($"Exceeded maximum iterations ({MAX_ITERATIONS})");
                }

                // Get completion from the API
                GenerateContentResponse response =
                    await _predictionServiceClient.GenerateContentAsync(generateContentRequest);
                var assistantContent = response.Candidates[0].Content;

                // Add the assistant's response to conversation history
                conversationHistory.Add(assistantContent);

                // Check if there is a function call
                var functionCall = assistantContent.Parts[0].FunctionCall;
                if (functionCall != null)
                {
                    Console.WriteLine($"[DEBUG] Function called: {functionCall.Name}");

                    // Execute the appropriate function
                    string functionResult = "";
                    if (functionCall.Name == "search_documents" && functionCall.Args.Fields.ContainsKey("query"))
                    {
                        string searchQuery = functionCall.Args.Fields["query"].StringValue;
                        Console.WriteLine($"[DEBUG] Searching for documents with query: {searchQuery}");
                        functionResult = _documentRepository.SearchDocuments(searchQuery);
                    }
                    else if (functionCall.Name == "get_document_content" &&
                             functionCall.Args.Fields.ContainsKey("documentId"))
                    {
                        string documentId = functionCall.Args.Fields["documentId"].StringValue;
                        Console.WriteLine($"[DEBUG] Getting document with ID: {documentId}");
                        functionResult = _documentRepository.GetDocumentContent(documentId);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unexpected function call: {functionCall.Name}");
                    }

                    // Add function response to conversation history
                    var functionResponseContent = new Content
                    {
                        Parts =
                        {
                            new Part
                            {
                                FunctionResponse = new FunctionResponse
                                {
                                    Name = functionCall.Name,
                                    Response = new Struct
                                    {
                                        Fields =
                                        {
                                            { "result", Value.ForString(functionResult) }
                                        }
                                    }
                                }
                            }
                        }
                    };
                    conversationHistory.Add(functionResponseContent);

                    // Create a new request with the updated conversation history
                    generateContentRequest = new GenerateContentRequest
                    {
                        Model = $"projects/{_projectId}/locations/{_location}/publishers/{_publisher}/models/{_model}",
                        GenerationConfig = new GenerationConfig
                        {
                            Temperature = 0.2f
                        },
                        SystemInstruction = new Content
                        {
                            Parts = { new Part { Text = systemPrompt } }
                        },
                        Tools =
                        {
                            new Tool
                            {
                                FunctionDeclarations = { searchDocumentsFunc, getDocumentFunc }
                            }
                        }
                    };

                    // Use our extension method to add the conversation history
                    generateContentRequest.Contents.AddRange(conversationHistory);
                }
                else
                {
                    // No function call means we got a final text response
                    return assistantContent.Parts[0].Text;
                }
            } while (true);
        }
    }

    public class DocumentRepository
    {
        private readonly Dictionary<string, Document> _documents;

        public DocumentRepository()
        {
            // Initialize documents based on the PDFs
            _documents = new Dictionary<string, Document>
            {
                {
                    "DOC001",
                    new Document
                    {
                        Id = "DOC001",
                        Title = "Contoso Electronics Plan and Benefit Packages",
                        Summary =
                            "Information about Contoso Electronics health plans including Northwind Health Plus and Northwind Standard, their coverage details, and cost comparison.",
                        Content =
                            "Contoso Electronics offers two health plan options to employees: Northwind Health Plus and Northwind Standard.\n\nNorthwind Health Plus is a comprehensive plan providing coverage for medical, vision, and dental services. It includes prescription drug coverage, mental health and substance abuse coverage, and preventive care services. The plan covers emergency services both in-network and out-of-network, and offers coverage for hospital stays, doctor visits, lab tests, and X-rays. It covers generic, brand-name, and specialty drugs, as well as vision exams, glasses, contact lenses, dental exams, cleanings, and fillings.\n\nNorthwind Standard is a basic plan with more limited coverage. While it provides coverage for medical, vision, and dental services and preventive care, it does not cover emergency services, mental health and substance abuse, or out-of-network services. It only covers doctor visits and lab tests (not hospital stays), and only covers generic and brand-name drugs (not specialty drugs). It only covers vision exams and glasses, not contact lenses or dental services beyond basic exams.\n\nBoth plans offer preventive care coverage including routine physicals, well-child visits, immunizations, mammograms, colonoscopies, and other cancer screenings.\n\nCost Comparison: Contoso Electronics deducts the employee's portion of healthcare costs from each paycheck, spreading the cost over the year rather than requiring a lump sum payment. The employee's portion is calculated based on the selected plan and the number of people covered."
                    }
                },
                {
                    "DOC002",
                    new Document
                    {
                        Id = "DOC002",
                        Title = "Contoso Electronics Employee Handbook",
                        Summary =
                            "Company information, mission statement, core values, policies on performance reviews, workplace safety, privacy, and data security.",
                        Content =
                            "Contoso Electronics is a leader in the aerospace industry, providing advanced electronic components for both commercial and military aircraft. The company specializes in creating cutting-edge systems that are both reliable and efficient.\n\nMission: To provide the highest quality aircraft components to customers while maintaining a commitment to safety and excellence. The company has built a strong reputation in the aerospace industry and strives to continually improve products and services.\n\nCore Values:\n1. Quality: Providing the highest quality products and services\n2. Integrity: Valuing honesty, respect, and trustworthiness\n3. Innovation: Encouraging creativity and new approaches\n4. Teamwork: Believing collaboration leads to greater success\n5. Respect: Treating all employees, customers, and partners with dignity\n6. Excellence: Striving to exceed expectations\n7. Accountability: Taking responsibility for actions\n8. Community: Making a positive impact in communities\n\nPerformance Reviews are conducted annually and provide feedback on areas for improvement and goals for the upcoming year. They are two-way dialogues between managers and employees.\n\nWorkplace Safety is everyone's responsibility, with programs including hazard identification, training, PPE, emergency preparedness, reporting, inspections, and record keeping.\n\nThe company has a zero-tolerance policy for workplace violence and provides regular training on prevention.\n\nPrivacy Policy ensures protection of personal information, with controlled collection and secure data practices.\n\nData Security includes encryption requirements, access controls, regular backups, and annual security training."
                    }
                },
                {
                    "DOC003",
                    new Document
                    {
                        Id = "DOC003",
                        Title = "Roles Descriptions at Contoso Electronics",
                        Summary =
                            "Comprehensive descriptions of various job roles at Contoso Electronics, including executive positions, management roles, and individual contributor positions.",
                        Content =
                            "Contoso Electronics has a structured hierarchy of roles from executive leadership to individual contributors:\n\nExecutive Leadership:\n- Chief Executive Officer (CEO): Provides strategic direction and oversight to ensure long-term success and profitability. Develops and implements strategy, manages executive team, ensures compliance, and develops relationships with key stakeholders.\n- Chief Operating Officer (COO): Oversees day-to-day operations, develops strategies and monitors KPIs for all departments.\n- Chief Financial Officer (CFO): Provides strategic direction for financial operations, leads accounting, financial planning, budgeting, and risk management.\n- Chief Technology Officer (CTO): Leads technology strategy, development of new products and standards, and IT infrastructure security.\n\nVice Presidents:\n- VP of Sales: Drives sales and revenue growth, manages sales team and strategies.\n- VP of Marketing: Creates and manages marketing strategies to promote products and services.\n- VP of Operations: Oversees operational efficiency and customer service.\n- VP of Human Resources: Develops HR strategies aligned with business objectives.\n- VP of Research and Development: Leads R&D initiatives and product innovation.\n- VP of Product Management: Manages product strategy and roadmap.\n\nDirectors & Managers:\n- Each department has Director and Manager level positions that implement strategies, manage teams, and oversee day-to-day functions.\n\nIndividual Contributors:\n- Sales Representatives: Drive sales of Contoso products and services, maintain customer relationships.\n- Customer Service Representatives: Provide excellent service, address inquiries and resolve issues.\n\nEach role has specific qualifications and responsibilities aligned with the company's mission in the aerospace industry."
                    }
                }
            };
        }

        public string SearchDocuments(string query)
        {
            // Search for documents based on the query
            var searchResults = new List<object>();

            foreach (var doc in _documents.Values)
            {
                bool isRelevant =
                    doc.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    doc.Summary.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    doc.Content.Contains(query, StringComparison.OrdinalIgnoreCase);

                if (isRelevant)
                {
                    searchResults.Add(new
                    {
                        documentId = doc.Id,
                        title = doc.Title,
                        summary = doc.Summary
                    });
                }
            }

            // If no results found based on the query, return all documents
            if (searchResults.Count == 0)
            {
                foreach (var doc in _documents.Values)
                {
                    searchResults.Add(new
                    {
                        documentId = doc.Id,
                        title = doc.Title,
                        summary = doc.Summary
                    });
                }
            }

            // Serialize to JSON
            return JsonSerializer.Serialize(searchResults, new JsonSerializerOptions { WriteIndented = true });
        }

        public string GetDocumentContent(string documentId)
        {
            // Retrieve and return the content of the specified document
            if (_documents.TryGetValue(documentId, out var document))
            {
                var result = new
                {
                    documentId = document.Id,
                    title = document.Title,
                    content = document.Content
                };

                return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            }

            return JsonSerializer.Serialize(new { error = "Document not found" });
        }
    }

    public class Document
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public string Content { get; set; }
    }
}