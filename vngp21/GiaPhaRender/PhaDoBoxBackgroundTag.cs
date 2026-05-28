using vietnamgiapha;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Tag gắn lên nền ô (rect mặc định hoặc Viewbox SVG) — tách khỏi chữ.</summary>
    public sealed class PhaDoBoxBackgroundTag
    {
        public FamilyViewModel Family { get; }

        public PhaDoBoxBackgroundTag(FamilyViewModel family)
        {
            Family = family;
        }
    }
}
