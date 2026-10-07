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
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ApplicationConfiguration.Initialize();

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => CrashLog.Show(e.Exception);

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    CrashLog.Write(ex);
            };

            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            CrashLog.Show(ex);
        }
    }
}

public sealed class MainForm : Form
{
    readonly TextBox atende = Money(), caixa = Money(), cofre = Money();
    readonly ComboBox porta = new(), baud = new();
    readonly Label total = new(), status = new();
    readonly TabControl tabs = new();

    const string DefaultBaud = "9600";

    public MainForm()
    {
        Text = "Controle de Saldos CIS";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1120, 800);
        MinimumSize = new Size(980, 700);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(245, 245, 245);

        Build();
        RefreshPorts();
        UpdateTotal();
    }

    void Build()
    {
        var title = new Label
        {
            Text = "CONTROLE DE SALDOS CIS",
            Dock = DockStyle.Top,
            Height = 68,
            Font = new Font("Segoe UI", 21F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.White
        };

        Controls.Add(tabs);
        Controls.Add(title);

        tabs.Dock = DockStyle.Fill;
        tabs.Padding = new Point(12, 8);

        var p1 = new TabPage("Controle de Saldos")
        {
            BackColor = BackColor,
            Padding = new Padding(10)
        };

        var p2 = new TabPage("Impressão Personalizada")
        {
            BackColor = BackColor,
            Padding = new Padding(10),
            Tag = false
        };

        var p3 = new TabPage("Rótulos de Moedas")
        {
            BackColor = BackColor,
            Padding = new Padding(10),
            Tag = false
        };

        BuildSaldos(p1);
        tabs.TabPages.AddRange(new[] { p1, p2, p3 });

        tabs.SelectedIndexChanged += (_, _) =>
        {
            try
            {
                if (tabs.SelectedTab == p2 && p2.Controls.Count == 0)
                {
                    p2.Controls.Add(new TextPrinterEditor(Send));
                    p2.Tag = true;
                }
                else if (tabs.SelectedTab == p3 && p3.Controls.Count == 0)
                {
                    p3.Controls.Add(new CoinPrinterEditor(Send));
                    p3.Tag = true;
                }
            }
            catch (Exception ex)
            {
                CrashLog.Show(ex);
                tabs.SelectedTab = p1;
            }
        };
    }

    void BuildSaldos(Control page)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(18),
            BackColor = Color.White
        };

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
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
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(0, 12, 0, 0)
        };

        bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        total.Dock = DockStyle.Fill;
        total.Font = new Font("Segoe UI", 19F, FontStyle.Bold);
        total.TextAlign = ContentAlignment.MiddleCenter;
        total.BackColor = Color.FromArgb(248, 248, 248);
        total.BorderStyle = BorderStyle.FixedSingle;
        bottom.Controls.Add(total, 0, 0);

        var settings = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4
        };

        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        settings.Controls.Add(new Label
        {
            Text = "Porta CIS:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

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

        var buttons = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };

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
        clear.Click += (_, _) =>
        {
            atende.Clear();
            caixa.Clear();
            cofre.Clear();
            atende.Focus();
        };
        buttons.Controls.Add(clear, 1, 0);

        bottom.Controls.Add(buttons, 0, 2);

        status.Dock = DockStyle.Fill;
        status.TextAlign = ContentAlignment.MiddleCenter;
        status.ForeColor = Color.DimGray;
        status.Text = "Pronto.";
        bottom.Controls.Add(status, 0, 3);

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
            Margin = new Padding(0, 5, 0, 5)
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

    static TextBox Money() => new()
    {
        PlaceholderText = "0,00",
        MaxLength = 15,
        Font = new Font("Segoe UI", 24F, FontStyle.Bold),
        TextAlign = HorizontalAlignment.Right,
        Margin = new Padding(0, 4, 0, 4)
    };

    void EnterNext(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            SelectNextControl((Control)sender!, true, true, true, true);
        }
    }

    static decimal Value(TextBox t) =>
        decimal.TryParse(
            t.Text,
            NumberStyles.Number,
            CultureInfo.GetCultureInfo("pt-BR"),
            out var v) ? v : 0m;

    void UpdateTotal() =>
        total.Text = $"TOTAL   R$ {(Value(atende) + Value(caixa) + Value(cofre)):N2}";

    void RefreshPorts()
    {
        var old = porta.SelectedItem?.ToString();
        var ps = SerialPort.GetPortNames()
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        porta.Items.Clear();
        porta.Items.AddRange(ps);

        if (ps.Contains(old ?? "", StringComparer.OrdinalIgnoreCase))
            porta.SelectedItem = old;
        else if (ps.Contains("COM2", StringComparer.OrdinalIgnoreCase))
            porta.SelectedItem = "COM2";
        else if (ps.Length > 0)
            porta.SelectedIndex = 0;

        status.Text = ps.Length == 0
            ? "Nenhuma porta COM encontrada."
            : $"{ps.Length} porta(s) COM encontrada(s).";
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
            using var s = new SerialPort(
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

            s.Open();
            s.Write(data, 0, data.Length);
            s.Write(new byte[] { 0x1D, 0x56, 0x00 }, 0, 3);

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
        Font = new Font("Segoe UI", 10F),
        Text = "INFORMAÇÃO\n\nData: {DATA}\nHora: {HORA}"
    };

    readonly ComboBox font = new();
    readonly ComboBox align = new();
    readonly ComboBox models = new();

    readonly NumericUpDown size = new()
    {
        Minimum = 6,
        Maximum = 72,
        Value = 16
    };

    readonly NumericUpDown margin = new()
    {
        Minimum = 0,
        Maximum = 80,
        Value = 24
    };

    readonly NumericUpDown topMargin = new()
    {
        Minimum = 0,
        Maximum = 120,
        Value = 18
    };

    readonly CheckBox bold = new()
    {
        Text = "Negrito",
        AutoSize = true
    };

    readonly Panel previewHost = new()
    {
        BackColor = Color.FromArgb(232, 232, 232),
        AutoScroll = true
    };

    readonly PictureBox preview = new()
    {
        BackColor = Color.White,
        SizeMode = PictureBoxSizeMode.AutoSize
    };

    public TextPrinterEditor(Action<byte[]> p)
    {
        print = p;
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
            SplitterDistance = 440,
            Panel1MinSize = 380,
            Panel2MinSize = 500
        };

        Controls.Add(split);

        var leftHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.White
        };

        split.Panel1.Controls.Add(leftHost);

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Width = 405,
            Height = 620,
            ColumnCount = 1,
            RowCount = 11,
            Padding = new Padding(12),
            BackColor = Color.White
        };

        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        leftHost.Controls.Add(left);

        left.Controls.Add(Header("IMPRESSÃO PERSONALIZADA"), 0, 0);
        left.Controls.Add(text, 0, 1);
        left.Controls.Add(Field("Fonte", font), 0, 2);
        left.Controls.Add(Field("Tamanho", size), 0, 3);
        left.Controls.Add(Field("Alinhamento", align), 0, 4);
        left.Controls.Add(Field("Margem lateral", margin), 0, 5);
        left.Controls.Add(Field("Margem superior", topMargin), 0, 6);
        left.Controls.Add(bold, 0, 7);

        left.Controls.Add(new Label
        {
            Text = "A quebra de linha é automática para não cortar o texto na bobina.",
            Dock = DockStyle.Fill,
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 8);

        var modelRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };

        modelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        modelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));

        models.Dock = DockStyle.Fill;
        modelRow.Controls.Add(models, 0, 0);

        var save = new Button
        {
            Text = "Salvar modelo",
            Dock = DockStyle.Fill
        };
        save.Click += (_, _) => Save();
        modelRow.Controls.Add(save, 1, 0);

        left.Controls.Add(modelRow, 0, 9);

        var printButton = new Button
        {
            Text = "IMPRIMIR",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            BackColor = Color.White
        };
        printButton.Click += (_, _) => PrintCurrent();
        left.Controls.Add(printButton, 0, 10);

        font.Items.AddRange(InstalledFonts());
        font.Text = font.Items.Contains("Arial")
            ? "Arial"
            : font.Items.Count > 0 ? font.Items[0].ToString() : "Arial";

        align.Items.AddRange(new object[] { "Esquerda", "Centro", "Direita" });
        align.SelectedIndex = 1;

        var previewPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            BackColor = Color.FromArgb(232, 232, 232)
        };

        previewPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
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
        previewHost.Controls.Add(preview);

        previewHost.Resize += (_, _) => CenterPreview();

        text.TextChanged += (_, _) => Preview();
        font.SelectedIndexChanged += (_, _) => Preview();
        size.ValueChanged += (_, _) => Preview();
        align.SelectedIndexChanged += (_, _) => Preview();
        margin.ValueChanged += (_, _) => Preview();
        topMargin.ValueChanged += (_, _) => Preview();
        bold.CheckedChanged += (_, _) => Preview();

        models.Items.AddRange(Store.Names("text").ToArray());
        models.SelectedIndexChanged += (_, _) => LoadModel();
    }

    static Label Header(string value) => new()
    {
        Text = value,
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 15F, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft
    };

    static Control Field(string label, Control control)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };

        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
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

    static object[] InstalledFonts()
    {
        var preferred = new[]
        {
            "Arial",
            "Calibri",
            "Segoe UI",
            "Tahoma",
            "Verdana",
            "Times New Roman"
        };

        var installed = new HashSet<string>(
            FontFamily.Families.Select(f => f.Name),
            StringComparer.OrdinalIgnoreCase);

        return preferred
            .Where(installed.Contains)
            .Concat(installed.Where(f => !preferred.Contains(f, StringComparer.OrdinalIgnoreCase)).OrderBy(f => f))
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
            (int)margin.Value,
            (int)topMargin.Value);

    void PrintCurrent()
    {
        using var bitmap = RenderBitmap();
        print(Renderer.Raster(bitmap));
    }

    void Preview()
    {
        try
        {
            var bitmap = RenderBitmap();
            var old = preview.Image;
            preview.Image = bitmap;
            preview.Size = bitmap.Size;
            old?.Dispose();
            CenterPreview();
        }
        catch
        {
            preview.Image = null;
        }
    }

    void CenterPreview()
    {
        if (preview.Image is null)
            return;

        var x = Math.Max(
            10,
            (previewHost.ClientSize.Width - preview.Width) / 2);

        preview.Location = new Point(x, 12);

        previewHost.AutoScrollMinSize = new Size(
            Math.Max(preview.Width + 20, previewHost.ClientSize.Width),
            preview.Height + 24);
    }

    void Save()
    {
        var name = Prompt.Get("Nome do modelo", "Modelo de impressão");

        if (string.IsNullOrWhiteSpace(name))
            return;

        Store.Save(
            "text",
            name,
            new Model(
                text.Text,
                font.Text,
                (float)size.Value,
                bold.Checked,
                align.SelectedIndex,
                (int)margin.Value,
                (int)topMargin.Value));

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
        topMargin.Value = Math.Clamp(model.TopMargin, topMargin.Minimum, topMargin.Maximum);
    }
}

