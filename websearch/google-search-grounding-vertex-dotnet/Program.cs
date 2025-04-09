using Google.Cloud.AIPlatform.V1;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;

class Program
{
    static async Task Main(string[] args)
    {
        var sample = new GroundingGoogleSearchExample();
        await sample.RunAsync();
    }
}

public class GroundingGoogleSearchExample
{
    private static (string projectId, string serviceAccountKey) LoadCredentials()
    {
        try
        {
            string projectRoot = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.Parent.Parent.FullName;
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

    public async Task<string> RunAsync(
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

        var prompt = "What is the weather like in Adelaide tomorrow??";

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
            Tools =
            {
                new Tool
                {
                    GoogleSearch = new Tool.Types.GoogleSearch()
                }
            }
        };

        GenerateContentResponse response = await predictionServiceClient.GenerateContentAsync(generateContentRequest);

        string responseText = response.Candidates[0].Content.Parts[0].Text;
        Console.WriteLine(responseText);

        return responseText;
    }
}