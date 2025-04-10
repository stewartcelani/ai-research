using System;
using System.IO;
using System.Text;

namespace groq_structured_responses_dotnet
{
    public static class LoggingHelper
    {
        public static string SanitizeQueryForFilename(string query)
        {
            // Remove invalid filename characters and replace with underscores
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                query = query.Replace(c, '_');
            }
            
            // Replace spaces with underscores
            query = query.Replace(' ', '_');
            
            // Limit the length to avoid too long filenames
            if (query.Length > 50)
            {
                query = query.Substring(0, 50);
            }
            
            return query;
        }
        
        public static string CreateLogDirectory()
        {
            // Just directly create the path based on known structure
            // From bin/Debug/net9.0 go up to project root and then to examples
            string examplesPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "examples"));
            
            Console.WriteLine($"Calculated examples path: {examplesPath}");
            
            try 
            {
                // Create examples directory if it doesn't exist
                if (!Directory.Exists(examplesPath))
                {
                    Console.WriteLine("Creating examples directory as it doesn't exist");
                    Directory.CreateDirectory(examplesPath);
                }
                
                return examplesPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating examples directory: {ex.Message}");
                
                // Fallback to a "Logs" folder in the current directory
                string fallbackPath = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
                Console.WriteLine($"Falling back to: {fallbackPath}");
                
                if (!Directory.Exists(fallbackPath))
                {
                    Directory.CreateDirectory(fallbackPath);
                }
                
                return fallbackPath;
            }
        }
    }
    
    public class TeeTextWriter : TextWriter
    {
        private readonly TextWriter _console;
        private readonly StringWriter _stringWriter;
        
        public TeeTextWriter(TextWriter console)
        {
            _console = console;
            _stringWriter = new StringWriter();
        }
        
        public override void Write(char value)
        {
            _console.Write(value);
            _stringWriter.Write(value);
        }
        
        public override void Write(string value)
        {
            _console.Write(value);
            _stringWriter.Write(value);
        }
        
        public override void WriteLine(string value)
        {
            _console.WriteLine(value);
            _stringWriter.WriteLine(value);
        }
        
        public string GetOutput()
        {
            return _stringWriter.ToString();
        }
        
        public override Encoding Encoding => _console.Encoding;
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stringWriter.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}