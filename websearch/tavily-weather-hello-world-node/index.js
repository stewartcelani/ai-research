import { tavily } from "@tavily/core";
import OpenAI from "openai";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

// Get the directory name of the current module
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Load API keys from API_KEYS.json
const apiKeysPath = path.join(__dirname, '..', '..', 'API_KEYS.json');
const apiKeys = JSON.parse(fs.readFileSync(apiKeysPath, 'utf8'));

const tvly = tavily({ apiKey: apiKeys.tavily.api_key });

const openai = new OpenAI({
  apiKey: apiKeys.openai.api_key,
});

async function runSearch(userQuery) {
  // Get search results from Tavily
  const response = await tvly.search(userQuery);
  console.log(response);
  
  // Process results with GPT-4o-mini
  await processWithGPT(response, userQuery);
}

async function processWithGPT(tavilyResponse, userQuery) {
  const systemPrompt = "You are a helpful assistant that provides accurate and concise information based on web search results. Analyze the provided search results and respond to the user's query with relevant information. Adapt to local conventions appropriate to the query location (e.g., use Celsius for temperatures in most countries outside the US, use 24-hour time format where appropriate, format dates according to local customs DD/MM/YYYY vs MM/DD/YYYY, use metric or imperial units as locally preferred). Your goal is to provide the most useful and contextually appropriate response based on the search results.";
  
  // Format user content with sections
  const userContent = `## Web search results
${JSON.stringify(tavilyResponse)}

## User query:
${userQuery}`;

  const completion = await openai.chat.completions.create({
    model: "gpt-4o-mini",
    messages: [
      { role: "system", content: systemPrompt },
      { role: "user", content: userContent }
    ],
  });

  console.log("\nGPT-4o-mini synthesized answer for query: " + userQuery + "\n\n");
  console.log(completion.choices[0].message.content);
}

// Example usage
const query = "What is the weather in Adelaide going to be like this week?";
runSearch(query).catch(error => console.error(error));