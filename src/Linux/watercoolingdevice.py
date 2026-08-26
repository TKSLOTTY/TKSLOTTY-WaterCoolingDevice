#!/usr/bin/env python3
import json, time, struct, threading
from pathlib import Path
try:
    import hid
except ImportError:
    print("python3-hid が必要です: sudo apt install python3-hid")
    raise
import gi
gi.require_version("Gtk","3.0"); gi.require_version("Gdk","3.0")
from gi.repository import Gtk,Gdk,GLib
try:
    gi.require_version("AyatanaAppIndicator3","0.1")
    from gi.repository import AyatanaAppIndicator3 as AppIndicator3
except Exception:
    try:
        gi.require_version("AppIndicator3","0.1")
        from gi.repository import AppIndicator3
    except Exception:
        AppIndicator3=None

VERSION="0.2.3"; VID=0x2E8A; PID=0x1144; REPORT_ID=7; RESPONSE_FLAG=0x80; INTERFACE=4; REPORT_SIZE=64; TIMEOUT_MS=1500
CMD_GET_FAN_RPM=0x30; CMD_GET_DUTY=0x31; CMD_GET_WATER_TEMP=0x32
CMD_SET_DUTY1_TABLE=0x41; CMD_SET_DUTY2_TABLE=0x42; CMD_SET_WARNING_TEMP=0x44; CMD_GET_DUTY1_TABLE=0x45; CMD_GET_DUTY2_TABLE=0x46; CMD_GET_WARNING_TEMP=0x47
CMD_SET_PUMP_MODE=0x48; CMD_SET_PUMP_DUTY=0x49; CMD_GET_PUMP_STATUS=0x4A; CMD_GET_SENSOR_STATUS=0x4B; CMD_GET_SETTINGS_VERSION=0x4C
CMD_SET_LED_COUNT=0x4D; CMD_GET_LED_COUNT=0x4E; CMD_APPLY_LED_CONFIG=0x4F; CMD_SET_LED_LAYOUT=0x59; CMD_GET_LED_LAYOUT=0x5A; APPLY_LED_CONFIG_MAGIC=0x0044454C
CONFIG_DIR=Path.home()/".config"/"WaterCoolingDevice"; CONFIG_FILE=CONFIG_DIR/"linux-settings.json"; AUTOSTART_DIR=Path.home()/".config"/"autostart"; AUTOSTART_FILE=AUTOSTART_DIR/"watercoolingdevice.desktop"; APP_DIR=Path(__file__).resolve().parent
CSS=b'''window,notebook,.background{background:#1e2127;color:#ddd}notebook>header{background:#252932}notebook>header tabs tab{padding:9px 18px;color:#cfcfcf}notebook>header tabs tab:checked{background:#343a46;color:white}button{background:#343a46;color:#eee;border:1px solid #555b66;border-radius:4px;padding:5px 10px}button:hover{background:#414856}entry,spinbutton,combobox button{background:#292d35;color:#eee}frame{border:1px solid #444a55;border-radius:5px}.value{color:#f2f2f2}.dim{color:#b8b8b8}.warning{color:#ffad42}.connected{color:#8be28b}.disconnected{color:#ff8f8f}.failsafe{background:#a52a2a;color:white;padding:9px}.pumpfault{background:#c77700;color:white;padding:9px}'''
JP_EN={
"状態表示":"Status","ファン出力編集":"Fan Output Settings","設定":"Settings","再接続":"Reconnect","水温":"Coolant Temperature","ファン1 出力":"Fan 1 Output","RPM 1":"Fan 1 Speed","ファン2 出力":"Fan 2 Output","RPM 2":"Fan 2 Speed","PUMP 出力":"PUMP Output","PUMP RPM":"PUMP Speed","水温ポイント":"Temperature Point","本体へ保存":"Save to Device","再読込":"Reload","初期値に戻す":"Restore Defaults","ファン1 → ファン2へコピー":"Copy Fan 1 to Fan 2","警告温度:":"Warning Temperature:","表示言語":"Display Language","自動起動":"Startup","温度表示":"Temperature Display","警告音":"Warning Sound","ウィンドウ表示":"Window","常に手前に表示":"Always on Top","Ubuntu起動時に起動":"Start with Ubuntu","警告温度でBEEPを鳴らす":"Beep at Warning Temperature","PUMP設定":"PUMP Settings","FAN2をPUMPとして使用（実験的機能）":"Use FAN2 as PUMP (experimental)","ARGB LED構成":"ARGB LED Layout","LED数:":"LED Count:","配置:":"Layout:","ファン（円形）":"ARGB Fan Ring","LEDテープ（直線）":"LED Strip","マトリックス":"Matrix","横:":"Width:","縦:":"Height:","ジグザグ配線":"Serpentine Wiring","LED構成を本体へ保存・適用":"Save and Apply LED Layout","警告温度の色を選択":"Choose Warning Color","20℃以下の色を選択":"Choose Low Temperature Color","最小化すると通知領域に現在水温と温度色を表示します。":"When minimized, the notification area shows coolant temperature.",
"接続デバイス:":"Device:","● 未接続":"● Disconnected","● 未接続 — 再接続待機中":"● Disconnected — waiting to reconnect","● 水冷ファンコントローラ 接続済み":"● WaterCoolingDevice connected","センサーエラー - フェイルセーフ作動中（ファン出力 100%）":"Sensor error - failsafe active (fan output 100%)","ポンプエラー - フェイルセーフ作動中（ポンプ出力 100%）":"Pump error - failsafe active (pump output 100%)","ファンカーブ  —  X: 水温 20～60°C / Y: ファン出力 0～100%":"Fan curve — X: coolant 20–60°C / Y: fan output 0–100%","マウス編集: 左ドラッグ＝FAN1 / 右ドラッグ＝FAN2":"Mouse edit: left drag = FAN1 / right drag = FAN2","設定は本体受信後500msで保存されます。":"Settings are saved 500 ms after the device receives them.","実験的機能／対応ポンプでのみ使用／設定によってはポンプ停止の可能性":"Experimental feature / use only with compatible pumps / incorrect settings may stop the pump","%（最小35%）":"% (minimum 35%)","WaterCoolingDeviceを開く":"Open WaterCoolingDevice","終了":"Quit","センサー":"Sensor","故障":"Fault",
"温度センサー異常":"Temperature Sensor Error","温度センサー異常のためフェイルセーフが発動しています。（出力100%）":"The temperature sensor has failed. Failsafe is active (output 100%).","安全のためファン出力を100%に固定しています。センサーと配線を確認してください。":"For safety, fan output is fixed at 100%. Check the sensor and wiring."
}
def load_settings():
    d={"warning_temp":40,"pump_mode":False,"pump_duty":60,"selected_serial":"","english":False,"always_on_top":False,"beep_on_warning":False,"low_color":"#2d7fff","warning_color":"#ff3b30"}
    try:
        if CONFIG_FILE.exists(): d.update(json.loads(CONFIG_FILE.read_text(encoding="utf-8")))
    except Exception: pass
    return d
