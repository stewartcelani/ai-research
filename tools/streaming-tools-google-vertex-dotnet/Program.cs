using Google.Cloud.AIPlatform.V1;
using Google.Apis.Auth.OAuth2;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Type = Google.Cloud.AIPlatform.V1.Type;
using Value = Google.Protobuf.WellKnownTypes.Value;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            var sample = new StreamingToolsSample();
            await sample.StreamWithTools();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CRITICAL ERROR: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            
            if (ex.InnerException != null)
            {
                Console.WriteLine($"INNER EXCEPTION: {ex.InnerException.Message}");
                Console.WriteLine(ex.InnerException.StackTrace);
            }
        }
    }
}

public class StreamingToolsSample
{
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

    // Simple mock weather service
    private string GetWeather(string location)
    {
        Console.WriteLine($"[Weather] Getting weather for: {location}");
        
        var weatherData = new Dictionary<string, (string condition, int temperature)>
        {
            { "new york", ("Sunny", 72) },
            { "london", ("Rainy", 65) },
            { "tokyo", ("Cloudy", 70) },
            { "sydney", ("Partly Cloudy", 80) }
        };

        // Default to unknown if location not found
        if (!weatherData.TryGetValue(location.ToLower(), out var weather))
        {
            var result = new
            {
                location,
                condition = "Unknown",
                temperature = 0,
                message = "Location not found in database"
            };
            
            Console.WriteLine($"[Weather] Result: {JsonSerializer.Serialize(result)}");
            return JsonSerializer.Serialize(result);
        }

        var weatherResult = new
        {
            location,
            condition = weather.condition,
            temperature = weather.temperature,
            unit = "Fahrenheit"
        };
        
        Console.WriteLine($"[Weather] Result: {JsonSerializer.Serialize(weatherResult)}");
        return JsonSerializer.Serialize(weatherResult);
    }

