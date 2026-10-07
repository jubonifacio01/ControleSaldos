using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.Json;
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
    readonly TextBox atende = MoneyBox(), caixa = MoneyBox(), cofre = MoneyBox();
    readonly ComboBox porta = new(), baud = new();
    readonly Label total = new(), status = new();
    readonly TabControl tabs = new();

    const string DefaultBaud = "9600";

    public MainForm()
    {
        Text = "Controle de Saldos CIS";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1120, 780);
        MinimumSize = new Size(980, 700);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(245, 245, 245);

        Build();
        RefreshPorts();
        UpdateTotal();
    }

    void Build()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.White
        };
        header.Controls.Add(new Label
        {
            Text = "CONTROLE DE SALDOS CIS",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 21F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        });
        Controls.Add(header);

        tabs.Dock = DockStyle.Fill;
        tabs.Padding = new Point(12, 8);
        Controls.Add(tabs);

        var p1 = new TabPage("Controle de Saldos") { BackColor = BackColor, Padding = new Padding(18) };
        var p2 = new TabPage("Impressão Personalizada") { BackColor = BackColor, Padding = new Padding(18) };
        var p3 = new TabPage("Rótulos de Moedas") { BackColor = BackColor, Padding = new Padding(18) };

        BuildSaldos(p1);
        p2.Controls.Add(new TextPrinterEditor(Send));
        p3.Controls.Add(new CoinPrinterEditor(Send));

        tabs.TabPages.AddRange(new[] { p1, p2, p3 });
    }

    void BuildSaldos(Control page)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12),
            BackColor = Color.White
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "Informe os três saldos para impressão",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11F),
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        AddBalanceRow(root, "CORREIOS ATENDE", atende, 1);
        AddBalanceRow(root, "CAIXA", caixa, 2);
        AddBalanceRow(root, "COFRE", cofre, 3);

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(0, 16, 0, 0)
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

        total.Dock = DockStyle.Fill;
        total.Font = new Font("Segoe UI", 19F, FontStyle.Bold);
        total.TextAlign = ContentAlignment.MiddleCenter;
        total.BackColor = Color.FromArgb(248, 248, 248);
        total.BorderStyle = BorderStyle.FixedSingle;
        bottom.Controls.Add(total, 0, 0);
        bottom.SetColumnSpan(total, 2);

        var settings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        settings.Controls.Add(new Label { Text = "Porta CIS", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        porta.Dock = DockStyle.Fill;
        porta.DropDownStyle = ComboBoxStyle.DropDownList;
        settings.Controls.Add(porta, 1, 0);

        var refresh = new Button { Text = "Atualizar", Dock = DockStyle.Fill };
        refresh.Click += (_, _) => RefreshPorts();
        settings.Controls.Add(refresh, 2, 0);

        baud.Dock = DockStyle.Fill;
        baud.DropDownStyle = ComboBoxStyle.DropDownList;
        baud.Items.AddRange(new object[] { "9600", "19200", "38400", "57600", "115200" });
        baud.SelectedItem = DefaultBaud;
        settings.Controls.Add(baud, 3, 0);

        bottom.Controls.Add(settings, 0, 1);
        bottom.SetColumnSpan(settings, 2);

        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var print = new Button
        {
            Text = "IMPRIMIR SALDOS",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            BackColor = Color.White
        };
        print.Click += (_, _) => Send(Renderer.Saldos(Value(atende), Value(caixa), Value(cofre)));
        buttons.Controls.Add(print, 0, 0);

        var clear = new Button
        {
            Text = "LIMPAR",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            BackColor = Color.White
        };
        clear.Click += (_, _) => { atende.Clear(); caixa.Clear(); cofre.Clear(); atende.Focus(); };
        buttons.Controls.Add(clear, 1, 0);

        bottom.Controls.Add(buttons, 0, 2);
        bottom.SetColumnSpan(buttons, 2);
        root.Controls.Add(bottom, 0, 4);

        foreach (var t in new[] { atende, caixa, cofre })
        {
            t.TextChanged += (_, _) => UpdateTotal();
            t.KeyDown += EnterNext;
        }
    }

    static void AddBalanceRow(TableLayoutPanel root, string label, TextBox box, int row)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Margin = new Padding(0, 4, 0, 4)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        panel.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 12.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        box.Dock = DockStyle.Fill;
        panel.Controls.Add(box, 1, 0);
        root.Controls.Add(panel, 0, row);
    }

    static TextBox MoneyBox() => new()
    {
        PlaceholderText = "0,00",
        MaxLength = 15,
        Font = new Font("Segoe UI", 24F, FontStyle.Bold),
        TextAlign = HorizontalAlignment.Right,
        Margin = new Padding(0, 5, 0, 5)
    };

    void EnterNext(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            SelectNextControl((Control)sender!, true, true, true, true);
        }
    }

    static decimal Value(TextBox box) =>
        decimal.TryParse(box.Text, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out var v) ? v : 0m;

    void UpdateTotal() =>
        total.Text = $"TOTAL   R$ {(Value(atende) + Value(caixa) + Value(cofre)):N2}";

    void RefreshPorts()
    {
        var old = porta.SelectedItem?.ToString();
        var ports = SerialPort.GetPortNames().OrderBy(x => x).ToArray();

        porta.Items.Clear();
        porta.Items.AddRange(ports);

        if (ports.Contains(old ?? "", StringComparer.OrdinalIgnoreCase))
            porta.SelectedItem = old;
        else if (ports.Contains("COM2", StringComparer.OrdinalIgnoreCase))
            porta.SelectedItem = "COM2";
        else if (ports.Length > 0)
            porta.SelectedIndex = 0;

        status.Text = ports.Length == 0
            ? "Nenhuma porta COM encontrada."
            : $"{ports.Length} porta(s) COM encontrada(s).";
    }

    void Send(byte[] data)
    {
        if (porta.SelectedItem is null)
        {
            MessageBox.Show(
                "Selecione a porta COM da CIS.",
                "Impressora não selecionada",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            using var serial = new SerialPort(
                porta.Text,
                int.Parse(baud.Text),
                Parity.None,
                8,
                StopBits.One)
            {
                Handshake = Handshake.None,
                DtrEnable = false,
                RtsEnable = false,
                WriteTimeout = 5000
            };

            serial.Open();
            serial.Write(data, 0, data.Length);
            serial.Write(new byte[] { 0x1D, 0x56, 0x00 }, 0, 3);
            status.Text = $"Impressão enviada para {porta.Text}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Não foi possível imprimir na {porta.Text}.\n\n{ex.Message}",
                "Erro de impressão",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}

public sealed class TextPrinterEditor : UserControl
{
    readonly Action<byte[]> print;
    readonly TextBox text = new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        AcceptsReturn = true,
        Text = "INFORMAÇÃO\n\nData: {DATA}\nHora: {HORA}"
    };

    readonly ComboBox font = new();
    readonly ComboBox align = new();
    readonly ComboBox models = new();
    readonly NumericUpDown size = new() { Minimum = 6, Maximum = 72, Value = 18 };
    readonly NumericUpDown margin = new() { Minimum = 0, Maximum = 80, Value = 24 };
    readonly CheckBox bold = new() { Text = "Negrito", AutoSize = true };
    readonly Panel previewHost = new() { BackColor = Color.FromArgb(230, 230, 230), AutoScroll = true };
    readonly PictureBox preview = new() { BackColor = Color.White };

    public TextPrinterEditor(Action<byte[]> printAction)
    {
        print = printAction;
        Dock = DockStyle.Fill;
        Build();
        Preview();
    }

    void Build()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 470,
            FixedPanel = FixedPanel.Panel1,
            IsSplitterFixed = false
        };
        Controls.Add(split);

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 10,
            Padding = new Padding(8),
            AutoScroll = true,
            BackColor = Color.White
        };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        split.Panel1.Controls.Add(left);

        left.Controls.Add(Header("IMPRESSÃO PERSONALIZADA"), 0, 0);
        left.Controls.Add(text, 0, 1);

        left.Controls.Add(Field("Fonte", font), 0, 2);
        left.Controls.Add(Field("Tamanho", size), 0, 3);
        left.Controls.Add(Field("Alinhamento", align), 0, 4);
        left.Controls.Add(Field("Margem lateral", margin), 0, 5);

        font.Items.AddRange(PreferredFonts());
        font.Text = font.Items.Contains("Arial") ? "Arial" : font.Items[0]?.ToString() ?? "Arial";

        align.Items.AddRange(new object[] { "Esquerda", "Centro", "Direita" });
        align.SelectedIndex = 1;

        left.Controls.Add(bold, 0, 6);
        left.Controls.Add(new Label
        {
            Text = "Variáveis disponíveis: {DATA} e {HORA}",
            ForeColor = Color.DimGray,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 7);

        var modelRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        modelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        modelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        models.Dock = DockStyle.Fill;
        modelRow.Controls.Add(models, 0, 0);
        var save = new Button { Text = "Salvar modelo", Dock = DockStyle.Fill };
        save.Click += (_, _) => Save();
        modelRow.Controls.Add(save, 1, 0);
        left.Controls.Add(modelRow, 0, 8);

        var printButton = new Button
        {
            Text = "IMPRIMIR",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            BackColor = Color.White
        };
        printButton.Click += (_, _) => print(Renderer.Raster(RenderBitmap()));
        left.Controls.Add(printButton, 0, 9);

        var previewPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            BackColor = Color.FromArgb(230, 230, 230)
        };
        previewPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        previewPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        split.Panel2.Controls.Add(previewPanel);

        previewPanel.Controls.Add(new Label
        {
            Text = "PRÉVIA — BOBINA 80 mm",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        }, 0, 0);

        previewHost.Dock = DockStyle.Fill;
        previewPanel.Controls.Add(previewHost, 0, 1);
        previewHost.Resize += (_, _) => CenterPreview();
        previewHost.Controls.Add(preview);

        text.TextChanged += (_, _) => Preview();
        font.SelectedIndexChanged += (_, _) => Preview();
        size.ValueChanged += (_, _) => Preview();
        align.SelectedIndexChanged += (_, _) => Preview();
        margin.ValueChanged += (_, _) => Preview();
        bold.CheckedChanged += (_, _) => Preview();

        models.Items.AddRange(Store.Names("text").ToArray());
        models.SelectedIndexChanged += (_, _) => LoadModel();
    }

    static Label Header(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 15F, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft
    };

    static Control Field(string label, Control control)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        row.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        control.Dock = DockStyle.Fill;
        row.Controls.Add(control, 1, 0);
        return row;
    }

    static object[] PreferredFonts()
    {
        var preferred = new[] { "Arial", "Calibri", "Segoe UI", "Tahoma", "Verdana", "Times New Roman" };
        var installed = new HashSet<string>(
            FontFamily.Families.Select(x => x.Name),
            StringComparer.OrdinalIgnoreCase);

        return preferred.Where(installed.Contains)
            .Concat(installed.Where(x => !preferred.Contains(x, StringComparer.OrdinalIgnoreCase)).OrderBy(x => x))
            .Cast<object>()
            .ToArray();
    }

    Bitmap RenderBitmap() =>
        Renderer.TextBitmap(
            text.Text,
            font.Text,
            (float)size.Value,
            bold.Checked,
            align.SelectedIndex,
            (int)margin.Value);

    void Preview()
    {
        try
        {
            preview.Image?.Dispose();
            preview.Image = RenderBitmap();
            preview.Size = preview.Image.Size;
            CenterPreview();
        }
        catch
        {
            preview.Image = null;
        }
    }

    void CenterPreview()
    {
        if (preview.Image is null) return;

        int x = Math.Max(10, (previewHost.ClientSize.Width - preview.Width) / 2);
        preview.Location = new Point(x, 12);
        previewHost.AutoScrollMinSize = new Size(
            Math.Max(preview.Width + 20, previewHost.ClientSize.Width),
            preview.Height + 24);
    }

    void Save()
    {
        var name = Prompt.Get("Nome do modelo", "Modelo de impressão");
        if (string.IsNullOrWhiteSpace(name)) return;

        Store.Save(
            "text",
            name,
            new Model(
                text.Text,
                font.Text,
                (float)size.Value,
                bold.Checked,
                align.SelectedIndex,
                (int)margin.Value));

        models.Items.Clear();
        models.Items.AddRange(Store.Names("text").ToArray());
        models.SelectedItem = name;
    }

    void LoadModel()
    {
        if (models.SelectedItem is not string name ||
            !Store.Load("text", name, out var model))
            return;

        text.Text = model.Text;
        font.Text = model.Font;
        size.Value = Math.Clamp((decimal)model.Size, size.Minimum, size.Maximum);
        bold.Checked = model.Bold;
        align.SelectedIndex = Math.Clamp(model.Align, 0, 2);
        margin.Value = Math.Clamp(model.Margin, margin.Minimum, margin.Maximum);
    }
}

