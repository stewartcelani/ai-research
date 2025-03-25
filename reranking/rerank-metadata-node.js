// Cohere Rerank API Example - using Node.js https
// This example demonstrates using the Rerank API with structured data and threshold filtering

const https = require('https');
const fs = require('fs');
const path = require('path');

// Load API key from API_KEYS.json
const apiKeysPath = path.join(__dirname, '..', 'API_KEYS.json');
const apiKeys = JSON.parse(fs.readFileSync(apiKeysPath, 'utf8'));
const API_KEY = apiKeys.cohere.api_key;

// Example query about home repairs
const query = 'i have a leaky tap';

// Example structured data as YAML-like strings
const documents = [
  `Title: How to fix a leaky faucet
Author: Jane Doe
Date: March 15, 2023
Content: To fix a leaky faucet, first turn off the water supply. Then, disassemble the faucet handle by removing the decorative cap, unscrewing the handle, and removing the cartridge. Check for worn O-rings or damaged washers and replace them. Reassemble the faucet and turn the water back on to test.`,
  
  `Title: Common bathroom plumbing issues
Author: Bob Smith
Date: January 10, 2023
Content: Bathroom plumbing issues include clogged drains, running toilets, and low water pressure. For drains, try using a plunger or drain snake. For toilets, check the flapper valve. For low pressure, inspect the aerator for mineral buildup.`,
  
  `Title: How to repair a dishwasher
Author: Alice Johnson
Date: April 20, 2023
Content: If your dishwasher isn't cleaning properly, check the spray arms for clogs, inspect the filter, and ensure the water supply is adequate. For leaks, examine the door gasket for damage or the water inlet valve for failure.`,
  
  `Title: Fixing sink drainage problems
Author: Michael Wilson
Date: February 5, 2023
Content: Slow draining sinks are often caused by debris buildup. Start by using a plunger, then try a mixture of baking soda and vinegar. For persistent clogs, disassemble the P-trap under the sink to remove any obstructions.`
];

// Sample relevance threshold (typically determined through testing)
const RELEVANCE_THRESHOLD = 0.5;

// Function to make rerank request using Node.js https
function makeRerankRequest() {
  return new Promise((resolve, reject) => {
    // Prepare the request body
    const requestData = JSON.stringify({
      model: 'rerank-v3.5',
      query,
      documents,
      top_n: documents.length, // Return all results
      max_tokens_per_doc: 2048 // Limit tokens per document
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

// Run the example
async function runExample() {
  console.log('Sending request to Cohere Rerank API...');
  console.log(`Query: "${query}"`);
  console.log('Using relevance threshold:', RELEVANCE_THRESHOLD);
  
  try {
    const data = await makeRerankRequest();
    
    console.log('\nRanked Results:');
    
    if (data.results && Array.isArray(data.results)) {
      // Display all results with their scores
      data.results.forEach((result, index) => {
        const document = documents[result.index];
        const documentTitle = document.split('\n')[0].replace('Title: ', '');
        
        console.log(`\nRank ${index + 1}:`);
        console.log(`Document: ${documentTitle}`);
        console.log(`Relevance Score: ${result.relevance_score.toFixed(4)}`);
        console.log(`Index: ${result.index}`);
      });
      
      // Filter documents by relevance threshold
      const relevantDocuments = data.results.filter(
        result => result.relevance_score >= RELEVANCE_THRESHOLD
      );
      
      console.log('\n--- Filtered Results (Above Threshold) ---');
      if (relevantDocuments.length > 0) {
        relevantDocuments.forEach((result, index) => {
          const document = documents[result.index];
          const documentTitle = document.split('\n')[0].replace('Title: ', '');
          
          console.log(`\nRelevant Document ${index + 1}:`);
          console.log(`Title: ${documentTitle}`);
          console.log(`Relevance Score: ${result.relevance_score.toFixed(4)}`);
        });
      } else {
        console.log('No documents found above the relevance threshold.');
      }
      
      // Extract specific fields from the structured data for the most relevant document
      if (data.results.length > 0) {
        const topResult = data.results[0];
        const topDocument = documents[topResult.index];
        const documentLines = topDocument.split('\n');
        
        console.log('\n--- Most Relevant Document Details ---');
        documentLines.forEach(line => {
          console.log(line);
        });
      }
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

// Run the example when the script is executed
runExample();