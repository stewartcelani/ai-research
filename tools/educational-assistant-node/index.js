import OpenAI from "openai";
import chalk from "chalk";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

// Get the directory name of the current module
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Load API keys from API_KEYS.json
const apiKeysPath = path.join(__dirname, '..', '..', 'API_KEYS.json');
const apiKeys = JSON.parse(fs.readFileSync(apiKeysPath, 'utf8'));

// Initialize the OpenAI client with your API key
const openai = new OpenAI({
    apiKey: apiKeys.openai.api_key,
});

// Define an array of educational tools that the AI can use
const educationalTools = [
  {
    name: "study_planner",
    description: "Creates a study schedule based on specified subjects and available time",
    parameters: {
      type: "object",
      properties: {
        subjects: {
          type: "array",
          description: "List of subjects to study",
          items: {
            type: "string"
          }
        },
        hoursAvailable: {
          type: "number",
          description: "Total hours available for studying"
        },
        prioritySubject: {
          type: "string",
          description: "Subject that should receive more focus (optional)"
        },
        startDate: {
          type: "string",
          description: "Start date for the study plan (YYYY-MM-DD format)"
        },
        daysUntilDeadline: {
          type: "number",
          description: "Number of days until the deadline or exam"
        }
      },
      required: ["subjects", "hoursAvailable", "daysUntilDeadline"]
    }
  },
  {
    name: "formula_solver",
    description: "Solves mathematical problems and shows step-by-step solutions",
    parameters: {
      type: "object",
      properties: {
        formula: {
          type: "string",
          description: "The mathematical formula or equation to solve"
        },
        variables: {
          type: "object",
          description: "Key-value pairs of known variables and their values",
          additionalProperties: {
            type: "number"
          }
        },
        solveFor: {
          type: "string",
          description: "The variable to solve for"
        },
        showSteps: {
          type: "boolean",
          description: "Whether to show the step-by-step solution"
        }
      },
      required: ["formula", "solveFor"]
    }
  },
  {
    name: "concept_explainer",
    description: "Explains complex concepts with analogies and examples at different educational levels",
    parameters: {
      type: "object",
      properties: {
        concept: {
          type: "string",
          description: "The concept to explain"
        },
        educationLevel: {
          type: "string",
          description: "The target education level (elementary, middle, high, undergraduate, graduate)",
          enum: ["elementary", "middle", "high", "undergraduate", "graduate"]
        },
        includeAnalogies: {
          type: "boolean",
          description: "Whether to include analogies in the explanation"
        },
        includeExamples: {
          type: "boolean",
          description: "Whether to include real-world examples"
        },
        formatAsMarkdown: {
          type: "boolean",
          description: "Whether to format the explanation as markdown"
        }
      },
      required: ["concept", "educationLevel"]
    }
  },
  {
    name: "research_summarizer",
    description: "Summarizes research papers or articles and extracts key points",
    parameters: {
      type: "object",
      properties: {
        title: {
          type: "string",
          description: "Title of the research paper or article"
        },
        abstract: {
          type: "string",
          description: "Abstract or introduction of the paper"
        },
        focusAreas: {
          type: "array",
          description: "Specific areas to focus on in the summary",
          items: {
            type: "string"
          }
        },
        maxLength: {
          type: "number",
          description: "Maximum length of the summary in words"
        },
        includeMethodology: {
          type: "boolean",
          description: "Whether to include methodology details"
        },
        includeFindings: {
          type: "boolean",
          description: "Whether to include key findings"
        },
        includeLimitations: {
          type: "boolean",
          description: "Whether to include study limitations"
        }
      },
      required: ["title", "abstract"]
    }
  }
];

/**
 * Processes a user query and calls the appropriate educational tool
 * @param {string} userQuery - The user's question or request
 * @param {boolean} debug - Whether to enable detailed debug logging
 * @returns {Promise<object>} - The result of processing the query
 */
