namespace vietnamgiapha.GiaPhaRender
{
    public enum PhaDoResizeCorner
    {
        TopLeft = 0,
        TopRight = 1,
        BottomLeft = 2,
        BottomRight = 3
    }

    /// <summary>Tag trên ô vuông góc selection — kéo để đổi width/height ô.</summary>
    public sealed class PhaDoResizeHandleTag
    {
        public int FamilyId { get; }
        public PhaDoResizeCorner Corner { get; }

        public PhaDoResizeHandleTag(int familyId, PhaDoResizeCorner corner)
        {
            FamilyId = familyId;
            Corner = corner;
        }
    }
}
