// Load API key from API_KEYS.json
const fs = require('fs');
const path = require('path');

// Read API keys from the root of the project
const apiKeysPath = path.join(__dirname, '..', 'API_KEYS.json');
const apiKeys = JSON.parse(fs.readFileSync(apiKeysPath, 'utf8'));
const API_KEY = apiKeys.cohere.api_key;

const https = require('https');

const query = 'What is the capital of the United States?';
const documents = [
  'Carson City is the capital city of the American state of Nevada.',
  'The Commonwealth of the Northern Mariana Islands is a group of islands in the Pacific Ocean. Its capital is Saipan.',
  'Washington, D.C. (also known as simply Washington or D.C., and officially as the District of Columbia) is the capital of the United States. It is a federal district.',
  'Capital punishment (the death penalty) has existed in the United States since before the United States was a country. As of 2017, capital punishment is legal in 30 of the 50 states.'
];

// Prepare the request body in JSON format
const requestData = JSON.stringify({
  model: 'rerank-v3.5',
  query,
  documents,
  top_n: documents.length // Return all results
});

// Set up the request options
const options = {
  hostname: 'api.cohere.com',
  path: '/v2/rerank',
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${API_KEY}`,
    'Content-Length': Buffer.byteLength(requestData)
  }
};

// Make the request
function makeRequest() {
  return new Promise((resolve, reject) => {
    const req = https.request(options, (res) => {
      let data = '';
      
      // Handle data chunks
      res.on('data', (chunk) => {
        data += chunk;
      });
      
      // Handle end of response
      res.on('end', () => {
        if (res.statusCode >= 200 && res.statusCode < 300) {
          try {
            const parsedData = JSON.parse(data);
            resolve(parsedData);
          } catch (e) {
            reject(new Error(`Error parsing response: ${e.message}`));
          }
        } else {
          let errorMessage = `HTTP error! status: ${res.statusCode}`;
          try {
            const errorData = JSON.parse(data);
            errorMessage += `, message: ${errorData.message || JSON.stringify(errorData)}`;
          } catch (e) {
            errorMessage += `, body: ${data}`;
          }
          reject(new Error(errorMessage));
        }
      });
    });
    
    // Handle request errors
    req.on('error', (error) => {
      reject(error);
    });
    
    // Write data to request body
    req.write(requestData);
    req.end();
  });
}

// Run the main function
async function main() {
  try {
    console.log('Sending request to Cohere Rerank API...');
    console.log(`Query: "${query}"`);
    const data = await makeRequest();
    
    console.log('\nRanked Results:');
    if (data.results && Array.isArray(data.results)) {
      data.results.forEach((result, index) => {
        console.log(`\nRank ${index + 1}:`);
        console.log(`Document: ${documents[result.index || 0]}`);
        console.log(`Relevance Score: ${result.relevance_score || result.score || 'N/A'}`);
        console.log(`Index: ${result.index || 'N/A'}`);
      });
    } else {
      console.log('Unexpected response format:', JSON.stringify(data, null, 2));
    }
  } catch (error) {
    console.error('Error:', error.message);
    
    if (error.message.includes('401')) {
      console.error('\nTip: Your API key may be invalid or expired. Please check your API key.');
    }
  }
}

main();