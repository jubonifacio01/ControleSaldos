using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ControleSaldosCIS;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public sealed class MainForm : Form
{
    private readonly TextBox txtAtende = CreateMoneyBox();
    private readonly TextBox txtCaixa = CreateMoneyBox();
    private readonly TextBox txtCofre = CreateMoneyBox();

    private readonly ComboBox cmbPorta = new();
    private readonly ComboBox cmbVelocidade = new();
    private readonly Label lblTotal = new();
    private readonly Label lblStatus = new();

    private const string DefaultBaud = "9600";

    public MainForm()
    {
        Text = "Controle de Saldos";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(680, 760);
        ClientSize = new Size(680, 760);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(245, 245, 245);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        BuildInterface();
        AtualizarTotal();
        AtualizarPortas();
    }

    private void BuildInterface()
    {
        var title = new Label
        {
            Text = "CONTROLE DE SALDOS",
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds = new Rectangle(30, 25, 620, 45)
        };
        Controls.Add(title);

        var subtitle = new Label
        {
            Text = "Informe os três saldos para impressão",
            Font = new Font("Segoe UI", 10.5F),
            ForeColor = Color.DimGray,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Bounds = new Rectangle(30, 70, 620, 30)
        };
        Controls.Add(subtitle);

        AddSaldoRow("CORREIOS ATENDE", txtAtende, 125);
        AddSaldoRow("CAIXA", txtCaixa, 240);
        AddSaldoRow("COFRE", txtCofre, 355);

        var totalPanel = new Panel
        {
            Bounds = new Rectangle(30, 470, 620, 78),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White
        };
        Controls.Add(totalPanel);

        lblTotal.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
        lblTotal.Dock = DockStyle.Fill;
        lblTotal.TextAlign = ContentAlignment.MiddleCenter;
        totalPanel.Controls.Add(lblTotal);

        var printerLabel = new Label
        {
            Text = "Porta da CIS:",
            Bounds = new Rectangle(30, 575, 115, 32),
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(printerLabel);

        cmbPorta.Bounds = new Rectangle(145, 572, 230, 36);
        cmbPorta.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbPorta.Font = new Font("Segoe UI", 11F);
        Controls.Add(cmbPorta);

        var refresh = new Button
        {
            Text = "ATUALIZAR",
            Bounds = new Rectangle(385, 572, 125, 36),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
        };
        refresh.Click += (_, _) => AtualizarPortas();
        Controls.Add(refresh);

        var baudLabel = new Label
        {
            Text = "Velocidade:",
            Bounds = new Rectangle(30, 620, 115, 32),
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(baudLabel);

        cmbVelocidade.Bounds = new Rectangle(145, 617, 230, 36);
        cmbVelocidade.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbVelocidade.Font = new Font("Segoe UI", 11F);
        cmbVelocidade.Items.AddRange(new object[] { "9600", "19200", "38400", "57600", "115200" });
        cmbVelocidade.SelectedItem = DefaultBaud;
        Controls.Add(cmbVelocidade);

        var print = new Button
        {
            Text = "IMPRIMIR",
            Bounds = new Rectangle(30, 675, 300, 55),
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            BackColor = Color.White
        };
        print.Click += (_, _) => ImprimirCIS();
        Controls.Add(print);

        var clear = new Button
        {
            Text = "LIMPAR",
            Bounds = new Rectangle(350, 675, 300, 55),
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            BackColor = Color.White
        };
        clear.Click += (_, _) =>
        {
            txtAtende.Clear();
            txtCaixa.Clear();
            txtCofre.Clear();
            AtualizarTotal();
            txtAtende.Focus();
        };
        Controls.Add(clear);

        lblStatus.Text = "Pronto.";
        lblStatus.Font = new Font("Segoe UI", 9F);
        lblStatus.ForeColor = Color.DimGray;
        lblStatus.AutoSize = false;
        lblStatus.TextAlign = ContentAlignment.MiddleCenter;
        lblStatus.Bounds = new Rectangle(30, 735, 620, 22);
        Controls.Add(lblStatus);

        txtAtende.TextChanged += (_, _) => AtualizarTotal();
        txtCaixa.TextChanged += (_, _) => AtualizarTotal();
        txtCofre.TextChanged += (_, _) => AtualizarTotal();

        txtAtende.KeyDown += MoneyKeyDown;
        txtCaixa.KeyDown += MoneyKeyDown;
        txtCofre.KeyDown += MoneyKeyDown;

        AcceptButton = print;
    }

    private void AddSaldoRow(string caption, TextBox box, int top)
    {
        var label = new Label
        {
            Text = caption,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            Bounds = new Rectangle(30, top + 18, 210, 45),
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(label);

        box.Bounds = new Rectangle(240, top, 410, 82);
        box.Font = new Font("Segoe UI", 26F, FontStyle.Bold);
        box.TextAlign = HorizontalAlignment.Right;
        box.Margin = new Padding(0);
        box.BorderStyle = BorderStyle.FixedSingle;
        Controls.Add(box);
    }

    private static TextBox CreateMoneyBox()
    {
        return new TextBox
        {
            PlaceholderText = "0,00",
            MaxLength = 15
        };
    }

    private void MoneyKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            SelectNextControl((Control)sender!, true, true, true, true);
        }
    }

    private void AtualizarTotal()
    {
        decimal total = LerValor(txtAtende) + LerValor(txtCaixa) + LerValor(txtCofre);
        lblTotal.Text = $"TOTAL   R$ {total:N2}";
    }

    private static decimal LerValor(TextBox box)
    {
        var text = (box.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(text))
            return 0m;

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out var br))
            return br;

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var inv))
            return inv;

        return 0m;
    }

    private void AtualizarPortas()
    {
        string atual = cmbPorta.SelectedItem?.ToString() ?? "";
        var portas = SerialPort.GetPortNames()
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        cmbPorta.Items.Clear();
        foreach (var porta in portas)
            cmbPorta.Items.Add(porta);

        if (portas.Contains(atual, StringComparer.OrdinalIgnoreCase))
            cmbPorta.SelectedItem = atual;
        else if (portas.Contains("COM2", StringComparer.OrdinalIgnoreCase))
            cmbPorta.SelectedItem = "COM2";
        else if (portas.Length > 0)
            cmbPorta.SelectedIndex = 0;

        if (portas.Length == 0)
            lblStatus.Text = "Nenhuma porta COM encontrada.";
        else
            lblStatus.Text = $"{portas.Length} porta(s) COM encontrada(s). A CIS foi identificada como COM2 no seu computador.";
    }

    private void ImprimirCIS()
    {
        if (cmbPorta.SelectedItem is null)
        {
            MessageBox.Show(
                "Nenhuma porta COM foi selecionada.\n\nA CIS precisa aparecer no Gerenciador de Dispositivos em Portas (COM e LPT).",
                "CIS não encontrada",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        decimal atende = LerValor(txtAtende);
        decimal caixa = LerValor(txtCaixa);
        decimal cofre = LerValor(txtCofre);
        decimal total = atende + caixa + cofre;

        int baud = int.Parse(cmbVelocidade.SelectedItem?.ToString() ?? DefaultBaud);
        string porta = cmbPorta.SelectedItem.ToString()!;

        try
        {
            byte[] dados = MontarCupom(atende, caixa, cofre, total);

            using var serial = new SerialPort(porta, baud, Parity.None, 8, StopBits.One)
            {
                Handshake = Handshake.None,
                DtrEnable = false,
                RtsEnable = false,
                WriteTimeout = 5000,
                ReadTimeout = 5000
            };

            serial.Open();
            serial.Write(dados, 0, dados.Length);
            serial.Write(new byte[] { 0x1D, 0x56, 0x00 }, 0, 3); // corte, se suportado

            lblStatus.Text = $"Impressão enviada para {porta} a {baud} baud.";
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show(
                $"A porta {porta} está ocupada por outro programa.\n\nFeche o programa que estiver usando a CIS e tente novamente.",
                "Porta ocupada",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Não foi possível imprimir na {porta}.\n\nDetalhes: {ex.Message}\n\nSe a porta estiver correta, experimente outra velocidade (baud rate).",
                "Erro de impressão",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static byte[] MontarCupom(decimal atende, decimal caixa, decimal cofre, decimal total)
    {
        using var ms = new MemoryStream();

        void W(params byte[] b) => ms.Write(b, 0, b.Length);
        void T(string s)
        {
            byte[] bytes = Encoding.GetEncoding(1252).GetBytes(s);
            ms.Write(bytes, 0, bytes.Length);
        }

        W(0x1B, 0x40);             // inicializa
        W(0x1B, 0x61, 0x01);       // centraliza
        W(0x1B, 0x45, 0x01);       // negrito
        W(0x1D, 0x21, 0x11);       // largura/altura 2x
        T("CONTROLE DE SALDOS");
        W(0x0A);

        W(0x1D, 0x21, 0x00);
        T(DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
        W(0x0A, 0x0A);

        W(0x1B, 0x61, 0x00);       // esquerda
        W(0x1B, 0x45, 0x00);
        T($"CORREIOS ATENDE: R$ {atende:N2}\r\n");
        T($"CAIXA:           R$ {caixa:N2}\r\n");
        T($"COFRE:           R$ {cofre:N2}\r\n");
        W(0x0A);

        W(0x1B, 0x45, 0x01);
        W(0x1D, 0x21, 0x11);
        T($"TOTAL: R$ {total:N2}");
        W(0x0A, 0x0A, 0x0A, 0x0A);

        W(0x1D, 0x21, 0x00);
        W(0x1B, 0x45, 0x00);
        W(0x1B, 0x61, 0x01);
        T("Fim\r\n\r\n");

        return ms.ToArray();
    }
}
