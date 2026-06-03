const crypto = require('crypto');

// Default Fallbacks for easy out-of-the-box local testing/deployment.
// The user can override these via Vercel Environment Variables.
const FIREBASE_DB_URL = process.env.FIREBASE_DB_URL || "https://typesetterer-fb516-default-rtdb.firebaseio.com";
const FIREBASE_API_KEY = process.env.FIREBASE_API_KEY || "AIzaSyAKhMr5RDkj2drfQc7p2hsk43CGxEOGX48";

const DEFAULT_PRIVATE_KEY = `-----BEGIN PRIVATE KEY-----
MIIEuwIBADANBgkqhkiG9w0BAQEFAASCBKUwggShAgEAAoIBAQDxKrIRHg19EY96
GMGmcIUjvJbay6lXIslKUDqiB7whRdDSY4vVrKIkRtdP6DCbBQGObiBSVVdLq0KD
CRxjasvJV7THLRUPEljGYYL5UpC+C+Q4m4DwBcJhzAtHv64hNVqp9VappSl/L+qC
0Dp+CHtanv9x0c9qbvHNfXd5QyGLDfgLEOa7mUgcEWShaosCHM0HXdP4sdxxyP6K
mTutwtqaIDJ0ei9dvYcu5Hwd+A/iNmSRA5A14qImuUHsQQiE5fC8s+qehzmK3SGJ
pjO8WV+w0molNTKpNRtcg0hbRm4zZQFPhrYOfd8drzSAM1ytAjFG0WXMyjtYaFt3
cd72zt9BAgMBAAECgf9EsXhuo4wnZeEZoywCUHPYo7w7PBsCMxlX7BJU4XKCxggl
jbR02dBQaSxveHC6y2o+6qBEWiuBRFZ54MWkBr96S5GbgBc9Z5EQKgSkGeoz+z10
Z2Dvuf5YBImzsmKP5Kj53cKZoYJP/mql/GWzkI9ZE6KimkiiWbAUvMRuIU7rYBf+
u7NoIjF5Pa2LeKMkJNnn+xXxPE2ab277p5WxgzhcJ65fIjnv4fzKcdtyerSdIzAz
Wl+WHzvnvBQ3mmUkTC/V70qzxM7Ul+gR4ZZ+NKuJXTzr4CCxTbP2c5Uqi8BtFLGh
9+MTOTRLj0q0nxF4UkfA/NbIKp+1r93cz4WhHSECgYEA+IzABEBbigwfZyB8fd1d
9clMcwQQ6U4s17Inq40gk9LwNCKgpjbEY5GqcTsYKSd3y3G51Qzx/xtnRSMv4vQg
zGw38QlqCbH9fxGGWC3oCDpldoHfT5Vq3jNNtMBDPWL988wVgp2kGKsI/E6guqSz
+7f9CHZxRZV2CQHsuqznt+ECgYEA+GVKsbqj74R/eyV8RCt3fg6QcKX82dA4Cr8H
HrB/YVqUYk4Wm0WNNm3nBtBNGq0vAD2r2j3UeKiEj8UG2A8Lq1I5BRg/WhnbTDrX
0Df7fqxkclr2I8ljWdegNKaraSFST+aSCf88eA4j5t+lnan4/gRgOCbnkU2b091N
JQHsk2ECgYAF56okL7c9B16zn4T1pI236iljziPstwu/sB7bJ4YvnOJSCsSQTlrK
Fsra19GSILrq3W/RzPXu3qLMdLKNjMk+sg9+LYAUbOMd4rezXpvVnjWM5Db++AjK
8KMW+T5ftftckGuuZ7pfT6jt/Zw58vHHuC5NxiqYwZ1FR4NtR/BVYQKBgCIed7YV
9vKtdn1J1tneRpmQEpMGmcapbSk8uBOlFroeoQcnxr8pPI5fZj5vWkfxxeT4k6R0
nby9GQhOffQrjOTJdH+YpwsPRiogYwh8yBTMRTfX+rtLrFPo5F6XDjBwnUfQWfr5
jirHdnL+AVe4wzTREQ5leaVhzae6XCBbNU3BAoGBAPRwsGHc8dc7FaGVMW91yQpO
oUTZGZ/63RsViXhHdfs7WV29e9ytohGyE+6GgK6Llpv9r8XxC86vvyhG9Iyx3wUw
jQWD9Afo1RjE5mQtTE56y9fxW0tIsBKD5D8NpP6UN249qmSWQ2rv6ab8H3vKPlrr
r7XDd/MEvH5J/NT3MYPp
-----END PRIVATE KEY-----`;

