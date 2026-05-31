using System;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Offset + style chữ khi xuất SVG — cùng quy tắc với canvas WPF.</summary>
    internal static class PhaDoSvgExportHelper
    {
        public static PhaDoPersonLayoutOffset GetTitleLineOffset(PhaDoTitleStyle titleStyle, int lineIndex)
        {
            if (titleStyle?.LineOffsetsByIndex != null
                && titleStyle.LineOffsetsByIndex.TryGetValue(lineIndex, out var offset)
                && offset != null)
            {
                return offset;
            }

            return new PhaDoPersonLayoutOffset();
        }

        public static PhaDoPersonLayoutOffset GetPersonSlotOffset(PhaDoBoxStyle boxStyle, int slot)
        {
            if (boxStyle?.PersonOffsetsBySlot != null
                && boxStyle.PersonOffsetsBySlot.TryGetValue(slot, out var offset)
                && offset != null)
            {
                return offset;
            }

            return new PhaDoPersonLayoutOffset();
        }

        public static PhaDoPersonTextStyle ResolveSlotTextStyle(
            PhaDoBoxStyle boxStyle,
            GiaPhaRenderOptions options,
            int slot,
            PhaDoPersonTextRole role,
            PhaDoBoxElementKind elementKind)
        {
            if (boxStyle?.PersonTextStylesBySlot != null
                && boxStyle.PersonTextStylesBySlot.TryGetValue(slot, out var custom)
                && custom != null
                && !custom.IsEmpty())
            {
                return custom;
            }

            if (elementKind == PhaDoBoxElementKind.Person)
            {
                var roleStyle = role == PhaDoPersonTextRole.Main ? boxStyle?.Main : boxStyle?.Spouse;
                if (roleStyle != null && !roleStyle.IsEmpty())
                {
                    return roleStyle.Clone();
                }
            }

            return null;
        }

        public static double DefaultFontPt(
            GiaPhaRenderOptions options,
            PhaDoBoxElementKind elementKind,
            PhaDoPersonTextRole role)
        {
            if (options == null)
            {
                return 9;
            }

            switch (elementKind)
            {
                case PhaDoBoxElementKind.GenerationLabel:
                    return options.HeaderFontPt;
                case PhaDoBoxElementKind.ExtraNote:
                    return options.NoteFontPt > 0 ? options.NoteFontPt : 6.5;
                case PhaDoBoxElementKind.Person:
                    return role == PhaDoPersonTextRole.Main
                        ? options.MainNameFontPt
                        : options.SpouseFontPt;
                default:
                    return options.MainNameFontPt;
            }
        }

        public static string DefaultFillHex(PhaDoBoxElementKind elementKind)
        {
            switch (elementKind)
            {
                case PhaDoBoxElementKind.GenerationLabel:
                    return "#464646";
                case PhaDoBoxElementKind.ExtraNote:
                    return "#5A606C";
                default:
                    return "#000000";
            }
        }

        public static void ResolveDrawParams(
            PhaDoPersonTextStyle style,
            GiaPhaRenderOptions options,
            double defaultPt,
            PhaDoPersonTextRole role,
            PhaDoBoxElementKind elementKind,
            out double fontPt,
            out string fontFamily,
            out string fillHex,
            out bool bold)
        {
            fontPt = style?.FontPt ?? defaultPt;
            fontFamily = !string.IsNullOrWhiteSpace(style?.FontFamilyName)
                ? style.FontFamilyName
                : (options?.FontFamilyName ?? "Segoe UI");
            fillHex = !string.IsNullOrWhiteSpace(style?.ForegroundHex)
                ? style.ForegroundHex
                : DefaultFillHex(elementKind);
            bool boldDefault = role == PhaDoPersonTextRole.Main
                && elementKind == PhaDoBoxElementKind.Person;
            bold = style?.Bold ?? boldDefault;
        }
    }
}
