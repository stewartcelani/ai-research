using System.Diagnostics;
using System.Text.Json;
using Google.Cloud.AIPlatform.V1;
using Google.Apis.Auth.OAuth2;
using groq_structured_responses_dotnet;
using Type = Google.Cloud.AIPlatform.V1.Type;
using Value = Google.Protobuf.WellKnownTypes.Value;

public class Gemini25ProExample
{
    private readonly PredictionServiceClient _predictionServiceClient;
    private readonly string _projectId;
    private const string Location = "us-central1";
    private const string Publisher = "google";
    private const string ModelId = "gemini-2.5-pro-preview-03-25";
    private readonly string _query;
    private readonly bool _showFullApiResponse;

    public Gemini25ProExample(string query, bool showFullApiResponse = false)
    {
        _showFullApiResponse = showFullApiResponse;
        _query = query;

        // Setup API client timing
        var setupStopwatch = Stopwatch.StartNew();

        var (projectId, serviceAccountKey) = GoogleHelper.GetCredentials();
        _projectId = projectId;

        var credential = GoogleCredential.FromJson(serviceAccountKey)
            .CreateScoped("https://www.googleapis.com/auth/cloud-platform");

        _predictionServiceClient = new PredictionServiceClientBuilder
        {
            Endpoint = $"{Location}-aiplatform.googleapis.com",
            Credential = credential
        }.Build();

        setupStopwatch.Stop();
        Console.WriteLine($"API client setup completed in: {setupStopwatch.ElapsedMilliseconds}ms");
    }

    public async Task RunAsync()
    {
        // Overall timing for the entire process
        var totalStopwatch = Stopwatch.StartNew();

        Console.WriteLine("\n========== GOOGLE GEMINI-2.5-PRO STRUCTURED RESPONSE TEST ==========\n");
        Console.WriteLine($"Using Model: {ModelId}");
        Console.WriteLine("Starting process...\n");

        var query = _query;
        Console.WriteLine($"Query: \"{query}\"\n");

        // API call and response processing timing
        var apiStopwatch = Stopwatch.StartNew();
        var structuredResponse = await GetStructuredResponseAsync(query);
        apiStopwatch.Stop();

        totalStopwatch.Stop();

        // Display timing results
        Console.WriteLine("\n========== TIMING RESULTS ==========");
        Console.WriteLine($"API Call + Processing Time: {apiStopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Total Execution Time: {totalStopwatch.ElapsedMilliseconds}ms");

        // Display structured response properties
        structuredResponse.LogProperties();
    }

    public async Task<StructuredResponse> GetStructuredResponseAsync(string query)
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

        // Construct the system prompt with additional JSON formatting instructions for Gemini
        var systemMessage = PromptHelper.SystemPrompt +
                            "\n\nYou MUST respond in valid JSON format that matches the StructuredResponse class with these properties: Goal, Reasoning, UseTools, Plan, and SuggestedChatTitle.";

        var responseSchema = new OpenApiSchema
        {
            Type = Type.Object
        };

        // Add the standard properties
        responseSchema.Properties.Add("goal", new OpenApiSchema
        {
            Type = Type.String,
            Description = "Clear, specific statement of what the ideal response should accomplish and contain."
        });

        responseSchema.Properties.Add("reasoning", new OpenApiSchema
        {
            Type = Type.String,
            Description = "Brief explanation of why tools are needed or not needed, referencing the query and context."
        });

        responseSchema.Properties.Add("useTools", new OpenApiSchema
        {
            Type = Type.Boolean,
            Description =
                "Set to true if external tools are required. Set to false if the answer can be generated directly without tools."
        });

        responseSchema.Properties.Add("plan", new OpenApiSchema
        {
            Type = Type.String,
            Description =
                "Detailed markdown-formatted execution plan. Required only when useTools is true. Null or empty otherwise."
        });
        responseSchema.Properties.Add("suggestedChatTitle", new OpenApiSchema
        {
            Type = Type.String,
            Description =
                "A concise, descriptive title for this chat (50 characters or less). Should capture the main topic or intent."
        });

