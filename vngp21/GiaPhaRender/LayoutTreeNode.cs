using System.Collections.Generic;
using vietnamgiapha;

namespace vietnamgiapha.GiaPhaRender
{
    internal sealed class LayoutTreeNode
    {
        public FamilyViewModel Family { get; }
        public FamilyCardMetrics Metrics { get; }
        public List<LayoutTreeNode> Children { get; } = new List<LayoutTreeNode>();

        public double Xmm { get; set; }
        public double Ymm { get; set; }
        public double WidthMm => Metrics.WidthMm;
        public double HeightMm => Metrics.HeightMm;
        public double SlotHeightMm => Metrics.SlotHeightMm;
        public int Generation => Metrics.Generation;
        public int Level { get; set; }

        public LayoutTreeNode(FamilyViewModel family, FamilyCardMetrics metrics)
        {
            Family = family;
            Metrics = metrics;
        }
    }
}