async function processEducationalQuery(userQuery, debug = false) {
  try {
    logDebug(debug, "SYSTEM", `Processing query: "${userQuery}"`);
    logDebug(debug, "SYSTEM", `Available tools: ${educationalTools.map(tool => tool.name).join(", ")}`);

    // Step 1: Call the OpenAI API to analyze the query and determine which tool to use
    logDebug(debug, "SYSTEM", "Sending request to OpenAI API...");
    const completion = await openai.chat.completions.create({
      model: "gpt-4o-mini", // You can change this to a different model
      messages: [{ role: "user", content: userQuery }],
      functions: educationalTools,
      function_call: "auto",
    });

    // Step 2: Extract the response
    const response = completion.choices[0].message;
    logDebug(debug, "AI RESPONSE", `Raw response type: ${response.function_call ? "function_call" : "content"}`);

    // Step 3: If a function call was generated, process it
    if (response.function_call) {
      const { name, arguments: argsString } = response.function_call;
      const args = JSON.parse(argsString);
      
      logDebug(debug, "FUNCTION CALL", `Selected tool: ${chalk.green(name)}`);
      logDebug(debug, "FUNCTION CALL", `Arguments: ${chalk.yellow(JSON.stringify(args, null, 2))}`);

      // Step 4: Execute the appropriate function based on the tool name
      switch (name) {
        case "study_planner":
          return await createStudyPlan(args, debug);
        
        case "formula_solver":
          return await solveMathProblem(args, debug);
        
        case "concept_explainer":
          return await explainConcept(args, debug);
        
        case "research_summarizer":
          return await summarizeResearch(args, debug);
        
        default:
          throw new Error(`Unknown tool: ${name}`);
      }
    } else {
      // If no function call was generated, return the text response
      logDebug(debug, "TEXT RESPONSE", response.content);
      return { type: "text_response", content: response.content };
    }
  } catch (error) {
    logDebug(debug, "ERROR", `Error processing query: ${error.message}`);
    console.error(chalk.red(`Error: ${error.message}`));
    return { type: "error", error: error.message };
  }
}

/**
 * Creates a study plan based on specified subjects and available time
 * @param {object} args - The arguments for the study planner
 * @param {boolean} debug - Whether to enable detailed debug logging
 * @returns {Promise<object>} - The generated study plan
 */
async function createStudyPlan(args, debug) {
  const { subjects, hoursAvailable, prioritySubject, startDate, daysUntilDeadline } = args;
  
  logDebug(debug, "STUDY PLANNER", `Creating study plan for ${subjects.length} subjects over ${daysUntilDeadline} days`);
  logDebug(debug, "STUDY PLANNER", `Total hours available: ${hoursAvailable}`);

  // Calculate hours per day
  const hoursPerDay = hoursAvailable / daysUntilDeadline;
  logDebug(debug, "STUDY PLANNER", `Average hours per day: ${hoursPerDay.toFixed(2)}`);

  // Create a study schedule with weighted hours based on subjects
  let schedule = [];
  let totalWeight = subjects.length;
  
  // If there's a priority subject, give it more weight
  let subjectWeights = {};
  subjects.forEach(subject => {
    subjectWeights[subject] = subject === prioritySubject ? 2 : 1;
    totalWeight += subject === prioritySubject ? 1 : 0;
  });
  
  logDebug(debug, "STUDY PLANNER", `Subject weights: ${JSON.stringify(subjectWeights, null, 2)}`);

  // Calculate start date
  let currentDate = startDate ? new Date(startDate) : new Date();
  
  // Generate day-by-day schedule
  for (let day = 1; day <= daysUntilDeadline; day++) {
    let daySchedule = {
      day,
      date: new Date(currentDate).toISOString().split('T')[0],
      sessions: []
    };

    let remainingHours = hoursPerDay;
    
    // Distribute hours based on weights
    for (const subject of subjects) {
      const subjectHours = (subjectWeights[subject] / totalWeight) * hoursPerDay;
      const roundedHours = Math.round(subjectHours * 2) / 2; // Round to nearest 0.5
      
      if (roundedHours > 0) {
        daySchedule.sessions.push({
          subject,
          hours: roundedHours,
          timeBlock: generateTimeBlock(roundedHours)
        });
        
        remainingHours -= roundedHours;
      }
    }
    
    // Add any remaining time to the priority subject
    if (remainingHours > 0 && prioritySubject) {
      const existingSession = daySchedule.sessions.find(session => session.subject === prioritySubject);
      if (existingSession) {
        existingSession.hours += remainingHours;
        existingSession.timeBlock = generateTimeBlock(existingSession.hours);
      } else {
        daySchedule.sessions.push({
          subject: prioritySubject,
          hours: remainingHours,
          timeBlock: generateTimeBlock(remainingHours)
        });
      }
    }
    
    schedule.push(daySchedule);
    
    // Move to next day
    currentDate.setDate(currentDate.getDate() + 1);
  }
  
  logDebug(debug, "STUDY PLANNER", `Generated schedule with ${schedule.length} days`);
  
  return {
    type: "study_plan",
    subjects,
    totalHours: hoursAvailable,
    daysUntilDeadline,
    averageHoursPerDay: hoursPerDay,
    prioritySubject: prioritySubject || "None specified",
    schedule
  };
}