public sealed class CoinPrinterEditor : UserControl
{
    readonly Action<byte[]> print;
    readonly ComboBox coin = new(), font = new(), align = new();
    readonly TextBox value = new() { Text = "100,00" };
    readonly NumericUpDown qty = new() { Minimum = 1, Maximum = 100, Value = 1 };
    readonly NumericUpDown titleSize = new() { Minimum = 8, Maximum = 72, Value = 25 };
    readonly NumericUpDown valueSize = new() { Minimum = 8, Maximum = 72, Value = 22 };
    readonly CheckBox border = new() { Text = "Borda", Checked = true, AutoSize = true };
    readonly Panel previewHost = new() { BackColor = Color.FromArgb(230, 230, 230), AutoScroll = true };
    readonly PictureBox preview = new() { BackColor = Color.White };

    public CoinPrinterEditor(Action<byte[]> printAction)
    {
        print = printAction;
        Dock = DockStyle.Fill;

        coin.Items.AddRange(new object[]
        {
            "5 CENTAVOS",
            "10 CENTAVOS",
            "25 CENTAVOS",
            "50 CENTAVOS",
            "1 REAL"
        });
        coin.SelectedIndex = 3;

        font.Items.AddRange(PreferredFonts());
        font.Text = font.Items.Contains("Arial") ? "Arial" : font.Items[0]?.ToString() ?? "Arial";

        align.Items.AddRange(new object[] { "Esquerda", "Centro", "Direita" });
        align.SelectedIndex = 1;

        Build();
        Preview();
    }

