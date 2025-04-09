using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var example = new TavilySearchExample();
        await example.RunAsync();
    }
}

public class TavilySearchExample
{
    private static string GetApiKey()
    {
        try
        {
            string projectRoot = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.Parent.Parent.FullName;
            string apiKeysPath = Path.Combine(projectRoot, "API_KEYS.json");
            string jsonContent = File.ReadAllText(apiKeysPath);
            using JsonDocument doc = JsonDocument.Parse(jsonContent);
            var linkup = doc.RootElement.GetProperty("tavily");
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
        var apiKey = GetApiKey();
        
        // Create HTTP client
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        
        Console.WriteLine("Enter URL to extract (or press Enter for default Wikipedia AI page):");
        var userUrl = Console.ReadLine();
        
        var url = string.IsNullOrWhiteSpace(userUrl) 
            ? "https://en.wikipedia.org/wiki/Artificial_intelligence" 
            : userUrl;
        
        Console.WriteLine($"Extracting content from: {url}");
        
        // Run basic extraction
        Console.WriteLine("\n===== BASIC EXTRACTION =====");
        var basicStopwatch = new Stopwatch();
        basicStopwatch.Start();
        var basicResult = await RunExtractionAsync(httpClient, url, "basic");
        basicStopwatch.Stop();
        var basicElapsedTime = basicStopwatch.Elapsed.TotalSeconds;
        DisplayResults(basicResult, "basic", basicElapsedTime);
        
        // Run advanced extraction
        Console.WriteLine("\n===== ADVANCED EXTRACTION =====");
        var advancedStopwatch = new Stopwatch();
        advancedStopwatch.Start();
        var advancedResult = await RunExtractionAsync(httpClient, url, "advanced");
        advancedStopwatch.Stop();
        var advancedElapsedTime = advancedStopwatch.Elapsed.TotalSeconds;
        DisplayResults(advancedResult, "advanced", advancedElapsedTime);
        
        // Compare the results
        Console.WriteLine("\n===== EXTRACTION COMPARISON =====");
        Console.WriteLine($"Basic extraction time: {basicElapsedTime:F2} seconds (API reported: {basicResult?.ResponseTime:F2} seconds)");
        Console.WriteLine($"Advanced extraction time: {advancedElapsedTime:F2} seconds (API reported: {advancedResult?.ResponseTime:F2} seconds)");
        Console.WriteLine($"Time difference: {Math.Abs(advancedElapsedTime - basicElapsedTime):F2} seconds");
        
        if (basicResult?.Results.Count > 0 && advancedResult?.Results.Count > 0)
        {
            var basicContentLength = basicResult.Results[0].RawContent?.Length ?? 0;
            var advancedContentLength = advancedResult.Results[0].RawContent?.Length ?? 0;
            
            Console.WriteLine($"Basic content length: {basicContentLength:N0} characters");
            Console.WriteLine($"Advanced content length: {advancedContentLength:N0} characters");
            Console.WriteLine($"Content length difference: {Math.Abs(advancedContentLength - basicContentLength):N0} characters");
        }
    }

    private async Task<ExtractResponse> RunExtractionAsync(HttpClient httpClient, string url, string extractDepth)
    {
        // Create request body
        var requestBody = new
        {
            urls = new[] { url },
            include_images = false,
            extract_depth = extractDepth
        };
        
        // Serialize request body
        var jsonContent = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        
        try
        {
            // Send request
            var response = await httpClient.PostAsync("https://api.tavily.com/extract", content);
            
            // Process response
            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                
                var options = new JsonSerializerOptions { 
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                return JsonSerializer.Deserialize<ExtractResponse>(jsonResponse, options);
            }
            else
            {
                Console.WriteLine($"Error: {response.StatusCode}");
                Console.WriteLine(await response.Content.ReadAsStringAsync());
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception occurred: {ex.Message}");
            return null;
        }
    }

    private void DisplayResults(ExtractResponse extractResult, string extractType, double elapsedTime)
    {
        if (extractResult == null)
        {
            Console.WriteLine($"No results to display for {extractType} extraction due to an error.");
            return;
        }
        
        Console.WriteLine($"Extraction successful with '{extractType}' depth!");
        Console.WriteLine($"API response time: {extractResult.ResponseTime:F2} seconds");
        Console.WriteLine($"Local execution time: {elapsedTime:F2} seconds");
        
        // Display results
        if (extractResult.Results.Count > 0)
        {
            foreach (var result in extractResult.Results)
            {
                Console.WriteLine($"\nExtracted from: {result.Url}");
                
                // Display content summary
                if (result.RawContent != null)
                {
                    var contentPreview = result.RawContent.Length > 500 
                        ? result.RawContent.Substring(0, 500) + "..." 
                        : result.RawContent;
                    
                    Console.WriteLine($"Content Preview ({result.RawContent.Length} characters total):");
                    Console.WriteLine(contentPreview);
                    
                    // Option to view full content
                    Console.WriteLine("\nPress 'F' to view full content or any other key to continue:");
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.F)
                    {
                        Console.WriteLine("\nFull Content:");
                        Console.WriteLine(result.RawContent);
                    }
                }
                else
                {
                    Console.WriteLine("No content extracted.");
                }
                
                if (result.Images?.Count > 0)
                {
                    Console.WriteLine($"Found {result.Images.Count} images");
                    foreach (var image in result.Images.Take(5))
                    {
                        Console.WriteLine($"- {image}");
                    }
                    
                    if (result.Images.Count > 5)
                    {
                        Console.WriteLine($"... and {result.Images.Count - 5} more images");
                    }
                }
            }
        }
        
        if (extractResult.FailedResults?.Count > 0)
        {
            Console.WriteLine("\nFailed extractions:");
            foreach (var failed in extractResult.FailedResults)
            {
                Console.WriteLine($"- {failed.Url}: {failed.Error}");
            }
        }
    }
}

// Models for deserializing the API response
public class ExtractResponse
{
    [JsonPropertyName("results")]
    public List<ExtractResult> Results { get; set; } = new List<ExtractResult>();
    
    [JsonPropertyName("failed_results")]
    public List<FailedResult> FailedResults { get; set; } = new List<FailedResult>();
    
    [JsonPropertyName("response_time")]
    public double ResponseTime { get; set; }
}

public class ExtractResult
{
    [JsonPropertyName("url")]
    public string Url { get; set; }
    
    [JsonPropertyName("raw_content")]
    public string RawContent { get; set; }
    
    [JsonPropertyName("images")]
    public List<string> Images { get; set; } = new List<string>();
}

public class FailedResult
{
    [JsonPropertyName("url")]
    public string Url { get; set; }
    
    [JsonPropertyName("error")]
    public string Error { get; set; }
}