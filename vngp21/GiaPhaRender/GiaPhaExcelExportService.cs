using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using vietnamgiapha;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Xuất phả đồ Excel: ô text + đường nối cha–con, căn theo layout mm (nén vừa khổ Excel).</summary>
    public static class GiaPhaExcelExportService
    {
        private const int LinesPerBox = 3;
        private const int MinColSpan = 2;
        private const int TitleRowCount = 3;
        private const int ExcelMaxColumn = 16384;
        private const int ExcelMaxRow = 1048576;
        /// <summary>Chiều rộng lưới mục tiêu — giữ file gọn, tránh vượt 16384 cột.</summary>
        private const int TargetGridColumns = 360;

        private sealed class ExcelGridMapper
        {
            public double MinXmm { get; private set; }
            public double MinYmm { get; private set; }
            public double ColUnitMm { get; private set; }
            public double RowUnitMm { get; private set; }

            public static ExcelGridMapper FromResult(GiaPhaRenderResult result)
            {
                double minX = double.MaxValue;
                double maxX = double.MinValue;
                double minY = double.MaxValue;
                double maxY = double.MinValue;

                foreach (var n in result.Nodes)
                {
                    double w = n.Metrics?.WidthMm ?? 42;
                    double h = n.Metrics?.HeightMm ?? 20;
                    minX = Math.Min(minX, n.Xmm);
                    maxX = Math.Max(maxX, n.Xmm + w);
                    minY = Math.Min(minY, n.Ymm);
                    maxY = Math.Max(maxY, n.Ymm + h);
                }

                foreach (var seg in result.Connectors)
                {
                    minX = Math.Min(minX, Math.Min(seg.X1mm, seg.X2mm));
                    maxX = Math.Max(maxX, Math.Max(seg.X1mm, seg.X2mm));
                    minY = Math.Min(minY, Math.Min(seg.Y1mm, seg.Y2mm));
                    maxY = Math.Max(maxY, Math.Max(seg.Y1mm, seg.Y2mm));
                }

                if (minX == double.MaxValue)
                {
                    minX = 0;
                    maxX = 100;
                    minY = 0;
                    maxY = 100;
                }

                double widthMm = Math.Max(maxX - minX, 1);
                double rowUnit = result.Options != null
                    ? Math.Max(4.0, result.Options.GenerationGapMm / 5.0)
                    : 5.0;

                double colUnit = Math.Max(4.0, widthMm / TargetGridColumns);

                return new ExcelGridMapper
                {
                    MinXmm = minX,
                    MinYmm = minY,
                    ColUnitMm = colUnit,
                    RowUnitMm = rowUnit
                };
            }

            public int ToCol(double xmm)
            {
                int col = 2 + (int)Math.Round((xmm - MinXmm) / ColUnitMm);
                return Clamp(col, 1, ExcelMaxColumn);
            }

            public int ToRow(double ymm)
            {
                int row = TitleRowCount + 2 + (int)Math.Round((ymm - MinYmm) / RowUnitMm);
                return Clamp(row, 1, ExcelMaxRow);
            }

            public int ColSpanFromWidth(double widthMm)
            {
                int span = Math.Max(MinColSpan, (int)Math.Ceiling(widthMm / ColUnitMm));
                return Math.Min(span, 40);
            }
        }

        private sealed class NodePlacement
        {
            public GiaPhaPlacedNode Node { get; set; }
            public int StartRow { get; set; }
            public int StartCol { get; set; }
            public int ColSpan { get; set; }
            public int CenterCol => StartCol + ColSpan / 2;
            public int BottomRow => StartRow + LinesPerBox - 1;
        }

        public static void ExportText(
            string filePath,
            GiaPhaRenderOptions options,
            GiaPhaRenderResult result)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("filePath required", nameof(filePath));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var mapper = ExcelGridMapper.FromResult(result);
            var placements = BuildPlacements(result, mapper);
            var occupied = BuildOccupiedSet(placements);

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Phả đồ");
                ws.Style.Font.FontName = "Segoe UI";
                ws.Style.Font.FontSize = 9;

                WriteTitle(ws, options);
                DrawConnectors(ws, result, mapper, occupied);

                foreach (var p in placements)
                {
                    WriteFamilyBox(ws, p);
                }

                ApplySheetSizing(ws, placements);
                workbook.SaveAs(filePath);
            }
        }

        private static void WriteTitle(IXLWorksheet ws, GiaPhaRenderOptions options)
        {
            string line1 = options?.Title?.Trim();
            if (string.IsNullOrWhiteSpace(line1))
            {
                line1 = "Phả đồ gia phả";
            }

            ws.Cell(1, 1).Value = line1;
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = (int)Math.Round(options?.TitleFontPt ?? 14);

            string line2 = options?.TitleLine2?.Trim();
            if (!string.IsNullOrWhiteSpace(line2))
            {
                ws.Cell(2, 1).Value = line2;
                ws.Cell(2, 1).Style.Font.FontSize = (int)Math.Round(options?.TitleLine2FontPt ?? 12);
            }

            ws.Cell(3, 1).Value = "Xuất lúc: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        }

        private static List<NodePlacement> BuildPlacements(GiaPhaRenderResult result, ExcelGridMapper mapper)
        {
            var occupied = new HashSet<(int Row, int Col)>();
            var list = new List<NodePlacement>();

            var byRow = result.Nodes
                .GroupBy(n => mapper.ToRow(n.Ymm))
                .OrderBy(g => g.Key);

            foreach (var rowGroup in byRow)
            {
                foreach (var node in rowGroup.OrderBy(n => n.Xmm))
                {
                    double widthMm = node.Metrics?.WidthMm ?? 42;
                    int colSpan = mapper.ColSpanFromWidth(widthMm);
                    double centerXmm = node.Xmm + widthMm / 2.0;
                    int centerCol = mapper.ToCol(centerXmm);
                    int startCol = Math.Max(1, centerCol - colSpan / 2);
                    int startRow = rowGroup.Key;

                    startCol = ResolveStartColumn(occupied, startRow, startCol, colSpan);

                    MarkBlock(occupied, startRow, startCol, colSpan, LinesPerBox);

                    list.Add(new NodePlacement
                    {
                        Node = node,
                        StartRow = startRow,
                        StartCol = startCol,
                        ColSpan = colSpan
                    });
                }
            }

            return list;
        }

        private static int ResolveStartColumn(
            HashSet<(int Row, int Col)> occupied,
            int startRow,
            int startCol,
            int colSpan)
        {
            int maxStart = ExcelMaxColumn - colSpan;
            if (maxStart < 1)
            {
                maxStart = 1;
            }

            startCol = Clamp(startCol, 1, maxStart);

            const int maxAttempts = 2000;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (!IsBlockOccupied(occupied, startRow, startCol, colSpan, LinesPerBox))
                {
                    return startCol;
                }

                startCol++;
                if (startCol > maxStart)
                {
                    startCol = 1;
                }
            }

            return FindFreeColumn(occupied, startRow, colSpan);
        }

        private static int FindFreeColumn(
            HashSet<(int Row, int Col)> occupied,
            int startRow,
            int colSpan)
        {
            int maxStart = Math.Max(1, ExcelMaxColumn - colSpan);
            for (int c = 1; c <= maxStart; c++)
            {
                if (!IsBlockOccupied(occupied, startRow, c, colSpan, LinesPerBox))
                {
                    return c;
                }
            }
            return 1;
        }

        private static HashSet<(int Row, int Col)> BuildOccupiedSet(List<NodePlacement> placements)
        {
            var set = new HashSet<(int Row, int Col)>();
            foreach (var p in placements)
            {
                MarkBlock(set, p.StartRow, p.StartCol, p.ColSpan, LinesPerBox);
            }
            return set;
        }

        private static bool IsBlockOccupied(
            HashSet<(int Row, int Col)> occupied,
            int startRow,
            int startCol,
            int colSpan,
            int rowSpan)
        {
            for (int r = startRow; r < startRow + rowSpan; r++)
            {
                if (r < 1 || r > ExcelMaxRow)
                {
                    return true;
                }

                for (int c = startCol; c < startCol + colSpan; c++)
                {
                    if (c < 1 || c > ExcelMaxColumn)
                    {
                        return true;
                    }

                    if (occupied.Contains((r, c)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static void MarkBlock(
            HashSet<(int Row, int Col)> occupied,
            int startRow,
            int startCol,
            int colSpan,
            int rowSpan)
        {
            for (int r = startRow; r < startRow + rowSpan; r++)
            {
                for (int c = startCol; c < startCol + colSpan; c++)
                {
                    if (r >= 1 && r <= ExcelMaxRow && c >= 1 && c <= ExcelMaxColumn)
                    {
                        occupied.Add((r, c));
                    }
                }
            }
        }

        private static void DrawConnectors(
            IXLWorksheet ws,
            GiaPhaRenderResult result,
            ExcelGridMapper mapper,
            HashSet<(int Row, int Col)> occupied)
        {
            foreach (var seg in result.Connectors)
            {
                int c1 = mapper.ToCol(seg.X1mm);
                int r1 = mapper.ToRow(seg.Y1mm);
                int c2 = mapper.ToCol(seg.X2mm);
                int r2 = mapper.ToRow(seg.Y2mm);

                var border = seg.Kind == GiaPhaConnectorKind.Bus
                    ? XLBorderStyleValues.Medium
                    : XLBorderStyleValues.Thin;

                if (Math.Abs(seg.X1mm - seg.X2mm) < 0.05)
                {
                    DrawVertical(ws, c1, r1, r2, border, occupied);
                }
                else if (Math.Abs(seg.Y1mm - seg.Y2mm) < 0.05)
                {
                    DrawHorizontal(ws, r1, c1, c2, border, occupied);
                }
            }
        }

        private static void DrawVertical(
            IXLWorksheet ws,
            int col,
            int row1,
            int row2,
            XLBorderStyleValues style,
            HashSet<(int Row, int Col)> occupied)
        {
            col = Clamp(col, 1, ExcelMaxColumn);
            int rMin = Clamp(Math.Min(row1, row2), 1, ExcelMaxRow);
            int rMax = Clamp(Math.Max(row1, row2), 1, ExcelMaxRow);

            for (int r = rMin; r < rMax; r++)
            {
                if (occupied.Contains((r, col)) && occupied.Contains((r + 1, col)))
                {
                    continue;
                }

                var cell = ws.Cell(r, col);
                cell.Style.Border.BottomBorder = style;
                cell.Style.Border.BottomBorderColor = XLColor.Black;
                cell.Style.Border.LeftBorder = style;
                cell.Style.Border.LeftBorderColor = XLColor.Black;
            }
        }

        private static void DrawHorizontal(
            IXLWorksheet ws,
            int row,
            int col1,
            int col2,
            XLBorderStyleValues style,
            HashSet<(int Row, int Col)> occupied)
        {
            row = Clamp(row, 1, ExcelMaxRow);
            int cMin = Clamp(Math.Min(col1, col2), 1, ExcelMaxColumn);
            int cMax = Clamp(Math.Max(col1, col2), 1, ExcelMaxColumn);

            for (int c = cMin; c < cMax; c++)
            {
                if (occupied.Contains((row, c)) && occupied.Contains((row, c + 1)))
                {
                    continue;
                }

                var cell = ws.Cell(row, c);
                cell.Style.Border.RightBorder = style;
                cell.Style.Border.RightBorderColor = XLColor.Black;
                cell.Style.Border.BottomBorder = style;
                cell.Style.Border.BottomBorderColor = XLColor.Black;
            }
        }

        private static void WriteFamilyBox(IXLWorksheet ws, NodePlacement placement)
        {
            var node = placement.Node;
            var lines = BuildCardLines(node);
            int startCol = Clamp(placement.StartCol, 1, ExcelMaxColumn);
            int endCol = Clamp(placement.StartCol + placement.ColSpan - 1, 1, ExcelMaxColumn);
            if (endCol < startCol)
            {
                endCol = startCol;
            }

            int fillHue = (node.Family?.familyInfo?.FamilyId ?? 0) % 6;
            var fill = BranchFill(fillHue);

            for (int i = 0; i < LinesPerBox; i++)
            {
                int row = Clamp(placement.StartRow + i, 1, ExcelMaxRow);
                var range = ws.Range(row, startCol, row, endCol);
                range.Merge();
                range.Style.Alignment.WrapText = true;
                range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                range.Style.Fill.BackgroundColor = fill;
                range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                if (i < lines.Count && !string.IsNullOrEmpty(lines[i]))
                {
                    range.Value = lines[i];
                    if (i == 0)
                    {
                        range.Style.Font.Bold = true;
                    }
                }
            }

            ws.Range(
                    Clamp(placement.StartRow, 1, ExcelMaxRow),
                    startCol,
                    Clamp(placement.BottomRow, 1, ExcelMaxRow),
                    endCol)
                .Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        }

        private static void ApplySheetSizing(IXLWorksheet ws, List<NodePlacement> placements)
        {
            if (placements.Count == 0)
            {
                return;
            }

            int maxCol = Math.Min(ExcelMaxColumn, placements.Max(p => p.StartCol + p.ColSpan - 1) + 1);
            int maxRow = Math.Min(ExcelMaxRow, placements.Max(p => p.BottomRow) + 2);

            for (int c = 1; c <= maxCol; c++)
            {
                ws.Column(c).Width = 3.2;
            }

            for (int r = TitleRowCount + 1; r <= maxRow; r++)
            {
                ws.Row(r).Height = 14;
            }

            ws.SheetView.FreezeRows(TitleRowCount);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static List<string> BuildCardLines(GiaPhaPlacedNode node)
        {
            var metrics = node.Metrics;
            string line1;
            if (metrics?.MainPerson != null)
            {
                line1 = FormatMain(metrics.MainPerson);
            }
            else if (node.Family != null)
            {
                line1 = node.Family.Name0 ?? node.Family.Name ?? "Gia đình";
            }
            else
            {
                line1 = "Gia đình";
            }

            string line2 = "";
            if (metrics?.Spouses != null && metrics.Spouses.Count > 0)
            {
                var spouseLine = new StringBuilder();
                for (int i = 0; i < metrics.Spouses.Count; i++)
                {
                    if (i > 0)
                    {
                        spouseLine.Append("  ");
                    }
                    spouseLine.Append(FormatSpouse(metrics.Spouses[i]));
                }
                line2 = spouseLine.ToString();
            }

            string line3 = "";
            if (metrics?.SpouseOverflow != null)
            {
                line3 = metrics.SpouseOverflow.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "";
            }

            return new List<string> { line1, line2, line3 };
        }

        private static string FormatMain(PersonInfo p)
        {
            return "★ " + (p.MANS_NAME_HUY ?? "").Trim();
        }

        private static string FormatSpouse(PersonInfo p)
        {
            string g = p.MANS_GENDER == "Nữ" ? "♀" : "♂";
            return g + " " + (p.MANS_NAME_HUY ?? "").Trim();
        }

        private static XLColor BranchFill(int branchIndex)
        {
            var colors = new[]
            {
                XLColor.FromHtml("#FFF3E0"),
                XLColor.FromHtml("#E8F5E9"),
                XLColor.FromHtml("#E3F2FD"),
                XLColor.FromHtml("#FCE4EC"),
                XLColor.FromHtml("#EDE7F6"),
                XLColor.FromHtml("#FFF9C4")
            };
            return colors[branchIndex % colors.Length];
        }
    }
}
