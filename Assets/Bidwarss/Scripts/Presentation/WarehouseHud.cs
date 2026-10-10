using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bidwarss.Domain;
using UnityEngine;

namespace Bidwarss
{
    public sealed class WarehouseHud : MonoBehaviour
    {
        GUIStyle title,bigTitle,heading,body,small,button,field;
        bool showHistory,showWorld,showSettings;
        // Underlying menus are disabled while a modal panel (records, settings) is open.
        bool modal;
        Vector2 scroll,depotScroll;
        // Record filters: 0 = every team size on the local tab, otherwise 1-4 players.
        int filterPlayers;
        bool filterDaily,filterCurrentRules=true,worldDirty;
        string rulesCache;
        RunScore[] shownLocal=new RunScore[0];
        int shownKey=int.MinValue;
        static readonly CultureInfo UsCulture=CultureInfo.GetCultureInfo("en-US");
        readonly Color ink=new Color(.045f,.06f,.09f),paper=new Color(.95f,.92f,.81f),gold=new Color(1,.74f,.22f),teal=new Color(.22f,.78f,.72f);
        void Styles()
        {
            if(title!=null)return;
            title=new GUIStyle(GUI.skin.label){fontSize=48,fontStyle=FontStyle.Bold,wordWrap=true,clipping=TextClipping.Clip,richText=false};title.normal.textColor=paper;
            bigTitle=new GUIStyle(title){fontSize=64};
            heading=new GUIStyle(title){fontSize=23};body=new GUIStyle(title){fontSize=18,fontStyle=FontStyle.Normal};
            small=new GUIStyle(body){fontSize=15};button=new GUIStyle(GUI.skin.button){fontSize=19,fontStyle=FontStyle.Bold,padding=new RectOffset(12,12,10,10)};
            field=new GUIStyle(GUI.skin.textField){fontSize=20,padding=new RectOffset(12,12,8,8)};
        }
        void Box(Rect r,Color color)
        {
            Color old=GUI.color;GUI.color=ink;GUI.DrawTexture(new Rect(r.x+5,r.y+6,r.width,r.height),Texture2D.whiteTexture);
            GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;
        }
        void Text(Rect r,string value,GUIStyle style,Color? color=null)
        {var old=style.normal.textColor;if(color.HasValue)style.normal.textColor=color.Value;GUI.Label(r,value,style);style.normal.textColor=old;}
        bool Button(Rect r,string text,Color color)
        {Color old=GUI.backgroundColor;GUI.backgroundColor=color;bool pressed=GUI.Button(r,text,button);GUI.backgroundColor=old;return pressed;}
        void Bar(Rect r,float fraction,Color fill)
        {
            Color old=GUI.color;GUI.color=new Color(.1f,.15f,.19f);GUI.DrawTexture(r,Texture2D.whiteTexture);
            GUI.color=fill;GUI.DrawTexture(new Rect(r.x,r.y,r.width*Mathf.Clamp01(fraction),r.height),Texture2D.whiteTexture);GUI.color=old;
        }
        public static string Money(int value)=>"$"+value.ToString("N0",UsCulture);
        public static string Clock(double seconds)=>TimeSpan.FromSeconds(Math.Max(0,seconds)).ToString(@"hh\:mm\:ss");
        void OnGUI()
        {
            if(Application.isBatchMode)return;
            var session=SessionMenu.Instance;if(session==null||session.network==null)return;
            Styles();float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);
            Matrix4x4 old=GUI.matrix;GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1600*scale)/2,(Screen.height-900*scale)/2,0),Quaternion.identity,Vector3.one*scale);
            var world=WarehouseWorld.Instance;var player=WarehousePlayer.Local;
            // Locking the cursor (back to the game) closes any open panel and keeps the settings.
            if(player!=null&&Cursor.lockState==CursorLockMode.Locked)
            {
                if(showSettings)GameSettings.Save();
                showHistory=false;showSettings=false;
            }
            modal=showHistory||showSettings;
            GUI.enabled=!modal;
            if(world!=null&&player!=null)
            {
                InGame(world,player);
                if(player.ResultsVisible)Results(world);
                else if(Cursor.lockState!=CursorLockMode.Locked)Pause(session,world);
            }
            else MainMenu(session);
            GUI.enabled=true;
            if(showHistory)History(world);
            if(showSettings)Settings();
            GUI.matrix=old;
        }
        void MainMenu(SessionMenu session)
        {
            Box(new Rect(90,100,600,690),new Color(.075f,.12f,.17f,.98f));
            Text(new Rect(125,123,530,75),"BIDWARSS",title,gold);
            Text(new Rect(128,202,510,60),"Kutuları aç. Değeri keşfet.\nDepoyu birlikte düzene sok.",heading);
            Text(new Rect(130,290,500,24),"OYUNCU ADI",small,teal);
            session.DisplayName=GUI.TextField(new Rect(130,321,500,43),session.DisplayName,16,field);
            Text(new Rect(130,380,500,24),"HOST ADRESİ • IP veya alan adı, isteğe bağlı :port",small,teal);
            session.Address=GUI.TextField(new Rect(130,411,500,43),session.Address,60,field);
            session.Daily=GUI.Toggle(new Rect(130,470,490,30),session.Daily," Günlük ortak depo (UTC)",body);
            if(!session.Daily)
            {
                Text(new Rect(130,510,250,25),"SEED • boş = yeni depo",small);
                session.SeedText=GUI.TextField(new Rect(395,504,235,38),session.SeedText,11,field);
            }
            GUI.enabled=!modal&&!session.network.IsListening&&!session.Restarting;
            if(Button(new Rect(130,563,240,55),"ODA KUR",gold))session.StartSession(true);
            if(Button(new Rect(390,563,240,55),"KATIL",teal))session.StartSession(false);
            GUI.enabled=!modal;
            if(session.network.IsListening)
            {
                if(Button(new Rect(130,630,500,40),"Bağlantıyı iptal et",paper))session.Leave();
            }
            else
            {
                if(Button(new Rect(130,630,240,40),"REKORLAR",paper))OpenHistory();
                if(Button(new Rect(390,630,240,40),"AYARLAR",paper))showSettings=true;
            }
            Text(new Rect(130,692,500,70),session.Status,small);
            Box(new Rect(1000,190,465,445),paper);
            Text(new Rect(1030,213,405,40),"HER EŞYANIN BİR HİKÂYESİ VAR",heading,ink);
            Text(new Rect(1030,278,405,155),"7 farklı durum\nAynı türden 10'lu istifler\nEn fazla 4 oyuncu\nBütün depo bitince tek kazanç",body,ink);
            Text(new Rect(1030,465,405,135),"İlk sürümde 10 kutu, 120 eşya.\nSüre kaydedilir; para puanından düşülmez.\nBağlantı: LAN / doğrudan IP veya alan adı\nKlavye-fare veya gamepad",small,ink);
        }
        void InGame(WarehouseWorld world,WarehousePlayer player)
        {
            Box(new Rect(28,25,395,160),new Color(.06f,.1f,.15f,.94f));
            Text(new Rect(48,36,350,30),"BIDWARSS / ANA DEPO",heading,gold);
            Text(new Rect(48,78,350,28),"Kutu "+world.OpenCount+" / "+world.Crates.Count+"     Eşya "+world.PlacedCount.Value+" / "+world.Items.Count,body);
            Bar(new Rect(48,122,350,12),world.Items.Count==0?0:(float)world.PlacedCount.Value/world.Items.Count,teal);
            Text(new Rect(48,148,350,25),Clock(world.Elapsed)+"   •   Seed "+world.Seed.Value,small);
            Box(new Rect(1180,25,390,112),new Color(.06f,.1f,.15f,.94f));
            Text(new Rect(1202,36,350,25),"YERLEŞTİRİLEN DEĞER",small,teal);
            Text(new Rect(1200,67,350,56),Money(world.SecuredDollars.Value),title,gold);
            int heldKind;int held=world.HeldCount(player.OwnerClientId,out heldKind);
            Box(new Rect(1180,158,390,held>0?175:95),new Color(.06f,.1f,.15f,.94f));
            Text(new Rect(1200,172,350,58),held>0?world.catalog.entries[heldKind].title+"  "+held+"/10":"ELLERİN BOŞ",heading);
            if(held>0)
            {
                Text(new Rect(1200,236,350,30),"Taşınan değer: "+Money(world.HeldValue(player.OwnerClientId)),body,gold);
                Text(new Rect(1200,274,350,50),"E: Aynı türü topla / istifle\nQ: Bir eşya bırak",small);
            }
            float listY=held>0?355:278;
            Text(new Rect(1200,listY,350,30),"DEPO LİSTESİ",heading,teal);
            int rowCount=0;
            for(int kind=0;kind<world.catalog.entries.Length;kind++)
            {
                for(int i=0;i<world.Stacks.Count;i++)
                    if(world.Stacks[i].kind==kind){rowCount++;break;}
            }
            float listHeight=735-listY-42;
            depotScroll=GUI.BeginScrollView(new Rect(1188,listY+42,380,listHeight),depotScroll,
                new Rect(0,0,355,Mathf.Max(listHeight,rowCount*56)));
            int row=0;
            for(int kind=0;kind<world.catalog.entries.Length;kind++)
            {
                int need=0,done=0;
                for(int i=0;i<world.Stacks.Count;i++)if(world.Stacks[i].kind==kind){need+=10;done+=world.Stacks[i].count;}
                if(need==0)continue;
                Text(new Rect(12,row*56,245,50),world.catalog.entries[kind].title,small,done==need?teal:paper);
                Text(new Rect(265,row*56,85,28),done+" / "+need,small,done==need?teal:paper);
                row++;
            }
            GUI.EndScrollView();
            if(Cursor.lockState==CursorLockMode.Locked)
            {
                Text(new Rect(791,434,30,40),"+",heading,player.Looked!=null?gold:paper);
                Box(new Rect(365,790,870,58),new Color(.045f,.06f,.09f,.93f));
                Text(new Rect(385,803,830,44),player.CurrentHint,body);
                if(player.Looked!=null && player.Looked.kind==TargetKind.Crate && player.Looked.id<world.Crates.Count)
                {
                    var crate=world.Crates[player.Looked.id];
                    if(!crate.opened)
                    {
                        Bar(new Rect(570,735,460,17),crate.progress,gold);
                        if(crate.opener!=ItemState.Nobody && crate.opener!=player.OwnerClientId)
                            Text(new Rect(590,700,450,30),"Arkadaşın kutuyu açıyor…",small);
                    }
                }
                if(Time.unscaledTime<player.ToastUntil)
                    Text(new Rect(465,675,740,45),player.Toast,heading,gold);
                Text(new Rect(38,853,1300,25),"WASD: Hareket    E: Al / istifle    E basılı: Kutu aç    Q: Bırak    ESC: Menü    •    Gamepad: sol çubuk, A, B, Start",small);
            }
        }
        void Pause(SessionMenu session,WarehouseWorld world)
        {
            bool host=session.network.IsHost;
            Box(new Rect(570,150,460,host?600:360),new Color(.06f,.1f,.15f,.98f));
            Text(new Rect(610,172,380,48),"DEPO MOLASI",heading,gold);
            Text(new Rect(610,224,380,35),"Co-op oturumu devam ediyor.",small);
            if(Button(new Rect(610,270,380,50),"OYUNA DÖN",teal))WarehousePlayer.LockCursor(true);
            if(Button(new Rect(610,333,380,50),"AYARLAR",paper))showSettings=true;
            if(Button(new Rect(610,396,380,50),"ODADAN AYRIL",paper))session.Leave();
            if(!host)return;
            Text(new Rect(610,470,380,28),"HOST",small,teal);
            session.RoomLocked=GUI.Toggle(new Rect(610,500,380,30),session.RoomLocked," Odayı kilitle (yeni oyuncu alma)",body);
            ulong kickTarget=ulong.MaxValue;int line=0;
            foreach(var other in WarehousePlayer.Players.Values)
            {
                if(other==null||other.IsLocalPlayer)continue;
                float y=545+line*50;line++;
                if(line>3)break;
                Text(new Rect(610,y+5,270,36),other.PlayerName.Value.ToString(),body);
                if(Button(new Rect(890,y,100,40),"AT",gold))kickTarget=other.OwnerClientId;
            }
            if(line==0)Text(new Rect(610,548,380,30),"Odada başka oyuncu yok.",small);
            if(kickTarget!=ulong.MaxValue)session.Kick(kickTarget);
        }
        void Results(WarehouseWorld world)
        {
            Box(new Rect(330,90,940,720),new Color(.06f,.1f,.15f,.99f));
            Text(new Rect(370,115,850,70),"DEPO TAMAMLANDI!",title,gold);
            Text(new Rect(370,192,840,45),world.TeamNames.Value.ToString(),body);
            Text(new Rect(370,249,800,85),Money(world.FinalDollars.Value),bigTitle,teal);
            Text(new Rect(375,342,820,36),world.Items.Count+" eşya • "+world.Stacks.Count+" tam istif • "+Clock(world.Elapsed)+" • "+world.PeakPlayers.Value+" oyuncu",body);
            for(int c=0;c<7;c++)
            {
                int count=0,value=0;
                for(int i=0;i<world.Items.Count;i++)if((int)world.Items[i].condition==c){count++;value+=world.Items[i].dollars;}
                float x=375+(c%4)*215,y=403+(c/4)*75;
                Text(new Rect(x,y,205,27),GameRules.ConditionNames[c]+" • "+count,small,ItemVisual.ConditionColors[c]);
                Text(new Rect(x,y+29,205,30),Money(value),heading);
            }
            Text(new Rect(375,564,820,44),"Kazanç eşya değerlerinin toplamıdır. Süre şu an puanı değiştirmez.",small);
            Text(new Rect(375,606,820,44),ScoreHistory.LastError??"Sonuç bu bilgisayardaki rekor geçmişine kaydedildi.",small);
            if(Button(new Rect(375,662,255,50),"REKORLAR",paper))OpenHistory();
            if(SessionMenu.Instance.network.IsHost)
            {
                if(Button(new Rect(650,662,255,50),"AYNI DEPO",teal))SessionMenu.Instance.NewRound(true);
                if(Button(new Rect(925,662,255,50),"YENİ DEPO",gold))SessionMenu.Instance.NewRound(false);
            }
            if(Button(new Rect(375,727,805,40),"Depoya bak • TAB ile sonuçlara geri dön",paper))WarehousePlayer.Local.CloseResults();
        }
        void OpenHistory()
        {
            showHistory=true;showWorld=false;rulesCache=null;shownKey=int.MinValue;
        }
        string CurrentRules(WarehouseWorld world)
        {
            if(rulesCache==null)rulesCache=world!=null?world.RulesHash.Value.ToString():SessionMenu.Instance.sceneWorld.Rules.Fingerprint();
            return rulesCache;
        }
        int WorldPlayers(WarehouseWorld world)
        {
            if(filterPlayers>0)return filterPlayers;
            return world!=null?Mathf.Clamp(world.PeakPlayers.Value,1,4):1;
        }
        // Rebuilt only when a filter or the stored results change, not on every IMGUI event.
        RunScore[] LocalEntries(WarehouseWorld world)
        {
            var all=ScoreHistory.Entries;
            int daily=SessionMenu.DailySeed();
            int key=unchecked((((filterPlayers*31+(filterDaily?1:0))*31+(filterCurrentRules?1:0))*31+all.Count)*31+(filterDaily?daily:0));
            if(key!=shownKey)
            {
                shownKey=key;
                string rules=filterCurrentRules?CurrentRules(world):null;
                var list=new List<RunScore>();
                foreach(var s in all)
                {
                    if(filterPlayers>0&&s.player_count!=filterPlayers)continue;
                    if(rules!=null&&s.rules_hash!=rules)continue;
                    if(filterDaily&&s.seed!=daily)continue;
                    list.Add(s);
                }
                shownLocal=list.ToArray();
            }
            return shownLocal;
        }
        void History(WarehouseWorld world)
        {
            Box(new Rect(260,80,1080,750),new Color(.04f,.07f,.11f,1));
            Text(new Rect(295,101,850,55),"REKOR TABLOSU",title,gold);
            if(Button(new Rect(1180,100,120,45),"KAPAT",paper))showHistory=false;
            if(Button(new Rect(295,182,360,45),"BU BİLGİSAYAR",showWorld?paper:teal))showWorld=false;
            if(Button(new Rect(675,182,360,45),"DÜNYA",showWorld?teal:paper)){showWorld=true;worldDirty=true;}
            // Filters: team size, today's shared depot and (local only) the current rule set.
            GUI.enabled=!showWorld;
            if(Button(new Rect(295,238,100,40),"TÜMÜ",filterPlayers==0?teal:paper)){filterPlayers=0;}
            GUI.enabled=true;
            for(int n=1;n<=4;n++)
                if(Button(new Rect(295+n*108,238,100,40),n+" KİŞİ",filterPlayers==n?teal:paper)){filterPlayers=n;worldDirty=true;}
            if(Button(new Rect(735,238,170,40),"GÜNLÜK",filterDaily?gold:paper)){filterDaily=!filterDaily;worldDirty=true;}
            if(!showWorld&&Button(new Rect(915,238,230,40),"BU KURALLAR",filterCurrentRules?teal:paper))filterCurrentRules=!filterCurrentRules;
            var online=LeaderboardClient.Instance;
            if(showWorld&&worldDirty&&online!=null&&!online.Busy)
            {
                worldDirty=false;
                online.Refresh(CurrentRules(world),WorldPlayers(world),filterDaily?(int?)SessionMenu.DailySeed():null);
            }
            if(showWorld&&online==null)worldDirty=false;
            string note=showWorld
                ?(online!=null?online.Status:"Sıralama servisi bağlı değil.")
                :"Yerel kayıtlar • doğrulanmamış • ilk 100 sonuç"+(filterCurrentRules?" • yalnız bu kurallar":" • tüm kurallar");
            Text(new Rect(295,288,1000,30),note,small);
            var entries=showWorld?(online!=null?online.Entries:new RunScore[0]):LocalEntries(world);
            scroll=GUI.BeginScrollView(new Rect(290,326,1010,447),scroll,new Rect(0,0,970,Math.Max(420,entries.Length*65)));
            for(int i=0;i<entries.Length;i++)
            {
                var s=entries[i];float y=i*65;
                Text(new Rect(8,y,55,35),(i+1).ToString("00"),heading,teal);
                Text(new Rect(72,y,430,33),s.team,body);
                Text(new Rect(72,y+30,650,25),s.player_count+" kişi • "+Clock(s.elapsed_milliseconds/1000d)+" • Seed "+s.seed+" • "+s.rules_hash.Substring(0,Math.Min(8,s.rules_hash.Length)),small);
                Text(new Rect(735,y,230,42),Money(s.total_dollars),heading,gold);
            }
            if(entries.Length==0)Text(new Rect(12,15,850,55),showWorld?"Bu kategoride henüz skor yok.":"Bu filtre için sonuç yok. İlk depoyu tamamla!",body);
            GUI.EndScrollView();
        }
        float Slider(float y,string label,float value,float min,float max,string format)
        {
            Text(new Rect(520,y,420,28),label,small,teal);
            float next=GUI.HorizontalSlider(new Rect(520,y+38,420,24),value,min,max);
            Text(new Rect(960,y+28,120,30),next.ToString(format,CultureInfo.InvariantCulture),small);
            return next;
        }
        void Settings()
        {
            Box(new Rect(480,130,640,640),new Color(.04f,.07f,.11f,1));
            Text(new Rect(520,151,420,55),"AYARLAR",title,gold);
            if(Button(new Rect(960,155,120,45),"KAPAT",paper)){GameSettings.Save();showSettings=false;}
            GameSettings.MouseSensitivity=Slider(240,"Fare / sağ çubuk hassasiyeti",GameSettings.MouseSensitivity,GameSettings.MinSensitivity,GameSettings.MaxSensitivity,"0.00");
            GameSettings.Volume=Slider(330,"Ses seviyesi",GameSettings.Volume,0,1,"0%");
            GameSettings.MusicVolume=Slider(420,"Müzik",GameSettings.MusicVolume,0,1,"0%");
            GameSettings.Fov=Slider(510,"Görüş alanı",GameSettings.Fov,GameSettings.MinFov,GameSettings.MaxFov,"0");
            if(Event.current.type==EventType.MouseUp)GameSettings.Save();
            Text(new Rect(520,600,560,60),"WASD / sol çubuk: hareket • E / A: al, istifle, kutu aç (basılı)\nQ / B: bırak • ESC / Start: menü • TAB / Geri: sonuç",small);
            if(Button(new Rect(520,684,250,48),"VARSAYILANLAR",paper))GameSettings.ResetDefaults();
        }
    }
}
