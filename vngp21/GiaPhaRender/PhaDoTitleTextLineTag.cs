namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>
    /// Tag trên TextBlock dòng chữ trong khối tiêu đề phả đồ.
    /// Cho phép click chọn từng dòng để chỉnh font/màu riêng.
    /// </summary>
    public sealed class PhaDoTitleTextLineTag
    {
        /// <summary>0=Tên, 1=OTAI, 2=Số gia đình·người, 3=Kích thước W×H.</summary>
        public int LineIndex { get; }
        public PhaDoTitleTextLineTag(int lineIndex) => LineIndex = lineIndex;
    }
}
