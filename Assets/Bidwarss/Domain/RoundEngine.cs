using System;
using System.Collections.Generic;

namespace Bidwarss.Domain
{
    public sealed class RoundItem
    {
        public const ulong Nobody = ulong.MaxValue;
        public int id, kind, crate, dollars, stack = -1, stackIndex = -1;
        // Crate kind this item was drawn for (-1 = any). Used only while the crates are assigned.
        public int preferredKind = -1;
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
        readonly int[] stackCaps;
        readonly bool[] opened;
        // Crates that hold something this round. Random depot: only the used part of the reserved crates; otherwise all.
        readonly bool[] active;
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
        // How many items pallet #index takes (10, or fewer for the last pallet of a manifest item).
        public int StackCapacity(int index) => stackCaps[index];
        public int ActiveCrateCount { get; private set; }
        public bool IsActive(int crate) => crate >= 0 && crate < active.Length && active[crate];
        public bool IsOpen(int crate) => crate >= 0 && crate < opened.Length && opened[crate];

        public RoundEngine(GameRules rules, int seed)
        {
            rules.Validate(); Seed = seed; RulesHash = rules.Fingerprint();
            var random = new StableRandom(seed);
            active = new bool[rules.crateCount];
            if (rules.Manifest)
            {
                var mKinds = new List<int>(); var mCaps = new List<int>();
                if (rules.RandomActive) BuildRandom(rules, ref random, mKinds, mCaps);
                else BuildManifest(rules, ref random, mKinds, mCaps);
                stackKinds = mKinds.ToArray(); stackCaps = mCaps.ToArray(); stackCounts = new int[stackKinds.Length];
                opened = new bool[rules.crateCount];
                if (!rules.RandomActive) for (int c = 0; c < active.Length; c++) active[c] = true;
                foreach (var it in items) active[it.crate] = true;
                for (int c = 0; c < active.Length; c++) if (active[c]) ActiveCrateCount++;
                return;
            }
            var groups = new int[rules.items.Length];
            // Item types that at least one crate of this depot accepts.
            var eligible = new List<int>();
            for (int i = 0; i < groups.Length; i++) if (rules.IsEligible(rules.items[i])) eligible.Add(i);
            int assigned = 0;
            // Which crate kind each group of an item type was drawn for (parallel to groups[kind]).
            var groupKinds = new List<int>[groups.Length];
            for (int i = 0; i < groupKinds.Length; i++) groupKinds[i] = new List<int>();
            // Free crate slots per crate kind: one slot per crate. Every group of ten identical items uses ten slots of
            // its kind, so the depot is filled crate by crate (one item each) and the mix of crate kinds matches the mix of items.
            var free = new long[CrateKinds.Count];
            if (rules.crateKinds != null) foreach (int k in rules.crateKinds) free[k]++;
            // parcel boxes hold whole packs: their kind takes exactly PackedGroups groups
            if (rules.crateKinds != null && rules.packedKind >= 0 && rules.packedKind < free.Length) free[rules.packedKind] = (long)rules.PackedGroups * GameRules.StackSize;
            // Few item types in the legacy (kind-less) mode: every type appears (variety).
            if (rules.crateKinds == null && eligible.Count <= rules.totalGroups)
            {
                var types = new List<int>(eligible);
                random.Shuffle(types);
                foreach (int t in types) if (groups[t] == 0 && assigned < rules.totalGroups) { groups[t]++; assigned++; groupKinds[t].Add(-1); }
            }
            while (assigned < rules.totalGroups)
            {
                int before = assigned;
                DrawGroup(rules, ref random, groups, groupKinds, free, ref assigned);
                if (assigned == before) throw new ArgumentException("Eşya türleri toplam istif sayısını karşılamıyor.");
            }
            var stackTypes = new List<int>();
            int conditionWeight = 0;
            foreach (var band in rules.conditions) conditionWeight += band.weight;
            for (int kind = 0; kind < groups.Length; kind++)
            {
                for (int group = 0; group < groups[kind]; group++) stackTypes.Add(kind);
                for (int n = 0; n < groups[kind] * GameRules.StackSize; n++)
                {
                    int group = n / GameRules.StackSize;
                    int condition, dollars;
                    Roll(rules, ref random, kind, conditionWeight, out condition, out dollars);
                    items.Add(new RoundItem { kind = kind, condition = (ItemCondition)condition, dollars = dollars, preferredKind = groupKinds[kind][group] });
                    TotalDollars = checked(TotalDollars + dollars);
                }
            }
            random.Shuffle(items); random.Shuffle(stackTypes);
            stackKinds = stackTypes.ToArray(); stackCounts = new int[stackKinds.Length]; opened = new bool[rules.crateCount];
            for (int c = 0; c < active.Length; c++) active[c] = true;
            ActiveCrateCount = active.Length;
            stackCaps = new int[stackKinds.Length]; for (int i = 0; i < stackCaps.Length; i++) stackCaps[i] = GameRules.StackSize;
            for (int i = 0; i < items.Count; i++) items[i].id = i;
            AssignCrates(rules, ref random);
        }