/**
 * Generates a sample time block string based on hours
 * @param {number} hours - Number of hours for the time block
 * @returns {string} - A formatted time block string
 */
function generateTimeBlock(hours) {
  // This is a simplified function that would be more complex in a real application
  if (hours <= 1) {
    return "1 session of 1 hour";
  } else if (hours <= 2) {
    return "1 session of 2 hours";
  } else {
    const sessions = Math.ceil(hours / 1.5);
    return `${sessions} sessions of ${(hours / sessions).toFixed(1)} hours each`;
  }
}

/**
 * Solves a mathematical problem and shows steps
 * @param {object} args - The arguments for the formula solver
 * @param {boolean} debug - Whether to enable detailed debug logging
 * @returns {Promise<object>} - The solution with steps
 */
async function solveMathProblem(args, debug) {
  const { formula, variables = {}, solveFor, showSteps = true } = args;
  
  logDebug(debug, "FORMULA SOLVER", `Solving formula: ${formula}`);
  logDebug(debug, "FORMULA SOLVER", `Solving for: ${solveFor}`);
  logDebug(debug, "FORMULA SOLVER", `Given variables: ${JSON.stringify(variables, null, 2)}`);

  try {
    // In a real implementation, you might use a math library like mathjs
    // For this example, we'll simulate solving a simple equation
    
    // Simplified example for demonstration purposes
    let solution;
    let steps = [];
    
    // Simulate solving with steps
    if (formula.includes("=")) {
      const [leftSide, rightSide] = formula.split("=");
      steps.push(`Starting with the equation: ${formula}`);
      
      if (Object.keys(variables).length > 0) {
        steps.push(`Substituting known variables: ${JSON.stringify(variables)}`);
        // In a real implementation, we would actually substitute variables here
      }
      
      // Simplified solver logic - this would be much more sophisticated in a real implementation
      const mockSolution = Math.random() * 10; // Placeholder solution
      solution = mockSolution.toFixed(2);
      
      steps.push(`Isolating ${solveFor} on one side of the equation`);
      steps.push(`Simplifying the equation`);
      steps.push(`Final result: ${solveFor} = ${solution}`);
    } else {
      // Handle expressions without equals sign
      steps.push(`Evaluating the expression: ${formula}`);
      
      // Simplified calculation - would be more complex in real implementation
      const mockResult = Math.random() * 100; // Placeholder result
      solution = mockResult.toFixed(2);
      
      steps.push(`Final result: ${solution}`);
    }
    
    logDebug(debug, "FORMULA SOLVER", `Solution: ${solveFor} = ${solution}`);
    if (showSteps) {
      logDebug(debug, "FORMULA SOLVER", `Steps: ${steps.join("\n  ")}`);
    }
    
    return {
      type: "math_solution",
      formula,
      solveFor,
      solution,
      steps: showSteps ? steps : [],
      variables
    };
  } catch (error) {
    logDebug(debug, "FORMULA SOLVER ERROR", error.message);
    return {
      type: "math_error",
      formula,
      error: error.message
    };
  }
}

/**
 * Explains a concept with analogies and examples
 * @param {object} args - The arguments for the concept explainer
 * @param {boolean} debug - Whether to enable detailed debug logging
 * @returns {Promise<object>} - The explanation with examples and analogies
 */