public sealed class CoinPrinterEditor : UserControl
{
    readonly Action<byte[]> print;

    readonly ComboBox coin = new();
    readonly ComboBox font = new();
    readonly ComboBox align = new();

    readonly TextBox value = new()
    {
        Text = "100,00"
    };

    readonly NumericUpDown qty = new()
    {
        Minimum = 1,
        Maximum = 100,
        Value = 1
    };

    readonly NumericUpDown titleSize = new()
    {
        Minimum = 8,
        Maximum = 72,
        Value = 25
    };

    readonly NumericUpDown valueSize = new()
    {
        Minimum = 8,
        Maximum = 72,
        Value = 22
    };

    readonly CheckBox border = new()
    {
        Text = "Borda",
        Checked = true,
        AutoSize = true
    };

    readonly Panel previewHost = new()
    {
        BackColor = Color.FromArgb(232, 232, 232),
        AutoScroll = true
    };

    readonly PictureBox preview = new()
    {
        BackColor = Color.White,
        SizeMode = PictureBoxSizeMode.AutoSize
    };

    public CoinPrinterEditor(Action<byte[]> p)
    {
        print = p;
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

        font.Items.AddRange(TextPrinterEditorFonts());
        font.Text = font.Items.Contains("Arial")
            ? "Arial"
            : font.Items.Count > 0 ? font.Items[0].ToString() : "Arial";

        align.Items.AddRange(new object[] { "Esquerda", "Centro", "Direita" });
        align.SelectedIndex = 1;

        Build();
        Preview();
    }

