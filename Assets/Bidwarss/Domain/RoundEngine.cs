using System;
using System.Collections.Generic;

namespace Bidwarss.Domain
{
    public sealed class RoundItem
    {
        public const ulong Nobody = ulong.MaxValue;
        public int id, kind, crate, crateSlot, dollars, stack = -1, stackIndex = -1;
        public ItemCondition condition;
        public ItemLocation location;
        public ulong holder = Nobody;
    }

    // Engine-independent state machine. Unity owns spatial validation and networking.
    // Every mutating method is called on the game server only.
    //
    // A depot is rolled completely from the seed: which containers are in use, how many racks are filled,
    // which item types appear, how many of each (usually 5, 10, 15 or 20, fewer for big pieces),
    // which container each piece sits in and what condition and price it has.
    public sealed class RoundEngine
    {
        readonly List<RoundItem> items = new List<RoundItem>();
        readonly int[] stackKinds;
        readonly int[] stackCapacities;
        readonly int[] stackCounts;
        readonly bool[] active;
        readonly bool[] opened;
        public IReadOnlyList<RoundItem> Items => items.AsReadOnly();
        public int Seed { get; private set; }
        public string RulesHash { get; private set; }
        public int StackCount => stackKinds.Length;
        public int CrateCount => opened.Length;
        public int ActiveCrateCount { get; private set; }
        public int OpenedCount { get; private set; }
        public int PlacedCount { get; private set; }
        public int SecuredDollars { get; private set; }
        public int FinalDollars { get; private set; }
        public bool Completed { get; private set; }
        public int TotalDollars { get; private set; }
        public int StackKind(int index) => stackKinds[index];
        public int StackCapacity(int index) => stackCapacities[index];
        public int StackCountAt(int index) => stackCounts[index];
        public bool IsActive(int crate) => crate >= 0 && crate < active.Length && active[crate];
        public bool IsOpen(int crate) => crate >= 0 && crate < opened.Length && opened[crate];

        public RoundEngine(GameRules rules, int seed)
        {
            rules.Validate(); Seed = seed; RulesHash = rules.Fingerprint();
            var random = new StableRandom(seed);
            int kinds = rules.items.Length;
            var tables = new ClassTable[kinds];
            var limits = new int[kinds];
            for (int i = 0; i < kinds; i++) { tables[i] = rules.items[i].Table(); limits[i] = rules.items[i].EffectiveMaxCount(); }

            int wantedCrates = rules.minCrates + random.Range(rules.crateCount - rules.minCrates + 1);
            int rackBudget = rules.minGroups + random.Range(rules.totalGroups - rules.minGroups + 1);
            var counts = ChooseCounts(rules.items, limits, rackBudget, ref random);

            // Racks: one per ten pieces of a type, the last one smaller. Their order in the depot is random too.
            var rackKinds = new List<int>();
            var rackCapacities = new List<int>();
            for (int kind = 0; kind < kinds; kind++)
                for (int remaining = counts[kind]; remaining > 0; remaining -= GameRules.StackSize)
                { rackKinds.Add(kind); rackCapacities.Add(Math.Min(GameRules.StackSize, remaining)); }
            var rackOrder = new List<int>();
            for (int i = 0; i < rackKinds.Count; i++) rackOrder.Add(i);
            random.Shuffle(rackOrder);
            stackKinds = new int[rackOrder.Count]; stackCapacities = new int[rackOrder.Count]; stackCounts = new int[rackOrder.Count];
            for (int i = 0; i < rackOrder.Count; i++) { stackKinds[i] = rackKinds[rackOrder[i]]; stackCapacities[i] = rackCapacities[rackOrder[i]]; }

            // Pieces: condition and price come from the item's own class table.
            for (int kind = 0; kind < kinds; kind++)
            {
                var table = tables[kind];
                for (int n = 0; n < counts[kind]; n++)
                {
                    int ticket = random.Range(table.totalWeight), condition = 0;
                    for (; condition < GameRules.ConditionCount - 1; condition++) { ticket -= table.weight[condition]; if (ticket < 0) break; }
                    int dollars = table.min[condition] + random.Range(table.max[condition] - table.min[condition] + 1);
                    items.Add(new RoundItem { kind = kind, condition = (ItemCondition)condition, dollars = dollars });
                    TotalDollars = checked(TotalDollars + dollars);
                }
            }
            random.Shuffle(items);

            // Containers: a random subset is in use and they hold very different amounts.
            active = new bool[rules.crateCount]; opened = new bool[rules.crateCount];
            var crateOrder = new List<int>();
            for (int i = 0; i < rules.crateCount; i++) crateOrder.Add(i);
            random.Shuffle(crateOrder);
            ActiveCrateCount = Math.Min(wantedCrates, items.Count);
            var used = crateOrder.GetRange(0, ActiveCrateCount);
            var share = new int[rules.crateCount];
            foreach (int crate in used) { active[crate] = true; share[crate] = 1 + random.Range(4); }
            var load = new int[rules.crateCount];
            for (int i = 0; i < items.Count; i++)
            {
                int crate;
                if (i < used.Count) crate = used[i]; // nobody gets an empty container
                else
                {
                    int total = 0;
                    foreach (int c in used) if (load[c] < GameRules.CrateLimit) total += share[c];
                    int ticket = random.Range(total); crate = -1;
                    foreach (int c in used)
                    {
                        if (load[c] >= GameRules.CrateLimit) continue;
                        ticket -= share[c];
                        if (ticket < 0) { crate = c; break; }
                    }
                }
                items[i].id = i; items[i].crate = crate; items[i].crateSlot = load[crate]++;
            }
        }