const PRIVATE_KEY = process.env.RSA_PRIVATE_KEY 
  ? process.env.RSA_PRIVATE_KEY.replace(/\\n/g, '\n')
  : DEFAULT_PRIVATE_KEY;

// Decrypted Photoshop execution scripts (kept secure on server)
const SCRIPT_PAYLOAD = {
  payloadKey: "Typesetterer3.BuiltInScript.v1",
  scriptSha256: "08E66099A75A7AAAD812476B0FC6119C690A3A7A5BCA9EF10177983B3CD0E7B8",
  payloadBase64: "S/J4ZXNldHRhcogkYkGZQ3kSe6mRI5B0LR5I9ziGalWDZVRrYgMMEEEcIw6PdTBWvyMHAUNbvh6j0cgSEVDmgAsg9RVKQQlq11M1gpiC5zHzRO7xfkCwflrj0Bh0emBY86MP97dq3X7oMZkz1K7RoRgUjoWZ05/ZXveNS95f+fejs0mRkghSWevoD2SWj1sqb2JwpUSvPJEz9dUbXgXjYBz6+blbNpD6GKPKIpPO+EyZkUkvieYvu+2BFVd06PXmQqcBZA9nkJP3USVk24vN20Rp910jV3BKFMqj53sbb9ZdZ3etAzT2g6UW9NJLkGhsGV+XkmCHerANad55eV/9kTDLYgggAPKDIUlaHVfW4JQJAQ1dCvB6f4vTyPbAe3XblIFDzLO5e43GboGbvOcxNu0A1uZMMae9MYjSQ3xeePwJIdUfxaZCfL09CVfldkUQvonNBzCc8gyhr7s9UTKbF9ylXPzo3xNYmKCGG0XcN/JkRSDBDpYPdoPAE0W7/8KSMyLHkt1dZTyZvLfITnoyG6XRCQQiPKUbsMS+BqeniIkCIBg366Z8o2TL0Q+gmmHvRJBWyKIo8Rb4gcSOCP9IFgi11fx9DDRD3WK3AFdSeX2HqX0VWY32rlehClkmugzc0Yow6/V2Ffzq9DuBX+Abxrxq0SyssC45MPgScD3ykv0KL8N4/xOptFc7OFCEmBGzVWWul1Z3c/r4uiA6KFhPFGAdD4ueg69Ur1Ev1VLFtzTvxfbhNz+rtdBd115w1Vit7JALhEjjeY5TonH/kiP/y9eVE4UnKwgb0CgOaI0jsuXk6aKCmDJEKWZaaTnrMsVDhjN5gyZ2UYYufvUstDCZNLWgjJQsGGC6SX2R1TW9UM36S9YA/ijMObj40cvzoyvzf0ruyjDS0YJwEQxn5gdRMHpeoZe3718CAK21/x3D9oKqF7SrYjJMnWHMZeR9RSnoq6WhZPicqvqbrd8dWPOfCrbWuc4fpCYrVetvXfoq99MDqt0kkx2irxOPDfanat2eqd391EzcrALq5Ku7OW4BjoGJS0ddl0Q7Vdh2fC4fEd/QtcVVq7ojBz2FI4E+e2VnwVGl2hULSxLDlyrpZltS0Cm6vU3VkSkabQX2WaUWRmh450FqalyL4tHrkPjw3VN0gzs4N2Pvv4WJK26YMct6SBRpPadjAVMc90LfF0PF4WR2Gv7hD0A4/SpNr0RSnd1Bo4SWqbK0BXTBhuGgPoyqJWyu37MMTM3T3aCcupDOCxy+czRb258b8lHnS/9aPDOe1syhQRpc43UNc8Kr56LH0gJ0OOriZriiHbMP/ZDPT/OeKOFweHRl"
};

// Helper to obtain an anonymous sign-in token from Firebase Auth
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

