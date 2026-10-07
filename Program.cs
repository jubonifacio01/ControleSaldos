using System;
using System.Collections.Generic;
using System.Drawing;
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
    [STAThread] static void Main()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public sealed class MainForm : Form
{
    readonly TextBox atende=Money(), caixa=Money(), cofre=Money();
    readonly ComboBox porta=new(), baud=new();
    readonly Label total=new(), status=new();
    readonly TabControl tabs=new();
    const string DefaultBaud="9600";

    public MainForm()
    {
        Text="Controle de Saldos CIS"; StartPosition=FormStartPosition.CenterScreen;
        ClientSize=new Size(1080,760); MinimumSize=new Size(1080,760);
        Font=new Font("Segoe UI",10); BackColor=Color.FromArgb(245,245,245);
        Build();
        RefreshPorts(); UpdateTotal();
    }

    void Build()
    {
        var title=new Label{Text="CONTROLE DE SALDOS CIS",Dock=DockStyle.Top,Height=62,
            Font=new Font("Segoe UI",21,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter};
        Controls.Add(tabs); Controls.Add(title); tabs.Dock=DockStyle.Fill; tabs.Padding=new Point(12,8);

        var p1=new TabPage("Controle de Saldos"){BackColor=BackColor};
        var p2=new TabPage("Impressão Personalizada"){BackColor=BackColor};
        var p3=new TabPage("Rótulos de Moedas"){BackColor=BackColor};
        BuildSaldos(p1); p2.Controls.Add(new TextPrinterEditor(Send)); p3.Controls.Add(new CoinPrinterEditor(Send));
        tabs.TabPages.AddRange(new[]{p1,p2,p3});
    }

    void BuildSaldos(Control p)
    {
        AddRow(p,"CORREIOS ATENDE",atende,65); AddRow(p,"CAIXA",caixa,180); AddRow(p,"COFRE",cofre,295);
        foreach(var t in new[]{atende,caixa,cofre}){t.TextChanged+=(_,_)=>UpdateTotal();t.KeyDown+=EnterNext;}
        var box=new Panel{Bounds=new Rectangle(30,410,620,78),BackColor=Color.White,BorderStyle=BorderStyle.FixedSingle};
        total.Dock=DockStyle.Fill; total.TextAlign=ContentAlignment.MiddleCenter; total.Font=new Font("Segoe UI",20,FontStyle.Bold); box.Controls.Add(total);p.Controls.Add(box);

        p.Controls.Add(new Label{Text="Porta da CIS:",Bounds=new Rectangle(30,525,115,32),TextAlign=ContentAlignment.MiddleLeft});
        porta.Bounds=new Rectangle(145,522,230,36); porta.DropDownStyle=ComboBoxStyle.DropDownList; p.Controls.Add(porta);
        var refb=new Button{Text="ATUALIZAR",Bounds=new Rectangle(385,522,125,36)};refb.Click+=(_,_)=>RefreshPorts();p.Controls.Add(refb);

        p.Controls.Add(new Label{Text="Velocidade:",Bounds=new Rectangle(30,570,115,32)});
        baud.Bounds=new Rectangle(145,567,230,36);baud.DropDownStyle=ComboBoxStyle.DropDownList;
        baud.Items.AddRange(new object[]{"9600","19200","38400","57600","115200"});baud.SelectedItem=DefaultBaud;p.Controls.Add(baud);

        var pr=new Button{Text="IMPRIMIR SALDOS",Bounds=new Rectangle(30,625,300,55),Font=new Font("Segoe UI",13,FontStyle.Bold),BackColor=Color.White};
        pr.Click+=(_,_)=>Send(Renderer.Saldos(Value(atende),Value(caixa),Value(cofre)));p.Controls.Add(pr);
        var cl=new Button{Text="LIMPAR",Bounds=new Rectangle(350,625,300,55),Font=new Font("Segoe UI",13,FontStyle.Bold),BackColor=Color.White};
        cl.Click+=(_,_)=>{atende.Clear();caixa.Clear();cofre.Clear();};p.Controls.Add(cl);
        status.Bounds=new Rectangle(30,690,620,24);status.TextAlign=ContentAlignment.MiddleCenter;status.ForeColor=Color.DimGray;p.Controls.Add(status);
    }

    static TextBox Money()=>new(){PlaceholderText="0,00",MaxLength=15,Font=new Font("Segoe UI",26,FontStyle.Bold),TextAlign=HorizontalAlignment.Right};
    static void AddRow(Control p,string s,TextBox t,int y){p.Controls.Add(new Label{Text=s,Font=new Font("Segoe UI",13,FontStyle.Bold),Bounds=new Rectangle(30,y+18,210,45)});t.Bounds=new Rectangle(240,y,410,82);p.Controls.Add(t);}
    void EnterNext(object? s,KeyEventArgs e){if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;SelectNextControl((Control)s!,true,true,true,true);}}
    static decimal Value(TextBox t){return decimal.TryParse(t.Text,NumberStyles.Number,CultureInfo.GetCultureInfo("pt-BR"),out var v)?v:0;}
    void UpdateTotal()=>total.Text=$"TOTAL   R$ {(Value(atende)+Value(caixa)+Value(cofre)):N2}";

    void RefreshPorts()
    {
        var old=porta.SelectedItem?.ToString();var ps=SerialPort.GetPortNames().OrderBy(x=>x).ToArray();
        porta.Items.Clear();porta.Items.AddRange(ps);
        if(ps.Contains(old??"",StringComparer.OrdinalIgnoreCase))porta.SelectedItem=old;
        else if(ps.Contains("COM2",StringComparer.OrdinalIgnoreCase))porta.SelectedItem="COM2";
        else if(ps.Length>0)porta.SelectedIndex=0;
        status.Text=ps.Length==0?"Nenhuma porta COM encontrada.":$"{ps.Length} porta(s) COM encontrada(s).";
    }

    void Send(byte[] data)
    {
        if(porta.SelectedItem is null){MessageBox.Show("Selecione a porta COM da CIS.");return;}
        try
        {
            using var s=new SerialPort(porta.Text,int.Parse(baud.Text),Parity.None,8,StopBits.One){Handshake=Handshake.None,DtrEnable=false,RtsEnable=false,WriteTimeout=5000};
            s.Open();s.Write(data,0,data.Length);s.Write(new byte[]{0x1D,0x56,0x00},0,3);
            status.Text=$"Impressão enviada para {porta.Text}.";
        }
        catch(Exception ex){MessageBox.Show($"Não foi possível imprimir na {porta.Text}.\n\n{ex.Message}","Erro de impressão",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }
}

public sealed class TextPrinterEditor:UserControl
{
    readonly Action<byte[]> print; readonly TextBox text=new(){Multiline=true,ScrollBars=ScrollBars.Vertical,Text="INFORMAÇÃO\n\nData: {DATA}\nHora: {HORA}"};
    readonly ComboBox font=new(),align=new(),models=new(); readonly NumericUpDown size=new(){Minimum=6,Maximum=72,Value=16},margin=new(){Minimum=0,Maximum=100,Value=24};
    readonly CheckBox bold=new(){Text="Negrito",AutoSize=true};readonly PictureBox preview=new(){BackColor=Color.White,SizeMode=PictureBoxSizeMode.Zoom};
    public TextPrinterEditor(Action<byte[]> p){print=p;Dock=DockStyle.Fill;Build();Preview();}
    void Build()
    {
        var l=new Panel{Dock=DockStyle.Left,Width=500,Padding=new Padding(18)};var r=new Panel{Dock=DockStyle.Fill,Padding=new Padding(18)};Controls.Add(r);Controls.Add(l);
        int y=10;l.Controls.Add(new Label{Text="IMPRESSÃO PERSONALIZADA",Font=new Font("Segoe UI",15,FontStyle.Bold),Bounds=new Rectangle(0,y,450,30)});y+=40;
        text.Bounds=new Rectangle(0,y,450,245);l.Controls.Add(text);y+=260;
        Add(l,"Fonte",font,y);font.Items.AddRange(FontFamily.Families.Select(x=>x.Name).OrderBy(x=>x).ToArray());font.Text="Arial";y+=42;
        Add(l,"Tamanho",size,y);y+=42;Add(l,"Alinhamento",align,y);align.Items.AddRange(new object[]{"Esquerda","Centro","Direita"});align.SelectedIndex=1;y+=42;
        Add(l,"Margem",margin,y);y+=42;bold.Bounds=new Rectangle(75,y,100,30);l.Controls.Add(bold);y+=45;
        l.Controls.Add(new Label{Text="Variáveis: {DATA}  {HORA}",ForeColor=Color.DimGray,Bounds=new Rectangle(0,y,450,25)});y+=32;
        models.Bounds=new Rectangle(0,y,270,32);l.Controls.Add(models);var save=new Button{Text="Salvar modelo",Bounds=new Rectangle(280,y,150,32)};save.Click+=(_,_)=>Save();l.Controls.Add(save);y+=45;
        var pr=new Button{Text="IMPRIMIR",Bounds=new Rectangle(0,y,430,48),Font=new Font("Segoe UI",11,FontStyle.Bold)};pr.Click+=(_,_)=>print(Render());l.Controls.Add(pr);
        r.Controls.Add(new Label{Text="PRÉVIA — BOBINA 80 mm",Dock=DockStyle.Top,Height=35,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI",11,FontStyle.Bold)});preview.Dock=DockStyle.Fill;r.Controls.Add(preview);
        text.TextChanged+=(_,_)=>Preview();font.SelectedIndexChanged+=(_,_)=>Preview();size.ValueChanged+=(_,_)=>Preview();align.SelectedIndexChanged+=(_,_)=>Preview();margin.ValueChanged+=(_,_)=>Preview();bold.CheckedChanged+=(_,_)=>Preview();
        models.Items.AddRange(Store.Names("text").ToArray());models.SelectedIndexChanged+=(_,_)=>LoadModel();
    }
    static void Add(Control p,string label,Control c,int y){p.Controls.Add(new Label{Text=label,Bounds=new Rectangle(0,y,75,30)});c.Bounds=new Rectangle(80,y,350,30);p.Controls.Add(c);}
    byte[] Render()=>Renderer.Text(text.Text,font.Text,(float)size.Value,bold.Checked,align.SelectedIndex,(int)margin.Value);
    void Preview(){preview.Image?.Dispose();preview.Image=Renderer.Preview(Render());}
    void Save(){var n=Prompt.Get("Nome do modelo","Modelo de impressão");if(string.IsNullOrWhiteSpace(n))return;Store.Save("text",n,new Model(text.Text,font.Text,(float)size.Value,bold.Checked,align.SelectedIndex,(int)margin.Value));models.Items.Clear();models.Items.AddRange(Store.Names("text").ToArray());models.SelectedItem=n;}
    void LoadModel(){if(models.SelectedItem is not string n||!Store.Load("text",n,out var m))return;text.Text=m.Text;font.Text=m.Font;size.Value=(decimal)m.Size;bold.Checked=m.Bold;align.SelectedIndex=m.Align;margin.Value=m.Margin;}
}

public sealed class CoinPrinterEditor:UserControl
{
    readonly Action<byte[]> print;readonly ComboBox coin=new(),font=new(),align=new();readonly TextBox value=new(){Text="100,00"};readonly NumericUpDown qty=new(){Minimum=1,Maximum=100,Value=1},titleSize=new(){Minimum=8,Maximum=72,Value=25},valueSize=new(){Minimum=8,Maximum=72,Value=22};readonly CheckBox border=new(){Text="Borda",Checked=true,AutoSize=true};readonly PictureBox preview=new(){BackColor=Color.White,SizeMode=PictureBoxSizeMode.Zoom};
    public CoinPrinterEditor(Action<byte[]> p){print=p;Dock=DockStyle.Fill;coin.Items.AddRange(new object[]{"5 CENTAVOS","10 CENTAVOS","25 CENTAVOS","50 CENTAVOS","1 REAL"});coin.SelectedIndex=3;font.Items.AddRange(FontFamily.Families.Select(x=>x.Name).OrderBy(x=>x).ToArray());font.Text="Arial";align.Items.AddRange(new object[]{"Esquerda","Centro","Direita"});align.SelectedIndex=1;Build();Preview();}
    void Build()
    {
        var l=new Panel{Dock=DockStyle.Left,Width=430,Padding=new Padding(18)};var r=new Panel{Dock=DockStyle.Fill,Padding=new Padding(18)};Controls.Add(r);Controls.Add(l);int y=12;
        l.Controls.Add(new Label{Text="RÓTULO DE MOEDAS",Font=new Font("Segoe UI",15,FontStyle.Bold),Bounds=new Rectangle(0,y,390,35)});y+=55;
        Add(l,"Tipo de moeda",coin,y);y+=44;Add(l,"Valor do saquinho",value,y);y+=44;Add(l,"Quantidade",qty,y);y+=44;Add(l,"Fonte",font,y);y+=44;Add(l,"Tam. moeda",titleSize,y);y+=44;Add(l,"Tam. valor",valueSize,y);y+=44;Add(l,"Alinhamento",align,y);y+=44;
        border.Bounds=new Rectangle(155,y,100,30);l.Controls.Add(border);y+=50;
        var pr=new Button{Text="IMPRIMIR RÓTULOS",Bounds=new Rectangle(0,y,380,50),Font=new Font("Segoe UI",11,FontStyle.Bold)};pr.Click+=(_,_)=>PrintMany();l.Controls.Add(pr);
        r.Controls.Add(new Label{Text="PRÉVIA — RÓTULO 80 mm",Dock=DockStyle.Top,Height=35,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI",11,FontStyle.Bold)});preview.Dock=DockStyle.Fill;r.Controls.Add(preview);
        foreach(var c in new Control[]{coin,value,font,align,titleSize,valueSize,border}){c.Click+=(_,_)=>Preview();c.TextChanged+=(_,_)=>Preview();}qty.ValueChanged+=(_,_)=>Preview();titleSize.ValueChanged+=(_,_)=>Preview();valueSize.ValueChanged+=(_,_)=>Preview();align.SelectedIndexChanged+=(_,_)=>Preview();font.SelectedIndexChanged+=(_,_)=>Preview();
    }
    static void Add(Control p,string s,Control c,int y){p.Controls.Add(new Label{Text=s,Bounds=new Rectangle(0,y,150,30)});c.Bounds=new Rectangle(155,y,240,30);p.Controls.Add(c);}
    byte[] Render()=>Renderer.Coin(coin.Text,value.Text,font.Text,(float)titleSize.Value,(float)valueSize.Value,align.SelectedIndex,border.Checked);
    void Preview(){preview.Image?.Dispose();preview.Image=Renderer.Preview(Render());}
    void PrintMany(){using var ms=new MemoryStream();for(int i=0;i<(int)qty.Value;i++){var b=Render();ms.Write(b,0,b.Length);}print(ms.ToArray());}
}

public record Model(string Text,string Font,float Size,bool Bold,int Align,int Margin);

public static class Store
{
    static string FileFor(string t)=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"ControleSaldosCIS","models",t+".json");
    public static IEnumerable<string> Names(string t){try{if(!File.Exists(FileFor(t)))return Array.Empty<string>();return (JsonSerializer.Deserialize<Dictionary<string,Model>>(File.ReadAllText(FileFor(t)))??new()).Keys.OrderBy(x=>x);}catch{return Array.Empty<string>();}}
    public static void Save(string t,string n,Model m){Directory.CreateDirectory(Path.GetDirectoryName(FileFor(t))!);var d=Read(t);d[n]=m;File.WriteAllText(FileFor(t),JsonSerializer.Serialize(d,new JsonSerializerOptions{WriteIndented=true}));}
    public static bool Load(string t,string n,out Model m){var d=Read(t);if(d.TryGetValue(n,out m!))return true;m=new("", "Arial",16,false,1,24);return false;}
    static Dictionary<string,Model> Read(string t){try{return File.Exists(FileFor(t))?JsonSerializer.Deserialize<Dictionary<string,Model>>(File.ReadAllText(FileFor(t)))??new():new();}catch{return new();}}
}

public static class Renderer
{
    public const int W=576;
    static string Expand(string s)=>s.Replace("{DATA}",DateTime.Now.ToString("dd/MM/yyyy")).Replace("{HORA}",DateTime.Now.ToString("HH:mm"));
    public static byte[] Saldos(decimal a,decimal c,decimal f){using var b=new Bitmap(W,520);using var g=Graphics.FromImage(b);g.Clear(Color.White);g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;using var t=new Font("Arial",25,FontStyle.Bold);using var n=new Font("Arial",16);using var x=new Font("Arial",19,FontStyle.Bold);Center(g,"CONTROLE DE SALDOS",t,30);Center(g,DateTime.Now.ToString("dd/MM/yyyy HH:mm"),n,75);Left(g,$"CORREIOS ATENDE: R$ {a:N2}",n,130);Left(g,$"CAIXA:           R$ {c:N2}",n,170);Left(g,$"COFRE:           R$ {f:N2}",n,210);Center(g,$"TOTAL: R$ {a+c+f:N2}",x,275);return Raster(b);}
    public static byte[] Text(string raw,string family,float size,bool bold,int align,int margin){using var f=new Font(string.IsNullOrWhiteSpace(family)?"Arial":family,size,bold?FontStyle.Bold:FontStyle.Regular);var lines=Expand(raw).Replace("\r","").Split('\n');int lh=Math.Max(14,(int)Math.Ceiling(f.GetHeight()+5)),h=Math.Max(80,margin*2+lines.Length*lh+10);using var b=new Bitmap(W,h);using var g=Graphics.FromImage(b);g.Clear(Color.White);g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;var sf=new StringFormat{Alignment=align==1?StringAlignment.Center:align==2?StringAlignment.Far:StringAlignment.Near};float y=margin;foreach(var line in lines){g.DrawString(line,f,Brushes.Black,new RectangleF(margin,y,W-margin*2,lh+4),sf);y+=lh;}return Raster(b);}
    public static byte[] Coin(string coin,string raw,string family,float ts,float vs,int align,bool border){decimal.TryParse(raw.Replace("R$","").Trim(),NumberStyles.Number,CultureInfo.GetCultureInfo("pt-BR"),out var v);using var b=new Bitmap(W,300);using var g=Graphics.FromImage(b);g.Clear(Color.White);g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;using var tf=new Font(string.IsNullOrWhiteSpace(family)?"Arial":family,ts,FontStyle.Bold);using var vf=new Font(string.IsNullOrWhiteSpace(family)?"Arial":family,vs,FontStyle.Bold);var sf=new StringFormat{Alignment=align==1?StringAlignment.Center:align==2?StringAlignment.Far:StringAlignment.Near};g.DrawString(coin,tf,Brushes.Black,new RectangleF(20,60,W-40,60),sf);g.DrawString(v.ToString("C2",CultureInfo.GetCultureInfo("pt-BR")),vf,Brushes.Black,new RectangleF(20,145,W-40,55),sf);if(border)g.DrawRectangle(Pens.Black,10,10,W-21,279);return Raster(b);}
    static void Center(Graphics g,string s,Font f,float y)=>g.DrawString(s,f,Brushes.Black,new RectangleF(0,y,W,42),new StringFormat{Alignment=StringAlignment.Center});
    static void Left(Graphics g,string s,Font f,float y)=>g.DrawString(s,f,Brushes.Black,new RectangleF(24,y,W-48,35));
    public static byte[] Raster(Bitmap b){int wb=(b.Width+7)/8;using var m=new MemoryStream();m.Write(new byte[]{0x1B,0x40,0x1D,0x76,0x30,0x00,(byte)wb,(byte)(wb>>8),(byte)b.Height,(byte)(b.Height>>8)});for(int y=0;y<b.Height;y++)for(int xb=0;xb<wb;xb++){byte q=0;for(int k=0;k<8;k++){int x=xb*8+k;if(x<b.Width&&b.GetPixel(x,y).GetBrightness()<.5)q|=(byte)(0x80>>k);}m.WriteByte(q);}m.Write(new byte[]{10,10,10},0,3);return m.ToArray();}
    public static Bitmap Preview(byte[] data){int p=Array.IndexOf(data,(byte)0x1D);if(p<0||p+9>=data.Length)return new Bitmap(W,300);int wb=data[p+6]|data[p+7]<<8,h=data[p+8]|data[p+9]<<8,w=wb*8;var b=new Bitmap(w,Math.Max(1,h));int d=p+10;for(int y=0;y<h;y++)for(int x=0;x<w;x++){int i=d+y*wb+x/8;if(i<data.Length&&(data[i]&(0x80>>(x%8)))!=0)b.SetPixel(x,y,Color.Black);}return b;}
}

public static class Prompt
{
    public static string? Get(string title,string label){using var f=new Form{Text=title,StartPosition=FormStartPosition.CenterParent,ClientSize=new Size(420,145),FormBorderStyle=FormBorderStyle.FixedDialog};var l=new Label{Text=label,Bounds=new Rectangle(20,18,370,25)};var t=new TextBox{Bounds=new Rectangle(20,48,370,30)};var ok=new Button{Text="OK",DialogResult=DialogResult.OK,Bounds=new Rectangle(220,95,80,30)};var ca=new Button{Text="Cancelar",DialogResult=DialogResult.Cancel,Bounds=new Rectangle(310,95,80,30)};f.Controls.AddRange(new Control[]{l,t,ok,ca});f.AcceptButton=ok;f.CancelButton=ca;return f.ShowDialog()==DialogResult.OK?t.Text.Trim():null;}
}