using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DGLABtMod.Content.Config;
using DGLABtMod.Content.Mains;
using DGLABtMod.Content.Mains.UI;
using FullSerializer;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using ReLogic.Peripherals.RGB.Corsair;
using Steamworks;
using Terraria;
using Terraria.GameContent.UI.Chat;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using tModPorter;
using WebSocketSharp.Server;
using static System.Collections.Specialized.BitVector32;
using static System.Net.Mime.MediaTypeNames;
using static ConnectDevice;

//此处代码用于检测玩家 debuff 受伤事件(水中的窒息除外.水中窒息也是一个无法处理的),接收主要伤害\死亡\重生信息,并对他们做出反应.还可以控制强度.
namespace DGLABtMod.Content.Mains
{
    public class Configs
    {
        public bool WARN { get; set; } = false;
        public bool WARN1 { get; set; } = false;
        public bool WARN2 { get; set; } = false;
        public bool WARN3 { get; set; } = false;
        public bool WARN4 { get; set; } = false;
        public bool A { get; set; } = true;//我操，我真是服了我了.强度控制这块做了好几天了，开始做别的，才发现这个根本就没有引用
        public int MaxA { get; set; } = 100;
        public int LowA { get; set; } = 0;
        public bool B { get; set; } = true;
        public int MaxB { get; set; } = 100;
        public int LowB { get; set; } = 0;
        public int CommStrength { get; set; } = 3;
        public int BuffStrength { get; set; } = 3;
        public int BuffPlusStrength { get; set; } = 6;
        public int BuffTroubleStrength { get; set; } = 10;
        public int Death { get; set; } = 90;
        public int DeathTime { get; set; } = 300;
        public int WaitTime { get; set; } = 10;
        public int Speed { get; set; } = 3;
        public int SpeedBuff { get; set; } = 2;
        public int SpeedBuffPlus { get; set; } = 6;
        //public int WaveDuration { get; set; }
        //public string CommWave { get; set; } = "aa0a0a64000000";
        //public string BuffWave { get; set; } = "aa0a0a64000000";
        //public string BuffPlusWave { get; set; } = "aa0a0a64000000";
        //public string BuffTroubleWave { get; set; } = "aA0A0A0A64646464";
        //public string DeathWave { get; set; } = "aA0A0A0A1E1E1E1E";
        public int Time { get; set; } = 3;
        //public int UIx { get; set; }
        //public int UIy { get; set; }
        public int Port { get; set; } = 9999;
        public string IP { get; set; } = "";
        public bool IsDebug { get; set; } = false;
        public bool DebugBuff { get; set; } = false;
        
    }
    public static class WaveStore
    {
        // DeathWave (12个元素)
        public static string DeathWave = "[\"0A0A0A0A64646464\", \"0A0A0A0A64646464\", \"0A0A0A0A64646464\", \"0A0A0A0A64646464\", \"0A0A0A0A64646464\", \"0A0A0A0A64646464\", \"0A0A0A0A64646464\", \"0A0A0A0A64646464\", \"0A0A0A0A64646464\", \"0A0A0A0A64646464\", \"0A0A0A0A64646464\", \"0A0A0A0A64646464\"]";

        // CommWave (12个元素)
        public static string CommWave = "[\"1414141464646464\", \"1414141464646464\", \"0A0A0A0A50505050\", \"0A0A0A0A50505050\", \"0A0A0A0A3C3C3C3C\", \"0A0A0A0A3C3C3C3C\", \"0A0A0A0A28282828\", \"0A0A0A0A28282828\", \"0A0A0A0A14141414\", \"0A0A0A0A14141414\", \"0A0A0A0A0A0A0A0A\", \"0A0A0A0A0A0A0A0A\"]";

        // BuffWave (12个元素)
        public static string BuffWave = "[\"0A0A0A0A0A0A0A0A\", \"0A0A0A0A0A0A0A0A\", \"0A0A0A0A1E1E1E1E\", \"0A0A0A0A1E1E1E1E\", \"0A0A0A0A28282828\", \"1414141432323232\", \"1414141432323232\", \"0A0A0A0A28282828\", \"0A0A0A0A1E1E1E1E\", \"0A0A0A0A1E1E1E1E\", \"0A0A0A0A0A0A0A0A\", \"0A0A0A0A0A0A0A0A\"]";

