// Cohere Rerank API Example - using .NET 8
// This example demonstrates using the Rerank API with structured data and threshold filtering

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.IO;

namespace RerankExample
{
    class Document
    {
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Content { get; set; } = string.Empty;
        
        public override string ToString()
        {
            return $"""
                Title: {Title}
                Author: {Author}
                Date: {Date:MMMM d, yyyy}
                Content: {Content}
                """;
        }
    }

    class RerankRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "rerank-v3.5";
        
        [JsonPropertyName("query")]
        public string Query { get; set; } = "";
        
        [JsonPropertyName("documents")]
        public List<string> Documents { get; set; } = [];
        
        [JsonPropertyName("top_n")]
        public int TopN { get; set; }
        
        [JsonPropertyName("max_tokens_per_doc")]
        public int MaxTokensPerDoc { get; set; } = 2048;
    }

    class RerankResponse
    {
        [JsonPropertyName("results")]
        public List<RerankResult> Results { get; set; } = [];
    }

    class RerankResult
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }
        
        [JsonPropertyName("relevance_score")]
        public double RelevanceScore { get; set; }
    }

    class Program
    {
        // Load API key from API_KEYS.json
        private static readonly string API_KEY = LoadApiKey();
        
        // Example query about home repairs
        private const string QUERY = "i have a leaky tap";
        
        // Sample relevance threshold (typically determined through testing)
        private const double RELEVANCE_THRESHOLD = 0.5;
        
        private static string LoadApiKey()
        {
            try
            {
                string projectRoot = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.Parent.Parent.FullName;
                string apiKeysPath = Path.Combine(projectRoot, "API_KEYS.json");
                string jsonContent = File.ReadAllText(apiKeysPath);
                using JsonDocument doc = JsonDocument.Parse(jsonContent);
                return doc.RootElement.GetProperty("cohere").GetProperty("api_key").GetString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading API key: {ex.Message}");
                return null;
            }
        }
        
        static async Task Main(string[] args)
        {
            // Create structured document data using the Document class
            var documents = new List<Document>
            {
                new()
                {
                    Title = "How to fix a leaky faucet",
                    Author = "Jane Doe",
                    Date = new DateTime(2023, 3, 15),
                    Content = "To fix a leaky faucet, first turn off the water supply. Then, disassemble the faucet handle by removing the decorative cap, unscrewing the handle, and removing the cartridge. Check for worn O-rings or damaged washers and replace them. Reassemble the faucet and turn the water back on to test."
                },
                
                new()
                {
                    Title = "Common bathroom plumbing issues",
                    Author = "Bob Smith",
                    Date = new DateTime(2023, 1, 10),
                    Content = "Bathroom plumbing issues include clogged drains, running toilets, and low water pressure. For drains, try using a plunger or drain snake. For toilets, check the flapper valve. For low pressure, inspect the aerator for mineral buildup."
                },
                
                new()
                {
                    Title = "How to repair a dishwasher",
                    Author = "Alice Johnson",
                    Date = new DateTime(2023, 4, 20),
                    Content = "If your dishwasher isn't cleaning properly, check the spray arms for clogs, inspect the filter, and ensure the water supply is adequate. For leaks, examine the door gasket for damage or the water inlet valve for failure."
                },
                
                new()
                {
                    Title = "Fixing sink drainage problems",
                    Author = "Michael Wilson",
                    Date = new DateTime(2023, 2, 5),
                    Content = "Slow draining sinks are often caused by debris buildup. Start by using a plunger, then try a mixture of baking soda and vinegar. For persistent clogs, disassemble the P-trap under the sink to remove any obstructions."
                }
            };
            
            // Create HTTP client
            using var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri("https://api.cohere.com/");
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", API_KEY);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            Console.WriteLine("Sending request to Cohere Rerank API...");
            Console.WriteLine($"Query: \"{QUERY}\"");
            Console.WriteLine($"Using relevance threshold: {RELEVANCE_THRESHOLD}");

            try
            {
                // Convert documents to strings for the API
                var documentStrings = documents.Select(doc => doc.ToString()).ToList();
                
                // Prepare the request
                var rerankRequest = new RerankRequest
                {
                    Query = QUERY,
                    Documents = documentStrings,
                    TopN = documents.Count
                };

                // Send the request
                var response = await httpClient.PostAsJsonAsync("v2/rerank", rerankRequest);
                
                // Handle HTTP errors
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"HTTP error! Status: {(int)response.StatusCode} {response.StatusCode}");
                    Console.WriteLine($"Error details: {errorContent}");
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        Console.WriteLine("\nTip: Your API key may be invalid or expired. Please check your API key.");
                    }
                    
                    return;
                }
                
                // Parse the response
                var result = await response.Content.ReadFromJsonAsync<RerankResponse>();
                
                if (result?.Results == null || result.Results.Count == 0)
                {
                    Console.WriteLine("Unexpected response format or empty results");
                    return;
                }
                
                Console.WriteLine("\nRanked Results:");
                
                // Display all results with their scores
                for (int i = 0; i < result.Results.Count; i++)
                {
                    var item = result.Results[i];
                    var document = documents[item.Index];
                    
                    Console.WriteLine($"\nRank {i + 1}:");
                    Console.WriteLine($"Document: {document.Title}");
                    Console.WriteLine($"Author: {document.Author}");
                    Console.WriteLine($"Date: {document.Date:MMMM d, yyyy}");
                    Console.WriteLine($"Relevance Score: {item.RelevanceScore:F4}");
                    Console.WriteLine($"Index: {item.Index}");
                }
                
                // Filter documents by relevance threshold
                var relevantDocuments = result.Results.Where(r => r.RelevanceScore >= RELEVANCE_THRESHOLD).ToList();
                
                Console.WriteLine("\n--- Filtered Results (Above Threshold) ---");
                if (relevantDocuments.Count > 0)
                {
                    for (int i = 0; i < relevantDocuments.Count; i++)
                    {
                        var item = relevantDocuments[i];
                        var document = documents[item.Index];
                        
                        Console.WriteLine($"\nRelevant Document {i + 1}:");
                        Console.WriteLine($"Title: {document.Title}");
                        Console.WriteLine($"Author: {document.Author}");
                        Console.WriteLine($"Date: {document.Date:MMMM d, yyyy}");
                        Console.WriteLine($"Relevance Score: {item.RelevanceScore:F4}");
                    }
                }
                else
                {
                    Console.WriteLine("No documents found above the relevance threshold.");
                }
                
                // Show the most relevant document with all its details
                if (result.Results.Count > 0)
                {
                    var topResult = result.Results[0];
                    var topDocument = documents[topResult.Index];
                    
                    Console.WriteLine("\n--- Most Relevant Document Details ---");
                    Console.WriteLine($"Title: {topDocument.Title}");
                    Console.WriteLine($"Author: {topDocument.Author}");
                    Console.WriteLine($"Date: {topDocument.Date:MMMM d, yyyy}");
                    Console.WriteLine($"Content: {topDocument.Content}");
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                
                if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Console.WriteLine("\nTip: Your API key may be invalid or expired. Please check your API key.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
            }
        }
    }
}