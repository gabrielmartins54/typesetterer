# Typesetterer 3.0 Authentication & Licensing Backend

This is the secure backend system for Typesetterer 3.0. It acts as an API proxy between your desktop client and the Firebase Realtime Database. It prevents reverse-engineers from finding your database secrets and signs all licensing requests cryptographically to block local cracking attempts.

---

## 1. Local Testing & Key Generation

The project works out-of-the-box using vanilla Node.js features (Node 18+ required).

### Generate Product Keys (10 at a time)
To generate 10 new product keys and write them to your database, run:
```bash
node create-keys.js
```
The script will output the new keys to your console (e.g. `TR3-XXXX-XXXX-XXXX-XXXX`).

### Re-generate RSA Security Keys
To generate a new pair of RSA private/public keys, run:
```bash
node generate-keys.js
```
* **`private_key.pem`**: Upload the contents of this file to your server environment variables as `RSA_PRIVATE_KEY`.
* **`public_key.pem`**: Embed this string in `AuthService.cs` inside your client application.

---

## 2. Deploying to Vercel (Free Hosting)

Vercel provides a free serverless function hosting tier that auto-scales and supports HTTPS out-of-the-box.

### Option A: Via GitHub (Recommended)
1. Push this `backend` directory (or the whole project) to your GitHub repository.
2. Log in to [Vercel](https://vercel.com/) and click **Add New Project**.
3. Import your GitHub repository.
4. If your backend is in a subdirectory, set the **Root Directory** option in Vercel to `backend`.
5. Under **Environment Variables**, add:
   * `FIREBASE_DB_URL`: `https://typesetterer-fb516-default-rtdb.firebaseio.com`
   * `FIREBASE_API_KEY`: `AIzaSyAKhMr5RDkj2drfQc7p2hsk43CGxEOGX48`
   * `RSA_PRIVATE_KEY`: *(The contents of your `private_key.pem`)*
6. Click **Deploy**. Vercel will give you a domain name like `https://your-project.vercel.app`.

### Option B: Via Vercel CLI (Command Line)
1. Open your terminal in the `backend` directory.
2. Run `npm i -g vercel` to install the Vercel CLI.
3. Run `vercel login` and follow the prompts.
4. Run `vercel` to deploy to a preview domain.
5. Set your environment variables in the Vercel Dashboard project settings.
6. Run `vercel --prod` to deploy to production.

---

## 3. Connecting the C# Client

Once your backend is deployed:
1. Open `AuthService.cs` in the C# project.
2. Locate the `BackendUrl` constant:
   ```csharp
   private const string BackendUrl = "https://your-project.vercel.app/api/auth";
   ```
3. Replace the placeholder URL with your real Vercel URL.
4. Compile the client. It will now securely authenticate through your Vercel backend!