        responseSchema.Required.Add("goal");
        responseSchema.Required.Add("reasoning");
        responseSchema.Required.Add("useTools");
        responseSchema.Required.Add("suggestedChatTitle");

        // Prepare the request payload for Gemini
        var generateContentRequest = new GenerateContentRequest
        {
            Model = $"projects/{_projectId}/locations/{Location}/publishers/{Publisher}/models/{ModelId}",
            GenerationConfig = new GenerationConfig
            {
                Temperature = 0.0f, // Low temperature for deterministic planning
                TopP = 0.95f,
                MaxOutputTokens = 4096, // Increased to accommodate detailed plans
                ResponseMimeType = "application/json",
                ResponseSchema = responseSchema
            },
            SystemInstruction = new Content
            {
                Parts = { new Part { Text = systemMessage } }
            },
            Contents =
            {
                new Content
                {
                    Role = "USER",
                    Parts =
                    {
                        new Part { Text = userMessage }
                    }
                }
            }
        };

        prepStopwatch.Stop();
        Console.WriteLine($"Request preparation completed in: {prepStopwatch.ElapsedMilliseconds}ms");

        try
        {
            // Time the API request
            var requestStopwatch = Stopwatch.StartNew();
            Console.WriteLine("Sending request to Google AI API...");

            // Send the request to Google AI API
            var response = await _predictionServiceClient.GenerateContentAsync(generateContentRequest);

            requestStopwatch.Stop();
            Console.WriteLine($"Received response in: {requestStopwatch.ElapsedMilliseconds}ms");

            // Time the response processing
            var processStopwatch = Stopwatch.StartNew();

            // Log full response for debugging
            if (_showFullApiResponse)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("\n========== FULL API RESPONSE ==========\n");
                Console.WriteLine(response);
                Console.ResetColor();
            }
           
            // Extract the text response from the API
            var responseText = response.Candidates[0].Content.Parts[0].Text;

            // Clean up the response text to ensure it's valid JSON
            responseText = CleanJsonResponse(responseText);

            /*// Log the extracted JSON for debugging
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n========== EXTRACTED JSON RESPONSE ==========\n");
            Console.WriteLine(responseText);
            Console.ResetColor();*/

            // Parse the JSON response into our StructuredResponse class
            StructuredResponse structuredResponse;
            try
            {
                structuredResponse = JsonSerializer.Deserialize<StructuredResponse>(
                    responseText,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }
                );

                // Validate required fields
                if (string.IsNullOrWhiteSpace(structuredResponse.Goal) ||
                    string.IsNullOrWhiteSpace(structuredResponse.Reasoning) ||
                    string.IsNullOrWhiteSpace(structuredResponse.SuggestedChatTitle))
                {
                    Console.WriteLine("Warning: Response missing required fields. Creating fallback response.");
                    throw new Exception("Missing required fields in structured response.");
                }
            }
            catch (Exception jsonEx)
            {
                Console.WriteLine($"Error parsing JSON response: {jsonEx.Message}");
                throw new Exception($"Error parsing JSON response: {jsonEx.Message}");
            }

            processStopwatch.Stop();
            Console.WriteLine($"Response processing completed in: {processStopwatch.ElapsedMilliseconds}ms");

            return structuredResponse;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calling Google AI API: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }

            throw;
        }
    }

    // Helper method to clean up the JSON response if it contains markdown code blocks
    private string CleanJsonResponse(string text)
    {
        // Remove markdown code blocks if present
        if (text.StartsWith("```json"))
        {
            text = text.Substring("```json".Length);
        }
        else if (text.StartsWith("```"))
        {
            text = text.Substring("```".Length);
        }

        if (text.EndsWith("```"))
        {
            text = text.Substring(0, text.Length - "```".Length);
        }

        // Trim any whitespace
        text = text.Trim();

        return text;
    }
}