def save_settings(d):
    CONFIG_DIR.mkdir(parents=True,exist_ok=True); t=CONFIG_FILE.with_suffix('.tmp'); t.write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf-8'); t.replace(CONFIG_FILE)
class DeviceInfo:
    def __init__(self,r): self.path=r['path']; self.serial=r.get('serial_number') or ''; self.product=r.get('product_string') or 'WaterCoolingDeviceV1.0'
    @property
    def label(self): return f"{self.product} — {self.serial or self.path}"
class HidClient:
    def __init__(self): self.dev=None; self.info=None; self.lock=threading.Lock()
    @staticmethod
    def enumerate_devices(): return [DeviceInfo(x) for x in hid.enumerate(VID,PID) if x.get('interface_number')==INTERFACE]
    @property
    def connected(self): return self.dev is not None
    def connect(self,info):
        with self.lock:
            self._close(); d=hid.device(); d.open_path(info.path); self.dev=d; self.info=info
    def close(self):
        with self.lock: self._close()
    def _close(self):
        if self.dev:
            try:self.dev.close()
            except Exception:pass
        self.dev=None; self.info=None
    def query(self,cmd,ch=0,value=0):
        with self.lock:
            if not self.dev: raise OSError('WaterCoolingDevice 未接続')
            r=bytearray(REPORT_SIZE); r[0]=REPORT_ID; r[1]=cmd; r[2]=ch; struct.pack_into('<i',r,3,int(value));
            if self.dev.write(r)<=0: raise OSError('HID write failed')
            a=self.dev.read(REPORT_SIZE,TIMEOUT_MS)
            if not a: raise TimeoutError('Picoから応答がありません')
            d=bytes(a); exp=RESPONSE_FLAG|cmd
            if len(d)>=7 and d[0]==REPORT_ID and d[1]==exp and d[2]==ch:return struct.unpack_from('<i',d,3)[0]
            if len(d)>=6 and d[0]==exp and d[1]==ch:return struct.unpack_from('<i',d,2)[0]
            raise OSError('Unexpected HID response: '+d[:12].hex(' '))
    def read_fan_table(self,fan):
        c=CMD_GET_DUTY1_TABLE if fan==1 else CMD_GET_DUTY2_TABLE; return [self.query(c,i) for i in range(5)]
    def write_fan_table(self,fan,vals):
        c=CMD_SET_DUTY1_TABLE if fan==1 else CMD_SET_DUTY2_TABLE
        for i,v in enumerate(vals):
            if self.query(c,i,v)!=v: raise OSError('Duty table readback mismatch')
    def configure_pump(self,en,duty):
        duty=max(35,min(100,int(duty)))
        if self.query(CMD_SET_PUMP_DUTY,0,duty)!=duty:raise OSError('Pump duty mismatch')
        mode=1 if en else 0
        if self.query(CMD_SET_PUMP_MODE,0,mode)!=mode:raise OSError('Pump mode mismatch')
    def read_led_config(self):
        a=[]
        for p in (0,1):
            count=self.query(CMD_GET_LED_COUNT,p); raw=self.query(CMD_GET_LED_LAYOUT,p)&0xffffffff; a.append((count,raw&255,(raw>>8)&255,(raw>>16)&255,bool((raw>>24)&1)))
        return a
    def write_led_config(self,cfgs):
        for p,(count,layout,w,h,serp) in enumerate(cfgs):
            if self.query(CMD_SET_LED_COUNT,p,count)!=count:raise OSError('LED count mismatch')
            raw=int(layout)|(int(w)<<8)|(int(h)<<16)|((1 if serp else 0)<<24); signed=raw if raw<0x80000000 else raw-0x100000000
            if self.query(CMD_SET_LED_LAYOUT,p,signed)!=signed:raise OSError('LED layout mismatch')
        if self.query(CMD_APPLY_LED_CONFIG,0,APPLY_LED_CONFIG_MAGIC)!=APPLY_LED_CONFIG_MAGIC:raise OSError('LED apply mismatch')