    // Let's try a two-phase approach - first get the function calls, then stream the final response
    public async Task StreamWithTools()
    {
        var (projectId, serviceAccountKey) = LoadCredentials();
        if (projectId == null || serviceAccountKey == null)
        {
            throw new InvalidOperationException("Failed to load credentials.");
        }

        string location = "us-central1";
        string publisher = "google";
        string model = "gemini-2.0-flash"; 

        var credential = GoogleCredential.FromJson(serviceAccountKey)
            .CreateScoped("https://www.googleapis.com/auth/cloud-platform");
        
        var predictionServiceClient = new PredictionServiceClientBuilder
        {
            Endpoint = $"{location}-aiplatform.googleapis.com",
            Credential = credential
        }.Build();

        // Define the weather tool
        var getWeatherFunc = new FunctionDeclaration
        {
            Name = "get_weather",
            Description = "Get current weather information for a specified location",
            Parameters = new OpenApiSchema
            {
                Type = Type.Object,
                Properties =
                {
                    ["location"] = new OpenApiSchema
                    {
                        Type = Type.String,
                        Description = "The city or location to get weather for"
                    }
                },
                Required = { "location" }
            }
        };

        string prompt = "What's the weather like in New York and London today? Give me a comparison.";
        Console.WriteLine($"USER: {prompt}\n");

        // Phase 1: Non-streaming call to get function calls
        var initialRequest = new GenerateContentRequest
        {
            Model = $"projects/{projectId}/locations/{location}/publishers/{publisher}/models/{model}",
            Contents =
            {
                new Content
                {
                    Role = "USER",
                    Parts =
                    {
                        new Part { Text = prompt }
                    }
                }
            },
            Tools =
            {
                new Tool
                {
                    FunctionDeclarations = { getWeatherFunc }
                }
            }
        };

        try
        {
            Console.WriteLine("[Phase 1] Getting function calls non-streaming...");
            var initialResponse = await predictionServiceClient.GenerateContentAsync(initialRequest);
            
            var assistantResponse = initialResponse.Candidates[0].Content;
            Console.WriteLine("[Phase 1] Response received");
            
            List<Content> conversationHistory = new List<Content>();
            conversationHistory.Add(initialRequest.Contents[0]); // Add user's prompt
            
            // If there's function calling, handle it
            var functionCalls = new List<FunctionCall>();
            foreach (var part in assistantResponse.Parts)
            {
                if (part.FunctionCall != null)
                {
                    functionCalls.Add(part.FunctionCall);
                }
            }
            
            // Process any function calls
            if (functionCalls.Count > 0)
            {
                Console.WriteLine($"[Phase 1] Found {functionCalls.Count} function calls");
                
                // Process each function call
                var functionResponseContent = new Content { Role = "FUNCTION" };
                
                foreach (var functionCall in functionCalls)
                {
                    Console.WriteLine($"[Phase 1] Processing function call: {functionCall.Name}");
                    string functionResult = "";
                    
                    if (functionCall.Name == "get_weather" &&
                        functionCall.Args?.Fields.ContainsKey("location") == true)
                    {
                        string locationParam = functionCall.Args.Fields["location"].StringValue;
                        functionResult = GetWeather(locationParam);
                    }
                    
                    // Add function response part
                    functionResponseContent.Parts.Add(new Part
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
                    });
                }
                
                // Add assistant's response with function calls to conversation
                conversationHistory.Add(assistantResponse);
                
                // Add function response
                if (functionResponseContent.Parts.Count > 0)
                {
                    conversationHistory.Add(functionResponseContent);
                }
                
                // Phase 2: Get streaming response with function results
                Console.WriteLine("[Phase 2] Getting final streaming response with function results...");
                
                var finalRequest = new GenerateContentRequest
                {
                    Model = $"projects/{projectId}/locations/{location}/publishers/{publisher}/models/{model}",
                    Tools =
                    {
                        new Tool
                        {
                            FunctionDeclarations = { getWeatherFunc }
                        }
                    }
                };
                
                // Add all conversation history
                foreach (var content in conversationHistory)
                {
                    finalRequest.Contents.Add(content);
                }
                
                // Stream the final response
                var responseStream = predictionServiceClient.StreamGenerateContent(finalRequest);
                var enumerableStream = responseStream.GetResponseStream();
                
                Console.WriteLine("\nFINAL RESPONSE:");
                await foreach (var chunk in enumerableStream)
                {
                    if (chunk.Candidates.Count > 0 && chunk.Candidates[0].Content.Parts.Count > 0)
                    {
                        foreach (var part in chunk.Candidates[0].Content.Parts)
                        {
                            if (part.Text != null)
                            {
                                Console.Write(part.Text);
                            }
                        }
                    }
                }
                Console.WriteLine("\n[Complete]");
            }
            else
            {
                Console.WriteLine("[Phase 1] No function calls detected, streaming direct response");
                
                // Add assistant's response to conversation 
                conversationHistory.Add(assistantResponse);
                
                // Stream the non-function response
                var responseStream = predictionServiceClient.StreamGenerateContent(initialRequest);
                var enumerableStream = responseStream.GetResponseStream();
                
                Console.WriteLine("\nRESPONSE:");
                await foreach (var chunk in enumerableStream)
                {
                    if (chunk.Candidates.Count > 0 && chunk.Candidates[0].Content.Parts.Count > 0)
                    {
                        foreach (var part in chunk.Candidates[0].Content.Parts)
                        {
                            if (part.Text != null)
                            {
                                Console.Write(part.Text);
                            }
                        }
                    }
                }
                Console.WriteLine("\n[Complete]");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            
            if (ex.InnerException != null)
            {
                Console.WriteLine($"INNER EXCEPTION: {ex.InnerException.Message}");
                Console.WriteLine(ex.InnerException.StackTrace);
            }
            
            // Try a simpler approach as fallback
            Console.WriteLine("\n[FALLBACK] Trying simpler non-streaming approach");
            
            try
            {
                var simpleRequest = new GenerateContentRequest
                {
                    Model = $"projects/{projectId}/locations/{location}/publishers/{publisher}/models/{model}",
                    Contents =
                    {
                        new Content
                        {
                            Role = "USER",
                            Parts =
                            {
                                new Part { Text = "Tell me about the weather in New York and London without using any external data or tools." }
                            }
                        }
                    }
                };
                
                var simpleResponse = await predictionServiceClient.GenerateContentAsync(simpleRequest);
                if (simpleResponse.Candidates.Count > 0 && 
                    simpleResponse.Candidates[0].Content.Parts.Count > 0 &&
                    simpleResponse.Candidates[0].Content.Parts[0].Text != null)
                {
                    Console.WriteLine("\nFALLBACK RESPONSE:");
                    Console.WriteLine(simpleResponse.Candidates[0].Content.Parts[0].Text);
                }
            }
            catch (Exception fallbackEx)
            {
                Console.WriteLine($"FALLBACK ERROR: {fallbackEx.Message}");
            }
        }
    }
}