        // BuffTroubleWave (6个元素)
        public static string BuffTroubleWave = "[\"0A0A0A0A64646464\", \"0A0A0A0A64646464\", \"0A0A0A0A64646464\", \"0A0A0A0A64646464\", \"0A0A0A0A1E1E1E1E\", \"0A0A0A0A1E1E1E1E\"]";

        // BuffPlusWave (12个元素)
        public static string BuffPlusWave = "[\"1414141464646464\", \"1414141464646464\", \"1414141400000000\", \"1414141400000000\", \"1414141464646464\", \"1414141464646464\", \"1414141400000000\", \"1414141400000000\", \"1414141464646464\", \"1414141464646464\", \"1414141464646464\", \"1414141464646464\"]";
    }
    public static class ConfigStore
    {
        public static Configs Current { get; set; }
    }
    public class Control : ModSystem
    {
        

        public static bool Debuff;
        public static bool DebuffPlus;
        public static bool DebuffTrouble;

        public static int NowStrengthA;
        public static int NowStrengthB;

        public static int EndStrengthA;
        public static int EndStrengthB;

        
        
        public static void CommAdd(int damage)//普通伤害
        {
            if ((damage / 10) == 0)
            {
                NowStrengthA++;
                NowStrengthB++;
            }
            NowStrengthA = NowStrengthA + (ConfigStore.Current.CommStrength) * (damage / 10);
            NowStrengthB = NowStrengthB + (ConfigStore.Current.CommStrength) * (damage / 10);
            wait = 0;//重置等待时间
            ConnectDevice.SendWaveCommand(WaveStore.CommWave);
        }
        public override void PostUpdateEverything()//每一帧都会被调用
        {
            //Main.NewText("更新更新");
            Control.TickScd();
            var myUI = ModContent.GetInstance<MyUISystem>().myUI;
            myUI.UpdateText(Control.EndStrengthA, ConfigStore.Current.MaxA, Control.EndStrengthB, ConfigStore.Current.MaxB);
            
            Control.BuffTroubleAdd();
            Control.Max();
            wavecd--;
        }
        public static int t = 0;
        public static int wait = 0;
        public static bool OnDeath = false;//已弃用

