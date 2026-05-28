using System.Collections.Generic;
using vietnamgiapha;

namespace vietnamgiapha.GiaPhaRender
{
    public enum GiaPhaConnectorKind
    {
        Trunk,
        Bus,
        Branch
    }

    public enum GiaPhaCanvasConnectorLineKind
    {
        Trunk,
        Bus,
        Branch
    }

    public sealed class GiaPhaCanvasConnectorTag
    {
        public int ParentFamilyId { get; set; }
        public int ChildFamilyId { get; set; }
        public GiaPhaCanvasConnectorLineKind LineKind { get; set; }
    }

    public sealed class GiaPhaPlacedNode
    {
        public FamilyViewModel Family { get; set; }
        public FamilyCardMetrics Metrics { get; set; }
        public double Xmm { get; set; }
        public double Ymm { get; set; }
        public int Level { get; set; }

        public double Xpx(double dpi) => PrintUnits.MmToPixels(Xmm, dpi);
        public double Ypx(double dpi) => PrintUnits.MmToPixels(Ymm, dpi);
        public double WidthPx(double dpi) => PrintUnits.MmToPixels(Metrics.WidthMm, dpi);
        public double HeightPx(double dpi) => PrintUnits.MmToPixels(Metrics.HeightMm, dpi);
    }

    public sealed class GiaPhaConnector
    {
        public GiaPhaConnectorKind Kind { get; set; }
        public double X1mm { get; set; }
        public double Y1mm { get; set; }
        public double X2mm { get; set; }
        public double Y2mm { get; set; }
    }

    public sealed class GiaPhaGenerationBand
    {
        public int Level { get; set; }
        public double Ymm { get; set; }
        public double HeightMm { get; set; }
    }

    public sealed class GiaPhaRenderResult
    {
        public GiaPhaRenderOptions Options { get; set; }
        public double Dpi { get; set; }
        public double Scale { get; set; }
        public double PageWidthMm { get; set; }
        public double PageHeightMm { get; set; }
        public double PageWidthPixels { get; set; }
        public double PageHeightPixels { get; set; }
        public double ContentWidthMm { get; set; }
        public double ContentHeightMm { get; set; }
        public double CanvasWidthPixels { get; set; }
        public double CanvasHeightPixels { get; set; }

        public string SizeSummary =>
            (int)PageWidthMm + "×" + (int)PageHeightMm + " mm ("
            + (int)CanvasWidthPixels + "×" + (int)CanvasHeightPixels + " px @ " + (int)Dpi + " dpi)";

        public List<GiaPhaPlacedNode> Nodes { get; } = new List<GiaPhaPlacedNode>();
        public List<GiaPhaConnector> Connectors { get; } = new List<GiaPhaConnector>();
        public List<GiaPhaGenerationBand> GenerationBands { get; } = new List<GiaPhaGenerationBand>();
    }
}
