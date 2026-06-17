using System;
using System.Collections.Generic;

namespace vietnamgiapha.AI
{
    /// <summary>Ảnh chụp rules Qwen đã tải từ ai/intent/*.txt.</summary>
    public sealed class GiaPhaIntentRulesSnapshot
    {
        public string SystemPrompt { get; set; }
        public string UserPromptHeader { get; set; }
        public string[] BlockedKeywords { get; set; }
        public DateTime LoadedAt { get; set; }
        public string IntentDirectory { get; set; }
        public List<GiaPhaIntentRuleFileInfo> Files { get; set; } = new List<GiaPhaIntentRuleFileInfo>();
    }

    public sealed class GiaPhaIntentRuleFileInfo
    {
        public string FileName { get; set; }
        public bool Exists { get; set; }
        public int LineCount { get; set; }
        public int CharCount { get; set; }
        public DateTime? LastWriteUtc { get; set; }
    }
}