    void Build()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 430
        };
        Controls.Add(split);

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 10,
            Padding = new Padding(8),
            BackColor = Color.White
        };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        for (int i = 1; i < 9; i++) left.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        split.Panel1.Controls.Add(left);

        left.Controls.Add(Header("RÓTULO DE MOEDAS"), 0, 0);
        left.Controls.Add(Field("Tipo de moeda", coin), 0, 1);
        left.Controls.Add(Field("Valor do saquinho", value), 0, 2);
        left.Controls.Add(Field("Quantidade", qty), 0, 3);
        left.Controls.Add(Field("Fonte", font), 0, 4);
        left.Controls.Add(Field("Tam. moeda", titleSize), 0, 5);
        left.Controls.Add(Field("Tam. valor", valueSize), 0, 6);
        left.Controls.Add(Field("Alinhamento", align), 0, 7);

        var borderRow = new Panel { Dock = DockStyle.Fill };
        border.Dock = DockStyle.Left;
        borderRow.Controls.Add(border);
        left.Controls.Add(borderRow, 0, 8);

        var printButton = new Button
        {
            Text = "IMPRIMIR RÓTULOS",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            BackColor = Color.White
        };
        printButton.Click += (_, _) => PrintMany();
        left.Controls.Add(printButton, 0, 9);

        var previewPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            BackColor = Color.FromArgb(230, 230, 230)
        };
        previewPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        previewPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        split.Panel2.Controls.Add(previewPanel);

        previewPanel.Controls.Add(new Label
        {
            Text = "PRÉVIA — RÓTULO 80 mm",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        }, 0, 0);

        previewHost.Dock = DockStyle.Fill;
        previewPanel.Controls.Add(previewHost, 0, 1);
        previewHost.Resize += (_, _) => CenterPreview();
        previewHost.Controls.Add(preview);

        coin.SelectedIndexChanged += (_, _) => Preview();
        value.TextChanged += (_, _) => Preview();
        font.SelectedIndexChanged += (_, _) => Preview();
        titleSize.ValueChanged += (_, _) => Preview();
        valueSize.ValueChanged += (_, _) => Preview();
        align.SelectedIndexChanged += (_, _) => Preview();
        border.CheckedChanged += (_, _) => Preview();
    }

    static Label Header(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 15F, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft
    };

    static Control Field(string label, Control control)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        row.Controls.Add(new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        control.Dock = DockStyle.Fill;
        row.Controls.Add(control, 1, 0);
        return row;
    }

    static object[] PreferredFonts()
    {
        var preferred = new[] { "Arial", "Calibri", "Segoe UI", "Tahoma", "Verdana", "Times New Roman" };
        var installed = new HashSet<string>(
            FontFamily.Families.Select(x => x.Name),
            StringComparer.OrdinalIgnoreCase);

        return preferred.Where(installed.Contains)
            .Concat(installed.Where(x => !preferred.Contains(x, StringComparer.OrdinalIgnoreCase)).OrderBy(x => x))
            .Cast<object>()
            .ToArray();
    }

    Bitmap RenderBitmap() =>
        Renderer.CoinBitmap(
            coin.Text,
            value.Text,
            font.Text,
            (float)titleSize.Value,
            (float)valueSize.Value,
            align.SelectedIndex,
            border.Checked);

    void Preview()
    {
        try
        {
            preview.Image?.Dispose();
            preview.Image = RenderBitmap();
            preview.Size = preview.Image.Size;
            CenterPreview();
        }
        catch
        {
            preview.Image = null;
        }
    }

    void CenterPreview()
    {
        if (preview.Image is null) return;

        int x = Math.Max(10, (previewHost.ClientSize.Width - preview.Width) / 2);
        preview.Location = new Point(x, 12);
        previewHost.AutoScrollMinSize = new Size(
            Math.Max(preview.Width + 20, previewHost.ClientSize.Width),
            preview.Height + 24);
    }

    void PrintMany()
    {
        using var ms = new MemoryStream();

        for (int i = 0; i < (int)qty.Value; i++)
        {
            using var bitmap = RenderBitmap();
            var data = Renderer.Raster(bitmap);
            ms.Write(data, 0, data.Length);
        }

        print(ms.ToArray());
    }
}

