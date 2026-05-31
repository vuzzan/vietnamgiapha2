using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Serialization;
using vietnamgiapha.GiaPhaRender;

namespace vietnamgiapha
{
    /// <summary>Một khung SVG trên cloud (danh sách hoặc chi tiết).</summary>
    public sealed class SvgCloudItem
    {
        public int Id { get; set; }

        [JsonPropertyName("svg_category")]
        public string Category { get; set; }

        [JsonPropertyName("svg_name")]
        public string Name { get; set; }

        [JsonPropertyName("svg_author")]
        public string Author { get; set; }

        [JsonPropertyName("count_download")]
        public int CountDownload { get; set; }

        [JsonPropertyName("update_date")]
        public string UpdateDate { get; set; }

        [JsonPropertyName("svg_data")]
        public string SvgData { get; set; }

        public string TreeLabel =>
            Name + " — " + (string.IsNullOrWhiteSpace(Author) ? "?" : Author)
            + " (" + CountDownload + " lượt tải)";
    }

    /// <summary>Nhóm category trên TreeView.</summary>
    public sealed class SvgCategoryGroup
    {
        public string Category { get; set; }
        public ObservableCollection<SvgCloudItem> Items { get; set; } = new ObservableCollection<SvgCloudItem>();

        public string DisplayName =>
            (Category ?? "Chung") + " (" + Items.Count + ")";
    }

    /// <summary>Node TreeView — category hoặc item.</summary>
    public sealed class SvgTreeNode
    {
        public bool IsCategory { get; set; }
        public SvgCategoryGroup Group { get; set; }
        public SvgCloudItem Item { get; set; }

        public string DisplayText
        {
            get
            {
                if (IsCategory)
                {
                    return Group?.DisplayName ?? "";
                }

                return Item?.TreeLabel ?? "";
            }
        }

        public ObservableCollection<SvgTreeNode> Children { get; set; } = new ObservableCollection<SvgTreeNode>();
    }

    /// <summary>Một khung trong catalog local (file gia phả).</summary>
    public sealed class SvgLocalListItem
    {
        public string SvgId { get; set; }
        public string FilePath { get; set; }
        public PhaDoSvgShape Shape { get; set; }

        public string DisplayText
        {
            get
            {
                if (Shape == null)
                {
                    return SvgId ?? "";
                }

                return SvgId + " ("
                    + Shape.ViewBoxWidth.ToString("0.##", CultureInfo.InvariantCulture)
                    + "×"
                    + Shape.ViewBoxHeight.ToString("0.##", CultureInfo.InvariantCulture)
                    + ")";
            }
        }
    }
}
