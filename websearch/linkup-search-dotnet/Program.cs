using System.Text.Json;
using System.Net.Http.Headers;

class Program
{
    static async Task Main(string[] args)
    {
        var sample = new LinkupSearchExample();
        
        // Example 1: Using sourcedAnswer output type with standard depth
        Console.WriteLine("EXAMPLE 1: STANDARD SOURCED ANSWER");
        Console.WriteLine("==============================");
        await sample.RunAsync();
        
        // Example 2: Using searchResults output type with standard depth
        Console.WriteLine("\n\nEXAMPLE 2: STANDARD SEARCH RESULTS");
        Console.WriteLine("==============================");
        await sample.RunSearchResultsExampleAsync();
        
        // Example 3: Using sourcedAnswer output type with deep search
        Console.WriteLine("\n\nEXAMPLE 3: DEEP SOURCED ANSWER");
        Console.WriteLine("==============================");
        await sample.RunDeepSourcedAnswerAsync();
        
        // Example 4: Using searchResults output type with deep search
        Console.WriteLine("\n\nEXAMPLE 4: DEEP SEARCH RESULTS");
        Console.WriteLine("==============================");
        await sample.RunDeepSearchResultsAsync();
    }
}

public class LinkupSearchExample
{
    private const string API_URL = "https://api.linkup.so/v1/search";
    
    // Custom weather result classes
    public class WeatherSearchResponse
    {
        public List<WeatherResult> results { get; set; }
    }

    public class WeatherResult
    {
        public string type { get; set; }
        public string name { get; set; }
        public string url { get; set; }
        public string content { get; set; }
    }
    
    // Classes to deserialize standard Linkup search results
    public class LinkupSearchResponse
    {
        public List<SearchResult> results { get; set; }
    }

    public class SearchResult
    {
        public string title { get; set; }
        public string url { get; set; }
        public string snippet { get; set; }
        public List<ContentBlock> contentBlocks { get; set; }
    }

    public class ContentBlock
    {
        public string type { get; set; }
        public string content { get; set; }
    }
    
    private static string GetApiKey()
    {
        try
        {
            string projectRoot = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.Parent.Parent.FullName;
            string apiKeysPath = Path.Combine(projectRoot, "API_KEYS.json");
            string jsonContent = File.ReadAllText(apiKeysPath);
            using JsonDocument doc = JsonDocument.Parse(jsonContent);
            var linkup = doc.RootElement.GetProperty("linkup");
            string apiKey = linkup.GetProperty("api_key").GetString();
            if (string.IsNullOrEmpty(apiKey)) 
            {
                throw new Exception("API key not found in API_KEYS.json");
            }
            return apiKey;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading credentials: {ex.Message}");
            throw;
        }
    }