class FanCurveArea(Gtk.DrawingArea):
    TEMPS=(20,30,40,50,60)
    def __init__(self,editable=False,changed=None):
        super().__init__()
        self.set_size_request(500,300)
        self.set_hexpand(True); self.set_vexpand(True)
        self.editable=editable; self.changed=changed
        self.fan1=[10,20,30,50,70]; self.fan2=[10,20,30,50,70]
        self.current_temp=float('nan'); self.current_duty1=0; self.current_duty2=0
        self.pump_mode=False; self.pump_duty=60; self.drag=0
        self.add_events(Gdk.EventMask.BUTTON_PRESS_MASK|Gdk.EventMask.BUTTON_RELEASE_MASK|Gdk.EventMask.POINTER_MOTION_MASK)
        self.connect('draw',self.draw_graph); self.connect('button-press-event',self.press); self.connect('button-release-event',self.release); self.connect('motion-notify-event',self.motion)
    def set_tables(self,a,b): self.fan1=list(a); self.fan2=list(b); self.queue_draw()
    def set_current(self,t,d1,d2): self.current_temp=t; self.current_duty1=d1; self.current_duty2=d2; self.queue_draw()
    def set_pump_mode(self,en,duty): self.pump_mode=bool(en); self.pump_duty=max(35,min(100,int(duty))); self.queue_draw()
    def geom(self):
        a=self.get_allocation(); return 64,28,max(1,a.width-92),max(1,a.height-80)
    def xy(self,i,v):
        x,y,w,h=self.geom(); return x+i*w/4,y+h-(max(0,min(100,v))/100.0)*h
    def draw_graph(self,wid,cr):
        import math
        a=self.get_allocation(); cr.set_source_rgb(.094,.106,.125); cr.rectangle(0,0,a.width,a.height); cr.fill()
        x,y,w,h=self.geom()
        # grid
        cr.set_source_rgb(.235,.267,.306); cr.set_line_width(1)
        for p in range(0,101,20):
            yy=y+h-p*h/100.0; cr.move_to(x,yy); cr.line_to(x+w,yy)
        for i in range(5):
            xx=x+i*w/4.0; cr.move_to(xx,y); cr.line_to(xx,y+h)
        cr.stroke()
        # axes
        cr.set_source_rgb(.59,.63,.67); cr.set_line_width(1.5); cr.move_to(x,y); cr.line_to(x,y+h); cr.line_to(x+w,y+h); cr.stroke()
        # labels
        cr.set_source_rgb(.86,.86,.86); cr.select_font_face('Sans',0,0); cr.set_font_size(11)
        for i,t in enumerate(self.TEMPS):
            cr.move_to(x+i*w/4.0-14,y+h+24); cr.show_text(f'{t}°C')
        for p in range(0,101,20):
            cr.move_to(12,y+h-p*h/100.0+4); cr.show_text(f'{p}%')
        # legend
        cr.set_line_width(3); cr.set_source_rgb(0,.745,1); cr.move_to(x+w-190,12); cr.line_to(x+w-170,12); cr.stroke(); cr.move_to(x+w-164,16); cr.show_text('FAN1')
        cr.set_source_rgb(1,.43,.35); cr.move_to(x+w-92,12); cr.line_to(x+w-72,12); cr.stroke(); cr.move_to(x+w-66,16); cr.show_text('PUMP' if self.pump_mode else 'FAN2')
        vals2=[self.pump_duty]*5 if self.pump_mode else self.fan2
        # curves
        for vals,rgb,dashed,square in ((self.fan1,(0,.745,1),False,False),(vals2,(1,.43,.35),True,True)):
            cr.set_source_rgb(*rgb); cr.set_line_width(3); cr.set_dash([8,6] if dashed else [])
            for i,v in enumerate(vals):
                xx,yy=self.xy(i,v); cr.move_to(xx,yy) if i==0 else cr.line_to(xx,yy)
            cr.stroke(); cr.set_dash([])
            for i,v in enumerate(vals):
                xx,yy=self.xy(i,v)
                if square: cr.rectangle(xx-4,yy-4,8,8); cr.fill()
                else: cr.arc(xx,yy,4,0,6.283185); cr.fill()
        # current temperature + current duty markers (monitor graph only)
        if not self.editable and isinstance(self.current_temp,(int,float)) and math.isfinite(self.current_temp):
            tt=max(20.0,min(60.0,float(self.current_temp))); xx=x+(tt-20.0)/40.0*w
            cr.set_source_rgb(1,.86,.31); cr.set_line_width(1.5); cr.set_dash([6,5]); cr.move_to(xx,y); cr.line_to(xx,y+h); cr.stroke(); cr.set_dash([])
            for duty,rgb in ((self.current_duty1,(0,.745,1)),(self.current_duty2,(1,.43,.35))):
                yy=y+h-max(0,min(100,duty))/100.0*h; cr.set_source_rgb(*rgb); cr.arc(xx,yy,6,0,6.283185); cr.fill_preserve(); cr.set_source_rgb(1,1,1); cr.set_line_width(2); cr.stroke()
        return False
    def apply_event(self,e):
        if not self.editable or not self.drag:return
        x,y,w,h=self.geom(); i=max(0,min(4,round((e.x-x)/(w/4.0)))); v=max(0,min(100,round((y+h-e.y)/h*100)))
        (self.fan1 if self.drag==1 else self.fan2)[i]=v; self.queue_draw(); self.changed and self.changed(self.drag,i,v)
    def press(self,w,e):
        if e.button in (1,3): self.drag=1 if e.button==1 else 2; self.apply_event(e)
        return True
    def release(self,*a): self.drag=0; return True
    def motion(self,w,e): self.apply_event(e); return True
