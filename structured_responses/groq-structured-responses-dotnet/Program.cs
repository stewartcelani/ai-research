using System.Text.Json;

class Program
{
    static async Task Main(string[] args)
    {
        // Set to true to run the desired example
        var runGeminiExample = false;
        var runQwqExample = false;
        var runLlamaScoutExample = false;
        var runLlamaMaverickExample = true;
        var runLlama33SpecdecExample = false;
        var runLlamaMaverickFireworksExample = false; // New Fireworks example
        
        /*var query = "please give me a breakdown of the new POE2 patch";*/
        var query = "can you please tell me the differences between the SA, VIC and QLD employee handbooks?";

        if (runGeminiExample)
        {
            var geminiExample = new Gemini2FlashExample(query);
            await geminiExample.RunAsync();
        }

        if (runQwqExample)
        {
            var qwqSample = new GroqQwqSample(query);
            await qwqSample.RunAsync();
        }

        if (runLlamaScoutExample)
        {
            var llamaScoutExample = new Llama4ScoutExample(query);
            await llamaScoutExample.RunAsync();
        }

        if (runLlamaMaverickExample)
        {
            var llamaMaverickExample = new Llama4MaverickExample(query);
            await llamaMaverickExample.RunAsync();
        }

        if (runLlama33SpecdecExample)
        {
            var llama33SpecdecExample = new Llama33SpecdecExample(query);
            await llama33SpecdecExample.RunAsync();
        }
        
        if (runLlamaMaverickFireworksExample)
        {
            var llamaMaverickFireworksExample = new Llama4MaverickFireworksExample(query);
            await llamaMaverickFireworksExample.RunAsync();
        }
    }
}