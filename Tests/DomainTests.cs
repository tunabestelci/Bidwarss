using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Bidwarss.Domain;

// Dependency-free test runner: every test runs even if an earlier one fails, and the exit code reports the result.
// Set BIDWARSS_WRITE_GOLDEN=1 to regenerate Tests/golden_depots.txt after an intentional change to the generator.
public static class DomainTests
{
    static int checks;
    static readonly List<string> failures = new List<string>();
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Run(string name, Action test)
    {
        try { test(); }
        catch (Exception ex) { failures.Add(name + ": " + ex.Message); }
    }

    static void CheckCarryAndTools()
    {
        for(int count=1;count<=10;count++)
        {
            var layout=new CarryLayout(count);
            Check(layout.Width<=.61f && layout.Depth<=.45f,"Carry bundle must fit in arms");
            for(int i=0;i<count;i++)
            {
                layout.Position(i,out float x,out float y,out float z);
                Check(y>=0 && y+.42f*layout.Scale<=.45f,"Items must stay above the palms and below eye line");
                for(int j=0;j<i;j++)
                {
                    layout.Position(j,out float bx,out float by,out float bz);
                    bool separated=Math.Abs(x-bx)>=.44f*layout.Scale || Math.Abs(y-by)>=.42f*layout.Scale || Math.Abs(z-bz)>=.40f*layout.Scale;
                    Check(separated,"Carried item bounds overlap");
                }
            }
        }
        Check(OpeningSequence.Sample(OpeningMode.CutThenPry,.449f,out _) == OpeningTool.BoxCutter,"Cut must precede pry");
        Check(OpeningSequence.Sample(OpeningMode.CutThenPry,.45f,out var start) == OpeningTool.PryBar && start==0,"Pry starts at stage boundary");
        foreach(OpeningMode mode in Enum.GetValues(typeof(OpeningMode)))
            for(int p=-10;p<=110;p++)
            {
                OpeningSequence.Sample(mode,p/100f,out var phase);
                Check(phase>=0 && phase<=1,"Tool phase must be normalized");
            }
        Check(OpeningSequence.Sample(OpeningMode.Hands,.7f,out _)==OpeningTool.None,"Door mode must not show a blade");
    }
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        for (; dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Assets", "Bidwarss", "Data", "ItemCatalog.json"))) return dir.FullName;
        dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (; dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Assets", "Bidwarss", "Data", "ItemCatalog.json"))) return dir.FullName;
        throw new Exception("Depo kökü bulunamadı.");
    }

    static double Num(JsonElement element, string name, double fallback)
    { JsonElement value; return element.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : fallback; }

    // Same file the game and the wiki use; parsed by hand so the tests need no JSON mapping library.
    static CatalogFileData LoadCatalog()
    {
        using (var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "Assets", "Bidwarss", "Data", "ItemCatalog.json"))))
        {
            var root = doc.RootElement;
            var data = new CatalogFileData { format = root.GetProperty("format").GetString(), version = root.GetProperty("version").GetInt32() };
            data.tiers = root.GetProperty("tiers").EnumerateArray().Select(t => t.GetString()).ToArray();
            data.items = root.GetProperty("items").EnumerateArray().Select(e =>
            {
                var item = new CatalogItemData
                {
                    key = e.GetProperty("key").GetString(), title = e.GetProperty("title").GetString(),
                    shape = e.TryGetProperty("shape", out var shape) ? shape.GetString() : "",
                    kg = (float)Num(e, "kg", 0), heightCm = (int)Num(e, "heightCm", 0), widthCm = (int)Num(e, "widthCm", 0), depthCm = (int)Num(e, "depthCm", 0),
                    baseValue = (int)Num(e, "baseValue", 100), collector = (float)Num(e, "collector", 0), maxCount = (int)Num(e, "maxCount", 0),
                    selectionWeight = (int)Num(e, "selectionWeight", 1)
                };
                if (e.TryGetProperty("classes", out var classes))
                    item.classes = classes.EnumerateArray().Select(c => new CatalogClassData { chance = (float)Num(c, "chance", 0), priceMin = (int)Num(c, "priceMin", 1), priceMax = (int)Num(c, "priceMax", 1) }).ToArray();
                return item;
            }).ToArray();
            return data;
        }
    }

    static GameRules CatalogRules()
    {
        var catalog = LoadCatalog();
        string problem = catalog.Problem();
        if (problem != null) throw new Exception("Katalog geçersiz: " + problem);
        return new GameRules { items = catalog.ToRules() };
    }

    static GameRules SmallRules() => new GameRules { items = new[] {
        new ItemRule { key = "mirror", baseDollars = 80, heightCm = 80 }, new ItemRule { key = "table", baseDollars = 100, widthCm = 120 },
        new ItemRule { key = "chair", baseDollars = 60 }, new ItemRule { key = "radio", baseDollars = 120 }, new ItemRule { key = "clock", baseDollars = 450, heightCm = 200, kg = 60 } } };

    public static int Main()
    {
        Run("carry layout and opening tools", CheckCarryAndTools);
        Run("catalog file", CatalogFile);
        Run("depot invariants", DepotInvariants);
        Run("size limits", SizeLimits);
        Run("randomness", Randomness);
        Run("class tables", ClassTables);
        Run("exported tables", ExportedTables);
        Run("extreme seeds", ExtremeSeeds);
        Run("full round", FullRound);
        Run("many rounds can be completed", ManyRounds);
        Run("disconnect hand-over", DisconnectHandOver);
        Run("rule limits", RuleLimits);
        Run("player names", PlayerNames);
        Run("golden depots", GoldenDepots);
        if (failures.Count > 0)
        {
            foreach (var failure in failures) Console.Error.WriteLine("FAIL " + failure);
            Console.Error.WriteLine(failures.Count + " test(s) failed after " + checks + " assertions.");
            return 1;
        }
        Console.WriteLine("PASS: " + checks + " assertions; random depots, size limits, class tables, rounds, limits, names and golden depots.");
        return 0;
    }

    static void CatalogFile()
    {
        var catalog = LoadCatalog();
        Check(catalog.Problem() == null, "Default catalog is valid: " + catalog.Problem());
        Check(catalog.items.Length >= 10, "Default catalog offers enough variety");
        Check(catalog.tiers.SequenceEqual(GameRules.ConditionNames), "Catalog tier names match the game's condition names");
        var broken = LoadCatalog(); broken.tiers = broken.tiers.Take(6).ToArray();
        Check(broken.Problem() != null, "Six tiers rejected");
        broken = LoadCatalog(); broken.format = "other";
        Check(broken.Problem() != null, "Wrong format rejected");
        broken = LoadCatalog(); broken.items[1].key = broken.items[0].key;
        Check(broken.Problem() != null, "Duplicate keys rejected");
        broken = LoadCatalog(); broken.items[0].classes = new[] { new CatalogClassData { chance = 100, priceMin = 1, priceMax = 2 } };
        Check(broken.Problem() != null, "A class table needs seven rows");
    }

    static void DepotInvariants()
    {
        var rules = CatalogRules();
        var tables = rules.items.Select(i => i.Table()).ToArray();
        for (int seed = -1000; seed < 1000; seed++)
        {
            var round = new RoundEngine(rules, seed);
            int n = round.Items.Count;
            Check(n >= 1 && n <= rules.totalGroups * GameRules.StackSize, "Item count within rack capacity (" + n + ")");
            Check(round.Items.Select(x => x.id).Distinct().Count() == n && round.Items.Select((x, i) => x.id == i).All(same => same), "Ids are 0..n-1 in order");
            Check(round.StackCount >= 1 && round.StackCount <= rules.totalGroups, "Rack count within the scene's racks");
            Check(round.Items.Count == Enumerable.Range(0, round.StackCount).Sum(s => round.StackCapacity(s)), "Rack capacities add up to the item count");
            for (int s = 0; s < round.StackCount; s++) Check(round.StackCapacity(s) >= 1 && round.StackCapacity(s) <= GameRules.StackSize, "Rack capacity 1..10");
            foreach (var group in round.Items.GroupBy(x => x.kind))
            {
                Check(group.Count() <= rules.items[group.Key].EffectiveMaxCount(), "Type limit respected: " + rules.items[group.Key].key);
                Check(group.Count() == Enumerable.Range(0, round.StackCount).Where(s => round.StackKind(s) == group.Key).Sum(s => round.StackCapacity(s)), "Racks of a type hold exactly its pieces");
            }
            var activeCrates = Enumerable.Range(0, round.CrateCount).Where(round.IsActive).ToArray();
            Check(activeCrates.Length == round.ActiveCrateCount, "Active crate count matches");
            Check(activeCrates.Length >= Math.Min(rules.minCrates, n) && activeCrates.Length <= rules.crateCount, "Between min and max crates in use");
            foreach (var crate in round.Items.GroupBy(x => x.crate))
            {
                Check(round.IsActive(crate.Key), "No piece sits in an unused container");
                Check(crate.Count() <= GameRules.CrateLimit, "Container load within the reveal area");
                Check(crate.Select(x => x.crateSlot).OrderBy(v => v).SequenceEqual(Enumerable.Range(0, crate.Count())), "Container slots are 0..k-1");
            }
            Check(activeCrates.All(c => round.Items.Any(x => x.crate == c)), "No active container is empty");
            foreach (var item in round.Items)
            {
                var table = tables[item.kind]; int c = (int)item.condition;
                Check(table.weight[c] > 0, "A condition with zero chance never appears");
                Check(item.dollars >= table.min[c] && item.dollars <= table.max[c], "Price inside the class band");
                Check(item.location == ItemLocation.Sealed, "Everything starts sealed");
            }
            Check(round.TotalDollars == round.Items.Sum(x => x.dollars), "Total is the sum of prices");
        }
        var a = new RoundEngine(rules, 11); var b = new RoundEngine(rules, 11);
        Check(Summary(a) == Summary(b), "Seed reproducibility");
        Check(a.RulesHash == b.RulesHash && a.RulesHash.Length == 64, "Rules hash is a stable SHA-256 hex string");
        Check(Summary(a) != Summary(new RoundEngine(rules, 12)), "Different seeds give different depots");
    }

    static void SizeLimits()
    {
        var rules = CatalogRules();
        int clock = Array.FindIndex(rules.items, i => i.key == "boy-saati"), stool = Array.FindIndex(rules.items, i => i.key == "plastik-tabure");
        Check(rules.items[clock].EffectiveMaxCount() == 5, "A two metre grandfather clock is capped at five");
        Check(rules.items[stool].EffectiveMaxCount() == 20, "A plastic stool may reach twenty");
        Check(new ItemRule { key = "x", heightCm = 110 }.EffectiveMaxCount() == 10, "About a metre: ten");
        Check(new ItemRule { key = "x", heightCm = 75 }.EffectiveMaxCount() == 15, "Seventy-five centimetres: fifteen");
        Check(new ItemRule { key = "x", heightCm = 20, kg = 70 }.EffectiveMaxCount() == 5, "Very heavy things are capped even when small");
        Check(new ItemRule { key = "x", heightCm = 20, kg = 35 }.EffectiveMaxCount() == 10, "Heavy things are capped");
        Check(new ItemRule { key = "x" }.EffectiveMaxCount() == 20, "Unknown size means no size limit");
        Check(new ItemRule { key = "x", heightCm = 200, maxCount = 12 }.EffectiveMaxCount() == 12, "An explicit limit wins over the size rule");
        Check(new ItemRule { key = "x", maxCount = 20 }.EffectiveMaxCount() == 20, "Never above twenty");
        int clocks = 0, stools20 = 0, rounds = 0;
        for (int seed = 0; seed < 600; seed++)
        {
            var round = new RoundEngine(rules, seed); rounds++;
            int c = round.Items.Count(x => x.kind == clock), s = round.Items.Count(x => x.kind == stool);
            Check(c <= 5, "Never more than five clocks"); clocks += c; if (s == 20) stools20++;
        }
        Check(clocks > 0, "Clocks still show up sometimes");
        Check(stools20 > 0, "Twenty small pieces of one type do happen");
    }

    static void Randomness()
    {
        var rules = CatalogRules();
        var itemTotals = new HashSet<int>(); var crateCounts = new HashSet<int>(); var rackCounts = new HashSet<int>(); var kindSets = new HashSet<string>();
        var countSeen = new Dictionary<int, int>(); int oddLots = 0, groups = 0;
        for (int seed = 0; seed < 600; seed++)
        {
            var round = new RoundEngine(rules, seed);
            itemTotals.Add(round.Items.Count); crateCounts.Add(round.ActiveCrateCount); rackCounts.Add(round.StackCount);
            kindSets.Add(string.Join(",", round.Items.Select(x => x.kind).Distinct().OrderBy(k => k)));
            foreach (var g in round.Items.GroupBy(x => x.kind))
            {
                groups++; int n = g.Count(); int seen; countSeen.TryGetValue(n, out seen); countSeen[n] = seen + 1;
                if (n % 5 != 0) oddLots++;
            }
        }
        Check(itemTotals.Count >= 20, "The number of pieces changes from game to game (" + itemTotals.Count + ")");
        Check(crateCounts.Min() == rules.minCrates && crateCounts.Max() == rules.crateCount, "Between 6 and 10 containers are used");
        Check(rackCounts.Count >= 4, "The number of filled racks varies");
        Check(kindSets.Count >= 300, "Nearly every depot has its own mix of types (" + kindSets.Count + ")");
        foreach (int option in GameRules.CountOptions) Check(countSeen.ContainsKey(option) && countSeen[option] > groups / 20, "Count " + option + " is common");
        Check(oddLots > 0 && oddLots < groups / 4, "Odd lots exist but stay the exception (" + oddLots + " of " + groups + ")");
        // Cargo is spread unevenly: the busiest container holds clearly more than the quietest one in most depots.
        int uneven = 0;
        for (int seed = 0; seed < 200; seed++)
        {
            var load = new RoundEngine(rules, seed).Items.GroupBy(x => x.crate).Select(g => g.Count()).ToArray();
            if (load.Max() >= load.Min() * 2) uneven++;
        }
        Check(uneven > 120, "Containers hold very different amounts (" + uneven + "/200)");
    }

    static void ClassTables()
    {
        var m = ItemRule.Multipliers(0);
        Check(Math.Abs(m[2] - 1) < 1e-9, "Orta is the market value");
        for (int i = 1; i < m.Length; i++) Check(m[i] > m[i - 1], "Better class, higher multiplier");
        Check(ItemRule.Multipliers(10)[6] > ItemRule.Multipliers(0)[6] * 4, "A high collector score lifts the top class enormously");
        var stool = new ItemRule { key = "s", baseDollars = 10, collector = 0 }.Table();
        Check(stool.min[2] <= 10 && stool.max[2] >= 10, "Average class brackets the base price");
        Check(Enumerable.Range(0, 7).All(k => stool.min[k] >= 1 && stool.max[k] >= stool.min[k]), "Bands are valid even for cheap items");
        var clock = new ItemRule { key = "c", baseDollars = 450, collector = 7 }.Table();
        Check(clock.max[6] > 4000 && clock.max[6] < 20000, "A collector's clock reaches thousands (" + clock.max[6] + ")");
        Check(clock.totalWeight == 100000, "Default chances add up to a hundred percent");
        var explicitTable = new ItemRule { key = "e", classChance = new float[] { 50, 50, 0, 0, 0, 0, 0 }, classMin = new[] { 5, 10, 1, 1, 1, 1, 1 }, classMax = new[] { 6, 12, 1, 1, 1, 1, 1 } };
        var round = new RoundEngine(new GameRules { items = new[] { explicitTable, new ItemRule { key = "f", baseDollars = 20 } } }, 3);
        Check(round.Items.Where(x => x.kind == 0).All(x => x.condition <= ItemCondition.Bad && x.dollars >= 5 && x.dollars <= 12), "An explicit class table is honoured");
        bool threw = false;
        try { new ItemRule { key = "bad", classChance = new float[] { 100, 0, 0, 0, 0, 0, 0 }, classMin = new[] { 1, 1, 1 }, classMax = new[] { 1, 1, 1 } }.Table(); } catch (ArgumentException) { threw = true; }
        Check(threw, "A table with the wrong number of rows is rejected");
        threw = false;
        try { new ItemRule { key = "bad", classChance = new float[7], classMin = new[] { 1, 1, 1, 1, 1, 1, 1 }, classMax = new[] { 1, 1, 1, 1, 1, 1, 1 } }.Table(); } catch (ArgumentException) { threw = true; }
        Check(threw, "A table where nothing can drop is rejected");
    }

    // The wiki exports every item with its full class table (chances with three decimals). The game must draw the same
    // depot from that file as from the derived tables, otherwise the wiki and the game would disagree.
    static void ExportedTables()
    {
        var derived = CatalogRules();
        var exported = CatalogRules();
        foreach (var item in exported.items)
        {
            var table = item.Table();
            item.classChance = table.weight.Select(w => (float)(w / 1000.0)).ToArray();
            item.classMin = table.min.ToArray(); item.classMax = table.max.ToArray();
        }
        Check(exported.items.All(i => i.HasExplicitTable) && derived.items.All(i => !i.HasExplicitTable), "One catalog carries explicit tables, the other derives them");
        for (int seed = -50; seed < 50; seed++)
            Check(Summary(new RoundEngine(exported, seed)) == Summary(new RoundEngine(derived, seed)), "Explicit tables reproduce depot " + seed);
        Check(exported.Fingerprint() == derived.Fingerprint(), "Explicit and derived tables share one ranking board");
    }

    static void ExtremeSeeds()
    {
        var rules = CatalogRules();
        foreach (int seed in new[] { 0, int.MinValue, int.MaxValue, -1, 1 })
        {
            var round = new RoundEngine(rules, seed);
            Check(round.Items.Count > 0 && round.TotalDollars > 0, "Seed " + seed + " produces a valid depot");
        }
    }

    static void Solve(RoundEngine round, ulong player)
    {
        string error;
        for (int c = 0; c < round.CrateCount; c++) if (round.IsActive(c) && !round.IsOpen(c)) Check(round.Open(c, out error), "Open active crate " + c);
        for (;;)
        {
            var next = round.Items.FirstOrDefault(x => x.location == ItemLocation.Loose);
            if (next == null) break;
            int kind = next.kind;
            foreach (var item in round.Items.Where(x => x.kind == kind && x.location == ItemLocation.Loose).Take(GameRules.StackSize).ToArray()) Check(round.Take(item.id, player, out error), "Take a piece");
            for (int s = 0; s < round.StackCount; s++)
            {
                int held; if (round.HeldCount(player, out held) == 0) break;
                if (round.StackKind(s) == kind && round.StackCountAt(s) < round.StackCapacity(s)) round.Place(s, player, out error);
            }
            int k; Check(round.HeldCount(player, out k) == 0, "The hand is empty after stacking");
        }
    }

    static void FullRound()
    {
        var rules = SmallRules(); string error;
        var round = new RoundEngine(rules, 11);
        Check(!round.Take(0, 0, out error), "Sealed item cannot be picked up");
        int inactive = Enumerable.Range(0, round.CrateCount).FirstOrDefault(c => !round.IsActive(c));
        int crate = Enumerable.Range(0, round.CrateCount).First(round.IsActive);
        if (!round.IsActive(inactive)) Check(!round.Open(inactive, out error) && round.OpenedCount == 0, "A container that is not in use cannot be opened");
        Check(round.Open(crate, out error), "Open once"); Check(!round.Open(crate, out error), "Duplicate open rejected");
        var item = round.Items.First(x => x.crate == crate);
        Check(round.Take(item.id, 0, out error), "Host client id zero supported"); Check(!round.Take(item.id, 1, out error), "Double pickup rejected");
        Check(!round.Drop(item.id, 1), "Other player cannot drop held item");
        round.Disconnect(0); Check(item.location == ItemLocation.Loose, "Disconnect releases item");
        foreach (var c in Enumerable.Range(0, round.CrateCount).Where(round.IsActive)) round.Open(c, out error);
        var first = round.Items.First(); int kind = first.kind;
        int wrongStack = Enumerable.Range(0, round.StackCount).First(s => round.StackKind(s) != kind);
        Check(round.Take(first.id, 1, out error), "Take one");
        Check(round.Place(wrongStack, 1, out error) == 0, "Wrong category placement rejected");
        var foreign = round.Items.FirstOrDefault(x => x.kind != kind && x.location == ItemLocation.Loose);
        if (foreign != null) Check(!round.Take(foreign.id, 1, out error), "Mixed carry rejected");
        foreach (var more in round.Items.Where(x => x.kind == kind && x.location == ItemLocation.Loose).Take(GameRules.StackSize - 1).ToArray()) Check(round.Take(more.id, 1, out error), "Collect up to ten");
        var extra = round.Items.FirstOrDefault(x => x.kind == kind && x.location == ItemLocation.Loose);
        int heldKind; int held = round.HeldCount(1, out heldKind);
        if (held == GameRules.StackSize && extra != null) Check(!round.Take(extra.id, 1, out error), "Carry cap");
        int rack = Enumerable.Range(0, round.StackCount).First(s => round.StackKind(s) == kind);
        int placed = round.Place(rack, 1, out error);
        Check(placed == Math.Min(held, round.StackCapacity(rack)), "Deposit fills the rack up to its capacity (placed " + placed + ", held " + held + ", capacity " + round.StackCapacity(rack) + ", error " + error + ")");
        var stacked = round.Items.Where(x => x.stack == rack).OrderBy(x => x.stackIndex).ToArray();
        if (stacked.Length > 1) Check(!round.Take(stacked[0].id, 2, out error), "Cannot remove middle of stack");
        var top = stacked.Last();
        int before = round.PlacedCount;
        Check(round.Take(top.id, 2, out error), "Can retrieve top item"); Check(round.PlacedCount == before - 1, "Retrieve updates progress");
        Check(round.Place(rack, 2, out error) == 1, "Return item"); Check(round.Place(rack, 2, out error) == 0, "Duplicate placement has no payout");
        var spare = round.Items.FirstOrDefault(x => x.kind == kind && x.location == ItemLocation.Loose);
        if (spare != null && round.StackCapacity(rack) == round.StackCountAt(rack))
        {
            Check(round.Take(spare.id, 2, out error), "Take a spare piece");
            Check(round.Place(rack, 2, out error) == 0 && error.Contains("tamamlandı"), "A full rack says so");
            Check(round.Drop(spare.id, 2), "Put the spare piece back");
        }
        Solve(round, 3);
        Check(round.Completed, "Complete only when all items and stacks done");
        Check(round.FinalDollars == round.Items.Sum(x => x.dollars), "Exact payout sum");
        int paid = round.FinalDollars;
        Check(!round.Take(0, 0, out error) && round.Place(0, 0, out error) == 0 && round.FinalDollars == paid, "Finished run immutable");
    }

    static void ManyRounds()
    {
        var rules = CatalogRules();
        for (int seed = 100; seed < 300; seed++)
        {
            var round = new RoundEngine(rules, seed);
            Check(!round.Completed, "A fresh round is not complete");
            Solve(round, 1);
            Check(round.Completed && round.PlacedCount == round.Items.Count && round.FinalDollars == round.TotalDollars, "Round " + seed + " can be finished");
            Check(round.OpenedCount == round.ActiveCrateCount, "Only the containers in use had to be opened");
        }
    }

    static void DisconnectHandOver()
    {
        var round = new RoundEngine(SmallRules(), 5); string error;
        int crate = Enumerable.Range(0, round.CrateCount).First(round.IsActive);
        round.Open(crate, out error);
        var item = round.Items.First(x => x.crate == crate);
        Check(round.Take(item.id, 7, out error), "First player takes the item");
        int kind;
        Check(round.HeldCount(7, out kind) == 1 && kind == item.kind, "Hand holds one item");
        round.Disconnect(7);
        Check(round.HeldCount(7, out kind) == 0, "Disconnect empties the hand");
        Check(round.Take(item.id, 8, out error), "Released item can be taken by a teammate");
        Check(item.holder == 8 && item.location == ItemLocation.Held, "New owner recorded");
    }

    static void RuleLimits()
    {
        var rules = SmallRules();
        Action<string, Action<GameRules>> rejects = (what, change) =>
        {
            var broken = SmallRules(); change(broken);
            bool threw = false; try { new RoundEngine(broken, 1); } catch (ArgumentException) { threw = true; }
            Check(threw, what);
        };
        rejects("Impossible rack range rejected", r => { r.totalGroups = 30; r.minGroups = 31; });
        rejects("Too few containers for the busiest depot rejected", r => { r.minCrates = 2; });
        rejects("More containers than exist rejected", r => { r.minCrates = 12; });
        rejects("Duplicate item keys rejected", r => r.items[1].key = r.items[0].key);
        rejects("Over-limit explicit count rejected", r => r.items[0].maxCount = 21);
        rejects("Negative size rejected", r => r.items[0].heightCm = -1);
        rejects("Collector score above ten rejected", r => r.items[0].collector = 11);
        var changed = SmallRules(); changed.items[0].baseDollars++;
        Check(changed.Fingerprint() != rules.Fingerprint(), "Changed balance uses a different ranking board");
        Check(SmallRules().Fingerprint() == rules.Fingerprint(), "Equal rules share a ranking board");
        var bigger = SmallRules(); bigger.items[0].heightCm = 200;
        Check(bigger.Fingerprint() != rules.Fingerprint(), "A changed size limit changes the board");
        var lone = new RoundEngine(new GameRules { items = new[] { new ItemRule { key = "only", baseDollars = 5 } } }, 9);
        Check(lone.Items.Count >= 1 && lone.Items.Count <= 20 && lone.Items.All(x => x.kind == 0), "A one-type catalog still makes a depot");
    }

    static void PlayerNames()
    {
        Check(NameRules.Clean(null) == "Oyuncu", "Null name falls back");
        Check(NameRules.Clean("   ") == "Oyuncu", "Blank name falls back");
        Check(NameRules.Clean("<>|") == "Oyuncu", "Only forbidden characters falls back");
        Check(NameRules.Clean("a<b>/c\\d|e") == "abcde", "Markup and separator characters removed");
        Check(NameRules.Clean("Tu​na") == "Tuna", "Zero-width characters removed");
        Check(NameRules.Clean("A   B") == "A B", "Repeated spaces collapse");
        Check(NameRules.Clean("A😀B") == "AB", "Surrogate pairs removed so truncation cannot split them");
        Check(NameRules.Clean("a\nb\tc") == "abc", "Control characters removed");
        Check(NameRules.Clean("Şükrü") == "Şükrü", "Turkish letters kept");
        string longName = NameRules.Clean(new string('x', 40));
        Check(longName.Length == NameRules.MaxLength, "Names are capped at sixteen characters");
        Check(NameRules.Clean(NameRules.Clean("  Tuna  ")) == NameRules.Clean("Tuna"), "Cleaning is idempotent");
    }

    // One canonical line per depot. The wiki's simulator (Site/kasa-defteri.html) must print the same lines for the same seeds.
    public static string Summary(RoundEngine round)
    {
        var sb = new StringBuilder();
        sb.Append("items=").Append(round.Items.Count);
        sb.Append(";active=").Append(string.Join(",", Enumerable.Range(0, round.CrateCount).Where(round.IsActive)));
        sb.Append(";load=").Append(string.Join(",", round.Items.GroupBy(x => x.crate).OrderBy(g => g.Key).Select(g => g.Key + ":" + g.Count())));
        sb.Append(";racks=").Append(string.Join(",", Enumerable.Range(0, round.StackCount).Select(s => round.StackKind(s) + ":" + round.StackCapacity(s))));
        sb.Append(";kinds=").Append(string.Join(",", round.Items.GroupBy(x => x.kind).OrderBy(g => g.Key).Select(g => g.Key + ":" + g.Count())));
        sb.Append(";total=").Append(round.TotalDollars);
        uint hash = 2166136261;
        foreach (var item in round.Items)
            foreach (int value in new[] { item.kind, (int)item.condition, item.dollars, item.crate, item.crateSlot })
                for (int shift = 0; shift < 32; shift += 8) { hash ^= (uint)((value >> shift) & 255); hash = unchecked(hash * 16777619u); }
        sb.Append(";fnv=").Append(hash.ToString("x8"));
        return sb.ToString();
    }

    static readonly int[] GoldenSeeds = { 0, 1, 2, 3, 7, 42, 99, 777, 2026, 123456789, -5, -2026, int.MinValue, int.MaxValue };

    static void GoldenDepots()
    {
        var rules = CatalogRules();
        var lines = GoldenSeeds.Select(seed => seed + ";" + Summary(new RoundEngine(rules, seed))).ToArray();
        string path = Path.Combine(RepoRoot(), "Tests", "golden_depots.txt");
        if (Environment.GetEnvironmentVariable("BIDWARSS_WRITE_GOLDEN") == "1") { File.WriteAllText(path, string.Join("\n", lines) + "\n"); Console.WriteLine("Golden file written: " + path); return; }
        var expected = File.ReadAllText(path).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Check(expected.Length == lines.Length, "Golden file has one line per seed");
        for (int i = 0; i < lines.Length; i++) Check(lines[i] == expected[i], "Depot of seed " + GoldenSeeds[i] + " changed.\n  now:    " + lines[i] + "\n  golden: " + expected[i]);
    }
}