// Serverless Handler
module.exports = async (req, res) => {
  // CORS Headers
  res.setHeader('Access-Control-Allow-Origin', '*');
  res.setHeader('Access-Control-Allow-Methods', 'POST, OPTIONS');
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type');

  if (req.method === 'OPTIONS') {
    return res.status(200).end();
  }

  if (req.method !== 'POST') {
    return res.status(405).json({ error: 'Method Not Allowed' });
  }

  try {
    const { action, hwid, accessKey } = req.body || {};

    if (!hwid) {
      return res.status(400).json({ error: 'Missing hardware ID (hwid)' });
    }

    const idToken = await getFirebaseToken();

    if (action === 'check_hwid') {
      const dbUrl = `${FIREBASE_DB_URL}/keys.json?orderBy="hwid"&equalTo="${hwid}"&auth=${idToken}`;
      const dbRes = await fetch(dbUrl);
      if (!dbRes.ok) throw new Error('Database query failed');
      
      const keys = await dbRes.json();
      let matchedActiveKey = false;
      if (keys && typeof keys === 'object') {
        for (const k in keys) {
          if (keys[k].status === 'active') {
            matchedActiveKey = true;
            break;
          }
        }
      }

      if (matchedActiveKey) {
        return sendSignedResponse(res, {
          authResult: 'Success',
          hwid,
          scriptPayload: SCRIPT_PAYLOAD
        });
      } else {
        return sendSignedResponse(res, {
          authResult: 'InvalidKey',
          hwid
        });
      }

    } else if (action === 'validate_key') {
      if (!accessKey) {
        return res.status(400).json({ error: 'Missing accessKey' });
      }

      const keyUrl = `${FIREBASE_DB_URL}/keys/${accessKey}.json?auth=${idToken}`;
      const dbRes = await fetch(keyUrl);
      if (!dbRes.ok) throw new Error('Database query failed');
      
      const keyData = await dbRes.json();
      if (!keyData) {
        return sendSignedResponse(res, { authResult: 'InvalidKey', hwid });
      }
      if (keyData.status !== 'active') {
        return sendSignedResponse(res, { authResult: 'InactiveKey', hwid });
      }

      // First use: link HWID
      if (!keyData.hwid) {
        // Verify HWID is not already linked to another key
        const hwidCheckUrl = `${FIREBASE_DB_URL}/keys.json?orderBy="hwid"&equalTo="${hwid}"&auth=${idToken}`;
        const checkRes = await fetch(hwidCheckUrl);
        const checkKeys = await checkRes.json();
        
        if (checkKeys && Object.keys(checkKeys).length > 0) {
          return sendSignedResponse(res, { authResult: 'HwidMismatch', hwid });
        }

        // Link HWID to this key
        keyData.hwid = hwid;
        const updateUrl = `${FIREBASE_DB_URL}/keys/${accessKey}.json?auth=${idToken}`;
        await fetch(updateUrl, {
          method: 'PATCH',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ hwid })
        });

        return sendSignedResponse(res, {
          authResult: 'Success',
          hwid,
          scriptPayload: SCRIPT_PAYLOAD
        });
      }

      // Subsequent uses: validate HWID
      if (keyData.hwid === hwid) {
        return sendSignedResponse(res, {
          authResult: 'Success',
          hwid,
          scriptPayload: SCRIPT_PAYLOAD
        });
      }

      return sendSignedResponse(res, { authResult: 'HwidMismatch', hwid });

    } else {
      return res.status(400).json({ error: 'Invalid action' });
    }

  } catch (error) {
    console.error('Auth handler error:', error);
    return res.status(500).json({ error: 'Internal Server Error', details: error.message });
  }
};

// Cryptographically sign response payload using RSA private key
function sendSignedResponse(res, data) {
  const payloadObj = {
    ...data,
    timestamp: Date.now()
  };

  const payloadString = JSON.stringify(payloadObj);
  const payloadBase64 = Buffer.from(payloadString).toString('base64');

  const sign = crypto.createSign('SHA256');
  sign.update(payloadBase64);
  sign.end();
  const signature = sign.sign(PRIVATE_KEY, 'base64');

  return res.status(200).json({
    payload: payloadBase64,
    signature: signature
  });
}
