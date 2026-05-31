namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Tag trên ô vuông góc resize khối tiêu đề phả đồ.</summary>
    public sealed class PhaDoTitleResizeHandleTag
    {
        public PhaDoResizeCorner Corner { get; }
        public PhaDoTitleResizeHandleTag(PhaDoResizeCorner corner) => Corner = corner;
    }

    /// <summary>Tag đặt trên background hit-test của khối tiêu đề để nhận click chọn.</summary>
    public sealed class PhaDoTitleHitTag { }
}