    public async Task RunAsync()
    {
        // Get API key
        var apiKey = GetApiKey();
        
        // Create HTTP client
        using var httpClient = new HttpClient();
        
        // Add authorization header
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        
        // Create the request body
        var requestBody = new
        {
            q = "What is the weather like in Adelaide this weekend?",
            depth = "standard", // Using standard for faster results
            outputType = "sourcedAnswer", // Using sourcedAnswer to get formatted answer with sources
            includeImages = false
        };
        
        // Serialize the request body to JSON
        var jsonContent = JsonSerializer.Serialize(requestBody);
        var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
        httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        
        try
        {
            Console.WriteLine("Sending sourcedAnswer request to Linkup API...");
            
            // Send the POST request
            var response = await httpClient.PostAsync(API_URL, httpContent);
            
            // Ensure success
            response.EnsureSuccessStatusCode();
            
            // Read the response content
            var responseContent = await response.Content.ReadAsStringAsync();
            
            Console.WriteLine("Response received:");
            
            // Parse the JSON response
            using JsonDocument doc = JsonDocument.Parse(responseContent);
            
            // Extract and display the answer
            var answer = doc.RootElement.GetProperty("answer").GetString();
            Console.WriteLine("\nANSWER:");
            Console.WriteLine(answer);
            Console.WriteLine();
            
            // Extract and display sources
            Console.WriteLine("SOURCES:");
            var sources = doc.RootElement.GetProperty("sources");
            foreach (var source in sources.EnumerateArray())
            {
                var name = source.GetProperty("name").GetString();
                var url = source.GetProperty("url").GetString();
                var snippet = source.GetProperty("snippet").GetString();
                
                Console.WriteLine($"- {name}");
                Console.WriteLine($"  URL: {url}");
                Console.WriteLine($"  Snippet: {snippet?.Substring(0, Math.Min(100, snippet?.Length ?? 0))}...");
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error making API request: {ex.Message}");
            
            // If there's an inner exception, display it for more detail
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
        }
    }
    
    public async Task RunSearchResultsExampleAsync()
    {
        // Get API key
        var apiKey = GetApiKey();
        
        // Create HTTP client
        using var httpClient = new HttpClient();
        
        // Add authorization header
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        
        // Create the request body - now using searchResults instead of sourcedAnswer
        var requestBody = new
        {
            q = "What is the weather like in Adelaide this weekend?",
            depth = "standard",
            outputType = "searchResults", // Changed to searchResults
            includeImages = false
        };
        
        // Serialize the request body to JSON
        var jsonContent = JsonSerializer.Serialize(requestBody);
        var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
        httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        
        try
        {
            Console.WriteLine("Sending searchResults request to Linkup API...");
            
            // Send the POST request
            var response = await httpClient.PostAsync(API_URL, httpContent);
            
            // Ensure success
            response.EnsureSuccessStatusCode();
            
            // Read the response content
            var responseContent = await response.Content.ReadAsStringAsync();
            
            // 1. First display the raw JSON
            Console.WriteLine("\nRaw JSON Response:");
            Console.WriteLine(responseContent.Substring(0, Math.Min(500, responseContent.Length)) + "...");
            
            // 2. Parse using System.Text.Json and deserialize to our WeatherSearchResponse class
            Console.WriteLine("\nDeserialized Weather Results:");
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            try
            {
                // Attempt to deserialize to our custom weather results class
                var weatherResults = JsonSerializer.Deserialize<WeatherSearchResponse>(responseContent, options);
                
                if (weatherResults != null && weatherResults.results != null)
                {
                    foreach (var result in weatherResults.results)
                    {
                        Console.WriteLine($"\nSource: {result.name}");
                        Console.WriteLine($"URL: {result.url}");
                        Console.WriteLine($"Content: {result.content?.Substring(0, Math.Min(150, result.content?.Length ?? 0))}...");
                    }
                    
                    Console.WriteLine($"\nTotal weather results: {weatherResults.results.Count}");
                }
                else
                {
                    Console.WriteLine("No weather results found or unable to parse as weather results.");
                }
            }
            catch (JsonException weatherEx)
            {
                Console.WriteLine($"Error deserializing as weather results: {weatherEx.Message}");
                Console.WriteLine("Falling back to standard search results format...");
                
                try
                {
                    // Fall back to standard search results format
                    var searchResponse = JsonSerializer.Deserialize<LinkupSearchResponse>(responseContent, options);
                    
                    if (searchResponse != null && searchResponse.results != null)
                    {
                        foreach (var result in searchResponse.results)
                        {
                            Console.WriteLine($"\nTitle: {result.title}");
                            Console.WriteLine($"URL: {result.url}");
                            Console.WriteLine($"Snippet: {result.snippet?.Substring(0, Math.Min(150, result.snippet?.Length ?? 0))}...");
                            
                            if (result.contentBlocks != null && result.contentBlocks.Count > 0)
                            {
                                Console.WriteLine("Content Blocks:");
                                var blockCount = 0;
                                
                                foreach (var block in result.contentBlocks)
                                {
                                    blockCount++;
                                    if (blockCount > 2) break; // Only show first 2 blocks
                                    
                                    var blockContent = block.content;
                                    Console.WriteLine($"  - {blockContent?.Substring(0, Math.Min(100, blockContent?.Length ?? 0))}...");
                                }
                                
                                if (result.contentBlocks.Count > 2)
                                {
                                    Console.WriteLine($"  ... and {result.contentBlocks.Count - 2} more blocks");
                                }
                            }
                            
                            Console.WriteLine("-----------------------------------");
                        }
                        
                        Console.WriteLine($"\nTotal results found: {searchResponse.results.Count}");
                    }
                    else
                    {
                        Console.WriteLine("No search results found or unable to parse response.");
                    }
                }
                catch (JsonException standardEx)
                {
                    Console.WriteLine($"Error deserializing as standard results: {standardEx.Message}");
                    Console.WriteLine("Printing raw results as fallback...");
                    Console.WriteLine(responseContent);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error making API request: {ex.Message}");
            
            // If there's an inner exception, display it for more detail
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
        }
    }
    
    public async Task RunDeepSourcedAnswerAsync()
    {
        // Get API key
        var apiKey = GetApiKey();
        
        // Create HTTP client
        using var httpClient = new HttpClient();
        
        // Add authorization header
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        
        // Create the request body
        var requestBody = new
        {
            q = "What innovations have there been in quantum computing in 2024?",
            depth = "deep", // Using deep for more comprehensive results
            outputType = "sourcedAnswer", 
            includeImages = false
        };
        
        // Serialize the request body to JSON
        var jsonContent = JsonSerializer.Serialize(requestBody);
        var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
        httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        
        try
        {
            Console.WriteLine("Sending deep sourcedAnswer request to Linkup API...");
            Console.WriteLine("(This may take longer due to deep search)");
            
            // Send the POST request
            var response = await httpClient.PostAsync(API_URL, httpContent);
            
            // Ensure success
            response.EnsureSuccessStatusCode();
            
            // Read the response content
            var responseContent = await response.Content.ReadAsStringAsync();
            
            Console.WriteLine("Response received:");
            
            // Parse the JSON response
            using JsonDocument doc = JsonDocument.Parse(responseContent);
            
            // Extract and display the answer
            var answer = doc.RootElement.GetProperty("answer").GetString();
            Console.WriteLine("\nDEEP ANSWER:");
            Console.WriteLine(answer);
            Console.WriteLine();
            
            // Extract and display sources
            Console.WriteLine("SOURCES:");
            var sources = doc.RootElement.GetProperty("sources");
            
            // Count total sources
            int sourceCount = 0;
            foreach (var source in sources.EnumerateArray())
            {
                sourceCount++;
                var name = source.GetProperty("name").GetString();
                var url = source.GetProperty("url").GetString();
                var snippet = source.GetProperty("snippet").GetString();
                
                Console.WriteLine($"- {name}");
                Console.WriteLine($"  URL: {url}");
                Console.WriteLine($"  Snippet: {snippet?.Substring(0, Math.Min(100, snippet?.Length ?? 0))}...");
                Console.WriteLine();
                
                // Only show first 3 sources to avoid overwhelming output
                if (sourceCount >= 3)
                {
                    Console.WriteLine($"... and {sources.GetArrayLength() - 3} more sources");
                    break;
                }
            }
            
            Console.WriteLine($"Total sources: {sources.GetArrayLength()}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error making API request: {ex.Message}");
            
            // If there's an inner exception, display it for more detail
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
        }
    }
    
    public async Task RunDeepSearchResultsAsync()
    {
        // Get API key
        var apiKey = GetApiKey();
        
        // Create HTTP client
        using var httpClient = new HttpClient();
        
        // Add authorization header
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        
        // Create the request body - now using searchResults with deep search
        var requestBody = new
        {
            q = "What innovations have there been in quantum computing in 2024?",
            depth = "deep", // Using deep for more comprehensive results
            outputType = "searchResults",
            includeImages = false
        };
        
        // Serialize the request body to JSON
        var jsonContent = JsonSerializer.Serialize(requestBody);
        var httpContent = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
        httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        
        try
        {
            Console.WriteLine("Sending deep searchResults request to Linkup API...");
            Console.WriteLine("(This may take longer due to deep search)");
            
            // Send the POST request
            var response = await httpClient.PostAsync(API_URL, httpContent);
            
            // Ensure success
            response.EnsureSuccessStatusCode();
            
            // Read the response content
            var responseContent = await response.Content.ReadAsStringAsync();
            
            // 1. First display a snippet of the raw JSON
            Console.WriteLine("\nRaw JSON Response (first 300 characters):");
            Console.WriteLine(responseContent.Substring(0, Math.Min(300, responseContent.Length)) + "...");
            
            // 2. Parse using System.Text.Json and deserialize to our LinkupSearchResponse class
            Console.WriteLine("\nDESERIALIZED DEEP SEARCH RESULTS:");
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            try
            {
                // Deserialize to standard search results format
                var searchResponse = JsonSerializer.Deserialize<LinkupSearchResponse>(responseContent, options);
                
                if (searchResponse != null && searchResponse.results != null)
                {
                    int resultCount = 0;
                    
                    foreach (var result in searchResponse.results)
                    {
                        resultCount++;
                        Console.WriteLine($"\nResult {resultCount}:");
                        Console.WriteLine($"Title: {result.title}");
                        Console.WriteLine($"URL: {result.url}");
                        Console.WriteLine($"Snippet: {result.snippet?.Substring(0, Math.Min(150, result.snippet?.Length ?? 0))}...");
                        
                        // Only show the first 3 results to avoid overwhelming output
                        if (resultCount >= 3)
                        {
                            Console.WriteLine($"\n... and {searchResponse.results.Count - 3} more results");
                            break;
                        }
                        
                        Console.WriteLine("-----------------------------------");
                    }
                    
                    Console.WriteLine($"\nTotal deep search results found: {searchResponse.results.Count}");
                    
                    // Compare against standard search to highlight the difference
                    Console.WriteLine("\nDeep search typically provides more comprehensive and higher quality results compared to standard search.");
                }
                else
                {
                    Console.WriteLine("No search results found or unable to parse response.");
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error deserializing results: {ex.Message}");
                Console.WriteLine("Printing raw results excerpt as fallback...");
                Console.WriteLine(responseContent.Substring(0, Math.Min(500, responseContent.Length)) + "...");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error making API request: {ex.Message}");
            
            // If there's an inner exception, display it for more detail
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
        }
    }
}