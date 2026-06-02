using System;

namespace vietnamgiapha
{
    /// <summary>Một khối phả con trên sơ đồ — tên người chính + bounds layout (cm).</summary>
    public sealed class PhaDoSubtreeBranchBlock
    {
        public string FamilyName { get; set; }
        public string MainPersonName { get; set; }
        public string SpouseNamesText { get; set; }
        public int FamilyId { get; set; }
        public int Generation { get; set; }
        public int NodeCount { get; set; }
        public double MinXmm { get; set; }
        public double MinYmm { get; set; }
        public double MaxXmm { get; set; }
        public double MaxYmm { get; set; }

        /// <summary>True = nhánh STOP (≥ ngưỡng) → bắt đầu phả con riêng; false = non-STOP (nhỏ, vẽ tiếp).</summary>
        public bool IsStop { get; set; } = true;

        /// <summary>Số nhánh non-STOP trong ô tổng hợp (0 nếu đây là block đơn lẻ).</summary>
        public int NonStopGroupCount { get; set; }

        /// <summary>
        /// FamilyId của cha trực tiếp trong sơ đồ — chỉ dùng cho ô tổng hợp non-STOP
        /// để gắn đúng cha mà không cần tìm gần nhất theo X.
        /// </summary>
        public int SummaryParentId { get; set; }

        /// <summary>True khi đây là ô đại diện cho nhóm gộp nhiều nhánh non-STOP.</summary>
        public bool IsNonStopSummary => !IsStop && NonStopGroupCount > 0;

        public double WidthCm => Math.Max(0, MaxXmm - MinXmm) / 10.0;
        public double HeightCm => Math.Max(0, MaxYmm - MinYmm) / 10.0;

        public string DisplayTitle =>
            (Generation > 0 ? "Đời " + Generation + " · " : "") + (MainPersonName ?? "Gia đình");

        public string GenerationText => Generation > 0 ? "Đời " + Generation : "Đời ?";

        public string SizeText =>
            WidthCm.ToString("0.#") + " × " + HeightCm.ToString("0.#") + " cm";
    }
}
