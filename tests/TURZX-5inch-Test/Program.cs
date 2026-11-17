// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Management;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UsbScreenTest {
static class Program {
 [STAThread] static void Main(string[] args) {
  Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
  if(args.Contains("--self-test")) { SelfTest.Run(); return; }
  Application.ThreadException += (s,e)=>MessageBox.Show(e.Exception.ToString(),"エラー");
  Application.Run(new MainForm());
 }
}
sealed class Wire : IDisposable {
 readonly SerialPort port; readonly Action<string> log;
 public Wire(string name,Action<string> logger) { log=logger; port=new SerialPort(name,115200) { ReadTimeout=1500, WriteTimeout=10000, Handshake=Handshake.RequestToSend }; port.Open(); log("OPEN "+name+" 115200 RTS/CTS"); }
 public void Send(byte[] data) { log("TX "+data.Length+" bytes: "+BitConverter.ToString(data.Take(32).ToArray())); for(int i=0;i<data.Length;i+=4096) port.Write(data,i,Math.Min(4096,data.Length-i)); }
 public byte[] Read(int n, bool required) { var b=new byte[n]; int k=0; var watch=System.Diagnostics.Stopwatch.StartNew(); while(k<n && watch.ElapsedMilliseconds<2500) { try { k+=port.Read(b,k,n-k); } catch(TimeoutException) { break; } } Array.Resize(ref b,k); log("RX "+k+" bytes: "+BitConverter.ToString(b.Take(64).ToArray())); if(required && k!=n) throw new IOException("応答不足: "+k+" / "+n+" bytes。機種・COMポートを確認してください。"); return b; }
 public void Dispose() { port.Dispose(); log("CLOSE"); }
}
static class Packets {
 public static byte[] A(int command,int x,int y,int ex,int ey) { return new byte[]{(byte)(x>>2),(byte)((x&3)<<6 | y>>4),(byte)((y&15)<<4 | ex>>6),(byte)((ex&63)<<2 | ey>>8),(byte)ey,(byte)command}; }
 public static byte[] Pad(byte[] b,byte fill) { int n=((b.Length+249)/250)*250; var r=Enumerable.Repeat(fill,n).ToArray(); Buffer.BlockCopy(b,0,r,0,b.Length); return r; }
 public static byte[] Escape(byte[] b) { using(var m=new MemoryStream()) { for(int i=0;i<b.Length;i+=249) { if(i>0)m.WriteByte(0); m.Write(b,i,Math.Min(249,b.Length-i)); } return m.ToArray(); } }
 public static void BE(Stream s,int value,int bytes) { for(int i=bytes-1;i>=0;i--)s.WriteByte((byte)(value>>(i*8))); }
 public static byte[] Pixels(Bitmap b,int size) { using(var m=new MemoryStream()) { for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++) { Color c=b.GetPixel(x,y); if(size==2) { int v=(c.R>>3)<<11 | (c.G>>2)<<5 | c.B>>3; m.WriteByte((byte)v);m.WriteByte((byte)(v>>8)); } else { m.WriteByte(c.B);m.WriteByte(c.G);m.WriteByte(c.R);if(size==4)m.WriteByte(255); } } return m.ToArray(); } }
 public static byte[][] PartialC(Bitmap b,int x,int y,int count,int pixelSize) {
  byte[] raw; using(var m=new MemoryStream()) { byte[] p=Pixels(b,pixelSize);for(int row=0;row<b.Height;row++) { BE(m,(y+row)*800+x,3);BE(m,b.Width,2);m.Write(p,row*b.Width*pixelSize,b.Width*pixelSize); } raw=m.ToArray(); }
  byte[] header;using(var m=new MemoryStream()) { m.Write(new byte[]{0xcc,0xef,0x69,0},0,4);BE(m,raw.Length+2,3);BE(m,0,3);BE(m,count,4);header=Pad(m.ToArray(),0); }
  return new[]{header,Pad(Escape(raw).Concat(new byte[]{0xef,0x69}).ToArray(),0)};
 }
}
sealed class Screen : IDisposable {
 readonly Wire w; readonly bool revC; int rom=87,count; readonly Action<string> log;
 public Screen(string port,bool c,Action<string> logger) { revC=c;log=logger;w=new Wire(port,logger);try { Initialize(); } catch {w.Dispose();throw;} }
 void C(byte[] command,int read) { w.Send(Packets.Pad(command,0));if(read>0)w.Read(read,true); }
 void Initialize() {
  if(!revC) { w.Send(Enumerable.Repeat((byte)69,6).ToArray());var id=w.Read(6,true);if(!id.All(v=>v==2))throw new IOException("UsbPCMonitor 5インチの応答（02×6）ではありません。");w.Send(Packets.A(109,0,0,0,0));var o=new byte[16];o[5]=121;o[6]=102;o[7]=3;o[8]=32;o[9]=1;o[10]=224;w.Send(o);w.Send(Packets.A(110,153,0,0,0)); }
  else { C(new byte[]{1,0xef,0x69,0,0,0,1,0,0,0,0xc5,0xd3},0);string id=Encoding.ASCII.GetString(w.Read(23,true));log("DISPLAY ID "+id);if(!id.StartsWith("chs_"))throw new IOException("Rev.Cの応答ではありません。");var parts=id.Trim('\0').Split('.');int v;if(parts.Length>2 && int.TryParse(new string(parts[2].TakeWhile(char.IsDigit).ToArray()),out v) && v>=80 && v<=100)rom=v;log("ROM "+rom+" partial="+(rom>88?"BGRA32":"BGR24"));C(new byte[]{0x79,0xef,0x69,0,0,0,1},0);C(new byte[]{0x96,0xef,0x69,0,0,0,1},1024);C(new byte[]{0x7b,0xef,0x69,0,0,0,1,0,0,0,102},0);C(new byte[]{0x7d,0xef,0x69,0,0,0,5,0,0,0,0x2d,0,0,0,0},0); }
 }
 public void Draw(Bitmap image,int x,int y,bool full) {
  if(x<0 || y<0 || x+image.Width>800 || y+image.Height>480)throw new ArgumentOutOfRangeException("image");
  if(!revC) { w.Send(Packets.A(197,x,y,x+image.Width-1,y+image.Height-1));w.Send(Packets.Pixels(image,2)); }
  else if(full) { C(new byte[]{0x86,0xef,0x69,0,0,0,1},0);w.Send(Enumerable.Repeat((byte)0x2c,250).ToArray());C(new byte[]{0xc8,0xef,0x69,0,0x17,0x70,0x0e,0x10},0);w.Send(Packets.Pad(Packets.Escape(Packets.Pixels(image,4)),0));w.Read(1024,true);Status(); }
  else {var p=Packets.PartialC(image,x,y,count++,rom>88?4:3);w.Send(p[0]);w.Send(p[1]);Status();}
 }
 void Status() { C(new byte[]{0xcf,0xef,0x69,0,0,0,1},1024); }
 public void Dispose() {w.Dispose();}
}
sealed class PortInfo {
 public string Name,Description,Id;public override string ToString(){return Name+"  "+Description;}
 public bool Matches(bool c) {string id=Id.ToUpperInvariant();return c ? id.Contains("VID_0525&PID_A4A7") || id.Contains("VID_1D6B&PID_0121") || id.Contains("VID_1D6B&PID_0106") || id.Contains("20080411") : id.Contains("VID_1A86&PID_5722") || id.Contains("USB35INCHIPSV2");}
 public static List<PortInfo> Scan() {var result=SerialPort.GetPortNames().OrderBy(x=>x).Select(x=>new PortInfo{Name=x,Description="シリアルポート",Id=""}).ToList();try {using(var q=new ManagementObjectSearcher("SELECT Name,PNPDeviceID FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'") )foreach(ManagementObject o in q.Get()) {string name=Convert.ToString(o["Name"]);foreach(var p in result)if(name.Contains("("+p.Name+")")) {p.Description=name;p.Id=Convert.ToString(o["PNPDeviceID"]);} }}catch{}return result;}
}
sealed class MainForm : Form {
 readonly ComboBox model=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=270},ports=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=350};
 readonly PictureBox preview=new PictureBox{Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.Black};
 readonly TextBox console=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill};
 readonly CheckBox offline=new CheckBox{Text="プレビューのみ",AutoSize=true,Checked=true};
 readonly NumericUpDown seconds=new NumericUpDown{Minimum=1,Maximum=3600,Value=10,Width=65};
 readonly Button connect=new Button{Text="接続",AutoSize=true};
 readonly List<Control> actions=new List<Control>(); readonly StreamWriter file; readonly object logLock=new object();
 Screen screen;Bitmap background;CancellationTokenSource cancel;bool busy,closing;readonly string logPath;
 public MainForm() {
  Text="USB Screen Test — 800 × 480";Size=new Size(1100,850);MinimumSize=new Size(950,750);
  string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"UsbScreenTest","Logs");Directory.CreateDirectory(dir);logPath=Path.Combine(dir,DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".log");file=new StreamWriter(logPath,false,Encoding.UTF8){AutoFlush=true};
  model.Items.AddRange(new object[]{"UsbPCMonitor 5インチ（Rev.A）","Turing / TURZX 5インチ（Rev.C）"});model.SelectedIndex=0;
  var top=new FlowLayoutPanel{Dock=DockStyle.Top,Height=115,Padding=new Padding(8),AutoScroll=true};top.Controls.AddRange(new Control[]{model,ports});
  Add(top,"COM再検出",()=>{Scan(false);return Task.FromResult(0);});Add(top,"自動選択",()=>{Scan(true);return Task.FromResult(0);});top.Controls.Add(offline);top.Controls.Add(connect);connect.Click+=async(s,e)=>await Run(Toggle);
  Add(top,"背景を送信",SendBackground);Add(top,"画像を開く",LoadImage);top.Controls.Add(seconds);Add(top,"秒 カウントダウン",Countdown);var stop=new Button{Text="停止",AutoSize=true};stop.Click+=(s,e)=>{if(cancel!=null)cancel.Cancel();};top.Controls.Add(stop);
  var logs=new Button{Text="ログフォルダー",AutoSize=true};logs.Click+=(s,e)=>System.Diagnostics.Process.Start("explorer.exe",dir);top.Controls.Add(logs);
  var split=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,SplitterDistance=430};split.Panel1.Controls.Add(preview);split.Panel2.Controls.Add(console);Controls.Add(split);Controls.Add(top);
  background=MakeBackground();ShowPreview(background);Scan(false);Log("START / "+logPath);Log("既定はプレビューのみ。実機へ送る場合はチェックを外して接続。");
  model.SelectedIndexChanged+=(s,e)=>Scan(false);
  FormClosing+=async(s,e)=>{if(closing)return;e.Cancel=true;if(cancel!=null)cancel.Cancel();if(busy){Log("処理終了後にもう一度閉じてください。");return;}closing=true;if(screen!=null)await Task.Run(()=>screen.Dispose());file.Dispose();background.Dispose();if(preview.Image!=null)preview.Image.Dispose();Close();};
 }
 void Add(Control panel,string text,Func<Task> action){var b=new Button{Text=text,AutoSize=true};b.Click+=async(s,e)=>await Run(action);panel.Controls.Add(b);actions.Add(b);}
 async Task Run(Func<Task> action){if(busy)return;busy=true;foreach(var c in actions)c.Enabled=false;connect.Enabled=false;model.Enabled=screen==null && !busy;ports.Enabled=false;offline.Enabled=false;try{await action();}catch(OperationCanceledException){Log("停止しました。");}catch(Exception ex){Log("ERROR "+ex);if(screen!=null){try{screen.Dispose();}catch{}screen=null;}MessageBox.Show(this,ex.Message,"通信エラー",MessageBoxButtons.OK,MessageBoxIcon.Error);}finally{busy=false;foreach(var c in actions)c.Enabled=true;connect.Enabled=true;connect.Text=screen==null?"接続":"切断";model.Enabled=screen==null;ports.Enabled=screen==null;offline.Enabled=screen==null;}}
 void Log(string text){string line=DateTime.Now.ToString("HH:mm:ss.fff")+" "+text;lock(logLock)file.WriteLine(line);if(IsHandleCreated && InvokeRequired)BeginInvoke(new Action(()=>Append(line)));else Append(line);}
 void Append(string line){if(console.TextLength>100000)console.Clear();console.AppendText(line+Environment.NewLine);}
 void Scan(bool auto){if(screen!=null)return;ports.Items.Clear();foreach(var p in PortInfo.Scan()){ports.Items.Add(p);Log("PORT "+p+" "+p.Id);}if(ports.Items.Count>0)ports.SelectedIndex=0;if(auto){var matches=ports.Items.Cast<PortInfo>().Where(p=>p.Matches(model.SelectedIndex==1)).ToList();if(matches.Count==1){ports.SelectedItem=matches[0];Log("AUTO "+matches[0]);}else{ports.SelectedIndex=-1;Log("自動選択候補 "+matches.Count+" 件。COMを手動選択してください（他機器へのプローブ送信は行いません）。");}}}
 async Task Toggle(){if(screen!=null){await Task.Run(()=>screen.Dispose());screen=null;return;}if(offline.Checked){Log("プレビューのみ有効。背景／カウントダウンを実行できます。");return;}var p=ports.SelectedItem as PortInfo;if(p==null)throw new IOException("COMポートを選択してください。");bool c=model.SelectedIndex==1;screen=await Task.Run(()=>new Screen(p.Name,c,Log));Log("接続完了");}
 async Task Send(Bitmap b,int x,int y,bool full){if(!offline.Checked && screen==null)throw new IOException("先に接続してください。");if(screen!=null)await Task.Run(()=>screen.Draw(b,x,y,full));Log((full?"FULL ":"PARTIAL ")+x+","+y+" "+b.Width+"x"+b.Height);}
 async Task SendBackground(){await Send(background,0,0,true);ShowPreview(background);}
 async Task LoadImage(){using(var d=new OpenFileDialog{Filter="画像|*.png;*.jpg;*.jpeg;*.bmp"})if(d.ShowDialog(this)==DialogResult.OK){using(var src=Image.FromFile(d.FileName)){var b=new Bitmap(800,480);using(var g=Graphics.FromImage(b)){g.Clear(Color.Black);float scale=Math.Min(800f/src.Width,480f/src.Height);float w=src.Width*scale,h=src.Height*scale;g.DrawImage(src,(800-w)/2,(480-h)/2,w,h);}background.Dispose();background=b;}await SendBackground();}}
 async Task Countdown(){cancel=new CancellationTokenSource();var token=cancel.Token;try{await SendBackground();int start=(int)seconds.Value;var clock=System.Diagnostics.Stopwatch.StartNew();for(int value=start;value>=0;){token.ThrowIfCancellationRequested();using(var patch=background.Clone(new Rectangle(250,160,300,160),background.PixelFormat)){using(var g=Graphics.FromImage(patch))using(var f=new Font("Segoe UI",80,FontStyle.Bold,GraphicsUnit.Pixel))using(var sf=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})g.DrawString(value.ToString("D2"),f,Brushes.White,new RectangleF(0,0,300,160),sf);await Send(patch,250,160,false);using(var frame=(Bitmap)background.Clone()){using(var g=Graphics.FromImage(frame))g.DrawImageUnscaled(patch,250,160);ShowPreview(frame);}}if(value==0)break;await Task.Delay(100,token);int next=Math.Max(0,start-(int)clock.Elapsed.TotalSeconds);while(next==value){await Task.Delay(50,token);next=Math.Max(0,start-(int)clock.Elapsed.TotalSeconds);}value=next;}}finally{cancel.Dispose();cancel=null;}}
 void ShowPreview(Bitmap b){var old=preview.Image;preview.Image=(Bitmap)b.Clone();if(old!=null)old.Dispose();}
 public static Bitmap MakeBackground(){var b=new Bitmap(800,480);using(var g=Graphics.FromImage(b))using(var fill=new LinearGradientBrush(new Rectangle(0,0,800,480),Color.FromArgb(14,25,46),Color.FromArgb(28,70,85),30f))using(var pen=new Pen(Color.FromArgb(45,100,120)))using(var font=new Font("Segoe UI",24,FontStyle.Bold)){g.FillRectangle(fill,0,0,800,480);for(int x=0;x<800;x+=40)g.DrawLine(pen,x,0,x,479);for(int y=0;y<480;y+=40)g.DrawLine(pen,0,y,799,y);g.DrawString("USB SCREEN TEST   800 x 480",font,Brushes.White,40,32);g.DrawRectangle(Pens.Cyan,250,160,299,159);g.DrawString("PARTIAL UPDATE / COUNTDOWN",new Font("Segoe UI",16),Brushes.Cyan,205,350);Brush[] colors={Brushes.Red,Brushes.Lime,Brushes.Blue,Brushes.White};for(int i=0;i<4;i++)g.FillRectangle(colors[i],40+i*180,420,170,20);g.DrawRectangle(Pens.White,0,0,799,479);}return b;}
}
static class SelfTest {
 public static void Run(){Action<bool,string> check=(ok,msg)=>{if(!ok)throw new Exception(msg);};check(BitConverter.ToString(Packets.A(197,0,0,799,479))=="00-00-0C-7D-DF-C5","RevA header");using(var b=new Bitmap(2,1)){b.SetPixel(0,0,Color.Red);b.SetPixel(1,0,Color.Blue);check(BitConverter.ToString(Packets.Pixels(b,2))=="00-F8-1F-00","565 little endian");var p=Packets.PartialC(b,250,160,1,3);check(p[0][4]==0 && p[0][5]==0 && p[0][6]==13,"C length");check(p[1][0]==1 && p[1][1]==0xf4 && p[1][2]==0xfa,"C address");check(p[1][11]==0xef && p[1][12]==0x69,"C trailer");}check(Packets.Escape(new byte[498]).Length==499,"C escape");check(Packets.Pad(new byte[250],0).Length==250,"padding");File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test-result.txt"),"PASS: RevA header, RGB565, RevC row address/length/trailer, escaping, padding");}
}
}