    static object[] TextPrinterEditorFonts()
    {
        var preferred = new[]
        {
            "Arial",
            "Calibri",
            "Segoe UI",
            "Tahoma",
            "Verdana",
            "Times New Roman"
        };

        var installed = new HashSet<string>(
            FontFamily.Families.Select(f => f.Name),
            StringComparer.OrdinalIgnoreCase);

        return preferred
            .Where(installed.Contains)
            .Concat(installed.Where(f => !preferred.Contains(f, StringComparer.OrdinalIgnoreCase)).OrderBy(f => f))
            .Cast<object>()
            .ToArray();
    }

    void Build()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 430,
            Panel1MinSize = 360,
            Panel2MinSize = 500
        };

        Controls.Add(split);

        var leftHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.White
        };

        split.Panel1.Controls.Add(leftHost);

        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Width = 395,
            Height = 540,
            ColumnCount = 1,
            RowCount = 10,
            Padding = new Padding(12),
            BackColor = Color.White
        };

        for (int i = 0; i < 10; i++)
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, i == 0 ? 45 : 42));

        leftHost.Controls.Add(left);

        left.Controls.Add(new Label
        {
            Text = "RÓTULO DE MOEDAS",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

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
            BackColor = Color.FromArgb(232, 232, 232)
        };

        previewPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
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
        previewHost.Controls.Add(preview);

        previewHost.Resize += (_, _) => CenterPreview();

        coin.SelectedIndexChanged += (_, _) => Preview();
        value.TextChanged += (_, _) => Preview();
        font.SelectedIndexChanged += (_, _) => Preview();
        titleSize.ValueChanged += (_, _) => Preview();
        valueSize.ValueChanged += (_, _) => Preview();
        align.SelectedIndexChanged += (_, _) => Preview();
        border.CheckedChanged += (_, _) => Preview();
    }

    static Control Field(string label, Control control)
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2
        };

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
            var bitmap = RenderBitmap();
            var old = preview.Image;
            preview.Image = bitmap;
            preview.Size = bitmap.Size;
            old?.Dispose();
            CenterPreview();
        }
        catch
        {
            preview.Image = null;
        }
    }

    void CenterPreview()
    {
        if (preview.Image is null)
            return;

        var x = Math.Max(
            10,
            (previewHost.ClientSize.Width - preview.Width) / 2);

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

public record Model(
    string Text,
    string Font,
    float Size,
    bool Bold,
    int Align,
    int Margin,
    int TopMargin = 18);

public static class Store
{
    static string FileFor(string t) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ControleSaldosCIS",
            "models",
            t + ".json");

    public static IEnumerable<string> Names(string t)
    {
        try
        {
            if (!File.Exists(FileFor(t)))
                return Array.Empty<string>();

            return (JsonSerializer.Deserialize<Dictionary<string, Model>>(
                File.ReadAllText(FileFor(t))) ?? new())
                .Keys
                .OrderBy(x => x);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public static void Save(string t, string n, Model m)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FileFor(t))!);

        var d = Read(t);
        d[n] = m;

        File.WriteAllText(
            FileFor(t),
            JsonSerializer.Serialize(
                d,
                new JsonSerializerOptions { WriteIndented = true }));
    }

    public static bool Load(string t, string n, out Model m)
    {
        var d = Read(t);

        if (d.TryGetValue(n, out m!))
            return true;

        m = new Model("", "Arial", 16, false, 1, 24, 18);
        return false;
    }

    static Dictionary<string, Model> Read(string t)
    {
        try
        {
            return File.Exists(FileFor(t))
                ? JsonSerializer.Deserialize<Dictionary<string, Model>>(
                    File.ReadAllText(FileFor(t))) ?? new()
                : new();
        }
        catch
        {
            return new();
        }
    }
}

