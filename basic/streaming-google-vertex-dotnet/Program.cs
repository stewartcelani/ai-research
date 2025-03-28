using Google.Cloud.AIPlatform.V1;
using System;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;

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

    public async Task<string> TextInput(
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

        // This returns a StreamGenerateContentStream
        var responseStream = predictionServiceClient.StreamGenerateContent(generateContentRequest);

        // Convert it to an enumerable stream
        var enumerableStream = responseStream.GetResponseStream();

        string fullResponse = "";

        // Now we can use await foreach
        await foreach (var response in enumerableStream)
        {
            if (response.Candidates.Count > 0 &&
                response.Candidates[0].Content.Parts.Count > 0 &&
                response.Candidates[0].Content.Parts[0].Text != null)
            {
                var textChunk = response.Candidates[0].Content.Parts[0].Text;
                fullResponse += textChunk;
                Console.WriteLine($"Received chunk: {textChunk}");
            }
        }

        // Return the full response
        return fullResponse;
    }
}