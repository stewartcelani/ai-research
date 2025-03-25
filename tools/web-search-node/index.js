import OpenAI from "openai";
import { tavily } from "@tavily/core";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

// Get the directory name of the current module
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Load API keys from API_KEYS.json
const apiKeysPath = path.join(__dirname, '..', '..', 'API_KEYS.json');
const apiKeys = JSON.parse(fs.readFileSync(apiKeysPath, 'utf8'));

// Initialize APIs with your keys
const openai = new OpenAI({
  apiKey: apiKeys.openai.api_key
});

const tvly = tavily({ 
  apiKey: apiKeys.tavily.api_key
});

// Define the tools the model can use
const tools = [
  {
    type: "function",
    function: {
      name: "web_search",
      description: "Search the web for current information on a given query. Only use when you need up-to-date information or don't know the answer.",
      parameters: {
        type: "object",
        properties: {
          query: {
            type: "string",
            description: "The search query to look up on the web",
          },
        },
        required: ["query"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "calculate",
      description: "Perform a mathematical calculation",
      parameters: {
        type: "object",
        properties: {
          expression: {
            type: "string",
            description: "The mathematical expression to evaluate (e.g., '2 + 2')",
          },
        },
        required: ["expression"],
      },
    },
  },
  {
    type: "function",
    function: {
      name: "get_current_time",
      description: "Get the current date and time",
      parameters: {
        type: "object",
        properties: {},
      },
    },
  },
];

// Implement the tool functions
async function webSearch(query) {
  try {
    console.log(`Performing web search for: "${query}"`);
    const response = await tvly.search(query);
    return response;
  } catch (error) {
    console.error("Error during web search:", error);
    return { error: "Failed to retrieve search results" };
  }
}

function calculate(expression) {
  try {
    console.log(`Calculating: ${expression}`);
    // Using a safer approach than eval
    const result = Function(`"use strict"; return (${expression})`)();
    return {
      result: result,
    };
  } catch (error) {
    return { error: "Invalid mathematical expression" };
  }
}

function getCurrentTime() {
  console.log("Getting current time");
  const now = new Date();
  return {
    iso: now.toISOString(),
    readable: now.toString(),
  };
}

// Function to process tool calls
async function processToolCalls(toolCalls) {
  const toolResults = [];

  for (const toolCall of toolCalls) {
    const functionName = toolCall.function.name;
    const functionArgs = JSON.parse(toolCall.function.arguments);
    
    let result;
    switch (functionName) {
      case "web_search":
        result = await webSearch(functionArgs.query);
        break;
      case "calculate":
        result = calculate(functionArgs.expression);
        break;
      case "get_current_time":
        result = getCurrentTime();
        break;
      default:
        result = { error: `Unknown function: ${functionName}` };
    }

    toolResults.push({
      tool_call_id: toolCall.id,
      output: JSON.stringify(result),
    });
  }

  return toolResults;
}

// Main function to process a user query
async function processQuery(userQuery) {
  try {
    console.log(`\nProcessing query: "${userQuery}"`);

    // First interaction with the model to determine which tools to use
    const initialResponse = await openai.chat.completions.create({
      model: "gpt-4o-mini",
      messages: [
        {
          role: "system",
          content: `You are a helpful assistant that can use tools to answer user queries. 
          Only use the web_search tool when necessary for current information or facts you don't know.
          For simple calculations, definitions, or general knowledge, use your built-in knowledge.
          For mathematical calculations, use the calculate tool.
          For current time information, use the get_current_time tool.
          Think carefully about whether you need to use web search before doing so.`,
        },
        { role: "user", content: userQuery },
      ],
      tools: tools,
      tool_choice: "auto",
    });

    const initialMessage = initialResponse.choices[0].message;

    // If no tool calls were made, return the direct response
    if (!initialMessage.tool_calls) {
      return {
        response: initialMessage.content,
        usedTools: [],
      };
    }

    // Process the tool calls
    const toolResults = await processToolCalls(initialMessage.tool_calls);
    const usedTools = initialMessage.tool_calls.map(call => call.function.name);
    
    // Final interaction with the model to get the answer
    const finalResponse = await openai.chat.completions.create({
      model: "gpt-4o-mini",
      messages: [
        {
          role: "system",
          content: `You are a helpful assistant that provides accurate and concise information.
          Respond to the user's query with relevant information based on the tool results.
          If the results came from a web search, make sure to provide up-to-date information.`,
        },
        { role: "user", content: userQuery },
        initialMessage,
        ...toolResults.map(result => ({
          role: "tool",
          tool_call_id: result.tool_call_id,
          content: result.output,
        })),
      ],
    });

    return {
      response: finalResponse.choices[0].message.content,
      usedTools,
    };
  } catch (error) {
    console.error("Error processing query:", error);
    return {
      response: "Sorry, I encountered an error while processing your request.",
      error: error.message,
    };
  }
}

// Example queries to demonstrate web search required and not required
const exampleQueries = [
  // Queries likely to require web search
  "What is the current weather in Sydney, Australia?",
  "What are the latest news about AI developments?",
  "Who won the most recent Formula 1 race?",
  
  // Queries that likely don't require web search
  "What is the capital of France?",
  "Calculate 347 times 512",
  "What is the current time?",
  "Explain how photosynthesis works",
];

// Run the examples
async function runExamples() {
  console.log("STARTING EXAMPLES\n");
  
  for (const query of exampleQueries) {
    console.log("\n===================================");
    console.log(`QUERY: ${query}`);
    console.log("===================================\n");
    
    const result = await processQuery(query);
    
    console.log("\nFINAL RESPONSE:");
    console.log(result.response);
    
    console.log("\nTOOLS USED:");
    if (result.usedTools.length > 0) {
      console.log(result.usedTools.join(", "));
    } else {
      console.log("No tools were used. Used model's knowledge base.");
    }
    
    console.log("\n===================================\n");
  }
}

// Process a single query manually
async function processSingleQuery(query) {
  const result = await processQuery(query);
  
  console.log("\nFINAL RESPONSE:");
  console.log(result.response);
  
  console.log("\nTOOLS USED:");
  if (result.usedTools.length > 0) {
    console.log(result.usedTools.join(", "));
  } else {
    console.log("No tools were used. Used model's knowledge base.");
  }
}

// Main execution
async function main() {
  if (process.argv.length > 2) {
    // If a command line argument is provided, process it as a query
    const query = process.argv.slice(2).join(" ");
    await processSingleQuery(query);
  } else {
    // Otherwise, run the example queries
    await runExamples();
  }
}

// Run the main function
main().catch(error => {
  console.error("Fatal error:", error);
  process.exit(1);
});