        // Condition and price of one item: the item's own Kasa Defteri table, or the depot-wide percent bands.
        static void Roll(GameRules rules, ref StableRandom random, int kind, int conditionWeight, out int condition, out int dollars)
        {
            var rule = rules.items[kind];
            condition = 0;
            if (rule.HasConditionTable)
            {
                int sum = 0; for (int c = 0; c < 7; c++) sum += rule.condWeights[c];
                int ticket = random.Range(sum);
                for (; condition < 6; condition++) { ticket -= rule.condWeights[condition]; if (ticket < 0) break; }
                while (rule.condWeights[condition] == 0 && condition > 0) condition--;
                dollars = rule.condMin[condition] + random.Range(rule.condMax[condition] - rule.condMin[condition] + 1);
            }
            else
            {
                int ticket = random.Range(conditionWeight);
                for (; condition < 6; condition++) { ticket -= rules.conditions[condition].weight; if (ticket < 0) break; }
                var band = rules.conditions[condition];
                int percent = band.minimumPercent + random.Range(band.maximumPercent - band.minimumPercent + 1);
                dollars = rules.Price(kind, (ItemCondition)condition, percent);
            }
        }

        // Manifest round: every item type with a roundCount, exactly that many. Every crate holds a pack of ONE item type.
        // Per crate kind the crates are shared among its item types so that no crate holds more than the type's packSize
        // while there are crates to spare (the depot builder places sum(ceil(count/packSize)) crates per kind); every crate
        // gets at least one item as long as the kind has at least as many items as crates.
        void BuildManifest(GameRules rules, ref StableRandom random, List<int> stackKindList, List<int> capList)
        {
            int conditionWeight = 0;
            foreach (var band in rules.conditions) conditionWeight += band.weight;
            var typeKind = new int[rules.items.Length];
            var stackPairs = new List<KeyValuePair<int, int>>();
            for (int kind = 0; kind < rules.items.Length; kind++)
            {
                var rule = rules.items[kind];
                typeKind[kind] = rule.roundCount > 0 ? rules.ManifestKind(rule) : -1;
                if (typeKind[kind] < 0) continue;
                for (int n = 0; n < rule.roundCount; n++)
                {
                    int condition, dollars;
                    Roll(rules, ref random, kind, conditionWeight, out condition, out dollars);
                    items.Add(new RoundItem { kind = kind, condition = (ItemCondition)condition, dollars = dollars, preferredKind = typeKind[kind] });
                    TotalDollars = checked(TotalDollars + dollars);
                }
                for (int left = rule.roundCount; left > 0; left -= GameRules.StackSize)
                    stackPairs.Add(new KeyValuePair<int, int>(kind, Math.Min(GameRules.StackSize, left)));
            }
            random.Shuffle(stackPairs);
            foreach (var p in stackPairs) { stackKindList.Add(p.Key); capList.Add(p.Value); }

            // crates per kind, shuffled
            for (int ck = 0; ck < CrateKinds.Count; ck++)
            {
                var crates = new List<int>();
                for (int c = 0; c < rules.crateKinds.Length; c++) if (rules.crateKinds[c] == ck) crates.Add(c);
                if (crates.Count == 0) continue;
                random.Shuffle(crates);
                var types = new List<int>();
                for (int kind = 0; kind < typeKind.Length; kind++) if (typeKind[kind] == ck) types.Add(kind);
                if (types.Count == 0) continue;
                var count = new int[types.Count]; var share = new int[types.Count];
                // crates the depot builder reserved for one item type (rules.crateItems)
                var own = new List<int>[types.Count];
                var pool = new List<int>();
                for (int t = 0; t < types.Count; t++) own[t] = new List<int>();
                foreach (int c in crates)
                {
                    int want = rules.crateItems != null ? rules.crateItems[c] : -1;
                    int t = want >= 0 ? types.IndexOf(want) : -1;
                    if (t >= 0 && own[t].Count < rules.items[want].roundCount) own[t].Add(c); else pool.Add(c);
                }
                int spare = pool.Count;
                for (int t = 0; t < types.Count; t++)
                {
                    count[t] = rules.items[types[t]].roundCount;
                    share[t] = own[t].Count;
                    if (share[t] == 0 && spare > 0) { share[t] = 1; spare--; }
                }
                // one crate at a time to the type whose crates are fullest relative to its pack size
                while (spare > 0)
                {
                    int best = -1; double bestLoad = 0;
                    for (int t = 0; t < types.Count; t++)
                    {
                        if (share[t] >= count[t]) continue;
                        double load = (double)count[t] / share[t] / Math.Max(1, rules.items[types[t]].packSize);
                        if (best < 0 || load > bestLoad + 1e-9) { best = t; bestLoad = load; }
                    }
                    if (best < 0) break;
                    share[best]++; spare--;
                }
                int next = 0;
                for (int t = 0; t < types.Count; t++)
                {
                    var list = new List<RoundItem>();
                    foreach (var item in items) if (item.kind == types[t]) list.Add(item);
                    var boxes = new List<int>(own[t]);
                    while (boxes.Count < share[t] && next < pool.Count) boxes.Add(pool[next++]);
                    if (boxes.Count == 0) boxes.Add(pool.Count > 0 ? pool[Math.Min(next, pool.Count - 1)] : crates[0]);
                    int n = boxes.Count;
                    for (int k = 0; k < n; k++)
                    {
                        int from = k * list.Count / n, to = (k + 1) * list.Count / n;
                        for (int i = from; i < to; i++) list[i].crate = boxes[k];
                    }
                }
            }
            random.Shuffle(items);
            for (int i = 0; i < items.Count; i++) items[i].id = i;
        }

