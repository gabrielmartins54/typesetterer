using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Typesetterer3;

public class AuthForm : Form
{
    private TextBox _keyBox;
    private Button _loginBtn;
    private Label _statusLabel;
    
    public bool IsAuthenticated { get; private set; } = false;

    public AuthForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "Typesetterer 3.0 - Ativação";
        this.Size = new Size(350, 180);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.MaximizeBox = false;
        this.MinimizeBox = false;

        var label = new Label { Text = "Insira sua chave de acesso:", Location = new Point(20, 20), AutoSize = true };
        
        _keyBox = new TextBox { Location = new Point(20, 45), Width = 290 };
        
        _loginBtn = new Button { Text = "Validar", Location = new Point(20, 75), Width = 290, Height = 30 };
        _loginBtn.Click += async (s, e) => await ValidateKey();

        _statusLabel = new Label { Text = "", Location = new Point(20, 110), Width = 290, ForeColor = Color.Red };

        this.Controls.Add(label);
        this.Controls.Add(_keyBox);
        this.Controls.Add(_loginBtn);
        this.Controls.Add(_statusLabel);
        
        this.AcceptButton = _loginBtn;
    }

    private async Task ValidateKey()
    {
        var key = _keyBox.Text.Trim();
        if (string.IsNullOrEmpty(key)) return;

        _loginBtn.Enabled = false;
        _keyBox.Enabled = false;
        _statusLabel.Text = "Validando...";
        _statusLabel.ForeColor = Color.Black;

        // Usando a API Key fornecida do Firebase
        var authService = new AuthService("https://typesetterer-fb516-default-rtdb.firebaseio.com/", "AIzaSyAKhMr5RDkj2drfQc7p2hsk43CGxEOGX48");
        var result = await authService.ValidateKeyAsync(key);

        switch (result)
        {
            case AuthResult.Success:
                IsAuthenticated = true;
                this.DialogResult = DialogResult.OK;
                this.Close();
                break;
            case AuthResult.InvalidKey:
                _statusLabel.Text = "Chave inválida.";
                _statusLabel.ForeColor = Color.Red;
                break;
            case AuthResult.InactiveKey:
                _statusLabel.Text = "Chave desativada/expirada.";
                _statusLabel.ForeColor = Color.Red;
                break;
            case AuthResult.HwidMismatch:
                _statusLabel.Text = "Chave em uso em outro computador.";
                _statusLabel.ForeColor = Color.Red;
                break;
            case AuthResult.Error:
                _statusLabel.Text = "Erro de conexão. Verifique a internet.";
                _statusLabel.ForeColor = Color.Red;
                break;
        }

        if (result != AuthResult.Success)
        {
            _loginBtn.Enabled = true;
            _keyBox.Enabled = true;
            _keyBox.Focus();
        }
    }
}
