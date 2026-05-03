using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Typesetterer3;

internal static class Program
{
    private const string MutexName = "Global\\Typesetterer3.SingleInstance";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Typesetterer 3.0 is already running.", "Typesetterer 3.0", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        
        // Verifica se o PC já tem uma chave vinculada e ativa
        var authService = new AuthService("https://typesetterer-fb516-default-rtdb.firebaseio.com/", "AIzaSyAKhMr5RDkj2drfQc7p2hsk43CGxEOGX48");
        var hwidResult = authService.CheckHwidAsync().GetAwaiter().GetResult();

        if (hwidResult == AuthResult.Success)
        {
            Application.Run(new MainForm());
        }
        else
        {
            using var authForm = new AuthForm();
            if (authForm.ShowDialog() == DialogResult.OK)
            {
                Application.Run(new MainForm());
            }
        }
    }
}
