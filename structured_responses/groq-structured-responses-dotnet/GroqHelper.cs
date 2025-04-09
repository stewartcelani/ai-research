using System.Text.Json;

public static class GroqHelper
{
    public static string GetApiKey()
    {
        try
        {
            var projectRoot = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.Parent.Parent.FullName;
            var apiKeysPath = Path.Combine(projectRoot, "API_KEYS.json");
            var jsonContent = File.ReadAllText(apiKeysPath);
            using JsonDocument doc = JsonDocument.Parse(jsonContent);
            var googleCloud = doc.RootElement.GetProperty("groq");
            var apiKey = googleCloud.GetProperty("api_key").GetString();
            return apiKey;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading credentials: {ex.Message}");
            throw;
        }
    }


}