public record Model(string Text, string Font, float Size, bool Bold, int Align, int Margin);

public static class Store
{
    static string FileFor(string type) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ControleSaldosCIS",
            "models",
            type + ".json");

    public static IEnumerable<string> Names(string type)
    {
        try
        {
            if (!File.Exists(FileFor(type))) return Array.Empty<string>();
            return (JsonSerializer.Deserialize<Dictionary<string, Model>>(
                File.ReadAllText(FileFor(type))) ?? new()).Keys.OrderBy(x => x);
        }
        catch { return Array.Empty<string>(); }
    }

    public static void Save(string type, string name, Model model)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FileFor(type))!);
        var data = Read(type);
        data[name] = model;
        File.WriteAllText(
            FileFor(type),
            JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static bool Load(string type, string name, out Model model)
    {
        var data = Read(type);
        if (data.TryGetValue(name, out model!)) return true;
        model = new("", "Arial", 16, false, 1, 24);
        return false;
    }

    static Dictionary<string, Model> Read(string type)
    {
        try
        {
            return File.Exists(FileFor(type))
                ? JsonSerializer.Deserialize<Dictionary<string, Model>>(File.ReadAllText(FileFor(type))) ?? new()
                : new();
        }
        catch { return new(); }
    }
}

public static class Renderer
{
    public const int Width = 576;