        // Counts a random round may take: mostly 5/10/15/20 (a lot of 20 is a full two-pallet shelf), sometimes an odd lot.
        public static readonly int[] LotSizes = { 5, 10, 15, 20, 30, 40, 60, 80, 100, 120 };

        // Random depot round. Everything is drawn here, nothing depends on the crate numbers:
        //   1. a pallet budget (half of the pallet slots up to all of them),
        //   2. item types by their weight, each type at most once, with a count of 5/10/15/20 (sometimes an odd lot) that
        //      never exceeds the item's size/weight limit nor what its reserved crates can hold,
        //   3. for every type a random number of its reserved crates (at least as many as the pack size needs),
        //      the items are split evenly over them; every other reserved crate stays empty (inactive).
        void BuildRandom(GameRules rules, ref StableRandom random, List<int> stackKindList, List<int> capList)
        {
            int conditionWeight = 0;
            foreach (var band in rules.conditions) conditionWeight += band.weight;
            int types = rules.items.Length;
            var reserved = new List<int>[types];
            for (int t = 0; t < types; t++) reserved[t] = new List<int>();
            for (int c = 0; c < rules.crateCount; c++)
            {
                int t = rules.crateItems[c];
                if (t >= 0 && t < types) reserved[t].Add(c);
            }
            // candidate types: in the pool, have a crate kind in this depot and at least one reserved crate of that kind
            var cap = new int[types]; var candidates = new List<int>();
            for (int t = 0; t < types; t++)
            {
                var rule = rules.items[t];
                if (!rule.InPool) continue;
                int kind = rules.ManifestKind(rule);
                if (kind < 0) continue;
                reserved[t].RemoveAll(c => rules.crateKinds[c] != kind);
                if (reserved[t].Count == 0) continue;
                cap[t] = Math.Min(rule.EffectiveMaxCount(), reserved[t].Count * Math.Max(1, rule.packSize));
                if (cap[t] < 1) continue;
                candidates.Add(t);
            }
            if (candidates.Count == 0) throw new ArgumentException("Rastgele tur için uygun eşya yok: eşyalara Tur başına adet verin ve Loot > 2 ile kasaları yeniden dizin.");
            // Item target of this round: somewhere in DepotMinItems..DepotMaxItems, cut down to what the depot can hold
            // (every pallet takes 10 items, no type above its own limit). If the depot is smaller than the wanted range the
            // round simply fills 90-100% of what it has.
            int budget = Math.Max(1, rules.totalGroups);
            long capacity = 0; foreach (int t in candidates) capacity += cap[t];
            int possible = (int)Math.Min(Math.Min(capacity, (long)budget * GameRules.StackSize), GameRules.DepotMaxItems);
            int lowest = possible >= GameRules.DepotMinItems ? GameRules.DepotMinItems : Math.Max(1, (9 * possible + 9) / 10);
            int target = lowest + random.Range(possible - lowest + 1);
            var counts = ChooseCounts(rules, cap, candidates, budget, target, ref random);
            var chosen = new List<int>(); var chosenCount = new List<int>();
            for (int t = 0; t < types; t++) if (counts[t] > 0) { chosen.Add(t); chosenCount.Add(counts[t]); }
            var stackPairs = new List<KeyValuePair<int, int>>();
            for (int k = 0; k < chosen.Count; k++)
            {
                int kind = chosen[k];
                for (int n = 0; n < chosenCount[k]; n++)
                {
                    int condition, dollars;
                    Roll(rules, ref random, kind, conditionWeight, out condition, out dollars);
                    items.Add(new RoundItem { kind = kind, condition = (ItemCondition)condition, dollars = dollars, preferredKind = rules.ManifestKind(rules.items[kind]) });
                    TotalDollars = checked(TotalDollars + dollars);
                }
                for (int left = chosenCount[k]; left > 0; left -= GameRules.StackSize)
                    stackPairs.Add(new KeyValuePair<int, int>(kind, Math.Min(GameRules.StackSize, left)));
            }
            random.Shuffle(stackPairs);
            foreach (var p in stackPairs) { stackKindList.Add(p.Key); capList.Add(p.Value); }
            // crates
            for (int k = 0; k < chosen.Count; k++)
            {
                int kind = chosen[k], n = chosenCount[k];
                var boxes = new List<int>(reserved[kind]);
                random.Shuffle(boxes);
                int pack = Math.Max(1, rules.items[kind].packSize);
                int minBoxes = Math.Min(boxes.Count, (n + pack - 1) / pack), maxBoxes = Math.Min(boxes.Count, n);
                // three draws, the largest wins: the depot looks full (items spread over several crates), packed crates are rarer
                int span = maxBoxes - minBoxes + 1;
                int b = minBoxes + Math.Max(Math.Max(random.Range(span), random.Range(span)), random.Range(span));
                var list = new List<RoundItem>();
                foreach (var item in items) if (item.kind == kind) list.Add(item);
                for (int c = 0; c < b; c++)
                {
                    int from = c * list.Count / b, to = (c + 1) * list.Count / b;
                    for (int i = from; i < to; i++) list[i].crate = boxes[c];
                }
            }
            random.Shuffle(items);
            for (int i = 0; i < items.Count; i++) items[i].id = i;
        }

