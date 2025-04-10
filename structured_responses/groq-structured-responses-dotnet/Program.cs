using System;
using System.IO;
using System.Threading.Tasks;

namespace groq_structured_responses_dotnet
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Set to true to run the desired example
            var runGeminiFlashExample = true;
            var runGeminiProExample = true;
            var runQwqExample = true;
            var runLlamaScoutExample = true;
            var runLlamaMaverickExample = false;
            var runLlama33SpecdecExample = false;
            var runGrok3MiniExample = false;
            var runLlamaMaverickFireworksExample = false;

            var showFullApiResponse = false;
            
            /*var query = "please give me a breakdown of the new POE2 patch";*/
            var query = "can you read the latest poe patch notes and give a comprehensive analysis of the changes?";
            
            // Sanitize query for filename
            string sanitizedQuery = LoggingHelper.SanitizeQueryForFilename(query);
            string logDirectory = LoggingHelper.CreateLogDirectory();
            
            // Check if we're using the fallback directory
            if (logDirectory.EndsWith("Logs"))
            {
                Console.WriteLine("NOTICE: Using fallback 'Logs' directory instead of 'examples' directory");
            }
            
            // Define log filename based on the query with timestamp to avoid overwriting
            string logFilename = Path.Combine(logDirectory, $"{sanitizedQuery}_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            Console.WriteLine($"Log will be saved to: {logFilename}");
            
            // Create log file
            using (StreamWriter logWriter = new StreamWriter(logFilename, false))
            {
                logWriter.WriteLine($"Query: {query}");
                logWriter.WriteLine($"Timestamp: {DateTime.Now}");
                logWriter.WriteLine();
                
                // Run models and capture output
                if (runGeminiFlashExample)
                {
                    string output = await RunModelWithOutputCapture(
                        async () =>
                        {
                            var geminiExample = new Gemini2FlashExample(query, showFullApiResponse);
                            await geminiExample.RunAsync();
                        }
                    );

                    logWriter.WriteLine(output);
                    logWriter.WriteLine(new string('-', 80));
                    logWriter.WriteLine();
                }
                
                
                if (runGeminiProExample)
                {
                    string output = await RunModelWithOutputCapture(
                        async () => {
                            var gemProExample = new Gemini25ProExample(query, showFullApiResponse);
                            await gemProExample.RunAsync();
                        }
                    );
                    
                    logWriter.WriteLine(output);
                    logWriter.WriteLine(new string('-', 80));
                    logWriter.WriteLine();
                }

                
                if (runQwqExample)
                {
                    string output = await RunModelWithOutputCapture(
                        async () => {
                            var qwqSample = new GroqQwqExample(query, showFullApiResponse);
                            await qwqSample.RunAsync();
                        }
                    );
                    
                    logWriter.WriteLine(output);
                    logWriter.WriteLine(new string('-', 80));
                    logWriter.WriteLine();
                }

                if (runLlamaScoutExample)
                {
                    string output = await RunModelWithOutputCapture(
                        async () => {
                            var llamaScoutExample = new Llama4ScoutExample(query, showFullApiResponse);
                            await llamaScoutExample.RunAsync();
                        }
                    );
                    
                    logWriter.WriteLine(output);
                    logWriter.WriteLine(new string('-', 80));
                    logWriter.WriteLine();
                }

                if (runLlamaMaverickExample)
                {
                    string output = await RunModelWithOutputCapture(
                        async () => {
                            var llamaMaverickExample = new Llama4MaverickExample(query, showFullApiResponse);
                            await llamaMaverickExample.RunAsync();
                        }
                    );
                    
                    logWriter.WriteLine(output);
                    logWriter.WriteLine(new string('-', 80));
                    logWriter.WriteLine();
                }

                if (runLlama33SpecdecExample)
                {
                    string output = await RunModelWithOutputCapture(
                        async () => {
                            var llama33SpecdecExample = new Llama33SpecdecExample(query, showFullApiResponse);
                            await llama33SpecdecExample.RunAsync();
                        }
                    );
                    
                    logWriter.WriteLine(output);
                    logWriter.WriteLine(new string('-', 80));
                    logWriter.WriteLine();
                }
                
                if (runGrok3MiniExample)
                {
                    string output = await RunModelWithOutputCapture(
                        async () => {
                            var grok3MiniExample = new Grok3MiniExample(query, "low", showFullApiResponse);
                            await grok3MiniExample.RunAsync();
                        }
                    );
                    
                    logWriter.WriteLine(output);
                    logWriter.WriteLine(new string('-', 80));
                    logWriter.WriteLine();
                }
                
                if (runLlamaMaverickFireworksExample)
                {
                    string output = await RunModelWithOutputCapture(
                        async () => {
                            var llamaMaverickFireworksExample = new Llama4MaverickFireworksExample(query, showFullApiResponse);
                            await llamaMaverickFireworksExample.RunAsync();
                        }
                    );
                    
                    logWriter.WriteLine(output);
                    logWriter.WriteLine(new string('-', 80));
                    logWriter.WriteLine();
                }
            }
            
            // Display the absolute path for clarity
            Console.WriteLine($"All outputs logged to: {Path.GetFullPath(logFilename)}");
        }
        
        static async Task<string> RunModelWithOutputCapture(Func<Task> modelRunAction)
        {
            // Save original output
            TextWriter originalOutput = Console.Out;
            
            try
            {
                // Create a tee writer to capture output while still displaying to console
                using (var teeWriter = new TeeTextWriter(originalOutput))
                {
                    // Redirect console output to our tee writer
                    Console.SetOut(teeWriter);
                    
                    // Run the model
                    await modelRunAction();
                    
                    // Get the captured output
                    return teeWriter.GetOutput();
                }
            }
            finally
            {
                // Restore original output
                Console.SetOut(originalOutput);
            }
        }
    }
}