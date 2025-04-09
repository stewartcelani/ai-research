using System;

// Class to represent the structured response from Groq
public class StructuredResponse
{
    public string Goal { get; set; }
    public string Reasoning { get; set; }
    public bool UseTools { get; set; }
    public string Plan { get; set; }
    public string SuggestedChatTitle { get; set; }

    // Method to display each property clearly in the console
    public void LogProperties()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n========== STRUCTURED RESPONSE PROPERTIES ==========\n");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("GOAL:");
        Console.ResetColor();
        Console.WriteLine(Goal ?? "[No goal provided]");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("REASONING:");
        Console.ResetColor();
        Console.WriteLine(Reasoning ?? "[No reasoning provided]");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("USE TOOLS:");
        Console.ResetColor();
        Console.WriteLine(UseTools);
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("PLAN:");
        Console.ResetColor();
        if (!string.IsNullOrEmpty(Plan))
        {
            Console.WriteLine(Plan);
        }
        else
        {
            Console.WriteLine("[No plan provided]");
        }

        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("SUGGESTED CHAT TITLE:");
        Console.ResetColor();
        Console.WriteLine(SuggestedChatTitle ?? "[No chat title suggested]");
        Console.WriteLine();
    }
}