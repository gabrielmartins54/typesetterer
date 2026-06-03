using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Typesetterer3;

public class KeyData
{
    [JsonProperty("hwid")]
    public string Hwid { get; set; } = string.Empty;

    [JsonProperty("status")]
    public string Status { get; set; } = "active";
}

public enum AuthResult
{
    Success,
    InvalidKey,
    InactiveKey,
    HwidMismatch,
    Error
}

public class ServerScriptPayload
{
    [JsonProperty("payloadKey")]
    public string PayloadKey { get; set; } = string.Empty;

    [JsonProperty("scriptSha256")]
    public string ScriptSha256 { get; set; } = string.Empty;

    [JsonProperty("payloadBase64")]
    public string PayloadBase64 { get; set; } = string.Empty;
}

public class AuthPayload
{
    [JsonProperty("authResult")]
    public string AuthResult { get; set; } = string.Empty;

    [JsonProperty("hwid")]
    public string Hwid { get; set; } = string.Empty;

    [JsonProperty("timestamp")]
    public long Timestamp { get; set; }

    [JsonProperty("scriptPayload")]
    public ServerScriptPayload? ScriptPayload { get; set; }
}

public class AuthResponse
{
    [JsonProperty("payload")]
    public string Payload { get; set; } = string.Empty;

    [JsonProperty("signature")]
    public string Signature { get; set; } = string.Empty;
}

public class AuthService
{
    // Configure your production backend API URL here.
    // For local testing, you can use: "http://localhost:3000/api/auth"
    private const string BackendUrl = "http://localhost:3000/api/auth";

    private const string PublicKeyPem = @"-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA8SqyER4NfRGPehjBpnCF
I7yW2supVyLJSlA6oge8IUXQ0mOL1ayiJEbXT+gwmwUBjm4gUlVXS6tCgwkcY2rL
yVe0xy0VDxJYxmGC+VKQvgvkOJuA8AXCYcwLR7+uITVaqfVWqaUpfy/qgtA6fgh7
Wp7/cdHPam7xzX13eUMhiw34CxDmu5lIHBFkoWqLAhzNB13T+LHcccj+ipk7rcLa
miAydHovXb2HLuR8HfgP4jZkkQOQNeKiJrlB7EEIhOXwvLPqnoc5it0hiaYzvFlf
sNJqJTUyqTUbXINIW0ZuM2UBT4a2Dn3fHa80gDNcrQIxRtFlzMo7WGhbd3He9s7f
QQIDAQAB
-----END PUBLIC KEY-----";

    private static readonly HttpClient Client = new();

    public AuthService(string? firebaseDbUrl = null, string? apiKey = null)
    {
        // Ignored, using secure server backend now
    }

    public async Task<AuthResult> CheckHwidAsync()
    {
        try
        {
            var hwid = HwidGenerator.GetHwid();
            var requestBody = new { action = "check_hwid", hwid };
            var jsonContent = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            var response = await Client.PostAsync(BackendUrl, jsonContent);
            if (!response.IsSuccessStatusCode)
            {
                return AuthResult.Error;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var authResponse = JsonConvert.DeserializeObject<AuthResponse>(responseBody);
            if (authResponse == null) return AuthResult.Error;

            return ProcessAuthPayload(authResponse.Payload, authResponse.Signature, hwid);
        }
        catch
        {
            return AuthResult.Error;
        }
    }

    public async Task<AuthResult> ValidateKeyAsync(string accessKey)
    {
        try
        {
            var hwid = HwidGenerator.GetHwid();
            var requestBody = new { action = "validate_key", accessKey, hwid };
            var jsonContent = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            var response = await Client.PostAsync(BackendUrl, jsonContent);
            if (!response.IsSuccessStatusCode)
            {
                return AuthResult.Error;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var authResponse = JsonConvert.DeserializeObject<AuthResponse>(responseBody);
            if (authResponse == null) return AuthResult.Error;

            return ProcessAuthPayload(authResponse.Payload, authResponse.Signature, hwid);
        }
        catch
        {
            return AuthResult.Error;
        }
    }

    private AuthResult ProcessAuthPayload(string payloadBase64, string signatureBase64, string expectedHwid)
    {
        // 1. Cryptographically verify signature
        if (!VerifySignature(payloadBase64, signatureBase64))
        {
            return AuthResult.Error;
        }

        // 2. Decode the payload
        var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(payloadBase64));
        var payload = JsonConvert.DeserializeObject<AuthPayload>(payloadJson);
        if (payload == null) return AuthResult.Error;

        // 3. Verify hardware ID matches
        if (payload.Hwid != expectedHwid)
        {
            return AuthResult.HwidMismatch;
        }

        // 4. Verify timestamp to prevent replay attacks (allow 10-minute window)
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (Math.Abs(now - payload.Timestamp) > 600000)
        {
            return AuthResult.Error;
        }

        // Parse result
        var result = Enum.TryParse<AuthResult>(payload.AuthResult, out var authResult)
            ? authResult
            : AuthResult.Error;

        // 5. If successful, inject the server-delivered script payload
        if (result == AuthResult.Success && payload.ScriptPayload != null)
        {
            BuiltInScript.LoadedPayloadBase64 = payload.ScriptPayload.PayloadBase64;
            BuiltInScript.LoadedPayloadKey = payload.ScriptPayload.PayloadKey;
            BuiltInScript.LoadedScriptSha256 = payload.ScriptPayload.ScriptSha256;
        }

        return result;
    }

    private static bool VerifySignature(string payloadBase64, string signatureBase64)
    {
        try
        {
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payloadBase64);
            byte[] signatureBytes = Convert.FromBase64String(signatureBase64);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(PublicKeyPem);

            return rsa.VerifyData(payloadBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }
}
