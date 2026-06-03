const http = require('http');
const authHandler = require('./api/auth.js');

const server = http.createServer((req, res) => {
  // Add Express-like helpers to res for compatibility with Vercel's environment
  res.status = function(statusCode) {
    this.statusCode = statusCode;
    return this;
  };
  res.json = function(data) {
    this.setHeader('Content-Type', 'application/json');
    this.end(JSON.stringify(data));
    return this;
  };

  // Parse incoming JSON body for POST requests
  if (req.method === 'POST') {
    let body = '';
    req.on('data', chunk => {
      body += chunk.toString();
    });
    req.on('end', () => {
      try {
        req.body = JSON.parse(body);
      } catch (e) {
        req.body = {};
      }
      authHandler(req, res);
    });
  } else {
    req.body = {};
    authHandler(req, res);
  }
});

const PORT = process.env.PORT || 3000;
server.listen(PORT, () => {
  console.log(`\n====================================`);
  console.log(`Local authentication server is running!`);
  console.log(`URL: http://localhost:${PORT}`);
  console.log(`====================================`);
  console.log(`To test locally:`);
  console.log(`1. Change BackendUrl in AuthService.cs to:`);
  console.log(`   "http://localhost:${PORT}/api/auth"`);
  console.log(`2. Build and run your C# client application.`);
  console.log(`====================================\n`);
});
