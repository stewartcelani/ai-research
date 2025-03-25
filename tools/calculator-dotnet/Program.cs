using System.Text.Json;
using OpenAI.Chat;
using System.IO;

/// <summary>
/// Static class for calculator operations
/// </summary>
public static class Calculator
{
    /// <summary>
    /// Adds two numbers
    /// </summary>
    public static double Add(double a, double b) => a + b;

    /// <summary>
    /// Subtracts the second number from the first
    /// </summary>
    public static double Subtract(double a, double b) => a - b;

    /// <summary>
    /// Multiplies two numbers
    /// </summary>
    public static double Multiply(double a, double b) => a * b;

    /// <summary>
    /// Divides the first number by the second
    /// </summary>
    public static double Divide(double a, double b) => b != 0 ? a / b : double.NaN;
}

/// <summary>
/// Static class containing calculator tool definitions
/// </summary>
public static class CalculatorTools
{
    /// <summary>
    /// Gets the add tool definition
    /// </summary>
    public static ChatTool GetAddTool() => ChatTool.CreateFunctionTool(
        "add",
        "Adds two numbers together",
        BinaryData.FromBytes("""
                             {
                                 "type": "object",
                                 "properties": {
                                     "firstNumber": {
                                         "type": "number",
                                         "description": "The first number in the addition"
                                     },
                                     "secondNumber": {
                                         "type": "number",
                                         "description": "The second number in the addition"
                                     }
                                 },
                                 "required": ["firstNumber", "secondNumber"]
                             }
                             """u8.ToArray())
    );

    /// <summary>
    /// Gets the subtract tool definition
    /// </summary>
    public static ChatTool GetSubtractTool() => ChatTool.CreateFunctionTool(
        "subtract",
        "Subtracts the second number from the first number",
        BinaryData.FromBytes("""
                             {
                                 "type": "object",
                                 "properties": {
                                     "firstNumber": {
                                         "type": "number",
                                         "description": "The number to subtract from"
                                     },
                                     "secondNumber": {
                                         "type": "number",
                                         "description": "The number to subtract"
                                     }
                                 },
                                 "required": ["firstNumber", "secondNumber"]
                             }
                             """u8.ToArray())
    );

    /// <summary>
    /// Gets the multiply tool definition
    /// </summary>
    public static ChatTool GetMultiplyTool() => ChatTool.CreateFunctionTool(
        "multiply",
        "Multiplies two numbers together",
        BinaryData.FromBytes("""
                             {
                                 "type": "object",
                                 "properties": {
                                     "firstNumber": {
                                         "type": "number",
                                         "description": "The first number in the multiplication"
                                     },
                                     "secondNumber": {
                                         "type": "number",
                                         "description": "The second number in the multiplication"
                                     }
                                 },
                                 "required": ["firstNumber", "secondNumber"]
                             }
                             """u8.ToArray())
    );

