const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

console.log('Generating 2048-bit RSA key pair...');

const { publicKey, privateKey } = crypto.generateKeyPairSync('rsa', {
  modulusLength: 2048,
  publicKeyEncoding: {
    type: 'spki',
    format: 'pem'
  },
  privateKeyEncoding: {
    type: 'pkcs8',
    format: 'pem'
  }
});

fs.writeFileSync(path.join(__dirname, 'private_key.pem'), privateKey);
fs.writeFileSync(path.join(__dirname, 'public_key.pem'), publicKey);

console.log('\nKeys generated successfully!');
console.log('====================================');
console.log('1. private_key.pem -> Store this securely on your server (e.g., in Vercel Environment Variables).');
console.log('2. public_key.pem  -> Embed this in your C# client application.');
console.log('====================================\n');
