using System;
using System.Collections.Generic;

namespace Bidwarss.Domain
{
    public sealed class RoundItem
    {
        public const ulong Nobody = ulong.MaxValue;
        public int id, kind, crate, dollars, stack = -1, stackIndex = -1;
        public ItemCondition condition;
        public ItemLocation location;
        public ulong holder = Nobody;
    }

    // Engine-independent state machine. Unity owns spatial validation and networking.
    // Every mutating method is called on the game server only.
    public sealed class RoundEngine
    {
        readonly List<RoundItem> items = new List<RoundItem>();
        readonly int[] stackKinds;
        readonly int[] stackCounts;
        readonly bool[] opened;
        public IReadOnlyList<RoundItem> Items => items.AsReadOnly();
        public int Seed { get; private set; }
        public string RulesHash { get; private set; }
        public int StackCount => stackKinds.Length;
        public int CrateCount => opened.Length;
        public int OpenedCount { get; private set; }
        public int PlacedCount { get; private set; }
        public int SecuredDollars { get; private set; }
        public int FinalDollars { get; private set; }
        public bool Completed { get; private set; }
        public int TotalDollars { get; private set; }
        public int StackKind(int index) => stackKinds[index];
        public int StackCountAt(int index) => stackCounts[index];
        public bool IsOpen(int crate) => crate >= 0 && crate < opened.Length && opened[crate];

        public RoundEngine(GameRules rules, int seed)
        {
            rules.Validate(); Seed = seed; RulesHash = rules.Fingerprint();
            var random = new StableRandom(seed);
            var groups = new int[rules.items.Length];
            int assigned = 0;
            if (groups.Length <= rules.totalGroups)
            {
                // Variety: when the group budget permits every type, each appears at least once.
                var types = new List<int>();
                for (int i = 0; i < groups.Length; i++) types.Add(i);
                random.Shuffle(types);
                for (int i = 0; i < groups.Length; i++) { groups[types[i]]++; assigned++; }
            }
            else
            {
                // More types than groups: draw distinct types by selection weight, so common goods
                // show up often and rare collectibles seldom.
                var pool = new List<int>();
                for (int i = 0; i < groups.Length; i++) pool.Add(i);
                while (assigned < rules.totalGroups)
                {
                    int weight = 0;
                    foreach (int i in pool) weight += rules.items[i].selectionWeight;
                    int ticket = random.Range(weight);
                    for (int p = 0; p < pool.Count; p++)
                    {
                        ticket -= rules.items[pool[p]].selectionWeight;
                        if (ticket < 0) { groups[pool[p]]++; assigned++; pool.RemoveAt(p); break; }
                    }
                }
            }
            while (assigned < rules.totalGroups)
            {
                int weight = 0;
                for (int i = 0; i < groups.Length; i++) if (groups[i] < rules.items[i].maxGroups) weight += rules.items[i].selectionWeight;
                int ticket = random.Range(weight);
                for (int i = 0; i < groups.Length; i++)
                {
                    if (groups[i] >= rules.items[i].maxGroups) continue;
                    ticket -= rules.items[i].selectionWeight;
                    if (ticket < 0) { groups[i]++; assigned++; break; }
                }
            }
            var stackTypes = new List<int>();
            int conditionWeight = 0;
            foreach (var band in rules.conditions) conditionWeight += band.weight;
            for (int kind = 0; kind < groups.Length; kind++)
            {
                for (int group = 0; group < groups[kind]; group++) stackTypes.Add(kind);
                for (int n = 0; n < groups[kind] * GameRules.StackSize; n++)
                {
                    int ticket = random.Range(conditionWeight), condition = 0;
                    for (; condition < 6; condition++) { ticket -= rules.conditions[condition].weight; if (ticket < 0) break; }
                    int low, high;
                    rules.PriceBand(kind, (ItemCondition)condition, out low, out high);
                    int dollars = low + random.Range(high - low + 1);
                    items.Add(new RoundItem { kind = kind, condition = (ItemCondition)condition, dollars = dollars });
                    TotalDollars = checked(TotalDollars + dollars);
                }
            }
            random.Shuffle(items); random.Shuffle(stackTypes);
            stackKinds = stackTypes.ToArray(); stackCounts = new int[stackKinds.Length]; opened = new bool[rules.crateCount];
            for (int i = 0; i < items.Count; i++) { items[i].id = i; items[i].crate = i % rules.crateCount; }
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
            if (stackCounts[stack] == GameRules.StackSize) { error = "Bu istif tamamlandı: 10/10."; return 0; }
            int placed = 0;
            foreach (var item in items)
            {
                if (item.holder != player || item.location != ItemLocation.Held || stackCounts[stack] == GameRules.StackSize) continue;
                item.location = ItemLocation.Stacked; item.holder = RoundItem.Nobody; item.stack = stack;
                item.stackIndex = stackCounts[stack]++; placed++; PlacedCount++; SecuredDollars = checked(SecuredDollars + item.dollars);
            }
            if (PlacedCount == items.Count && OpenedCount == opened.Length)
            {
                Completed = true;
                foreach (int filled in stackCounts) if (filled != GameRules.StackSize) Completed = false;
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
