const admin = require("firebase-admin");
const serviceAccount = require("./typesetterer-fb516-firebase-adminsdk-fbsvc-8d4bfbbf10.json");

const projectId = serviceAccount.project_id;
const databaseURL = `https://${projectId}-default-rtdb.firebaseio.com`;

admin.initializeApp({
  credential: admin.credential.cert(serviceAccount),
  databaseURL: databaseURL
});

const db = admin.database();
const ref = db.ref("keys");

async function fix() {
  const snapshot = await ref.once("value");
  const data = snapshot.val();
  
  for (const key in data) {
    const item = data[key];
    if (item.Hwid !== undefined) {
      // Fix uppercase to lowercase
      await ref.child(key).update({
        hwid: item.Hwid,
        status: item.Status,
        Hwid: null, // delete old
        Status: null // delete old
      });
      console.log(`Fixed key ${key}`);
    }
  }
  console.log("Concluído!");
  process.exit(0);
}

fix();
