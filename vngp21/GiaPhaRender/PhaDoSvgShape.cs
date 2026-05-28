using System;
using System.Globalization;
using vietnamgiapha;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Một khung SVG trong catalog gia phả — markup lưu Base64 trong file .json.</summary>
    public sealed class PhaDoSvgShape
    {
        public string SvgId { get; set; }
        public string SvgBase64 { get; set; }
        public double ViewBoxWidth { get; set; } = 100;
        public double ViewBoxHeight { get; set; } = 80;

        public string GetSvgMarkup()
        {
            if (string.IsNullOrWhiteSpace(SvgBase64))
            {
                return null;
            }

            try
            {
                return Util.Base64Decode(SvgBase64);
            }
            catch
            {
                return null;
            }
        }

        public static PhaDoSvgShape FromMarkup(string svgId, string sanitizedSvgMarkup, double viewBoxWidth, double viewBoxHeight)
        {
            return new PhaDoSvgShape
            {
                SvgId = svgId,
                SvgBase64 = string.IsNullOrWhiteSpace(sanitizedSvgMarkup)
                    ? null
                    : Util.Base64Encode(sanitizedSvgMarkup),
                ViewBoxWidth = viewBoxWidth,
                ViewBoxHeight = viewBoxHeight
            };
        }
    }
}