        // Weights of the lot sizes 5/10/15/20 (the same numbers as the Kasa Defteri site).
        static readonly int[] LotWeights = { 3, 4, 3, 2, 2, 2, 1, 1, 1, 1 };

        // How many of an item one round gets, 1..limit: mostly a full lot (5/10/15/20), now and then an odd lot of 3..limit.
        // Same rule as DepotSim.pickCount on the site.
        static int DrawLot(ref StableRandom random, int limit)
        {
            int total = 0;
            for (int i = 0; i < LotSizes.Length; i++) if (LotSizes[i] <= limit) total += LotWeights[i];
            if (total == 0) return limit;
            if (limit >= 4 && random.Range(6) == 0) return 3 + random.Range(Math.Min(limit, 20) - 2);
            int ticket = random.Range(total);
            for (int i = 0; i < LotSizes.Length; i++)
            {
                if (LotSizes[i] > limit) continue;
                ticket -= LotWeights[i];
                if (ticket < 0) return LotSizes[i];
            }
            return limit;
        }

        static int PalletsFor(int count) { return (count + GameRules.StackSize - 1) / GameRules.StackSize; }

        // Next bigger count of a type: the next lot size, finally the type's own limit.
        static int NextLot(int count, int limit)
        {
            foreach (int lot in LotSizes) if (lot > count && lot <= limit) return lot;
            return count < limit ? limit : 0;
        }

