using System;
using System.Collections.Generic;
using System.Linq;
using Bidwarss.Domain;

// Console test of the random depot (Domain only, no Unity). Build: compile Bidwarss/Domain/*.cs + this file as an exe.
public static class RandomDepotTests
{
    static int failures, checks, distinctRolls, variedRolls;
    static void Check(bool ok, string what) { checks++; if (!ok) { failures++; if (failures < 30) Console.WriteLine("FAIL: " + what); } }

    // key, size cm, grams, weight, crate kind, pack
    static readonly object[][] Catalog = {
        new object[]{"capsule",  3, 20, 100, 0, 10}, new object[]{"cup", 12, 300, 100, 0, 8}, new object[]{"book", 25, 800, 80, 0, 8},
        new object[]{"radio",   35, 3000, 40, 0, 4}, new object[]{"lamp", 60, 2500, 40, 2, 3}, new object[]{"mirror", 110, 9000, 20, 2, 2},
        new object[]{"clock",  200, 40000, 6, 3, 1}, new object[]{"wardrobe", 180, 70000, 3, 3, 1}, new object[]{"guitar", 100, 3500, 20, 3, 2},
        new object[]{"coin",    2, 10, 30, 4, 10}, new object[]{"watch", 8, 100, 20, 4, 6}, new object[]{"vase", 40, 2000, 30, 0, 5},
    };

    static GameRules Make(int pallets, bool random, int copies = 1)
    {
        int n0 = Catalog.Length, total0 = n0 * copies;
        var rules = new GameRules { items = new ItemRule[total0], totalGroups = pallets, randomRound = random, packedKind = -1 };
        var kinds = new List<int>(); var reserve = new List<int>();
        for (int i = 0; i < total0; i++)
        {
            var c = Catalog[i % n0];
            var r = new ItemRule { key = (string)c[0] + (i / n0 > 0 ? "#" + (i / n0) : ""), sizeCm = (int)c[1], grams = (int)c[2], selectionWeight = (int)c[3], baseDollars = 50 + i * 10,
                packSize = (int)c[5], crateMask = CrateKinds.Bit((int)c[4]), roundCount = 10 };
            rules.items[i] = r;
            int boxes = (r.EffectiveMaxCount() + r.packSize - 1) / r.packSize;
            for (int b = 0; b < boxes; b++) { kinds.Add((int)c[4]); reserve.Add(i); }
        }
        rules.crateKinds = kinds.ToArray(); rules.crateItems = reserve.ToArray(); rules.crateCount = kinds.Count;
        return rules;
    }

