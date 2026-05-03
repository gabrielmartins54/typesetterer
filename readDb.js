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

async function check() {
  const snapshot = await ref.once("value");
  const data = snapshot.val();
  console.log(JSON.stringify(data, null, 2));
  process.exit(0);
}

check();
