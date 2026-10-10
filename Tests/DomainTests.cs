using System;
using System.Collections.Generic;
using System.Linq;
using Bidwarss.Domain;

// Dependency-free test runner: every test runs even if an earlier one fails, and the exit code reports the result.
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
    static GameRules Rules() => new GameRules { items = new[] {
        new ItemRule { key = "mirror", baseDollars = 80 }, new ItemRule { key = "table", baseDollars = 100 },
        new ItemRule { key = "chair", baseDollars = 60 }, new ItemRule { key = "radio", baseDollars = 120 }, new ItemRule { key = "lamp", baseDollars = 50 } } };

    public static int Main()
    {
        Run("generation", Generation);
        Run("extreme seeds", ExtremeSeeds);
        Run("condition distribution", ConditionDistribution);
        Run("full round", FullRound);
        Run("disconnect hand-over", DisconnectHandOver);
        Run("price floor", PriceFloor);
        Run("rule limits", RuleLimits);
        Run("player names", PlayerNames);
        if (failures.Count > 0)
        {
            foreach (var failure in failures) Console.Error.WriteLine("FAIL " + failure);
            Console.Error.WriteLine(failures.Count + " test(s) failed after " + checks + " assertions.");
            return 1;
        }
        Console.WriteLine("PASS: " + checks + " assertions; 2000 generated scenarios; inventory, races, completion, pricing, limits and player names.");
        return 0;
    }

    static void Generation()
    {
        var rules = Rules();
        for (int seed = -1000; seed < 1000; seed++)
        {
            var round = new RoundEngine(rules, seed);
            Check(round.Items.Count == 120, "Budget must be exact");
            foreach (var group in round.Items.GroupBy(x => x.kind))
            {
                Check(group.Count() % 10 == 0, "Per-type totals must be divisible by ten");
                Check(group.Count() <= rules.items[group.Key].maxGroups * 10, "Type cap exceeded");
            }
            Check(round.Items.Select(x => x.id).Distinct().Count() == 120, "Unique ids");
            Check(round.Items.GroupBy(x => x.crate).All(g => g.Count() == 12), "Balanced boxes");
            foreach (var item in round.Items)
            {
                var band = rules.conditions[(int)item.condition];
                Check(item.dollars >= rules.items[item.kind].baseDollars * band.minimumPercent / 100 && item.dollars <= rules.items[item.kind].baseDollars * band.maximumPercent / 100, "Condition price bounds");
            }
        }
        var a = new RoundEngine(rules, 11); var b = new RoundEngine(rules, 11);
        Check(a.Items.Select(x => x.kind + ":" + x.dollars + ":" + x.crate).SequenceEqual(b.Items.Select(x => x.kind + ":" + x.dollars + ":" + x.crate)), "Seed reproducibility");
        Check(a.RulesHash == b.RulesHash && a.RulesHash.Length == 64, "Rules hash is a stable SHA-256 hex string");
        var c = new RoundEngine(rules, 12);
        Check(!a.Items.Select(x => x.kind + ":" + x.dollars).SequenceEqual(c.Items.Select(x => x.kind + ":" + x.dollars)), "Different seeds give different depots");
    }

    static void ExtremeSeeds()
    {
        var rules = Rules();
        foreach (int seed in new[] { 0, int.MinValue, int.MaxValue, -1, 1 })
        {
            var round = new RoundEngine(rules, seed);
            Check(round.Items.Count == 120 && round.TotalDollars > 0, "Seed " + seed + " produces a valid depot");
        }
    }

    static void ConditionDistribution()
    {
        var rules = Rules();
        var counts = new int[7];
        for (int seed = 0; seed < 200; seed++)
            foreach (var item in new RoundEngine(rules, seed).Items) counts[(int)item.condition]++;
        for (int i = 0; i < 7; i++) Check(counts[i] > 0, "Condition " + GameRules.ConditionNames[i] + " must occur");
        Check(counts[(int)ItemCondition.Average] > counts[(int)ItemCondition.Legendary], "Legendary must be rarer than Average");
    }

    static void FullRound()
    {
        var rules = Rules(); string error;
        var a = new RoundEngine(rules, 11);
        Check(!a.Take(0, 0, out error), "Sealed item cannot be picked up");
        Check(a.Open(0, out error), "Open once"); Check(!a.Open(0, out error), "Duplicate open rejected");
        Check(a.Take(0, 0, out error), "Host client id zero supported"); Check(!a.Take(0, 1, out error), "Double pickup rejected");
        Check(!a.Drop(0, 1), "Other player cannot drop held item");
        a.Disconnect(0); Check(a.Items[0].location == ItemLocation.Loose, "Disconnect releases item");
        for (int c = 1; c < a.CrateCount; c++) a.Open(c, out error);
        int first = a.Items[0].kind;
        foreach (var item in a.Items.Where(x => x.kind == first).Take(10)) Check(a.Take(item.id, 1, out error), "Collect ten");
        int foreign = a.Items.First(x => x.kind != first).id;
        Check(!a.Take(foreign, 1, out error), "Mixed carry rejected");
        var extra = a.Items.FirstOrDefault(x => x.kind == first && x.location == ItemLocation.Loose);
        if (extra != null) Check(!a.Take(extra.id, 1, out error), "Carry cap");
        int correct = Enumerable.Range(0, a.StackCount).First(s => a.StackKind(s) == first);
        int wrong = Enumerable.Range(0, a.StackCount).First(s => a.StackKind(s) != first);
        Check(a.Place(wrong, 1, out error) == 0, "Wrong category placement rejected");
        Check(a.Place(correct, 1, out error) == 10, "Deposit complete bundle");
        var placed = a.Items.Where(x => x.stack == correct).OrderBy(x => x.stackIndex).ToArray();
        Check(!a.Take(placed[0].id, 2, out error), "Cannot remove middle of stack");
        Check(a.Take(placed[9].id, 2, out error), "Can retrieve top item");
        Check(a.PlacedCount == 9, "Retrieve updates progress");
        Check(a.Place(correct, 2, out error) == 1, "Return item");
        Check(a.Place(correct, 2, out error) == 0, "Duplicate placement has no payout");
        for (int stack = 0; stack < a.StackCount; stack++)
        {
            foreach (var item in a.Items.Where(x => x.kind == a.StackKind(stack) && x.location == ItemLocation.Loose).Take(10).ToArray()) a.Take(item.id, 3, out error);
            a.Place(stack, 3, out error);
            // A full stack can leave carried items; place them in the next matching stack.
            int kind; if (a.HeldCount(3, out kind) > 0)
            {
                int dest = Enumerable.Range(0, a.StackCount).First(s => a.StackKind(s) == kind && a.StackCountAt(s) < 10);
                a.Place(dest, 3, out error);
            }
        }
        Check(a.Completed, "Complete only when all items and stacks done");
        Check(a.FinalDollars == a.Items.Sum(x => x.dollars), "Exact payout sum");
        int paid = a.FinalDollars;
        Check(!a.Take(0, 0, out error) && a.Place(0, 0, out error) == 0 && a.FinalDollars == paid, "Finished run immutable");
    }

    static void DisconnectHandOver()
    {
        var round = new RoundEngine(Rules(), 5); string error;
        round.Open(0, out error);
        var item = round.Items.First(x => x.crate == 0);
        Check(round.Take(item.id, 7, out error), "First player takes the item");
        int kind;
        Check(round.HeldCount(7, out kind) == 1 && kind == item.kind, "Hand holds one item");
        round.Disconnect(7);
        Check(round.HeldCount(7, out kind) == 0, "Disconnect empties the hand");
        Check(round.Take(item.id, 8, out error), "Released item can be taken by a teammate");
        Check(item.holder == 8 && item.location == ItemLocation.Held, "New owner recorded");
    }

    static void PriceFloor()
    {
        var rules = Rules(); rules.items[0].baseDollars = 1;
        Check(rules.Price(0, ItemCondition.Terrible, 15) == 1, "Prices never drop below one dollar");
        Check(rules.Price(0, ItemCondition.Legendary, 600) == 6, "Legendary multiplier applies");
        bool threw = false;
        try { rules.Price(0, ItemCondition.Terrible, 99); } catch (ArgumentOutOfRangeException) { threw = true; }
        Check(threw, "Percent outside the condition band is rejected");
    }

    static void RuleLimits()
    {
        var rules = Rules();
        var invalid = Rules(); invalid.totalGroups = 30;
        bool threw = false; try { new RoundEngine(invalid, 1); } catch (ArgumentException) { threw = true; }
        Check(threw, "Impossible capacity rejected");
        var constrained = Rules(); constrained.totalGroups = 5; foreach (var i in constrained.items) i.maxGroups = 1;
        var limited = new RoundEngine(constrained, 8); Check(limited.Items.GroupBy(x => x.kind).All(g => g.Count() == 10), "Exact caps");
        var changed = Rules(); changed.items[0].baseDollars++;
        Check(changed.Fingerprint() != rules.Fingerprint(), "Changed balance uses different ranking board");
        var duplicate = Rules(); duplicate.items[1].key = duplicate.items[0].key;
        threw = false; try { duplicate.Validate(); } catch (ArgumentException) { threw = true; }
        Check(threw, "Duplicate item keys rejected");
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
}
