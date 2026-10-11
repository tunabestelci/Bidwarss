using System;
using System.Linq;
using Bidwarss.Domain;
using UnityEngine;

namespace Bidwarss
{
    // All 2D interface. Virtual canvas is 1600 x 900 and is scaled to fit the window.
    public sealed class WarehouseHud : MonoBehaviour
    {
        static readonly string[] ShortNames = { "Koşu", "Tarayıcı", "Mıknatıs", "Dron" };
        GUIStyle field;
        bool showHistory, showWorld, showAchievements;
        Vector2 scroll, achievementScroll;
        float displayMoney, moneyPulse, hoverAlpha, resultsStart = -10;
        int lastMoneyTarget;
        bool wasResults;
        float scale = 1;
        Vector2 offset;
        InteractionTarget lastLooked;
        readonly Color ink = UiKit.Ink, paper = UiKit.Paper, gold = UiKit.Gold, teal = UiKit.Teal;

        public static string Money(int value) { return FeedbackHub.Money(value); }
        // "3,5 kg  •  45 x 28 x 10 cm" (Kasa Defteri)
        static string SizeInfo(ItemCatalog.Entry e)
        {
            string s = e.kg > 0f ? e.kg.ToString(e.kg < 10f ? "0.##" : "0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + " kg" : "";
            Vector3 r = e.realSize;
            if (r.x > 0f && r.y > 0f && r.z > 0f)
                s += (s.Length > 0 ? "  •  " : "") + Mathf.RoundToInt(r.y * 100f) + " x " + Mathf.RoundToInt(r.x * 100f) + " x " + Mathf.RoundToInt(r.z * 100f) + " cm";
            return s;
        }
        public static string Clock(double seconds) { return TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"hh\:mm\:ss"); }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var world = WarehouseWorld.Instance;
            var player = WarehousePlayer.Local;
            if (world != null && world.IsSpawned)
            {
                int target = world.Completed.Value ? world.FinalDollars.Value : world.SecuredDollars.Value;
                if (target > lastMoneyTarget) moneyPulse = 1f;
                lastMoneyTarget = target;
                displayMoney = Mathf.Lerp(displayMoney, target, 1 - Mathf.Exp(-7f * dt));
                if (Mathf.Abs(displayMoney - target) < .6f) displayMoney = target;
            }
            moneyPulse = Mathf.MoveTowards(moneyPulse, 0, dt * 3f);
            bool hasTarget = player != null && player.Looked != null && Cursor.lockState == CursorLockMode.Locked;
            if (hasTarget) lastLooked = player.Looked;
            hoverAlpha = Mathf.MoveTowards(hoverAlpha, hasTarget ? 1f : 0f, dt * 7f);
        }