public static class Renderer
{
    public const int Width = 576;

    static string Expand(string s) =>
        s.Replace("{DATA}", DateTime.Now.ToString("dd/MM/yyyy"))
         .Replace("{HORA}", DateTime.Now.ToString("HH:mm"));

    public static byte[] Saldos(decimal a, decimal c, decimal f)
    {
        using var b = new Bitmap(Width, 520);
        using var g = Graphics.FromImage(b);

        g.Clear(Color.White);
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

        using var t = new Font("Arial", 25, FontStyle.Bold);
        using var n = new Font("Arial", 16);
        using var x = new Font("Arial", 19, FontStyle.Bold);

        Center(g, "CONTROLE DE SALDOS", t, 30);
        Center(g, DateTime.Now.ToString("dd/MM/yyyy HH:mm"), n, 75);
        Left(g, $"CORREIOS ATENDE: R$ {a:N2}", n, 130);
        Left(g, $"CAIXA:           R$ {c:N2}", n, 170);
        Left(g, $"COFRE:           R$ {f:N2}", n, 210);
        Center(g, $"TOTAL: R$ {a + c + f:N2}", x, 275);

        return Raster(b);
    }

    public static Bitmap TextBitmap(
        string raw,
        string family,
        float size,
        bool bold,
        int align,
        int margin,
        int topMargin)
    {
        family = string.IsNullOrWhiteSpace(family) ? "Arial" : family;

        using var font = new Font(
            family,
            size,
            bold ? FontStyle.Bold : FontStyle.Regular);

        var maxWidth = Math.Max(40, Width - margin * 2);
        var lines = WrapText(Expand(raw), font, maxWidth);

        var lineHeight = Math.Max(
            16,
            (int)Math.Ceiling(font.GetHeight() + 5));

        var height = Math.Max(
            90,
            topMargin + margin + lines.Count * lineHeight + 20);

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

        float y = topMargin;

        foreach (var line in lines)
        {
            g.DrawString(
                line,
                font,
                Brushes.Black,
                new RectangleF(
                    margin,
                    y,
                    maxWidth,
                    lineHeight + 5),
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
            var current = "";

            foreach (var word in words)
            {
                var candidate = string.IsNullOrEmpty(current)
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
                    foreach (var part in SplitLongWord(word, font, maxWidth))
                        result.Add(part);
                }
            }

            if (!string.IsNullOrEmpty(current))
                result.Add(current);
        }

        return result.Count == 0
            ? new List<string> { "" }
            : result;
    }