        public static void TickScd()//每过一秒会触发一次该函数中的if
        {
            t = t+1;
            if (t == 60 /*&& OnDeath == false*/)
            {
                t = 0;
                BuffAdd();
                BuffPlusAdd();
                Down();
                //Control.Death();
                wait++;//强度下降倒计时
                strengthcdA--;
                strengthcdB--;
                
                //Main.NewText("一秒过去了");
            }
            
            
        }
        public static void BuffAdd()
        {
            if (Debuff)
            {
                wait = 0;//重置等待时间
                NowStrengthA = NowStrengthA + ConfigStore.Current.BuffStrength;
                NowStrengthB = NowStrengthB + ConfigStore.Current.BuffStrength;
                ConnectDevice.SendWaveCommand(WaveStore.BuffWave);
            }
            
        }
        public static void BuffPlusAdd()
        {
            if (DebuffPlus)
            {
                wait = 0;//重置等待时间
                NowStrengthA = NowStrengthA + ConfigStore.Current.BuffPlusStrength;
                NowStrengthB = NowStrengthB + ConfigStore.Current.BuffPlusStrength;
                ConnectDevice.SendWaveCommand(WaveStore.BuffPlusWave);
            }
        }
        //当函数被调用时会使强度加.如果函数达到被调用条件，每一帧都会被调用,但是这里仍然只会加一次
        private static bool _strengthModified = false;
        public static void BuffTroubleAdd()
        {
            if (DebuffTrouble)
            {
                ConnectDevice.SendWaveCommand(WaveStore.BuffTroubleWave);
                wait = 0;//重置等待时间
                if (!_strengthModified) // 只在首次进入DebuffTrouble时加
                {
                    NowStrengthA += ConfigStore.Current.BuffTroubleStrength;
                    NowStrengthB += ConfigStore.Current.BuffTroubleStrength;
                    _strengthModified = true;
                }
            }
            else
            {
                
                if (_strengthModified) // 只在首次离开DebuffTrouble时减
                {
                    NowStrengthA -= ConfigStore.Current.BuffTroubleStrength;
                    NowStrengthB -= ConfigStore.Current.BuffTroubleStrength;
                    _strengthModified = false;
                }
            }
        }
        public static int deathTime = 0;
        public static void Death()
        {
            //Main.NewText("死亡界面"+deathTime);
            if(ConfigStore.Current.DeathTime != 0)
            {
                deathTime++;
            }
            if (deathTime <= ConfigStore.Current.DeathTime || ConfigStore.Current.DeathTime == 0)
            {
                NowStrengthA = ConfigStore.Current.Death;
                NowStrengthB = ConfigStore.Current.Death;
                //Debug.DebugSendMsg("siwangsa", 255, 255, 255);
                //Main.NewText("发送死亡波形");
                ConnectDevice.SendWaveCommand(WaveStore.DeathWave);
            }
            if(deathTime >= ConfigStore.Current.DeathTime && ConfigStore.Current.DeathTime != 0)//发布前最后的更改
            {
                ConnectDevice.StopWave();
            }
        }
        public static void Respawn()
        {
            NowStrengthA = 0;
            NowStrengthB = 0;
            deathTime = 0;
            StopWave();
            wait = ConfigStore.Current.WaitTime;//重置等待时间
        }
        public static void Down()
        {
            if (wait >= ConfigStore.Current.WaitTime)
            {
                NowStrengthA = NowStrengthA - ConfigStore.Current.Speed;
                NowStrengthB = NowStrengthB - ConfigStore.Current.Speed;
                wait = ConfigStore.Current.WaitTime - 1;//下降等待时间
            }
            
        }
        public static void DownBuff()
        {
            if(t == 59)
            {
                NowStrengthA = NowStrengthA - ConfigStore.Current.SpeedBuff;
                NowStrengthB = NowStrengthB - ConfigStore.Current.SpeedBuff;
                //Console.WriteLine("程度已下降");
            }
        }
        public static void DownBuffPlus()
        {
            if (t == 59)
            {
                NowStrengthA = NowStrengthA - ConfigStore.Current.SpeedBuffPlus;
                NowStrengthB = NowStrengthB - ConfigStore.Current.SpeedBuffPlus;
                StopWave();
                //Console.WriteLine("程度已下降");
            }
        }
        public static void Max()//控制输出强度范围(写完之后才知道 Math.Clamp() 。初学者，请见谅)
        {
            
            if (NowStrengthA > ConfigStore.Current.MaxA)
            {
                EndStrengthA = ConfigStore.Current.MaxA;
            }
            else if (NowStrengthA < 0)
            {
                EndStrengthA = 0;
                NowStrengthA = 0;
            }
            else
            {
                EndStrengthA = NowStrengthA;
            }
            if (NowStrengthA < ConfigStore.Current.LowA)
            {
                EndStrengthA = ConfigStore.Current.LowA;
                NowStrengthA = ConfigStore.Current.LowA;
            }
            if (EndStrengthA > 200)
            {
                EndStrengthA = 200;
                NowStrengthA = 200;
            }
            if (NowStrengthA > 400)
            {
                NowStrengthA = 400;
            }
            //
            if (NowStrengthB > 300)
            {
                EndStrengthB = 300;
            }
            if (NowStrengthB > ConfigStore.Current.MaxB)
            {
                EndStrengthB = ConfigStore.Current.MaxB;
            }
            else if (NowStrengthB < 0)
            {
                EndStrengthB = 0;
                NowStrengthB = 0;
            }
            else
            {
                EndStrengthB = NowStrengthB;
            }
            if (NowStrengthB < ConfigStore.Current.LowB)
            {
                EndStrengthB = ConfigStore.Current.LowB; 
                NowStrengthB = ConfigStore.Current.LowB;
            }
            if (EndStrengthB > 200)
            {
                EndStrengthB = 200;
                NowStrengthB = 200;
            }
            if (NowStrengthB > 400)
            {
                NowStrengthB = 400;
            }
            //设置强度
            ConnectDevice.SendStrengthACommand(EndStrengthA);
            ConnectDevice.SendStrengthBCommand(EndStrengthB);
            if (_currentClient == null) return;
            //SendConnSussMsg();

            //强度控制到此结束

        }
        //public static bool IsConn;
        public static void CheckConnect()//左键点击ui触发
        {
            if (!ControlBuff.IsReadWarn) { Main.NewText("操作失败,请先在配置中阅读并确认重要安全警示", 255, 0, 0); return; }
            //Main.LocalPlayer.mouseInterface = true;
            if (IsConn && IsRunning)
            {
                Main.NewText("当前连接状态:已连接.WebSocket服务启动状态:已启动",10,240,10);
            }
            else if (IsConn && !IsRunning)
            {
                Main.NewText("当前连接状态:已连接.WebSocket服务启动状态:未启动(??不应该啊?)");
            }
            else if (!IsConn && IsRunning)
            {
                Main.NewText("当前连接状态:未连接.WebSocket服务启动状态:已启动");
            }
            else
            {
                Main.NewText("当前连接状态:未连接.WebSocket服务启动状态:未启动");
            }
            

        }
        
