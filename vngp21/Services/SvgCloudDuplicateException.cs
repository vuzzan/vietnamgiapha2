using System;

namespace vietnamgiapha
{
    /// <summary>SVG trùng nội dung trên kho cloud (API code = 2).</summary>
    public sealed class SvgCloudDuplicateException : Exception
    {
        public const int ApiCode = 2;

        public SvgCloudItem Existing { get; }

        /// <summary>Thông báo hiển thị cho người dùng (nhiều dòng).</summary>
        public string UserMessage { get; }

        public SvgCloudDuplicateException(string userMessage, SvgCloudItem existing)
            : base(userMessage)
        {
            UserMessage = userMessage ?? "";
            Existing = existing;
        }
    }
}
