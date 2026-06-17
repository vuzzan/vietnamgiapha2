namespace vietnamgiapha.AI
{
    /// <summary>Link tải llama-server và model Qwen GGUF — mở từ Cài đặt AI.</summary>
    public static class LocalLlamaDownloadLinks
    {
        public const string LlamaServerReleasesUrl =
            "https://github.com/ggml-org/llama.cpp/releases";

        /// <summary>Kho Qwen3-4B GGUF (gợi ý ~2.5 GB, Q4_K_M).</summary>
        public const string Qwen3_4bGgufUrl =
            "https://huggingface.co/Qwen/Qwen3-4B-GGUF";

        /// <summary>Danh sách repo Qwen có GGUF trên Hugging Face.</summary>
        public const string QwenGgufSearchUrl =
            "https://huggingface.co/models?search=qwen3+gguf";
    }
}
