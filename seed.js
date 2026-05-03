const admin = require("firebase-admin");
const serviceAccount = require("./typesetterer-fb516-firebase-adminsdk-fbsvc-8d4bfbbf10.json");

// Construct DB URL from project ID
const projectId = serviceAccount.project_id;
const databaseURL = `https://${projectId}-default-rtdb.firebaseio.com`;

admin.initializeApp({
  credential: admin.credential.cert(serviceAccount),
  databaseURL: databaseURL
});

const db = admin.database();
const ref = db.ref("keys");

function generateCode() {
  const chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
  let result = "";
  for(let i=0; i<12; i++) {
    result += chars.charAt(Math.floor(Math.random() * chars.length));
    if(i === 3 || i === 7) result += "-";
  }
  return result;
}

async function seed() {
  console.log("Gerando 10 chaves...");
  for(let i=0; i<10; i++) {
    const code = generateCode();
    await ref.child(code).set({
      hwid: "",
      status: "active"
    });
    console.log(`Chave gerada: ${code}`);
  }
  console.log("Concluído!");
  process.exit(0);
}

seed();