    static float MeasureWidth(string text, Font font)
    {
        using var bitmap = new Bitmap(1, 1);
        using var g = Graphics.FromImage(bitmap);
        return g.MeasureString(text, font).Width;
    }

    static IEnumerable<string> SplitLongWord(
        string word,
        Font font,
        float maxWidth)
    {
        var chunk = "";

        foreach (var c in word)
        {
            var candidate = chunk + c;

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
        string raw,
        string family,
        float titleSize,
        float valueSize,
        int align,
        bool border)
    {
        family = string.IsNullOrWhiteSpace(family) ? "Arial" : family;

        decimal.TryParse(
            raw.Replace("R$", "").Trim(),
            NumberStyles.Number,
            CultureInfo.GetCultureInfo("pt-BR"),
            out var v);

        using var b = new Bitmap(Width, 300);
        using var g = Graphics.FromImage(b);

        g.Clear(Color.White);
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

        using var tf = new Font(
            family,
            titleSize,
            FontStyle.Bold);

        using var vf = new Font(
            family,
            valueSize,
            FontStyle.Bold);

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
            tf,
            Brushes.Black,
            new RectangleF(20, 60, Width - 40, 60),
            format);

        g.DrawString(
            v.ToString("C2", CultureInfo.GetCultureInfo("pt-BR")),
            vf,
            Brushes.Black,
            new RectangleF(20, 145, Width - 40, 60),
            format);

        if (border)
            g.DrawRectangle(Pens.Black, 10, 10, Width - 21, 279);

        return new Bitmap(b);
    }