        // How many pieces of each type. A first pass hands racks to types in random order,
        // a second pass tops up types that still have room while racks are left over.
        static int[] ChooseCounts(ItemRule[] rules, int[] limits, int rackBudget, ref StableRandom random)
        {
            var counts = new int[rules.Length];
            var pool = new List<int>();
            for (int i = 0; i < rules.Length; i++) pool.Add(i);
            int left = rackBudget;
            while (pool.Count > 0 && left > 0)
            {
                int total = 0;
                foreach (int p in pool) total += rules[p].selectionWeight;
                int ticket = random.Range(total), at = 0;
                for (; at < pool.Count - 1; at++) { ticket -= rules[pool[at]].selectionWeight; if (ticket < 0) break; }
                int kind = pool[at]; pool.RemoveAt(at);
                counts[kind] = PickCount(Math.Min(limits[kind], left * GameRules.StackSize), ref random);
                left -= GameRules.RacksFor(counts[kind]);
            }
            while (left > 0)
            {
                var growable = new List<int>();
                for (int i = 0; i < counts.Length; i++)
                {
                    int next = NextOption(counts[i], limits[i]);
                    if (counts[i] > 0 && next > 0 && GameRules.RacksFor(next) - GameRules.RacksFor(counts[i]) <= left) growable.Add(i);
                }
                if (growable.Count == 0) break;
                int pick = growable[random.Range(growable.Count)], grown = NextOption(counts[pick], limits[pick]);
                left -= GameRules.RacksFor(grown) - GameRules.RacksFor(counts[pick]); counts[pick] = grown;
            }
            return counts;
        }

        // Mostly 5, 10, 15 or 20; now and then an odd lot such as 7 or 13.
        static int PickCount(int limit, ref StableRandom random)
        {
            int total = 0;
            for (int i = 0; i < GameRules.CountOptions.Length; i++) if (GameRules.CountOptions[i] <= limit) total += GameRules.CountWeights[i];
            if (total == 0) return limit;
            if (limit >= 4 && random.Range(6) == 0) return 3 + random.Range(limit - 2);
            int ticket = random.Range(total);
            for (int i = 0; i < GameRules.CountOptions.Length; i++)
            {
                if (GameRules.CountOptions[i] > limit) continue;
                ticket -= GameRules.CountWeights[i];
                if (ticket < 0) return GameRules.CountOptions[i];
            }
            return limit;
        }