        public static void Connect()//右键点击ui触发
        {
            if (!ControlBuff.IsReadWarn) { Main.NewText("操作失败,请先在配置中阅读并确认重要安全警示",255,0,0); return; }
            

            //Main.LocalPlayer.mouseInterface = true;
            //Main.NewText("UI 被点击！"); // 示例：显示提示

            Main.NewText("正在启动WebSocket服务");
                try
                {
                    ConnectDevice.StartServer();
                }
                catch
                {
                    Main.NewText("启动服务失败,请尝试修改端口号后重试,详见README.md");
                }
                Main.NewText("正在准备创建二维码");
            if (IsRunning)
            {
                try
                {
                    SocketConn.QRCode();
                    string picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                    Main.NewText($"二维码创建成功,已自动打开(如果没打开,请手动打开)(保存于{picturesPath}\\DGLAB-X-tMod\\DGLAB_tMod_QRCode.png)");
                }
                catch
                {
                    Main.NewText("二维码创建失败,详见README.md");
                }
            }
            else
            {
                Main.NewText("服务未启动,无法创建二维码,详见README.md");
            }
                
        }
        public static void Stop()
        {
            if (!ControlBuff.IsReadWarn) { Main.NewText("操作失败,请先在配置中阅读并确认重要安全警示", 255, 0, 0); return; }
            
            Main.NewText("即将关闭服务");
            StopServer();
        }

        public override void OnWorldUnload()
        {
            Stop();
        }

    }
}
    
