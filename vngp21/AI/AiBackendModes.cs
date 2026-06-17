namespace vietnamgiapha.AI
{
    /// <summary>Chế độ backend AI — lưu dạng chuỗi trong ai-settings.json.</summary>
    public static class AiBackendModes
    {
        public const string RuleEngine = "RuleEngine";
        public const string LocalLlama = "LocalLlama";
        public const string CloudApi = "CloudApi";

        public static bool IsRuleEngine(string mode)
        {
            return string.IsNullOrEmpty(mode) || mode == RuleEngine;
        }

        public static bool IsLocalLlama(string mode)
        {
            return mode == LocalLlama;
        }

        public static bool IsCloudApi(string mode)
        {
            return mode == CloudApi;
        }
    }
}