    static string Expand(string text) =>
        text.Replace("{DATA}", DateTime.Now.ToString("dd/MM/yyyy"))
            .Replace("{HORA}", DateTime.Now.ToString("HH:mm"));

    public static byte[] Saldos(decimal atende, decimal caixa, decimal cofre)
    {
        using var bitmap = new Bitmap(Width, 500);
        using var g = Graphics.FromImage(bitmap);

        g.Clear(Color.White);
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

        using var title = new Font("Arial", 25, FontStyle.Bold);
        using var normal = new Font("Arial", 16);
        using var bold = new Font("Arial", 19, FontStyle.Bold);

        Center(g, "CONTROLE DE SALDOS", title, 30);
        Center(g, DateTime.Now.ToString("dd/MM/yyyy HH:mm"), normal, 75);
        Left(g, $"CORREIOS ATENDE: R$ {atende:N2}", normal, 130);
        Left(g, $"CAIXA:           R$ {caixa:N2}", normal, 170);
        Left(g, $"COFRE:           R$ {cofre:N2}", normal, 210);
        Center(g, $"TOTAL: R$ {atende + caixa + cofre:N2}", bold, 275);

        return Raster(bitmap);
    }

    public static Bitmap TextBitmap(
        string raw,
        string family,
        float size,
        bool bold,
        int align,
        int margin)
    {
        family = string.IsNullOrWhiteSpace(family) ? "Arial" : family;

        using var font = new Font(
            family,
            size,
            bold ? FontStyle.Bold : FontStyle.Regular);

        var lines = WrapText(Expand(raw), font, Width - margin * 2);
        int lineHeight = Math.Max(16, (int)Math.Ceiling(font.GetHeight() + 5));
        int height = Math.Max(90, margin * 2 + lines.Count * lineHeight + 18);

        var bitmap = new Bitmap(Width, height);
        using var g = Graphics.FromImage(bitmap);

        g.Clear(Color.White);
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

        var format = new StringFormat
        {
            Alignment = align == 1
                ? StringAlignment.Center
                : align == 2
                    ? StringAlignment.Far
                    : StringAlignment.Near,
            LineAlignment = StringAlignment.Near,
            FormatFlags = StringFormatFlags.NoClip
        };

        float y = margin;
        float available = Width - margin * 2;

        foreach (var line in lines)
        {
            g.DrawString(
                line,
                font,
                Brushes.Black,
                new RectangleF(margin, y, available, lineHeight + 5),
                format);

            y += lineHeight;
        }

        return bitmap;
    }