        static int NextOption(int count, int limit)
        {
            foreach (int option in GameRules.CountOptions) if (option > count && option <= limit) return option;
            return 0;
        }

        public int HeldCount(ulong player, out int kind)
        {
            kind = -1; int count = 0;
            foreach (var item in items) if (item.holder == player && item.location == ItemLocation.Held) { count++; kind = item.kind; }
            return count;
        }

        public bool Open(int crate, out string error)
        {
            error = "";
            if (Completed || crate < 0 || crate >= opened.Length || opened[crate]) { error = "Bu kutu zaten açık."; return false; }
            if (!active[crate]) { error = "Bu konteyner bu turda kullanılmıyor."; return false; }
            opened[crate] = true; OpenedCount++;
            foreach (var item in items) if (item.crate == crate) item.location = ItemLocation.Loose;
            return true;
        }

        public bool Take(int id, ulong player, out string error)
        {
            error = "";
            if (Completed || player == RoundItem.Nobody || id < 0 || id >= items.Count) { error = "İşlem geçersiz."; return false; }
            var item = items[id];
            if (item.location != ItemLocation.Loose && item.location != ItemLocation.Stacked) { error = "Eşyayı başka oyuncu almış olabilir."; return false; }
            int kind; int count = HeldCount(player, out kind);
            if (count >= GameRules.StackSize) { error = "En fazla 10 eşya taşıyabilirsin."; return false; }
            if (count > 0 && kind != item.kind) { error = "Elde yalnız aynı tür eşyalar birikir."; return false; }
            if (item.location == ItemLocation.Stacked)
            {
                // Only the final item of a stack can be removed; prevents floating gaps.
                if (item.stackIndex != stackCounts[item.stack] - 1) { error = "İstifin son eşyasını al."; return false; }
                stackCounts[item.stack]--; PlacedCount--; SecuredDollars -= item.dollars;
            }
            item.location = ItemLocation.Held; item.holder = player; item.stack = -1; item.stackIndex = -1;
            return true;
        }

        public int Place(int stack, ulong player, out string error)
        {
            error = "";
            if (Completed || stack < 0 || stack >= stackKinds.Length) { error = "İstif geçersiz."; return 0; }
            int kind; int count = HeldCount(player, out kind);
            if (count == 0) { error = "Elinde eşya yok."; return 0; }
            if (kind != stackKinds[stack]) { error = "Bu alan başka bir eşya türüne ayrılmış."; return 0; }
            if (stackCounts[stack] >= stackCapacities[stack]) { error = "Bu istif tamamlandı: " + stackCapacities[stack] + "/" + stackCapacities[stack] + "."; return 0; }
            int placed = 0;
            foreach (var item in items)
            {
                if (item.holder != player || item.location != ItemLocation.Held || stackCounts[stack] >= stackCapacities[stack]) continue;
                item.location = ItemLocation.Stacked; item.holder = RoundItem.Nobody; item.stack = stack;
                item.stackIndex = stackCounts[stack]++; placed++; PlacedCount++; SecuredDollars = checked(SecuredDollars + item.dollars);
            }
            if (PlacedCount == items.Count && OpenedCount == ActiveCrateCount)
            {
                Completed = true;
                for (int i = 0; i < stackCounts.Length; i++) if (stackCounts[i] != stackCapacities[i]) Completed = false;
                if (Completed) FinalDollars = SecuredDollars; // Exactly one immutable payout.
            }
            return placed;
        }

        public bool Drop(int id, ulong player)
        {
            if (Completed || id < 0 || id >= items.Count) return false;
            var item = items[id];
            if (item.location != ItemLocation.Held || item.holder != player) return false;
            item.holder = RoundItem.Nobody; item.location = ItemLocation.Loose;
            return true;
        }

        public void Disconnect(ulong player)
        { foreach (var item in items) if (item.holder == player) Drop(item.id, player); }
    }
}
