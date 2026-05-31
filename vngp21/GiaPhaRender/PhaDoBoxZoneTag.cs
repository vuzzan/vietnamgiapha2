namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Vùng trang trí SVG quanh ô gia đình: trên / dưới / trái / phải.</summary>
    public enum PhaDoBoxZone { Top, Bottom, Left, Right }

    /// <summary>Tag trên UIElement visual zone SVG quanh ô gia đình — để xóa/vẽ lại.</summary>
    public sealed class PhaDoBoxZoneTag
    {
        public int FamilyId { get; }
        public PhaDoBoxZone Zone { get; }
        public PhaDoBoxZoneTag(int familyId, PhaDoBoxZone zone) { FamilyId = familyId; Zone = zone; }
    }
}