    static List<string> WrapText(string text, Font font, float maxWidth)
    {
        var result = new List<string>();

        foreach (var original in text.Replace("\r", "").Split('\n'))
        {
            if (string.IsNullOrEmpty(original))
            {
                result.Add("");
                continue;
            }

            var words = original.Split(' ', StringSplitOptions.None);
            string current = "";

            foreach (var word in words)
            {
                string candidate = string.IsNullOrEmpty(current)
                    ? word
                    : current + " " + word;

                if (MeasureWidth(candidate, font) <= maxWidth)
                {
                    current = candidate;
                    continue;
                }

                if (!string.IsNullOrEmpty(current))
                {
                    result.Add(current);
                    current = "";
                }

                if (MeasureWidth(word, font) <= maxWidth)
                {
                    current = word;
                }
                else
                {
                    foreach (var chunk in SplitLongWord(word, font, maxWidth))
                        result.Add(chunk);
                }
            }

            if (!string.IsNullOrEmpty(current))
                result.Add(current);
        }

        return result.Count == 0 ? new List<string> { "" } : result;
    }

    static float MeasureWidth(string text, Font font)
    {
        using var bitmap = new Bitmap(1, 1);
        using var g = Graphics.FromImage(bitmap);
        return g.MeasureString(text, font).Width;
    }

