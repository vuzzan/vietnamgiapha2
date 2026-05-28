using System.Collections.Generic;
using vietnamgiapha.GiaPhaRender;

namespace vietnamgiapha
{
    /// <summary>Trạng thái làm việc — khôi phục sau khi tắt/mở lại app.</summary>
    public sealed class AppWorkspaceSession
    {
        public string Version { get; set; } = "1";

        /// <summary>Đường dẫn file gia phả .json đang mở.</summary>
        public string DataFilePath { get; set; }

        public int SelectedTabIndex { get; set; }

        public PhaDoWorkspaceState PhaDo { get; set; }
    }

    public sealed class PhaDoWorkspaceState
    {
        /// <summary>0=Ngang, 1=Dọc từng ký tự, 2=Dọc theo từ (Word).</summary>
        public int CardLayoutIndex { get; set; }

        /// <summary>Phiên bản cũ — migrate sang CardLayoutIndex.</summary>
        public bool VerticalCards { get; set; }

        /// <summary>Phiên bản cũ — migrate sang CardLayoutIndex.</summary>
        public bool VerticalWordStyle { get; set; }

        public double Zoom { get; set; } = 1.0;

        public int SelectedFamilyId { get; set; }

        public bool IsRendered { get; set; }

        public double ScrollHorizontal { get; set; }

        public double ScrollVertical { get; set; }

        public Dictionary<int, double> OffsetXmmByFamilyId { get; set; } = new Dictionary<int, double>();

        public Dictionary<int, double> OffsetYmmByFamilyId { get; set; } = new Dictionary<int, double>();

        /// <summary>Phiên bản cũ — chỉ cỡ chữ chung; migrate sang BoxStyleByFamilyId.</summary>
        public Dictionary<int, double> FontPtByFamilyId { get; set; } = new Dictionary<int, double>();

        /// <summary>Phiên bản cũ — màu nền; migrate sang BoxStyleByFamilyId.</summary>
        public Dictionary<int, string> FillColorHexByFamilyId { get; set; } = new Dictionary<int, string>();

        /// <summary>Kiểu ô đầy đủ (nền + chữ chính + chữ phụ).</summary>
        public Dictionary<int, PhaDoBoxStyle> BoxStyleByFamilyId { get; set; }
            = new Dictionary<int, PhaDoBoxStyle>();

        /// <summary>Kiểu khối tiêu đề 2 dòng trên cùng.</summary>
        public PhaDoTitleStyle TitleStyle { get; set; }
    }
}