    public static byte[] Raster(Bitmap b)
    {
        var widthBytes = (b.Width + 7) / 8;

        using var m = new MemoryStream();

        m.Write(
            new byte[]
            {
                0x1B, 0x40,
                0x1D, 0x76, 0x30, 0x00,
                (byte)(widthBytes & 0xFF),
                (byte)((widthBytes >> 8) & 0xFF),
                (byte)(b.Height & 0xFF),
                (byte)((b.Height >> 8) & 0xFF)
            },
            0,
            10);

        for (int y = 0; y < b.Height; y++)
        {
            for (int xByte = 0; xByte < widthBytes; xByte++)
            {
                byte value = 0;

                for (int bit = 0; bit < 8; bit++)
                {
                    int x = xByte * 8 + bit;

                    if (x < b.Width &&
                        b.GetPixel(x, y).GetBrightness() < 0.5f)
                    {
                        value |= (byte)(0x80 >> bit);
                    }
                }

                m.WriteByte(value);
            }
        }

        m.Write(new byte[] { 0x0A, 0x0A, 0x0A }, 0, 3);

        return m.ToArray();
    }

    static void Center(Graphics g, string s, Font f, float y) =>
        g.DrawString(
            s,
            f,
            Brushes.Black,
            new RectangleF(0, y, Width, 42),
            new StringFormat { Alignment = StringAlignment.Center });

    static void Left(Graphics g, string s, Font f, float y) =>
        g.DrawString(
            s,
            f,
            Brushes.Black,
            new RectangleF(24, y, Width - 48, 35));
}

public static class Prompt
{
    public static string? Get(string title, string label)
    {
        using var f = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(420, 145),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false
        };

        var l = new Label
        {
            Text = label,
            Bounds = new Rectangle(20, 18, 370, 25)
        };

        var t = new TextBox
        {
            Bounds = new Rectangle(20, 48, 370, 30)
        };

        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Bounds = new Rectangle(220, 95, 80, 30)
        };

        var ca = new Button
        {
            Text = "Cancelar",
            DialogResult = DialogResult.Cancel,
            Bounds = new Rectangle(310, 95, 80, 30)
        };

        f.Controls.AddRange(new Control[] { l, t, ok, ca });
        f.AcceptButton = ok;
        f.CancelButton = ca;

        return f.ShowDialog() == DialogResult.OK
            ? t.Text.Trim()
            : null;
    }
}


internal static class CrashLog
{
    static string LogPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ControleSaldosCIS",
            "erro-inicializacao.txt");

    public static void Write(Exception ex)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath)!;
            Directory.CreateDirectory(dir);

            File.AppendAllText(
                LogPath,
                $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}]\\r\\n{ex}\\r\\n\\r\\n");
        }
        catch
        {
            // Não deixar o tratamento de erro gerar outro erro.
        }
    }

    public static void Show(Exception ex)
    {
        Write(ex);

        try
        {
            MessageBox.Show(
                $"O Controle de Saldos CIS encontrou um erro e não conseguiu concluir esta operação.\\n\\n" +
                $"Detalhes: {ex.Message}\\n\\n" +
                $"O erro foi registrado em:\\n{LogPath}",
                "Controle de Saldos CIS",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
        }
    }
}