        void Styles()
        {
            if (field != null) return;
            field = new GUIStyle(GUI.skin.textField) { fontSize = 20, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 8, 4, 4) };
            foreach (var state in new[] { field.normal, field.hover, field.active, field.focused, field.onNormal, field.onHover, field.onActive, field.onFocused })
            { state.background = null; state.textColor = paper; }
            field.focused.textColor = Color.white;
        }

        void OnGUI()
        {
            if (Application.isBatchMode) return;
            var session = SessionMenu.Instance; if (session == null || session.network == null) return;
            Styles();
            scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
            offset = new Vector2((Screen.width - 1600 * scale) / 2, (Screen.height - 900 * scale) / 2);
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offset.x, offset.y, 0), Quaternion.identity, Vector3.one * scale);
            var world = WarehouseWorld.Instance; var player = WarehousePlayer.Local;
            if (world != null && player != null)
            {
                InGame(world, player);
                if (player.ResultsVisible) Results(world);
                else { wasResults = false; if (Cursor.lockState != CursorLockMode.Locked) Pause(session, world); }
            }
            else MainMenu(session);
            if (showHistory) History(world);
            if (showAchievements) AchievementsPanel();
            GUI.matrix = old;
        }

        // ================= Main menu ===========================================================================
        void MainMenu(SessionMenu session)
        {
            UiKit.Card(new Rect(90, 90, 620, 730), new Color(.06f, .09f, .14f, .96f), 22);
            UiKit.Fill(new Rect(90, 90, 620, 8), gold, 4);
            UiKit.Text(new Rect(130, 120, 540, 80), "BIDWARSS", 66, gold, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Text(new Rect(132, 205, 540, 60), "Kutuları aç. Değeri keşfet.\nDepoyu birlikte düzene sok.", 22, paper);
            UiKit.Text(new Rect(132, 292, 500, 22), "OYUNCU ADI", 14, teal, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Fill(new Rect(130, 318, 500, 46), new Color(.03f, .05f, .08f, .95f), 10);
            session.DisplayName = GUI.TextField(new Rect(134, 320, 492, 42), session.DisplayName, 16, field);
            UiKit.Text(new Rect(132, 380, 500, 22), "HOST ADRESİ • aynı PC için 127.0.0.1", 14, teal, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Fill(new Rect(130, 406, 500, 46), new Color(.03f, .05f, .08f, .95f), 10);
            session.Address = GUI.TextField(new Rect(134, 408, 492, 42), session.Address, 45, field);
            session.Daily = UiKit.Toggle(new Rect(132, 470, 490, 30), session.Daily, "Günlük ortak depo (UTC)");
            if (!session.Daily)
            {
                UiKit.Text(new Rect(132, 512, 250, 26), "SEED • boş = yeni depo", 15, UiKit.Dim);
                UiKit.Fill(new Rect(395, 506, 235, 40), new Color(.03f, .05f, .08f, .95f), 10);
                session.SeedText = GUI.TextField(new Rect(399, 507, 227, 38), session.SeedText, 11, field);
            }
            GUI.enabled = !session.network.IsListening && !session.Restarting;
            if (UiKit.Button(new Rect(130, 570, 240, 58), "ODA KUR", gold, 22)) session.StartSession(true);
            if (UiKit.Button(new Rect(390, 570, 240, 58), "KATIL", teal, 22)) session.StartSession(false);
            GUI.enabled = true;
            if (session.network.IsListening)
            {
                if (UiKit.Button(new Rect(130, 646, 500, 44), "Bağlantıyı iptal et", paper, 18)) session.Leave();
            }
            else
            {
                if (UiKit.Button(new Rect(130, 646, 240, 44), "REKORLAR", paper, 18)) { showHistory = true; showWorld = false; }
                if (UiKit.Button(new Rect(390, 646, 240, 44), "BAŞARIMLAR", paper, 18)) showAchievements = true;
            }
            UiKit.Text(new Rect(132, 712, 500, 90), session.Status, 16, UiKit.Dim, TextAnchor.UpperLeft, FontStyle.Normal, false, true);

            UiKit.Card(new Rect(980, 150, 520, 560), paper, 22);
            UiKit.Text(new Rect(1012, 176, 456, 40), "HER EŞYANIN BİR HİKÂYESİ VAR", 24, ink, TextAnchor.UpperLeft, FontStyle.Bold, false);
            string[] lines = { "10 farklı eşya türü, 7 durum", "Aynı türden 10'lu istifler", "Tarayıcı, mıknatıs, dron: depo düzeldikçe yeni aletler", "En fazla 4 oyuncu, tek ortak kazanç", "Efsane eşyalar parlar. Onları bul." };
            for (int i = 0; i < lines.Length; i++)
            {
                UiKit.Fill(new Rect(1014, 240 + i * 48 + 8, 12, 12), i == 4 ? gold : new Color(.16f, .55f, .52f), 6);
                UiKit.Text(new Rect(1040, 236 + i * 48, 430, 44), lines[i], 19, ink, TextAnchor.UpperLeft, FontStyle.Normal, false, true);
            }
            UiKit.Fill(new Rect(1012, 496, 456, 2), new Color(0, 0, 0, .12f));
            UiKit.Text(new Rect(1012, 512, 456, 24), "KARİYERİN", 14, new Color(.16f, .5f, .47f), TextAnchor.UpperLeft, FontStyle.Bold, false);
            UiKit.Text(new Rect(1012, 538, 456, 90),
                Achievements.LifetimePlaced.ToString("N0") + " eşya yerleştirdin • " + Achievements.LifetimeDepots + " depo tamamladın\n" +
                Achievements.UnlockedCount + " / " + Achievements.All.Length + " başarım • toplam " + Money(Achievements.LifetimeMoney),
                17, ink, TextAnchor.UpperLeft, FontStyle.Normal, false, true);
            UiKit.Text(new Rect(1012, 640, 456, 50), "Bağlantı: LAN / doğrudan IP • UDP 7777", 14, new Color(.3f, .32f, .35f), TextAnchor.UpperLeft, FontStyle.Normal, false, true);
        }

        // ================= In-game =============================================================================
        Vector2 ToGui(Vector3 screen) { return new Vector2((screen.x - offset.x) / scale, (Screen.height - screen.y - offset.y) / scale); }

        void InGame(WarehouseWorld world, WarehousePlayer player)
        {
            int total = Mathf.Max(1, world.Items.Count), placed = world.PlacedCount.Value;
            float progress = (float)placed / total;
            int stacksDone = 0;
            for (int i = 0; i < world.Stacks.Count; i++) if (world.Stacks[i].Full) stacksDone++;

            // Mission card
            UiKit.Card(new Rect(24, 22, 410, 186), UiKit.Panel);
            UiKit.Text(new Rect(46, 34, 250, 28), "ANA DEPO", 22, gold, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Text(new Rect(292, 36, 122, 26), Clock(world.Elapsed), 20, paper, TextAnchor.UpperRight, FontStyle.Bold);
            UiKit.Bar(new Rect(46, 72, 366, 18), progress, teal);
            UiKit.Text(new Rect(46, 72, 366, 18), Mathf.FloorToInt(progress * 100) + "%", 12, ink, TextAnchor.MiddleCenter, FontStyle.Bold, false);
            Chip(new Rect(46, 102, 114, 60), "KASA", world.OpenCount + "/" + world.ActiveBoxCount);
            Chip(new Rect(172, 102, 114, 60), "EŞYA", placed + "/" + total);
            Chip(new Rect(298, 102, 114, 60), "PALET", stacksDone + "/" + world.Stacks.Count);
            UiKit.Text(new Rect(46, 172, 366, 18), "Seed " + world.Seed.Value + "  •  " + Mathf.Max(1, WarehousePlayer.Players.Count) + " oyuncu", 12, UiKit.Dim, TextAnchor.UpperLeft, FontStyle.Normal, false);

            // Money card
            UiKit.Card(new Rect(1150, 22, 426, 114), UiKit.Panel);
            UiKit.Text(new Rect(1174, 34, 380, 22), world.Completed.Value ? "TOPLAM KAZANÇ" : "YERLEŞTİRİLEN DEĞER", 14, teal, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Text(new Rect(1174, 56, 380, 64), Money((int)displayMoney), Mathf.RoundToInt(50 * (1 + moneyPulse * .14f)), Color.Lerp(gold, Color.white, moneyPulse * .5f), TextAnchor.UpperLeft, FontStyle.Bold);

            // Hands
            int heldKind; int held = world.HeldCount(player.OwnerClientId, out heldKind);
            float listY;
            if (held > 0)
            {
                var entry = world.catalog.entries[heldKind];
                UiKit.Card(new Rect(1150, 150, 426, 122), UiKit.Panel);
                UiKit.Fill(new Rect(1174, 168, 16, 16), entry.color, 8);
                UiKit.Text(new Rect(1198, 162, 260, 30), entry.title, 22, paper, TextAnchor.UpperLeft, FontStyle.Bold);
                UiKit.Text(new Rect(1440, 162, 112, 30), held + " / " + GameRules.StackSize, 22, held >= GameRules.StackSize ? gold : paper, TextAnchor.UpperRight, FontStyle.Bold);
                UiKit.Pips(new Rect(1174, 198, 378, 14), held, GameRules.StackSize, entry.color);
                UiKit.Text(new Rect(1174, 222, 380, 24), "Taşınan değer: " + Money(world.HeldValue(player.OwnerClientId)), 17, gold);
                UiKit.Text(new Rect(1174, 246, 380, 20), "E: Aynı türü topla  •  Q: Bırak  •  G: Dron", 13, UiKit.Dim, TextAnchor.UpperLeft, FontStyle.Normal, false);
                listY = 286;
            }
            else
            {
                UiKit.Card(new Rect(1150, 150, 426, 54), UiKit.Panel);
                UiKit.Text(new Rect(1174, 163, 380, 30), "ELLERİN BOŞ  •  bir eşya al", 18, UiKit.Dim, TextAnchor.UpperLeft, FontStyle.Bold);
                listY = 218;
            }

            // Category list
            int rows = 0;
            for (int kind = 0; kind < world.catalog.entries.Length; kind++) if (StackNeed(world, kind) > 0) rows++;
            UiKit.Card(new Rect(1150, listY, 426, 52 + rows * 34), UiKit.Panel);
            UiKit.Text(new Rect(1174, listY + 12, 300, 26), "DEPO LİSTESİ", 16, teal, TextAnchor.UpperLeft, FontStyle.Bold);
            int row = 0;
            for (int kind = 0; kind < world.catalog.entries.Length; kind++)
            {
                int need = StackNeed(world, kind); if (need == 0) continue;
                int done = StackDone(world, kind);
                float y = listY + 46 + row * 34; row++;
                if (kind == world.GuideKind) UiKit.Outline(new Rect(1164, y - 3, 398, 32), gold, 2f, 10);
                UiKit.Fill(new Rect(1176, y + 6, 14, 14), world.catalog.entries[kind].color, 7);
                UiKit.Text(new Rect(1200, y, 150, 26), world.catalog.entries[kind].title, 16, done == need ? teal : paper, TextAnchor.MiddleLeft, FontStyle.Normal, false);
                UiKit.Bar(new Rect(1350, y + 8, 120, 10), (float)done / need, done == need ? teal : gold);
                UiKit.Text(new Rect(1478, y, 78, 26), done + "/" + need, 15, done == need ? teal : paper, TextAnchor.MiddleRight, FontStyle.Bold, false);
            }

            Toolbelt(world, player, placed, total);
            Hotbar(world, player);

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Crosshair(world, player);
                if (!string.IsNullOrEmpty(player.CurrentHint))
                {
                    UiKit.Card(new Rect(500, 812, 600, 52), UiKit.Panel, 26);
                    UiKit.Text(new Rect(520, 812, 560, 52), player.CurrentHint, 19, paper, TextAnchor.MiddleCenter, FontStyle.Normal, false);
                }
                if (Time.unscaledTime < player.ToastUntil)
                {
                    float fade = Mathf.Clamp01((player.ToastUntil - Time.unscaledTime) / .4f);
                    var old = GUI.color; GUI.color = new Color(1, 1, 1, fade);
                    UiKit.Card(new Rect(450, 735, 700, 52), new Color(.16f, .1f, .05f, .95f), 26);
                    UiKit.Text(new Rect(470, 735, 660, 52), player.Toast, 20, gold, TextAnchor.MiddleCenter, FontStyle.Bold, false);
                    GUI.color = old;
                }
                UiKit.Text(new Rect(0, 874, 1600, 22), "WASD hareket  •  1 eller  •  2 kasa açıcı  •  3 maket bıçağı  •  E al / istifle  •  E basılı: kapı / kasa / koli aç  •  Q bırak  •  SHIFT koş  •  SPACE zıpla  •  F tarayıcı  •  G dron  •  ESC menü", 13, new Color(1, 1, 1, .5f), TextAnchor.MiddleCenter, FontStyle.Normal, true);
            }
            Toasts();
            Popups(player);
        }

        static int StackNeed(WarehouseWorld world, int kind)
        { int n = 0; for (int i = 0; i < world.Stacks.Count; i++) if (world.Stacks[i].kind == kind) n += world.Stacks[i].Cap; return n; }
        static int StackDone(WarehouseWorld world, int kind)
        { int n = 0; for (int i = 0; i < world.Stacks.Count; i++) if (world.Stacks[i].kind == kind) n += world.Stacks[i].count; return n; }

        void Chip(Rect r, string label, string value)
        {
            UiKit.Fill(r, UiKit.PanelLight, 12);
            UiKit.Text(new Rect(r.x + 12, r.y + 8, r.width - 16, 16), label, 12, teal, TextAnchor.UpperLeft, FontStyle.Bold, false);
            UiKit.Text(new Rect(r.x + 12, r.y + 24, r.width - 16, 30), value, 25, paper, TextAnchor.UpperLeft, FontStyle.Bold);
        }

        void Toolbelt(WarehouseWorld world, WarehousePlayer player, int placed, int total)
        {
            for (int i = 0; i < Upgrades.Count; i++)
            {
                var upgrade = (Upgrade)i;
                bool unlocked = Upgrades.Unlocked(upgrade, placed, total);
                float progress = Upgrades.Progress(upgrade, placed, total);
                bool active = unlocked && ((upgrade == Upgrade.Scanner && player.ScannerOn) || (upgrade == Upgrade.Sprint && player.SprintActive));
                var slot = new Rect(24 + i * 112, 790, 104, 100);
                UiKit.Card(slot, unlocked ? UiKit.Panel : new Color(.05f, .07f, .1f, .88f), 16);
                if (active) UiKit.Outline(slot, Color.Lerp(gold, Color.white, .5f + .5f * Mathf.Sin(Time.unscaledTime * 8f)), 3f, 16);
                else if (unlocked) UiKit.Outline(slot, new Color(teal.r, teal.g, teal.b, .8f), 2f, 16);
                UiKit.Icon(upgrade, new Vector2(slot.x + 52, slot.y + 38), 40, unlocked ? (active ? gold : paper) : new Color(.5f, .55f, .62f, .6f));
                UiKit.Pill(new Rect(slot.x + 8, slot.y + 8, Upgrades.Keys[i].Length * 8 + 14, 18), Upgrades.Keys[i], new Color(1, 1, 1, unlocked ? .16f : .08f), unlocked ? paper : UiKit.Dim, 11);
                UiKit.Text(new Rect(slot.x, slot.y + 64, slot.width, 18), ShortNames[i], 13, unlocked ? paper : UiKit.Dim, TextAnchor.UpperCenter, FontStyle.Bold, false);
                if (unlocked) UiKit.Text(new Rect(slot.x, slot.y + 80, slot.width, 16), active ? "AKTİF" : "AÇIK", 11, active ? gold : teal, TextAnchor.UpperCenter, FontStyle.Bold, false);
                else
                {
                    UiKit.Bar(new Rect(slot.x + 12, slot.y + 84, slot.width - 24, 8), progress, gold);
                    UiKit.Text(new Rect(slot.x, slot.y + 6, slot.width - 8, 18), "%" + Upgrades.UnlockPercent[i], 12, gold, TextAnchor.UpperRight, FontStyle.Bold, false);
                }
            }
        }

        // Quick slots above the tool belt: 1 = bare hands, 2 = crate opener, 3 = box cutter.
        void Hotbar(WarehouseWorld world, WarehousePlayer player)
        {
            int heldKind; bool carrying = world.HeldCount(player.OwnerClientId, out heldKind) > 0;
            string[] names = { "Eller", "Kasa Açıcı", "Maket Bıçağı" };
            for (int i = 0; i < WarehousePlayer.SlotCount; i++)
            {
                bool active = player.Tool == i;
                bool blocked = i != WarehousePlayer.HandsSlot && carrying;
                var slot = new Rect(24 + i * 112, 676, 104, 100);
                float pop = active ? Mathf.Clamp01((Time.unscaledTime - player.ToolSwitchTime) / .18f) : 0;
                UiKit.Card(slot, active ? new Color(.12f, .17f, .24f, .97f) : UiKit.Panel, 16);
                if (active) UiKit.Outline(slot, Color.Lerp(gold, Color.white, .25f + .25f * Mathf.Sin(Time.unscaledTime * 6f) * pop), 3f, 16);
                else UiKit.Outline(slot, new Color(1, 1, 1, .12f), 1.5f, 16);
                var c = new Vector2(slot.x + 52, slot.y + 42);
                Color ink2 = active ? gold : (blocked ? new Color(.5f, .55f, .62f, .6f) : paper);
                if (i == 0)
                {
                    // work glove: palm, thumb and four fingers
                    UiKit.Fill(new Rect(c.x - 15, c.y - 4, 30, 24), ink2, 9);
                    UiKit.Fill(new Rect(c.x - 22, c.y + 2, 11, 16), ink2, 5);
                    for (int f = 0; f < 4; f++) UiKit.Fill(new Rect(c.x - 15 + f * 8, c.y - 20 + (f == 1 || f == 2 ? -3 : 0), 7, 20), ink2, 3.5f);
                    UiKit.Fill(new Rect(c.x - 15, c.y + 14, 30, 6), new Color(1f, .76f, .17f, active ? 1f : .55f), 3);
                }
                else if (i == WarehousePlayer.CutterSlot)
                {
                    // box cutter: yellow body, dark grip line, steel blade sticking out
                    var old = GUI.matrix;
                    GUIUtility.RotateAroundPivot(-30, c);
                    UiKit.Fill(new Rect(c.x + 14, c.y - 4, 18, 7), blocked ? ink2 : new Color(.86f, .9f, .95f), 2);
                    UiKit.Fill(new Rect(c.x - 30, c.y - 7, 46, 14), blocked ? ink2 : new Color(1f, .82f, .12f), 6);
                    UiKit.Fill(new Rect(c.x - 24, c.y - 1.5f, 30, 3), blocked ? ink2 : new Color(.15f, .16f, .2f), 1.5f);
                    UiKit.Fill(new Rect(c.x - 4, c.y - 11, 8, 6), blocked ? ink2 : new Color(.15f, .16f, .2f), 2);
                    GUI.matrix = old;
                }
                else
                {
                    // pry bar drawn at an angle
                    var old = GUI.matrix;
                    GUIUtility.RotateAroundPivot(-38, c);
                    UiKit.Fill(new Rect(c.x - 6, c.y - 3.5f, 38, 7), blocked ? ink2 : new Color(.78f, .82f, .88f), 3);
                    UiKit.Fill(new Rect(c.x - 34, c.y - 5, 30, 10), blocked ? ink2 : new Color(1f, .76f, .17f), 5);
                    UiKit.Fill(new Rect(c.x + 26, c.y - 12, 7, 15), blocked ? ink2 : new Color(.78f, .82f, .88f), 3);
                    GUI.matrix = old;
                }
                UiKit.Pill(new Rect(slot.x + 8, slot.y + 8, 24, 18), (i + 1).ToString(), new Color(1, 1, 1, active ? .22f : .12f), active ? gold : UiKit.Dim, 12);
                UiKit.Text(new Rect(slot.x, slot.y + 70, slot.width, 20), names[i], 14, active ? gold : (blocked ? UiKit.Dim : paper), TextAnchor.UpperCenter, FontStyle.Bold, false);
                if (blocked) UiKit.Text(new Rect(slot.x, slot.y + 8, slot.width - 8, 18), "DOLU", 11, UiKit.Dim, TextAnchor.UpperRight, FontStyle.Bold, false);
            }
        }

        void Crosshair(WarehouseWorld world, WarehousePlayer player)
        {
            var center = new Vector2(800, 450);
            var looked = player.Looked;
            bool targeted = looked != null;
            UiKit.Fill(new Rect(center.x - 3, center.y - 3, 6, 6), targeted ? gold : new Color(1, 1, 1, .85f), 3);
            if (targeted) UiKit.Outline(new Rect(center.x - 13, center.y - 13, 26, 26), new Color(gold.r, gold.g, gold.b, .9f), 2f, 13);
            if (looked != null && looked.kind == TargetKind.Box && looked.id >= 0 && looked.id < world.Crates.Count)
            {
                var crate = world.Crates[looked.id];
                if (!crate.opened && crate.progress > 0) UiKit.Ring(center, 30, crate.progress, world.IsCardboard(looked.id) ? new Color(1f, .82f, .12f) : gold, 36, 6);
            }
            if (looked != null && looked.kind == TargetKind.Crate && looked.id >= 0 && looked.id < world.Doors.Count)
            {
                var door = world.Doors[looked.id];
                if (!door.opened && door.progress > 0) UiKit.Ring(center, 30, door.progress, teal, 36, 6);
            }
            if (hoverAlpha <= .01f || lastLooked == null) return;
            var old = GUI.color; GUI.color = new Color(1, 1, 1, hoverAlpha);
            var card = new Rect(center.x - 165, center.y + 46 - (1 - hoverAlpha) * 8, 330, 100);
            if (lastLooked.kind == TargetKind.Item && lastLooked.id >= 0 && lastLooked.id < world.Items.Count)
            {
                var item = world.Items[lastLooked.id];
                if (item.location != ItemLocation.Sealed && item.kind >= 0 && item.kind < world.catalog.entries.Length)
                {
                    int c = (int)item.condition;
                    UiKit.Card(card, UiKit.Panel, 18);
                    if (item.condition == ItemCondition.Legendary) UiKit.Outline(card, new Color(gold.r, gold.g, gold.b, .6f + .4f * Mathf.Sin(Time.unscaledTime * 6f)), 2.5f, 18);
                    UiKit.Text(new Rect(card.x + 18, card.y + 12, 290, 30), world.catalog.entries[item.kind].title, 24, paper, TextAnchor.UpperLeft, FontStyle.Bold);
                    UiKit.Pill(new Rect(card.x + 18, card.y + 54, 128, 28), GameRules.ConditionNames[c], ItemVisual.ConditionColors[c], ink, 15);
                    UiKit.Text(new Rect(card.x + 150, card.y + 46, 164, 44), Money(item.dollars), 32, gold, TextAnchor.MiddleRight, FontStyle.Bold);
                    var en = world.catalog.entries[item.kind];
                    string info = SizeInfo(en);
                    if (info.Length > 0)
                    {
                        var sub = new Rect(card.x, card.yMax - 2, card.width, 30);
                        UiKit.Card(sub, UiKit.PanelLight, 12);
                        UiKit.Text(new Rect(sub.x + 18, sub.y + 4, sub.width - 36, 22), info, 15, UiKit.Dim, TextAnchor.MiddleLeft, FontStyle.Normal, false);
                    }
                }
            }
            else if (lastLooked.kind == TargetKind.Slot && lastLooked.id >= 0 && lastLooked.id < world.Stacks.Count)
            {
                var stack = world.Stacks[lastLooked.id]; var entry = world.catalog.entries[stack.kind];
                int heldKind; int held = world.HeldCount(player.OwnerClientId, out heldKind);
                UiKit.Card(card, UiKit.Panel, 18);
                UiKit.Fill(new Rect(card.x + 18, card.y + 20, 16, 16), entry.color, 8);
                UiKit.Text(new Rect(card.x + 42, card.y + 12, 270, 30), entry.title + " Paleti", 24, paper, TextAnchor.UpperLeft, FontStyle.Bold);
                UiKit.Pips(new Rect(card.x + 18, card.y + 52, 294, 12), stack.count, stack.Cap, stack.Full ? teal : entry.color);
                string note = stack.Full ? "Palet dolu" : (held == 0 ? "Bu türden eşyalar buraya" : (heldKind == stack.kind ? "E: İstifle" : "Elindekiler farklı tür"));
                UiKit.Text(new Rect(card.x + 18, card.y + 70, 294, 22), note, 15, held > 0 && heldKind == stack.kind ? teal : UiKit.Dim, TextAnchor.UpperLeft, FontStyle.Normal, false);
            }
            else if (lastLooked.kind == TargetKind.Box && lastLooked.id >= 0 && lastLooked.id < world.Crates.Count)
            {
                var crate = world.Crates[lastLooked.id];
                bool cardboard = world.IsCardboard(lastLooked.id);
                UiKit.Card(card, UiKit.Panel, 18);
                UiKit.Text(new Rect(card.x + 18, card.y + 12, 290, 30), (cardboard ? "KOLİ " : "KASA ") + world.ActiveBoxNumber(lastLooked.id) + " / " + world.ActiveBoxCount, 24, paper, TextAnchor.UpperLeft, FontStyle.Bold);
                if (crate.opened)
                {
                    int inside = world.ItemsInside(lastLooked.id);
                    UiKit.Text(new Rect(card.x + 18, card.y + 54, 290, 26), inside > 0 ? "Açık • içinde " + inside + " eşya var" : "Boş", 18, inside > 0 ? teal : UiKit.Dim, TextAnchor.UpperLeft, FontStyle.Normal, false);
                }
                else
                {
                    UiKit.Bar(new Rect(card.x + 18, card.y + 56, 294, 14), crate.progress, cardboard ? new Color(1f, .82f, .12f) : gold);
                    bool tool = player.Tool == world.ToolFor(lastLooked.id);
                    string working = cardboard ? "Bant kesiliyor…" : "Vidalar sökülüyor…";
                    string text = crate.opener != ItemState.Nobody && crate.opener != player.OwnerClientId ? "Arkadaşın açıyor…" :
                        (!tool ? (cardboard ? "3 ile maket bıçağını al" : "2 ile kasa açıcıyı al") :
                        (crate.opener == player.OwnerClientId ? working : (crate.progress > 0 ? "E'yi basılı tut • kaldığı yerden devam" : "E'yi basılı tut")));
                    UiKit.Text(new Rect(card.x + 18, card.y + 74, 294, 22), text, 15, tool ? UiKit.Dim : gold, TextAnchor.UpperLeft, FontStyle.Normal, false);
                }
            }
            else if (lastLooked.kind == TargetKind.Crate && lastLooked.id >= 0 && lastLooked.id < world.Doors.Count)
            {
                var door = world.Doors[lastLooked.id];
                UiKit.Card(card, UiKit.Panel, 18);
                UiKit.Text(new Rect(card.x + 18, card.y + 12, 290, 30), "KONTEYNER " + (lastLooked.id + 1), 24, paper, TextAnchor.UpperLeft, FontStyle.Bold);
                if (door.opened) UiKit.Text(new Rect(card.x + 18, card.y + 54, 290, 26), "Kapı açık • içeride kasalar var", 16, UiKit.Dim, TextAnchor.UpperLeft, FontStyle.Normal, false);
                else
                {
                    UiKit.Bar(new Rect(card.x + 18, card.y + 56, 294, 14), door.progress, teal);
                    UiKit.Text(new Rect(card.x + 18, card.y + 74, 294, 22), "E'yi basılı tut", 15, UiKit.Dim, TextAnchor.UpperLeft, FontStyle.Normal, false);
                }
            }
            GUI.color = old;
        }

        void Toasts()
        {
            var list = FeedbackHub.Toasts;
            float now = Time.unscaledTime;
            for (int i = 0; i < list.Count; i++)
            {
                var toast = list[i];
                float age = now - toast.born;
                float slide = Mathf.Clamp01(age / .28f);
                float ease = 1 - (1 - slide) * (1 - slide) * (1 - slide);
                float fade = Mathf.Clamp01((toast.lifetime - age) / .5f);
                var old = GUI.color; GUI.color = new Color(1, 1, 1, Mathf.Min(ease, fade));
                var r = new Rect(560, 24 + i * 76 - (1 - ease) * 40, 480, 66);
                UiKit.Card(r, UiKit.Panel, 16);
                UiKit.Fill(new Rect(r.x, r.y + 10, 6, r.height - 20), toast.color, 3);
                UiKit.Text(new Rect(r.x + 24, r.y + 9, r.width - 40, 26), toast.title, 18, toast.color, TextAnchor.UpperLeft, FontStyle.Bold);
                UiKit.Text(new Rect(r.x + 24, r.y + 35, r.width - 40, 26), toast.body, 15, paper, TextAnchor.UpperLeft, FontStyle.Normal, false);
                GUI.color = old;
            }
        }

        void Popups(WarehousePlayer player)
        {
            var eye = player.Eye; if (eye == null) return;
            float now = Time.unscaledTime;
            foreach (var popup in FeedbackHub.Popups)
            {
                float age = (now - popup.born) / popup.lifetime;
                if (age < 0 || age > 1) continue;
                Vector3 screen = eye.WorldToScreenPoint(popup.position);
                if (screen.z <= 0) continue;
                Vector2 p = ToGui(screen);
                float rise = 70 * (1 - (1 - age) * (1 - age));
                float alpha = age < .75f ? 1 : 1 - (age - .75f) / .25f;
                float pop = age < .12f ? Mathf.Lerp(.6f, 1.15f, age / .12f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((age - .12f) / .12f));
                int size = Mathf.RoundToInt(28 * popup.size * pop);
                UiKit.Text(new Rect(p.x - 200, p.y - rise - size, 400, size + 8), popup.text, size,
                    new Color(popup.color.r, popup.color.g, popup.color.b, alpha), TextAnchor.LowerCenter, FontStyle.Bold);
            }
        }

        // ================= Pause / results =====================================================================
        void Pause(SessionMenu session, WarehouseWorld world)
        {
            UiKit.Fill(new Rect(-800, -450, 3200, 1800), new Color(0, 0, 0, .42f));
            UiKit.Card(new Rect(550, 150, 500, 610), new Color(.06f, .09f, .14f, .98f), 22);
            UiKit.Text(new Rect(590, 176, 420, 50), "DEPO MOLASI", 32, gold, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Text(new Rect(590, 226, 420, 28), "Co-op oturumu devam ediyor.", 16, UiKit.Dim, TextAnchor.UpperLeft, FontStyle.Normal, false);
            UiKit.Text(new Rect(590, 276, 200, 24), "MÜZİK", 14, teal, TextAnchor.UpperLeft, FontStyle.Bold, false);
            float music = UiKit.Slider(new Rect(590, 302, 420, 26), GameAudio.MusicVolume, teal);
            if (!Mathf.Approximately(music, GameAudio.MusicVolume)) GameAudio.MusicVolume = music;
            UiKit.Text(new Rect(590, 346, 200, 24), "EFEKTLER", 14, teal, TextAnchor.UpperLeft, FontStyle.Bold, false);
            float sfx = UiKit.Slider(new Rect(590, 372, 420, 26), GameAudio.SfxVolume, gold);
            if (!Mathf.Approximately(sfx, GameAudio.SfxVolume)) GameAudio.SfxVolume = sfx;
            bool bob = UiKit.Toggle(new Rect(590, 424, 420, 28), WarehousePlayer.HeadBob, "Kamera sallanması");
            if (bob != WarehousePlayer.HeadBob) WarehousePlayer.HeadBob = bob;
            if (UiKit.Button(new Rect(590, 480, 420, 54), "OYUNA DÖN", teal, 21)) WarehousePlayer.LockCursor(true);
            if (UiKit.Button(new Rect(590, 548, 420, 46), "BAŞARIMLAR", paper, 18)) showAchievements = true;
            if (UiKit.Button(new Rect(590, 608, 420, 46), "REKORLAR", paper, 18)) { showHistory = true; showWorld = false; }
            if (UiKit.Button(new Rect(590, 668, 420, 46), "ODADAN AYRIL", new Color(.95f, .55f, .5f), 18)) session.Leave();
        }

        static Color GradeColor(Grade grade)
        {
            switch (grade)
            {
                case Grade.S: return new Color(1f, .78f, .28f);
                case Grade.A: return new Color(.28f, .86f, .78f);
                case Grade.B: return new Color(.45f, .68f, 1f);
                default: return new Color(.72f, .76f, .82f);
            }
        }

        void Results(WarehouseWorld world)
        {
            if (!wasResults) { wasResults = true; resultsStart = Time.unscaledTime; }
            float since = Time.unscaledTime - resultsStart;
            float t = Mathf.Clamp01(since / 1.8f);
            float e = 1 - Mathf.Pow(1 - t, 3);
            int total = Mathf.Max(1, world.Items.Count);
            var grade = Grading.Rate(world.Elapsed, total, world.PeakPlayers.Value);
            Color gradeColor = GradeColor(grade);

            UiKit.Fill(new Rect(-800, -450, 3200, 1800), new Color(0, 0, 0, .5f));
            UiKit.Card(new Rect(250, 50, 1100, 800), new Color(.06f, .09f, .14f, .99f), 26);
            UiKit.Fill(new Rect(250, 50, 1100, 8), gold, 4);
            UiKit.Text(new Rect(290, 78, 720, 66), "DEPO TAMAMLANDI!", 50, gold, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Text(new Rect(292, 146, 720, 32), world.TeamNames.Value.ToString(), 20, paper, TextAnchor.UpperLeft, FontStyle.Normal, false);
            UiKit.Text(new Rect(290, 190, 720, 96), Money((int)(world.FinalDollars.Value * e)), 82, teal, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Text(new Rect(292, 292, 740, 30),
                world.Items.Count + " eşya  •  " + world.Stacks.Count + " tam istif  •  " + Clock(world.Elapsed) + "  •  " + world.PeakPlayers.Value + " oyuncu", 18, UiKit.Dim, TextAnchor.UpperLeft, FontStyle.Normal, false);

            // Grade badge, popping in after the money finishes counting.
            float pop = Mathf.Clamp01((since - 1.2f) / .4f);
            if (pop > 0)
            {
                float s = Mathf.Lerp(.4f, 1f, 1 - Mathf.Pow(1 - pop, 3));
                float size = 190 * s;
                var badge = new Rect(1150 - size * .5f, 200 - size * .5f + 10, size, size);
                UiKit.Fill(new Rect(badge.x - 10, badge.y - 10, badge.width + 20, badge.height + 20), new Color(gradeColor.r, gradeColor.g, gradeColor.b, .18f * pop), (size + 20) * .5f);
                UiKit.Fill(badge, new Color(.03f, .05f, .08f, 1), size * .5f);
                UiKit.Outline(badge, gradeColor, 8f, size * .5f);
                UiKit.Text(badge, Grading.Letters[(int)grade], Mathf.RoundToInt(120 * s), gradeColor, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Text(new Rect(1000, 316, 300, 30), Grading.Titles[(int)grade], 22, gradeColor, TextAnchor.UpperCenter, FontStyle.Bold);
            }

            // Best find
            ItemState best = default(ItemState); bool any = false;
            for (int i = 0; i < world.Items.Count; i++) if (!any || world.Items[i].dollars > best.dollars) { best = world.Items[i]; any = true; }
            if (any && best.kind >= 0 && best.kind < world.catalog.entries.Length)
            {
                UiKit.Fill(new Rect(290, 344, 1020, 64), UiKit.PanelLight, 14);
                UiKit.Text(new Rect(312, 352, 200, 20), "EN DEĞERLİ BULUNTU", 12, teal, TextAnchor.UpperLeft, FontStyle.Bold, false);
                UiKit.Text(new Rect(312, 372, 500, 32), world.catalog.entries[best.kind].title, 24, paper, TextAnchor.UpperLeft, FontStyle.Bold);
                UiKit.Pill(new Rect(700, 366, 130, 30), GameRules.ConditionNames[(int)best.condition], ItemVisual.ConditionColors[(int)best.condition], ink, 15);
                UiKit.Text(new Rect(1000, 358, 290, 44), Money(best.dollars), 32, gold, TextAnchor.MiddleRight, FontStyle.Bold);
            }

            // Value by condition
            int[] count = new int[7]; int[] value = new int[7]; int maxValue = 1;
            for (int i = 0; i < world.Items.Count; i++) { int c = (int)world.Items[i].condition; count[c]++; value[c] += world.Items[i].dollars; }
            for (int c = 0; c < 7; c++) maxValue = Mathf.Max(maxValue, value[c]);
            for (int c = 0; c < 7; c++)
            {
                float y = 428 + c * 42;
                UiKit.Fill(new Rect(292, y + 6, 14, 14), ItemVisual.ConditionColors[c], 7);
                UiKit.Text(new Rect(316, y, 200, 28), GameRules.ConditionNames[c] + "  •  " + count[c], 18, paper, TextAnchor.MiddleLeft, FontStyle.Normal, false);
                UiKit.Bar(new Rect(560, y + 8, 520, 12), e * value[c] / maxValue, ItemVisual.ConditionColors[c]);
                UiKit.Text(new Rect(1090, y, 210, 28), Money(value[c]), 20, paper, TextAnchor.MiddleRight, FontStyle.Bold, false);
            }
            UiKit.Text(new Rect(292, 726, 1010, 26), "Kazanç eşya değerlerinin toplamıdır. Süre yalnızca notu etkiler; para ve sıralama değişmez.", 15, UiKit.Dim, TextAnchor.UpperLeft, FontStyle.Normal, false);
            UiKit.Text(new Rect(292, 750, 1010, 26), ScoreHistory.LastError ?? "Sonuç bu bilgisayardaki rekor geçmişine kaydedildi.", 15, UiKit.Dim, TextAnchor.UpperLeft, FontStyle.Normal, false);
            if (UiKit.Button(new Rect(290, 782, 230, 46), "REKORLAR", paper, 18)) { showHistory = true; showWorld = false; }
            if (SessionMenu.Instance.network.IsHost)
            {
                if (UiKit.Button(new Rect(540, 782, 230, 46), "AYNI DEPO", teal, 18)) SessionMenu.Instance.NewRound(true);
                if (UiKit.Button(new Rect(790, 782, 230, 46), "YENİ DEPO", gold, 18)) SessionMenu.Instance.NewRound(false);
            }
            if (UiKit.Button(new Rect(1040, 782, 270, 46), "Depoya bak (TAB)", paper, 17)) WarehousePlayer.Local.CloseResults();
        }

        // ================= Records / achievements ====================================================================
        void History(WarehouseWorld world)
        {
            UiKit.Fill(new Rect(-800, -450, 3200, 1800), new Color(0, 0, 0, .55f));
            UiKit.Card(new Rect(260, 70, 1080, 770), new Color(.04f, .07f, .11f, 1), 24);
            UiKit.Text(new Rect(295, 92, 850, 55), "REKOR TABLOSU", 44, gold, TextAnchor.UpperLeft, FontStyle.Bold);
            if (UiKit.Button(new Rect(1190, 96, 120, 44), "KAPAT", paper, 18)) showHistory = false;
            if (UiKit.Button(new Rect(295, 172, 360, 44), "BU BİLGİSAYAR", showWorld ? paper : teal, 18)) showWorld = false;
            if (UiKit.Button(new Rect(675, 172, 360, 44), "DÜNYA", showWorld ? teal : paper, 18))
            {
                showWorld = true;
                var rules = world != null ? world.RulesHash.Value.ToString() : SessionMenu.Instance.sceneWorld.Rules.Fingerprint();
                LeaderboardClient.Instance?.Refresh(rules, world != null ? Math.Max(1, world.PeakPlayers.Value) : 1);
            }
            var online = LeaderboardClient.Instance;
            UiKit.Text(new Rect(295, 232, 1010, 60), showWorld ? (online != null ? online.Status : "Sıralama servisi bağlı değil.") : "Yerel kayıtlar • doğrulanmamış • ilk 100 sonuç. Farklı seed ve kurallar karşılaştırmayı etkiler.", 15, UiKit.Dim, TextAnchor.UpperLeft, FontStyle.Normal, false, true);
            var entries = showWorld ? (online != null ? online.Entries : new RunScore[0]) : ScoreHistory.Entries.ToArray();
            scroll = GUI.BeginScrollView(new Rect(290, 300, 1030, 520), scroll, new Rect(0, 0, 990, Math.Max(480, entries.Length * 68)));
            for (int i = 0; i < entries.Length; i++)
            {
                var s = entries[i]; float y = i * 68;
                UiKit.Fill(new Rect(0, y, 985, 62), i % 2 == 0 ? new Color(1, 1, 1, .05f) : new Color(1, 1, 1, .02f), 12);
                UiKit.Text(new Rect(14, y + 8, 55, 40), (i + 1).ToString("00"), 26, i < 3 ? gold : teal, TextAnchor.UpperLeft, FontStyle.Bold, false);
                UiKit.Text(new Rect(84, y + 6, 470, 30), s.team, 20, paper, TextAnchor.UpperLeft, FontStyle.Bold, false);
                UiKit.Text(new Rect(84, y + 34, 640, 24), s.player_count + " kişi • " + Clock(s.elapsed_milliseconds / 1000d) + " • Seed " + s.seed + " • " + s.rules_hash.Substring(0, Math.Min(8, s.rules_hash.Length)), 14, UiKit.Dim, TextAnchor.UpperLeft, FontStyle.Normal, false);
                UiKit.Text(new Rect(740, y + 10, 230, 40), Money(s.total_dollars), 26, gold, TextAnchor.MiddleRight, FontStyle.Bold, false);
            }
            if (entries.Length == 0) UiKit.Text(new Rect(14, 18, 900, 40), "Henüz bir sonuç yok. İlk depoyu tamamla!", 20, paper, TextAnchor.UpperLeft, FontStyle.Normal, false);
            GUI.EndScrollView();
        }

        void AchievementsPanel()
        {
            UiKit.Fill(new Rect(-800, -450, 3200, 1800), new Color(0, 0, 0, .55f));
            UiKit.Card(new Rect(240, 70, 1120, 770), new Color(.04f, .07f, .11f, 1), 24);
            UiKit.Text(new Rect(280, 92, 800, 55), "BAŞARIMLAR", 44, gold, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.Text(new Rect(282, 148, 800, 28), Achievements.UnlockedCount + " / " + Achievements.All.Length + " açıldı", 18, teal, TextAnchor.UpperLeft, FontStyle.Bold, false);
            if (UiKit.Button(new Rect(1200, 96, 120, 44), "KAPAT", paper, 18)) showAchievements = false;
            for (int i = 0; i < Achievements.All.Length; i++)
            {
                var def = Achievements.All[i]; bool done = Achievements.IsUnlocked(def.id);
                var r = new Rect(280 + (i % 2) * 520, 200 + (i / 2) * 104, 500, 92);
                UiKit.Card(r, done ? UiKit.PanelLight : new Color(.06f, .08f, .11f, .95f), 16, .5f);
                UiKit.Fill(new Rect(r.x + 18, r.y + 22, 48, 48), done ? gold : new Color(1, 1, 1, .09f), 24);
                if (done) UiKit.Outline(new Rect(r.x + 28, r.y + 32, 28, 28), ink, 4f, 14);
                else UiKit.Text(new Rect(r.x + 18, r.y + 22, 48, 48), "?", 26, UiKit.Dim, TextAnchor.MiddleCenter, FontStyle.Bold, false);
                UiKit.Text(new Rect(r.x + 82, r.y + 14, 400, 30), def.title, 22, done ? paper : UiKit.Dim, TextAnchor.UpperLeft, FontStyle.Bold, false);
                UiKit.Text(new Rect(r.x + 82, r.y + 46, 400, 40), def.description, 15, UiKit.Dim, TextAnchor.UpperLeft, FontStyle.Normal, false, true);
            }
            UiKit.Text(new Rect(282, 736, 1040, 30),
                "Toplam: " + Achievements.LifetimePlaced.ToString("N0") + " eşya yerleştirildi • " + Achievements.LifetimeDepots + " depo tamamlandı • " + Money(Achievements.LifetimeMoney) + " kazanıldı",
                17, paper, TextAnchor.UpperLeft, FontStyle.Normal, false);
            UiKit.Text(new Rect(282, 772, 1040, 26), "Başarımlar bu bilgisayarda saklanır ve puanı etkilemez.", 14, UiKit.Dim, TextAnchor.UpperLeft, FontStyle.Normal, false);
        }
    }
}