    /// <summary>
    /// Gets the divide tool definition
    /// </summary>
    public static ChatTool GetDivideTool() => ChatTool.CreateFunctionTool(
        "divide",
        "Divides the first number by the second number",
        BinaryData.FromBytes("""
                             {
                                 "type": "object",
                                 "properties": {
                                     "firstNumber": {
                                         "type": "number",
                                         "description": "The dividend (number to be divided)"
                                     },
                                     "secondNumber": {
                                         "type": "number",
                                         "description": "The divisor (number to divide by)"
                                     }
                                 },
                                 "required": ["firstNumber", "secondNumber"]
                             }
                             """u8.ToArray())
    );
}

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
        Console.WriteLine("===== Calculator Examples with Dedicated Tools =====\n");

        // List of example calculations to demonstrate
        var calculationExamples = new List<string>
        {
            "Calculate 24 divided by 6",
            "What is 125 + 37?",
            "Multiply 14 by 8",
            "Calculate 100 - 42",
            "What is the result of 3.5 * 9.2?",
            "Divide 144 by 12"
        };

        // Process each calculation example
        foreach (var example in calculationExamples)
        {
            Console.WriteLine($"\n[EXAMPLE]: {example}");
            Console.WriteLine("------------------------------");
            await ProcessCalculation(example);
            Console.WriteLine("------------------------------");
        }
    }

    private static async Task ProcessCalculation(string userQuery)
    {
        Console.WriteLine($"[LOG] Starting processing for query: \"{userQuery}\"");
        
        // Create a ChatClient with the specified model
        Console.WriteLine($"[LOG] Initializing chat client with model: gpt-4o-mini");
        ChatClient client = new("gpt-4o-mini", API_KEY);

        // Create messages and options
        List<ChatMessage> messages = new()
        {
            new UserChatMessage(userQuery)
        };

        Console.WriteLine("[LOG] Setting up available calculator tools:");
        Console.WriteLine("   - add: Adds two numbers together");
        Console.WriteLine("   - subtract: Subtracts the second number from the first number");
        Console.WriteLine("   - multiply: Multiplies two numbers together");
        Console.WriteLine("   - divide: Divides the first number by the second number");
        
        ChatCompletionOptions options = new()
        {
            Tools = { 
                CalculatorTools.GetAddTool(),
                CalculatorTools.GetSubtractTool(), 
                CalculatorTools.GetMultiplyTool(), 
                CalculatorTools.GetDivideTool() 
            }
        };

        // Create a loop to handle potential tool calls
        var requiresAction = true;
        var turnCount = 0;

        while (requiresAction)
        {
            turnCount++;
            Console.WriteLine($"[LOG] Turn {turnCount}: Sending request to OpenAI API");
            
            // Get completion from the API
            Console.WriteLine("[LOG] Waiting for API response...");
            ChatCompletion completion = await client.CompleteChatAsync(messages, options);

            // Handle the response based on finish reason
            switch (completion.FinishReason)
            {
                case ChatFinishReason.Stop:
                    // Normal response - print it and exit the loop
                    Console.WriteLine("[LOG] Received normal response (no tool calls)");
                    Console.WriteLine($"[ASSISTANT]: {completion.Content[0].Text}");
                    requiresAction = false;
                    break;

                case ChatFinishReason.ToolCalls:
                    // Tool calls need to be processed
                    Console.WriteLine($"[LOG] Received response with {completion.ToolCalls.Count} tool call(s)");
                    messages.Add(new AssistantChatMessage(completion));

                    foreach (var toolCall in completion.ToolCalls)
                    {
                        Console.WriteLine($"[LOG] Processing tool call: ID={toolCall.Id}, Function={toolCall.FunctionName}");
                        Console.WriteLine($"[LOG] Raw function arguments: {toolCall.FunctionArguments}");
                        
                        using var argsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                        var firstNumber = argsJson.RootElement.GetProperty("firstNumber").GetDouble();
                        var secondNumber = argsJson.RootElement.GetProperty("secondNumber").GetDouble();
                        
                        Console.WriteLine($"[LOG] Parsed arguments: firstNumber={firstNumber}, secondNumber={secondNumber}");
                        
                        double result = 0;
                        string operationSymbol = "";

                        // Perform the appropriate operation based on which tool was called
                        Console.WriteLine($"[LOG] Executing operation: {toolCall.FunctionName}");
                        switch (toolCall.FunctionName)
                        {
                            case "add":
                                result = Calculator.Add(firstNumber, secondNumber);
                                operationSymbol = "+";
                                break;
                            case "subtract":
                                result = Calculator.Subtract(firstNumber, secondNumber);
                                operationSymbol = "-";
                                break;
                            case "multiply":
                                result = Calculator.Multiply(firstNumber, secondNumber);
                                operationSymbol = "*";
                                break;
                            case "divide":
                                result = Calculator.Divide(firstNumber, secondNumber);
                                operationSymbol = "/";
                                break;
                        }

                        Console.WriteLine($"[LOG] Operation result: {result}");
                        
                        // Add the tool response to the conversation
                        Console.WriteLine($"[LOG] Adding tool response to conversation: {result}");
                        messages.Add(new ToolChatMessage(toolCall.Id, result.ToString()));
                        Console.WriteLine($"[FUNCTION]: The result of {firstNumber} {operationSymbol} {secondNumber} = {result}");
                    }
                    break;

                default:
                    Console.WriteLine($"[LOG] Unexpected finish reason: {completion.FinishReason}");
                    Console.WriteLine($"Unexpected finish reason: {completion.FinishReason}");
                    requiresAction = false;
                    break;
            }
        }
        
        Console.WriteLine("[LOG] Processing complete");
    }
}