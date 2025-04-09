using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using groq_structured_responses_dotnet;

public class Llama4MaverickFireworksExample
{
    private readonly HttpClient _client;
    private const string FireworksApiUrl = "https://api.fireworks.ai/inference/v1/chat/completions";
    private const string ModelId = "accounts/fireworks/models/llama4-maverick-instruct-basic";
    private readonly string _query;

    public Llama4MaverickFireworksExample(string query)
    {
        _query = query;
        _client = new HttpClient();
    }

    public async Task RunAsync()
    {
        // Overall timing for the entire process
        var totalStopwatch = Stopwatch.StartNew();

        Console.WriteLine("\n========== FIREWORKS LLAMA-4-MAVERICK STRUCTURED RESPONSE TEST ==========\n");
        Console.WriteLine($"Using Model: {ModelId}");
        Console.WriteLine("Starting process...\n");

        // Setup API client timing
        var setupStopwatch = Stopwatch.StartNew();
        var apiKey = FireworksHelper.GetApiKey();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        setupStopwatch.Stop();
        Console.WriteLine($"API client setup completed in: {setupStopwatch.ElapsedMilliseconds}ms");

        var query = _query;
        Console.WriteLine($"Query: \"{query}\"\n");

        // API call and response processing timing
        var apiStopwatch = Stopwatch.StartNew();
        var responseData = await GetStructuredResponseAsync(query);
        apiStopwatch.Stop();

        totalStopwatch.Stop();

        // Display timing results
        Console.WriteLine("\n========== TIMING RESULTS ==========");
        Console.WriteLine($"API Call + Processing Time: {apiStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Total Execution Time: {totalStopwatch.ElapsedMilliseconds}ms");

        // Display structured response properties
        responseData.LogProperties();
    }

    public async Task<StructuredResponse> GetStructuredResponseAsync(string query)
    {
        try
    {
        // Create the user message with the query in JSON format
        var userMessage = $@"## User Question
```json
{{
 ""query"": ""{query}""
}}
```";

        // Time request preparation
        var prepStopwatch = Stopwatch.StartNew();

        // Prepare the request payload
        var requestPayload = new
        {
            model = ModelId,
            messages = new object[]
            {
                new { role = "system", content = new object[] { new { type = "text", text = PromptHelper.SystemPrompt } } },
                new { role = "user", content = new object[] { new { type = "text", text = userMessage } } }
            },
            temperature = 0.0,
            max_tokens = 4096,
            top_p = 1,
            top_k = 40,
            presence_penalty = 0,
            frequency_penalty = 0,
            response_format = new { type = "json_object" }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestPayload),
            Encoding.UTF8,
            "application/json"
        );

        prepStopwatch.Stop();
        Console.WriteLine($"Request preparation completed in: {prepStopwatch.ElapsedMilliseconds}ms");


            // Time the API request
            var requestStopwatch = Stopwatch.StartNew();
            Console.WriteLine("Sending request to Fireworks API...");

            // Send the request to Fireworks API
            var response = await _client.PostAsync(FireworksApiUrl, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\nError Response ({response.StatusCode}):");
                Console.WriteLine(errorContent);
                Console.ResetColor();
            }
            
            response.EnsureSuccessStatusCode();

            requestStopwatch.Stop();
            Console.WriteLine($"Received response in: {requestStopwatch.ElapsedMilliseconds}ms");

            // Time the response processing
            var processStopwatch = Stopwatch.StartNew();

            var responseBody = await response.Content.ReadAsStringAsync();

            // Log full response for debugging
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("\n========== FULL API RESPONSE ==========\n");
            Console.WriteLine(responseBody);
            Console.ResetColor();

            using var jsonDoc = JsonDocument.Parse(responseBody);

            // Extract the structured response from the model's content
            var choices = jsonDoc.RootElement.GetProperty("choices");
            var firstChoice = choices[0];
            var message = firstChoice.GetProperty("message");
            
            // In Fireworks API, the content might be an array of objects instead of a string
            JsonElement contentElement = message.GetProperty("content");
            string modelContent;
            
            if (contentElement.ValueKind == JsonValueKind.Array)
            {
                // If content is an array, find the first text element
                modelContent = "";
                foreach (var item in contentElement.EnumerateArray())
                {
                    if (item.TryGetProperty("type", out var typeElement) && 
                        typeElement.GetString() == "text" && 
                        item.TryGetProperty("text", out var textElement))
                    {
                        modelContent = textElement.GetString();
                        break;
                    }
                }
            }
            else
            {
                // If content is a string
                modelContent = contentElement.GetString();
            }

            // Parse the JSON response into our StructuredResponse class
            var structuredResponse = JsonSerializer.Deserialize<StructuredResponse>(
                modelContent,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            );

            processStopwatch.Stop();
            Console.WriteLine($"Response processing completed in: {processStopwatch.ElapsedMilliseconds}ms");

            return structuredResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling Fireworks API: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }

            throw;
        }
    }
}