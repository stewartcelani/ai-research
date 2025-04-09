using System.Text.Json;

namespace groq_structured_responses_dotnet;

public static class GoogleHelper
{
    public static (string projectId, string serviceAccountKey) GetCredentials()
    {
        try
        {
            var projectRoot = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.Parent.Parent.FullName;
            var apiKeysPath = Path.Combine(projectRoot, "API_KEYS.json");
            var jsonContent = File.ReadAllText(apiKeysPath);
            using JsonDocument doc = JsonDocument.Parse(jsonContent);
            var googleCloud = doc.RootElement.GetProperty("googleCloud");
            var projectId = googleCloud.GetProperty("projectId").GetString();
            var serviceAccountKey = googleCloud.GetProperty("serviceAccountKey").ToString();
            return (projectId, serviceAccountKey);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading credentials: {ex.Message}");
            throw;
        }
    }
}