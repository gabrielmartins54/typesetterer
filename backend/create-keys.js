const crypto = require('crypto');

const FIREBASE_DB_URL = process.env.FIREBASE_DB_URL || "https://typesetterer-fb516-default-rtdb.firebaseio.com";
const FIREBASE_API_KEY = process.env.FIREBASE_API_KEY || "AIzaSyAKhMr5RDkj2drfQc7p2hsk43CGxEOGX48";

// Generates a random alphanumeric key: e.g. TR3-XXXX-XXXX-XXXX-XXXX
function generateProductKey() {
  const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789';
  const segment = () => Array.from({ length: 4 }, () => chars[Math.floor(Math.random() * chars.length)]).join('');
  return `TR3-${segment()}-${segment()}-${segment()}-${segment()}`;
}

async function getFirebaseToken() {
  const url = `https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=${FIREBASE_API_KEY}`;
  const res = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ returnSecureToken: true })
  });
  if (!res.ok) {
    const errText = await res.text();
    throw new Error(`Firebase Auth failed: ${errText}`);
  }
  const data = await res.json();
  return data.idToken;
}

async function main() {
  console.log('Connecting to Firebase Database...');
  try {
    const idToken = await getFirebaseToken();
    console.log('Generating 10 product keys...');
    
    const keys = [];
    for (let i = 0; i < 10; i++) {
      keys.push(generateProductKey());
    }

    console.log('Writing keys to Firebase...');
    for (const key of keys) {
      const keyUrl = `${FIREBASE_DB_URL}/keys/${key}.json?auth=${idToken}`;
      const payload = {
        hwid: "",
        status: "active"
      };

      const res = await fetch(keyUrl, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (!res.ok) {
        throw new Error(`Failed to write key ${key}: ${res.statusText}`);
      }
    }

    console.log('\n====================================');
    console.log('Successfully created 10 new product keys:');
    console.log('====================================');
    keys.forEach((key, index) => {
      console.log(`${index + 1}. ${key}`);
    });
    console.log('====================================\n');

  } catch (error) {
    console.error('Error generating product keys:', error);
  }
}

main();
