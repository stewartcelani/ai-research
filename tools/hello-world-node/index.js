const OpenAI = require("openai");
const fs = require('fs');
const path = require('path');

// Load API keys from API_KEYS.json
const apiKeysPath = path.join(__dirname, '..', '..', 'API_KEYS.json');
const apiKeys = JSON.parse(fs.readFileSync(apiKeysPath, 'utf8'));

const openai = new OpenAI({
    apiKey: apiKeys.openai.api_key,
});

const functions = [
    {
        name: "greet",
        description: "Greets a person by name",
        parameters: {
            type: "object",
            properties: {
                name: {
                    type: "string",
                    description: "The name of the person to greet"
                }
            },
            required: ["name"]
        }
    }
];

async function main() {
    const userMessage = "Greet someone named John";
    const completion = await openai.chat.completions.create({
        model: "gpt-4o-mini",
        messages: [{ role: "user", content: userMessage }],
        functions: functions,
    });

    const response = completion.choices[0].message;
    if (response.function_call) {
        const { name, arguments: args } = response.function_call;
        if (name === "greet") {
            const { name: personName } = JSON.parse(args);
            const greeting = greet(personName);
            console.log(greeting);
        }
    } else {
        console.log(response.content);
    }
}

function greet(name) {
    return `Hello, ${name}!`;
}

main();