        // Counts per item type: types are drawn by weight, each with a lot (5/10/15/20, small things up to 120), until the item
        // target or the pallet budget is used up; then chosen types grow to their next lot while target and pallets allow.
        // Same procedure as DepotSim.chooseCounts on the site, driven by an item target.
        static int[] ChooseCounts(GameRules rules, int[] limits, List<int> candidates, int rackBudget, int target, ref StableRandom random)
        {
            var counts = new int[limits.Length];
            var pool = new List<int>(candidates);
            int left = rackBudget, total = 0;
            while (pool.Count > 0 && left > 0 && total < target)
            {
                long weight = 0; foreach (int t in pool) weight += Math.Max(1, rules.items[t].selectionWeight);
                long ticket = random.RangeLong(weight); int at = 0;
                for (; at < pool.Count - 1; at++) { ticket -= Math.Max(1, rules.items[pool[at]].selectionWeight); if (ticket < 0) break; }
                int kind = pool[at]; pool.RemoveAt(at);
                int n = DrawLot(ref random, Math.Min(limits[kind], left * GameRules.StackSize));
                n = Math.Min(n, Math.Max(3, target - total));
                counts[kind] = n; total += n;
                left -= PalletsFor(n);
            }
            while (left >= 0 && total < target)
            {
                var growable = new List<int>();
                for (int i = 0; i < counts.Length; i++)
                {
                    int next = NextLot(counts[i], limits[i]);
                    if (counts[i] > 0 && next > 0 && PalletsFor(next) - PalletsFor(counts[i]) <= left && total + (next - counts[i]) <= GameRules.DepotMaxItems) growable.Add(i);
                }
                if (growable.Count == 0) break;
                int pick = growable[random.Range(growable.Count)], grown = NextLot(counts[pick], limits[pick]);
                left -= PalletsFor(grown) - PalletsFor(counts[pick]); total += grown - counts[pick]; counts[pick] = grown;
            }
            return counts;
        }

