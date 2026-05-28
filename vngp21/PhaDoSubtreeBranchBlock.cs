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

        public double WidthCm => Math.Max(0, MaxXmm - MinXmm) / 10.0;
        public double HeightCm => Math.Max(0, MaxYmm - MinYmm) / 10.0;

        public string DisplayTitle =>
            (Generation > 0 ? "Đời " + Generation + " · " : "") + (MainPersonName ?? "Gia đình");

        public string GenerationText => Generation > 0 ? "Đời " + Generation : "Đời ?";

        public string SizeText =>
            WidthCm.ToString("0.#") + " × " + HeightCm.ToString("0.#") + " cm";
    }
}
