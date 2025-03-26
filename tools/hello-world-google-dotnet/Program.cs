using Google.Cloud.AIPlatform.V1;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Type = Google.Cloud.AIPlatform.V1.Type;
using Value = Google.Protobuf.WellKnownTypes.Value;

class Program
{
    static async Task Main(string[] args)
    {
        var sample = new ToolUseSample();

        // Call both methods
        Console.WriteLine("===== RUNNING TOOL USE SAMPLE =====");
        await sample.RunToolUseSample();

        Console.WriteLine("\n===== RUNNING STRUCTURED RESPONSE SAMPLE =====");
        string result = await sample.GenerateContentWithResponseSchema();
        Console.WriteLine($"Structured Response Result: {result}");
    }
}

public class ToolUseSample
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

    public async Task RunToolUseSample(
        string location = "us-central1",
        string publisher = "google",
        string model = "gemini-2.0-flash")
    {
        var (projectId, serviceAccountKey) = LoadCredentials();
        if (projectId == null || serviceAccountKey == null)
        {
            throw new InvalidOperationException("Failed to load credentials.");
        }

        var credential = GoogleCredential.FromJson(serviceAccountKey)
            .CreateScoped("https://www.googleapis.com/auth/cloud-platform");
        var predictionServiceClient = new PredictionServiceClientBuilder
        {
            Endpoint = $"{location}-aiplatform.googleapis.com",
            Credential = credential
        }.Build();

        // Define the user's prompt
        var userPromptContent = new Content
        {
            Role = "USER",
            Parts =
            {
                new Part { Text = "Greet someone named John" }
            }
        };

        // Define the tool for function calling
        const string functionName = "greet";
        var greetFunc = new FunctionDeclaration
        {
            Name = functionName,
            Description = "Greets a person by name",
            Parameters = new OpenApiSchema
            {
                Type = Type.Object,
                Properties =
                {
                    ["name"] = new OpenApiSchema
                    {
                        Type = Type.String,
                        Description = "The name of the person to greet"
                    }
                },
                Required = { "name" }
            }
        };

        // Send the prompt with the tool
        var generateContentRequest = new GenerateContentRequest
        {
            Model = $"projects/{projectId}/locations/{location}/publishers/{publisher}/models/{model}",
            GenerationConfig = new GenerationConfig
            {
                Temperature = 0.2f
            },
            Contents =
            {
                userPromptContent
            },
            Tools =
            {
                new Tool
                {
                    FunctionDeclarations = { greetFunc }
                }
            }
        };

        // Main conversation loop
        do
        {
            // Get completion from the API
            GenerateContentResponse response =
                await predictionServiceClient.GenerateContentAsync(generateContentRequest);
            var assistantContent = response.Candidates[0].Content;

            // Check if there is a function call
            var functionCall = assistantContent.Parts[0].FunctionCall;
            if (functionCall != null)
            {
                Console.WriteLine("[ASSISTANT]: I'll help you greet John.");
                Console.WriteLine($"Function call: {functionCall.Name}");

                string name = "";
                // Extract the arguments
                if (functionCall.Name == functionName && functionCall.Args.Fields.ContainsKey("name"))
                {
                    name = functionCall.Args.Fields["name"].StringValue;
                    var greeting = Greet(name);
                    Console.WriteLine($"[FUNCTION]: {greeting}");

                    // Create a new request with the function response
                    generateContentRequest = new GenerateContentRequest
                    {
                        Model = $"projects/{projectId}/locations/{location}/publishers/{publisher}/models/{model}",
                        Contents =
                        {
                            userPromptContent, // User prompt
                            assistantContent, // Function call response
                            new Content
                            {
                                Parts =
                                {
                                    new Part
                                    {
                                        FunctionResponse = new()
                                        {
                                            Name = functionName,
                                            Response = new()
                                            {
                                                Fields =
                                                {
                                                    { "result", Value.ForString(greeting) }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        },
                        Tools =
                        {
                            new Tool
                            {
                                FunctionDeclarations = { greetFunc }
                            }
                        }
                    };
                }
                else
                {
                    Console.WriteLine("Unexpected function call.");
                    break;
                }
            }
            else
            {
                // Normal text response - print it and exit the loop
                Console.WriteLine($"[ASSISTANT]: {assistantContent.Parts[0].Text}");
                break;
            }
        } while (true);
    }

    public async Task<string> GenerateContentWithResponseSchema(
        string location = "us-central1",
        string publisher = "google",
        string model = "gemini-2.0-flash")
    {
        var (projectId, serviceAccountKey) = LoadCredentials();
        if (projectId == null || serviceAccountKey == null)
        {
            throw new InvalidOperationException("Failed to load credentials.");
        }

        var credential = GoogleCredential.FromJson(serviceAccountKey)
            .CreateScoped("https://www.googleapis.com/auth/cloud-platform");
        var predictionServiceClient = new PredictionServiceClientBuilder
        {
            Endpoint = $"{location}-aiplatform.googleapis.com",
            Credential = credential
        }.Build();

        var responseSchema = new OpenApiSchema
        {
            Type = Type.Array,
            Items = new OpenApiSchema
            {
                Type = Type.Object,
                Properties =
                {
                    ["rating"] = new OpenApiSchema { Type = Type.Integer },
                    ["flavor"] = new OpenApiSchema { Type = Type.String }
                },
                Required = { "rating", "flavor" }
            }
        };

        string prompt = @"
            Reviews from our social media:

            - ""Absolutely loved it! Best ice cream I've ever had."" Rating: 4, Flavor: Strawberry Cheesecake
            - ""Quite good, but a bit too sweet for my taste."" Rating: 1, Flavor: Mango Tango";

        var generateContentRequest = new GenerateContentRequest
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
            GenerationConfig = new GenerationConfig
            {
                ResponseMimeType = "application/json",
                ResponseSchema = responseSchema
            },
        };

        GenerateContentResponse response = await predictionServiceClient.GenerateContentAsync(generateContentRequest);

        string responseText = response.Candidates[0].Content.Parts[0].Text;
        Console.WriteLine(responseText);

        return responseText;
    }

    private static string Greet(string name)
    {
        return $"Hello, {name}!";
    }
}