async function explainConcept(args, debug) {
  const { 
    concept, 
    educationLevel, 
    includeAnalogies = true, 
    includeExamples = true,
    formatAsMarkdown = true
  } = args;
  
  logDebug(debug, "CONCEPT EXPLAINER", `Explaining concept: ${concept}`);
  logDebug(debug, "CONCEPT EXPLAINER", `For education level: ${educationLevel}`);
  logDebug(debug, "CONCEPT EXPLAINER", `Include analogies: ${includeAnalogies}`);
  logDebug(debug, "CONCEPT EXPLAINER", `Include examples: ${includeExamples}`);
  
  // In a real implementation, this would call another AI model or a knowledge base
  // For this example, we'll return a mock explanation
  
  let explanation = `Explanation of ${concept} for ${educationLevel} level students`;
  const analogies = includeAnalogies ? [
    `Analogy 1: ${concept} is like...`,
    `Analogy 2: Think of ${concept} as...`
  ] : [];
  
  const examples = includeExamples ? [
    `Example 1: In real life, ${concept} can be seen when...`,
    `Example 2: A practical application of ${concept} is...`
  ] : [];
  
  // Format the explanation based on the requested format
  let formattedExplanation;
  if (formatAsMarkdown) {
    formattedExplanation = `# ${concept}\n\n## Explanation\n${explanation}\n\n`;
    
    if (analogies.length > 0) {
      formattedExplanation += `## Analogies\n- ${analogies.join('\n- ')}\n\n`;
    }
    
    if (examples.length > 0) {
      formattedExplanation += `## Examples\n- ${examples.join('\n- ')}\n\n`;
    }
    
    formattedExplanation += `## Summary\nThese explanations of ${concept} are tailored for ${educationLevel} level understanding.`;
  } else {
    formattedExplanation = explanation;
    
    if (analogies.length > 0) {
      formattedExplanation += `\n\nAnalogies:\n${analogies.join('\n')}`;
    }
    
    if (examples.length > 0) {
      formattedExplanation += `\n\nExamples:\n${examples.join('\n')}`;
    }
    
    formattedExplanation += `\n\nSummary: These explanations of ${concept} are tailored for ${educationLevel} level understanding.`;
  }
  
  logDebug(debug, "CONCEPT EXPLAINER", `Generated explanation of ${concept} (${formattedExplanation.length} chars)`);
  
  return {
    type: "concept_explanation",
    concept,
    educationLevel,
    explanation: formattedExplanation,
    hasAnalogies: includeAnalogies,
    hasExamples: includeExamples,
    format: formatAsMarkdown ? "markdown" : "plain_text"
  };
}

/**
 * Summarizes research papers or articles
 * @param {object} args - The arguments for the research summarizer
 * @param {boolean} debug - Whether to enable detailed debug logging
 * @returns {Promise<object>} - The research summary
 */
async function summarizeResearch(args, debug) {
  const { 
    title, 
    abstract, 
    focusAreas = [], 
    maxLength = 500,
    includeMethodology = true,
    includeFindings = true,
    includeLimitations = false
  } = args;
  
  logDebug(debug, "RESEARCH SUMMARIZER", `Summarizing paper: "${title}"`);
  logDebug(debug, "RESEARCH SUMMARIZER", `Abstract length: ${abstract.length} characters`);
  logDebug(debug, "RESEARCH SUMMARIZER", `Focus areas: ${focusAreas.join(", ") || "None specified"}`);
  logDebug(debug, "RESEARCH SUMMARIZER", `Max length: ${maxLength} words`);
  
  // In a real implementation, this would analyze the paper more thoroughly
  // For this example, we'll generate a mock summary
  
  // Calculate a rough word count for the abstract
  const wordCount = abstract.split(/\s+/).length;
  const compressionRatio = Math.min(maxLength / wordCount, 1);
  
  logDebug(debug, "RESEARCH SUMMARIZER", `Abstract word count: ${wordCount}`);
  logDebug(debug, "RESEARCH SUMMARIZER", `Compression ratio: ${compressionRatio.toFixed(2)}`);
  
  // Create sections of the summary based on requested components
  const sections = {
    overview: `This is a summary of the paper titled "${title}". The paper explores...`,
    methodology: includeMethodology ? "The researchers used a methodology involving..." : null,
    findings: includeFindings ? "The key findings of this research include..." : null,
    limitations: includeLimitations ? "The study has several limitations, including..." : null
  };
  
  // Filter out null sections and join them
  const summaryText = Object.values(sections)
    .filter(section => section !== null)
    .join("\n\n");
  
  // Create a list of key terms (simulated)
  const keyTerms = ["term1", "term2", "term3"];
  
  logDebug(debug, "RESEARCH SUMMARIZER", `Generated summary (${summaryText.split(/\s+/).length} words)`);
  
  return {
    type: "research_summary",
    title,
    summary: summaryText,
    focusAreas,
    wordCount: summaryText.split(/\s+/).length,
    keyTerms,
    sections: Object.keys(sections).filter(key => sections[key] !== null)
  };
}

