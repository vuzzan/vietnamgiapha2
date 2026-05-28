using System;
using System.Collections.Generic;
using System.Linq;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Vị trí lệch (mm) của một dòng/cột người trong ô — so với layout mặc định.</summary>
    public sealed class PhaDoPersonLayoutOffset
    {
        public double DeltaXmm { get; set; }
        public double DeltaYmm { get; set; }
    }

    /// <summary>Kích thước ô, lệch chữ trong ô, lệch vị trí ô trên phả đồ — copy từ ô mẫu.</summary>
    public sealed class PhaDoBoxLayoutSnapshot
    {
        public double? CustomWidthMm { get; set; }
        public double? CustomHeightMm { get; set; }
        public Dictionary<int, PhaDoPersonLayoutOffset> PersonOffsetsBySlot { get; set; }
            = new Dictionary<int, PhaDoPersonLayoutOffset>();
        public double? OffsetXmm { get; set; }
        public double? OffsetYmm { get; set; }

        public bool HasAny =>
            CustomWidthMm.HasValue
            || CustomHeightMm.HasValue
            || (PersonOffsetsBySlot != null && PersonOffsetsBySlot.Count > 0)
            || OffsetXmm.HasValue
            || OffsetYmm.HasValue;

        public static PhaDoBoxLayoutSnapshot FromBoxStyle(
            PhaDoBoxStyle style,
            double? offsetXmm,
            double? offsetYmm)
        {
            var snap = new PhaDoBoxLayoutSnapshot
            {
                CustomWidthMm = style?.CustomWidthMm,
                CustomHeightMm = style?.CustomHeightMm,
                OffsetXmm = offsetXmm,
                OffsetYmm = offsetYmm
            };

            if (style?.PersonOffsetsBySlot != null)
            {
                foreach (var kv in style.PersonOffsetsBySlot)
                {
                    if (kv.Value == null)
                    {
                        continue;
                    }

                    snap.PersonOffsetsBySlot[kv.Key] = new PhaDoPersonLayoutOffset
                    {
                        DeltaXmm = kv.Value.DeltaXmm,
                        DeltaYmm = kv.Value.DeltaYmm
                    };
                }
            }

            return snap;
        }
    }

    /// <summary>Phạm vi áp dụng kiểu từ dialog chỉnh ô.</summary>
    public enum PhaDoStyleApplyScope
    {
        SingleBox = 0,
        AllBoxesInLevel = 1
    }

    /// <summary>Phân biệt chữ người chính / người phụ trên canvas.</summary>
    public enum PhaDoPersonTextRole
    {
        Main = 0,
        Spouse = 1
    }

    /// <summary>Loại phần tử có thể chọn trong ô.</summary>
    public enum PhaDoBoxElementKind
    {
        /// <summary>Chữ "Đời N" — ngang + dọc từng ký tự.</summary>
        GenerationLabel = 0,
        /// <summary>Người chính hoặc người phụ (một dòng / một cột).</summary>
        Person = 1
    }

    /// <summary>Tag WPF gắn lên TextBlock/StackPanel cột — giữ Family + vai trò chữ.</summary>
    public sealed class PhaDoBoxVisualTag
    {
        /// <summary>Text đời — chỉ layout ngang / dọc từng chữ.</summary>
        public const int FamilyLabelSlotIndex = -1;

        /// <summary>0 = người chính, 1..n = người phụ.</summary>
        public const int MainPersonSlotIndex = 0;

        public FamilyViewModel Family { get; }
        public PhaDoPersonTextRole Role { get; }
        public PhaDoBoxElementKind ElementKind { get; }
        public int PersonSlotIndex { get; }

        public bool IsSelectable =>
            ElementKind == PhaDoBoxElementKind.GenerationLabel
                ? PersonSlotIndex == FamilyLabelSlotIndex
                : PersonSlotIndex >= MainPersonSlotIndex;

        /// <summary>Alias — phần tử được chọn / kéo trong ô.</summary>
        public bool IsMovablePerson => IsSelectable;

        public PhaDoBoxVisualTag(
            FamilyViewModel family,
            PhaDoPersonTextRole role,
            PhaDoBoxElementKind elementKind,
            int personSlotIndex)
        {
            Family = family;
            Role = role;
            ElementKind = elementKind;
            PersonSlotIndex = personSlotIndex;
        }
    }

    /// <summary>Font / cỡ / màu chữ một nhóm (chính hoặc phụ).</summary>
    public sealed class PhaDoPersonTextStyle
    {
        public string FontFamilyName { get; set; }
        public double? FontPt { get; set; }
        public string ForegroundHex { get; set; }

        public PhaDoPersonTextStyle Clone()
        {
            return new PhaDoPersonTextStyle
            {
                FontFamilyName = FontFamilyName,
                FontPt = FontPt,
                ForegroundHex = ForegroundHex
            };
        }

        public bool IsEmpty()
        {
            return string.IsNullOrWhiteSpace(FontFamilyName)
                && !FontPt.HasValue
                && string.IsNullOrWhiteSpace(ForegroundHex);
        }
    }

    /// <summary>Kiểu tùy chỉnh cả ô: nền + chữ người chính + chữ người phụ.</summary>
    public sealed class PhaDoBoxStyle : IPhaDoSvgFrameStyle
    {
        public string FillColorHex { get; set; }
        public PhaDoPersonTextStyle Main { get; set; } = new PhaDoPersonTextStyle();
        public PhaDoPersonTextStyle Spouse { get; set; } = new PhaDoPersonTextStyle();

        /// <summary>Tham chiếu catalog trong file gia phả.</summary>
        public string ShapeSvgId { get; set; }

        /// <summary>SVG đã sanitize — runtime / dialog; file gia phả lưu qua ShapeSvgId + catalog.</summary>
        public string CustomShapeSvg { get; set; }
        public double CustomShapeViewBoxWidth { get; set; } = 100;
        public double CustomShapeViewBoxHeight { get; set; } = 80;

        /// <summary>Kích thước ô tùy chỉnh (mm) — kéo góc selection.</summary>
        public double? CustomWidthMm { get; set; }
        public double? CustomHeightMm { get; set; }

        /// <summary>Lệch vị trí từng người trong ô (slot → mm).</summary>
        public Dictionary<int, PhaDoPersonLayoutOffset> PersonOffsetsBySlot { get; set; }
            = new Dictionary<int, PhaDoPersonLayoutOffset>();

        public PhaDoBoxStyle Clone()
        {
            return new PhaDoBoxStyle
            {
                FillColorHex = FillColorHex,
                Main = Main?.Clone() ?? new PhaDoPersonTextStyle(),
                Spouse = Spouse?.Clone() ?? new PhaDoPersonTextStyle(),
                ShapeSvgId = ShapeSvgId,
                CustomShapeSvg = CustomShapeSvg,
                CustomShapeViewBoxWidth = CustomShapeViewBoxWidth,
                CustomShapeViewBoxHeight = CustomShapeViewBoxHeight,
                CustomWidthMm = CustomWidthMm,
                CustomHeightMm = CustomHeightMm,
                PersonOffsetsBySlot = PersonOffsetsBySlot?.ToDictionary(
                    kv => kv.Key,
                    kv => new PhaDoPersonLayoutOffset
                    {
                        DeltaXmm = kv.Value?.DeltaXmm ?? 0,
                        DeltaYmm = kv.Value?.DeltaYmm ?? 0
                    }) ?? new Dictionary<int, PhaDoPersonLayoutOffset>()
            };
        }

        /// <summary>Bản sao lưu session — không nhúng markup SVG nếu đã có ShapeSvgId.</summary>
        public PhaDoBoxStyle CloneForSession()
        {
            var clone = Clone();
            if (!string.IsNullOrWhiteSpace(clone.ShapeSvgId))
            {
                clone.CustomShapeSvg = null;
            }

            return clone;
        }

        public bool HasCustomShape =>
            !string.IsNullOrWhiteSpace(CustomShapeSvg) || !string.IsNullOrWhiteSpace(ShapeSvgId);

        public bool HasAnyOverride()
        {
            return HasCustomShape
                || CustomWidthMm.HasValue
                || CustomHeightMm.HasValue
                || (PersonOffsetsBySlot != null && PersonOffsetsBySlot.Count > 0)
                || !string.IsNullOrWhiteSpace(FillColorHex)
                || (Main != null && !Main.IsEmpty())
                || (Spouse != null && !Spouse.IsEmpty());
        }
    }
}
