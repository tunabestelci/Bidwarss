using System;
using System.Text;

namespace Bidwarss.Domain
{
    // Epic (Cok iyi) and legendary (Efsane) finds are valuable because they once belonged to somebody famous:
    // "Mira Starling'in Motosikleti". The owners are invented stage names, never real people.
    // Purely cosmetic and derived from (seed, item id) without touching the round's random stream,
    // so prices, groups and conditions are identical with or without it.
    public static class Provenance
    {
        public const int None = -1;

        static readonly string[] First = {
            "Mira", "Lara", "Selin", "Arda", "Kaan", "Nova", "Ece", "Deniz", "Rüya", "Aras", "Luna", "Emre",
            "Zeynep", "Leo", "Ayla", "Orion", "Defne", "Rex", "Naz", "Cem", "Stella", "Bora", "Sera", "Atlas" };
        static readonly string[] Last = {
            "Starling", "Vexley", "Noirel", "Valdis", "Kozmo", "Aurelio", "Zephyr", "Lunaris", "Montera", "Rivage", "Solace", "Draven",
            "Kaplan-Rose", "Yıldırım Vale", "Blackwood", "Marlowe", "Cascade", "Moretti Vane", "Sunder", "Halcyon", "Ardent", "Kestrel", "Velvet", "Ember" };

        public static int NameCount => First.Length * Last.Length;
        public static bool Applies(ItemCondition condition) => condition >= ItemCondition.VeryGood;

        // Deterministic owner for one item of one round; None for ordinary conditions.
        public static int Pick(int seed, int itemId, ItemCondition condition)
        {
            if (!Applies(condition)) return None;
            var random = new StableRandom(unchecked((int)(((uint)seed * 2654435761u) ^ ((uint)(itemId + 1) * 40503u) ^ 0x5bd1e995u)));
            random.Next(); random.Next(); // warm-up: xorshift needs a few steps to spread a small seed
            return random.Range(NameCount);
        }

        public static string Owner(int star)
        {
            if (star < 0 || star >= NameCount) return "";
            return First[star % First.Length] + " " + Last[star / First.Length];
        }

        // Genitive of a name with Turkish vowel harmony: Mira Starling -> Mira Starling'in, Mira Lunara -> Mira Lunara'nın.
        public static string Genitive(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            int last = -1;
            for (int i = name.Length - 1; i >= 0; i--) if (IsVowel(name[i])) { last = i; break; }
            if (last < 0) return name + "'in";
            char v = char.ToLowerInvariant(name[last]);
            string suffix = (v == 'a' || v == 'ı') ? "ın" : (v == 'e' || v == 'i') ? "in" : (v == 'o' || v == 'u') ? "un" : "ün";
            return name + "'" + (IsVowel(name[name.Length - 1]) ? "n" : "") + suffix;
        }

        // "Mira Starling'in Motosikleti" from the possessed form stored in the catalog ("motosikleti").
        public static string Title(string owned, int star)
        {
            string owner = Owner(star);
            if (owner.Length == 0 || string.IsNullOrWhiteSpace(owned)) return owned ?? "";
            return Genitive(owner) + " " + Capitalize(owned);
        }

        static string Capitalize(string text)
        {
            var sb = new StringBuilder(text.Length);
            bool start = true;
            foreach (char c in text)
            {
                // Turkish casing: i -> İ, ı -> I.
                sb.Append(start && char.IsLetter(c) ? (c == 'i' ? 'İ' : c == 'ı' ? 'I' : char.ToUpperInvariant(c)) : c);
                start = c == ' ';
            }
            return sb.ToString();
        }

        static bool IsVowel(char c)
        {
            switch (char.ToLowerInvariant(c))
            {
                case 'a': case 'e': case 'ı': case 'i': case 'o': case 'ö': case 'u': case 'ü': return true;
                default: return false;
            }
        }
    }
}
