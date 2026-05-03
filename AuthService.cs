using Firebase.Auth;
using Firebase.Database;
using Firebase.Database.Query;
using Newtonsoft.Json;
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

public class AuthService
{
    private readonly string _firebaseDbUrl;
    private readonly string _apiKey;

    public AuthService(string firebaseDbUrl, string apiKey)
    {
        _firebaseDbUrl = firebaseDbUrl;
        _apiKey = apiKey;
    }

    private async Task<Firebase.Database.FirebaseClient> GetClientAsync()
    {
        var config = new Firebase.Auth.FirebaseAuthConfig
        {
            ApiKey = _apiKey,
            AuthDomain = "typesetterer-fb516.firebaseapp.com"
        };
        var authClient = new Firebase.Auth.FirebaseAuthClient(config);
        var userCredential = await authClient.SignInAnonymouslyAsync();
        var token = await userCredential.User.GetIdTokenAsync();

        var options = new Firebase.Database.FirebaseOptions
        {
            AuthTokenAsyncFactory = () => Task.FromResult(token)
        };
        return new Firebase.Database.FirebaseClient(_firebaseDbUrl, options);
    }

    public async Task<AuthResult> CheckHwidAsync()
    {
        try
        {
            var firebaseClient = await GetClientAsync();
            var hwid = HwidGenerator.GetHwid();
            
            var existingKeys = await firebaseClient.Child("keys").OrderBy("hwid").EqualTo(hwid).OnceAsync<KeyData>();
            foreach (var item in existingKeys)
            {
                if (item.Object.Status == "active")
                    return AuthResult.Success;
            }
            return AuthResult.InvalidKey;
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
            var firebaseClient = await GetClientAsync();
            var hwid = HwidGenerator.GetHwid();
            var keyNode = firebaseClient.Child("keys").Child(accessKey);
            
            var keyData = await keyNode.OnceSingleAsync<KeyData>();

            if (keyData == null) return AuthResult.InvalidKey;
            if (keyData.Status != "active") return AuthResult.InactiveKey;

            // Primeiro uso: Vincula HWID
            if (string.IsNullOrEmpty(keyData.Hwid))
            {
                // Antes de vincular, checa se o HWID já existe em OUTRA chave
                var existingKeys = await firebaseClient.Child("keys").OrderBy("hwid").EqualTo(hwid).OnceAsync<KeyData>();
                if (existingKeys.Count > 0)
                {
                    return AuthResult.HwidMismatch; // Já tem chave com esse HWID
                }

                keyData.Hwid = hwid;
                await keyNode.PutAsync(keyData);
                return AuthResult.Success;
            }

            // Usos seguintes: Valida HWID
            if (keyData.Hwid == hwid) return AuthResult.Success;

            // Usado em outro PC
            return AuthResult.HwidMismatch;
        }
        catch
        {
            return AuthResult.Error;
        }
    }
}
