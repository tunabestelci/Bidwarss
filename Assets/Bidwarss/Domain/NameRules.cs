using System.Globalization;
using System.Text;

namespace Bidwarss.Domain
{
    // Engine-independent player-name sanitising so the exact same rules run in the game and in tests.
    public static class NameRules
    {
        public const int MaxLength = 16;
        public const string Fallback = "Oyuncu";

        public static string Clean(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Fallback;
            var result = new StringBuilder();
            bool lastSpace = false;
            foreach (char c in value.Trim())
            {
                if (result.Length >= MaxLength) break;
                if (char.IsControl(c) || char.IsSurrogate(c)) continue;
                var category = char.GetUnicodeCategory(c);
                // Zero-width and other invisible formatting characters allow impersonation.
                if (category == UnicodeCategory.Format || category == UnicodeCategory.PrivateUse ||
                    category == UnicodeCategory.OtherNotAssigned) continue;
                if (c == '<' || c == '>' || c == '/' || c == '\\' || c == '|') continue;
                bool space = char.IsWhiteSpace(c);
                if (space && lastSpace) continue;
                result.Append(space ? ' ' : c);
                lastSpace = space;
            }
            string clean = result.ToString().Trim();
            return clean.Length > 0 ? clean : Fallback;
        }
    }
}
