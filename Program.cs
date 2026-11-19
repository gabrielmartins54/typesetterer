using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Velopack;
using Velopack.Sources;

namespace Typesetterer3;

internal static class Program
{
    private const string MutexName = "Global\\Typesetterer3.SingleInstance";
    private const string GithubUrl = "https://github.com/gabrielmartins54/typesetterer";

    [STAThread]
    private static void Main(string[] args)
    {
        // Velopack startup handler (handles installation, shortcuts, and updates)
        VelopackApp.Build().Run();

        using var mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Typesetterer 3.0 is already running.", "Typesetterer 3.0", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();

        // Register COM message filter early so all Photoshop COM interactions
        // (GetActiveObject, DoJavaScript, etc.) can negotiate retries when PS is busy.
        ComMessageFilter.Register();
        
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