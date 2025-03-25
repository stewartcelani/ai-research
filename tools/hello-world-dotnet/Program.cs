using System.Text.Json;
using OpenAI.Chat;
using System.IO;

internal class Program
{
    // Load API key from API_KEYS.json
    private static readonly string API_KEY = LoadApiKey();
    
    private static string LoadApiKey()
    {
        try
        {
            string projectRoot = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.Parent.Parent.FullName;
            string apiKeysPath = Path.Combine(projectRoot, "API_KEYS.json");
            string jsonContent = File.ReadAllText(apiKeysPath);
            using JsonDocument doc = JsonDocument.Parse(jsonContent);
            return doc.RootElement.GetProperty("openai").GetProperty("api_key").GetString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading API key: {ex.Message}");
            return null;
        }
    }
    
    private static async Task Main()
    {
        // Create a ChatClient with the specified model
        ChatClient client = new("gpt-4o-mini", API_KEY);

        // Define the chat tool for function calling
        var greetTool = ChatTool.CreateFunctionTool(
            "greet",
            "Greets a person by name",
            BinaryData.FromBytes("""
                                 {
                                     "type": "object",
                                     "properties": {
                                         "name": {
                                             "type": "string",
                                             "description": "The name of the person to greet"
                                         }
                                     },
                                     "required": ["name"]
                                 }
                                 """u8.ToArray())
        );

        // Create messages and options
        List<ChatMessage> messages = new()
        {
            new UserChatMessage("Greet someone named John")
        };

        ChatCompletionOptions options = new()
        {
            Tools = { greetTool }
        };

        // Create a loop to handle potential tool calls
        var requiresAction = true;

        while (requiresAction)
        {
            // Get completion from the API
            ChatCompletion completion = await client.CompleteChatAsync(messages, options);

            // Handle the response based on finish reason
            switch (completion.FinishReason)
            {
                case ChatFinishReason.Stop:
                    // Normal response - print it and exit the loop
                    Console.WriteLine($"[ASSISTANT]: {completion.Content[0].Text}");
                    requiresAction = false;
                    break;

                case ChatFinishReason.ToolCalls:
                    // Tool calls need to be processed
                    messages.Add(new AssistantChatMessage(completion));

                    foreach (var toolCall in completion.ToolCalls)
                        if (toolCall.FunctionName == "greet")
                        {
                            using var argsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                            var name = argsJson.RootElement.GetProperty("name").GetString();
                            var greeting = Greet(name);

                            // Add the tool response to the conversation
                            messages.Add(new ToolChatMessage(toolCall.Id, greeting));
                            Console.WriteLine($"[FUNCTION]: {greeting}");
                        }

                    break;

                default:
                    Console.WriteLine($"Unexpected finish reason: {completion.FinishReason}");
                    requiresAction = false;
                    break;
            }
        }
    }
    private static string Greet(string name)
    {
        return $"Hello, {name}!";
    }
}