/**
 * Logs debug information if debug mode is enabled
 * @param {boolean} debug - Whether debug mode is enabled
 * @param {string} category - The category of the debug message
 * @param {string} message - The debug message
 */
function logDebug(debug, category, message) {
  if (!debug) return;
  
  const timestamp = new Date().toISOString();
  
  // Format the output based on category
  switch (category) {
    case "SYSTEM":
      console.log(chalk.cyan(`[${timestamp}] [${category}] ${message}`));
      break;
    case "AI RESPONSE":
      console.log(chalk.blue(`[${timestamp}] [${category}] ${message}`));
      break;
    case "FUNCTION CALL":
      console.log(chalk.green(`[${timestamp}] [${category}] ${message}`));
      break;
    case "ERROR":
      console.log(chalk.red(`[${timestamp}] [${category}] ${message}`));
      break;
    case "STUDY PLANNER":
      console.log(chalk.magenta(`[${timestamp}] [${category}] ${message}`));
      break;
    case "FORMULA SOLVER":
    case "FORMULA SOLVER ERROR":
      console.log(chalk.yellow(`[${timestamp}] [${category}] ${message}`));
      break;
    case "CONCEPT EXPLAINER":
      console.log(chalk.greenBright(`[${timestamp}] [${category}] ${message}`));
      break;
    case "RESEARCH SUMMARIZER":
      console.log(chalk.blueBright(`[${timestamp}] [${category}] ${message}`));
      break;
    default:
      console.log(chalk.white(`[${timestamp}] [${category}] ${message}`));
  }
}

// Example usage with different educational queries
async function runExamples() {
  console.log(chalk.bold("\n=== Educational AI Assistant Tool Calling Demo ===\n"));
  
  const examples = [
    {
      query: "I need to study for my biology, chemistry, and physics exams. I have 30 hours available over the next 10 days, and biology is most important. Can you make me a study plan starting tomorrow?",
      debug: true
    },
    {
      query: "Can you solve the quadratic equation x^2 - 5x + 6 = 0 for x? Show me all the steps.",
      debug: true
    },
    {
      query: "Explain what quantum entanglement is for a high school student. Use some analogies.",
      debug: true
    },
    {
      query: "Can you summarize this research paper? Title: 'Effects of Climate Change on Marine Ecosystems' Abstract: 'This study explores the various impacts of rising ocean temperatures on marine biodiversity, with particular focus on coral reef systems and pelagic food webs. Data collected over a 10-year period shows significant changes in species distribution and declining population numbers for temperature-sensitive organisms.'",
      debug: true
    }
  ];
  
  for (let i = 0; i < examples.length; i++) {
    const { query, debug } = examples[i];
    console.log(chalk.bold(`\n=== Example ${i + 1} ===`));
    console.log(chalk.bold(`Query: "${query}"`));
    console.log(chalk.bold("=== Processing... ===\n"));
    
    const result = await processEducationalQuery(query, debug);
    
    console.log(chalk.bold("\n=== Result ==="));
    console.log(chalk.green(JSON.stringify(result, null, 2)));
    console.log(chalk.bold("\n=== End of Example ===\n"));
  }
}

// Export the functions for use in other files
export {
  processEducationalQuery,
  createStudyPlan,
  solveMathProblem,
  explainConcept,
  summarizeResearch
};

// If this file is run directly, execute the examples
if (import.meta.url === new URL(import.meta.url).href) {
  runExamples().catch(error => {
    console.error(chalk.red(`Error running examples: ${error.message}`));
    process.exit(1);
  });
}