        // One group. With crate kinds: first a crate kind is picked in proportion to its free crate slots, then an item type
        // from that kind's own pool by weight. Rare items are therefore rare, and no kind runs out of crates before the
        // others (the depot is filled to exactly one item per crate whenever the item pools allow it).
        static void DrawGroup(GameRules rules, ref StableRandom random, int[] groups, List<int>[] groupKinds, long[] free, ref int assigned)
        {
            if (rules.crateKinds == null) { PickWeighted(rules, ref random, groups, groupKinds, -1, ref assigned); return; }
            var usable = new bool[CrateKinds.Count];
            for (int k = 0; k < CrateKinds.Count; k++)
                for (int i = 0; i < groups.Length && !usable[k]; i++)
                    usable[k] = groups[i] < rules.items[i].maxGroups && CrateKinds.Allows(rules.items[i].crateMask, k) && rules.IsEligible(rules.items[i]);
            var counts = new long[CrateKinds.Count];
            long total = 0;
            for (int k = 0; k < counts.Length; k++) { if (usable[k] && free[k] > 0) counts[k] = free[k]; total += counts[k]; }
            if (total <= 0)
            {
                // All free slots used up: further groups share crates (two items per crate), spread by crate count.
                foreach (int k in rules.crateKinds) if (usable[k]) { counts[k]++; total++; }
                if (total <= 0) return;
            }
            long ticket = random.RangeLong(total);
            for (int k = 0; k < counts.Length; k++)
            {
                ticket -= counts[k];
                if (ticket >= 0) continue;
                int before = assigned;
                PickWeighted(rules, ref random, groups, groupKinds, k, ref assigned);
                if (assigned > before) free[k] -= GameRules.StackSize;
                return;
            }
        }

        // One weighted draw of an item type (optionally limited to a crate kind); respects maxGroups and crate acceptance.
        static void PickWeighted(GameRules rules, ref StableRandom random, int[] groups, List<int>[] groupKinds, int crateKind, ref int assigned)
        {
            long weight = 0;
            for (int i = 0; i < groups.Length; i++)
                if (Candidate(rules, groups, i, crateKind)) weight += rules.items[i].selectionWeight;
            if (weight <= 0) return;
            long ticket = random.RangeLong(weight);
            for (int i = 0; i < groups.Length; i++)
            {
                if (!Candidate(rules, groups, i, crateKind)) continue;
                ticket -= rules.items[i].selectionWeight;
                if (ticket < 0) { groups[i]++; groupKinds[i].Add(crateKind); assigned++; return; }
            }
        }

        static bool Candidate(GameRules rules, int[] groups, int i, int crateKind)
        { return groups[i] < rules.items[i].maxGroups && rules.IsEligible(rules.items[i]) && (crateKind < 0 || CrateKinds.Allows(rules.items[i].crateMask, crateKind)); }

        // Every item goes into a crate of the kind it was drawn for, always the least loaded one (random among equals).
        // Only when all crates of that kind are taken (load >= 1) and another accepting crate is still empty, it goes there.
        // Crates that ended up empty then take an item from a crate holding several.
        void AssignCrates(GameRules rules, ref StableRandom random)
        {
            int crates = rules.crateCount;
            var load = new int[crates];
            var candidates = new List<int>();
            var done = new bool[items.Count];
            var packed = new bool[crates];
            AssignPacks(rules, ref random, load, done, packed);
            foreach (var item in items)
            {
                if (done[item.id]) continue;
                candidates.Clear(); int best = int.MaxValue;
                for (int c = 0; c < crates; c++)
                {
                    if (packed[c] || !rules.Accepts(rules.items[item.kind], c)) continue;
                    if (rules.crateKinds != null && item.preferredKind >= 0 && rules.crateKinds[c] != item.preferredKind) continue;
                    if (load[c] < best) { best = load[c]; candidates.Clear(); }
                    if (load[c] == best) candidates.Add(c);
                }
                if (candidates.Count == 0 || best >= 1)
                {
                    int bestAny = candidates.Count == 0 ? int.MaxValue : best;
                    var any = new List<int>(); int anyBest = int.MaxValue;
                    for (int c = 0; c < crates; c++)
                    {
                        if (packed[c] || !rules.Accepts(rules.items[item.kind], c)) continue;
                        if (load[c] < anyBest) { anyBest = load[c]; any.Clear(); }
                        if (load[c] == anyBest) any.Add(c);
                    }
                    if (any.Count > 0 && anyBest < bestAny) { candidates.Clear(); candidates.AddRange(any); }
                }
                if (candidates.Count == 0) { for (int c = 0; c < crates; c++) if (!packed[c]) candidates.Add(c); } // cannot happen for eligible kinds
                if (candidates.Count == 0) { for (int c = 0; c < crates; c++) candidates.Add(c); }
                int pick = candidates[candidates.Count == 1 ? 0 : random.Range(candidates.Count)];
                item.crate = pick; load[pick]++;
            }
            for (int c = 0; c < crates; c++)
            {
                if (load[c] > 0 || packed[c]) continue;
                RoundItem donor = null;
                foreach (var item in items)
                    if (!packed[item.crate] && load[item.crate] > 1 && rules.Accepts(rules.items[item.kind], c) && (donor == null || load[item.crate] > load[donor.crate])) donor = item;
                if (donor == null) continue;
                load[donor.crate]--; donor.crate = c; load[c]++;
            }
        }

