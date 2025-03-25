using Google.Cloud.AIPlatform.V1;
using System;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;

class Program
{
    static async Task Main(string[] args)
    {
        var sample = new TextInputSample();
        await sample.TextInput();
    }
}

public class TextInputSample
{
    // Load project ID from API_KEYS.json
    private static readonly string PROJECT_ID = LoadProjectId();
    
    private static string LoadProjectId()
    {
        try
        {
            string projectRoot = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.Parent.Parent.FullName;
            string apiKeysPath = Path.Combine(projectRoot, "API_KEYS.json");
            string jsonContent = File.ReadAllText(apiKeysPath);
            using JsonDocument doc = JsonDocument.Parse(jsonContent);
            return doc.RootElement.GetProperty("googleCloud").GetProperty("projectId").GetString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading Google Cloud project ID: {ex.Message}");
            return "your-project-id-here";
        }
    }
    
    public async Task<string> TextInput(
        string projectId = null,
        string location = "us-central1",
        string publisher = "google",
        string model = "gemini-2.0-flash")
    {
        // Use loaded project ID if not explicitly provided
        projectId = projectId ?? PROJECT_ID;

        var predictionServiceClient = new PredictionServiceClientBuilder
        {
            Endpoint = $"{location}-aiplatform.googleapis.com"
        }.Build();
        string prompt = @"What's a good name for a flower shop that specializes in selling bouquets of dried flowers?";

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
            }
        };

        GenerateContentResponse response = await predictionServiceClient.GenerateContentAsync(generateContentRequest);

        string responseText = response.Candidates[0].Content.Parts[0].Text;
        Console.WriteLine(responseText);

        return responseText;
    }
}