    static IEnumerable<string> SplitLongWord(string word, Font font, float maxWidth)
    {
        string chunk = "";

        foreach (char c in word)
        {
            string candidate = chunk + c;

            if (MeasureWidth(candidate, font) <= maxWidth)
            {
                chunk = candidate;
            }
            else
            {
                if (!string.IsNullOrEmpty(chunk))
                    yield return chunk;

                chunk = c.ToString();
            }
        }

        if (!string.IsNullOrEmpty(chunk))
            yield return chunk;
    }

    public static Bitmap CoinBitmap(
        string coin,
        string rawValue,
        string family,
        float titleSize,
        float valueSize,
        int align,
        bool border)
    {
        family = string.IsNullOrWhiteSpace(family) ? "Arial" : family;

        decimal.TryParse(
            rawValue.Replace("R$", "").Trim(),
            NumberStyles.Number,
            CultureInfo.GetCultureInfo("pt-BR"),
            out var value);

        using var titleFont = new Font(family, titleSize, FontStyle.Bold);
        using var valueFont = new Font(family, valueSize, FontStyle.Bold);

        var bitmap = new Bitmap(Width, 300);
        using var g = Graphics.FromImage(bitmap);

        g.Clear(Color.White);
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

        var format = new StringFormat
        {
            Alignment = align == 1
                ? StringAlignment.Center
                : align == 2
                    ? StringAlignment.Far
                    : StringAlignment.Near
        };

        g.DrawString(
            coin,
            titleFont,
            Brushes.Black,
            new RectangleF(20, 60, Width - 40, 60),
            format);

        g.DrawString(
            value.ToString("C2", CultureInfo.GetCultureInfo("pt-BR")),
            valueFont,
            Brushes.Black,
            new RectangleF(20, 145, Width - 40, 60),
            format);

        if (border)
            g.DrawRectangle(Pens.Black, 10, 10, Width - 21, 279);

        return bitmap;
    }

    public static byte[] Raster(Bitmap bitmap)
    {
        int widthBytes = (bitmap.Width + 7) / 8;

        using var stream = new MemoryStream();

        stream.Write(new byte[]
        {
            0x1B, 0x40,
            0x1D, 0x76, 0x30, 0x00,
            (byte)(widthBytes & 0xFF),
            (byte)((widthBytes >> 8) & 0xFF),
            (byte)(bitmap.Height & 0xFF),
            (byte)((bitmap.Height >> 8) & 0xFF)
        });

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int xByte = 0; xByte < widthBytes; xByte++)
            {
                byte value = 0;

                for (int bit = 0; bit < 8; bit++)
                {
                    int x = xByte * 8 + bit;

                    if (x < bitmap.Width &&
                        bitmap.GetPixel(x, y).GetBrightness() < 0.5f)
                    {
                        value |= (byte)(0x80 >> bit);
                    }
                }

                stream.WriteByte(value);
            }
        }

        stream.Write(new byte[] { 0x0A, 0x0A, 0x0A }, 0, 3);
        return stream.ToArray();
    }

    static void Center(Graphics g, string text, Font font, float y) =>
        g.DrawString(
            text,
            font,
            Brushes.Black,
            new RectangleF(0, y, Width, 42),
            new StringFormat { Alignment = StringAlignment.Center });

    static void Left(Graphics g, string text, Font font, float y) =>
        g.DrawString(
            text,
            font,
            Brushes.Black,
            new RectangleF(24, y, Width - 48, 35));
}

public static class Prompt
{
    public static string? Get(string title, string label)
    {
        using var form = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(420, 145),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false
        };

        var labelControl = new Label
        {
            Text = label,
            Bounds = new Rectangle(20, 18, 370, 25)
        };

        var input = new TextBox
        {
            Bounds = new Rectangle(20, 48, 370, 30)
        };

        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Bounds = new Rectangle(220, 95, 80, 30)
        };

        var cancel = new Button
        {
            Text = "Cancelar",
            DialogResult = DialogResult.Cancel,
            Bounds = new Rectangle(310, 95, 80, 30)
        };

        form.Controls.AddRange(new Control[] { labelControl, input, ok, cancel });
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        return form.ShowDialog() == DialogResult.OK
            ? input.Text.Trim()
            : null;
    }
}