public class ControlBuff : GlobalBuff//用于检测受伤事件
{
    public int _144;
    public static bool IsReadWarn = false;//用于判断用户是否已阅读安全警示
    public static void LoadConfig()//每当加载配置和保存配置的时，都会调用这个函数.用于读取配置的json文件并将配置加载到内存中(防止疯狂读取硬盘造成卡顿)
    {
    try
    {
        string DocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);//获取文档文件夹路径
        string JsonPath = DocumentsPath + "\\My Games\\Terraria\\tModLoader\\ModConfigs\\DGLABtMod_MainConfig.json";//补全配置文件完整的路径
        if (!File.Exists(JsonPath))
        {
            ConfigStore.Current = System.Text.Json.JsonSerializer.Deserialize<Configs>("{}");//加载默认值
            goto Start;
        }
        string ConfigJson;
        try
        {
            ConfigJson = File.ReadAllText(JsonPath);//将配置中的文本内容加载到ConfigJson里
        }
        catch
        {
            goto Start;
        }
        if (string.IsNullOrEmpty(ConfigJson))
        {
            goto Start;
        }
        ConfigStore.Current = System.Text.Json.JsonSerializer.Deserialize<Configs>(ConfigJson);//加载到内存中

        Debug.DebugSendMsg("调试模式已启动", 255, 255, 255);
        Main.NewText("DGLAB:新配置已加载");
    }
    catch(Exception ex)
    {
        Main.NewText("DGLAB:配置加载失败："+ex,255);
        goto Start;
    }
    Start:
    try
    {
        if (ConfigStore.Current.WARN && ConfigStore.Current.WARN1 && ConfigStore.Current.WARN2 && ConfigStore.Current.WARN3 && ConfigStore.Current.WARN4)
        {
            IsReadWarn = true;
        }
        else//v1.0.1新增代码
        {
            IsReadWarn = false;
        }

        if (!ConfigStore.Current.A)//启用通道判断
        {
            ConfigStore.Current.MaxA = 0;
            ConfigStore.Current.LowA = 0;
        }
        if (!ConfigStore.Current.B)
        {
            ConfigStore.Current.MaxB = 0;
            ConfigStore.Current.LowB = 0;
        }
    }
    catch { }
    }
        public static int t = 0;
    public override void Update(int type, Player player, ref int buffIndex)
    {


        //Main.NewText(Control.NowStrengthA);//这段要删这段要删
        //受伤型 buff
        if (
            player.HasBuff(20) ||
            player.HasBuff(24) ||
            player.HasBuff(44) ||
            player.HasBuff(67) ||
            player.HasBuff(68) ||
            player.HasBuff(334)
            )
        {
            Control.Debuff = true;
            if (ConfigStore.Current.DebugBuff)
            {
                Debug.DebugSendMsg("调试:玩家正处于受伤型buff状态", 255, 100, 100);
            }
        }
        else
        {
            Control.Debuff = false;
            if (ConfigStore.Current.DebugBuff)
            {
                Debug.DebugSendMsg("调试:玩家已脱离受伤型buff状态", 255, 100, 100);
            }
        }
        //强受伤型 buff
        if (
            player.HasBuff(70) ||
            player.HasBuff(39) ||
            player.HasBuff(144)//带电 buff,这下感同身受了
            )
        {
            Control.DebuffPlus = true;
            if (ConfigStore.Current.DebugBuff)
            {
                Debug.DebugSendMsg("调试:玩家正处于超强受伤型buff状态", 255, 0, 255);
            }
        }
        else
        {
            Control.DebuffPlus = false;
            if (ConfigStore.Current.DebugBuff)
            {
                Debug.DebugSendMsg("调试:玩家已脱离超强受伤型buff状态", 255, 0, 255);
            }
        }
        //物理干扰型buff(比如虚弱)
        if (
          player.HasBuff(30) ||
          player.HasBuff(33) ||
          player.HasBuff(38) ||
          player.HasBuff(69)
          )
        {
            Control.DebuffTrouble = true;
            if (ConfigStore.Current.DebugBuff)
            {
                Debug.DebugSendMsg("调试:玩家正处于物理干扰性buff状态", 255, 255, 100);
            }
        }
        else
        {
            Control.DebuffTrouble = false;
            //Control.BuffTroubleAdd();
            //Control.BuffTroubleAdd();
            if (ConfigStore.Current.DebugBuff)
            {
                Debug.DebugSendMsg("调试:玩家已脱离物理干扰型buff状态", 255, 255, 100);
            }
        }

        
        if (
            player.HasBuff(206) ||
            player.HasBuff(26) ||
            player.HasBuff(2) ||
            player.HasBuff(151) ||
            player.HasBuff(89) ||
            player.HasBuff(87) ||
            player.HasBuff(48)
          )
        {
            Control.DownBuff();
            if (ConfigStore.Current.DebugBuff)
            {
                Debug.DebugSendMsg("调试:玩家已处于生命回复buff状态", 255, 255, 255);
            }
        }
        else
        {
            if (ConfigStore.Current.DebugBuff)
            {
                Debug.DebugSendMsg("调试:玩家已脱离生命回复buff状态", 255, 255, 255);
            }
        }

        if (
            player.HasBuff(207) ||
            player.HasBuff(58) ||
            player.HasBuff(186)
          )
        {
            Control.DownBuffPlus();
            if (ConfigStore.Current.DebugBuff)
            {
                Debug.DebugSendMsg("调试:玩家已处于强生命回复buff状态", 255, 230, 230);
            }
        }
        else
        {
            if (ConfigStore.Current.DebugBuff)
            {
                Debug.DebugSendMsg("调试:玩家已脱离强生命回复buff状态", 255, 230, 230);
            }
        }



        if (player.HasBuff(144 ) && _144==0)//小彩蛋
        {
            Main.NewText("居然是 带电 Buff,这下感同身受了吧~",249, 228, 156);
            Console.WriteLine("居然是 带电 Buff,这下感同身受了吧~");
            Console.WriteLine("如果你在控制台看见此文本(不是指编辑器,IDE),说明你是个细心的人");//虽然正常情况下控制台是隐藏的。
            _144++;
        }
    }

}