        // Parcel boxes: every group drawn for the parcel kind is split over a few boxes (2 boxes = 5+5, 3 boxes = 4+3+3).
        // Groups of bigger things (small packSize) get the extra boxes, so a box holds 3-5 of one item type.
        void AssignPacks(GameRules rules, ref StableRandom random, int[] load, bool[] done, bool[] packed)
        {
            if (rules.packedKind < 0 || rules.crateKinds == null) return;
            var boxes = new List<int>();
            for (int c = 0; c < rules.crateKinds.Length; c++) if (rules.crateKinds[c] == rules.packedKind) boxes.Add(c);
            if (boxes.Count == 0) return;
            random.Shuffle(boxes);
            // groups of ten (same item type, drawn for the parcel kind), in item order
            var byKind = new SortedDictionary<int, List<RoundItem>>();
            foreach (var item in items)
            {
                if (item.preferredKind != rules.packedKind) continue;
                List<RoundItem> list;
                if (!byKind.TryGetValue(item.kind, out list)) { list = new List<RoundItem>(); byKind[item.kind] = list; }
                list.Add(item);
            }
            var groupList = new List<List<RoundItem>>();
            foreach (var pair in byKind)
                for (int i = 0; i < pair.Value.Count; i += GameRules.StackSize)
                    groupList.Add(pair.Value.GetRange(i, Math.Min(GameRules.StackSize, pair.Value.Count - i)));
            if (groupList.Count == 0) return;
            // bigger things first: they get the extra boxes (fewer per box)
            groupList.Sort((x, y) =>
            {
                int px = rules.items[x[0].kind].packSize, py = rules.items[y[0].kind].packSize;
                return px != py ? px.CompareTo(py) : x[0].kind.CompareTo(y[0].kind);
            });
            int g = groupList.Count, b = boxes.Count, next = 0;
            for (int gi = 0; gi < g && next < b; gi++)
            {
                int n = Math.Max(1, b / g + (gi < b % g ? 1 : 0));
                n = Math.Min(n, b - next);
                var group = groupList[gi];
                for (int k = 0; k < n; k++)
                {
                    int box = boxes[next + k];
                    packed[box] = true;
                    // even split, the first boxes take the remainder (10 over 3 boxes: 4,3,3)
                    int from = k * group.Count / n, to = (k + 1) * group.Count / n;
                    for (int i = from; i < to; i++) { group[i].crate = box; load[box]++; done[group[i].id] = true; }
                }
                next += n;
            }
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
            if (crate >= 0 && crate < active.Length && !active[crate]) { error = "Bu kutu bu turda boş."; return false; }
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
            if (stackCounts[stack] >= stackCaps[stack]) { error = "Bu istif tamamlandı: " + stackCaps[stack] + "/" + stackCaps[stack] + "."; return 0; }
            int placed = 0;
            foreach (var item in items)
            {
                if (item.holder != player || item.location != ItemLocation.Held || stackCounts[stack] >= stackCaps[stack]) continue;
                item.location = ItemLocation.Stacked; item.holder = RoundItem.Nobody; item.stack = stack;
                item.stackIndex = stackCounts[stack]++; placed++; PlacedCount++; SecuredDollars = checked(SecuredDollars + item.dollars);
            }
            if (PlacedCount == items.Count && OpenedCount == ActiveCrateCount)
            {
                Completed = true;
                for (int i = 0; i < stackCounts.Length; i++) if (stackCounts[i] != stackCaps[i]) Completed = false;
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
