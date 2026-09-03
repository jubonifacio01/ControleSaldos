using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public sealed class MainForm : Form
{
    private readonly TextBox atende;
    private readonly TextBox caixa;
    private readonly TextBox cofre;
    private readonly Label totalLabel;
    private readonly ComboBox printerCombo;
    private readonly Button printButton;
    private readonly Button clearButton;

    private readonly CultureInfo br = new("pt-BR");

    public MainForm()
    {
        Text = "Controle de Saldos";
        Width = 520;
        Height = 510;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        var title = new Label
        {
            Text = "CONTROLE DE SALDOS",
            Left = 25, Top = 18, Width = 450, Height = 38,
            Font = new Font("Segoe UI", 17, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var subtitle = new Label
        {
            Text = "Informe os três saldos para impressão",
            Left = 25, Top = 55, Width = 450, Height = 25,
            Font = new Font("Segoe UI", 9),
            TextAlign = ContentAlignment.MiddleCenter
        };

        atende = MoneyBox();
        caixa = MoneyBox();
        cofre = MoneyBox();

        AddField("CORREIOS ATENDE", atende, 95);
        AddField("CAIXA", caixa, 175);
        AddField("COFRE", cofre, 255);

        totalLabel = new Label
        {
            Text = "TOTAL  R$ 0,00",
            Left = 25, Top = 335, Width = 450, Height = 42,
            Font = new Font("Segoe UI", 15, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            BorderStyle = BorderStyle.FixedSingle
        };

        var printerLabel = new Label
        {
            Text = "Impressora:",
            Left = 25, Top = 393, Width = 75, Height = 27,
            TextAlign = ContentAlignment.MiddleLeft
        };

        printerCombo = new ComboBox
        {
            Left = 100, Top = 390, Width = 350, Height = 30,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        clearButton = new Button
        {
            Text = "LIMPAR",
            Left = 25, Top = 435, Width = 130, Height = 38
        };

        printButton = new Button
        {
            Text = "IMPRIMIR",
            Left = 295, Top = 435, Width = 155, Height = 38,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };

        Controls.AddRange(new Control[]
        {
            title, subtitle, totalLabel, printerLabel, printerCombo,
            clearButton, printButton
        });

        foreach (var tb in new[] { atende, caixa, cofre })
        {
            tb.TextChanged += (_, _) => UpdateTotal();
            tb.KeyDown += MoneyBox_KeyDown;
        }

        clearButton.Click += (_, _) =>
        {
            atende.Clear();
            caixa.Clear();
            cofre.Clear();
            atende.Focus();
        };

        printButton.Click += (_, _) => PrintReport();

        LoadPrinters();
        atende.Focus();
    }

    private TextBox MoneyBox()
    {
        return new TextBox
        {
            Width = 360,
            Height = 38,
            Font = new Font("Segoe UI", 17, FontStyle.Bold),
            TextAlign = HorizontalAlignment.Right,
            PlaceholderText = "0,00"
        };
    }

    private void AddField(string caption, TextBox box, int top)
    {
        var label = new Label
        {
            Text = caption,
            Left = 25, Top = top, Width = 150, Height = 38,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        box.Left = 165;
        box.Top = top - 3;
        Controls.Add(label);
        Controls.Add(box);
    }

    private void MoneyBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            SelectNextControl((Control)sender!, true, true, true, true);
        }
    }

    private decimal ReadMoney(TextBox box)
    {
        var text = box.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return 0m;

        // Accepts both 1234,56 and 1.234,56, as well as plain 1234.56.
        text = text.Replace("R$", "").Trim();

        if (decimal.TryParse(text, NumberStyles.Number, br, out var value))
            return value;

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            return value;

        return 0m;
    }

    private void UpdateTotal()
    {
        var total = ReadMoney(atende) + ReadMoney(caixa) + ReadMoney(cofre);
        totalLabel.Text = $"TOTAL  {total.ToString("C", br)}";
    }

    private void LoadPrinters()
    {
        foreach (string printer in PrinterSettings.InstalledPrinters)
            printerCombo.Items.Add(printer);

        var defaultPrinter = new PrinterSettings().PrinterName;
        if (printerCombo.Items.Contains(defaultPrinter))
            printerCombo.SelectedItem = defaultPrinter;
        else if (printerCombo.Items.Count > 0)
            printerCombo.SelectedIndex = 0;
    }

    private void PrintReport()
    {
        if (printerCombo.SelectedItem is not string printerName || string.IsNullOrWhiteSpace(printerName))
        {
            MessageBox.Show("Selecione uma impressora.", "Controle de Saldos",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var report = new SaldoReport
        {
            Atende = ReadMoney(atende),
            Caixa = ReadMoney(caixa),
            Cofre = ReadMoney(cofre),
            PrinterName = printerName,
            DateTime = DateTime.Now
        };

        using var document = new PrintDocument();
        document.PrinterSettings.PrinterName = printerName;

        if (!document.PrinterSettings.IsValid)
        {
            MessageBox.Show("A impressora selecionada não está disponível.",
                "Controle de Saldos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // Thermal printers commonly use 80 mm paper. The actual driver controls
        // the physical paper size; we print within a conservative printable area.
        document.DefaultPageSettings.Margins = new Margins(8, 8, 8, 8);
        document.PrintPage += (_, e) => DrawReport(e, report);

        try
        {
            document.Print();
            statusSafe("Relatório enviado para a impressora.");
        }
        catch (Exception ex)
        {
            MessageBox.Show("Não foi possível imprimir.\n\n" + ex.Message,
                "Controle de Saldos", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DrawReport(PrintPageEventArgs e, SaldoReport r)
    {
        float x = e.MarginBounds.Left;
        float width = e.MarginBounds.Width;
        float y = e.MarginBounds.Top;

        using var titleFont = new Font("Arial", 13, FontStyle.Bold);
        using var smallFont = new Font("Arial", 8, FontStyle.Regular);
        using var labelFont = new Font("Arial", 11, FontStyle.Bold);
        using var valueFont = new Font("Arial", 19, FontStyle.Bold);
        using var totalLabelFont = new Font("Arial", 12, FontStyle.Bold);
        using var totalFont = new Font("Arial", 22, FontStyle.Bold);

        using var center = new StringFormat { Alignment = StringAlignment.Center };
        using var right = new StringFormat { Alignment = StringAlignment.Far };

        e.Graphics.DrawString("CONTROLE DE SALDOS", titleFont, Brushes.Black,
            new RectangleF(x, y, width, 28), center);
        y += 30;

        e.Graphics.DrawString(r.DateTime.ToString("dd/MM/yyyy  HH:mm"),
            smallFont, Brushes.Black, new RectangleF(x, y, width, 18), center);
        y += 28;

        using var pen = new Pen(Color.Black, 1);
        e.Graphics.DrawLine(pen, x, y, x + width, y);
        y += 12;

        DrawItem(e.Graphics, "CORREIOS ATENDE", r.Atende, x, width, ref y,
            labelFont, valueFont, center);
        DrawItem(e.Graphics, "CAIXA", r.Caixa, x, width, ref y,
            labelFont, valueFont, center);
        DrawItem(e.Graphics, "COFRE", r.Cofre, x, width, ref y,
            labelFont, valueFont, center);

        e.Graphics.DrawLine(pen, x, y, x + width, y);
        y += 12;

        e.Graphics.DrawString("TOTAL", totalLabelFont, Brushes.Black,
            new RectangleF(x, y, width, 25), center);
        y += 25;

        var total = r.Atende + r.Caixa + r.Cofre;
        e.Graphics.DrawString(total.ToString("C", br), totalFont, Brushes.Black,
            new RectangleF(x, y, width, 38), center);
        y += 45;

        e.HasMorePages = false;
    }

    private static void DrawItem(
        Graphics g, string label, decimal value, float x, float width, ref float y,
        Font labelFont, Font valueFont, StringFormat center)
    {
        g.DrawString(label, labelFont, Brushes.Black,
            new RectangleF(x, y, width, 24), center);
        y += 25;

        g.DrawString(value.ToString("C", new CultureInfo("pt-BR")), valueFont, Brushes.Black,
            new RectangleF(x, y, width, 35), center);
        y += 47;
    }

    private void statusSafe(string text)
    {
        // Keep the main UI deliberately simple; use the window title as transient feedback.
        Text = text;
        var timer = new System.Windows.Forms.Timer { Interval = 2200 };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            timer.Dispose();
            Text = "Controle de Saldos";
        };
        timer.Start();
    }

    private sealed class SaldoReport
    {
        public decimal Atende { get; init; }
        public decimal Caixa { get; init; }
        public decimal Cofre { get; init; }
        public string PrinterName { get; init; } = "";
        public DateTime DateTime { get; init; }
    }
}
