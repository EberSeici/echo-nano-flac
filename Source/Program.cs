using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Echo Nano FLAC")]
[assembly: System.Reflection.AssemblyVersion("1.5.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.5.0.0")]
namespace EchoNanoFLAC {
class FlacInfo {
    public bool IsOpus;
    public byte[] StreamInfo;
    public long AudioOffset, Samples;
    public int Rate, Bits, Channels;
    public List<string> Tags = new List<string>();
}
static class Converter {
    static readonly HashSet<string> Allowed = new HashSet<string>(new [] { "TITLE", "ARTIST", "ALBUM", "ALBUMARTIST", "ALBUM ARTIST", "TRACKNUMBER", "TRACKTOTAL", "TOTALTRACKS", "DISCNUMBER", "DISCTOTAL", "TOTALDISCS", "DATE", "YEAR", "GENRE", "COMPOSER" });
    static byte[] Read(BinaryReader r, int n) { var b=r.ReadBytes(n); if(b.Length!=n) throw new IOException("Archivo FLAC incompleto."); return b; }
    public static bool Supports(string path) {
        string extension=Path.GetExtension(path);
        return extension.Equals(".flac",StringComparison.OrdinalIgnoreCase) || extension.Equals(".opus",StringComparison.OrdinalIgnoreCase);
    }
    public static FlacInfo InspectSource(string path) {
        return Path.GetExtension(path).Equals(".opus",StringComparison.OrdinalIgnoreCase)?InspectOpus(path):Inspect(path);
    }
    static FlacInfo InspectOpus(string path) {
        var packets=new List<byte[]>();
        using(var r=new BinaryReader(File.OpenRead(path))) using(var packet=new MemoryStream()) {
            uint serial=0,sequence=0; bool first=true;
            while(packets.Count<2) {
                if(r.BaseStream.Position==r.BaseStream.Length)throw new IOException("Cabecera Opus incompleta.");
                var h=Read(r,27);
                if(Encoding.ASCII.GetString(h,0,4)!="OggS" || h[4]!=0)throw new IOException("No es un archivo Ogg Opus válido.");
                uint pageSerial=BitConverter.ToUInt32(h,14),pageSequence=BitConverter.ToUInt32(h,18);
                if(first) {if((h[5]&2)==0 || (h[5]&1)!=0)throw new IOException("Falta el inicio del flujo Opus.");serial=pageSerial;sequence=pageSequence;first=false;}
                else {if(serial!=pageSerial || pageSequence!=++sequence)throw new IOException("Cabecera Opus con páginas mezcladas o incompletas.");}
                if(((h[5]&1)!=0)!=(packet.Length>0))throw new IOException("Continuación Opus inválida.");
                var sizes=Read(r,h[26]);
                foreach(byte size in sizes) {
                    var data=Read(r,size);packet.Write(data,0,data.Length);
                    if(packet.Length>16*1024*1024)throw new IOException("Metadatos Opus demasiado grandes.");
                    if(size<255) {packets.Add(packet.ToArray());packet.SetLength(0);if(packets.Count==2)break;}
                }
            }
        }
        byte[] head=packets[0],comments=packets[1];
        if(head.Length<19 || Encoding.ASCII.GetString(head,0,8)!="OpusHead" || head[8]>15)throw new IOException("Cabecera Opus no admitida.");
        if(head[9]<1 || head[9]>2 || head[18]!=0)throw new IOException("Para Echo Nano se admite Opus mono o estéreo estándar; no se reduce el número de canales automáticamente.");
        if(comments.Length<16 || Encoding.ASCII.GetString(comments,0,8)!="OpusTags")throw new IOException("Faltan las etiquetas Opus.");
        var info=new FlacInfo {IsOpus=true,Rate=48000,Bits=24,Channels=head[9],Samples=0};
        using(var t=new BinaryReader(new MemoryStream(comments),Encoding.UTF8)) {
            t.BaseStream.Position=8;uint vendor=t.ReadUInt32();if(vendor>comments.Length)throw new IOException("Etiquetas Opus inválidas.");Read(t,(int)vendor);
            uint count=t.ReadUInt32();if(count>comments.Length/4)throw new IOException("Etiquetas Opus inválidas.");
            for(uint i=0;i<count;i++) {
                uint size=t.ReadUInt32();if(size>comments.Length)throw new IOException("Etiqueta Opus demasiado grande.");
                string tag=new UTF8Encoding(false,true).GetString(Read(t,(int)size));int equals=tag.IndexOf('=');
                if(equals>0 && Allowed.Contains(tag.Substring(0,equals).ToUpperInvariant()) && tag.IndexOf('\0')<0)info.Tags.Add(tag.Substring(0,equals).ToUpperInvariant()+tag.Substring(equals));
            }
        }
        return info;
    }
    public static FlacInfo Inspect(string path) {
        var info=new FlacInfo();
        using(var r=new BinaryReader(File.OpenRead(path))) {
            if(Encoding.ASCII.GetString(Read(r,4))!="fLaC") throw new IOException("No es un FLAC nativo válido.");
            bool last=false;
            while(!last) {
                var h=Read(r,4); last=(h[0]&128)!=0; int type=h[0]&127;
                int length=(h[1]<<16)|(h[2]<<8)|h[3]; var data=Read(r,length);
                if(type==0) { if(info.StreamInfo!=null || length!=34) throw new IOException("STREAMINFO inválido."); info.StreamInfo=data; }
                if(type==4) {
                    using(var t=new BinaryReader(new MemoryStream(data),Encoding.UTF8)) {
                        uint vendor=t.ReadUInt32(); if(vendor>data.Length) throw new IOException("Etiquetas inválidas."); Read(t,(int)vendor);
                        uint count=t.ReadUInt32(); if(count>data.Length/4) throw new IOException("Etiquetas inválidas.");
                        for(uint i=0;i<count;i++) {
                            uint size=t.ReadUInt32(); if(size>data.Length) throw new IOException("Etiqueta demasiado grande.");
                            string tag=new UTF8Encoding(false,true).GetString(Read(t,(int)size)); int equals=tag.IndexOf('=');
                            if(equals>0 && Allowed.Contains(tag.Substring(0,equals).ToUpperInvariant()) && tag.IndexOf('\0')<0)
                                info.Tags.Add(tag.Substring(0,equals).ToUpperInvariant()+tag.Substring(equals));
                        }
                    }
                }
            }
            info.AudioOffset=r.BaseStream.Position;
        }
        if(info.StreamInfo==null) throw new IOException("Falta STREAMINFO.");
        ulong packed=0; for(int i=10;i<18;i++) packed=(packed<<8)|info.StreamInfo[i];
        info.Rate=(int)(packed>>44); info.Channels=(int)((packed>>41)&7)+1; info.Bits=(int)((packed>>36)&31)+1; info.Samples=(long)(packed&0xfffffffffUL);
        return info;
    }
    static string Q(string s) { return "\""+s.Replace("\"","\\\"")+"\""; }
    static string Run(string args) {
        string exe=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ffmpeg.exe");
        if(!File.Exists(exe)) throw new IOException("Falta ffmpeg.exe. Extrae todo el ZIP en una carpeta.");
        var start=new ProcessStartInfo(exe,"-hide_banner -nostdin -v error -xerror "+args) { UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true };
        using(var p=Process.Start(start)) {
            var stdout=p.StandardOutput.ReadToEndAsync(); var stderr=p.StandardError.ReadToEndAsync(); p.WaitForExit(); Task.WaitAll(stdout,stderr);
            if(p.ExitCode!=0 || !String.IsNullOrWhiteSpace(stderr.Result)) throw new IOException("FFmpeg: "+stderr.Result.Trim());
            return stdout.Result.Trim();
        }
    }
    static string Hash(string file,int bits) {
        string format=Path.GetExtension(file).Equals(".opus",StringComparison.OrdinalIgnoreCase)?" -af aformat=sample_fmts=s32 -ar 48000":"";
        return Run("-i "+Q(file)+" -map 0:a:0"+format+" -c:a pcm_s"+bits+"le -f hash -hash MD5 -");
    }
    static void Block(Stream f,int type,bool last,byte[] data) {
        if(data.Length>0xffffff) throw new IOException("Demasiados metadatos.");
        f.WriteByte((byte)(type|(last?128:0))); f.WriteByte((byte)(data.Length>>16)); f.WriteByte((byte)(data.Length>>8)); f.WriteByte((byte)data.Length); f.Write(data,0,data.Length);
    }
    static byte[] Comment(List<string> tags) {
        using(var m=new MemoryStream()) {
            using(var w=new BinaryWriter(m,Encoding.UTF8,true)) {
                byte[] vendor=Encoding.UTF8.GetBytes("Echo Nano FLAC 1.0"); w.Write((uint)vendor.Length); w.Write(vendor); w.Write((uint)tags.Count);
                foreach(string tag in tags) { var data=Encoding.UTF8.GetBytes(tag); w.Write((uint)data.Length); w.Write(data); }
            }
            return m.ToArray();
        }
    }
    public static string ConvertFile(string source,string folder,Action<string> progress,string fallbackArtist="",string fallbackAlbum="") {
        var original=InspectSource(source);
        foreach(var pair in new[]{new[]{"ARTIST=",fallbackArtist},new[]{"ALBUM=",fallbackAlbum}}) {
            if(!String.IsNullOrWhiteSpace(pair[1]) && !original.Tags.Any(t=>t.StartsWith(pair[0]) && !String.IsNullOrWhiteSpace(t.Substring(pair[0].Length)))) {
                original.Tags.RemoveAll(t=>t.StartsWith(pair[0]));original.Tags.Add(pair[0]+pair[1].Trim());
            }
        }
        if(original.Bits!=16 && original.Bits!=24 && original.Bits!=32) throw new IOException("Profundidad no admitida: "+original.Bits+" bits. No se ha modificado el original.");
        Directory.CreateDirectory(folder);
        string token=Guid.NewGuid().ToString("N"); string encoded=Path.Combine(folder,".echo-"+token+".flac"); string clean=Path.Combine(folder,".echo-"+token+"-verified.flac");
        try {
            progress(original.IsOpus?"Opus → FLAC 24 bits":"Convirtiendo FLAC");
            string format=original.IsOpus?" -af aformat=sample_fmts=s32 -ar 48000":"";
            Run("-n -i "+Q(source)+" -map 0:a:0 -map_metadata -1 -map_chapters -1"+format+" -c:a flac -compression_level 5 -frame_size 4096 -sample_fmt "+(original.Bits==16?"s16":"s32")+" -bits_per_raw_sample "+original.Bits+" "+Q(encoded));
            var converted=Inspect(encoded);
            using(var input=File.OpenRead(encoded)) using(var output=new FileStream(clean,FileMode.CreateNew)) {
                output.Write(Encoding.ASCII.GetBytes("fLaC"),0,4); Block(output,0,false,converted.StreamInfo); Block(output,4,true,Comment(original.Tags));
                input.Position=converted.AudioOffset; input.CopyTo(output);
            }
            progress("Verificando audio y etiquetas");
            var final=Inspect(clean);
            if(original.Rate!=final.Rate || original.Bits!=final.Bits || original.Channels!=final.Channels || (original.Samples!=0 && original.Samples!=final.Samples)) throw new IOException("Los parámetros del audio no coinciden.");
            if(!original.Tags.SequenceEqual(final.Tags)) throw new IOException("Las etiquetas no coinciden.");
            if(final.StreamInfo[0]!=16 || final.StreamInfo[1]!=0 || final.StreamInfo[2]!=16 || final.StreamInfo[3]!=0) throw new IOException("Bloques FLAC incorrectos.");
            string hash=Hash(clean,final.Bits);
            string embedded="MD5="+BitConverter.ToString(final.StreamInfo,18,16).Replace("-","").ToLowerInvariant();
            if(hash!=Hash(source,original.Bits) || hash!=embedded) throw new IOException("El audio decodificado no coincide con el original.");
            string framehash=Run("-i "+Q(clean)+" -map 0:a:0 -c:a pcm_s32le -f framehash -");
            var durations=framehash.Split('\n').Where(x=>x.Length>0 && !x.StartsWith("#")).Select(x=>Int64.Parse(x.Split(',')[3].Trim())).ToArray();
            if(durations.Length==0 || durations.Take(durations.Length-1).Any(x=>x!=4096) || durations.Last()<1 || durations.Last()>4096 || durations.Sum()!=final.Samples) throw new IOException("La verificación de bloques falló.");
            string name=Path.GetFileNameWithoutExtension(source)+" - Echo Nano"; string destination=Path.Combine(folder,name+".flac"); int suffix=2;
            while(File.Exists(destination)) destination=Path.Combine(folder,name+" ("+(suffix++)+").flac");
            File.Move(clean,destination);
            return destination;
        } finally { if(File.Exists(encoded)) File.Delete(encoded); if(File.Exists(clean)) File.Delete(clean); }
    }
}

class DarkProgress : Control {
    int current, maximum=100;
    public int Value {get{return current;}set{current=value;Invalidate();}}
    public int Maximum {get{return maximum;}set{maximum=Math.Max(1,value);Invalidate();}}
    public DarkProgress() {DoubleBuffered=true;}
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.Clear(BackColor);
        using(var brush=new SolidBrush(ForeColor))
            e.Graphics.FillRectangle(brush,0,0,(int)(Width*Math.Min(1.0,(double)current/maximum)),Height);
    }
}
class AppMenuRenderer : ToolStripProfessionalRenderer {
    Color background,highlight,text;
    public AppMenuRenderer(Color bg,Color selected,Color fg){background=bg;highlight=selected;text=fg;}
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){e.Graphics.Clear(background);}
    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e){using(var b=new SolidBrush(background))e.Graphics.FillRectangle(b,e.AffectedBounds);}
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e){using(var b=new SolidBrush(e.Item.Selected?highlight:background))e.Graphics.FillRectangle(b,new Rectangle(Point.Empty,e.Item.Size));}
    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e){e.ArrowColor=text;base.OnRenderArrow(e);}
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e){using(var p=new Pen(highlight))e.Graphics.DrawLine(p,4,e.Item.Height/2,e.Item.Width-4,e.Item.Height/2);}
}
class MainForm : Form {
    MenuStrip menu=new MenuStrip(); string lastLog="";
    ListView list=new ListView(); TextBox output=new TextBox(); Label status=new Label(); DarkProgress bar=new DarkProgress();
    TextBox artist=new TextBox(), album=new TextBox();
    Button add=new Button(), folder=new Button(), clear=new Button(), browse=new Button(), start=new Button(), stop=new Button(), open=new Button();
    bool busy, stopRequested;
    ComboBox themePicker=new ComboBox(), materialPicker=new ComboBox();
    bool dark, materialActive, appearanceReady;
    Color surface, raised, muted, alternate, selection;
    Label appearanceNote=new Label();
    string settingsPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"EchoNanoFLAC","EchoNanoFLAC.settings");
    HashSet<string> paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public MainForm() {
        Text="Echo Nano · Conversor FLAC"; ClientSize=new Size(920,620); MinimumSize=new Size(920,620); StartPosition=FormStartPosition.CenterScreen; Font=new Font("Segoe UI",10); BackColor=Color.FromArgb(23,27,34); ForeColor=Color.FromArgb(234,239,247); AutoScaleMode=AutoScaleMode.Dpi;
        var title=new Label {Text="Tu música lista para Echo Nano",Font=new Font("Segoe UI",20,FontStyle.Bold),AutoSize=true,Location=new Point(24,20)}; Controls.Add(title);
        var desc=new Label {Text="FLAC sin pérdida · Opus → FLAC (no recupera calidad perdida) · Conserva artista y álbum",AutoSize=true,Location=new Point(26,66)}; Controls.Add(desc);
        Setup(add,"Añadir archivos",24,106,155); Setup(folder,"Añadir carpeta",190,106,155); Setup(clear,"Vaciar lista",356,106,130);
        list.Location=new Point(24,154); list.Size=new Size(872,208); list.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right; list.View=View.Details; list.FullRowSelect=true; list.GridLines=false;
        list.Columns.Add("Archivo",340); list.Columns.Add("Artista / Álbum",295); list.Columns.Add("Estado",215); Controls.Add(list); list.AllowDrop=true;
        list.DragEnter+=(s,e)=> { if(!busy && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect=DragDropEffects.Copy; };
        list.DragDrop+=(s,e)=>AddPaths((string[])e.Data.GetData(DataFormats.FileDrop));
        Controls.Add(new Label {Text="Artista si falta (opcional):",AutoSize=true,Location=new Point(24,370),Anchor=AnchorStyles.Bottom|AnchorStyles.Left});
        artist.SetBounds(24,394,410,28);artist.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;Controls.Add(artist);
        Controls.Add(new Label {Text="Álbum si falta (opcional):",AutoSize=true,Location=new Point(454,370),Anchor=AnchorStyles.Bottom|AnchorStyles.Left});
        album.SetBounds(454,394,442,28);album.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(album);
        var label=new Label {Text="Guardar copias en:",AutoSize=true,Location=new Point(24,436),Anchor=AnchorStyles.Bottom|AnchorStyles.Left}; Controls.Add(label);
        output.SetBounds(24,461,700,30); output.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right; output.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"FLAC - Echo Nano"); Controls.Add(output);
        Setup(browse,"Elegir…",742,458,154); browse.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;
        Setup(start,"Convertir y verificar",24,510,195); start.BackColor=Color.FromArgb(33,91,201); start.ForeColor=Color.White;
        Setup(stop,"Detener después del archivo",231,510,260); stop.Enabled=false; Setup(open,"Abrir resultados",704,510,192); open.Anchor=AnchorStyles.Bottom|AnchorStyles.Right;
        start.Anchor=stop.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;
        bar.SetBounds(24,563,872,10); bar.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right; Controls.Add(bar);
        status.SetBounds(24,582,872,25); status.Anchor=AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right; status.Text="Añade FLAC u Opus. Se guardan copias FLAC verificadas; los originales se conservan."; Controls.Add(status);
        add.Click+=(s,e)=> { using(var d=new OpenFileDialog {Filter="Audio compatible|*.flac;*.opus|Audio FLAC|*.flac|Audio Opus|*.opus",Multiselect=true}) if(d.ShowDialog()==DialogResult.OK) AddPaths(d.FileNames); };
        folder.Click+=(s,e)=> { using(var d=new FolderBrowserDialog()) if(d.ShowDialog()==DialogResult.OK) AddPaths(new[]{d.SelectedPath}); };
        clear.Click+=(s,e)=> { list.Items.Clear(); paths.Clear(); status.Text="Lista vacía."; };
        browse.Click+=(s,e)=> { using(var d=new FolderBrowserDialog()) if(d.ShowDialog()==DialogResult.OK) output.Text=d.SelectedPath; };
        start.Click+=async(s,e)=>await ConvertAll(); stop.Click+=(s,e)=> {stopRequested=true;stop.Enabled=false;status.Text="Se detendrá al terminar y verificar el archivo actual.";};
        open.Click+=(s,e)=> {try { if(Directory.Exists(output.Text)) Process.Start("explorer.exe", "\""+output.Text+"\""); } catch(Exception ex) {MessageBox.Show(ex.Message);} };
        FormClosing+=(s,e)=> { if(busy) { e.Cancel=true; MessageBox.Show("Pulsa Detener y espera a que termine el archivo actual antes de cerrar."); } };
        themePicker.SetBounds(76,106,160,30);themePicker.DropDownStyle=ComboBoxStyle.DropDownList;themePicker.Items.AddRange(new object[]{"Sistema","Claro","Oscuro"});
        materialPicker.SetBounds(318,106,160,30);materialPicker.DropDownStyle=ComboBoxStyle.DropDownList;materialPicker.Items.AddRange(new object[]{"Mica","Acrílico","Sólido"});
        themePicker.SelectedIndex=0;materialPicker.SelectedIndex=0;
        try {if(File.Exists(settingsPath)) {var values=File.ReadAllLines(settingsPath);int v;if(values.Length>0 && Int32.TryParse(values[0],out v) && v>=0 && v<=2)themePicker.SelectedIndex=v;if(values.Length>1 && Int32.TryParse(values[1],out v) && v>=0 && v<=2)materialPicker.SelectedIndex=v;}} catch(IOException) {} catch(UnauthorizedAccessException) {}
        themePicker.SelectedIndexChanged+=(sender,args)=> {ApplyTheme();SaveAppearance();};
        materialPicker.SelectedIndexChanged+=(sender,args)=> {ApplyTheme();SaveAppearance();};
        foreach(Control c in Controls) {var button=c as Button;if(button!=null)button.Paint+=(sender,args)=> {
            var b=(Button)sender;if(b.Enabled)return;
            using(var brush=new SolidBrush(b.BackColor))args.Graphics.FillRectangle(brush,1,1,b.Width-2,b.Height-2);
            TextRenderer.DrawText(args.Graphics,b.Text,b.Font,new Rectangle(2,2,b.Width-4,b.Height-4),muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        };}
        list.SizeChanged+=(sender,args)=> {list.Columns[2].Width=Math.Max(150,list.ClientSize.Width-list.Columns[0].Width-list.Columns[1].Width);};
        list.Columns[2].Width=Math.Max(150,list.ClientSize.Width-list.Columns[0].Width-list.Columns[1].Width);
        ConfigureListDrawing();BuildMenus();appearanceReady=true;ApplyTheme();
        Microsoft.Win32.SystemEvents.UserPreferenceChanged+=SystemPreferenceChanged;
        FormClosed+=(sender,args)=>Microsoft.Win32.SystemEvents.UserPreferenceChanged-=SystemPreferenceChanged;
    }
    ToolStripMenuItem MenuAction(ToolStripMenuItem parent,string text,Action action,Keys shortcut=Keys.None) {
        var item=new ToolStripMenuItem(text);item.ShortcutKeys=shortcut;item.Click+=(s,e)=>action();parent.DropDownItems.Add(item);return item;
    }
    void RemoveSelected() {
        if(busy)return;
        foreach(ListViewItem item in list.SelectedItems.Cast<ListViewItem>().ToArray()) {paths.Remove((string)item.Tag);list.Items.Remove(item);}
        status.Text=list.Items.Count+" archivos en la lista. Los archivos originales no se borran.";
    }
    void BuildMenus() {
        // Add a native menu row without changing the established content layout.
        var anchors=Controls.Cast<Control>().ToDictionary(c=>c,c=>c.Anchor);
        foreach(Control c in Controls)c.Anchor=AnchorStyles.Top|AnchorStyles.Left;
        ClientSize=new Size(ClientSize.Width,ClientSize.Height+28);MinimumSize=new Size(MinimumSize.Width,MinimumSize.Height+28);
        foreach(Control c in Controls){c.Top+=28;c.Anchor=anchors[c];}
        ToolStripMenuItem file=new ToolStripMenuItem("&Archivo"),edit=new ToolStripMenuItem("&Edición"),view=new ToolStripMenuItem("&Ver"),help=new ToolStripMenuItem("A&yuda");
        menu.Items.AddRange(new ToolStripItem[]{file,edit,view,help});menu.Dock=DockStyle.Top;MainMenuStrip=menu;Controls.Add(menu);menu.BringToFront();
        MenuAction(file,"Añadir archivos…",()=>add.PerformClick(),Keys.Control|Keys.O);
        MenuAction(file,"Añadir carpeta…",()=>folder.PerformClick(),Keys.Control|Keys.Shift|Keys.O);
        MenuAction(file,"Elegir carpeta de salida…",()=>browse.PerformClick());file.DropDownItems.Add(new ToolStripSeparator());
        MenuAction(file,"Convertir pendientes",()=>start.PerformClick(),Keys.Control|Keys.Enter);
        MenuAction(file,"Detener después del archivo actual",()=>stop.PerformClick());
        MenuAction(file,"Abrir resultados",()=>open.PerformClick());
        MenuAction(file,"Abrir último registro",()=> {if(File.Exists(lastLog)) {try{Process.Start("notepad.exe","\""+lastLog+"\"");}catch(Exception ex){MessageBox.Show(ex.Message);}}else MessageBox.Show("El registro estará disponible después de una conversión.");});
        file.DropDownItems.Add(new ToolStripSeparator());MenuAction(file,"Salir",()=>Close());
        MenuAction(edit,"Seleccionar todo",()=> {var input=ActiveControl as TextBox;if(input!=null){input.SelectAll();return;}list.Focus();foreach(ListViewItem item in list.Items)item.Selected=true;},Keys.Control|Keys.A);
        MenuAction(edit,"Quitar seleccionados de la lista",RemoveSelected);
        MenuAction(edit,"Vaciar lista",()=>clear.PerformClick());
        MenuAction(edit,"Restablecer estados para reconvertir",()=> {if(busy)return;foreach(ListViewItem item in list.Items)item.SubItems[2].Text="Pendiente";status.Text="La siguiente conversión creará nuevas copias, sin sobrescribir las anteriores.";});
        edit.DropDownOpening+=(s,e)=> {foreach(ToolStripItem item in edit.DropDownItems)item.Enabled=!busy;};
        list.KeyDown+=(s,e)=> {if(e.KeyCode==Keys.Delete){RemoveSelected();e.Handled=true;}};
        ToolStripMenuItem themes=new ToolStripMenuItem("Tema"),materials=new ToolStripMenuItem("Fondo de Windows");view.DropDownItems.Add(themes);view.DropDownItems.Add(materials);
        for(int i=0;i<3;i++) {int index=i;MenuAction(themes,themePicker.Items[i].ToString(),()=>themePicker.SelectedIndex=index);MenuAction(materials,materialPicker.Items[i].ToString(),()=>materialPicker.SelectedIndex=index);}
        MenuAction(view,"Estado del efecto de Windows",()=>MessageBox.Show(appearanceNote.Text,"Apariencia"));
        view.DropDownOpening+=(s,e)=> {for(int i=0;i<3;i++){((ToolStripMenuItem)themes.DropDownItems[i]).Checked=themePicker.SelectedIndex==i;((ToolStripMenuItem)materials.DropDownItems[i]).Checked=materialPicker.SelectedIndex==i;}};
        MenuAction(help,"Cómo preparar música para el Nano",()=>MessageBox.Show("1. Añade FLAC u Opus, o una carpeta completa.\n2. Revisa artista y álbum. Los campos opcionales solo completan datos faltantes: usa un lote por álbum.\n3. Elige la salida y convierte. Solo se entregan copias verificadas.\n4. Copia los FLAC al Nano y actualiza su biblioteca.\n\nFLAC conserva su audio original. Opus se decodifica a 48 kHz/24 bits: no recupera calidad perdida y ocupa más espacio.\nEsta herramienta prepara archivos ya extraídos; no extrae discos CD.","Guía rápida"));
        MenuAction(help,"Acerca de Echo Nano FLAC",()=>MessageBox.Show("Echo Nano FLAC 1.5\nConversión FLAC y Opus → FLAC.\nBloques de 4096, etiquetas y verificación de audio.\nMotor de audio: FFmpeg (GPL v3 o posterior).\nCódigo propio: licencia MIT.\nDesarrollado con ayuda de ChatGPT/Codex de OpenAI.\n\nNo es una aplicación oficial de OpenAI, FiiO o SNOWSKY.","Acerca de"));
        try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
    }
    void ColorMenus(ToolStripItemCollection items) {
        foreach(ToolStripItem item in items) {item.ForeColor=ForeColor;item.BackColor=raised;var sub=item as ToolStripMenuItem;if(sub!=null) {sub.DropDown.BackColor=raised;sub.DropDown.ForeColor=ForeColor;ColorMenus(sub.DropDownItems);}}
    }
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr handle,int attribute,ref int value,int size);
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct Margins {public int Left,Right,Top,Bottom;}
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    static extern int DwmExtendFrameIntoClientArea(IntPtr handle,ref Margins margins);
    protected override void OnHandleCreated(EventArgs e) {base.OnHandleCreated(e);if(appearanceReady)ApplyMaterial();}
    protected override void OnPaintBackground(PaintEventArgs e) {
        if(!materialActive) {base.OnPaintBackground(e);return;}
        // Leave the native backdrop exposed around an opaque, readable content surface.
        e.Graphics.Clear(Color.Black);
        using(var brush=new SolidBrush(BackColor))e.Graphics.FillRectangle(brush,12,12,Math.Max(0,ClientSize.Width-24),Math.Max(0,ClientSize.Height-24));
    }
    static int Personalization(string name,int fallback) {
        try {using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))return key==null?fallback:Convert.ToInt32(key.GetValue(name,fallback));}catch {return fallback;}
    }
    void SystemPreferenceChanged(object sender,Microsoft.Win32.UserPreferenceChangedEventArgs e) {
        if(IsDisposed || !IsHandleCreated)return;
        try {BeginInvoke(new Action(()=> {if(!IsDisposed)ApplyTheme();}));}catch(InvalidOperationException) {}
    }
    void SaveAppearance() {
        try {Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));File.WriteAllLines(settingsPath,new[]{themePicker.SelectedIndex.ToString(),materialPicker.SelectedIndex.ToString()});}
        catch(IOException) {status.Text="No se pudo guardar la preferencia.";}
        catch(UnauthorizedAccessException) {status.Text="No se pudieron guardar las preferencias del usuario.";}
    }
    void ApplyMaterial() {
        if(!IsHandleCreated)return;
        materialActive=false;
        bool allowed=materialPicker.SelectedIndex!=2 && !SystemInformation.HighContrast && Personalization("EnableTransparency",1)!=0 && !SystemInformation.TerminalServerSession;
        try {
            int enabled=dark?1:0;if(DwmSetWindowAttribute(Handle,20,ref enabled,4)!=0)DwmSetWindowAttribute(Handle,19,ref enabled,4);
            int rounded=2;DwmSetWindowAttribute(Handle,33,ref rounded,4);
            int backdrop=allowed?(materialPicker.SelectedIndex==1?3:2):1;
            int result=DwmSetWindowAttribute(Handle,38,ref backdrop,4);
            var margins=new Margins {Left=allowed && result==0?-1:0};
            int extended=DwmExtendFrameIntoClientArea(Handle,ref margins);
            materialActive=allowed && result==0 && extended==0;
            if(!materialActive) {backdrop=1;DwmSetWindowAttribute(Handle,38,ref backdrop,4);margins=new Margins();DwmExtendFrameIntoClientArea(Handle,ref margins);}
        }catch(DllNotFoundException) {}catch(EntryPointNotFoundException) {}
        appearanceNote.Text=materialActive?"Efecto nativo en el marco y los márgenes.":(materialPicker.SelectedIndex==2?"Fondo sólido.":"Windows usa el fondo sólido de respaldo.");
        Invalidate(true);
    }
    void ApplyTheme() {
        dark=themePicker.SelectedIndex==2 || (themePicker.SelectedIndex==0 && Personalization("AppsUseLightTheme",1)==0);
        bool contrast=SystemInformation.HighContrast;
        BackColor=contrast?SystemColors.Window:(dark?Color.FromArgb(23,27,34):Color.FromArgb(246,248,251));
        ForeColor=contrast?SystemColors.WindowText:(dark?Color.FromArgb(234,239,247):Color.FromArgb(27,34,46));
        surface=contrast?SystemColors.Window:(dark?Color.FromArgb(29,34,43):Color.White);
        raised=contrast?SystemColors.Control:(dark?Color.FromArgb(42,50,64):Color.FromArgb(230,236,244));
        muted=contrast?SystemColors.WindowText:(dark?Color.FromArgb(192,202,217):Color.FromArgb(66,79,97));
        alternate=dark?Color.FromArgb(33,39,49):Color.FromArgb(246,248,252);
        selection=contrast?SystemColors.Highlight:(dark?Color.FromArgb(43,70,108):Color.FromArgb(212,230,253));
        foreach(Control control in Controls) {
            if(control is Label) {control.ForeColor=muted;control.BackColor=BackColor;}
            var input=control as TextBox;
            if(input!=null) {input.BackColor=surface;input.ForeColor=ForeColor;input.BorderStyle=BorderStyle.FixedSingle;}
            var combo=control as ComboBox;
            if(combo!=null) {combo.BackColor=surface;combo.ForeColor=ForeColor;combo.FlatStyle=FlatStyle.Flat;}
            var button=control as Button;
            if(button!=null) {
                button.UseVisualStyleBackColor=false;button.BackColor=raised;button.ForeColor=contrast?SystemColors.ControlText:ForeColor;
                button.FlatAppearance.BorderColor=dark?Color.FromArgb(75,88,108):Color.FromArgb(179,190,206);
                button.FlatAppearance.MouseOverBackColor=dark?Color.FromArgb(57,70,91):Color.FromArgb(210,223,241);
                button.FlatAppearance.MouseDownBackColor=selection;
            }
        }
        menu.BackColor=raised;menu.ForeColor=ForeColor;menu.Renderer=new AppMenuRenderer(raised,selection,ForeColor);ColorMenus(menu.Items);
        foreach(Control control in Controls)if(control is Label && control.Font.Size>=18)control.ForeColor=ForeColor;
        start.BackColor=contrast?SystemColors.Highlight:Color.FromArgb(38,101,211);start.ForeColor=contrast?SystemColors.HighlightText:Color.White;start.FlatAppearance.BorderColor=start.BackColor;
        start.FlatAppearance.MouseOverBackColor=Color.FromArgb(48,119,234);start.FlatAppearance.MouseDownBackColor=Color.FromArgb(28,79,176);
        list.BackColor=surface;list.ForeColor=ForeColor;list.BorderStyle=BorderStyle.FixedSingle;list.HideSelection=false;
        bar.BackColor=raised;bar.ForeColor=contrast?SystemColors.Highlight:Color.FromArgb(66,139,238);
        ApplyMaterial();Invalidate(true);
    }
    void ConfigureListDrawing() {
        list.OwnerDraw=true;
        list.DrawColumnHeader+=(s,e)=> {
            using(var brush=new SolidBrush(raised))e.Graphics.FillRectangle(brush,e.Bounds);
            TextRenderer.DrawText(e.Graphics,e.Header.Text,list.Font,Rectangle.Inflate(e.Bounds,-8,0),ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        };
        list.DrawItem+=(s,e)=> {if(list.View!=View.Details)e.DrawDefault=true;};
        list.DrawSubItem+=(s,e)=> {
            Color background=e.Item.Selected?selection:(e.ItemIndex%2==0?surface:alternate);
            using(var brush=new SolidBrush(background))e.Graphics.FillRectangle(brush,e.Bounds);
            Color text=list.ForeColor;
            if(e.ColumnIndex==2 && e.SubItem.Text.StartsWith("Verificado"))text=dark?Color.FromArgb(127,224,177):Color.FromArgb(20,107,65);
            if(e.ColumnIndex==2 && e.SubItem.Text.StartsWith("Error"))text=dark?Color.FromArgb(255,162,162):Color.FromArgb(166,35,35);
            if(SystemInformation.HighContrast)text=e.Item.Selected?SystemColors.HighlightText:SystemColors.WindowText;
            TextRenderer.DrawText(e.Graphics,e.SubItem.Text,list.Font,Rectangle.Inflate(e.Bounds,-8,0),text,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        };
    }
    void Setup(Button b,string text,int x,int y,int width) {b.Text=text;b.SetBounds(x,y,width,36);b.FlatStyle=FlatStyle.Flat;Controls.Add(b);}
    void AddPaths(IEnumerable<string> selected) {
        if(busy) return;
        foreach(string path in selected) {
            try {
                if(Directory.Exists(path)) {AddPaths(Directory.GetFiles(path,"*",SearchOption.AllDirectories).Where(Converter.Supports));continue;}
                if(!File.Exists(path) || !Converter.Supports(path) || !paths.Add(Path.GetFullPath(path))) continue;
                var item=new ListViewItem(Path.GetFileName(path)); item.Tag=Path.GetFullPath(path);
                try {var info=Converter.InspectSource(path); string artist=info.Tags.FirstOrDefault(t=>t.StartsWith("ARTIST="));string album=info.Tags.FirstOrDefault(t=>t.StartsWith("ALBUM="));item.SubItems.Add((artist==null?"Sin artista":artist.Substring(7))+" / "+(album==null?"Sin álbum":album.Substring(6)));item.SubItems.Add(info.IsOpus?"Opus → FLAC 24 bits":"FLAC · "+info.Bits+" bits");}
                catch(Exception ex) {item.SubItems.Add("");item.SubItems.Add("Revisar: "+ex.Message);}
                list.Items.Add(item);
            } catch(Exception ex) {MessageBox.Show("No se pudo leer "+path+"\n"+ex.Message);}
        }
        status.Text=list.Items.Count+" archivos. Se conservan las etiquetas que existan en el original.";
    }
    async Task ConvertAll() {
        if(list.Items.Count==0) {MessageBox.Show("Añade al menos un archivo FLAC u Opus.");return;}
        var pending=list.Items.Cast<ListViewItem>().Where(item=>!item.SubItems[2].Text.StartsWith("Verificado")).ToArray();
        if(pending.Length==0){MessageBox.Show("Todos los archivos de la lista ya están verificados. Añade más canciones o usa Edición → Restablecer estados para reconvertir.");return;}
        string target;
        try {target=Path.GetFullPath(output.Text);Directory.CreateDirectory(target);} catch(Exception ex) {MessageBox.Show("No se puede usar esa carpeta: "+ex.Message);return;}
        string fallbackArtist=artist.Text, fallbackAlbum=album.Text;
        busy=true;stopRequested=false;add.Enabled=folder.Enabled=clear.Enabled=browse.Enabled=start.Enabled=output.Enabled=artist.Enabled=album.Enabled=false;stop.Enabled=true;
        int ok=0,errors=0;bar.Value=0;bar.Maximum=pending.Length;var log=new StringBuilder("Echo Nano FLAC — "+DateTime.Now+Environment.NewLine);
        try {
            foreach(ListViewItem item in pending) {
                if(stopRequested) break;
                var progress=new Progress<string>(message=> {item.SubItems[2].Text=message;status.Text=message+": "+item.Text;});
                try {
                    string src=(string)item.Tag;
                    string result=await Task.Run(()=>Converter.ConvertFile(src,target,message=>((IProgress<string>)progress).Report(message),fallbackArtist,fallbackAlbum));
                    item.SubItems[2].Text="Verificado ✓";ok++;log.AppendLine("OK | "+src+" | "+result+(Path.GetExtension(src).Equals(".opus",StringComparison.OrdinalIgnoreCase)?" | Opus decodificado a PCM de 48 kHz/24 bits y verificado; no recupera la calidad perdida.":" | Audio FLAC idéntico al original."));
                } catch(Exception ex) {errors++;item.SubItems[2].Text="Error (ver registro)";log.AppendLine("ERROR | "+item.Tag+" | "+ex.Message);}
                bar.Value++;
            }
            string summary=ok+" verificados · "+errors+" errores · "+(pending.Length-bar.Value)+" pendientes";
            log.AppendLine(summary);
            try {lastLog=Path.Combine(target,"Verificacion-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".txt");File.WriteAllText(lastLog,log.ToString(),Encoding.UTF8);} catch(Exception ex) {MessageBox.Show("No se pudo guardar el registro: "+ex.Message);}
            status.Text=summary;MessageBox.Show(summary,"Conversión terminada");
        } finally {busy=false;add.Enabled=folder.Enabled=clear.Enabled=browse.Enabled=start.Enabled=output.Enabled=artist.Enabled=album.Enabled=true;stop.Enabled=false;}
    }
}
static class Program {
    [STAThread] static int Main(string[] args) {
        if((args.Length==3 || args.Length==5) && args[0]=="--convert") {
            try {string result=Converter.ConvertFile(Path.GetFullPath(args[1]),Path.GetFullPath(args[2]),s=>{},args.Length==5?args[3]:"",args.Length==5?args[4]:"");File.WriteAllText(Path.Combine(args[2],"cli-result.txt"),result);return 0;}
            catch(Exception ex) {File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"cli-error.txt"),ex.ToString());return 1;}
        }
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new MainForm());return 0;
    }
}
}