class MainWindow(Gtk.Window):
    def __init__(self):
        super().__init__(title='Water Cooling Device Controller - PUMP Edition');self.set_default_size(980,680);self.set_size_request(820,580);self.set_position(Gtk.WindowPosition.CENTER)
        self.settings=load_settings();self.client=HidClient();self.device_entries=[];self.refresh_busy=False;self.last_scan=0;self.current_temp=None;self.sensor_fault=False;self.failsafe_dialog=None;self.warning_alarm=False;self.last_settings_version=-1;self.indicator=None
        self.connect('delete-event',self.on_close);self.connect('window-state-event',self.on_window_state);p=Gtk.CssProvider();p.load_from_data(CSS);Gtk.StyleContext.add_provider_for_screen(Gdk.Screen.get_default(),p,Gtk.STYLE_PROVIDER_PRIORITY_APPLICATION)
        self.build_ui();self.build_indicator();self.set_keep_above(bool(self.settings.get('always_on_top')));GLib.timeout_add(1000,self.on_tick);self.show_all();self.device_box.hide();self.fail_bar.hide();self.pump_bar.hide();GLib.idle_add(self.auto_connect)
    def tr(self,s):return JP_EN.get(s,s) if self.settings.get('english') else s
    def lbl(self,s,x=0):return Gtk.Label(label=self.tr(s),xalign=x)
    def build_ui(self):
        root=Gtk.Box(orientation=Gtk.Orientation.VERTICAL);self.add(root);self.device_box=Gtk.Box(spacing=8,margin=8);self.device_box.pack_start(self.lbl('接続デバイス:'),False,False,0);self.device_combo=Gtk.ComboBoxText();self.device_combo.set_hexpand(True);self.device_box.pack_start(self.device_combo,True,True,0);root.pack_start(self.device_box,False,False,0)
        self.notebook=Gtk.Notebook();root.pack_start(self.notebook,True,True,0);self.tab_labels=[self.lbl('状態表示'),self.lbl('ファン出力編集'),self.lbl('設定')];self.notebook.append_page(self.monitor_tab(),self.tab_labels[0]);self.notebook.append_page(self.editor_tab(),self.tab_labels[1]);self.notebook.append_page(self.settings_tab(),self.tab_labels[2])
    def monitor_tab(self):
        o=Gtk.Box(orientation=Gtk.Orientation.VERTICAL,spacing=8,margin=14);h=Gtk.Box(spacing=10);self.connection_label=self.lbl('● 未接続');h.pack_start(self.connection_label,True,True,0);b=Gtk.Button(label=self.tr('再接続'));b.connect('clicked',lambda *_:self.manual_reconnect());h.pack_start(b,False,False,0);o.pack_start(h,False,False,0)
        self.fail_bar=self.lbl('センサーエラー - フェイルセーフ作動中（ファン出力 100%）',.5);self.fail_bar.get_style_context().add_class('failsafe');o.pack_start(self.fail_bar,False,False,0);self.pump_bar=self.lbl('ポンプエラー - フェイルセーフ作動中（ポンプ出力 100%）',.5);self.pump_bar.get_style_context().add_class('pumpfault');o.pack_start(self.pump_bar,False,False,0)
        g=Gtk.Grid(column_spacing=7);g.set_column_homogeneous(True);self.value_labels={};self.card_titles={}
        for i,(title,key) in enumerate((("水温","temp"),("ファン1 出力","duty1"),("RPM 1","rpm1"),("ファン2 出力","duty2"),("RPM 2","rpm2"))):
            f=Gtk.Frame();box=Gtk.Box(orientation=Gtk.Orientation.VERTICAL,spacing=6,margin=8);f.add(box);t=self.lbl(title,.5);t.get_style_context().add_class('dim');box.pack_start(t,False,False,0);v=Gtk.Label(xalign=.5);v.set_markup("<span size='22000' weight='bold'>--</span>");box.pack_start(v,True,True,0);g.attach(f,i,0,1,1);self.value_labels[key]=v;self.card_titles[key]=t
        o.pack_start(g,False,False,0);f=Gtk.Frame(label=self.tr('ファンカーブ  —  X: 水温 20～60°C / Y: ファン出力 0～100%'));self.graph=FanCurveArea();f.add(self.graph);o.pack_start(f,True,True,0);return o
    def header(self,s):f=Gtk.Frame();l=self.lbl(s,.5);l.set_margin_top(7);l.set_margin_bottom(7);f.add(l);return f
    def editor_tab(self):
        o=Gtk.Box(orientation=Gtk.Orientation.VERTICAL,spacing=8,margin=18);g=Gtk.Grid(column_spacing=1,row_spacing=1);g.set_column_homogeneous(True);g.attach(self.header('水温ポイント'),0,0,1,1)
        for i,t in enumerate((20,30,40,50,60)):g.attach(self.header(f'{t} °C'),i+1,0,1,1)
        g.attach(self.header('ファン1 出力'),0,1,1,1);g.attach(self.header('ファン2 出力'),0,2,1,1);self.fan1_spins=[];self.fan2_spins=[]
        for i,v in enumerate((10,20,30,50,70)):
            for r,a in ((1,self.fan1_spins),(2,self.fan2_spins)):
                s=Gtk.SpinButton.new_with_range(0,100,1);s.set_value(v);s.connect('value-changed',self.editor_changed);a.append(s);g.attach(s,i+1,r,1,1)
        o.pack_start(g,False,False,0);f=Gtk.Frame(label=self.tr('マウス編集: 左ドラッグ＝FAN1 / 右ドラッグ＝FAN2'));self.edit_graph=FanCurveArea(True,self.graph_changed);f.add(self.edit_graph);o.pack_start(f,True,True,0)
        row=Gtk.Box(spacing=7)
        for text,cb in (("本体へ保存",self.save_tables),("再読込",self.load_tables),("初期値に戻す",self.reset_tables),("ファン1 → ファン2へコピー",self.copy_fan1)):
            q=Gtk.Button(label=self.tr(text));q.connect('clicked',cb);row.pack_start(q,False,False,0)
        row.pack_start(self.lbl('警告温度:'),False,False,8);self.warning_spin=Gtk.SpinButton.new_with_range(0,60,1);self.warning_spin.set_value(self.settings.get('warning_temp',40));row.pack_start(self.warning_spin,False,False,0);row.pack_start(Gtk.Label(label='°C'),False,False,0);note=self.lbl('設定は本体受信後500msで保存されます。');note.get_style_context().add_class('dim');row.pack_start(note,False,False,10);o.pack_start(row,False,False,0);return o
    def pad(self,w):b=Gtk.Box(margin=9);b.pack_start(w,True,True,0);return b
    def settings_tab(self):
        sc=Gtk.ScrolledWindow();grid=Gtk.Grid(column_spacing=12,row_spacing=0,margin=30);sc.add(grid);r=0
        def add(title,w):
            nonlocal r
            h=self.header(title);h.set_size_request(180,-1);grid.attach(h,0,r,1,1);f=Gtk.Frame();f.add(w);grid.attach(f,1,r,1,1);r+=1
        self.english_check=Gtk.CheckButton(label='English');self.english_check.set_active(self.settings.get('english'));self.english_check.connect('toggled',self.save_english);add('表示言語',self.pad(self.english_check));self.startup_check=Gtk.CheckButton(label=self.tr('Ubuntu起動時に起動'));self.startup_check.set_active(AUTOSTART_FILE.exists());self.startup_check.connect('toggled',self.set_startup);add('自動起動',self.pad(self.startup_check))
        cr=Gtk.Box(spacing=7);self.low_button=Gtk.Button(label=self.tr('20℃以下の色を選択'));self.low_button.connect('clicked',lambda *_:self.choose_color('low_color'));cr.pack_start(self.low_button,False,False,0);self.low_preview=Gtk.DrawingArea();self.low_preview.set_size_request(44,24);self.low_preview.connect('draw',lambda w,c:self.draw_preview(c,'low_color'));cr.pack_start(self.low_preview,False,False,0);self.warn_button=Gtk.Button(label=self.tr('警告温度の色を選択'));self.warn_button.connect('clicked',lambda *_:self.choose_color('warning_color'));cr.pack_start(self.warn_button,False,False,0);self.warn_preview=Gtk.DrawingArea();self.warn_preview.set_size_request(44,24);self.warn_preview.connect('draw',lambda w,c:self.draw_preview(c,'warning_color'));cr.pack_start(self.warn_preview,False,False,0);cr.pack_start(self.lbl('最小化すると通知領域に現在水温と温度色を表示します。'),False,False,8);add('温度表示',self.pad(cr))
        self.beep_check=Gtk.CheckButton(label=self.tr('警告温度でBEEPを鳴らす'));self.beep_check.set_active(self.settings.get('beep_on_warning'));self.beep_check.connect('toggled',self.save_beep);add('警告音',self.pad(self.beep_check));self.top_check=Gtk.CheckButton(label=self.tr('常に手前に表示'));self.top_check.set_active(self.settings.get('always_on_top'));self.top_check.connect('toggled',self.set_top);add('ウィンドウ表示',self.pad(self.top_check))
        pr=Gtk.Box(spacing=8);self.pump_check=Gtk.CheckButton(label=self.tr('FAN2をPUMPとして使用（実験的機能）'));self.pump_check.set_active(self.settings.get('pump_mode'));pr.pack_start(self.pump_check,False,False,0);pr.pack_start(Gtk.Label(label='Pump Duty:'),False,False,6);self.pump_spin=Gtk.SpinButton.new_with_range(35,100,1);self.pump_spin.set_value(self.settings.get('pump_duty',60));pr.pack_start(self.pump_spin,False,False,0);pr.pack_start(Gtk.Label(label='%（最小35%）'),False,False,0);q=Gtk.Button(label=self.tr('本体へ保存'));q.connect('clicked',self.apply_pump);pr.pack_start(q,False,False,8);warn=self.lbl('実験的機能／対応ポンプでのみ使用／設定によってはポンプ停止の可能性');warn.get_style_context().add_class('warning');pr.pack_start(warn,False,False,8);add('PUMP設定',self.pad(pr));add('ARGB LED構成',self.pad(self.led_panel()));return sc
    def led_panel(self):
        b=Gtk.Box(orientation=Gtk.Orientation.VERTICAL,spacing=7);self.led_widgets=[]
        for p in (0,1):
            r=Gtk.Box(spacing=6);r.pack_start(Gtk.Label(label=f'GPIO {p}:'),False,False,0)
            r.pack_start(self.lbl('LED数:'),False,False,0);c=Gtk.SpinButton.new_with_range(1,64,1);c.set_value(6);r.pack_start(c,False,False,0)
            r.pack_start(self.lbl('配置:'),False,False,6);l=Gtk.ComboBoxText();[l.append_text(self.tr(t)) for t in ('ファン（円形）','LEDテープ（直線）','マトリックス')];l.set_active(0);r.pack_start(l,False,False,0)
            wl=self.lbl('横:');r.pack_start(wl,False,False,6);w=Gtk.SpinButton.new_with_range(1,64,1);w.set_value(3);r.pack_start(w,False,False,0)
            hl=self.lbl('縦:');r.pack_start(hl,False,False,0);h=Gtk.SpinButton.new_with_range(1,64,1);h.set_value(2);r.pack_start(h,False,False,0)
            serp=Gtk.CheckButton(label=self.tr('ジグザグ配線'));serp.set_active(True);r.pack_start(serp,False,False,6)
            b.pack_start(r,False,False,0);self.led_widgets.append((c,l,w,h,serp,wl,hl))
            l.connect('changed',lambda combo, idx=p:self.update_led_layout_visibility(idx))
        r=Gtk.Box(spacing=7);q=Gtk.Button(label=self.tr('再読込'));q.connect('clicked',self.load_led);r.pack_start(q,False,False,0);q=Gtk.Button(label=self.tr('LED構成を本体へ保存・適用'));q.connect('clicked',self.save_led);r.pack_start(q,False,False,0);b.pack_start(r,False,False,0)
        GLib.idle_add(self.update_all_led_layout_visibility)
        return b
    def update_led_layout_visibility(self,index):
        if not hasattr(self,'led_widgets') or index>=len(self.led_widgets):return
        c,l,w,h,serp,wl,hl=self.led_widgets[index];matrix=(l.get_active()==2)
        for widget in (wl,w,hl,h,serp):widget.set_visible(matrix)
    def update_all_led_layout_visibility(self):
        if hasattr(self,'led_widgets'):
            for i in range(len(self.led_widgets)):self.update_led_layout_visibility(i)
        return False
    def build_indicator(self):
        if not AppIndicator3:return
        self.indicator=AppIndicator3.Indicator.new('watercoolingdevice','utilities-system-monitor',AppIndicator3.IndicatorCategory.APPLICATION_STATUS);self.indicator.set_status(AppIndicator3.IndicatorStatus.ACTIVE)
        try:self.indicator.set_label('--.--°C','99.99°C')
        except Exception:pass
        m=Gtk.Menu();self.indicator_open=Gtk.MenuItem(label=self.tr('WaterCoolingDeviceを開く'));self.indicator_open.connect('activate',lambda *_:self.show_main());m.append(self.indicator_open);m.append(Gtk.SeparatorMenuItem());self.indicator_quit=Gtk.MenuItem(label=self.tr('終了'));self.indicator_quit.connect('activate',lambda *_:self.quit_app());m.append(self.indicator_quit);m.show_all();self.indicator.set_menu(m)
    def refresh_devices(self):
        try:ds=HidClient.enumerate_devices()
        except Exception:ds=[]
        old=self.settings.get('selected_serial','');self.device_entries=ds;self.device_combo.remove_all();sel=0
        for i,d in enumerate(ds):self.device_combo.append_text(d.label);sel=i if old and d.serial==old else sel
        if ds:self.device_combo.set_active(sel)
        self.device_box.set_visible(len(ds)>=2)
    def auto_connect(self):self.refresh_devices();self.device_entries and self.safe_connect();return False
    def safe_connect(self):
        try:self.connect_selected()
        except Exception as e:self.connection(f"{self.tr('● 未接続')}: {e}",False)
    def connect_selected(self):
        if not self.device_entries:raise OSError('Vendor HIDが見つかりません。')
        i=self.device_combo.get_active();i=0 if i<0 else i;d=self.device_entries[i];self.client.connect(d);self.settings['selected_serial']=d.serial;save_settings(self.settings);self.connection(self.tr('● 水冷ファンコントローラ 接続済み'),True);self.async_(self.load_initial)
    def manual_reconnect(self):self.client.close();self.refresh_devices();self.safe_connect()
    def connection(self,text,ok):self.connection_label.set_text(text);c=self.connection_label.get_style_context();c.remove_class('connected');c.remove_class('disconnected');c.add_class('connected' if ok else 'disconnected')
    def on_tick(self):
        now=time.monotonic()
        if not self.client.connected:
            if now-self.last_scan>=3:self.last_scan=now;self.refresh_devices();self.device_entries and self.safe_connect()
            return True
        if not self.refresh_busy:self.refresh_busy=True;self.async_(self.poll)
        return True
    def poll(self):
        try:
            t=self.client.query(CMD_GET_WATER_TEMP)/100.;v=self.client.query(CMD_GET_SETTINGS_VERSION);d1=self.client.query(CMD_GET_DUTY,0);r1=self.client.query(CMD_GET_FAN_RPM,0);d2=self.client.query(CMD_GET_DUTY,1);r2=self.client.query(CMD_GET_FAN_RPM,1);s=self.client.query(CMD_GET_SENSOR_STATUS);p=self.client.query(CMD_GET_PUMP_STATUS) if self.pump_check.get_active() else 0
            if self.last_settings_version>=0 and v!=self.last_settings_version:self.load_initial()
            self.last_settings_version=v;GLib.idle_add(self.update_status,t,d1,r1,d2,r2,s,p)
        except Exception:self.client.close();GLib.idle_add(self.connection,self.tr('● 未接続 — 再接続待機中'),False)
        finally:self.refresh_busy=False
    def temperature_rgba(self,t):
        lo=Gdk.RGBA(); hi=Gdk.RGBA()
        if not lo.parse(self.settings.get('low_color','#2d7fff')): lo.parse('#2d7fff')
        if not hi.parse(self.settings.get('warning_color','#ff3b30')): hi.parse('#ff3b30')
        upper=max(20.1,float(self.warning_spin.get_value_as_int()))
        ratio=max(0.0,min(1.0,(float(t)-20.0)/(upper-20.0)))
        return (lo.red+(hi.red-lo.red)*ratio, lo.green+(hi.green-lo.green)*ratio, lo.blue+(hi.blue-lo.blue)*ratio)
    def temp_markup(self,t,fault):
        if fault: return f"<span size='15000' weight='bold' foreground='#FA8072'>{self.tr('センサー')}\n{self.tr('故障')}</span>"
        r,g,b=self.temperature_rgba(t); color=f'#{round(r*255):02X}{round(g*255):02X}{round(b*255):02X}'
        return f"<span size='22000' weight='bold' foreground='{color}'>{t:.2f} °C</span>"
    def update_status(self,t,d1,r1,d2,r2,s,p):
        fault=s!=0 or t<-20 or t>60
        was_fault=self.sensor_fault
        self.sensor_fault=fault;self.current_temp=None if fault else t;self.value_labels['temp'].set_markup(self.temp_markup(t,fault));self.value_labels['duty1'].set_markup(f"<span size='22000' weight='bold'>{d1} %</span>");self.value_labels['rpm1'].set_markup(f"<span size='22000' weight='bold'>{r1} rpm</span>");self.value_labels['duty2'].set_markup(f"<span size='22000' weight='bold'>{d2} %</span>");self.value_labels['rpm2'].set_markup(f"<span size='22000' weight='bold'>{r2} rpm</span>");pump=self.pump_check.get_active();self.card_titles['duty2'].set_text(self.tr('PUMP 出力' if pump else 'ファン2 出力'));self.card_titles['rpm2'].set_text(self.tr('PUMP RPM' if pump else 'RPM 2'));self.graph.set_current(float('nan') if fault else t,d1,d2);self.graph.set_pump_mode(pump,self.pump_spin.get_value_as_int());self.edit_graph.set_pump_mode(False,self.pump_spin.get_value_as_int());self.fail_bar.set_visible(fault);self.pump_bar.set_visible(bool(pump and (p&2)));warning=self.warning_spin.get_value_as_int();alarm=(not fault and t>=warning)
        if fault and not was_fault:self.show_failsafe_dialog()
        if alarm and not self.warning_alarm and self.beep_check.get_active():
            try:Gdk.beep()
            except Exception:pass
        self.warning_alarm=alarm;self.update_tray();return False
    def show_failsafe_dialog(self):
        if self.failsafe_dialog is not None:
            try:self.failsafe_dialog.present()
            except Exception:pass
            return False
        d=Gtk.MessageDialog(transient_for=self,flags=Gtk.DialogFlags.MODAL|Gtk.DialogFlags.DESTROY_WITH_PARENT,message_type=Gtk.MessageType.ERROR,buttons=Gtk.ButtonsType.OK,text=self.tr('温度センサー異常'))
        d.format_secondary_text(self.tr('温度センサー異常のためフェイルセーフが発動しています。（出力100%）')+'\n\n'+self.tr('安全のためファン出力を100%に固定しています。センサーと配線を確認してください。'))
        d.set_title(self.tr('温度センサー異常'))
        d.set_keep_above(True)
        d.connect('response',self.on_failsafe_dialog_response)
        d.connect('destroy',lambda *_:setattr(self,'failsafe_dialog',None))
        self.failsafe_dialog=d
        self.show_main()
        d.show_all();d.present()
        return False
    def on_failsafe_dialog_response(self,d,*_):
        d.destroy();self.failsafe_dialog=None

    def update_tray(self):
        if self.indicator:
            try:self.indicator.set_label('ERR' if self.current_temp is None else f'{self.current_temp:.2f}°C','99.99°C')
            except Exception:pass
    def load_initial(self):
        try:w=self.client.query(CMD_GET_WARNING_TEMP);a=self.client.read_fan_table(1);b=self.client.read_fan_table(2);led=self.client.read_led_config();GLib.idle_add(self.apply_initial,w,a,b,led)
        except Exception:pass
    def apply_initial(self,w,a,b,led):
        self.warning_spin.set_value(max(0,min(60,w)));self.settings['warning_temp']=w;save_settings(self.settings)
        for s,v in zip(self.fan1_spins,a):s.set_value(v)
        for s,v in zip(self.fan2_spins,b):s.set_value(v)
        self.graph.set_tables(a,b);self.edit_graph.set_tables(a,b);self.apply_led(led);return False
    def editor_changed(self,*_):a=[s.get_value_as_int() for s in self.fan1_spins];b=[s.get_value_as_int() for s in self.fan2_spins];self.graph.set_tables(a,b);self.edit_graph.set_tables(a,b)
    def graph_changed(self,fan,i,v):(self.fan1_spins if fan==1 else self.fan2_spins)[i].set_value(v)
    def save_tables(self,*_):
        a=[s.get_value_as_int() for s in self.fan1_spins];b=[s.get_value_as_int() for s in self.fan2_spins];w=self.warning_spin.get_value_as_int();self.settings['warning_temp']=w;save_settings(self.settings)
        self.async_(lambda:self.safe(lambda:(self.client.write_fan_table(1,a),self.client.write_fan_table(2,b),self.client.query(CMD_SET_WARNING_TEMP,0,w))))
    def load_tables(self,*_):self.async_(self.load_initial)
    def reset_tables(self,*_):
        for a in (self.fan1_spins,self.fan2_spins):
            for s,v in zip(a,(10,20,30,50,70)):s.set_value(v)
    def copy_fan1(self,*_):
        for a,b in zip(self.fan1_spins,self.fan2_spins):b.set_value(a.get_value())
    def apply_pump(self,*_):
        e=self.pump_check.get_active();d=self.pump_spin.get_value_as_int();self.settings['pump_mode']=e;self.settings['pump_duty']=d;save_settings(self.settings);self.async_(lambda:self.safe(lambda:self.client.configure_pump(e,d)))
    def load_led(self,*_):self.async_(lambda:self.safe_led())
    def safe_led(self):
        try:self.require();x=self.client.read_led_config();GLib.idle_add(self.apply_led,x)
        except Exception:pass
    def apply_led(self,cfgs):
        for z,cfg in zip(self.led_widgets,cfgs):c,l,w,h,s=cfg;z[0].set_value(c);z[1].set_active(max(0,min(2,l)));z[2].set_value(max(1,w));z[3].set_value(max(1,h));z[4].set_active(s)
        self.update_all_led_layout_visibility();return False
    def save_led(self,*_):
        cfg=[]
        for c,l,w,h,s,wl,hl in self.led_widgets:
            cv=c.get_value_as_int();lv=l.get_active();wv=w.get_value_as_int();hv=h.get_value_as_int()
            if lv==2 and wv*hv!=cv:return
            cfg.append((cv,lv,wv,hv,s.get_active()))
        self.async_(lambda:self.safe(lambda:self.client.write_led_config(cfg)))
    def require(self):
        if not self.client.connected:raise OSError('本体へ接続してください')
    def safe(self,f):
        try:self.require();f()
        except Exception:pass
    def async_(self,f):threading.Thread(target=f,daemon=True).start()
    def translate_existing_text(self,text,to_english):
        rev={v:k for k,v in JP_EN.items()}
        return JP_EN.get(text,text) if to_english else rev.get(text,text)
    def refresh_language(self):
        to_en=bool(self.settings.get('english'))
        if hasattr(self,'tab_labels'):
            for label,key in zip(self.tab_labels,('状態表示','ファン出力編集','設定')):label.set_text(self.tr(key))
        def walk(widget):
            if isinstance(widget,Gtk.Label):
                txt=widget.get_text();new=self.translate_existing_text(txt,to_en)
                if new!=txt:widget.set_text(new)
            if isinstance(widget,Gtk.Frame):
                txt=widget.get_label()
                if txt:
                    new=self.translate_existing_text(txt,to_en)
                    if new!=txt:widget.set_label(new)
            if isinstance(widget,Gtk.Container):
                for child in widget.get_children():walk(child)
        walk(self)
        if hasattr(self,'led_widgets'):
            names=('ファン（円形）','LEDテープ（直線）','マトリックス')
            for z in self.led_widgets:
                combo=z[1];active=combo.get_active();combo.remove_all()
                for name in names:combo.append_text(self.tr(name))
                combo.set_active(max(0,active));self.update_led_layout_visibility(self.led_widgets.index(z))
        if hasattr(self,'indicator_open'):self.indicator_open.set_label(self.tr('WaterCoolingDeviceを開く'))
        if hasattr(self,'indicator_quit'):self.indicator_quit.set_label(self.tr('終了'))
        pump=self.pump_check.get_active() if hasattr(self,'pump_check') else False
        if hasattr(self,'card_titles'):
            self.card_titles['duty2'].set_text(self.tr('PUMP 出力' if pump else 'ファン2 出力'))
            self.card_titles['rpm2'].set_text(self.tr('PUMP RPM' if pump else 'RPM 2'))
        if self.sensor_fault and hasattr(self,'value_labels'):self.value_labels['temp'].set_markup(self.temp_markup(0,True))
        if self.failsafe_dialog is not None:
            try:
                self.failsafe_dialog.set_title(self.tr('温度センサー異常'))
                self.failsafe_dialog.set_markup(self.tr('温度センサー異常'))
                self.failsafe_dialog.format_secondary_text(self.tr('温度センサー異常のためフェイルセーフが発動しています。（出力100%）')+'\n\n'+self.tr('安全のためファン出力を100%に固定しています。センサーと配線を確認してください。'))
            except Exception:pass
    def save_english(self,w):
        self.settings['english']=w.get_active();save_settings(self.settings);self.refresh_language()
    def save_beep(self,w):self.settings['beep_on_warning']=w.get_active();save_settings(self.settings)
    def set_top(self,w):self.settings['always_on_top']=w.get_active();save_settings(self.settings);self.set_keep_above(w.get_active())
    def set_startup(self,w):
        try:
            if w.get_active():AUTOSTART_DIR.mkdir(parents=True,exist_ok=True);AUTOSTART_FILE.write_text(f'[Desktop Entry]\nType=Application\nName=WaterCoolingDevice\nExec=python3 "{APP_DIR/"watercoolingdevice.py"}"\nTerminal=false\nX-GNOME-Autostart-enabled=true\n',encoding='utf-8')
            elif AUTOSTART_FILE.exists():AUTOSTART_FILE.unlink()
        except Exception:pass
    def choose_color(self,key):
        d=Gtk.ColorChooserDialog(title='Color',transient_for=self);c=Gdk.RGBA();c.parse(self.settings.get(key,'#fff'));d.set_rgba(c)
        if d.run()==Gtk.ResponseType.OK:self.settings[key]=d.get_rgba().to_string();save_settings(self.settings);self.low_preview.queue_draw();self.warn_preview.queue_draw();self.value_labels['temp'].set_markup(self.temp_markup(self.current_temp,False)) if self.current_temp is not None else None
        d.destroy()
    def draw_preview(self,cr,key):c=Gdk.RGBA();c.parse(self.settings.get(key,'#fff'));cr.set_source_rgba(c.red,c.green,c.blue,c.alpha);cr.paint();return False
    def on_window_state(self,w,e):
        if e.new_window_state&Gdk.WindowState.ICONIFIED and self.indicator:GLib.idle_add(self.hide)
        return False
    def on_close(self,*_):
        if self.indicator:self.hide();return True
        return False
    def show_main(self):self.deiconify();self.show_all();self.device_box.set_visible(len(self.device_entries)>=2);self.fail_bar.set_visible(self.sensor_fault);self.present();return False
    def quit_app(self):self.client.close();Gtk.main_quit()
def main():MainWindow();Gtk.main()
if __name__=='__main__':main()
