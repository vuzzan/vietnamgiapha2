using System.Text;

namespace vietnamgiapha.AI
{
    /// <summary>Gói intent + câu trả lời fact — dùng nếu sau này cần LLM polish.</summary>
    public static class GiaPhaFactsBuilder
    {
        public static string Build(GiaPhaIntent intent, string answerText, FamilyViewModel currentFamily)
        {
            var sb = new StringBuilder(512);
            sb.AppendLine("=== INTENT ===");
            sb.AppendLine("kind: " + (intent != null ? intent.Kind : "?"));
            if (intent != null && intent.Names != null && intent.Names.Count > 0)
            {
                sb.AppendLine("names: " + string.Join(" | ", intent.Names));
            }

            if (intent != null && intent.Generation.HasValue)
            {
                sb.AppendLine("generation: " + intent.Generation.Value);
            }

            if (currentFamily?.familyInfo != null)
            {
                sb.AppendLine("selected_family_id: " + currentFamily.familyInfo.FamilyId);
            }

            sb.AppendLine();
            sb.AppendLine("=== FACTS (engine) ===");
            sb.AppendLine(answerText ?? "");
            return sb.ToString();
        }
    }
}
