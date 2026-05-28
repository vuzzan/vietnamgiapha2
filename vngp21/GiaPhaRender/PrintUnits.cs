using System;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Đơn vị mm và chuyển đổi pixel theo DPI (in A0).</summary>
    public static class PrintUnits
    {
        public const double MmPerInch = 25.4;

        public static double MmToPixels(double mm, double dpi)
        {
            return mm / MmPerInch * dpi;
        }

        public static double PixelsToMm(double pixels, double dpi)
        {
            return pixels * MmPerInch / dpi;
        }
    }
}