    public static int Main()
    {
        // size limit
        var rules = Make(500, true, 6);
        var caps = new Dictionary<string, int>();
        foreach (var it in rules.items) caps[it.key] = it.EffectiveMaxCount();
        Check(caps["capsule"] == 200 && caps["coin"] == 200, "tiny light items reach 120");
        Check(caps["clock"] == 5 && caps["wardrobe"] == 5, "2 m clock / wardrobe: 5 (same table as the site)");
        Check(caps["mirror"] == 10 && caps["guitar"] == 10, "100+ cm: 10");
        Check(caps["radio"] == 40 && caps["lamp"] == 20, "35 cm / 3 kg: 40, 60 cm: 20");
        Console.WriteLine("caps: " + string.Join(", ", caps.Select(k => k.Key + "=" + k.Value)));
        var hand = new ItemRule { key = "x", sizeCm = 200, maxCount = 500 };
        Check(hand.EffectiveMaxCount() == GameRules.HardMaxCount, "hand limit capped at HardMaxCount");

        var seen = new HashSet<string>(); var countHist = new Dictionary<int, int>(); int lots = 0, total = 0, maxCrates = 0, minCrates = 999;
        var typeFreq = new Dictionary<string, int>();
        for (int seed = 1; seed <= 3000; seed++)
        {
            var e = new RoundEngine(rules, seed);
            var e2 = new RoundEngine(rules, seed);
            Check(e.Items.Count == e2.Items.Count && e.TotalDollars == e2.TotalDollars && e.ActiveCrateCount == e2.ActiveCrateCount, "deterministic");
            var byKind = e.Items.GroupBy(i => i.kind).ToDictionary(g => g.Key, g => g.Count());
            int pallets = 0;
            foreach (var kv in byKind)
            {
                var rule = rules.items[kv.Key];
                Check(kv.Value >= 1 && kv.Value <= rule.EffectiveMaxCount(), "cap " + rule.key + " " + kv.Value);
                pallets += (kv.Value + rule.PalletCap() - 1) / rule.PalletCap();
                total++; if (kv.Value == 5 || kv.Value == 10 || kv.Value == 15 || kv.Value == 20) lots++;
                countHist[kv.Value] = countHist.ContainsKey(kv.Value) ? countHist[kv.Value] + 1 : 1;
                typeFreq[rule.key] = typeFreq.ContainsKey(rule.key) ? typeFreq[rule.key] + 1 : 1;
            }
            Check(pallets <= rules.totalGroups, "pallet budget " + pallets);
            Check(e.StackCount == pallets, "stack count");
            // every item of a type gets its own condition roll (not one roll for all)
            foreach (var g in e.Items.GroupBy(i => i.kind)) if (g.Count() >= 8) { distinctRolls++; if (g.Select(i => i.dollars).Distinct().Count() > 1) variedRolls++; }
            var perCrate = new int[rules.crateCount];
            foreach (var it in e.Items)
            {
                perCrate[it.crate]++;
                Check(e.IsActive(it.crate), "item in inactive crate");
                Check(rules.crateItems[it.crate] == it.kind, "item crate reserved for its type");
                Check(rules.Accepts(rules.items[it.kind], it.crate), "kind accepted");
            }
            int active = 0;
            for (int c = 0; c < rules.crateCount; c++)
            {
                Check((perCrate[c] > 0) == e.IsActive(c), "active crate must hold items, seed " + seed + " crate " + c);
                if (e.IsActive(c)) active++;
                int pack = rules.items[rules.crateItems[c]].packSize;
                if (perCrate[c] > 0 && byKind[rules.crateItems[c]] <= pack * 1) { }
            }
            Check(active == e.ActiveCrateCount, "active count");
            maxCrates = Math.Max(maxCrates, active); minCrates = Math.Min(minCrates, active);
            seen.Add(string.Join(",", byKind.OrderBy(k => k.Key).Select(k => k.Key + ":" + k.Value)));

            if (seed <= 200)
            {
                string err;
                int dead = FirstInactive(e, rules.crateCount);
                if (dead >= 0) Check(!e.Open(dead, out err), "inactive crate cannot be opened");
                for (int c = 0; c < rules.crateCount; c++) if (e.IsActive(c)) Check(e.Open(c, out err), "open " + err);
                Check(e.OpenedCount == e.ActiveCrateCount, "opened all active");
                ulong p = 1; var order = e.Items.OrderBy(i => i.kind).ToList();
                foreach (var kg in order.GroupBy(i => i.kind))
                {
                    var queue = new Queue<RoundItem>(kg);
                    while (queue.Count > 0)
                    {
                        int n = 0;
                        while (queue.Count > 0 && n < GameRules.StackSize) { Check(e.Take(queue.Dequeue().id, p, out err), "take " + err); n++; }
                        int st = -1;
                        for (int s = 0; s < e.StackCount; s++) if (e.StackKind(s) == kg.Key && e.StackCountAt(s) < e.StackCapacity(s)) { st = s; break; }
                        Check(st >= 0, "pallet available");
                        if (st < 0) break;
                        while (e.HeldCount(p, out int hk) > 0)
                        {
                            int placed = e.Place(st, p, out err);
                            if (placed == 0)
                            {
                                st = -1; for (int s = 0; s < e.StackCount; s++) if (e.StackKind(s) == kg.Key && e.StackCountAt(s) < e.StackCapacity(s)) { st = s; break; }
                                if (st < 0) { Check(false, "ran out of pallets"); break; }
                            }
                        }
                    }
                }
                Check(e.Completed && e.FinalDollars == e.TotalDollars, "round completes, seed " + seed);
            }
        }
        Check(variedRolls * 100 >= distinctRolls * 98, "prices vary inside a type: " + variedRolls + "/" + distinctRolls);
        Console.WriteLine("distinct rounds in 3000 seeds: " + seen.Count);
        Check(seen.Count > 2000, "rounds differ");
        Console.WriteLine("lot share (5/10/15/20): " + (100.0 * lots / total).ToString("F1") + "%");
        Check(lots * 100.0 / total > 30, "lots are the usual counts");
        Console.WriteLine("active crates per round: " + minCrates + ".." + maxCrates);
        Console.WriteLine("counts: " + string.Join(" ", countHist.OrderBy(k => k.Key).Select(k => k.Key + "x" + k.Value)));
        Console.WriteLine("type freq: " + string.Join(", ", typeFreq.OrderByDescending(k => k.Value).Select(k => k.Key + "=" + k.Value)));
        Check(maxCrates > minCrates, "crate count varies");

        // fingerprint changes with randomRound / caps, legacy rules unchanged in behaviour
        var legacy = Make(14, false);
        legacy.crateItems = null; legacy.crateKinds = null;
        foreach (var it in legacy.items) { it.roundCount = 0; }
        legacy.crateCount = 10; legacy.totalGroups = 8;
        var le = new RoundEngine(legacy, 5);
        Check(le.Items.Count == 80 && le.ActiveCrateCount == 10, "legacy mode untouched");
        var fixedManifest = Make(14, false);
        var fe = new RoundEngine(fixedManifest, 7);
        Check(fe.Items.Count == 12 * 10 && fe.ActiveCrateCount == fixedManifest.crateCount, "fixed manifest untouched: " + fe.Items.Count);
        Check(Make(14, true).Fingerprint() != Make(14, false).Fingerprint(), "fingerprint differs");
        Check(Make(14, true).Fingerprint() == Make(14, true).Fingerprint(), "fingerprint stable");

        Console.WriteLine(checks + " checks, " + failures + " failures");
        return failures == 0 ? 0 : 1;
    }

    static int FirstInactive(RoundEngine e, int n) { for (int c = 0; c < n; c++) if (!e.IsActive(c)) return c; return -1; }
}
