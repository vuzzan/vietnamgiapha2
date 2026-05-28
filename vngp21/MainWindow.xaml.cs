using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Windows.Media;
using System.Windows;
using System;
using System.Linq;
using System.Threading.Tasks;
using log4net.Repository;
using System.IO;
using System.Reflection;
using AutoUpdaterDotNET;
using System.Configuration;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Xml.Linq;
using WpfDraw.Class;
using System.Collections.Generic;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MessagePack;
using MessagePack.Resolvers;
using System.Windows.Threading;
using vngp21.Draw;
using vietnamgiapha.GiaPhaRender;
using System.Net;
using GalaSoft.MvvmLight.Command;
using Path = System.IO.Path;
using System.Text;
using System.ComponentModel;
using System.Windows.Data;

namespace vietnamgiapha
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger("m0");
        private readonly MainWindowViewModel viewModel;

        private double _phaDoZoom = 1.0;
        private const double PhaDoZoomMin = 0.15;
        private const double PhaDoZoomMax = 5.0;
        private const double PhaDoZoomStep = 0.15;
        private const int PhaDoTabIndex = 6;
        /// <summary>Dưới ngưỡng này không tách phả con (một phả đủ nhỏ).</summary>
        private const int PhaDoMinFamilyCountToSplitPhaiCon = 100;
        /// <summary>Mục tiêu mềm: mỗi phả con quanh mức này (cho phép dao động).</summary>
        private const int PhaDoTargetFamilyCountPerSubtree = 300;
        private GiaPhaRenderResult _phaDoRenderedLayout;
        private GiaPhaRenderOptions _phaDoCurrentOptions;

        private bool _phaDoIsDragging;
        private bool _phaDoIsPanning;
        private bool _phaDoPanMoved;
        private MouseButton _phaDoPanMouseButton;
        private Point _phaDoPanStartPoint;
        private double _phaDoPanStartScrollH;
        private double _phaDoPanStartScrollV;
        private int _phaDoDraggingFamilyId;
        private int _phaDoSelectedFamilyId;
        private int _phaDoSelectedPersonSlot = -1;
        private bool _phaDoIsDraggingPerson;
        private int _phaDoDraggingPersonFamilyId;
        private int _phaDoDraggingPersonSlot;
        private FrameworkElement _phaDoDraggingPersonElement;
        private Point _phaDoPersonDragStartCanvas;
        private double _phaDoPersonNaturalLeftPx;
        private double _phaDoPersonNaturalTopPx;
        private double _phaDoPersonDragStartDeltaXmm;
        private double _phaDoPersonDragStartDeltaYmm;
        private Point _phaDoDragStartPoint;
        private double _phaDoDragStartNodeXmm;
        private bool _phaDoMouseMovedWhileDrag;
        /// <summary>Kiểu tùy chỉnh từng ô: nền + chữ người chính + chữ người phụ.</summary>
        private readonly Dictionary<int, PhaDoBoxStyle> _phaDoBoxStyleByFamilyId = new Dictionary<int, PhaDoBoxStyle>();
        private PhaDoTitleStyle _phaDoTitleStyle = new PhaDoTitleStyle();
        private readonly Dictionary<int, double> _phaDoOffsetXmmByFamilyId = new Dictionary<int, double>();
        private readonly Dictionary<int, double> _phaDoOffsetYmmByFamilyId = new Dictionary<int, double>();
        private readonly Dictionary<int, double> _phaDoBaseXmmByFamilyId = new Dictionary<int, double>();
        private readonly Dictionary<int, double> _phaDoBaseYmmByFamilyId = new Dictionary<int, double>();

        private bool _phaDoIsResizing;
        private PhaDoResizeCorner _phaDoResizeCorner;
        private int _phaDoResizingFamilyId;
        private double _phaDoResizeStartXmm;
        private double _phaDoResizeStartYmm;
        private double _phaDoResizeStartWmm;
        private double _phaDoResizeStartHmm;
        private double _phaDoResizeMinWmm;
        private double _phaDoResizeMinHmm;

        private bool _phaDoImmersive;
        private bool _phaDoImmersivePaneWasOpen = true;
        private FrameworkElement _phaDoCachedTabStrip;
        private bool _isRestoringWorkspace;
        private bool _personGridIsRefreshing;
        private FamilyViewModel _personGridCacheRoot;
        private List<PersonGridRow> _personGridCachedRows;
        private readonly PersonGridRowCollection _personGridRows = new PersonGridRowCollection();
        private bool _personGridShowAllInFamily = true;
        private bool _personGridViewSortConfigured;
        private string _personSearchLastQuery;
        private int _personSearchLastIndex = -1;
        private int _personGridSelectedFamilyId;
        private FamilyViewModel _personGridSelectedFamilyRoot;
        private bool _personGridIsSelectingFamily;
        private static readonly SolidColorBrush PersonFamilyHighlightBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0xF2, 0xFF));
        private static readonly SolidColorBrush PersonFamilyBorderBrush = new SolidColorBrush(Color.FromRgb(0x9C, 0xBE, 0xE8));
        private static readonly MessagePackSerializerOptions MsgPackOptions =
            MessagePackSerializerOptions.Standard.WithResolver(ContractlessStandardResolverAllowPrivate.Instance);
        public ICollectionView PersonGridView { get; private set; }
        private readonly ObservableCollection<PhaDoRenderScopeItem> _phaDoRenderScopes = new ObservableCollection<PhaDoRenderScopeItem>();
        private int _phaDoRenderScopeSourceRootId;

        /// <summary>Một lựa chọn vẽ ở toolbar: Toàn phả hoặc một nhánh phả con.</summary>
        private sealed class PhaDoRenderScopeItem
        {
            public string Label { get; set; }
            public int FamilyId { get; set; }
            public FamilyViewModel RootFamily { get; set; }
            public bool IsWholeTree { get; set; }
            public int MaxGenerationInclusive { get; set; } = int.MaxValue;

            public override string ToString()
            {
                return Label ?? "";
            }
        }

        /// <summary>Xóa cache tab Người khi đổi file gia phả / cây mới.</summary>
        public void InvalidatePersonGridCache()
        {
            _personGridCacheRoot = null;
            _personGridCachedRows = null;
            _personGridSelectedFamilyRoot = null;
            _personGridSelectedFamilyId = 0;
            _personGridRows.Clear();
            SafeRefreshPersonGridView();
        }

        public bool IsRestoringWorkspace => _isRestoringWorkspace;

        private void SafeRefreshPersonGridView()
        {
            if (PersonGridView == null)
            {
                return;
            }

            // WPF không cho Refresh trong lúc DataGrid đang AddNew/EditItem.
            if (PersonGridView is IEditableCollectionView editable
                && (editable.IsAddingNew || editable.IsEditingItem))
            {
                Dispatcher.BeginInvoke(new Action(SafeRefreshPersonGridView), DispatcherPriority.Background);
                return;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    PersonGridView.Refresh();
                }
                catch (InvalidOperationException)
                {
                    // Nếu vẫn đang trong transaction edit thì defer tiếp
                    Dispatcher.BeginInvoke(new Action(SafeRefreshPersonGridView), DispatcherPriority.Background);
                }
            }), DispatcherPriority.Background);
        }

        private GiaPhaRenderOptions BuildPhaDoRenderOptions(bool forPrint = false)
        {
            var options = forPrint
                ? GiaPhaRenderOptions.ForFitContentPrint()
                : GiaPhaRenderOptions.ForFitContent(96);
            GiaPhaLayoutSettingsStore.Current?.ApplyTo(options);
            ApplyPhaDoTitleStyleToOptions(options);
            ApplySelectedPhaDoCardLayoutMode(options);
            return options;
        }

        private void ApplyPhaDoTitleStyleToOptions(GiaPhaRenderOptions options)
        {
            if (options == null)
            {
                return;
            }

            var gp = viewModel?.FamilyTree?.GP;
            if (_phaDoTitleStyle != null && gp?.SvgShapesById != null)
            {
                PhaDoSvgCatalog.ResolveShapeIntoFrame(_phaDoTitleStyle, gp.SvgShapesById);
            }

            PhaDoTitleStyleResolver.ApplyToOptions(
                options,
                _phaDoTitleStyle,
                gp?.GiaphaName,
                gp?.RF_OTAI);
        }

        /// <summary>Đọc radio kiểu chữ: 0 Ngang, 1 Dọc, 2 Dọc theo từ.</summary>
        private int GetPhaDoCardLayoutListIndex()
        {
            if (phaDoLayoutDocTu?.IsChecked == true)
            {
                return 2;
            }

            if (phaDoLayoutDoc?.IsChecked == true)
            {
                return 1;
            }

            return 0;
        }

        private void SetPhaDoCardLayoutIndex(int index)
        {
            if (phaDoLayoutNgang == null)
            {
                return;
            }

            index = Math.Max(0, Math.Min(2, index));
            phaDoLayoutNgang.IsChecked = index == 0;
            phaDoLayoutDoc.IsChecked = index == 1;
            phaDoLayoutDocTu.IsChecked = index == 2;
        }

        private void ApplySelectedPhaDoCardLayoutMode(GiaPhaRenderOptions options)
        {
            switch (GetPhaDoCardLayoutListIndex())
            {
                case 1:
                    GiaPhaRenderOptions.ApplyVerticalCardLayout(options);
                    break;
                case 2:
                    GiaPhaRenderOptions.ApplyVerticalWordCardLayout(options);
                    break;
                default:
                    options.CardLayoutMode = GiaPhaCardLayoutMode.Horizontal;
                    break;
            }
        }

        private static int ResolvePhaDoCardLayoutIndexFromSession(PhaDoWorkspaceState phaDo)
        {
            if (phaDo == null)
            {
                return 0;
            }

            if (phaDo.CardLayoutIndex >= 0 && phaDo.CardLayoutIndex <= 2)
            {
                return phaDo.CardLayoutIndex;
            }

            if (phaDo.VerticalWordStyle)
            {
                return 2;
            }

            if (phaDo.VerticalCards)
            {
                return 1;
            }

            return 0;
        }

        private static string DescribePhaDoCardLayoutIndex(int index)
        {
            switch (index)
            {
                case 1: return "thẻ dọc từng ký tự";
                case 2: return "thẻ dọc theo từ (Word)";
                default: return "thẻ ngang";
            }
        }

        private GiaPhaPlacedNode FindNodeByFamilyId(int familyId)
        {
            if (_phaDoRenderedLayout == null || familyId <= 0)
            {
                return null;
            }

            return _phaDoRenderedLayout.Nodes
                .FirstOrDefault(n => (n.Family?.familyInfo?.FamilyId ?? 0) == familyId);
        }

        private static int GetFamilyIdFromElementTag(object tag)
        {
            if (tag is PhaDoBoxVisualTag visualTag)
            {
                return visualTag.Family?.familyInfo?.FamilyId ?? 0;
            }

            if (tag is PhaDoBoxBackgroundTag bgTag)
            {
                return bgTag.Family?.familyInfo?.FamilyId ?? 0;
            }

            var family = tag as FamilyViewModel;
            return family?.familyInfo?.FamilyId ?? 0;
        }

        private bool TryResolveFamilyFromCanvasHit(
            DependencyObject source,
            out int familyId,
            out GiaPhaPlacedNode node)
        {
            familyId = 0;
            node = null;
            if (source == null || _phaDoRenderedLayout == null)
            {
                return false;
            }

            DependencyObject cursor = source;
            while (cursor != null)
            {
                if (cursor is FrameworkElement fe)
                {
                    familyId = GetFamilyIdFromElementTag(fe.Tag);
                    if (familyId > 0)
                    {
                        break;
                    }
                }

                cursor = VisualTreeHelper.GetParent(cursor);
            }

            if (familyId <= 0)
            {
                return false;
            }

            node = FindNodeByFamilyId(familyId);
            return node != null;
        }

        /// <summary>Chỉ viền chọn trên ô phả đồ (không đụng TreeView / ô Text).</summary>
        private void SelectPhaDoBoxOutline(int familyId, int? personSlot = null)
        {
            if (familyId <= 0)
            {
                return;
            }

            bool familyChanged = _phaDoSelectedFamilyId != familyId;
            _phaDoSelectedFamilyId = familyId;
            if (personSlot.HasValue)
            {
                _phaDoSelectedPersonSlot = personSlot.Value;
            }
            else if (familyChanged)
            {
                _phaDoSelectedPersonSlot = -1;
            }

            DrawSelectionOverlay(familyId);
            UpdatePhaDoSelectedBoxSizeStatus(familyId);
        }

        private void UpdatePhaDoSelectedBoxSizeStatus(int familyId)
        {
            if (viewModel == null)
            {
                return;
            }

            if (familyId <= 0 || !TryGetFamilyBoxPrintSizeMm(familyId, out double widthMm, out double heightMm))
            {
                viewModel.PhaDoSelectedBoxSizeText = "";
                return;
            }

            var culture = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
            viewModel.PhaDoSelectedBoxSizeText = string.Format(
                culture,
                " | Ô: {0:0.#} × {1:0.#} cm",
                widthMm / 10.0,
                heightMm / 10.0);
        }

        private bool TryGetFamilyBoxPrintSizeMm(int familyId, out double widthMm, out double heightMm)
        {
            widthMm = 0;
            heightMm = 0;
            var node = FindNodeByFamilyId(familyId);
            if (node?.Metrics == null)
            {
                return false;
            }

            widthMm = node.Metrics.WidthMm;
            heightMm = node.Metrics.HeightMm;
            return widthMm > 0 && heightMm > 0;
        }

        private static bool TryGetPhaDoBoxVisualTag(DependencyObject source, out PhaDoBoxVisualTag tag, out FrameworkElement element)
        {
            tag = null;
            element = null;
            DependencyObject cursor = source;
            while (cursor != null)
            {
                if (cursor is FrameworkElement fe && fe.Tag is PhaDoBoxVisualTag visualTag)
                {
                    tag = visualTag;
                    element = fe;
                    return true;
                }

                cursor = VisualTreeHelper.GetParent(cursor);
            }

            return false;
        }

        private bool TryResolvePersonElementHit(
            DependencyObject source,
            out int familyId,
            out int personSlot,
            out FrameworkElement element)
        {
            familyId = 0;
            personSlot = -1;
            element = null;
            if (!TryGetPhaDoBoxVisualTag(source, out var tag, out element) || !tag.IsMovablePerson)
            {
                return false;
            }

            familyId = tag.Family?.familyInfo?.FamilyId ?? 0;
            personSlot = tag.PersonSlotIndex;
            return familyId > 0;
        }

        private static bool IsHitOnBoxBackground(DependencyObject source, int familyId)
        {
            DependencyObject cursor = source;
            while (cursor != null)
            {
                if (cursor is FrameworkElement fe
                    && fe.Tag is PhaDoBoxBackgroundTag bg
                    && (bg.Family?.familyInfo?.FamilyId ?? 0) == familyId)
                {
                    return true;
                }

                cursor = VisualTreeHelper.GetParent(cursor);
            }

            return false;
        }

        private FrameworkElement FindPersonVisual(int familyId, int personSlot)
        {
            FrameworkElement best = null;
            int bestZ = int.MinValue;
            foreach (var child in theCanvas.Children.OfType<FrameworkElement>())
            {
                if (child.Tag is PhaDoBoxVisualTag tag
                    && (tag.Family?.familyInfo?.FamilyId ?? 0) == familyId
                    && tag.PersonSlotIndex == personSlot
                    && tag.IsSelectable)
                {
                    int z = Panel.GetZIndex(child);
                    if (best == null || z >= bestZ)
                    {
                        best = child;
                        bestZ = z;
                    }
                }
            }

            return best;
        }

        private static PhaDoPersonLayoutOffset GetPersonOffset(PhaDoBoxStyle style, int slot)
        {
            if (style?.PersonOffsetsBySlot != null
                && style.PersonOffsetsBySlot.TryGetValue(slot, out var offset)
                && offset != null)
            {
                return offset;
            }

            return new PhaDoPersonLayoutOffset();
        }

        private static void SetPersonOffset(PhaDoBoxStyle style, int slot, double deltaXmm, double deltaYmm)
        {
            if (style == null)
            {
                return;
            }

            if (style.PersonOffsetsBySlot == null)
            {
                style.PersonOffsetsBySlot = new Dictionary<int, PhaDoPersonLayoutOffset>();
            }

            if (Math.Abs(deltaXmm) < 0.01 && Math.Abs(deltaYmm) < 0.01)
            {
                style.PersonOffsetsBySlot.Remove(slot);
            }
            else
            {
                style.PersonOffsetsBySlot[slot] = new PhaDoPersonLayoutOffset
                {
                    DeltaXmm = deltaXmm,
                    DeltaYmm = deltaYmm
                };
            }
        }

        private void ApplyPersonOffsetsForFamily(int familyId)
        {
            var style = GetBoxStyleForFamily(familyId);
            if (style?.PersonOffsetsBySlot == null || style.PersonOffsetsBySlot.Count == 0)
            {
                return;
            }

            foreach (var child in theCanvas.Children.OfType<FrameworkElement>())
            {
                if (child.Tag is PhaDoBoxVisualTag tag
                    && (tag.Family?.familyInfo?.FamilyId ?? 0) == familyId
                    && tag.IsMovablePerson
                    && style.PersonOffsetsBySlot.TryGetValue(tag.PersonSlotIndex, out var offset)
                    && offset != null)
                {
                    double left = Canvas.GetLeft(child);
                    double top = Canvas.GetTop(child);
                    if (double.IsNaN(left))
                    {
                        left = 0;
                    }

                    if (double.IsNaN(top))
                    {
                        top = 0;
                    }

                    Canvas.SetLeft(child, left + MmToPx(offset.DeltaXmm));
                    Canvas.SetTop(child, top + MmToPx(offset.DeltaYmm));
                }
            }
        }

        private void ClearPersonSelectionHighlight()
        {
            var overlays = theCanvas.Children
                .OfType<FrameworkElement>()
                .Where(fe => Equals(fe.Tag, "__PhaDoPersonSelection"))
                .ToList();
            foreach (var fe in overlays)
            {
                theCanvas.Children.Remove(fe);
            }
        }

        private void UpdatePersonSelectionHighlight()
        {
            ClearPersonSelectionHighlight();
            if (_phaDoSelectedFamilyId <= 0 || _phaDoSelectedPersonSlot < 0)
            {
                return;
            }

            var element = FindPersonVisual(_phaDoSelectedFamilyId, _phaDoSelectedPersonSlot);
            if (element == null)
            {
                return;
            }

            element.UpdateLayout();
            double left = Canvas.GetLeft(element);
            double top = Canvas.GetTop(element);
            if (double.IsNaN(left))
            {
                left = 0;
            }

            if (double.IsNaN(top))
            {
                top = 0;
            }

            double w = element.ActualWidth;
            double h = element.ActualHeight;
            if (w < 1 || h < 1)
            {
                element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                w = element.DesiredSize.Width;
                h = element.DesiredSize.Height;
            }

            const double pad = 2;
            var outline = new Rectangle
            {
                Width = Math.Max(4, w + pad * 2),
                Height = Math.Max(4, h + pad * 2),
                Stroke = Brushes.DarkOrange,
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 3, 2 },
                Fill = Brushes.Transparent,
                IsHitTestVisible = false,
                Tag = "__PhaDoPersonSelection"
            };
            Canvas.SetLeft(outline, left - pad);
            Canvas.SetTop(outline, top - pad);
            Panel.SetZIndex(outline, 1003);
            theCanvas.Children.Add(outline);
        }

        private void GetPersonElementSize(FrameworkElement element, out double width, out double height)
        {
            element.UpdateLayout();
            width = element.ActualWidth;
            height = element.ActualHeight;
            if (width < 1 || height < 1)
            {
                element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                width = element.DesiredSize.Width;
                height = element.DesiredSize.Height;
            }
        }

        private void ApplyPersonOffsetClamped(
            int familyId,
            int personSlot,
            FrameworkElement element,
            double deltaXmm,
            double deltaYmm)
        {
            if (!TryGetFamilyBackgroundBounds(familyId, out double boxLeft, out double boxTop, out double boxW, out double boxH))
            {
                return;
            }

            GetPersonElementSize(element, out double elW, out double elH);
            double newLeft = _phaDoPersonNaturalLeftPx + MmToPx(deltaXmm);
            double newTop = _phaDoPersonNaturalTopPx + MmToPx(deltaYmm);
            newLeft = Math.Max(boxLeft, Math.Min(boxLeft + boxW - elW, newLeft));
            newTop = Math.Max(boxTop, Math.Min(boxTop + boxH - elH, newTop));

            Canvas.SetLeft(element, newLeft);
            Canvas.SetTop(element, newTop);

            deltaXmm = PxToMm(newLeft - _phaDoPersonNaturalLeftPx);
            deltaYmm = PxToMm(newTop - _phaDoPersonNaturalTopPx);
            var style = GetBoxStyleForFamily(familyId);
            SetPersonOffset(style, personSlot, deltaXmm, deltaYmm);
            _phaDoBoxStyleByFamilyId[familyId] = style;
        }

        private void BeginPersonDrag(MouseButtonEventArgs e, int familyId, int personSlot, FrameworkElement element)
        {
            SelectPhaDoBoxOutline(familyId, personSlot);

            var style = GetBoxStyleForFamily(familyId);
            var offset = GetPersonOffset(style, personSlot);
            double left = Canvas.GetLeft(element);
            double top = Canvas.GetTop(element);
            if (double.IsNaN(left))
            {
                left = 0;
            }

            if (double.IsNaN(top))
            {
                top = 0;
            }

            _phaDoIsDraggingPerson = true;
            _phaDoDraggingPersonFamilyId = familyId;
            _phaDoDraggingPersonSlot = personSlot;
            _phaDoDraggingPersonElement = element;
            _phaDoPersonNaturalLeftPx = left - MmToPx(offset.DeltaXmm);
            _phaDoPersonNaturalTopPx = top - MmToPx(offset.DeltaYmm);
            _phaDoPersonDragStartCanvas = e.GetPosition(theCanvas);
            _phaDoPersonDragStartDeltaXmm = offset.DeltaXmm;
            _phaDoPersonDragStartDeltaYmm = offset.DeltaYmm;
            theCanvas.CaptureMouse();
            theCanvas.Cursor = Cursors.SizeAll;
        }

        private void UpdatePersonDrag(Point canvasPoint)
        {
            if (!_phaDoIsDraggingPerson || _phaDoDraggingPersonElement == null)
            {
                return;
            }

            var delta = GetPhaDoCanvasDeltaMm(canvasPoint, _phaDoPersonDragStartCanvas);
            double newDeltaX = _phaDoPersonDragStartDeltaXmm + delta.X;
            double newDeltaY = _phaDoPersonDragStartDeltaYmm + delta.Y;
            ApplyPersonOffsetClamped(
                _phaDoDraggingPersonFamilyId,
                _phaDoDraggingPersonSlot,
                _phaDoDraggingPersonElement,
                newDeltaX,
                newDeltaY);
            UpdatePersonSelectionHighlight();
        }

        private void EndPersonDrag()
        {
            if (!_phaDoIsDraggingPerson)
            {
                return;
            }

            _phaDoIsDraggingPerson = false;
            _phaDoDraggingPersonFamilyId = 0;
            _phaDoDraggingPersonSlot = -1;
            _phaDoDraggingPersonElement = null;
            theCanvas.ReleaseMouseCapture();
            theCanvas.Cursor = null;
            DrawSelectionOverlay(_phaDoSelectedFamilyId);
            SaveWorkspaceSession();
        }

        private void SelectPhaDoFamily(int familyId, GiaPhaPlacedNode node)
        {
            SelectPhaDoBoxOutline(familyId);
            SelectFamilyInTreeView(node.Family);

            var main = node.Family?.ListPerson?.FirstOrDefault(p => p.IsMainPerson == 1)
                ?? node.Family?.ListPerson?.FirstOrDefault();
        }

        private void OpenFamilyDetailFromPhaDo(FamilyViewModel family)
        {
            if (family == null)
            {
                return;
            }

            SelectFamilyInTreeView(family);
            if (tabControl != null && tabControl.SelectedIndex != 1)
            {
                tabControl.SelectedIndex = 1;
            }
        }

        private static Brush GetDefaultBranchFillBrush(int familyId)
        {
            Color[] palette =
            {
                Color.FromRgb(255, 243, 224),
                Color.FromRgb(232, 245, 233),
                Color.FromRgb(227, 242, 253),
                Color.FromRgb(252, 228, 236),
                Color.FromRgb(237, 231, 246),
                Color.FromRgb(255, 249, 196)
            };
            return new SolidColorBrush(palette[Math.Abs(familyId) % palette.Length]);
        }

        private PhaDoBoxStyle GetBoxStyleForFamily(int familyId)
        {
            PhaDoBoxStyle style;
            if (_phaDoBoxStyleByFamilyId.TryGetValue(familyId, out var stored))
            {
                style = stored.Clone();
            }
            else
            {
                style = new PhaDoBoxStyle();
            }

            var gp = viewModel?.FamilyTree?.GP;
            var family = viewModel?.FamilyTree?.FindFamilyInfoById(familyId);
            if (string.IsNullOrWhiteSpace(style.ShapeSvgId)
                && family != null
                && !string.IsNullOrWhiteSpace(family.PhaDoShapeSvgId))
            {
                style.ShapeSvgId = family.PhaDoShapeSvgId;
            }

            PhaDoSvgCatalog.ResolveShapeIntoStyle(style, gp?.SvgShapesById);
            return style;
        }

        private void SetFamilyPhaDoShapeSvgId(int familyId, string svgId)
        {
            var family = viewModel?.FamilyTree?.FindFamilyInfoById(familyId);
            if (family == null)
            {
                return;
            }

            family.PhaDoShapeSvgId = string.IsNullOrWhiteSpace(svgId) ? null : svgId;
        }

        /// <summary>Đồng bộ svgId từ cây gia phả trong file .json vào bộ nhớ phả đồ.</summary>
        public void SyncPhaDoBoxStylesFromGiaPhaFile()
        {
            var gp = viewModel?.FamilyTree?.GP;
            if (gp?.familyRoot == null)
            {
                return;
            }

            SyncPhaDoBoxStylesFromFamilyInfo(gp.familyRoot, gp);
        }

        private void SyncPhaDoBoxStylesFromFamilyInfo(FamilyInfo family, GiaphaInfo gp)
        {
            if (family == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(family.PhaDoShapeSvgId))
            {
                if (!_phaDoBoxStyleByFamilyId.TryGetValue(family.FamilyId, out var style))
                {
                    style = new PhaDoBoxStyle();
                }

                style.ShapeSvgId = family.PhaDoShapeSvgId;
                PhaDoSvgCatalog.ResolveShapeIntoStyle(style, gp.SvgShapesById);
                _phaDoBoxStyleByFamilyId[family.FamilyId] = style;
            }

            if (family.FamilyChildren == null)
            {
                return;
            }

            foreach (var child in family.FamilyChildren)
            {
                SyncPhaDoBoxStylesFromFamilyInfo(child, gp);
            }
        }

        private void MigrateAndResolveBoxStylesFromSession()
        {
            var gp = viewModel?.FamilyTree?.GP;
            if (gp == null)
            {
                return;
            }

            foreach (var kv in _phaDoBoxStyleByFamilyId.ToList())
            {
                var style = kv.Value;
                if (style == null)
                {
                    continue;
                }

                int familyId = kv.Key;
                if (!string.IsNullOrWhiteSpace(style.CustomShapeSvg) && string.IsNullOrWhiteSpace(style.ShapeSvgId))
                {
                    string svgId = PhaDoSvgCatalog.UpsertShape(
                        gp,
                        style.CustomShapeSvg,
                        style.CustomShapeViewBoxWidth,
                        style.CustomShapeViewBoxHeight);
                    style.ShapeSvgId = svgId;
                    SetFamilyPhaDoShapeSvgId(familyId, svgId);
                }

                if (!string.IsNullOrWhiteSpace(style.ShapeSvgId))
                {
                    var family = viewModel.FamilyTree.FindFamilyInfoById(familyId);
                    if (family != null && string.IsNullOrWhiteSpace(family.PhaDoShapeSvgId))
                    {
                        SetFamilyPhaDoShapeSvgId(familyId, style.ShapeSvgId);
                    }
                }

                PhaDoSvgCatalog.ResolveShapeIntoStyle(style, gp.SvgShapesById);
                _phaDoBoxStyleByFamilyId[familyId] = style;
            }
        }

        private List<int> GetFamilyIdsInSameLevel(int familyId)
        {
            var node = FindNodeByFamilyId(familyId);
            if (node == null || _phaDoRenderedLayout?.Nodes == null)
            {
                return new List<int> { familyId };
            }

            return _phaDoRenderedLayout.Nodes
                .Where(n => n.Level == node.Level)
                .Select(n => n.Family?.familyInfo?.FamilyId ?? 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
        }

        private List<int> GetAllRenderedFamilyIds()
        {
            if (_phaDoRenderedLayout?.Nodes == null)
            {
                return new List<int>();
            }

            return _phaDoRenderedLayout.Nodes
                .Select(n => n.Family?.familyInfo?.FamilyId ?? 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
        }

        private PhaDoBoxLayoutSnapshot CaptureBoxLayoutSnapshot(int familyId)
        {
            double? ox = _phaDoOffsetXmmByFamilyId.TryGetValue(familyId, out double offsetX) ? offsetX : (double?)null;
            double? oy = _phaDoOffsetYmmByFamilyId.TryGetValue(familyId, out double offsetY) ? offsetY : (double?)null;
            return PhaDoBoxLayoutSnapshot.FromBoxStyle(GetBoxStyleForFamily(familyId), ox, oy);
        }

        private void ApplyBoxLayoutSnapshotToFamily(int targetFamilyId, PhaDoBoxLayoutSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            var style = GetBoxStyleForFamily(targetFamilyId);
            style.CustomWidthMm = snapshot.CustomWidthMm;
            style.CustomHeightMm = snapshot.CustomHeightMm;
            style.PersonOffsetsBySlot = snapshot.PersonOffsetsBySlot?.ToDictionary(
                kv => kv.Key,
                kv => new PhaDoPersonLayoutOffset
                {
                    DeltaXmm = kv.Value?.DeltaXmm ?? 0,
                    DeltaYmm = kv.Value?.DeltaYmm ?? 0
                }) ?? new Dictionary<int, PhaDoPersonLayoutOffset>();
            _phaDoBoxStyleByFamilyId[targetFamilyId] = style;

            if (snapshot.OffsetXmm.HasValue)
            {
                _phaDoOffsetXmmByFamilyId[targetFamilyId] = snapshot.OffsetXmm.Value;
            }
            else
            {
                _phaDoOffsetXmmByFamilyId.Remove(targetFamilyId);
            }

            if (snapshot.OffsetYmm.HasValue)
            {
                _phaDoOffsetYmmByFamilyId[targetFamilyId] = snapshot.OffsetYmm.Value;
            }
            else
            {
                _phaDoOffsetYmmByFamilyId.Remove(targetFamilyId);
            }
        }

        private async Task ApplyBoxLayoutFromSourceAsync(int sourceFamilyId, IList<int> targetFamilyIds, string actionLabel)
        {
            if (_phaDoRenderedLayout == null)
            {
                MessageBox.Show("Hãy vẽ phả đồ trước.", "Layout ô");
                return;
            }

            var snapshot = CaptureBoxLayoutSnapshot(sourceFamilyId);
            if (!snapshot.HasAny)
            {
                MessageBox.Show(
                    "Gia đình đang chọn chưa có chỉnh layout (kích thước ô, vị trí chữ trong ô, vị trí kéo ô…).",
                    "Layout ô");
                return;
            }

            if (targetFamilyIds == null || targetFamilyIds.Count == 0)
            {
                return;
            }

            foreach (int id in targetFamilyIds)
            {
                ApplyBoxLayoutSnapshotToFamily(id, snapshot);
            }

            await RenderPhaDoCoreAsync(resetZoom: false, resetScroll: false).ConfigureAwait(true);
            SelectPhaDoBoxOutline(sourceFamilyId, _phaDoSelectedPersonSlot >= 0 ? _phaDoSelectedPersonSlot : (int?)null);
            SaveWorkspaceSession();
            viewModel?.AddUserAction(actionLabel + ": " + targetFamilyIds.Count + " ô.");
        }

        private async void ApplyBoxLayoutToLevel_Click(int sourceFamilyId)
        {
            var targets = GetFamilyIdsInSameLevel(sourceFamilyId);
            int level = FindNodeByFamilyId(sourceFamilyId)?.Level ?? 0;
            await ApplyBoxLayoutFromSourceAsync(
                sourceFamilyId,
                targets,
                "Áp layout ô cho đời " + level).ConfigureAwait(true);
        }

        private async void ApplyBoxLayoutToAllGiaPha_Click(int sourceFamilyId)
        {
            await ApplyBoxLayoutFromSourceAsync(
                sourceFamilyId,
                GetAllRenderedFamilyIds(),
                "Áp layout ô cho toàn gia phả").ConfigureAwait(true);
        }

        private void OpenSvgLibraryManager_Click(object sender, RoutedEventArgs e)
        {
            var win = new SvgLibraryManagerWindow(
                viewModel?.FamilyTree?.GP,
                () => viewModel != null && viewModel.SaveFileCommandFunc())
            {
                Owner = this
            };
            win.Show();
        }

        private async void OpenSettingsDialog_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SettingsDialog(GiaPhaLayoutSettingsStore.Current.Clone())
            {
                Owner = this
            };

            if (dlg.ShowDialog() != true)
            {
                return;
            }

            try
            {
                GiaPhaLayoutSettingsStore.Save(dlg.Settings);
            }
            catch (Exception ex)
            {
                log.Warn("Lưu cài đặt layout thất bại.", ex);
                await this.ShowMessageAsync(
                    "Cài đặt",
                    "Không lưu được file cài đặt: " + ex.Message).ConfigureAwait(true);
                return;
            }

            if (_phaDoRenderedLayout != null && viewModel?.FamilyTree?.Family?.RootPerson != null)
            {
                await RunPhaDoRenderWithWaitDialogAsync(resetZoom: false, resetScroll: false).ConfigureAwait(true);
                viewModel.AddUserAction("Đã áp dụng cài đặt phả đồ mới — vẽ lại.");
            }
            else
            {
                viewModel?.AddUserAction("Đã lưu cài đặt phả đồ (áp dụng khi bấm Vẽ).");
            }
        }

        private async void PhaDoTitleStyle_Click(object sender, RoutedEventArgs e)
        {
            var catalog = viewModel?.FamilyTree?.GP?.SvgShapesById;
            var dlg = new PhaDoTitleStyleDialog(_phaDoTitleStyle?.Clone(), _phaDoCurrentOptions, catalog)
            {
                Owner = this
            };
            if (dlg.ShowDialog() != true || dlg.ResultStyle == null)
            {
                return;
            }

            _phaDoTitleStyle = dlg.ResultStyle.Clone();

            if (dlg.IsNewSvgFromEditor)
            {
                string savedId = PromptSaveNewSvgToCatalogForTitle(_phaDoTitleStyle);
                if (!string.IsNullOrWhiteSpace(savedId))
                {
                    _phaDoTitleStyle.ShapeSvgId = savedId;
                    PhaDoSvgCatalog.ResolveShapeIntoFrame(_phaDoTitleStyle, viewModel?.FamilyTree?.GP?.SvgShapesById);
                }
            }

            SaveWorkspaceSession();

            if (_phaDoRenderedLayout != null && viewModel.FamilyTree?.Family?.RootPerson != null)
            {
                await RenderPhaDoCoreAsync(resetZoom: false, resetScroll: false).ConfigureAwait(true);
                viewModel.AddUserAction("Đã cập nhật khối tiêu đề phả đồ.");
            }
        }

        private void ShowPhaDoBoxStyleDialog(int familyId)
        {
            var catalog = viewModel?.FamilyTree?.GP?.SvgShapesById;
            var dlg = new PhaDoBoxStyleDialog(
                GetBoxStyleForFamily(familyId),
                _phaDoCurrentOptions,
                catalog)
            {
                Owner = this
            };
            dlg.StyleApplyRequested += (s, e) =>
            {
                var targetIds = e.Scope == PhaDoStyleApplyScope.AllBoxesInLevel
                    ? GetFamilyIdsInSameLevel(familyId)
                    : new List<int> { familyId };

                ApplyPhaDoBoxStyleFromDialog(familyId, e);

                string savedSvgId = null;
                if (e.IsNewSvgFromEditor)
                {
                    savedSvgId = PromptSaveNewSvgToCatalog(e.Style, targetIds);
                }

                string refreshId = savedSvgId ?? e?.Style?.ShapeSvgId;
                if (string.IsNullOrWhiteSpace(refreshId)
                    && targetIds.Count > 0
                    && viewModel.FamilyTree.FindFamilyInfoById(targetIds[0]) is FamilyInfo fam
                    && !string.IsNullOrWhiteSpace(fam.PhaDoShapeSvgId))
                {
                    refreshId = fam.PhaDoShapeSvgId;
                }

                dlg.RefreshSvgFrameCatalog(viewModel?.FamilyTree?.GP?.SvgShapesById, refreshId);
            };
            dlg.ShowDialog();
            SaveWorkspaceSession();
        }

        /// <summary>Apply Box / Apply All In Level — cập nhật canvas xem trước, dialog không đóng.</summary>
        private void ApplyPhaDoBoxStyleFromDialog(int sourceFamilyId, PhaDoBoxStyleDialog.StyleApplyEventArgs e)
        {
            if (e?.Style == null)
            {
                return;
            }

            var targetIds = e.Scope == PhaDoStyleApplyScope.AllBoxesInLevel
                ? GetFamilyIdsInSameLevel(sourceFamilyId)
                : new List<int> { sourceFamilyId };

            var gp = viewModel?.FamilyTree?.GP;

            foreach (int id in targetIds)
            {
                var style = e.Style.Clone();

                if (!string.IsNullOrWhiteSpace(style.ShapeSvgId) && gp != null)
                {
                    if (gp.SvgShapesById == null
                        || !gp.SvgShapesById.ContainsKey(style.ShapeSvgId))
                    {
                        if (!string.IsNullOrWhiteSpace(style.CustomShapeSvg))
                        {
                            PhaDoSvgCatalog.UpsertShape(
                                gp,
                                style.CustomShapeSvg,
                                style.CustomShapeViewBoxWidth,
                                style.CustomShapeViewBoxHeight);
                        }
                    }

                    SetFamilyPhaDoShapeSvgId(id, style.ShapeSvgId);
                    PhaDoSvgCatalog.ResolveShapeIntoStyle(style, gp.SvgShapesById);
                }
                else if (!string.IsNullOrWhiteSpace(style.CustomShapeSvg) && gp != null)
                {
                    if (e.IsNewSvgFromEditor)
                    {
                        SetFamilyPhaDoShapeSvgId(id, null);
                    }
                    else
                    {
                        string svgId = PhaDoSvgCatalog.UpsertShape(
                            gp,
                            style.CustomShapeSvg,
                            style.CustomShapeViewBoxWidth,
                            style.CustomShapeViewBoxHeight);
                        style.ShapeSvgId = svgId;
                        SetFamilyPhaDoShapeSvgId(id, svgId);
                    }
                }
                else
                {
                    style.ShapeSvgId = null;
                    SetFamilyPhaDoShapeSvgId(id, null);
                    style.CustomShapeSvg = null;
                }

                if (style.HasAnyOverride())
                {
                    _phaDoBoxStyleByFamilyId[id] = style;
                }
                else
                {
                    _phaDoBoxStyleByFamilyId.Remove(id);
                }

                ApplyBoxStyleToFamilyVisuals(id);
            }

            if (_phaDoSelectedFamilyId > 0)
            {
                DrawSelectionOverlay(_phaDoSelectedFamilyId);
                SyncPhaDoToolbarFromBoxStyle(_phaDoSelectedFamilyId);
            }
        }

        /// <summary>Sau Apply khung mới — hỏi lưu catalog có tên, rồi ghi file gia phả. Trả về svgId đã lưu.</summary>
        private string PromptSaveNewSvgToCatalog(PhaDoBoxStyle style, List<int> familyIds)
        {
            if (style == null
                || string.IsNullOrWhiteSpace(style.CustomShapeSvg)
                || familyIds == null
                || familyIds.Count == 0)
            {
                return null;
            }

            var gp = viewModel?.FamilyTree?.GP;
            if (gp == null)
            {
                return null;
            }

            var answer = MessageBox.Show(
                "Bạn có muốn lưu khung SVG này vào file gia phả với tên để dùng lại sau?\n\n"
                + "• Có — đặt tên và lưu file gia phả\n"
                + "• Không — chỉ lưu mã tự động (hash), không ghi file ngay\n"
                + "• Hủy — giữ áp dụng tạm trên phả đồ",
                "Lưu khung SVG",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (answer == MessageBoxResult.Cancel)
            {
                return null;
            }

            string svgId;
            if (answer == MessageBoxResult.Yes)
            {
                svgId = PromptUserSvgFrameNameAndUpsert(gp, style);
                if (string.IsNullOrWhiteSpace(svgId))
                {
                    return null;
                }
            }
            else
            {
                svgId = PhaDoSvgCatalog.UpsertShape(
                    gp,
                    style.CustomShapeSvg,
                    style.CustomShapeViewBoxWidth,
                    style.CustomShapeViewBoxHeight);
            }

            if (string.IsNullOrWhiteSpace(svgId))
            {
                return null;
            }

            // Gán svgId lên family trước khi ghi file (catalog + family info trong .json).
            CommitSvgIdToFamilies(svgId, style, familyIds, gp);

            if (answer == MessageBoxResult.Yes)
            {
                if (viewModel.SaveFileCommandFunc())
                {
                    MessageBox.Show(
                        "Đã lưu khung \"" + svgId + "\" vào catalog và file gia phả.",
                        "Đã lưu",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        "Khung đã thêm vào catalog nhưng lưu file gia phả thất bại. Hãy Save file thủ công.",
                        "Lưu file",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }

            return svgId;
        }

        private string PromptSaveNewSvgToCatalogForTitle(PhaDoTitleStyle style)
        {
            if (style == null || string.IsNullOrWhiteSpace(style.CustomShapeSvg))
            {
                return null;
            }

            var gp = viewModel?.FamilyTree?.GP;
            if (gp == null)
            {
                return null;
            }

            var answer = MessageBox.Show(
                "Bạn có muốn lưu khung SVG này vào file gia phả với tên để dùng lại sau?\n\n"
                + "• Có — đặt tên và lưu file gia phả\n"
                + "• Không — chỉ lưu mã tự động (hash), không ghi file ngay\n"
                + "• Hủy — giữ áp dụng tạm trên phả đồ",
                "Lưu khung SVG (tiêu đề)",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (answer == MessageBoxResult.Cancel)
            {
                return null;
            }

            string svgId;
            if (answer == MessageBoxResult.Yes)
            {
                svgId = PromptUserSvgFrameNameAndUpsert(gp, style);
            }
            else
            {
                svgId = PhaDoSvgCatalog.UpsertShape(
                    gp,
                    style.CustomShapeSvg,
                    style.CustomShapeViewBoxWidth,
                    style.CustomShapeViewBoxHeight);
            }

            if (string.IsNullOrWhiteSpace(svgId))
            {
                return null;
            }

            if (answer == MessageBoxResult.Yes && viewModel.SaveFileCommandFunc())
            {
                MessageBox.Show(
                    "Đã lưu khung \"" + svgId + "\" vào catalog và file gia phả.",
                    "Đã lưu",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            return svgId;
        }

        private string PromptUserSvgFrameNameAndUpsert(GiaphaInfo gp, IPhaDoSvgFrameStyle style)
        {
            while (true)
            {
                string name = ShowSvgFrameNameInputDialog();
                if (name == null)
                {
                    return null;
                }

                string svgId = PhaDoSvgCatalog.NormalizeUserSvgId(name);
                if (string.IsNullOrWhiteSpace(svgId))
                {
                    MessageBox.Show(
                        "Tên không hợp lệ. Dùng chữ, số, dấu gạch ngang hoặc gạch dưới.",
                        "Tên khung SVG",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    continue;
                }

                if (gp.SvgShapesById != null && gp.SvgShapesById.ContainsKey(svgId))
                {
                    var overwrite = MessageBox.Show(
                        "Khung \"" + svgId + "\" đã có trong file. Ghi đè?",
                        "Trùng tên",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (overwrite != MessageBoxResult.Yes)
                    {
                        continue;
                    }
                }

                PhaDoSvgCatalog.UpsertShapeWithId(
                    gp,
                    svgId,
                    style.CustomShapeSvg,
                    style.CustomShapeViewBoxWidth,
                    style.CustomShapeViewBoxHeight);
                return svgId;
            }
        }

        private string ShowSvgFrameNameInputDialog()
        {
            var prompt = new Window
            {
                Title = "Tên khung SVG",
                Width = 400,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = "Nhập tên khung (vd: khung_vang, KhungDong):",
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(label, 0);

            var nameBox = new TextBox { Margin = new Thickness(0, 8, 0, 12) };
            Grid.SetRow(nameBox, 1);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(buttons, 2);

            string acceptedName = null;
            var okBtn = new Button { Content = "Lưu", Width = 80, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            okBtn.Click += (s, e) =>
            {
                acceptedName = nameBox.Text?.Trim();
                prompt.DialogResult = true;
                prompt.Close();
            };
            var cancelBtn = new Button { Content = "Hủy", Width = 80, IsCancel = true };
            cancelBtn.Click += (s, e) =>
            {
                prompt.DialogResult = false;
                prompt.Close();
            };

            buttons.Children.Add(okBtn);
            buttons.Children.Add(cancelBtn);
            root.Children.Add(label);
            root.Children.Add(nameBox);
            root.Children.Add(buttons);
            prompt.Content = root;
            nameBox.Focus();

            bool? dlg = prompt.ShowDialog();
            return dlg == true ? acceptedName : null;
        }

        private void CommitSvgIdToFamilies(string svgId, PhaDoBoxStyle style, List<int> familyIds, GiaphaInfo gp)
        {
            foreach (int fid in familyIds)
            {
                SetFamilyPhaDoShapeSvgId(fid, svgId);

                if (!_phaDoBoxStyleByFamilyId.TryGetValue(fid, out var boxStyle))
                {
                    boxStyle = style.Clone();
                    _phaDoBoxStyleByFamilyId[fid] = boxStyle;
                }

                boxStyle.ShapeSvgId = svgId;
                PhaDoSvgCatalog.ResolveShapeIntoStyle(boxStyle, gp.SvgShapesById);
                ApplyBoxStyleToFamilyVisuals(fid);
            }
        }

        private void SyncPhaDoToolbarFromBoxStyle(int familyId)
        {
            var style = GetBoxStyleForFamily(familyId);
            double pt = style.Main?.FontPt ?? _phaDoCurrentOptions?.MainNameFontPt ?? 9;
        }

        private void RemoveFamilyBackgroundVisual(int familyId)
        {
            var toRemove = theCanvas.Children
                .OfType<FrameworkElement>()
                .Where(fe => fe.Tag is PhaDoBoxBackgroundTag t
                    && (t.Family?.familyInfo?.FamilyId ?? 0) == familyId)
                .Cast<UIElement>()
                .ToList();
            foreach (var el in toRemove)
            {
                theCanvas.Children.Remove(el);
            }
        }

        private void ReplaceFamilyBackgroundVisual(int familyId, PhaDoBoxStyle boxStyle)
        {
            var node = FindNodeByFamilyId(familyId);
            if (node?.Family == null)
            {
                return;
            }

            RemoveFamilyBackgroundVisual(familyId);

            double x = MmToPx(node.Xmm);
            double y = MmToPx(node.Ymm);
            double w = MmToPx(node.Metrics.WidthMm);
            double h = MmToPx(node.Metrics.HeightMm);

            string fillHex = null;
            if (boxStyle != null && !string.IsNullOrWhiteSpace(boxStyle.FillColorHex))
            {
                fillHex = boxStyle.FillColorHex;
            }

            FrameworkElement bg;
            if (boxStyle != null && boxStyle.HasCustomShape)
            {
                bg = PhaDoBoxSvgWpfRenderer.CreateBackgroundElement(
                    boxStyle.CustomShapeSvg,
                    boxStyle.CustomShapeViewBoxWidth,
                    boxStyle.CustomShapeViewBoxHeight,
                    w, h,
                    node.Family,
                    fillHex);
            }
            else
            {
                bg = PhaDoBoxSvgWpfRenderer.CreateDefaultRectangle(w, h, node.Family, fillHex);
            }

            Canvas.SetLeft(bg, x);
            Canvas.SetTop(bg, y);
            Panel.SetZIndex(bg, 10);
            theCanvas.Children.Add(bg);
        }

        private bool TryGetFamilyBackgroundBounds(int familyId, out double left, out double top, out double width, out double height)
        {
            left = top = width = height = 0;
            var bg = theCanvas.Children
                .OfType<FrameworkElement>()
                .FirstOrDefault(fe => fe.Tag is PhaDoBoxBackgroundTag t
                    && (t.Family?.familyInfo?.FamilyId ?? 0) == familyId);
            if (bg == null)
            {
                return false;
            }

            left = Canvas.GetLeft(bg);
            top = Canvas.GetTop(bg);
            if (double.IsNaN(left))
            {
                left = 0;
            }

            if (double.IsNaN(top))
            {
                top = 0;
            }

            width = double.IsNaN(bg.Width) ? bg.ActualWidth : bg.Width;
            height = double.IsNaN(bg.Height) ? bg.ActualHeight : bg.Height;
            if (width < 1 || height < 1)
            {
                bg.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                width = bg.DesiredSize.Width;
                height = bg.DesiredSize.Height;
            }

            return width > 0 && height > 0;
        }

        /// <summary>Áp dụng kiểu đã lưu lên visual một ô (nền SVG/rect + chữ chính/phụ theo tag).</summary>
        private void ApplyBoxStyleToFamilyVisuals(int familyId)
        {
            var boxStyle = GetBoxStyleForFamily(familyId);

            ReplaceFamilyBackgroundVisual(familyId, boxStyle);

            double dpi = _phaDoCurrentOptions?.PrintDpi ?? 96;
            string defaultFont = _phaDoCurrentOptions?.FontFamilyName ?? "Segoe UI";
            double defaultMainPt = _phaDoCurrentOptions?.MainNameFontPt ?? 9;
            double defaultSpousePt = _phaDoCurrentOptions?.SpouseFontPt ?? 7.5;

            foreach (var child in theCanvas.Children)
            {
                if (child is TextBlock tb)
                {
                    if (TryGetPersonTextRole(tb.Tag, familyId, out var role))
                    {
                        ApplyPersonTextStyle(tb, role == PhaDoPersonTextRole.Main ? boxStyle.Main : boxStyle.Spouse,
                            role == PhaDoPersonTextRole.Main ? defaultMainPt : defaultSpousePt,
                            defaultFont, dpi, role == PhaDoPersonTextRole.Main);
                    }
                }
                else if (child is StackPanel column)
                {
                    if (!TryGetPersonTextRole(column.Tag, familyId, out var role))
                    {
                        continue;
                    }

                    var personStyle = role == PhaDoPersonTextRole.Main ? boxStyle.Main : boxStyle.Spouse;
                    double defaultPt = role == PhaDoPersonTextRole.Main ? defaultMainPt : defaultSpousePt;
                    foreach (var line in column.Children.OfType<TextBlock>())
                    {
                        ApplyPersonTextStyle(line, personStyle, defaultPt, defaultFont, dpi,
                            role == PhaDoPersonTextRole.Main);
                    }
                }
            }
        }

        private static bool TryGetPersonTextRole(object tag, int familyId, out PhaDoPersonTextRole role)
        {
            role = PhaDoPersonTextRole.Main;
            if (tag is PhaDoBoxVisualTag visualTag
                && (visualTag.Family?.familyInfo?.FamilyId ?? 0) == familyId
                && visualTag.ElementKind == PhaDoBoxElementKind.Person)
            {
                role = visualTag.Role;
                return true;
            }

            return false;
        }

        private static void ApplyPersonTextStyle(
            TextBlock tb,
            PhaDoPersonTextStyle personStyle,
            double defaultPt,
            string defaultFontFamily,
            double dpi,
            bool boldByDefault)
        {
            string fontName = string.IsNullOrWhiteSpace(personStyle?.FontFamilyName)
                ? defaultFontFamily
                : personStyle.FontFamilyName;
            tb.FontFamily = new FontFamily(fontName);

            double pt = personStyle?.FontPt ?? defaultPt;
            tb.FontSize = pt * dpi / 72.0;
            tb.FontWeight = boldByDefault ? FontWeights.Bold : FontWeights.Normal;

            if (!string.IsNullOrWhiteSpace(personStyle?.ForegroundHex))
            {
                tb.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(personStyle.ForegroundHex));
            }
            else
            {
                tb.Foreground = Brushes.Black;
            }
        }

        private void RefreshAllBoxStylesOnCanvas()
        {
            foreach (int familyId in _phaDoBoxStyleByFamilyId.Keys.ToList())
            {
                ApplyBoxStyleToFamilyVisuals(familyId);
            }
        }

        private void RefreshAllPersonOffsetsOnCanvas()
        {
            if (_phaDoRenderedLayout?.Nodes == null)
            {
                return;
            }

            foreach (var node in _phaDoRenderedLayout.Nodes)
            {
                int familyId = node.Family?.familyInfo?.FamilyId ?? 0;
                if (familyId > 0)
                {
                    ApplyPersonOffsetsForFamily(familyId);
                }
            }
        }

        private static void MergeBoxStyleFromSession(PhaDoBoxStyle target, PhaDoBoxStyle from)
        {
            if (target == null || from == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(from.FillColorHex))
            {
                target.FillColorHex = from.FillColorHex;
            }

            if (!string.IsNullOrWhiteSpace(from.ShapeSvgId))
            {
                target.ShapeSvgId = from.ShapeSvgId;
            }

            if (!string.IsNullOrWhiteSpace(from.CustomShapeSvg))
            {
                target.CustomShapeSvg = from.CustomShapeSvg;
                target.CustomShapeViewBoxWidth = from.CustomShapeViewBoxWidth;
                target.CustomShapeViewBoxHeight = from.CustomShapeViewBoxHeight;
            }

            if (from.CustomWidthMm.HasValue)
            {
                target.CustomWidthMm = from.CustomWidthMm;
            }

            if (from.CustomHeightMm.HasValue)
            {
                target.CustomHeightMm = from.CustomHeightMm;
            }

            if (from.PersonOffsetsBySlot != null)
            {
                if (target.PersonOffsetsBySlot == null)
                {
                    target.PersonOffsetsBySlot = new Dictionary<int, PhaDoPersonLayoutOffset>();
                }

                foreach (var kv in from.PersonOffsetsBySlot)
                {
                    if (kv.Value == null)
                    {
                        continue;
                    }

                    target.PersonOffsetsBySlot[kv.Key] = new PhaDoPersonLayoutOffset
                    {
                        DeltaXmm = kv.Value.DeltaXmm,
                        DeltaYmm = kv.Value.DeltaYmm
                    };
                }
            }

            if (from.Main != null)
            {
                if (!string.IsNullOrWhiteSpace(from.Main.FontFamilyName))
                {
                    target.Main.FontFamilyName = from.Main.FontFamilyName;
                }

                if (from.Main.FontPt.HasValue)
                {
                    target.Main.FontPt = from.Main.FontPt;
                }

                if (!string.IsNullOrWhiteSpace(from.Main.ForegroundHex))
                {
                    target.Main.ForegroundHex = from.Main.ForegroundHex;
                }
            }

            if (from.Spouse != null)
            {
                if (!string.IsNullOrWhiteSpace(from.Spouse.FontFamilyName))
                {
                    target.Spouse.FontFamilyName = from.Spouse.FontFamilyName;
                }

                if (from.Spouse.FontPt.HasValue)
                {
                    target.Spouse.FontPt = from.Spouse.FontPt;
                }

                if (!string.IsNullOrWhiteSpace(from.Spouse.ForegroundHex))
                {
                    target.Spouse.ForegroundHex = from.Spouse.ForegroundHex;
                }
            }
        }

        private static void LoadPhaDoBoxStylesFromSession(
            PhaDoWorkspaceState phaDo,
            Dictionary<int, PhaDoBoxStyle> target)
        {
            target.Clear();
            if (phaDo == null)
            {
                return;
            }

            if (phaDo.BoxStyleByFamilyId != null)
            {
                foreach (var kv in phaDo.BoxStyleByFamilyId)
                {
                    if (kv.Value == null)
                    {
                        continue;
                    }

                    if (!target.TryGetValue(kv.Key, out var merged))
                    {
                        merged = new PhaDoBoxStyle();
                    }
                    else
                    {
                        merged = merged.Clone();
                    }

                    MergeBoxStyleFromSession(merged, kv.Value);
                    target[kv.Key] = merged;
                }
            }

            // Phiên bản cũ: gộp FontPt + Fill vào BoxStyle
            if (phaDo.FontPtByFamilyId != null)
            {
                foreach (var kv in phaDo.FontPtByFamilyId)
                {
                    if (!target.TryGetValue(kv.Key, out var style))
                    {
                        style = new PhaDoBoxStyle();
                        target[kv.Key] = style;
                    }

                    style.Main.FontPt = kv.Value;
                }
            }

            if (phaDo.FillColorHexByFamilyId != null)
            {
                foreach (var kv in phaDo.FillColorHexByFamilyId)
                {
                    if (string.IsNullOrWhiteSpace(kv.Value))
                    {
                        continue;
                    }

                    if (!target.TryGetValue(kv.Key, out var style))
                    {
                        style = new PhaDoBoxStyle();
                        target[kv.Key] = style;
                    }

                    style.FillColorHex = kv.Value;
                }
            }
        }

        private double MmToPx(double mm)
        {
            var dpi = _phaDoCurrentOptions?.PrintDpi ?? 96;
            return PrintUnits.MmToPixels(mm, dpi);
        }

        private double PxToMm(double px)
        {
            var dpi = _phaDoCurrentOptions?.PrintDpi ?? 96;
            return PrintUnits.PixelsToMm(px, dpi);
        }

        /// <summary>Dịch ngang mọi phần tử của 1 gia đình (giữ khoảng cách giữa các cột dọc).</summary>
        private void ShiftFamilyVisualsByDelta(int familyId, double deltaPx)
        {
            if (Math.Abs(deltaPx) < 0.001)
            {
                return;
            }

            foreach (var fe in theCanvas.Children.OfType<FrameworkElement>())
            {
                if (GetFamilyIdFromElementTag(fe.Tag) != familyId)
                {
                    continue;
                }

                double left = Canvas.GetLeft(fe);
                if (!double.IsNaN(left))
                {
                    Canvas.SetLeft(fe, left + deltaPx);
                }
            }

            if (_phaDoSelectedFamilyId == familyId)
            {
                DrawSelectionOverlay(familyId);
            }
        }

        /// <summary>Đồng bộ X layout + ô + đường nối cha/con khi kéo ô gia đình.</summary>
        private void ApplyDraggedFamilyX(int familyId, double newXmm)
        {
            var node = FindNodeByFamilyId(familyId);
            if (node == null)
            {
                return;
            }

            double deltaMm = newXmm - node.Xmm;
            if (Math.Abs(deltaMm) < 0.001)
            {
                return;
            }

            node.Xmm = newXmm;
            ShiftFamilyVisualsByDelta(familyId, MmToPx(deltaMm));

            if (_phaDoBaseXmmByFamilyId.TryGetValue(familyId, out double baseXmm))
            {
                _phaDoOffsetXmmByFamilyId[familyId] = newXmm - baseXmm;
            }

            int parentId = node.Family?.Parent?.familyInfo?.FamilyId ?? 0;
            if (parentId > 0)
            {
                UpdateConnectorVisualsForParent(parentId);
            }

            UpdateConnectorVisualsForParent(familyId);
        }

        private double GetPhaDoDragDeltaMm(Point currentCanvasPoint)
        {
            return GetPhaDoCanvasDeltaMm(currentCanvasPoint, _phaDoDragStartPoint).X;
        }

        private Point GetPhaDoCanvasDeltaMm(Point currentCanvasPoint, Point startCanvasPoint)
        {
            double dxPx = currentCanvasPoint.X - startCanvasPoint.X;
            double dyPx = currentCanvasPoint.Y - startCanvasPoint.Y;
            if (theCanvas.LayoutTransform is ScaleTransform scale && scale.ScaleX > 0.001)
            {
                dxPx /= scale.ScaleX;
                dyPx /= scale.ScaleY;
            }

            return new Point(PxToMm(dxPx), PxToMm(dyPx));
        }

        private void BeginPhaDoResize(MouseButtonEventArgs e, int familyId, PhaDoResizeCorner corner)
        {
            var node = FindNodeByFamilyId(familyId);
            if (node?.Metrics == null)
            {
                return;
            }

            SelectPhaDoBoxOutline(familyId);

            var measured = FamilyCardMetrics.Measure(node.Family, _phaDoCurrentOptions, _phaDoRenderedLayout.Dpi);
            _phaDoResizeMinWmm = _phaDoCurrentOptions?.CardMinWidthMm ?? 12;
            _phaDoResizeMinHmm = Math.Max(8, measured.HeightMm * 0.45);

            _phaDoIsResizing = true;
            _phaDoResizingFamilyId = familyId;
            _phaDoResizeCorner = corner;
            _phaDoResizeStartXmm = node.Xmm;
            _phaDoResizeStartYmm = node.Ymm;
            _phaDoResizeStartWmm = node.Metrics.WidthMm;
            _phaDoResizeStartHmm = node.Metrics.HeightMm;
            _phaDoDragStartPoint = e.GetPosition(theCanvas);
            theCanvas.CaptureMouse();
        }

        private void UpdatePhaDoResize(Point currentCanvasPoint)
        {
            if (!_phaDoIsResizing || _phaDoResizingFamilyId <= 0)
            {
                return;
            }

            var delta = GetPhaDoCanvasDeltaMm(currentCanvasPoint, _phaDoDragStartPoint);
            double newX = _phaDoResizeStartXmm;
            double newY = _phaDoResizeStartYmm;
            double newW = _phaDoResizeStartWmm;
            double newH = _phaDoResizeStartHmm;

            switch (_phaDoResizeCorner)
            {
                case PhaDoResizeCorner.BottomRight:
                    newW = Math.Max(_phaDoResizeMinWmm, _phaDoResizeStartWmm + delta.X);
                    newH = Math.Max(_phaDoResizeMinHmm, _phaDoResizeStartHmm + delta.Y);
                    break;
                case PhaDoResizeCorner.BottomLeft:
                    newW = Math.Max(_phaDoResizeMinWmm, _phaDoResizeStartWmm - delta.X);
                    newX = _phaDoResizeStartXmm + _phaDoResizeStartWmm - newW;
                    newH = Math.Max(_phaDoResizeMinHmm, _phaDoResizeStartHmm + delta.Y);
                    break;
                case PhaDoResizeCorner.TopRight:
                    newW = Math.Max(_phaDoResizeMinWmm, _phaDoResizeStartWmm + delta.X);
                    newH = Math.Max(_phaDoResizeMinHmm, _phaDoResizeStartHmm - delta.Y);
                    newY = _phaDoResizeStartYmm + _phaDoResizeStartHmm - newH;
                    break;
                case PhaDoResizeCorner.TopLeft:
                    newW = Math.Max(_phaDoResizeMinWmm, _phaDoResizeStartWmm - delta.X);
                    newH = Math.Max(_phaDoResizeMinHmm, _phaDoResizeStartHmm - delta.Y);
                    newX = _phaDoResizeStartXmm + _phaDoResizeStartWmm - newW;
                    newY = _phaDoResizeStartYmm + _phaDoResizeStartHmm - newH;
                    break;
            }

            ApplyResizedFamilyBox(_phaDoResizingFamilyId, newX, newY, newW, newH);
        }

        private void EndPhaDoResize()
        {
            if (!_phaDoIsResizing)
            {
                return;
            }

            _phaDoIsResizing = false;
            _phaDoResizingFamilyId = 0;
            theCanvas.ReleaseMouseCapture();
            theCanvas.Cursor = null;
            SaveWorkspaceSession();
        }

        private void ApplyResizedFamilyBox(int familyId, double xMm, double yMm, double widthMm, double heightMm)
        {
            var node = FindNodeByFamilyId(familyId);
            if (node == null)
            {
                return;
            }

            var style = GetBoxStyleForFamily(familyId);
            style.CustomWidthMm = widthMm;
            style.CustomHeightMm = heightMm;
            _phaDoBoxStyleByFamilyId[familyId] = style;

            node.Xmm = xMm;
            node.Ymm = yMm;

            if (_phaDoBaseXmmByFamilyId.TryGetValue(familyId, out double baseXmm))
            {
                _phaDoOffsetXmmByFamilyId[familyId] = xMm - baseXmm;
            }

            if (_phaDoBaseYmmByFamilyId.TryGetValue(familyId, out double baseYmm))
            {
                _phaDoOffsetYmmByFamilyId[familyId] = yMm - baseYmm;
            }

            RedrawPhaDoFamilyBox(familyId);

            int parentId = node.Family?.Parent?.familyInfo?.FamilyId ?? 0;
            if (parentId > 0)
            {
                UpdateConnectorVisualsForParent(parentId);
            }

            UpdateConnectorVisualsForParent(familyId);
            if (_phaDoSelectedFamilyId == familyId)
            {
                UpdatePhaDoSelectedBoxSizeStatus(familyId);
            }
        }

        public void ScheduleExpandGiaPhaTreeView()
        {
            ExpandGiaPhaTreeViewAll();
            Dispatcher.BeginInvoke(
                new Action(ExpandGiaPhaTreeViewAll),
                DispatcherPriority.Loaded);
        }

        private void ExpandGiaPhaTreeViewAll()
        {
            viewModel?.FamilyTree?.Family?.ExpandAll();
        }

        private void SelectFamilyInTreeView(FamilyViewModel family)
        {
            if (family == null || viewModel?.FamilyTree?.Family == null)
            {
                return;
            }

            viewModel.FamilyTree.Family.SelectFamily(family);

            Dispatcher.BeginInvoke(
                new Action(() => ScrollTreeViewToFamily(family)),
                DispatcherPriority.Loaded);
        }

        private void ScrollTreeViewToFamily(FamilyViewModel family)
        {
            if (treeViewGiaPha == null || family == null)
            {
                return;
            }

            var item = FindTreeViewItem(treeViewGiaPha, family);
            item?.BringIntoView();
        }

        private static TreeViewItem FindTreeViewItem(ItemsControl itemsControl, FamilyViewModel family)
        {
            if (itemsControl == null || family == null)
            {
                return null;
            }

            for (int i = 0; i < itemsControl.Items.Count; i++)
            {
                if (!(itemsControl.Items[i] is FamilyViewModel candidate))
                {
                    continue;
                }

                var container = itemsControl.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (candidate == family)
                {
                    return container;
                }

                if (container != null)
                {
                    if (!container.IsExpanded)
                    {
                        container.IsExpanded = true;
                    }

                    container.UpdateLayout();
                    var nested = FindTreeViewItem(container, family);
                    if (nested != null)
                    {
                        return nested;
                    }
                }
            }

            return null;
        }

        private void ClearSelectionOverlay()
        {
            var overlays = theCanvas.Children
                .OfType<FrameworkElement>()
                .Where(fe => Equals(fe.Tag, "__PhaDoSelectionOverlay") || fe.Tag is PhaDoResizeHandleTag)
                .ToList();
            foreach (var fe in overlays)
            {
                theCanvas.Children.Remove(fe);
            }

            ClearPersonSelectionHighlight();
        }

        private void RemoveFamilyVisualsFromCanvas(int familyId)
        {
            var toRemove = theCanvas.Children
                .OfType<FrameworkElement>()
                .Where(fe => GetFamilyIdFromElementTag(fe.Tag) == familyId)
                .Cast<UIElement>()
                .ToList();
            foreach (var el in toRemove)
            {
                theCanvas.Children.Remove(el);
            }
        }

        private void ApplyBoxStyleSizeOverride(GiaPhaPlacedNode node, PhaDoBoxStyle style)
        {
            if (node?.Metrics == null || style == null)
            {
                return;
            }

            node.Metrics.ApplySizeOverride(style.CustomWidthMm, style.CustomHeightMm);
        }

        private void ApplyCustomBoxSizesFromStyles(GiaPhaRenderResult result)
        {
            if (result?.Nodes == null)
            {
                return;
            }

            foreach (var node in result.Nodes)
            {
                int familyId = node.Family?.familyInfo?.FamilyId ?? 0;
                if (familyId <= 0)
                {
                    continue;
                }

                if (_phaDoBoxStyleByFamilyId.TryGetValue(familyId, out var style))
                {
                    ApplyBoxStyleSizeOverride(node, style);
                }
            }
        }

        /// <summary>Đo lại và vẽ lại một box (giữ vị trí X/Y), không render toàn canvas.</summary>
        private void RefreshSelectedPhaDoFamilyBox()
        {
            if (_phaDoSelectedFamilyId <= 0)
            {
                return;
            }

            RedrawPhaDoFamilyBox(_phaDoSelectedFamilyId);
        }

        private void RedrawPhaDoFamilyBox(int familyId)
        {
            if (familyId <= 0 || _phaDoRenderedLayout == null || _phaDoCurrentOptions == null)
            {
                return;
            }

            var node = FindNodeByFamilyId(familyId);
            if (node?.Family == null)
            {
                return;
            }

            node.Metrics = FamilyCardMetrics.Measure(node.Family, _phaDoCurrentOptions, _phaDoRenderedLayout.Dpi);
            ApplyBoxStyleSizeOverride(node, GetBoxStyleForFamily(familyId));

            RemoveFamilyVisualsFromCanvas(familyId);

            var renderer = new FamilyTreeCanvasRenderer(_phaDoRenderedLayout);
            renderer.DrawSingleCard(theCanvas, node);

            ApplyBoxStyleToFamilyVisuals(familyId);
            ApplyPersonOffsetsForFamily(familyId);

            if (_phaDoSelectedFamilyId == familyId)
            {
                DrawSelectionOverlay(familyId);
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (tabControl?.SelectedIndex != PhaDoTabIndex)
            {
                if (_phaDoImmersive)
                {
                    SetPhaDoImmersive(false);
                }

                return;
            }

            if (_phaDoRenderedLayout == null || _phaDoSelectedFamilyId <= 0)
            {
                return;
            }

            RefreshSelectedPhaDoFamilyBox();
        }

        private void DrawSelectionOverlay(int familyId)
        {
            ClearSelectionOverlay();
            if (!TryGetFamilyBackgroundBounds(familyId, out double left, out double top, out double boxW, out double boxH))
            {
                return;
            }

            const double pad = 2.5;
            const double handle = 8;
            var outline = new Rectangle
            {
                Width = Math.Max(1, boxW + pad * 2),
                Height = Math.Max(1, boxH + pad * 2),
                Stroke = Brushes.DodgerBlue,
                StrokeThickness = 1.3,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = Brushes.Transparent,
                IsHitTestVisible = false,
                Tag = "__PhaDoSelectionOverlay"
            };
            Canvas.SetLeft(outline, left - pad);
            Canvas.SetTop(outline, top - pad);
            Panel.SetZIndex(outline, 1000);
            theCanvas.Children.Add(outline);

            double x0 = left - pad - handle / 2;
            double y0 = top - pad - handle / 2;
            double x1 = left + boxW + pad - handle / 2;
            double y1 = top + boxH + pad - handle / 2;
            AddSelectionHandle(x0, y0, handle, familyId, PhaDoResizeCorner.TopLeft);
            AddSelectionHandle(x1, y0, handle, familyId, PhaDoResizeCorner.TopRight);
            AddSelectionHandle(x0, y1, handle, familyId, PhaDoResizeCorner.BottomLeft);
            AddSelectionHandle(x1, y1, handle, familyId, PhaDoResizeCorner.BottomRight);
            UpdatePersonSelectionHighlight();
        }

        private void AddSelectionHandle(double x, double y, double size, int familyId, PhaDoResizeCorner corner)
        {
            var r = new Rectangle
            {
                Width = size,
                Height = size,
                Fill = Brushes.White,
                Stroke = Brushes.DodgerBlue,
                StrokeThickness = 1.2,
                IsHitTestVisible = true,
                Tag = new PhaDoResizeHandleTag(familyId, corner),
                Cursor = GetResizeCursor(corner)
            };
            Canvas.SetLeft(r, x);
            Canvas.SetTop(r, y);
            Panel.SetZIndex(r, 1002);
            theCanvas.Children.Add(r);
        }

        private static Cursor GetResizeCursor(PhaDoResizeCorner corner)
        {
            switch (corner)
            {
                case PhaDoResizeCorner.TopLeft:
                case PhaDoResizeCorner.BottomRight:
                    return Cursors.SizeNWSE;
                case PhaDoResizeCorner.TopRight:
                case PhaDoResizeCorner.BottomLeft:
                    return Cursors.SizeNESW;
                default:
                    return Cursors.Arrow;
            }
        }

        private bool TryResolveResizeHandle(DependencyObject source, out int familyId, out PhaDoResizeCorner corner)
        {
            familyId = 0;
            corner = PhaDoResizeCorner.BottomRight;
            if (source == null)
            {
                return false;
            }

            DependencyObject cursor = source;
            while (cursor != null)
            {
                if (cursor is FrameworkElement fe && fe.Tag is PhaDoResizeHandleTag tag)
                {
                    familyId = tag.FamilyId;
                    corner = tag.Corner;
                    return familyId > 0;
                }

                cursor = VisualTreeHelper.GetParent(cursor);
            }

            return false;
        }


        private void UpdateConnectorVisualsForParent(int parentFamilyId)
        {
            if (_phaDoRenderedLayout == null || parentFamilyId <= 0)
            {
                return;
            }

            var parent = FindNodeByFamilyId(parentFamilyId);
            if (parent == null || parent.Family == null)
            {
                return;
            }

            var byFamily = _phaDoRenderedLayout.Nodes
                .Where(n => n.Family != null)
                .ToDictionary(n => n.Family, n => n);

            var childNodes = parent.Family.Children
                .Where(byFamily.ContainsKey)
                .Select(c => byFamily[c])
                .ToList();
            if (childNodes.Count == 0)
            {
                return;
            }

            double parentCx = parent.Xmm + parent.Metrics.WidthMm / 2.0;
            double parentBottom = parent.Ymm + parent.Metrics.HeightMm;
            double childTop = childNodes.Min(c => c.Ymm);
            double gap = childTop - parentBottom;
            if (gap < _phaDoCurrentOptions.BusLineGapMm)
            {
                gap = _phaDoCurrentOptions.BusLineGapMm;
            }
            double busY = parentBottom + gap * 0.5;

            double busLeft = childNodes.Min(c => c.Xmm + c.Metrics.WidthMm / 2.0);
            double busRight = childNodes.Max(c => c.Xmm + c.Metrics.WidthMm / 2.0);
            busLeft = Math.Min(busLeft, parentCx);
            busRight = Math.Max(busRight, parentCx);
            double span = busRight - busLeft;
            if (span < _phaDoCurrentOptions.MinBusSpanMm)
            {
                double mid = (busLeft + busRight) / 2.0;
                busLeft = mid - _phaDoCurrentOptions.MinBusSpanMm / 2.0;
                busRight = mid + _phaDoCurrentOptions.MinBusSpanMm / 2.0;
            }

            foreach (var line in theCanvas.Children.OfType<Line>())
            {
                var tag = line.Tag as GiaPhaCanvasConnectorTag;
                if (tag == null || tag.ParentFamilyId != parentFamilyId)
                {
                    continue;
                }

                switch (tag.LineKind)
                {
                    case GiaPhaCanvasConnectorLineKind.Trunk:
                        line.X1 = MmToPx(parentCx);
                        line.Y1 = MmToPx(parentBottom);
                        line.X2 = MmToPx(parentCx);
                        line.Y2 = MmToPx(busY);
                        break;
                    case GiaPhaCanvasConnectorLineKind.Bus:
                        line.X1 = MmToPx(busLeft);
                        line.Y1 = MmToPx(busY);
                        line.X2 = MmToPx(busRight);
                        line.Y2 = MmToPx(busY);
                        break;
                    case GiaPhaCanvasConnectorLineKind.Branch:
                        var child = FindNodeByFamilyId(tag.ChildFamilyId);
                        if (child == null)
                        {
                            continue;
                        }
                        double childCx = child.Xmm + child.Metrics.WidthMm / 2.0;
                        line.X1 = MmToPx(childCx);
                        line.Y1 = MmToPx(busY);
                        line.X2 = MmToPx(childCx);
                        line.Y2 = MmToPx(child.Ymm);
                        break;
                }
            }
        }

        public MainWindow()
        {
            

            ILoggerRepository repository = log4net.LogManager.GetRepository(Assembly.GetCallingAssembly());
            var fileInfo = new FileInfo(@"log4net.config");
            log4net.Config.XmlConfigurator.Configure(repository, fileInfo);
            //
            InitializeComponent();
            phaDoSubtreeListBox.ItemsSource = _phaDoRenderScopes;

            log.Info("Application started...SecurityProtocol ");
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 |
            SecurityProtocolType.Tls11 |
            SecurityProtocolType.Tls;
            log.Info("Application started...version END ");
            //
            try
            {
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                string path = config.AppSettings.Settings["defaultSaveFolder"].Value;
                FileInfo f = new FileInfo(path);
                string drive = System.IO.Path.GetPathRoot(f.FullName);
                // check drive exist
                if (Directory.Exists(drive))
                {
                    // OK
                }
                else
                {
                    // Else
                    config.AppSettings.Settings["defaultSaveFolder"].Value = path.Replace(drive, "c:\\");
                    config.Save(ConfigurationSaveMode.Modified);
                    //
                    //ConfigurationManager.AppSettings["defaultSaveFolder"] = path.Replace(drive, "c:\\");
                }
            }
            catch (Exception ex)
            {

            }

            string ver = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            log.Info("Application started...version " + ver);
            AutoUpdater.Start("http://download.vietnamgiapha.com/files/autoupdate.xml");
            InitEvents();
            this.Title = this.Title + " - " + ver;
            this.viewModel = new MainWindowViewModel(DialogCoordinator.Instance, this);
            this.DataContext = this.viewModel;
            ResetPhaDoRenderScopes(viewModel?.FamilyTree?.Family?.RootPerson);
            PersonGridView = CollectionViewSource.GetDefaultView(_personGridRows);
            PersonGridView.Filter = FilterPersonGridRow;
            EnsurePersonGridViewSort();

            DeletePersonFromFamilyClick = new RelayCommand(DeletePersonFromFamilyClickFunc);
            theCanvas.PreviewMouseLeftButtonDown += TheCanvas_PreviewMouseLeftButtonDown;
            theCanvas.PreviewMouseRightButtonDown += TheCanvas_PreviewMouseRightButtonDown;
            theCanvas.PreviewMouseDown += TheCanvas_PreviewMouseDown;
            theCanvas.PreviewMouseMove += TheCanvas_PreviewMouseMove;
            theCanvas.PreviewMouseLeftButtonUp += TheCanvas_PreviewMouseLeftButtonUp;
            theCanvas.PreviewMouseUp += TheCanvas_PreviewMouseUp;

        }

        /// <summary>Thay cả danh sách một lần — tránh N lần CollectionChanged làm DataGrid layout lại.</summary>
        private sealed class PersonGridRowCollection : ObservableCollection<PersonGridRow>
        {
            public void ReplaceAll(IReadOnlyList<PersonGridRow> items)
            {
                CheckReentrancy();
                Items.Clear();
                if (items != null)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        Items.Add(items[i]);
                    }
                }

                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        private sealed class PersonGridRow
        {
            public PersonInfo Person { get; set; }
            public FamilyViewModel Family { get; set; }
            public int Generation { get; set; }
            public int FamilyId { get; set; }
            public string FamilyGroupKey { get; set; }
            public string FamilyGroupLabel { get; set; }
            public int PersonOrderInFamily { get; set; }
            public bool IsMainInFamily => Person?.IsMainPerson == 1;

            public string RoleInFamily => IsMainInFamily ? "Chính" : "Phối";
            // Dòng phối để trống cột "Gia đình" cho mắt dễ tách 2 cột.
            public string FamilyGroupLabelForDisplay => IsMainInFamily ? (FamilyGroupLabel ?? "") : "";

            public string PersonId
            {
                get => Person?.MANS_ID ?? "";
                set { if (Person != null) Person.MANS_ID = value ?? ""; }
            }

            public string PersonName
            {
                get => Person?.MANS_NAME_HUY ?? "";
                set { if (Person != null) Person.MANS_NAME_HUY = value ?? ""; }
            }

            public string Gender
            {
                get => Person?.MANS_GENDER ?? "";
                set
                {
                    if (Person == null)
                    {
                        return;
                    }

                    string v = (value ?? "").Trim();
                    if (v != "Nam" && v != "Nữ")
                    {
                        return;
                    }
                    Person.MANS_GENDER = v;
                }
            }

            public string BirthDate
            {
                get => Person?.MANS_DOB ?? "";
                set { if (Person != null) Person.MANS_DOB = value ?? ""; }
            }

            public string DeathDate
            {
                get => Person?.MANS_DOD ?? "";
                set { if (Person != null) Person.MANS_DOD = value ?? ""; }
            }

            public string Place
            {
                get => Person?.MANS_WOD ?? "";
                set { if (Person != null) Person.MANS_WOD = value ?? ""; }
            }

            public string Detail
            {
                get => Person?.MANS_DETAIL ?? "";
                set { if (Person != null) Person.MANS_DETAIL = value ?? ""; }
            }
        }

        private Task RefreshPersonGridRowsCoreAsync()
        {
            var root = viewModel?.FamilyTree?.Family?.RootPerson;
            if (root == null)
            {
                InvalidatePersonGridCache();
                return Task.CompletedTask;
            }

            // Dữ liệu Person đã có trên cây TreeView — chỉ lập chỉ mục phẳng + bind grid (không đọc file).
            if (_personGridCachedRows != null && ReferenceEquals(_personGridCacheRoot, root))
            {
                BindPersonGridRows(_personGridCachedRows);
                viewModel?.AddUserAction("Đã nạp " + _personGridCachedRows.Count + " người (bộ nhớ đệm).");
                return Task.CompletedTask;
            }

            var newRows = BuildPersonGridRowsFromTree(root);
            _personGridCacheRoot = root;
            _personGridCachedRows = newRows;
            BindPersonGridRows(newRows);
            viewModel?.AddUserAction("Đã tải " + newRows.Count + " người vào danh sách.");
            return Task.CompletedTask;
        }

        /// <summary>Duyệt cây gia đình đã có trong RAM — tạo dòng grid (tham chiếu PersonInfo, không copy).</summary>
        private static List<PersonGridRow> BuildPersonGridRowsFromTree(FamilyViewModel root)
        {
            var newRows = new List<PersonGridRow>(4096);
            foreach (var family in EnumerateAllFamiliesStatic(root))
            {
                int level = family.familyInfo?.FamilyLevel ?? 0;
                var listPerson = family.ListPerson;
                if (listPerson == null || listPerson.Count == 0)
                {
                    continue;
                }

                int familyId = family.familyInfo?.FamilyId ?? 0;
                string groupLabel = BuildFamilyGroupLabel(family);
                string groupKey = "GD" + familyId;
                for (int i = 0; i < listPerson.Count; i++)
                {
                    var p = listPerson[i];
                    if (p == null)
                    {
                        continue;
                    }

                    newRows.Add(new PersonGridRow
                    {
                        Person = p,
                        Family = family,
                        Generation = level,
                        FamilyId = familyId,
                        FamilyGroupKey = groupKey,
                        FamilyGroupLabel = groupLabel,
                        PersonOrderInFamily = p.IsMainPerson == 1 ? 0 : (i + 1)
                    });
                }
            }

            return newRows;
        }

        private void BindPersonGridRows(IReadOnlyList<PersonGridRow> rows)
        {
            EnsurePersonGridViewSort();
            if (personDataGrid != null)
            {
                personDataGrid.SetCurrentValue(UIElement.IsEnabledProperty, false);
            }

            try
            {
                _personGridRows.ReplaceAll(rows);
                SafeRefreshPersonGridView();
            }
            finally
            {
                if (personDataGrid != null)
                {
                    personDataGrid.SetCurrentValue(UIElement.IsEnabledProperty, true);
                }
            }
        }

        private static IEnumerable<FamilyViewModel> EnumerateAllFamiliesStatic(FamilyViewModel root)
        {
            if (root == null)
            {
                yield break;
            }

            var stack = new Stack<FamilyViewModel>(256);
            stack.Push(root);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur == null)
                {
                    continue;
                }

                yield return cur;

                var children = cur.Children;
                if (children == null || children.Count == 0)
                {
                    continue;
                }

                for (int i = children.Count - 1; i >= 0; i--)
                {
                    stack.Push(children[i]);
                }
            }
        }

        private async Task RunPersonGridRefreshWithWaitDialogAsync()
        {
            if (_personGridIsRefreshing)
            {
                return;
            }

            _personGridIsRefreshing = true;
            if (personGridLoadButton != null)
            {
                personGridLoadButton.IsEnabled = false;
            }

            var progress = await this.ShowProgressAsync(
                "Đang tải danh sách người...",
                "Đang quét gia phả và lập danh sách...\n\nĐã chờ: 0 giây").ConfigureAwait(true);
            progress.SetIndeterminate();

            var sw = Stopwatch.StartNew();
            DispatcherTimer timer = null;
            try
            {
                timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                timer.Tick += (_, __) => UpdatePersonGridLoadProgressMessage(progress, sw, completed: false);
                timer.Start();

                await RefreshPersonGridRowsCoreAsync().ConfigureAwait(true);
            }
            finally
            {
                timer?.Stop();
                sw.Stop();
                UpdatePersonGridLoadProgressMessage(progress, sw, completed: true);
                try
                {
                    await Task.Delay(400).ConfigureAwait(true);
                    await progress.CloseAsync().ConfigureAwait(true);
                }
                catch
                {
                    // dialog có thể đã đóng
                }

                _personGridIsRefreshing = false;
                if (personGridLoadButton != null)
                {
                    personGridLoadButton.IsEnabled = true;
                }
            }
        }

        private static void UpdatePersonGridLoadProgressMessage(
            ProgressDialogController progress,
            Stopwatch sw,
            bool completed)
        {
            if (progress == null || sw == null)
            {
                return;
            }

            double seconds = sw.Elapsed.TotalSeconds;
            int wholeSeconds = Math.Max(0, (int)Math.Floor(seconds));
            string message;
            if (completed)
            {
                message = "Hoàn tất tải danh sách người.\n\nThời gian: "
                    + wholeSeconds + " giây";
                if (seconds - wholeSeconds >= 0.05)
                {
                    message += " (" + seconds.ToString("0.0") + " s)";
                }
            }
            else
            {
                message = "Đang quét gia phả và lập danh sách...\n\nĐã chờ: "
                    + wholeSeconds + " giây";
            }

            progress.SetMessage(message);
        }

        private static string BuildFamilyGroupLabel(FamilyViewModel family)
        {
            if (family == null)
            {
                return "Gia đình";
            }

            int level = family.familyInfo?.FamilyLevel ?? 0;
            int id = family.familyInfo?.FamilyId ?? 0;
            string label = family.Name0 ?? family.Name ?? "";
            return "Đời " + level + " · GD " + id + " · " + label;
        }

        private void EnsurePersonGridViewSort()
        {
            if (PersonGridView == null || _personGridViewSortConfigured)
            {
                return;
            }

            // Không dùng ICollectionView.GroupDescriptions — DataGrid + group làm mất virtualization, rất chậm.
            // Sắp xếp theo gia đình + cột "Gia đình" để nhìn cùng nhóm.
            PersonGridView.GroupDescriptions.Clear();
            PersonGridView.SortDescriptions.Clear();
            PersonGridView.SortDescriptions.Add(new SortDescription("Generation", ListSortDirection.Ascending));
            PersonGridView.SortDescriptions.Add(new SortDescription("FamilyId", ListSortDirection.Ascending));
            PersonGridView.SortDescriptions.Add(new SortDescription("PersonOrderInFamily", ListSortDirection.Ascending));
            PersonGridView.SortDescriptions.Add(new SortDescription("PersonName", ListSortDirection.Ascending));
            _personGridViewSortConfigured = true;
        }

        private bool FilterPersonGridRow(object obj)
        {
            if (!(obj is PersonGridRow row))
            {
                return false;
            }

            if (!_personGridShowAllInFamily && !row.IsMainInFamily)
            {
                return false;
            }

            string genFilter = personGenFilterBox?.Text?.Trim() ?? "";
            string nameFilter = personNameFilterBox?.Text?.Trim() ?? "";
            string dobFilter = personDobFilterBox?.Text?.Trim() ?? "";
            string dodFilter = personDodFilterBox?.Text?.Trim() ?? "";
            string genderFilter = (personGenderFilterBox?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Tất cả";

            bool Match(string value, string filter)
            {
                if (string.IsNullOrWhiteSpace(filter))
                {
                    return true;
                }

                return (value ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            bool generationOk = string.IsNullOrWhiteSpace(genFilter)
                || row.Generation.ToString().IndexOf(genFilter, StringComparison.OrdinalIgnoreCase) >= 0;
            bool genderOk = genderFilter == "Tất cả"
                || string.Equals(row.Gender ?? "", genderFilter, StringComparison.OrdinalIgnoreCase);

            if (!generationOk || !genderOk)
            {
                return false;
            }

            return Match(row.PersonName, nameFilter)
                && Match(row.BirthDate, dobFilter)
                && Match(row.DeathDate, dodFilter);
        }

        private void PersonFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            SafeRefreshPersonGridView();
        }

        private void PersonGenderFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SafeRefreshPersonGridView();
        }

        private void PersonFullModeCheckChanged(object sender, RoutedEventArgs e)
        {
            _personGridShowAllInFamily = personFullModeCheckBox?.IsChecked == true;
            SafeRefreshPersonGridView();
        }

        private async void RefreshPersonGrid_Click(object sender, RoutedEventArgs e)
        {
            await RunPersonGridRefreshWithWaitDialogAsync().ConfigureAwait(true);
        }

        private void PersonSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                FindAndFocusPersonBySearch(personSearchBox?.Text);
                e.Handled = true;
            }
        }

        private void PersonSearch_Click(object sender, RoutedEventArgs e)
        {
            FindAndFocusPersonBySearch(personSearchBox?.Text);
        }

        private void FindAndFocusPersonBySearch(string query)
        {
            string key = query?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            if (_personGridRows.Count == 0)
            {
                viewModel?.AddUserAction("Chưa có danh sách — bấm \"Tải danh sách\" trước.");
                return;
            }

            // Bấm Tìm nhiều lần => nhảy kết quả kế tiếp (next) trong view đang hiển thị.
            if (!string.Equals(_personSearchLastQuery ?? "", key, StringComparison.OrdinalIgnoreCase))
            {
                _personSearchLastQuery = key;
                _personSearchLastIndex = -1;
            }

            var viewRows = PersonGridView?.Cast<object>()?.OfType<PersonGridRow>()?.ToList()
                ?? new List<PersonGridRow>();
            if (viewRows.Count == 0)
            {
                viewModel?.AddUserAction("Danh sách đang trống (do filter).");
                return;
            }

            int startIndex = 0;
            if (personDataGrid?.SelectedItem is PersonGridRow selectedRow)
            {
                int selectedIndex = viewRows.IndexOf(selectedRow);
                if (selectedIndex >= 0)
                {
                    startIndex = (selectedIndex + 1) % viewRows.Count;
                }
            }
            else if (_personSearchLastIndex >= 0 && _personSearchLastIndex < viewRows.Count)
            {
                startIndex = (_personSearchLastIndex + 1) % viewRows.Count;
            }

            PersonGridRow found = null;
            int foundIndex = -1;
            for (int pass = 0; pass < 2 && found == null; pass++)
            {
                int from = (pass == 0) ? startIndex : 0;
                int to = (pass == 0) ? viewRows.Count : startIndex;
                for (int i = from; i < to; i++)
                {
                    var row = viewRows[i];
                    if ((row.PersonName ?? "").IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        found = row;
                        foundIndex = i;
                        break;
                    }
                }
            }

            if (found == null)
            {
                viewModel?.AddUserAction("Không tìm thấy: " + key);
                return;
            }

            _personSearchLastIndex = foundIndex;
            if (FocusPersonGridRow(found))
            {
                viewModel?.AddUserAction("Tìm thấy: " + found.PersonName + " (" + found.FamilyGroupLabel + ")");
            }
        }

        private bool FocusPersonGridRow(PersonGridRow row)
        {
            if (row == null || personDataGrid == null)
            {
                return false;
            }

            personDataGrid.SelectedItem = row;
            personDataGrid.ScrollIntoView(row);
            personDataGrid.UpdateLayout();

            var container = personDataGrid.ItemContainerGenerator.ContainerFromItem(row) as DataGridRow;
            if (container != null)
            {
                container.IsSelected = true;
                container.BringIntoView();
                container.Focus();
                return true;
            }

            // Hàng chưa materialize (nhóm lớn) — thử lại sau layout
            Dispatcher.BeginInvoke(new Action(() =>
            {
                personDataGrid.ScrollIntoView(row);
                personDataGrid.UpdateLayout();
                if (personDataGrid.ItemContainerGenerator.ContainerFromItem(row) is DataGridRow deferredRow)
                {
                    deferredRow.IsSelected = true;
                    deferredRow.BringIntoView();
                    deferredRow.Focus();
                }
            }), DispatcherPriority.Loaded);

            personDataGrid.Focus();
            return true;
        }

        private void PersonDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (!(e.Row?.Item is PersonGridRow row))
            {
                return;
            }

            if (row.IsMainInFamily)
            {
                e.Row.FontWeight = FontWeights.SemiBold;
            }

            // Tô cả nhánh con của gia đình đang chọn để nhìn quan hệ cha-con rõ hơn.
            if (ShouldHighlightPersonRow(row))
            {
                e.Row.Background = PersonFamilyHighlightBrush;
                e.Row.BorderBrush = PersonFamilyBorderBrush;
                e.Row.BorderThickness = new Thickness(1);
                e.Row.Foreground = Brushes.DarkBlue;
            }
            else
            {
                e.Row.ClearValue(Control.BackgroundProperty);
                e.Row.ClearValue(Control.BorderBrushProperty);
                e.Row.ClearValue(Control.BorderThicknessProperty);
                e.Row.ClearValue(Control.ForegroundProperty);
            }

            int index = e.Row.GetIndex();
            if (index > 0
                && personDataGrid?.Items[index - 1] is PersonGridRow prev
                && prev.FamilyId != row.FamilyId)
            {
                e.Row.BorderBrush = PersonFamilyBorderBrush;
                e.Row.BorderThickness = new Thickness(0, 2, 0, 0);
            }
        }

        private void PersonDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_personGridIsSelectingFamily || personDataGrid == null)
            {
                return;
            }

            if (!(personDataGrid.SelectedItem is PersonGridRow selectedRow))
            {
                _personGridSelectedFamilyId = 0;
                _personGridSelectedFamilyRoot = null;
                UpdatePersonGridRowHighlighting();
                return;
            }

            int familyId = selectedRow.FamilyId;
            if (familyId <= 0)
            {
                _personGridSelectedFamilyId = 0;
                _personGridSelectedFamilyRoot = null;
                UpdatePersonGridRowHighlighting();
                return;
            }

            _personGridSelectedFamilyId = familyId;
            _personGridSelectedFamilyRoot = selectedRow.Family;

            // Auto select tất cả người cùng gia đình trong view đang hiển thị (đã filter)
            _personGridIsSelectingFamily = true;
            try
            {
                personDataGrid.SelectedItems.Clear();
                foreach (var item in personDataGrid.Items)
                {
                    if (item is PersonGridRow row && row.FamilyId == familyId)
                    {
                        personDataGrid.SelectedItems.Add(row);
                    }
                }
            }
            finally
            {
                _personGridIsSelectingFamily = false;
            }

            UpdatePersonGridRowHighlighting();
        }

        private void UpdatePersonGridRowHighlighting()
        {
            if (personDataGrid == null)
            {
                return;
            }

            // Chỉ cập nhật các row đang được materialize; row khác sẽ được tô qua LoadingRow khi cuộn.
            personDataGrid.UpdateLayout();
            for (int i = 0; i < personDataGrid.Items.Count; i++)
            {
                var item = personDataGrid.Items[i];
                if (!(item is PersonGridRow row))
                {
                    continue;
                }

                if (!(personDataGrid.ItemContainerGenerator.ContainerFromIndex(i) is DataGridRow container))
                {
                    continue;
                }

                if (ShouldHighlightPersonRow(row))
                {
                    container.Background = PersonFamilyHighlightBrush;
                    container.BorderBrush = PersonFamilyBorderBrush;
                    container.BorderThickness = new Thickness(1);
                    container.Foreground = Brushes.DarkBlue;
                }
                else
                {
                    container.ClearValue(Control.BackgroundProperty);
                    container.ClearValue(Control.BorderBrushProperty);
                    container.ClearValue(Control.BorderThicknessProperty);
                    container.ClearValue(Control.ForegroundProperty);
                }
            }
        }

        private bool ShouldHighlightPersonRow(PersonGridRow row)
        {
            if (row?.Family == null || _personGridSelectedFamilyRoot == null)
            {
                return false;
            }

            FamilyViewModel cursor = row.Family;
            while (cursor != null)
            {
                if (ReferenceEquals(cursor, _personGridSelectedFamilyRoot))
                {
                    return true;
                }

                cursor = cursor.Parent;
            }

            return false;
        }

        private void PersonDataGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (personDataGrid == null)
            {
                return;
            }

            DependencyObject cursor = e.OriginalSource as DependencyObject;
            while (cursor != null && !(cursor is DataGridRow))
            {
                cursor = VisualTreeHelper.GetParent(cursor);
            }

            if (cursor is DataGridRow row && row.Item is PersonGridRow personRow)
            {
                personDataGrid.SelectedItem = personRow;
                row.Focus();
            }
        }

        private async void PersonViewMiniPhaDo_Click(object sender, RoutedEventArgs e)
        {
            var selectedRow = personDataGrid?.SelectedItem as PersonGridRow;
            var selectedFamily = selectedRow?.Family;
            if (selectedFamily == null)
            {
                await this.ShowMessageAsync("Phả đồ nhỏ", "Hãy chọn một người trước.").ConfigureAwait(true);
                return;
            }

            var parentFamily = selectedFamily.Parent ?? selectedFamily;

            var progress = await this.ShowProgressAsync(
                "Phả đồ nhỏ...",
                "Đang dựng sơ đồ nhanh từ đời cha...\n\nĐã chờ: 0 giây").ConfigureAwait(true);
            progress.SetIndeterminate();
            var sw = Stopwatch.StartNew();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, __) => progress.SetMessage(
                "Đang dựng sơ đồ nhanh từ đời cha...\n\nĐã chờ: " + (int)sw.Elapsed.TotalSeconds + " giây");
            timer.Start();

            try
            {
                GiaPhaRenderResult layout = await ComputePhaDoLayoutSnapshotAsync(parentFamily).ConfigureAwait(true);
                if (!TryCreateFamilyNodeBlock(layout, parentFamily, out var parentBlock))
                {
                    await this.ShowMessageAsync("Phả đồ nhỏ", "Không lấy được layout của gia đình cha.").ConfigureAwait(true);
                    return;
                }

                if (!TryCreateFamilyNodeBlock(layout, selectedFamily, out var currentBlock))
                {
                    await this.ShowMessageAsync("Phả đồ nhỏ", "Không lấy được layout của gia đình hiện tại.").ConfigureAwait(true);
                    return;
                }

                var childBlocks = new List<PhaDoSubtreeBranchBlock>();
                if (selectedFamily.Children != null)
                {
                    foreach (var child in selectedFamily.Children)
                    {
                        if (TryCreateFamilyNodeBlock(layout, child, out var childBlock))
                        {
                            childBlocks.Add(childBlock);
                        }
                    }
                }

                string report = BuildMiniPhaDoReport(selectedFamily, parentFamily, layout, childBlocks.Count);

                var dlg = new PhaDoSubtreeMapDialog(dpi: 96)
                {
                    Owner = this,
                    Title = "Phả đồ nhỏ (đời cha)"
                };
                dlg.SetFocusedChainContent(report, parentBlock, currentBlock, childBlocks);

                timer.Stop();
                sw.Stop();
                try { await progress.CloseAsync().ConfigureAwait(true); } catch { }
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                log.Warn("Lỗi xem phả đồ nhỏ tab Người.", ex);
                await this.ShowMessageAsync("Phả đồ nhỏ", "Lỗi: " + ex.Message).ConfigureAwait(true);
            }
            finally
            {
                timer.Stop();
                sw.Stop();
                if (progress != null && progress.IsOpen)
                {
                    try { await progress.CloseAsync().ConfigureAwait(true); } catch { }
                }
            }
        }

        private static string BuildMiniPhaDoReport(
            FamilyViewModel selectedFamily,
            FamilyViewModel rootFamily,
            GiaPhaRenderResult layout,
            int branchCount)
        {
            int selectedId = selectedFamily?.familyInfo?.FamilyId ?? 0;
            int rootId = rootFamily?.familyInfo?.FamilyId ?? 0;
            string selectedName = selectedFamily?.Name0 ?? selectedFamily?.Name ?? "(không tên)";
            string rootName = rootFamily?.Name0 ?? rootFamily?.Name ?? "(không tên)";
            int nodeCount = layout?.Nodes?.Count ?? 0;
            return "Phả đồ nhỏ từ đời cha của gia đình đang chọn\n"
                + "- Gia đình chọn: GD " + selectedId + " · " + selectedName + "\n"
                + "- Gốc hiển thị: GD " + rootId + " · " + rootName + "\n"
                + "- Phạm vi vẽ: Cha -> Hiện tại -> Con trực tiếp\n"
                + "- Số gia đình trong phạm vi: " + nodeCount + "\n"
                + "- Số gia đình con trực tiếp: " + branchCount;
        }

        private static bool TryCreateFamilyNodeBlock(
            GiaPhaRenderResult layout,
            FamilyViewModel family,
            out PhaDoSubtreeBranchBlock block)
        {
            block = null;
            int familyId = family?.familyInfo?.FamilyId ?? 0;
            if (layout?.Nodes == null || familyId <= 0)
            {
                return false;
            }

            var node = layout.Nodes.FirstOrDefault(n => (n?.Family?.familyInfo?.FamilyId ?? 0) == familyId);
            if (node?.Metrics == null)
            {
                return false;
            }

            block = CreateBranchBlock(
                family,
                node.Xmm,
                node.Ymm,
                node.Xmm + node.Metrics.WidthMm,
                node.Ymm + node.Metrics.HeightMm,
                nodeCount: 1);
            return true;
        }

        private void PersonDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit)
            {
                return;
            }

            if (e.Row?.Item is PersonGridRow row)
            {
                row.FamilyGroupLabel = BuildFamilyGroupLabel(row.Family);
            }

            SafeRefreshPersonGridView();
        }
        public void UpdateHtmlGiaPha()
        {
            if (viewModel != null)
            {
                htmlEditorTocUoc.ContentHtml = viewModel.FamilyTree.Tocuoc;
                htmlEditorPhaKy.ContentHtml = viewModel.FamilyTree.PhaKy;
                htmlEditorHuongHoa.ContentHtml = viewModel.FamilyTree.HuongHoa;
                htmlEditorThuyto.ContentHtml = viewModel.FamilyTree.ThuyTo;

                if (viewModel.FamilyTree.GP.FileName.Length == 0)
                {
                    string defaultSaveFolder = ConfigurationManager.AppSettings["defaultSaveFolder"];
                    //viewModel.FamilyTree.GP.FileName = defaultSaveFolder +"\\"+viewModel.FamilyTree.GP.GiaphaName.Replace(" ", "_") + "_" + ".json";
                    viewModel.FamilyTree.GP.FileName = defaultSaveFolder + "\\" + viewModel.FamilyTree.GP.Username.Replace(" ", "_") + "_" + ".json";
                }
            }
        }
        void InitEvents()
        {
            htmlEditorPhaKy.DocumentReady += HtmlEditorPhaKy_DocumentReady;
            htmlEditorHuongHoa.DocumentReady += HtmlEditorHuongHoa_DocumentReady;
            htmlEditorThuyto.DocumentReady += HtmlEditorThuyto_DocumentReady;
            htmlEditorTocUoc.DocumentReady += HtmlEditorTocUoc_DocumentReady;
        }
        private void htmlEditorPhaKy_LostFocus(object sender, RoutedEventArgs e)
        {
            viewModel.FamilyTree.PhaKy = htmlEditorPhaKy.ContentHtml;
        }
        private void htmlEditorHuongHoa_LostFocus(object sender, RoutedEventArgs e)
        {
            viewModel.FamilyTree.HuongHoa    = htmlEditorHuongHoa.ContentHtml;
        }
        private void htmlEditorThuyto_LostFocus(object sender, RoutedEventArgs e)
        {
            viewModel.FamilyTree.ThuyTo = htmlEditorThuyto.ContentHtml;
        }
        private void htmlEditorTocUoc_LostFocus(object sender, RoutedEventArgs e)
        {
            viewModel.FamilyTree.Tocuoc = htmlEditorTocUoc.ContentHtml;
        }

        private void HtmlEditorTocUoc_DocumentReady(object sender, RoutedEventArgs e)
        {
            if (viewModel != null)
            {
                htmlEditorTocUoc.ContentHtml = viewModel.FamilyTree.Tocuoc;
            }
            else
            {
                htmlEditorTocUoc.ContentHtml = "";
            }
        }

        private void HtmlEditorThuyto_DocumentReady(object sender, RoutedEventArgs e)
        {
            if (viewModel != null)
            {
                htmlEditorThuyto.ContentHtml = viewModel.FamilyTree.ThuyTo;
            }
            else
            {
                htmlEditorThuyto.ContentHtml = "";
            }
        }

        private void HtmlEditorHuongHoa_DocumentReady(object sender, RoutedEventArgs e)
        {
            if (viewModel != null)
            {
                htmlEditorHuongHoa.ContentHtml = viewModel.FamilyTree.HuongHoa;
            }
            else
            {
                htmlEditorHuongHoa.ContentHtml = "";
            }
        }

        private void HtmlEditorPhaKy_DocumentReady(object sender, RoutedEventArgs e)
        {
            if (viewModel != null)
            {
                htmlEditorPhaKy.ContentHtml = viewModel.FamilyTree.PhaKy;
            }
            else
            {
                htmlEditorPhaKy.ContentHtml = "";
            }
        }

        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (!_isRestoringWorkspace
                    && viewModel?.FamilyTree?.GP != null
                    && !string.IsNullOrWhiteSpace(viewModel.FamilyTree.GP.FileName))
                {
                    viewModel.SaveFileCommandFunc();
                }

                SaveWorkspaceSession();
            }
            catch (Exception ex)
            {
                log.Warn("Lỗi khi lưu session lúc thoát app.", ex);
            }
        }

        private async void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await TryRestoreWorkspaceSessionAsync().ConfigureAwait(true);
            ScheduleExpandGiaPhaTreeView();
        }

        public void ResetPhaDoWorkspaceState()
        {
            _phaDoRenderedLayout = null;
            _phaDoOffsetXmmByFamilyId.Clear();
            _phaDoOffsetYmmByFamilyId.Clear();
            _phaDoBaseXmmByFamilyId.Clear();
            _phaDoBaseYmmByFamilyId.Clear();
            _phaDoBoxStyleByFamilyId.Clear();
            _phaDoIsResizing = false;
            _phaDoResizingFamilyId = 0;
            _phaDoSelectedFamilyId = 0;
            _phaDoSelectedPersonSlot = -1;
            _phaDoDraggingFamilyId = 0;
            _phaDoIsDraggingPerson = false;
            ClearSelectionOverlay();
            UpdatePhaDoSelectedBoxSizeStatus(0);
        }

        private void CapturePhaDoBaseLayout(GiaPhaRenderResult autoLayout)
        {
            _phaDoBaseXmmByFamilyId.Clear();
            _phaDoBaseYmmByFamilyId.Clear();
            if (autoLayout?.Nodes == null)
            {
                return;
            }

            foreach (var node in autoLayout.Nodes)
            {
                int familyId = node.Family?.familyInfo?.FamilyId ?? 0;
                if (familyId > 0)
                {
                    _phaDoBaseXmmByFamilyId[familyId] = node.Xmm;
                    _phaDoBaseYmmByFamilyId[familyId] = node.Ymm;
                }
            }
        }

        private void SaveWorkspaceSession()
        {
            var session = new AppWorkspaceSession
            {
                DataFilePath = viewModel?.FamilyTree?.GP?.FileName ?? "",
                SelectedTabIndex = tabControl?.SelectedIndex ?? 0,
                PhaDo = new PhaDoWorkspaceState
                {
                    CardLayoutIndex = GetPhaDoCardLayoutListIndex(),
                    Zoom = _phaDoZoom,
                    SelectedFamilyId = _phaDoSelectedFamilyId,
                    IsRendered = _phaDoRenderedLayout != null,
                    ScrollHorizontal = phaDoScrollViewer?.HorizontalOffset ?? 0,
                    ScrollVertical = phaDoScrollViewer?.VerticalOffset ?? 0,
                    OffsetXmmByFamilyId = new Dictionary<int, double>(_phaDoOffsetXmmByFamilyId),
                    OffsetYmmByFamilyId = new Dictionary<int, double>(_phaDoOffsetYmmByFamilyId),
                    BoxStyleByFamilyId = _phaDoBoxStyleByFamilyId.ToDictionary(
                        kv => kv.Key,
                        kv => kv.Value.CloneForSession()),
                    TitleStyle = _phaDoTitleStyle?.CloneForSession()
                }
            };
            AppWorkspaceSessionStore.Save(session);
        }

        private async Task TryRestoreWorkspaceSessionAsync()
        {
            var session = AppWorkspaceSessionStore.Load();
            if (session == null)
            {
                return;
            }

            _isRestoringWorkspace = true;
            try
            {
                if (!string.IsNullOrWhiteSpace(session.DataFilePath) && File.Exists(session.DataFilePath))
                {
                    GiaphaInfo loadedGiaPha = null;
                    GiaphaInfo gp = await LoadGiaPhaFromJsonWithProgressAsync(
                        session.DataFilePath,
                        "Đang khôi phục phiên làm việc...",
                        "Đang đọc dữ liệu gia phả từ session...\n\nĐã chờ: 0 giây",
                        async loaded =>
                        {
                            loaded.FileName = session.DataFilePath;
                            loadedGiaPha = loaded;
                            await viewModel.LoadGiaPhaForSessionRestoreAsync(loaded).ConfigureAwait(true);
                        }).ConfigureAwait(true);
                    if (gp != null)
                    {
                        if (loadedGiaPha == null)
                        {
                            gp.FileName = session.DataFilePath;
                            await viewModel.LoadGiaPhaForSessionRestoreAsync(gp).ConfigureAwait(true);
                        }
                        log.Info("Khôi phục file: " + session.DataFilePath);
                    }
                }

                if (tabControl != null
                    && session.SelectedTabIndex >= 0
                    && session.SelectedTabIndex < tabControl.Items.Count)
                {
                    tabControl.SelectedIndex = session.SelectedTabIndex;
                }

                var phaDo = session.PhaDo;
                if (phaDo == null)
                {
                    return;
                }

                SetPhaDoCardLayoutIndex(ResolvePhaDoCardLayoutIndexFromSession(phaDo));

                _phaDoOffsetXmmByFamilyId.Clear();
                if (phaDo.OffsetXmmByFamilyId != null)
                {
                    foreach (var kv in phaDo.OffsetXmmByFamilyId)
                    {
                        if (Math.Abs(kv.Value) > 0.001)
                        {
                            _phaDoOffsetXmmByFamilyId[kv.Key] = kv.Value;
                        }
                    }
                }

                _phaDoOffsetYmmByFamilyId.Clear();
                if (phaDo.OffsetYmmByFamilyId != null)
                {
                    foreach (var kv in phaDo.OffsetYmmByFamilyId)
                    {
                        if (Math.Abs(kv.Value) > 0.001)
                        {
                            _phaDoOffsetYmmByFamilyId[kv.Key] = kv.Value;
                        }
                    }
                }

                LoadPhaDoBoxStylesFromSession(phaDo, _phaDoBoxStyleByFamilyId);
                if (phaDo.TitleStyle != null)
                {
                    _phaDoTitleStyle = phaDo.TitleStyle.Clone();
                }

                SyncPhaDoBoxStylesFromGiaPhaFile();
                MigrateAndResolveBoxStylesFromSession();

                // Không auto vẽ phả đồ khi mở app (phả đồ nặng). Chỉ khôi phục trạng thái đã lưu,
                // người dùng bấm nút "Vẽ" khi cần.
                if (phaDo != null)
                {
                    if (phaDo.Zoom > 0.01)
                    {
                        SetPhaDoZoom(phaDo.Zoom);
                    }

                    if (phaDo.SelectedFamilyId > 0)
                    {
                        _phaDoSelectedFamilyId = phaDo.SelectedFamilyId;
                        UpdatePhaDoSelectedBoxSizeStatus(_phaDoSelectedFamilyId);
                    }

                    // Scroll chỉ áp dụng được sau khi đã vẽ (ScrollViewer có nội dung).
                    // Giữ lại trong session; khi bấm Vẽ sẽ render xong rồi user kéo tới vị trí mong muốn.

                    if (phaDo.IsRendered)
                    {
                        viewModel.AddUserAction("Đã khôi phục layout phả đồ (chưa vẽ). Bấm \"Vẽ\" để hiển thị.");
                    }
                }
            }
            catch (Exception ex)
            {
                log.Warn("Không khôi phục được workspace session.", ex);
            }
            finally
            {
                _isRestoringWorkspace = false;
            }
        }

        private async Task<GiaPhaRenderResult> RunPhaDoRenderWithWaitDialogAsync(
            bool resetZoom,
            bool resetScroll,
            FamilyViewModel rootOverride = null,
            int maxGenerationInclusive = int.MaxValue)
        {
            var progress = await this.ShowProgressAsync(
                "Đợi vẽ phả đồ...",
                "Đang tính toán layout và vẽ lên canvas...\n\nĐã chờ: 0 giây").ConfigureAwait(true);
            progress.SetIndeterminate();

            var sw = Stopwatch.StartNew();
            DispatcherTimer timer = null;
            try
            {
                timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                timer.Tick += (_, __) => UpdatePhaDoRenderProgressMessage(progress, sw, completed: false);
                timer.Start();

                return await RenderPhaDoCoreAsync(
                    resetZoom,
                    resetScroll,
                    rootOverride,
                    maxGenerationInclusive).ConfigureAwait(true);
            }
            finally
            {
                timer?.Stop();
                sw.Stop();
                UpdatePhaDoRenderProgressMessage(progress, sw, completed: true);
                try
                {
                    await Task.Delay(400).ConfigureAwait(true);
                    await progress.CloseAsync().ConfigureAwait(true);
                }
                catch
                {
                    // dialog có thể đã đóng
                }
            }
        }

        public async Task<GiaphaInfo> LoadGiaPhaFromJsonWithProgressAsync(
            string filePath,
            string title = "Đang mở file gia phả...",
            string message = "Đang đọc dữ liệu JSON...\n\nĐã chờ: 0 giây",
            Func<GiaphaInfo, Task> afterLoadAsync = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return null;
            }

            var progress = await this.ShowProgressAsync(title, message).ConfigureAwait(true);
            progress.SetIndeterminate();

            var sw = Stopwatch.StartNew();
            DispatcherTimer timer = null;
            string phaseText = "Đang đọc dữ liệu JSON...";
            try
            {
                timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                timer.Tick += (_, __) =>
                {
                    progress.SetMessage(phaseText + "\n\nĐã chờ: "
                        + (int)sw.Elapsed.TotalSeconds + " giây");
                };
                timer.Start();

                // Parse file lớn ở background để UI thread vẫn bơm message.
                var gp = await Task.Run(() => Database.FromJson(filePath)).ConfigureAwait(true);
                if (gp != null && afterLoadAsync != null)
                {
                    // Chỉ đóng progress sau khi dựng xong cây/binding để tránh cảm giác đóng sớm.
                    phaseText = "Đang dựng dữ liệu gia phả...";
                    progress.SetMessage(phaseText + "\n\nĐã chờ: " + (int)sw.Elapsed.TotalSeconds + " giây");
                    await afterLoadAsync(gp).ConfigureAwait(true);
                }

                return gp;
            }
            finally
            {
                timer?.Stop();
                sw.Stop();
                try
                {
                    await progress.CloseAsync().ConfigureAwait(true);
                }
                catch
                {
                    // dialog có thể đã đóng
                }
            }
        }

        public async Task<GiaphaInfo> LoadGiaPhaFromMessagePackWithProgressAsync(
            string filePath,
            string title = "Đang mở file MessagePack...",
            string message = "Đang đọc dữ liệu MessagePack...\n\nĐã chờ: 0 giây",
            Func<GiaphaInfo, Task> afterLoadAsync = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return null;
            }

            var progress = await this.ShowProgressAsync(title, message).ConfigureAwait(true);
            progress.SetIndeterminate();

            var sw = Stopwatch.StartNew();
            DispatcherTimer timer = null;
            string phaseText = "Đang đọc dữ liệu MessagePack...";
            try
            {
                timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                timer.Tick += (_, __) =>
                {
                    progress.SetMessage(phaseText + "\n\nĐã chờ: "
                        + (int)sw.Elapsed.TotalSeconds + " giây");
                };
                timer.Start();

                // File .msgpack đang đóng gói chuỗi JSON hiện tại để giảm thời gian parse text trực tiếp từ disk.
                var gp = await Task.Run(() =>
                {
                    byte[] bytes = File.ReadAllBytes(filePath);
                    // Ưu tiên đọc object MessagePack thật; vẫn fallback file cũ đang lưu dạng string JSON.
                    try
                    {
                        var payload = MessagePackSerializer.Deserialize<MsgPackGiaPhaPayload>(bytes, MsgPackOptions);
                        if (payload?.FamilyRoot != null)
                        {
                            return ConvertFromMsgPackPayload(payload);
                        }
                    }
                    catch
                    {
                        // Fallback file msgpack thế hệ đầu (lưu chuỗi json).
                    }

                    string json = MessagePackSerializer.Deserialize<string>(bytes, MsgPackOptions);
                    string jsonString = "{\"code\":0,\"msg\":\" \", \"data\":" + json + "}";
                    JsonObject jsonObject = (JsonObject)JsonObject.Parse(jsonString);
                    return Database.ParseJsonGiaPha(jsonObject);
                }).ConfigureAwait(true);

                if (gp != null && afterLoadAsync != null)
                {
                    phaseText = "Đang dựng dữ liệu gia phả...";
                    progress.SetMessage(phaseText + "\n\nĐã chờ: " + (int)sw.Elapsed.TotalSeconds + " giây");
                    await afterLoadAsync(gp).ConfigureAwait(true);
                }

                return gp;
            }
            finally
            {
                timer?.Stop();
                sw.Stop();
                try
                {
                    await progress.CloseAsync().ConfigureAwait(true);
                }
                catch
                {
                    // dialog có thể đã đóng
                }
            }
        }

        public sealed class MsgPackGiaPhaPayload
        {
            public int GiaphaId { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public string GiaphaName { get; set; }
            public string GiaphaNameRoot { get; set; }
            public string PhaKy { get; set; }
            public string Tocuoc { get; set; }
            public string ThuyTo { get; set; }
            public string HuongHoa { get; set; }
            public string RF_OTAI { get; set; }
            public string RF_DAYS { get; set; }
            public string RF_CHANNGON { get; set; }
            public MsgPackFamilyPayload FamilyRoot { get; set; }
            public Dictionary<string, PhaDoSvgShape> SvgShapesById { get; set; }
        }

        public sealed class MsgPackFamilyPayload
        {
            public int FamilyId { get; set; }
            public int FamilyUp { get; set; }
            public int FamilyOrder { get; set; }
            public int FamilyLevel { get; set; }
            public int FamilyNew { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public string PhaDoShapeSvgId { get; set; }
            public List<MsgPackPersonPayload> Persons { get; set; }
            public List<MsgPackFamilyPayload> Children { get; set; }
        }

        public sealed class MsgPackPersonPayload
        {
            public int IsMainPerson { get; set; }
            public string MANS_NAME_HUY { get; set; }
            public string MANS_NAME_TU { get; set; }
            public string MANS_NAME_THUONG { get; set; }
            public string MANS_NAME_THUY { get; set; }
            public string MANS_ID { get; set; }
            public string fid { get; set; }
            public string MANS_GENDER { get; set; }
            public string MANS_DOB { get; set; }
            public string MANS_DOD { get; set; }
            public string MANS_WOD { get; set; }
            public string MANS_DETAIL { get; set; }
            public string MANS_CONTHUMAY { get; set; }
        }

        /// <summary>
        /// Chuẩn hóa object runtime sang payload thuần dữ liệu để MessagePack không dính vòng tham chiếu UI.
        /// </summary>
        private static MsgPackGiaPhaPayload ConvertToMsgPackPayload(GiaphaInfo source)
        {
            if (source == null)
            {
                return null;
            }

            MsgPackFamilyPayload MapFamily(FamilyInfo family)
            {
                if (family == null)
                {
                    return null;
                }

                return new MsgPackFamilyPayload
                {
                    FamilyId = family.FamilyId,
                    FamilyUp = family.FamilyUp,
                    FamilyOrder = family.FamilyOrder,
                    FamilyLevel = family.FamilyLevel,
                    FamilyNew = family.FamilyNew,
                    X = family.X,
                    Y = family.Y,
                    Width = family.Width,
                    Height = family.Height,
                    PhaDoShapeSvgId = family.PhaDoShapeSvgId,
                    Persons = family.ListPerson?
                        .Select(p => new MsgPackPersonPayload
                        {
                            IsMainPerson = p?.IsMainPerson ?? 0,
                            MANS_NAME_HUY = p?.MANS_NAME_HUY ?? "",
                            MANS_NAME_TU = p?.MANS_NAME_TU ?? "",
                            MANS_NAME_THUONG = p?.MANS_NAME_THUONG ?? "",
                            MANS_NAME_THUY = p?.MANS_NAME_THUY ?? "",
                            MANS_ID = p?.MANS_ID ?? "",
                            fid = p?.fid ?? "",
                            MANS_GENDER = p?.MANS_GENDER ?? "Nam",
                            MANS_DOB = p?.MANS_DOB ?? "",
                            MANS_DOD = p?.MANS_DOD ?? "",
                            MANS_WOD = p?.MANS_WOD ?? "",
                            MANS_DETAIL = p?.MANS_DETAIL ?? "",
                            MANS_CONTHUMAY = p?.MANS_CONTHUMAY ?? ""
                        })
                        .ToList() ?? new List<MsgPackPersonPayload>(),
                    Children = family.FamilyChildren?
                        .Select(MapFamily)
                        .Where(c => c != null)
                        .ToList() ?? new List<MsgPackFamilyPayload>()
                };
            }

            return new MsgPackGiaPhaPayload
            {
                GiaphaId = source.GiaphaId,
                Username = source.Username ?? "",
                Password = source.Password ?? "",
                GiaphaName = source.GiaphaName ?? "",
                GiaphaNameRoot = source.GiaphaNameRoot ?? "",
                PhaKy = source.PhaKy ?? "",
                Tocuoc = source.Tocuoc ?? "",
                ThuyTo = source.ThuyTo ?? "",
                HuongHoa = source.HuongHoa ?? "",
                RF_OTAI = source.RF_OTAI ?? "",
                RF_DAYS = source.RF_DAYS ?? "",
                RF_CHANNGON = source.RF_CHANNGON ?? "",
                FamilyRoot = MapFamily(source.familyRoot),
                SvgShapesById = source.SvgShapesById != null
                    ? new Dictionary<string, PhaDoSvgShape>(source.SvgShapesById, StringComparer.Ordinal)
                    : new Dictionary<string, PhaDoSvgShape>(StringComparer.Ordinal)
            };
        }

        /// <summary>
        /// Dựng lại GiaphaInfo từ payload MessagePack và nối lại _familyInfo cho từng người.
        /// </summary>
        private static GiaphaInfo ConvertFromMsgPackPayload(MsgPackGiaPhaPayload payload)
        {
            if (payload == null)
            {
                return null;
            }

            FamilyInfo MapFamily(MsgPackFamilyPayload family)
            {
                if (family == null)
                {
                    return null;
                }

                var result = new FamilyInfo
                {
                    FamilyId = family.FamilyId,
                    FamilyUp = family.FamilyUp,
                    FamilyOrder = family.FamilyOrder,
                    FamilyLevel = family.FamilyLevel,
                    FamilyNew = family.FamilyNew,
                    X = family.X,
                    Y = family.Y,
                    Width = family.Width,
                    Height = family.Height,
                    PhaDoShapeSvgId = family.PhaDoShapeSvgId
                };

                var stagedMain = new List<(PersonInfo person, int isMain)>();
                foreach (var person in family.Persons ?? Enumerable.Empty<MsgPackPersonPayload>())
                {
                    var created = new PersonInfo(person?.MANS_NAME_HUY ?? "", result)
                    {
                        MANS_NAME_HUY = person?.MANS_NAME_HUY ?? "",
                        MANS_NAME_TU = person?.MANS_NAME_TU ?? "",
                        MANS_NAME_THUONG = person?.MANS_NAME_THUONG ?? "",
                        MANS_NAME_THUY = person?.MANS_NAME_THUY ?? "",
                        MANS_ID = person?.MANS_ID ?? "",
                        fid = person?.fid ?? "",
                        MANS_GENDER = string.IsNullOrWhiteSpace(person?.MANS_GENDER) ? "Nam" : person.MANS_GENDER,
                        MANS_DOB = person?.MANS_DOB ?? "",
                        MANS_DOD = person?.MANS_DOD ?? "",
                        MANS_WOD = person?.MANS_WOD ?? "",
                        MANS_DETAIL = person?.MANS_DETAIL ?? "",
                        MANS_CONTHUMAY = person?.MANS_CONTHUMAY ?? ""
                    };
                    result.ListPerson.Add(created);
                    stagedMain.Add((created, person?.IsMainPerson ?? 0));
                }

                // Chốt người chính sau khi đã add đủ danh sách để logic "chỉ 1 main" chạy ổn định.
                foreach (var item in stagedMain)
                {
                    item.person.IsMainPerson = item.isMain == 1 ? 1 : 0;
                }

                foreach (var child in family.Children ?? Enumerable.Empty<MsgPackFamilyPayload>())
                {
                    var childMapped = MapFamily(child);
                    if (childMapped != null)
                    {
                        result.FamilyChildren.Add(childMapped);
                    }
                }

                return result;
            }

            var gp = new GiaphaInfo
            {
                GiaphaId = payload.GiaphaId,
                Username = payload.Username ?? "",
                Password = payload.Password ?? "",
                GiaphaName = payload.GiaphaName ?? "",
                GiaphaNameRoot = payload.GiaphaNameRoot ?? "",
                PhaKy = payload.PhaKy ?? "",
                Tocuoc = payload.Tocuoc ?? "",
                ThuyTo = payload.ThuyTo ?? "",
                HuongHoa = payload.HuongHoa ?? "",
                RF_OTAI = payload.RF_OTAI ?? "",
                RF_DAYS = payload.RF_DAYS ?? "",
                RF_CHANNGON = payload.RF_CHANNGON ?? "",
                familyRoot = MapFamily(payload.FamilyRoot) ?? new FamilyInfo(),
                SvgShapesById = payload.SvgShapesById != null
                    ? new Dictionary<string, PhaDoSvgShape>(payload.SvgShapesById, StringComparer.Ordinal)
                    : new Dictionary<string, PhaDoSvgShape>(StringComparer.Ordinal)
            };

            return gp;
        }

        private void SaveAsMessagePack_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel?.FamilyTree == null)
            {
                MessageBox.Show("Chưa có dữ liệu gia phả để lưu.", "MessagePack");
                return;
            }

            try
            {
                var dialog = new SaveFileDialog
                {
                    DefaultExt = ".msgpack",
                    Filter = "MessagePack files (*.msgpack)|*.msgpack|All files (*.*)|*.*"
                };
                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                // Lưu object thật (không bọc JSON string) để giảm parse text lúc mở lại.
                var payloadObj = ConvertToMsgPackPayload(viewModel.FamilyTree.GP);
                byte[] payload = MessagePackSerializer.Serialize(payloadObj, MsgPackOptions);
                File.WriteAllBytes(dialog.FileName, payload);

                viewModel.AddUserAction("Đã lưu MessagePack: " + dialog.FileName);
                MessageBox.Show(
                    "Đã lưu MessagePack.\n\nFile: " + dialog.FileName
                    + "\nKích thước: " + payload.Length.ToString("#,##0") + " bytes",
                    "MessagePack");
            }
            catch (Exception ex)
            {
                log.Error("Lỗi lưu MessagePack.", ex);
                string detail = ex.InnerException != null ? ("\nChi tiết: " + ex.InnerException.Message) : "";
                MessageBox.Show("Lỗi lưu MessagePack: " + ex.Message + detail, "Có Lỗi");
            }
        }

        private async void OpenMessagePack_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                DefaultExt = ".msgpack",
                Filter = "MessagePack files (*.msgpack)|*.msgpack|All files (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                GiaphaInfo loadedGiaPha = null;
                GiaphaInfo gp = await LoadGiaPhaFromMessagePackWithProgressAsync(
                    dialog.FileName,
                    "Đang mở file MessagePack...",
                    "Đang đọc dữ liệu MessagePack...\n\nĐã chờ: 0 giây",
                    async loaded =>
                    {
                        // Không giữ đuôi .msgpack làm file save mặc định vì luồng Save hiện tại ghi JSON.
                        loaded.FileName = Path.ChangeExtension(dialog.FileName, ".json");
                        loadedGiaPha = loaded;
                        await viewModel.UpdateGiaPhaAsync(loaded, saveToJson: false).ConfigureAwait(true);
                    }).ConfigureAwait(true);

                if (gp != null)
                {
                    if (loadedGiaPha == null)
                    {
                        gp.FileName = Path.ChangeExtension(dialog.FileName, ".json");
                        await viewModel.UpdateGiaPhaAsync(gp, saveToJson: false).ConfigureAwait(true);
                    }

                    log.Info("OpenMessagePack_Click: Mở file xong: " + dialog.FileName);
                    viewModel.AddUserAction("Đã mở MessagePack: " + dialog.FileName);
                }
                else
                {
                    MessageBox.Show("Lỗi mở file MessagePack: " + dialog.FileName, "Có Lỗi");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi mở file MessagePack: " + ex.Message, "Có Lỗi");
                log.Error("OpenMessagePack_Click: Lỗi file: " + dialog.FileName);
                log.Error(ex);
            }
        }

        private static void UpdatePhaDoRenderProgressMessage(
            ProgressDialogController progress,
            Stopwatch sw,
            bool completed)
        {
            if (progress == null || sw == null)
            {
                return;
            }

            double seconds = sw.Elapsed.TotalSeconds;
            int wholeSeconds = Math.Max(0, (int)Math.Floor(seconds));
            string message;
            if (completed)
            {
                message = "Hoàn tất vẽ phả đồ.\n\nThời gian: "
                    + wholeSeconds + " giây";
                if (seconds - wholeSeconds >= 0.05)
                {
                    message += " (" + seconds.ToString("0.0") + " s)";
                }
            }
            else
            {
                message = "Đang tính toán layout và vẽ lên canvas...\n\nĐã chờ: "
                    + wholeSeconds + " giây";
            }

            progress.SetMessage(message);
        }

        private async void PhaDoAnalyzeSplit_Click(object sender, RoutedEventArgs e)
        {
            var root = viewModel?.FamilyTree?.Family?.RootPerson;
            if (root == null)
            {
                await this.ShowMessageAsync("Phả con", "Chưa có dữ liệu gia phả để phân tích.").ConfigureAwait(true);
                return;
            }

            var progress = await this.ShowProgressAsync(
                "Phả con...",
                "Đang phân tích và tính layout...\n\nĐã chờ: 0 giây").ConfigureAwait(true);
            progress.SetIndeterminate();
            var sw = Stopwatch.StartNew();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, __) => progress.SetMessage(
                "Đang phân tích và tính layout...\n\nĐã chờ: " + (int)sw.Elapsed.TotalSeconds + " giây");
            timer.Start();
            try
            {
                const int analysisMaxLevel = 30;
                GiaPhaRenderResult layout = await ComputePhaDoLayoutSnapshotAsync(root).ConfigureAwait(true);
                int deepSplitMinFamilies = ComputeAdaptiveMinBranchToSplitDeep(layout?.Nodes?.Count ?? 0);
                int splitLevel = ResolveSuggestedSplitLevel(
                    root,
                    layout,
                    minLevel: 1,
                    maxLevel: analysisMaxLevel,
                    minCutLevel: 3,
                    minBranchToSplitDeep: deepSplitMinFamilies);

                // Root0 mặc định 1–4. Nhưng nếu đề xuất tách sớm (ví dụ đời 4) thì root0 phải dừng trước đó
                // để tránh mâu thuẫn: text nói tách đời 4 nhưng sơ đồ lại đẩy sang đời 5.
                int defaultRoot0Max = 4;
                int effectiveRoot0Max = defaultRoot0Max;
                if (splitLevel > 0 && splitLevel <= defaultRoot0Max)
                {
                    effectiveRoot0Max = Math.Max(1, splitLevel - 1);
                }

                string report = BuildPhaDoSplitAnalysisReport(
                    root,
                    layout,
                    minLevel: 1,
                    maxLevel: analysisMaxLevel,
                    minCutLevel: 3,
                    rootLevelMax: effectiveRoot0Max,
                    minBranchToSplitDeep: deepSplitMinFamilies);

                var map = BuildSubtreeMap(
                    layout,
                    effectiveRoot0Max,
                    splitLevel,
                    subtreeMaxGeneration: analysisMaxLevel,
                    minBranchToSplitDeep: deepSplitMinFamilies);
                // Sau khi phân tích xong, đổ list để user chọn nhanh Toàn phả hoặc từng phả con.
                UpdatePhaDoRenderScopesFromMap(
                    root,
                    map,
                    splitLevel,
                    analysisMaxLevel,
                    deepSplitMinFamilies);
                var dlg = new PhaDoSubtreeMapDialog(dpi: 96) { Owner = this };
                dlg.SetContent(report, map.RootBlock, map.SubTrees, effectiveRoot0Max, splitLevel);

                // Đóng dialog chờ trước khi mở cửa sổ phân tích để tránh cảm giác "kẹt" spinner/đếm.
                timer.Stop();
                sw.Stop();
                try { await progress.CloseAsync().ConfigureAwait(true); } catch { }

                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                log.Warn("Lỗi phân tích phả con.", ex);
                await this.ShowMessageAsync("Phả con", "Lỗi: " + ex.Message).ConfigureAwait(true);
            }
            finally
            {
                timer.Stop();
                sw.Stop();
                if (progress != null && progress.IsOpen)
                {
                    try { await progress.CloseAsync().ConfigureAwait(true); } catch { }
                }
            }
        }

        /// <summary>Layout phả đồ đầy đủ (offsets + custom size) — dùng đo bounds cm.</summary>
        private async Task<GiaPhaRenderResult> ComputePhaDoLayoutSnapshotAsync(FamilyViewModel root)
        {
            var options = BuildPhaDoRenderOptions();
            var baseResult = await GiaPhaRenderService.ComputeLayoutAsync(root, options).ConfigureAwait(true);
            CapturePhaDoBaseLayout(baseResult);

            GiaPhaRenderResult result = GiaPhaManualLayoutService.ApplyManualOffsets(
                baseResult,
                _phaDoOffsetXmmByFamilyId,
                _phaDoOffsetYmmByFamilyId);
            ApplyCustomBoxSizesFromStyles(result);
            GiaPhaManualLayoutService.RebuildConnectorsOnly(result);
            GiaPhaRenderBoundsFitter.FitCanvasToContent(result);
            return result;
        }

        private static string FormatBoundsCm(double minXmm, double minYmm, double maxXmm, double maxYmm)
        {
            double wCm = Math.Max(0, maxXmm - minXmm) / 10.0;
            double hCm = Math.Max(0, maxYmm - minYmm) / 10.0;
            return wCm.ToString("0.#") + " × " + hCm.ToString("0.#") + " cm";
        }

        private static bool ShouldSplitPhaiCon(int familyCount)
        {
            return familyCount >= PhaDoMinFamilyCountToSplitPhaiCon;
        }

        /// <summary>
        /// Tính ngưỡng tách sâu theo quy mô phả: phả lớn thì ngưỡng tăng để số nhánh con cân đối hơn.
        /// </summary>
        private static int ComputeAdaptiveMinBranchToSplitDeep(int totalFamilyCount)
        {
            if (totalFamilyCount <= 0)
            {
                return PhaDoTargetFamilyCountPerSubtree;
            }

            int wishedBranchCount = Math.Max(1, (int)Math.Ceiling(totalFamilyCount / (double)PhaDoTargetFamilyCountPerSubtree));
            int dynamicThreshold = (int)Math.Floor(totalFamilyCount / (double)wishedBranchCount);
            return Math.Max(PhaDoMinFamilyCountToSplitPhaiCon, dynamicThreshold);
        }

        /// <summary>
        /// Độ sâu bước tách tự động: nhánh càng lớn thì đi sâu nhiều đời hơn để nhanh đạt size mục tiêu.
        /// </summary>
        private static int ComputeAdaptiveSplitStride(int representativeBranchSize)
        {
            if (representativeBranchSize <= 0)
            {
                return 2;
            }

            // log2(size/target) giúp nhảy đời theo cấp số nhân, tránh cố định +4 cho mọi phả.
            double ratio = representativeBranchSize / (double)Math.Max(1, PhaDoTargetFamilyCountPerSubtree);
            double strideRaw = Math.Log(Math.Max(1.0, ratio), 2.0) + 2.0;
            int stride = (int)Math.Round(strideRaw, MidpointRounding.AwayFromZero);
            return Math.Max(2, Math.Min(6, stride));
        }

        /// <summary>Tính median để đo độ lệch giữa các nhánh khi chọn đời cắt.</summary>
        private static double ComputeMedian(IReadOnlyList<int> values)
        {
            if (values == null || values.Count == 0)
            {
                return 0;
            }

            var sorted = values.OrderBy(v => v).ToList();
            int mid = sorted.Count / 2;
            if (sorted.Count % 2 == 1)
            {
                return sorted[mid];
            }

            return (sorted[mid - 1] + sorted[mid]) / 2.0;
        }

        /// <summary>
        /// Bộ đo lường tách phả con dùng chung cho report và vẽ — tránh hai luồng lệch thuật toán.
        /// </summary>
        private sealed class PhaConSplitMetrics
        {
            private readonly Dictionary<int, int> _subtreeSizeById = new Dictionary<int, int>();

            public int EffectiveMax { get; }
            public int MinBranch { get; }
            public int TargetBranch { get; }

            public PhaConSplitMetrics(int effectiveMax, int minBranch, int targetBranch)
            {
                EffectiveMax = effectiveMax;
                MinBranch = minBranch;
                TargetBranch = targetBranch;
            }

            public int SubtreeSize(FamilyViewModel node)
            {
                int id = node?.familyInfo?.FamilyId ?? 0;
                if (id <= 0)
                {
                    return 0;
                }
                if (_subtreeSizeById.TryGetValue(id, out int cached))
                {
                    return cached;
                }
                int sum = 1;
                if (node.Children != null)
                {
                    foreach (var child in node.Children)
                    {
                        sum += SubtreeSize(child);
                    }
                }
                _subtreeSizeById[id] = sum;
                return sum;
            }

            /// <summary>
            /// Số GD từ đời (anchor+1) đến trước đời probe — không gồm các root phả con kế tại probe.
            /// </summary>
            public int SegmentCountBeforeProbe(FamilyViewModel anchor, int probeLevel)
            {
                int anchorLevel = anchor?.familyInfo?.FamilyLevel ?? 0;
                if (anchor == null || probeLevel <= anchorLevel + 1)
                {
                    return 0;
                }

                int count = 0;
                var segmentStack = new Stack<FamilyViewModel>();
                segmentStack.Push(anchor);
                while (segmentStack.Count > 0)
                {
                    var cur = segmentStack.Pop();
                    if (cur == null)
                    {
                        continue;
                    }

                    int level = cur.familyInfo?.FamilyLevel ?? 0;
                    if (level > anchorLevel && level < probeLevel)
                    {
                        count++;
                    }

                    if (level < probeLevel && cur.Children != null)
                    {
                        foreach (var child in cur.Children)
                        {
                            segmentStack.Push(child);
                        }
                    }
                }

                return count;
            }

            /// <summary>
            /// Chọn đời kế tiếp theo từng nhánh cha: đoạn trước mốc phải ~TargetBranch và >= MinBranch.
            /// </summary>
            public bool TrySelectNextSplitLevel(
                FamilyViewModel baseRoot,
                out int probeLevel,
                out List<FamilyViewModel> eligibleRootsAtProbe,
                out int segmentSize)
            {
                probeLevel = -1;
                eligibleRootsAtProbe = null;
                segmentSize = 0;
                if (baseRoot == null)
                {
                    return false;
                }

                int anchorLevel = baseRoot.familyInfo?.FamilyLevel ?? 0;
                int bestLevel = -1;
                List<FamilyViewModel> bestRoots = null;
                int bestSegment = 0;
                double bestGap = double.MaxValue;
                double bestImbalance = double.MaxValue;
                int bestSinglePenalty = int.MaxValue;

                for (int probe = anchorLevel + 1; probe <= EffectiveMax; probe++)
                {
                    var rootsAtProbe = CollectRootsAtLevelFromBase(new[] { baseRoot }, probe);
                    var eligible = rootsAtProbe
                        .Where(r => SubtreeSize(r) >= MinBranch)
                        .ToList();
                    if (eligible.Count == 0)
                    {
                        continue;
                    }

                    int segment = SegmentCountBeforeProbe(baseRoot, probe);
                    // Đoạn quá ngắn thì không cắt sớm — tránh báo ~1 GD rồi vẫn coi là đủ tách tiếp.
                    if (segment < MinBranch)
                    {
                        continue;
                    }

                    double gap = Math.Abs(segment - TargetBranch);
                    var childSizes = eligible.Select(SubtreeSize).ToList();
                    double median = Math.Max(1.0, ComputeMedian(childSizes));
                    double max = childSizes.Max();
                    double imbalance = max / median;
                    int singlePenalty = eligible.Count <= 1 ? 1 : 0;

                    bool isBetter = gap < bestGap
                        || (Math.Abs(gap - bestGap) < 0.0001 && singlePenalty < bestSinglePenalty)
                        || (Math.Abs(gap - bestGap) < 0.0001
                            && singlePenalty == bestSinglePenalty
                            && imbalance < bestImbalance)
                        || (Math.Abs(gap - bestGap) < 0.0001
                            && singlePenalty == bestSinglePenalty
                            && Math.Abs(imbalance - bestImbalance) < 0.0001
                            && (bestLevel < 0 || probe < bestLevel));
                    if (isBetter)
                    {
                        bestLevel = probe;
                        bestRoots = eligible;
                        bestSegment = segment;
                        bestGap = gap;
                        bestImbalance = imbalance;
                        bestSinglePenalty = singlePenalty;
                    }
                }

                if (bestRoots == null || bestRoots.Count == 0)
                {
                    return false;
                }

                probeLevel = bestLevel;
                eligibleRootsAtProbe = bestRoots;
                segmentSize = bestSegment;
                return true;
            }

            public int SegmentSizeToNextSplit(FamilyViewModel node)
            {
                if (TrySelectNextSplitLevel(node, out int _, out List<FamilyViewModel> _, out int segment))
                {
                    return segment;
                }
                return SubtreeSize(node);
            }

            public bool CanContinueSplit(FamilyViewModel node)
            {
                return TrySelectNextSplitLevel(node, out int _, out List<FamilyViewModel> _, out int segment)
                    && segment >= MinBranch;
            }

            public List<FamilyViewModel> CollectNextSplitRoots(IEnumerable<FamilyViewModel> baseRoots)
            {
                var result = new List<FamilyViewModel>();
                if (baseRoots == null)
                {
                    return result;
                }

                var seen = new HashSet<int>();
                foreach (var baseRoot in baseRoots)
                {
                    if (!TrySelectNextSplitLevel(baseRoot, out int _, out List<FamilyViewModel> eligible, out _))
                    {
                        continue;
                    }

                    foreach (var root in eligible)
                    {
                        int id = root?.familyInfo?.FamilyId ?? 0;
                        if (id > 0 && seen.Add(id))
                        {
                            result.Add(root);
                        }
                    }
                }

                return result;
            }
        }

        private static int ResolveSuggestedSplitLevel(
            FamilyViewModel root,
            GiaPhaRenderResult layout,
            int minLevel,
            int maxLevel,
            int minCutLevel,
            int minBranchToSplitDeep)
        {
            // Chọn đời cắt theo độ cân bằng nhánh; không cố định "ít root nhất".
            if (root == null || layout?.Nodes == null || layout.Nodes.Count == 0)
            {
                return -1;
            }

            if (!ShouldSplitPhaiCon(layout.Nodes.Count))
            {
                return -1;
            }

            int observedMax = layout.Nodes.Max(n => n?.Family?.familyInfo?.FamilyLevel ?? 0);
            int effectiveMax = Math.Min(maxLevel, Math.Max(minLevel, observedMax));

            var nodesByLevel = new Dictionary<int, List<FamilyViewModel>>();
            for (int lvl = minLevel; lvl <= effectiveMax; lvl++)
            {
                nodesByLevel[lvl] = new List<FamilyViewModel>();
            }

            foreach (var n in layout.Nodes)
            {
                int lvl = n?.Family?.familyInfo?.FamilyLevel ?? 0;
                if (lvl >= minLevel && lvl <= effectiveMax)
                {
                    nodesByLevel[lvl].Add(n.Family);
                }
            }

            var subtreeSizeById = new Dictionary<int, int>();
            int SubtreeSize(FamilyViewModel node)
            {
                int id = node?.familyInfo?.FamilyId ?? 0;
                if (id <= 0)
                {
                    return 0;
                }
                if (subtreeSizeById.TryGetValue(id, out int cached))
                {
                    return cached;
                }
                int sum = 1;
                if (node.Children != null)
                {
                    foreach (var c in node.Children)
                    {
                        sum += SubtreeSize(c);
                    }
                }
                subtreeSizeById[id] = sum;
                return sum;
            }

            int bestLevel = -1;
            int bestCount = int.MaxValue;
            int bestMaxSub = int.MaxValue;
            double bestImbalance = double.MaxValue;
            double bestAverageGap = double.MaxValue;
            int startCut = Math.Max(minCutLevel, minLevel);
            for (int lvl = startCut; lvl <= effectiveMax; lvl++)
            {
                int count = nodesByLevel[lvl].Count;
                if (count <= 1)
                {
                    continue;
                }
                int maxSub = 0;
                int splittableBranches = 0;
                var sizes = new List<int>(count);
                foreach (var r in nodesByLevel[lvl])
                {
                    int sub = SubtreeSize(r);
                    sizes.Add(sub);
                    maxSub = Math.Max(maxSub, sub);
                    if (sub >= minBranchToSplitDeep)
                    {
                        splittableBranches++;
                    }
                }

                // Bỏ qua mốc cắt nếu không có nhánh nào đủ lớn để tách tiếp.
                if (splittableBranches == 0)
                {
                    continue;
                }

                double median = Math.Max(1.0, ComputeMedian(sizes));
                double imbalance = maxSub / median;
                double average = sizes.Average();
                double averageGap = Math.Abs(average - PhaDoTargetFamilyCountPerSubtree);
                bool isBetter = imbalance < bestImbalance
                    || (Math.Abs(imbalance - bestImbalance) < 0.0001 && averageGap < bestAverageGap)
                    || (Math.Abs(imbalance - bestImbalance) < 0.0001
                        && Math.Abs(averageGap - bestAverageGap) < 0.0001
                        && count < bestCount)
                    || (Math.Abs(imbalance - bestImbalance) < 0.0001
                        && Math.Abs(averageGap - bestAverageGap) < 0.0001
                        && count == bestCount
                        && maxSub < bestMaxSub);
                if (isBetter)
                {
                    bestLevel = lvl;
                    bestCount = count;
                    bestMaxSub = maxSub;
                    bestImbalance = imbalance;
                    bestAverageGap = averageGap;
                }
            }

            return bestLevel > 0 ? bestLevel : -1;
        }

        private sealed class PhaDoSubtreeMap
        {
            public PhaDoSubtreeBranchBlock RootBlock { get; set; }
            public List<PhaDoSubtreeBranchBlock> SubTrees { get; } = new List<PhaDoSubtreeBranchBlock>();
        }

        private static string GetFamilyMainPersonName(FamilyViewModel family)
        {
            if (family == null)
            {
                return "Gia đình";
            }

            var main = family.ListPerson?.FirstOrDefault(p => p.IsMainPerson == 1)
                ?? family.ListPerson?.FirstOrDefault();
            string name = main?.MANS_NAME_HUY;
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name.Trim();
            }

            return (family.Name0 ?? family.Name ?? "Gia đình").Trim();
        }

        private static string GetFamilyDisplayName(FamilyViewModel family)
        {
            return (family?.Name0 ?? family?.Name ?? "").Trim();
        }

        private static string GetFamilySpouseNames(FamilyViewModel family)
        {
            if (family?.ListPerson == null || family.ListPerson.Count == 0)
            {
                return "";
            }

            var spouseNames = family.ListPerson
                .Where(p => p != null && p.IsMainPerson != 1)
                .Select(p => (p.MANS_NAME_HUY ?? "").Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .ToList();

            return spouseNames.Count == 0 ? "" : string.Join(" | ", spouseNames);
        }

        private static PhaDoSubtreeBranchBlock CreateBranchBlock(
            FamilyViewModel family,
            double minXmm,
            double minYmm,
            double maxXmm,
            double maxYmm,
            int nodeCount)
        {
            return new PhaDoSubtreeBranchBlock
            {
                FamilyName = GetFamilyDisplayName(family),
                MainPersonName = GetFamilyMainPersonName(family),
                SpouseNamesText = GetFamilySpouseNames(family),
                FamilyId = family?.familyInfo?.FamilyId ?? 0,
                Generation = family?.familyInfo?.FamilyLevel ?? 0,
                NodeCount = nodeCount,
                MinXmm = minXmm,
                MinYmm = minYmm,
                MaxXmm = maxXmm,
                MaxYmm = maxYmm
            };
        }

        private static List<FamilyViewModel> CollectRootsAtLevelFromBase(
            IEnumerable<FamilyViewModel> baseRoots,
            int targetLevel)
        {
            var result = new List<FamilyViewModel>();
            if (baseRoots == null)
            {
                return result;
            }

            var seen = new HashSet<int>();
            foreach (var baseRoot in baseRoots)
            {
                if (baseRoot == null)
                {
                    continue;
                }

                var stack = new Stack<FamilyViewModel>();
                stack.Push(baseRoot);
                while (stack.Count > 0)
                {
                    var cur = stack.Pop();
                    int lvl = cur?.familyInfo?.FamilyLevel ?? 0;
                    if (lvl == targetLevel)
                    {
                        int id = cur?.familyInfo?.FamilyId ?? 0;
                        if (id > 0 && seen.Add(id))
                        {
                            result.Add(cur);
                        }
                        continue;
                    }

                    if (lvl > targetLevel || cur?.Children == null)
                    {
                        continue;
                    }

                    foreach (var c in cur.Children)
                    {
                        stack.Push(c);
                    }
                }
            }

            return result;
        }

        private static bool TryBoundsForPlacedNodes(
            IEnumerable<GiaPhaPlacedNode> nodes,
            out double minXmm,
            out double minYmm,
            out double maxXmm,
            out double maxYmm,
            out int count)
        {
            minXmm = double.PositiveInfinity;
            minYmm = double.PositiveInfinity;
            maxXmm = double.NegativeInfinity;
            maxYmm = double.NegativeInfinity;
            count = 0;

            if (nodes == null)
            {
                minXmm = minYmm = maxXmm = maxYmm = 0;
                return false;
            }

            foreach (var n in nodes)
            {
                if (n?.Metrics == null)
                {
                    continue;
                }

                double left = n.Xmm;
                double top = n.Ymm;
                double right = n.Xmm + n.Metrics.WidthMm;
                double bottom = n.Ymm + Math.Max(n.Metrics.HeightMm, n.Metrics.SlotHeightMm);
                minXmm = Math.Min(minXmm, left);
                minYmm = Math.Min(minYmm, top);
                maxXmm = Math.Max(maxXmm, right);
                maxYmm = Math.Max(maxYmm, bottom);
                count++;
            }

            if (count == 0)
            {
                minXmm = minYmm = maxXmm = maxYmm = 0;
                return false;
            }

            return true;
        }

        private static bool TryCompactBoundsForLevelRange(
            GiaPhaRenderResult layout,
            int minLevel,
            int maxLevel,
            out double minXmm,
            out double minYmm,
            out double maxXmm,
            out double maxYmm,
            out int count)
        {
            minXmm = minYmm = maxXmm = maxYmm = 0;
            count = 0;
            if (layout?.Nodes == null || layout.Nodes.Count == 0 || maxLevel < minLevel)
            {
                return false;
            }

            var nodes = layout.Nodes
                .Where(n =>
                    n?.Metrics != null
                    && (n?.Family?.familyInfo?.FamilyLevel ?? 0) >= minLevel
                    && (n?.Family?.familyInfo?.FamilyLevel ?? 0) <= maxLevel)
                .ToList();
            if (nodes.Count == 0)
            {
                return false;
            }

            double hGap = Math.Max(1, layout.Options?.HorizontalGapMm ?? 10);
            double vGap = Math.Max(1, layout.Options?.GenerationGapMm ?? 24);

            double totalH = 0;
            double maxW = 0;
            var groups = nodes
                .GroupBy(n => n.Family?.familyInfo?.FamilyLevel ?? 0)
                .OrderBy(g => g.Key)
                .ToList();

            for (int i = 0; i < groups.Count; i++)
            {
                var row = groups[i].ToList();
                double rowW = row.Sum(n => n.Metrics.WidthMm) + Math.Max(0, row.Count - 1) * hGap;
                double rowH = row.Max(n => Math.Max(n.Metrics.HeightMm, n.Metrics.SlotHeightMm));
                maxW = Math.Max(maxW, rowW);
                totalH += rowH;
                if (i < groups.Count - 1)
                {
                    totalH += vGap;
                }
            }

            minXmm = 0;
            minYmm = 0;
            maxXmm = maxW;
            maxYmm = totalH;
            count = nodes.Count;
            return true;
        }

        private static bool TryGetSubtreeBoundsMm(
            GiaPhaRenderResult result,
            FamilyViewModel subtreeRoot,
            int maxGeneration,
            out double minXmm,
            out double minYmm,
            out double maxXmm,
            out double maxYmm,
            out int nodeCount)
        {
            minXmm = minYmm = maxXmm = maxYmm = 0;
            nodeCount = 0;
            if (result?.Nodes == null || subtreeRoot?.familyInfo == null)
            {
                return false;
            }

            var nodeById = result.Nodes
                .Where(n => (n?.Family?.familyInfo?.FamilyId ?? 0) > 0)
                .ToDictionary(n => n.Family.familyInfo.FamilyId, n => n);

            var placed = new List<GiaPhaPlacedNode>();
            var stack = new Stack<FamilyViewModel>();
            stack.Push(subtreeRoot);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                int level = cur?.familyInfo?.FamilyLevel ?? 0;
                if (maxGeneration > 0 && level > maxGeneration)
                {
                    continue;
                }

                int id = cur?.familyInfo?.FamilyId ?? 0;
                if (id > 0 && nodeById.TryGetValue(id, out var placedNode))
                {
                    placed.Add(placedNode);
                }

                if (cur?.Children != null)
                {
                    foreach (var c in cur.Children)
                    {
                        int childLevel = c?.familyInfo?.FamilyLevel ?? 0;
                        // Chỉ tính phả con đến đời N, tránh kéo width/height bởi đời sâu hơn N.
                        if (maxGeneration > 0 && childLevel > maxGeneration)
                        {
                            continue;
                        }
                        stack.Push(c);
                    }
                }
            }

            return TryBoundsForPlacedNodes(placed, out minXmm, out minYmm, out maxXmm, out maxYmm, out nodeCount);
        }

        private static PhaDoSubtreeMap BuildSubtreeMap(
            GiaPhaRenderResult result,
            int rootLevelMax,
            int splitLevel,
            int subtreeMaxGeneration = int.MaxValue,
            int minBranchToSplitDeep = 100)
        {
            // Vẽ sơ đồ dựa trên bounds mm thật của layout (sau offsets + custom size).
            var map = new PhaDoSubtreeMap();
            if (result?.Nodes == null || result.Nodes.Count == 0)
            {
                map.RootBlock = new PhaDoSubtreeBranchBlock
                {
                    MainPersonName = "Root0",
                    Generation = 1,
                    NodeCount = 0,
                    MaxXmm = 10,
                    MaxYmm = 10
                };
                return map;
            }

            // Nếu không tách phả con (splitLevel <= 0) thì root0 đại diện cho toàn phả (mọi đời),
            // tránh trường hợp chỉ vẽ đời 1–4 rồi người dùng tưởng "không vẽ".
            List<GiaPhaPlacedNode> rootNodes;
            if (splitLevel <= 0)
            {
                rootNodes = result.Nodes.ToList();
            }
            else
            {
                rootNodes = result.Nodes
                    .Where(n => (n?.Family?.familyInfo?.FamilyLevel ?? 0) >= 1
                        && (n?.Family?.familyInfo?.FamilyLevel ?? 0) <= rootLevelMax)
                    .ToList();
            }

            if (splitLevel > 0)
            {
                // Root0 đời 1–N: đo compact theo riêng các đời này để tránh bị kéo giãn do hậu duệ sâu.
                if (!TryCompactBoundsForLevelRange(result, 1, rootLevelMax, out double cMinX, out double cMinY, out double cMaxX, out double cMaxY, out int cCount))
                {
                    TryBoundsForPlacedNodes(rootNodes, out cMinX, out cMinY, out cMaxX, out cMaxY, out cCount);
                }
                var rootFamilyCompact = result.Nodes.FirstOrDefault(n => n?.Family?.Parent == null)?.Family
                    ?? result.Nodes.OrderBy(n => n?.Family?.familyInfo?.FamilyLevel ?? 99).FirstOrDefault()?.Family;
                map.RootBlock = CreateBranchBlock(rootFamilyCompact, cMinX, cMinY, cMaxX, cMaxY, cCount);
            }
            else
            {
                TryBoundsForPlacedNodes(rootNodes, out double rMinX, out double rMinY, out double rMaxX, out double rMaxY, out int rCount);
                var rootFamilyWhole = result.Nodes.FirstOrDefault(n => n?.Family?.Parent == null)?.Family
                    ?? result.Nodes.OrderBy(n => n?.Family?.familyInfo?.FamilyLevel ?? 99).FirstOrDefault()?.Family;
                map.RootBlock = CreateBranchBlock(rootFamilyWhole, rMinX, rMinY, rMaxX, rMaxY, rCount);
            }
            // Root0 của phả con phải hiển thị theo tên gia đình gốc.
            if (string.IsNullOrWhiteSpace(map.RootBlock.FamilyName))
            {
                var rootFamily = result.Nodes.FirstOrDefault(n => n?.Family?.Parent == null)?.Family
                    ?? result.Nodes.OrderBy(n => n?.Family?.familyInfo?.FamilyLevel ?? 99).FirstOrDefault()?.Family;
                map.RootBlock.FamilyName = GetFamilyDisplayName(rootFamily);
            }

            if (splitLevel > 0)
            {
                var splitRoots = result.Nodes
                    .Where(n => (n?.Family?.familyInfo?.FamilyLevel ?? 0) == splitLevel)
                    .Select(n => n.Family)
                    .Where(f => f != null)
                    .ToList();

                var addedFamilyIds = new HashSet<int>();
                var splitMetrics = new PhaConSplitMetrics(
                    subtreeMaxGeneration,
                    minBranchToSplitDeep,
                    PhaDoTargetFamilyCountPerSubtree);

                bool TryAddBranch(FamilyViewModel family, out int count)
                {
                    count = 0;
                    int familyId = family?.familyInfo?.FamilyId ?? 0;
                    if (familyId <= 0 || addedFamilyIds.Contains(familyId))
                    {
                        return false;
                    }

                    if (!TryGetSubtreeBoundsMm(
                        result,
                        family,
                        subtreeMaxGeneration,
                        out double minX,
                        out double minY,
                        out double maxX,
                        out double maxY,
                        out count))
                    {
                        return false;
                    }

                    addedFamilyIds.Add(familyId);
                    map.SubTrees.Add(CreateBranchBlock(family, minX, minY, maxX, maxY, count));
                    return true;
                }

                // Cấp 1: chỉ tách box riêng cho nhánh đủ lớn; nhánh nhỏ coi như gộp vào phả cha.
                var currentLevelRoots = new List<FamilyViewModel>();
                foreach (var r in splitRoots)
                {
                    if (splitMetrics.SubtreeSize(r) < minBranchToSplitDeep)
                    {
                        continue;
                    }

                    if (TryAddBranch(r, out int _))
                    {
                        currentLevelRoots.Add(r);
                    }
                }

                // Chỉ nhánh có đoạn đủ dài mới mở rộng cấp sau (cùng logic report).
                var baseRootsForNext = currentLevelRoots
                    .Where(splitMetrics.CanContinueSplit)
                    .ToList();

                while (baseRootsForNext.Count > 0)
                {
                    var eligibleRootsAtLevel = splitMetrics.CollectNextSplitRoots(baseRootsForNext);

                    if (eligibleRootsAtLevel.Count == 0)
                    {
                        // Không còn nhánh tách ra nữa -> dừng, không vẽ thêm phả con.
                        break;
                    }

                    var nextBase = new List<FamilyViewModel>();
                    foreach (var r in eligibleRootsAtLevel)
                    {
                        if (TryAddBranch(r, out int _))
                        {
                            nextBase.Add(r);
                        }
                    }

                    baseRootsForNext = nextBase
                        .Where(splitMetrics.CanContinueSplit)
                        .ToList();
                }
            }

            map.SubTrees.Sort((a, b) =>
            {
                int w = b.WidthCm.CompareTo(a.WidthCm);
                if (w != 0)
                {
                    return w;
                }

                int c = b.NodeCount.CompareTo(a.NodeCount);
                return c != 0 ? c : a.FamilyId.CompareTo(b.FamilyId);
            });

            return map;
        }

        private static void AppendBranchBoundsLines(
            StringBuilder sb,
            GiaPhaRenderResult layout,
            IEnumerable<FamilyViewModel> branchRoots,
            int maxLines,
            bool onlySplittableBranches = false)
        {
            if (sb == null || layout == null || branchRoots == null)
            {
                return;
            }

            var rows = branchRoots
                .Select(r => new
                {
                    Id = r?.familyInfo?.FamilyId ?? 0,
                    MainName = GetFamilyMainPersonName(r),
                    Node = r
                })
                .Where(x => x.Id > 0 && x.Node != null)
                .Select(x =>
                {
                    TryGetSubtreeBoundsMm(
                        layout,
                        x.Node,
                        int.MaxValue,
                        out double minX,
                        out double minY,
                        out double maxX,
                        out double maxY,
                        out int count);
                    double wCm = Math.Max(0, maxX - minX) / 10.0;
                    return new
                    {
                        x.Id,
                        x.MainName,
                        Count = count,
                        Wcm = wCm,
                        Bounds = FormatBoundsCm(minX, minY, maxX, maxY)
                    };
                })
                .Where(x => onlySplittableBranches ? ShouldSplitPhaiCon(x.Count) : !ShouldSplitPhaiCon(x.Count))
                .OrderByDescending(x => x.Wcm)
                .ThenByDescending(x => x.Count)
                .ThenBy(x => x.Id)
                .Take(maxLines)
                .ToList();

            foreach (var row in rows)
            {
                sb.AppendLine("    + ID " + row.Id + " | " + row.MainName
                    + " | " + row.Count + " GD | " + row.Bounds);
            }
        }

        private static string BuildPhaDoSplitAnalysisReport(
            FamilyViewModel root,
            GiaPhaRenderResult layout,
            int minLevel,
            int maxLevel,
            int minCutLevel,
            int rootLevelMax = 4,
            int minBranchToSplitDeep = 500)
        {
            // Phân tích: đếm số gia đình theo đời + ước lượng size subtree + bounds layout (cm).
            if (root == null)
            {
                return "Không có dữ liệu.";
            }

            var nodes = new List<FamilyViewModel>();
            var stack = new Stack<FamilyViewModel>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur == null)
                {
                    continue;
                }
                nodes.Add(cur);
                if (cur.Children != null)
                {
                    // Push ngược để DFS ổn định.
                    for (int i = cur.Children.Count - 1; i >= 0; i--)
                    {
                        stack.Push(cur.Children[i]);
                    }
                }
            }

            if (nodes.Count == 0)
            {
                return "Không có dữ liệu.";
            }

            int observedMaxLevel = nodes.Max(n => n?.familyInfo?.FamilyLevel ?? 0);
            int effectiveMax = Math.Min(maxLevel, Math.Max(minLevel, observedMaxLevel));

            var nodesByLevel = new Dictionary<int, List<FamilyViewModel>>();
            for (int lvl = minLevel; lvl <= effectiveMax; lvl++)
            {
                nodesByLevel[lvl] = new List<FamilyViewModel>();
            }

            foreach (var n in nodes)
            {
                int lvl = n?.familyInfo?.FamilyLevel ?? 0;
                if (lvl < minLevel || lvl > effectiveMax)
                {
                    continue;
                }
                nodesByLevel[lvl].Add(n);
            }

            var splitMetrics = new PhaConSplitMetrics(
                effectiveMax,
                minBranchToSplitDeep,
                PhaDoTargetFamilyCountPerSubtree);
            int SubtreeSize(FamilyViewModel node) => splitMetrics.SubtreeSize(node);

            // Tìm đời cắt nhánh theo độ cân bằng kích thước, ưu tiên gần mục tiêu ~500 GD/nhánh.
            int bestLevel = -1;
            int bestCount = int.MaxValue;
            int bestMaxSubtree = int.MaxValue;
            double bestImbalance = double.MaxValue;
            double bestAverageGap = double.MaxValue;

            int startCut = Math.Max(minCutLevel, minLevel);
            for (int lvl = startCut; lvl <= effectiveMax; lvl++)
            {
                var roots = nodesByLevel[lvl];
                int count = roots.Count;
                if (count <= 1)
                {
                    continue;
                }
                int maxSub = 0;
                int eligibleCount = 0;
                var sizes = new List<int>(count);
                foreach (var r in roots)
                {
                    int size = SubtreeSize(r);
                    sizes.Add(size);
                    maxSub = Math.Max(maxSub, size);
                    if (size >= minBranchToSplitDeep)
                    {
                        eligibleCount++;
                    }
                }

                if (eligibleCount == 0)
                {
                    continue;
                }

                double median = Math.Max(1.0, ComputeMedian(sizes));
                double imbalance = maxSub / median;
                double average = sizes.Average();
                double averageGap = Math.Abs(average - PhaDoTargetFamilyCountPerSubtree);
                bool isBetter = imbalance < bestImbalance
                    || (Math.Abs(imbalance - bestImbalance) < 0.0001 && averageGap < bestAverageGap)
                    || (Math.Abs(imbalance - bestImbalance) < 0.0001
                        && Math.Abs(averageGap - bestAverageGap) < 0.0001
                        && count < bestCount)
                    || (Math.Abs(imbalance - bestImbalance) < 0.0001
                        && Math.Abs(averageGap - bestAverageGap) < 0.0001
                        && count == bestCount
                        && maxSub < bestMaxSubtree);
                if (isBetter)
                {
                    bestLevel = lvl;
                    bestCount = count;
                    bestMaxSubtree = maxSub;
                    bestImbalance = imbalance;
                    bestAverageGap = averageGap;
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("Tổng số gia đình: " + nodes.Count);
            sb.AppendLine("Đời tối đa quan sát: " + observedMaxLevel + " (giới hạn phân tích: " + effectiveMax + ")");
            sb.AppendLine("Mục tiêu mềm kích thước phả con: ~" + PhaDoTargetFamilyCountPerSubtree + " gia đình");
            sb.AppendLine("Ngưỡng tách sâu động: ≥ " + minBranchToSplitDeep + " gia đình");
            sb.AppendLine();
            sb.AppendLine("Số gia đình theo đời:");
            for (int lvl = minLevel; lvl <= effectiveMax; lvl++)
            {
                sb.AppendLine("- Đời " + lvl + ": " + nodesByLevel[lvl].Count);
            }

            if (!ShouldSplitPhaiCon(nodes.Count))
            {
                sb.AppendLine();
                sb.AppendLine("Tổng số gia đình < " + PhaDoMinFamilyCountToSplitPhaiCon
                    + " → không đề xuất tách phả con (giữ một phả).");
                if (layout != null)
                {
                    // Đo kích thước toàn phả (mọi đời) để khớp với sơ đồ root0 khi không tách.
                    if (TryBoundsForPlacedNodes(layout.Nodes, out double rMinX, out double rMinY, out double rMaxX, out double rMaxY, out int rCount))
                    {
                        sb.AppendLine("Kích thước toàn phả: " + rCount + " GD | "
                            + FormatBoundsCm(rMinX, rMinY, rMaxX, rMaxY));
                    }
                }
                return sb.ToString().TrimEnd();
            }

            sb.AppendLine();
            if (bestLevel <= 0)
            {
                sb.AppendLine("Không tìm thấy đời phù hợp để chia nhánh (cần đời có ≥ 2 gia đình).");
                sb.AppendLine("Gợi ý: thử tăng giới hạn đời hoặc chọn đời cắt thấp hơn/cao hơn.");
                return sb.ToString().TrimEnd();
            }

            sb.AppendLine("Gợi ý đời cắt nhánh: Đời " + bestLevel);
            sb.AppendLine("- Số root (phả con): " + bestCount);
            sb.AppendLine("- Subtree lớn nhất (ước lượng số gia đình trong 1 nhánh): " + bestMaxSubtree);
            sb.AppendLine("- Độ lệch cân bằng (max/median): " + bestImbalance.ToString("0.00"));

            if (layout != null)
            {
                var rootNodes = layout.Nodes
                    .Where(n => (n?.Family?.familyInfo?.FamilyLevel ?? 0) >= 1
                        && (n?.Family?.familyInfo?.FamilyLevel ?? 0) <= rootLevelMax)
                    .ToList();
                if (!TryCompactBoundsForLevelRange(layout, 1, rootLevelMax, out double rMinX, out double rMinY, out double rMaxX, out double rMaxY, out int rCount))
                {
                    TryBoundsForPlacedNodes(rootNodes, out rMinX, out rMinY, out rMaxX, out rMaxY, out rCount);
                }
                if (rCount > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Phả root0 (đời 1–" + rootLevelMax + "): " + rCount + " GD | "
                        + FormatBoundsCm(rMinX, rMinY, rMaxX, rMaxY));
                }

                sb.AppendLine();
                sb.AppendLine("Kích thước phả con tại đời " + bestLevel + " (layout thật, cm):");
                sb.AppendLine("  (chỉ nhánh ≥ " + minBranchToSplitDeep + " GD — đủ lớn để tách):");
                var rootsAtBestLevel = nodesByLevel[bestLevel];
                var eligibleAtBestLevel = rootsAtBestLevel.Where(r => SubtreeSize(r) >= minBranchToSplitDeep).ToList();
                var stoppedAtBestLevel = rootsAtBestLevel.Where(r => SubtreeSize(r) < minBranchToSplitDeep).ToList();
                AppendBranchBoundsLines(sb, layout, eligibleAtBestLevel, maxLines: 20, onlySplittableBranches: false);
                sb.AppendLine("  (nhánh nhỏ hơn ngưỡng — gộp vào phả cha, không tách box riêng):");
                AppendBranchBoundsLines(sb, layout, stoppedAtBestLevel, maxLines: 10, onlySplittableBranches: false);
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("Top nhánh lớn nhất ở đời " + bestLevel + " (~số GD, chưa đo cm):");
                var rootsAtBest = nodesByLevel[bestLevel]
                    .Select(r => new
                    {
                        Id = r?.familyInfo?.FamilyId ?? 0,
                        MainName = GetFamilyMainPersonName(r),
                        Size = SubtreeSize(r)
                    })
                    .OrderByDescending(x => x.Size)
                    .ThenBy(x => x.Id)
                    .Take(12)
                    .ToList();

                foreach (var r in rootsAtBest)
                {
                    sb.AppendLine("- ID " + r.Id + " | " + r.MainName + " | ~" + r.Size + " gia đình");
                }
            }

            sb.AppendLine();
            sb.AppendLine("Cách dùng:");
            sb.AppendLine("- Chỉ root đủ ngưỡng mới tách thành phả con riêng; root nhỏ sẽ gộp vào phả cha.");
            sb.AppendLine("- Nhánh nào ~size lớn nhất sẽ là trang nặng nhất.");

            // --- Phân tích sâu theo stride tự động ---
            sb.AppendLine();
            sb.AppendLine("Phân tích sâu theo cấp phả con (cùng thuật toán với sơ đồ vẽ):");
            sb.AppendLine("- Có box riêng: subtree >= " + minBranchToSplitDeep + " GD.");
            sb.AppendLine("- Tách tiếp cấp sau: đoạn từ node tới mốc phả con kế >= " + minBranchToSplitDeep
                + " GD (mục tiêu ~" + PhaDoTargetFamilyCountPerSubtree + ").");
            sb.AppendLine();

            int capIndex = 1;
            var rootsAtBestLevelForCaps = nodesByLevel[bestLevel]
                .Where(r => r != null)
                .ToList();
            var baseRootsForNext = rootsAtBestLevelForCaps
                .Where(splitMetrics.CanContinueSplit)
                .ToList();

            // Luôn in cấp 1; các cấp sau chỉ khi còn nhánh đủ đoạn tách tiếp.
            while (capIndex == 1 || baseRootsForNext.Count > 0)
            {
                if (capIndex > 1 && baseRootsForNext.Count == 0)
                {
                    break;
                }

                var rootsAtCap = capIndex == 1
                    ? rootsAtBestLevelForCaps
                    : splitMetrics.CollectNextSplitRoots(baseRootsForNext);
                if (rootsAtCap.Count == 0)
                {
                    break;
                }

                var eligibleForBox = rootsAtCap
                    .Where(r => SubtreeSize(r) >= minBranchToSplitDeep)
                    .ToList();
                var eligibleForContinue = rootsAtCap
                    .Where(splitMetrics.CanContinueSplit)
                    .ToList();
                var eligibleForBoxIds = new HashSet<int>(
                    eligibleForBox.Select(r => r?.familyInfo?.FamilyId ?? 0).Where(id => id > 0));
                var eligibleForContinueIds = new HashSet<int>(
                    eligibleForContinue.Select(r => r?.familyInfo?.FamilyId ?? 0).Where(id => id > 0));
                var stoppedAtCap = rootsAtCap
                    .Where(r =>
                    {
                        int id = r?.familyInfo?.FamilyId ?? 0;
                        return id > 0 && !eligibleForBoxIds.Contains(id);
                    })
                    .ToList();
                var boxOnlyNoContinue = eligibleForBox
                    .Where(r =>
                    {
                        int id = r?.familyInfo?.FamilyId ?? 0;
                        return id > 0 && eligibleForBoxIds.Contains(id) && !eligibleForContinueIds.Contains(id);
                    })
                    .ToList();

                int stride = ComputeAdaptiveSplitStride(
                    eligibleForContinue.Count > 0
                        ? (int)Math.Round(
                            eligibleForContinue.Average(r => splitMetrics.SegmentSizeToNextSplit(r)),
                            MidpointRounding.AwayFromZero)
                        : PhaDoTargetFamilyCountPerSubtree);
                int minLevelAtCap = rootsAtCap.Min(r => r?.familyInfo?.FamilyLevel ?? 0);
                int maxLevelAtCap = rootsAtCap.Max(r => r?.familyInfo?.FamilyLevel ?? 0);
                string levelLabel = minLevelAtCap == maxLevelAtCap
                    ? ("đời " + minLevelAtCap)
                    : ("đời " + minLevelAtCap + "–" + maxLevelAtCap);

                sb.AppendLine("• Cấp " + capIndex + " (" + levelLabel + ", stride tham chiếu: +" + stride + "): "
                    + rootsAtCap.Count + " gia đình phả con");
                sb.AppendLine("  - Đủ ngưỡng có box riêng (>= " + minBranchToSplitDeep + "): " + eligibleForBox.Count);
                sb.AppendLine("  - Đủ đoạn tách tiếp (>= " + minBranchToSplitDeep + "): " + eligibleForContinue.Count);
                sb.AppendLine("  - Nhánh dừng (không đủ box): " + stoppedAtCap.Count);
                if (boxOnlyNoContinue.Count > 0)
                {
                    sb.AppendLine("  - Có box nhưng đoạn quá ngắn, không tách cấp sau: " + boxOnlyNoContinue.Count);
                }

                var topSegment = eligibleForContinue
                    .Select(r => new
                    {
                        Id = r?.familyInfo?.FamilyId ?? 0,
                        MainName = GetFamilyMainPersonName(r),
                        Size = splitMetrics.SegmentSizeToNextSplit(r)
                    })
                    .OrderByDescending(x => x.Size)
                    .ThenBy(x => x.Id)
                    .Take(8)
                    .ToList();
                if (topSegment.Count > 0)
                {
                    sb.AppendLine("  - Nhánh lớn nhất (đoạn tới mốc tách kế, chỉ nhánh đủ tách tiếp):");
                    foreach (var t in topSegment)
                    {
                        sb.AppendLine("    + ID " + t.Id + " | " + t.MainName + " | ~" + t.Size);
                    }
                }
                else
                {
                    sb.AppendLine("  - Không có nhánh nào đủ đoạn để tách tiếp.");
                }

                if (layout != null)
                {
                    sb.AppendLine("  - Kích thước nhánh có box (layout thật, cm):");
                    AppendBranchBoundsLines(sb, layout, eligibleForBox, maxLines: 12, onlySplittableBranches: false);
                    if (stoppedAtCap.Count > 0)
                    {
                        sb.AppendLine("  - Nhánh dừng (không đủ box):");
                        AppendBranchBoundsLines(sb, layout, stoppedAtCap, maxLines: 8, onlySplittableBranches: false);
                    }
                    if (boxOnlyNoContinue.Count > 0)
                    {
                        sb.AppendLine("  - Có box nhưng không tách tiếp (đoạn < " + minBranchToSplitDeep + "):");
                        AppendBranchBoundsLines(sb, layout, boxOnlyNoContinue, maxLines: 8, onlySplittableBranches: false);
                    }
                }

                sb.AppendLine();

                baseRootsForNext = eligibleForContinue;
                if (baseRootsForNext.Count == 0)
                {
                    sb.AppendLine("  - Không còn nhánh đủ lớn ở cấp tiếp theo -> ẩn cấp sau trên diagram.");
                    break;
                }
                capIndex++;
            }

            return sb.ToString().TrimEnd();
        }

        private async Task<GiaPhaRenderResult> RenderPhaDoCoreAsync(
            bool resetZoom,
            bool resetScroll,
            FamilyViewModel rootOverride = null,
            int maxGenerationInclusive = int.MaxValue)
        {
            var root = rootOverride ?? viewModel.FamilyTree?.Family?.RootPerson;
            if (root == null)
            {
                return null;
            }

            var options = BuildPhaDoRenderOptions();
            _phaDoCurrentOptions = options;

            if (resetZoom)
            {
                ResetPhaDoZoom();
            }

            var baseResult = await GiaPhaRenderService.ComputeLayoutAsync(root, options).ConfigureAwait(true);
            CapturePhaDoBaseLayout(baseResult);

            GiaPhaRenderResult result = GiaPhaManualLayoutService.ApplyManualOffsets(
                baseResult,
                _phaDoOffsetXmmByFamilyId,
                _phaDoOffsetYmmByFamilyId);
            ApplyCustomBoxSizesFromStyles(result);
            // Cắt theo cấp root đang chọn: chỉ giữ node tới mốc đời yêu cầu.
            TrimRenderResultByMaxGeneration(result, maxGenerationInclusive);
            GiaPhaManualLayoutService.RebuildConnectorsOnly(result);
            GiaPhaRenderBoundsFitter.FitCanvasToContent(result);

            await Dispatcher.InvokeAsync(
                () => GiaPhaRenderService.PaintToCanvas(theCanvas, result),
                DispatcherPriority.Background).Task.ConfigureAwait(true);

            _phaDoRenderedLayout = result;
            RefreshAllBoxStylesOnCanvas();
            RefreshAllPersonOffsetsOnCanvas();

            if (_phaDoSelectedFamilyId > 0)
            {
                DrawSelectionOverlay(_phaDoSelectedFamilyId);
                UpdatePhaDoSelectedBoxSizeStatus(_phaDoSelectedFamilyId);
            }

            if (resetScroll)
            {
                var scroll = phaDoScrollViewer ?? FindParent<ScrollViewer>(theCanvas);
                if (scroll != null)
                {
                    scroll.ScrollToHorizontalOffset(0);
                    scroll.ScrollToVerticalOffset(0);
                }
            }

            return result;
        }

        void searchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                viewModel.FamilyTree.Family.SearchCommand.Execute(null);
            }
        }
        //private double treeViewHorizScrollPos = 0.0;
        //private bool treeViewResetHorizScroll = false;
        //private ScrollViewer treeViewScrollViewer = null;
        private bool mSuppressRequestBringIntoView;

        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            // Ignore re-entrant calls
            if (mSuppressRequestBringIntoView)
                return;

            // Cancel the current scroll attempt
            e.Handled = true;

            // Call BringIntoView using a rectangle that extends into "negative space" to the left of our
            // actual control. This allows the vertical scrolling behaviour to operate without adversely
            // affecting the current horizontal scroll position.
            mSuppressRequestBringIntoView = true;

            TreeViewItem tvi = sender as TreeViewItem;
            if (tvi != null)
            {
                Rect newTargetRect = new Rect(-200, 0, tvi.ActualWidth + 1000, tvi.ActualHeight);
                tvi.BringIntoView(newTargetRect);
            }

            mSuppressRequestBringIntoView = false;
        }

        // Correctly handle programmatically selected items
        private void OnSelected(object sender, RoutedEventArgs e)
        {
            TreeViewItem tvi = ((TreeViewItem)sender);
            tvi.BringIntoView();
            FamilyViewModel personModel = (FamilyViewModel)tvi.DataContext;
            viewModel.FamilyTree.Family.SelectedFamily = personModel;
            log.Info("Chọn trên cây: " + viewModel.FamilyTree.Family.SelectedFamily.Name);
            if (tabControl.SelectedIndex != 1)
            {
                tabControl.SelectedIndex = 1;
            }
            e.Handled = true;
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            TreeViewItem tvi = ((TreeViewItem)sender);
            FamilyViewModel personModel = (FamilyViewModel)tvi.DataContext;
            e.Handled = true;
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            
        }
        private void TreeViewItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var treeViewItem = VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject) as TreeViewItem;
            if (treeViewItem != null)
            {
                treeViewItem.Focus();
                e.Handled = true;
            }
        }
        static T VisualUpwardSearch2<T>(DependencyObject source) where T : DependencyObject
        {
            DependencyObject returnVal = source;

            while (returnVal != null && !(returnVal is T))
            {
                DependencyObject tempReturnVal = null;
                if (returnVal is Visual || returnVal is Visual3D)
                {
                    tempReturnVal = VisualTreeHelper.GetParent(returnVal);
                }
                if (tempReturnVal == null)
                {
                    returnVal = LogicalTreeHelper.GetParent(returnVal);
                }
                else returnVal = tempReturnVal;
            }

            return returnVal as T;
        }

        static DependencyObject VisualUpwardSearch<T>(DependencyObject source)
        {
            while (source != null && source.GetType() != typeof(T))
                source = VisualTreeHelper.GetParent(source);

            return source;
        }
        private void Treeview_Family_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Auto select người trong gia đình
            if (viewModel.FamilyTree.Family.SelectedFamily != null )
            {
                //viewModel.FamilyTree.Family.SelectedFamily.DebugFamilyClickFunc();
            }
        }
        private void ListView_ListGiaDinhCon_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (((ListView)sender).SelectedItem==null)
            {
                return;
            }
            viewModel.FamilyTree.Family.SelectedFamily = ((ListView)sender).SelectedItem as FamilyViewModel;
            // Auto select người trong gia đình
            if(viewModel.FamilyTree.Family.SelectedFamily!=null && 
                viewModel.FamilyTree.Family.SelectedFamily.Children.Count > 0)
            {
                viewModel.FamilyTree.Family.SelectedPerson = viewModel.FamilyTree.Family.SelectedFamily.ListPerson.First();
                viewModel.FamilyTree.Family.SelectedFamily.IsExpanded = true;
                viewModel.FamilyTree.Family.SelectedFamily.IsSelected = true;
                log.Info("Chọn người: " + viewModel.FamilyTree.Family.SelectedPerson.MANS_NAME_HUY);
            }
        }

        private void ListView_ListNguoiTrongGiaDinhChaMe_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

            PersonInfo listViewItem = ((ListView)sender).SelectedItem as PersonInfo;
            if (listViewItem != null && viewModel.FamilyTree.Family.SelectedFamily != null)
            {
                viewModel.FamilyTree.Family.SelectedFamily = viewModel.FamilyTree.Family.SelectedFamily.Parent;
                viewModel.FamilyTree.Family.SelectedFamily.IsExpanded = true;
                viewModel.FamilyTree.Family.SelectedFamily.IsSelected = true;
                log.Info("Chọn gia đình cha mẹ: " + viewModel.FamilyTree.Family.SelectedPerson.MANS_NAME_HUY);
            }
        }

        private void ListView_ListNguoiTrongGiaDinh_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            PersonInfo personInfo = ((ListView)sender).SelectedItem as PersonInfo;
            if (personInfo != null)
            {
                viewModel.FamilyTree.Family.SelectedPerson = personInfo;
                viewModel.FamilyTree.Family.SelectedFamily.IsExpanded = true;
                viewModel.FamilyTree.Family.SelectedFamily.IsSelected = true;
                log.Info("Chọn người: " + viewModel.FamilyTree.Family.SelectedPerson.MANS_NAME_HUY);
            }
        }

        private void ListView_ListNguoiTrongGiaDinh_MouseDown_1(object sender, MouseButtonEventArgs e)
        {
            PersonInfo personInfo = ((ListView)sender).SelectedItem as PersonInfo;
            if (personInfo != null)
            {
                viewModel.FamilyTree.Family.SelectedPerson = personInfo;
                log.Info("Chọn người: " + viewModel.FamilyTree.Family.SelectedPerson.MANS_NAME_HUY);
            }
        }

        private void htmlViewer_PhaKy_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            
        }

        private void MetroWindow_Initialized(object sender, EventArgs e)
        {
        }
        
        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            //this.viewModel.AddItemLogs("Begin login");
        }

        private async void BtnDownloadGiaPha_Click(object sender, RoutedEventArgs e)
        {
            if(viewModel.FamilyTree.Username.Trim().Length==0 || viewModel.FamilyTree.Password.Trim().Length == 0)
            {
                MessageBox.Show("Nhập user name và password của trang web vietnamgiapha.com");
                return;
            }
            if (MessageBox.Show("Xác nhận: Download gia phả từ web vietnamgiapha." + Environment.NewLine +
                "Khi download về, sẽ đè bẹp lên gia phả đang làm ?", "Xác nhận", MessageBoxButton.OKCancel) == MessageBoxResult.Cancel)
            {
                return;
            }
            //_progressDialogController = await _dialogCoordinator.ShowProgressAsync(this, "Please wait", null, true);
            //_progressDialogController.SetIndeterminate();
            //_progressDialogController.Canceled += ProgressDialogControllerCanceled;
            //await GetResultListTask();
            //await _progressDialogController.CloseAsync();
            //_progressDialogController.Canceled -= ProgressDialogControllerCanceled;
            //_progressDialogController = null;
            var _progressDialogController = await this.ShowProgressAsync("Đợi download từ web...", "Đang download từ web vietnamgiapha.com");
            _progressDialogController.SetProgress(0);
            _progressDialogController.SetIndeterminate();
            
            try
            {

                //BtnDownloadGiaPha.IsEnabled = false;
                GiaphaInfo gp = await Database.Download(viewModel.FamilyTree.Username.Trim().ToLower(), viewModel.FamilyTree.Password.Trim());
                _progressDialogController.SetProgress(1);
                if (gp != null)
                {
                    await viewModel.UpdateGiaPhaAsync(gp).ConfigureAwait(true);
                    //viewModel.FamilyTree = new GiaPhaViewModel(gp);
                    UpdateHtmlGiaPha();
                    //viewModel.FamilyTree.OnPropertyChanged("FamilyTree");
                    
                    log.Info("BtnDownloadGiaPha_Click: download từ web ngon lành ");
                    MessageBox.Show("Download từ web xong", "Ngon lành cành đào");
                    viewModel.AddUserAction("Download gia phả xong, ngon lành");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi download từ web: " + ex.Message, "Có Lỗi");
                log.Error("BtnDownloadGiaPha_Click: Lỗi download từ web");
                log.Error(ex);
            }
            //BtnDownloadGiaPha.IsEnabled = true;
            await _progressDialogController.CloseAsync();
        }

        private async void BtnUploadGiaPha_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.FamilyTree.Username.Trim().Length == 0 || viewModel.FamilyTree.Password.Trim().Length == 0)
            {
                MessageBox.Show("Nhập user name và password của trang web vietnamgiapha.com");
                return;
            }
            if (viewModel.FamilyTree.CheckValid().Length>0)
            {
                MessageBox.Show(viewModel.FamilyTree.CheckValid(), "Có lỗi");
                return;
            }

            if (viewModel.FamilyTree.GP.GiaphaId == 0)
            {
            }
            var _progressDialogController = await this.ShowProgressAsync("Đợi upload lên web...", "Đang upload từ web vietnamgiapha.com");
            _progressDialogController.SetProgress(0);
            _progressDialogController.SetIndeterminate();

            try
            {
                //BtnDownloadGiaPha.IsEnabled = false;
                string json = await Database.UploadWeb(
                    viewModel.FamilyTree.Username.Trim().ToLower(), 
                    viewModel.FamilyTree.Password.Trim(),
                    viewModel.FamilyTree.ToJson()
                    );
                JsonObject objData = (JsonObject)JsonObject.Parse(json);
                _progressDialogController.SetProgress(1);
                if ( Convert.ToInt32 (objData["code"].ToString()) == 0)
                {
                    log.Info("BtnUploadGiaPha_Click: upload lên web success: ");
                    //MessageBox.Show(gp, "Upload");
                    // Do reload gia phả
                    GiaphaInfo gp = Database.ParseJsonGiaPha(objData);
                    if (gp != null)
                    {
                        await viewModel.UpdateGiaPhaAsync(gp).ConfigureAwait(true);
                        UpdateHtmlGiaPha();
                        log.Info("BtnUploadGiaPha_Click: update lên web ngon lành ");
                        viewModel.AddUserAction("Update gia phả xong, ngon lành");
                        if ( MessageBox.Show("Update lên web xong, muốn coi lại không ???", "Ngon lành cành đào", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                        {
                            Process.Start(new ProcessStartInfo("https://www.vietnamgiapha.com/XemPhaHe/" + viewModel.FamilyTree.GiaphaId + "/gp.html"));
                        }
                    }
                }
                else
                {
                    //
                    MessageBox.Show("Lỗi upload web: " + Convert.ToInt32(objData["code"].ToString()) + Environment.NewLine +
                        objData["msg"].ToString()
                        , "Có Lỗi");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi upload web: " + ex.Message, "Có Lỗi");
                log.Error("BtnUploadGiaPha_Click: Lỗi upload web: " + ex.Message);
                log.Error(ex);
            }
            //BtnDownloadGiaPha.IsEnabled = true;
            await _progressDialogController.CloseAsync();
        }

        private void ListViewItem_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Delete)
                {
                    // XOA
                    if (ListView_ListNguoiTrongGiaDinh.SelectedItem != null)
                    {
                        PersonInfo obj = (PersonInfo)ListView_ListNguoiTrongGiaDinh.SelectedItem;
                        if (MessageBox.Show("Xóa [" + obj.MANS_NAME_HUY + "] ra khỏi gia đình này ?", "Xác Nhận", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                        {
                            if (obj._familyInfo.ListPerson.IndexOf(obj) > -1)
                            {
                                obj._familyInfo.ListPerson.Remove(obj);
                                obj._familyInfo.OnPropertyChanged("Name");
                                log.Info("Xóa [" + obj.MANS_NAME_HUY + "] ra khỏi gia đình");
                                viewModel.AddUserAction("Xóa [" + obj.MANS_NAME_HUY + "] ra khỏi gia đình " + obj._familyInfo.Name0);
                            }
                        }
                    }
                }
                else if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
                {

                    if (e.Key == Key.Up)
                    {
                        // SHILF UP//
                        if (ListView_ListNguoiTrongGiaDinh.SelectedItem != null)
                        {
                            // Chọn người
                            PersonInfo obj = (PersonInfo)ListView_ListNguoiTrongGiaDinh.SelectedItem;
                            // XOa người ra khỏi list
                            int index = obj._familyInfo.ListPerson.IndexOf(obj);
                            if (index > 1 && index < obj._familyInfo.ListPerson.Count)
                            {
                                obj._familyInfo.ListPerson.Remove(obj);
                                //Thêm vô phía trên
                                obj._familyInfo.ListPerson.Insert(index - 1, obj);

                                viewModel.AddUserAction("Chỉnh lên trước [" + obj.MANS_NAME_HUY + "] " + obj._familyInfo.Name0);
                            }
                        }
                    }
                    else if (e.Key == Key.Down)
                    {
                        // DOWN
                        if (ListView_ListNguoiTrongGiaDinh.SelectedItem != null)
                        {
                            // Chọn người
                            PersonInfo obj = (PersonInfo)ListView_ListNguoiTrongGiaDinh.SelectedItem;
                            // XOa người ra khỏi list
                            int index = obj._familyInfo.ListPerson.IndexOf(obj);
                            if (index > 0 && index < obj._familyInfo.ListPerson.Count)
                            {
                                obj._familyInfo.ListPerson.Remove(obj);
                                //Thêm vô phía trên
                                obj._familyInfo.ListPerson.Insert(index + 1, obj);

                                viewModel.AddUserAction("Chỉnh xuống dưới [" + obj.MANS_NAME_HUY + "] " + obj._familyInfo.Name0);
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                log.Error("ListViewItem_PreviewKeyDown: " + ex.Message);
                log.Error(ex);
            }
            
        }
        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void Hyperlink_RequestNavigate_1(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                if (Convert.ToInt32(viewModel.FamilyTree.GiaphaId) > 0)
                {
                    Process.Start(new ProcessStartInfo("https://www.vietnamgiapha.com/XemPhaHe/" + viewModel.FamilyTree.GiaphaId + "/gp.html"));
                    e.Handled = true;
                    return;
                }
            }
            catch (Exception ex)
            {

            }
            e.Handled = false;
        }

        private void ToggleSwitch_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            log.Error("OKOK");
        }

        private void ToggleSwitch_DataContextChanged_1(object sender, DependencyPropertyChangedEventArgs e)
        {

        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            //log.Error("OKOK");
            PersonInfo personInfo = ((ToggleSwitch)sender).DataContext as PersonInfo;
            if (personInfo.IsMainPerson == 0)
            {
                if (personInfo._familyInfo != null && personInfo._familyInfo.ListPerson != null)
                {
                    int countMain = 0;
                    for (int i = 0; i < personInfo._familyInfo.ListPerson.Count; i++)
                    {
                        countMain += personInfo._familyInfo.ListPerson[i].IsMainPerson;
                    }
                    if (countMain == 0)
                    {
                        // NO main person
                        personInfo.IsMainPerson = 1;
                    }
                }
                return;
            }
            if( personInfo._familyInfo!=null && personInfo._familyInfo.ListPerson!=null)
            {
                int countMain = 0;
                foreach (var person in personInfo._familyInfo.ListPerson)
                {
                    if( person.IsMainPerson==1)
                    {
                        countMain++;
                    }
                }

                var list = personInfo._familyInfo.ListPerson.OrderByDescending(v=> v.IsMainPerson).ToList();
                personInfo._familyInfo.ListPerson.Clear();
                for(int i=0;i<list.Count; i++)
                {
                    if( i==0)
                    {
                        personInfo._familyInfo.ListPerson.Add(list[i]);
                    }
                    else
                    {
                        list[i].MANS_GENDER = list[0].IsGioiTinhNam == 1 ? "Nữ" : "Nam";
                        personInfo._familyInfo.ListPerson.Add(list[i]);
                    }
                }
            }

            // "Nữ" + "Nam" 
        }

        private void ToggleSwitch_GioiTinh_Toggled(object sender, RoutedEventArgs e)
        {
            //log.Error("OKOK");
            PersonInfo personInfo = ((ToggleSwitch)sender).DataContext as PersonInfo;
            if (personInfo == null)
            {
                return;
            }
            if (personInfo.IsMainPerson==1 && personInfo._familyInfo != null && personInfo._familyInfo.ListPerson != null)
            {
                for (int i = 1; i < personInfo._familyInfo.ListPerson.Count; i++)
                {
                    personInfo._familyInfo.ListPerson[i].MANS_GENDER = personInfo.IsGioiTinhNam == 1 ? "Nữ" : "Nam";
                }
            }
        }

        private async void commandAutoChinh_Click(object sender, RoutedEventArgs e)
        {
            if(viewModel.StringAutoNameButton == "Undo Chỉnh")
            {
                // UNDO
                string defaultSaveFolder = ConfigurationManager.AppSettings["defaultSaveFolder"];
                string undo_filename = defaultSaveFolder + "\\auto_undo.json";
                try
                {
                    GiaphaInfo loadedUndo = null;
                    GiaphaInfo gp = await LoadGiaPhaFromJsonWithProgressAsync(
                        undo_filename,
                        "Đang mở file undo...",
                        "Đang đọc auto_undo.json...\n\nĐã chờ: 0 giây",
                        async loaded =>
                        {
                            loadedUndo = loaded;
                            await viewModel.UpdateGiaPhaAsync(loaded, saveToJson: false).ConfigureAwait(true);
                        }).ConfigureAwait(true);
                    if (gp != null)
                    {
                        if (loadedUndo == null)
                        {
                            await viewModel.UpdateGiaPhaAsync(gp, saveToJson: false).ConfigureAwait(true);
                        }
                        log.Info("OpenFileCommandFunc: Mở file UNDO xong: " + undo_filename);
                        MessageBox.Show("Đã UNDO tác vụ chỉnh tự động. Mọi thay đổi đã hoàn tác");
                        viewModel.StringAutoNameButton = "Tự Động Chỉnh";
                    }
                    else
                    {
                        MessageBox.Show("Lỗi mở file : " + undo_filename, "Có Lỗi");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi mở file : " + ex.Message, "Có Lỗi");
                    log.Error("OpenFileCommandFunc: Lỗi file: " + undo_filename);
                    log.Error(ex);
                }
            }
            else
            {
                if (viewModel.StringAutoName != null && viewModel.StringAutoName.Length > 0)
                {
                    viewModel.listStringAutoName.Clear();
                    // Do search
                    // Save undo file
                    Database.SaveJson(viewModel.FamilyTree, "auto_undo.json");
                    string message = "";
                    FamilyViewModel.AutoCorrect(viewModel.FamilyTree.FamilyViewModelRoot, ref message, viewModel.StringAutoName);
                    if (message.Length > 0)
                    {
                        viewModel.listStringAutoName = new ObservableCollection<string>(message.Split(new string[] { Environment.NewLine }, StringSplitOptions.None).ToList());
                        //
                        viewModel.StringAutoNameButton = "Undo Chỉnh";
                        MessageBox.Show("Đã tự động điều chỉnh, kiếm tra nếu có sai nhiều quá, thì bấm [Undo Chỉnh]. Hoặc mở lại file [auto_undo.json]");

                        viewModel.AddUserAction("Chạy xong tự động chỉnh");
                    }
                    else
                    {

                    }
                }
                else
                {
                    viewModel.listStringAutoName.Insert(0, "Có lỗi: Nhập tên tộc họ");
                }
            }
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var root = viewModel.FamilyTree?.Family?.RootPerson;
            if (root == null)
            {
                MessageBox.Show("Chưa có dữ liệu gia phả để vẽ.", "Có lỗi");
                return;
            }

            try
            {
                // Đổi file/load cây mới thì danh sách phả con cũ không còn hợp lệ, trả về mặc định Toàn phả.
                int currentRootId = root.familyInfo?.FamilyId ?? 0;
                if (_phaDoRenderScopeSourceRootId != currentRootId || _phaDoRenderScopes.Count == 0)
                {
                    ResetPhaDoRenderScopes(root);
                }

                // Luôn lấy scope từ item đang chọn; nếu chưa chọn thì fallback về item đầu (Toàn phả).
                var selectedScope = phaDoSubtreeListBox?.SelectedItem as PhaDoRenderScopeItem;
                if (selectedScope == null || !_phaDoRenderScopes.Contains(selectedScope))
                {
                    selectedScope = _phaDoRenderScopes.FirstOrDefault();
                    if (phaDoSubtreeListBox != null)
                    {
                        phaDoSubtreeListBox.SelectedItem = selectedScope;
                    }
                }
                FamilyViewModel renderRoot = selectedScope?.RootFamily ?? root;
                int maxGeneration = selectedScope?.MaxGenerationInclusive ?? int.MaxValue;
                var result = await RunPhaDoRenderWithWaitDialogAsync(
                    resetZoom: true,
                    resetScroll: true,
                    rootOverride: renderRoot,
                    maxGenerationInclusive: maxGeneration).ConfigureAwait(true);
                if (result == null)
                {
                    return;
                }

                SaveWorkspaceSession();

                string layoutMode = DescribePhaDoCardLayoutIndex(GetPhaDoCardLayoutListIndex());
                string scopeLabel = selectedScope?.Label ?? "Toàn phả";
                viewModel.AddUserAction(
                    "Vẽ phả hệ [" + scopeLabel + "] (" + layoutMode + "): " + result.Nodes.Count + " gia đình, " + result.SizeSummary);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi vẽ phả hệ: " + ex.Message, "Có Lỗi");
                log.Error("vẽ phả hệ", ex);
            }
        }

        /// <summary>Lấy gia đình theo ID từ root hiện tại để map block phả con sang node thật.</summary>
        private static FamilyViewModel FindFamilyById(FamilyViewModel root, int familyId)
        {
            if (root == null || familyId <= 0)
            {
                return null;
            }

            var stack = new Stack<FamilyViewModel>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur == null)
                {
                    continue;
                }

                if ((cur.familyInfo?.FamilyId ?? 0) == familyId)
                {
                    return cur;
                }

                if (cur.Children == null)
                {
                    continue;
                }

                foreach (var child in cur.Children)
                {
                    stack.Push(child);
                }
            }

            return null;
        }

        /// <summary>Giữ lại node đến một đời nhất định để vẽ theo scope Root0/Root1.</summary>
        private static void TrimRenderResultByMaxGeneration(GiaPhaRenderResult result, int maxGenerationInclusive)
        {
            if (result == null || maxGenerationInclusive <= 0 || maxGenerationInclusive == int.MaxValue)
            {
                return;
            }

            for (int i = result.Nodes.Count - 1; i >= 0; i--)
            {
                int level = result.Nodes[i]?.Family?.familyInfo?.FamilyLevel ?? 0;
                if (level > maxGenerationInclusive)
                {
                    result.Nodes.RemoveAt(i);
                }
            }

            for (int i = result.GenerationBands.Count - 1; i >= 0; i--)
            {
                if (result.GenerationBands[i].Level > maxGenerationInclusive)
                {
                    result.GenerationBands.RemoveAt(i);
                }
            }
        }

        /// <summary>Khởi tạo danh sách mặc định chỉ gồm Toàn phả cho cây hiện tại.</summary>
        private void ResetPhaDoRenderScopes(FamilyViewModel root)
        {
            _phaDoRenderScopes.Clear();
            _phaDoRenderScopeSourceRootId = root?.familyInfo?.FamilyId ?? 0;
            if (root == null)
            {
                return;
            }

            _phaDoRenderScopes.Add(new PhaDoRenderScopeItem
            {
                Label = "Toàn phả",
                FamilyId = _phaDoRenderScopeSourceRootId,
                RootFamily = root,
                IsWholeTree = true,
                MaxGenerationInclusive = int.MaxValue
            });
            if (phaDoSubtreeListBox != null)
            {
                phaDoSubtreeListBox.SelectedIndex = 0;
            }
        }

        /// <summary>Đổ list lựa chọn vẽ theo cấp root: Toàn phả, Root0 và các nhánh Root1.</summary>
        private void UpdatePhaDoRenderScopesFromMap(
            FamilyViewModel root,
            PhaDoSubtreeMap map,
            int splitLevel,
            int subtreeMaxGeneration,
            int minBranchToSplitDeep)
        {
            ResetPhaDoRenderScopes(root);
            if (root == null || map?.SubTrees == null || map.SubTrees.Count == 0 || splitLevel <= 0)
            {
                return;
            }

            // Root0: vẽ từ gốc đến mốc root1 rồi dừng.
            _phaDoRenderScopes.Add(new PhaDoRenderScopeItem
            {
                Label = "Phả con root 0 (đến root 1)",
                FamilyId = root.familyInfo?.FamilyId ?? 0,
                RootFamily = root,
                IsWholeTree = false,
                MaxGenerationInclusive = splitLevel
            });

            var splitMetrics = new PhaConSplitMetrics(
                subtreeMaxGeneration,
                minBranchToSplitDeep,
                PhaDoTargetFamilyCountPerSubtree);
            var seen = new HashSet<int>();
            foreach (var block in map.SubTrees)
            {
                int familyId = block?.FamilyId ?? 0;
                if (familyId <= 0 || !seen.Add(familyId))
                {
                    continue;
                }

                var family = FindFamilyById(root, familyId);
                if (family == null)
                {
                    continue;
                }

                int level = family.familyInfo?.FamilyLevel ?? 0;
                // Chỉ đưa nhánh root1 vào list; khi vẽ root1 sẽ dừng ở mốc root2 của chính nhánh đó.
                if (level != splitLevel)
                {
                    continue;
                }

                int maxGenerationInclusive = int.MaxValue;
                if (splitMetrics.TrySelectNextSplitLevel(family, out int nextSplitLevel, out _, out _))
                {
                    maxGenerationInclusive = nextSplitLevel;
                }

                _phaDoRenderScopes.Add(new PhaDoRenderScopeItem
                {
                    Label = "Phả con root 1 | ID " + familyId + " | " + GetFamilyMainPersonName(family),
                    FamilyId = familyId,
                    RootFamily = family,
                    IsWholeTree = false,
                    MaxGenerationInclusive = maxGenerationInclusive
                });
            }
        }

        private void ApplySelectedBoxEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_phaDoSelectedFamilyId <= 0 || _phaDoRenderedLayout == null)
            {
                MessageBox.Show("Hãy chọn 1 box trước.", "Thông báo");
                return;
            }

            var node = FindNodeByFamilyId(_phaDoSelectedFamilyId);
            if (node?.Family == null)
            {
                MessageBox.Show("Không tìm thấy box đã chọn.", "Thông báo");
                return;
            }



            RefreshSelectedPhaDoFamilyBox();
            SaveWorkspaceSession();
        }

        private void TheCanvas_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!TryResolveFamilyFromCanvasHit(e.OriginalSource as DependencyObject, out int familyId, out GiaPhaPlacedNode node))
            {
                return;
            }

            SelectPhaDoBoxOutline(familyId);

            var menu = new ContextMenu();
            var viewDetail = new MenuItem { Header = "Xem gia đình chi tiết" };
            viewDetail.Click += (s, args) => OpenFamilyDetailFromPhaDo(node.Family);
            var editStyle = new MenuItem { Header = "Chỉnh màu box, font chữ" };
            editStyle.Click += (s, args) => ShowPhaDoBoxStyleDialog(familyId);

            var applyLayoutLevel = new MenuItem { Header = "Áp layout cho đời" };
            applyLayoutLevel.Click += (s, args) => ApplyBoxLayoutToLevel_Click(familyId);

            var applyLayoutAll = new MenuItem { Header = "Áp layout cho toàn gia phả" };
            applyLayoutAll.Click += (s, args) => ApplyBoxLayoutToAllGiaPha_Click(familyId);

            menu.Items.Add(viewDetail);
            menu.Items.Add(editStyle);
            menu.Items.Add(new Separator());
            menu.Items.Add(applyLayoutLevel);
            menu.Items.Add(applyLayoutAll);
            menu.PlacementTarget = theCanvas;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
            e.Handled = true;
        }

        private void TheCanvas_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (TryResolveResizeHandle(e.OriginalSource as DependencyObject, out int resizeFamilyId, out PhaDoResizeCorner corner))
            {
                BeginPhaDoResize(e, resizeFamilyId, corner);
                e.Handled = true;
                return;
            }

            if (TryResolvePersonElementHit(
                    e.OriginalSource as DependencyObject,
                    out int personFamilyId,
                    out int personSlot,
                    out FrameworkElement personElement))
            {
                BeginPersonDrag(e, personFamilyId, personSlot, personElement);
                e.Handled = true;
                return;
            }

            if (!TryResolveFamilyFromCanvasHit(e.OriginalSource as DependencyObject, out int familyId, out GiaPhaPlacedNode node))
            {
                _phaDoSelectedFamilyId = 0;
                _phaDoSelectedPersonSlot = -1;
                ClearSelectionOverlay();
                UpdatePhaDoSelectedBoxSizeStatus(0);
                BeginPhaDoPan(e, MouseButton.Left);
                e.Handled = true;
                return;
            }

            _phaDoSelectedPersonSlot = -1;
            SelectPhaDoBoxOutline(familyId);

            if (!IsHitOnBoxBackground(e.OriginalSource as DependencyObject, familyId))
            {
                e.Handled = true;
                return;
            }

            _phaDoIsDragging = true;
            _phaDoMouseMovedWhileDrag = false;
            _phaDoDraggingFamilyId = familyId;
            _phaDoDragStartPoint = e.GetPosition(theCanvas);
            _phaDoDragStartNodeXmm = node.Xmm;
            theCanvas.CaptureMouse();
            e.Handled = true;
        }

        private void TheCanvas_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && e.MiddleButton == MouseButtonState.Pressed)
            {
                BeginPhaDoPan(e, MouseButton.Middle);
                e.Handled = true;
            }
        }

        private void BeginPhaDoPan(MouseButtonEventArgs e, MouseButton button)
        {
            if (phaDoScrollViewer == null || _phaDoIsPanning)
            {
                return;
            }

            _phaDoIsPanning = true;
            _phaDoPanMoved = false;
            _phaDoPanMouseButton = button;
            _phaDoPanStartPoint = e.GetPosition(phaDoScrollViewer);
            _phaDoPanStartScrollH = phaDoScrollViewer.HorizontalOffset;
            _phaDoPanStartScrollV = phaDoScrollViewer.VerticalOffset;
            theCanvas.Cursor = Cursors.Hand;
            theCanvas.CaptureMouse();
        }

        private void UpdatePhaDoPan(MouseEventArgs e)
        {
            if (!_phaDoIsPanning || phaDoScrollViewer == null)
            {
                return;
            }

            var pos = e.GetPosition(phaDoScrollViewer);
            double dx = pos.X - _phaDoPanStartPoint.X;
            double dy = pos.Y - _phaDoPanStartPoint.Y;
            if (Math.Abs(dx) > 0.5 || Math.Abs(dy) > 0.5)
            {
                _phaDoPanMoved = true;
            }

            phaDoScrollViewer.ScrollToHorizontalOffset(_phaDoPanStartScrollH - dx);
            phaDoScrollViewer.ScrollToVerticalOffset(_phaDoPanStartScrollV - dy);
        }

        private void EndPhaDoPan()
        {
            if (!_phaDoIsPanning)
            {
                return;
            }

            _phaDoIsPanning = false;
            theCanvas.ReleaseMouseCapture();
            theCanvas.Cursor = null;
            if (_phaDoPanMoved)
            {
                SaveWorkspaceSession();
            }
        }

        private void TheCanvas_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_phaDoIsPanning)
            {
                UpdatePhaDoPan(e);
                e.Handled = true;
                return;
            }

            if (_phaDoIsResizing)
            {
                UpdatePhaDoResize(e.GetPosition(theCanvas));
                e.Handled = true;
                return;
            }

            if (_phaDoIsDraggingPerson)
            {
                UpdatePersonDrag(e.GetPosition(theCanvas));
                e.Handled = true;
                return;
            }

            if (!_phaDoIsDragging || _phaDoDraggingFamilyId <= 0 || _phaDoCurrentOptions == null || _phaDoRenderedLayout == null)
            {
                return;
            }

            var pos = e.GetPosition(theCanvas);
            double dxPx = pos.X - _phaDoDragStartPoint.X;
            if (Math.Abs(dxPx) > 0.8)
            {
                _phaDoMouseMovedWhileDrag = true;
            }

            if (!_phaDoMouseMovedWhileDrag)
            {
                return;
            }

            double newXmm = _phaDoDragStartNodeXmm + GetPhaDoDragDeltaMm(pos);
            ApplyDraggedFamilyX(_phaDoDraggingFamilyId, newXmm);
            e.Handled = true;
        }

        private void TheCanvas_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_phaDoIsPanning && _phaDoPanMouseButton == MouseButton.Left)
            {
                EndPhaDoPan();
                e.Handled = true;
                return;
            }

            if (_phaDoIsResizing)
            {
                EndPhaDoResize();
                e.Handled = true;
                return;
            }

            if (_phaDoIsDraggingPerson)
            {
                EndPersonDrag();
                e.Handled = true;
                return;
            }

            if (!_phaDoIsDragging)
            {
                return;
            }

            // Chốt vị trí cuối tại thời điểm nhả chuột.
            if (_phaDoDraggingFamilyId > 0 && _phaDoCurrentOptions != null && _phaDoRenderedLayout != null)
            {
                var pos = e.GetPosition(theCanvas);
                double newXmm = _phaDoDragStartNodeXmm + GetPhaDoDragDeltaMm(pos);
                ApplyDraggedFamilyX(_phaDoDraggingFamilyId, newXmm);
            }

            _phaDoIsDragging = false;
            _phaDoDraggingFamilyId = 0;
            theCanvas.ReleaseMouseCapture();
            DrawSelectionOverlay(_phaDoSelectedFamilyId);
            if (_phaDoMouseMovedWhileDrag)
            {
                SaveWorkspaceSession();
            }

            e.Handled = true;
        }

        private void TheCanvas_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_phaDoIsPanning && e.ChangedButton == _phaDoPanMouseButton)
            {
                EndPhaDoPan();
                e.Handled = true;
            }
        }

        private async void ExportPhaDoExcel_Click(object sender, RoutedEventArgs e)
        {
            var root = viewModel.FamilyTree?.Family?.RootPerson;
            if (root == null)
            {
                MessageBox.Show("Chưa có dữ liệu gia phả để xuất.", "Có lỗi");
                return;
            }

            string baseName = viewModel.FamilyTree.GiaphaName;
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "PhaDo";
            }

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                baseName = baseName.Replace(c, '_');
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "Excel (*.xlsx)|*.xlsx",
                DefaultExt = "xlsx",
                FileName = baseName + ".xlsx",
                Title = "Xuất phả đồ ra Excel"
            };

            if (saveDialog.ShowDialog() != true)
            {
                return;
            }

            var progress = await this.ShowProgressAsync("Xuất Excel...", "Đang xuất phả đồ (text)");
            progress.SetIndeterminate();
            try
            {
                var options = BuildPhaDoRenderOptions();

                var result = _phaDoRenderedLayout
                    ?? await GiaPhaRenderService.ComputeLayoutAsync(root, options).ConfigureAwait(true);

                string outPath = saveDialog.FileName;
                await Task.Run(() => GiaPhaExcelExportService.ExportText(
                    outPath,
                    options,
                    result)).ConfigureAwait(true);

                viewModel.AddUserAction("Xuất Excel phả đồ: " + outPath + " (" + result.SizeSummary + ")");
                MessageBox.Show("Đã xuất phả đồ ra Excel:\n" + outPath, "Xong");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi xuất Excel: " + ex.Message, "Có lỗi");
                log.Error("xuất excel phả đồ", ex);
            }

            await progress.CloseAsync();
        }

        private async void ExportPhaDoXps_Click(object sender, RoutedEventArgs e)
        {
            var root = viewModel.FamilyTree?.Family?.RootPerson;
            if (root == null)
            {
                MessageBox.Show("Chưa có dữ liệu gia phả để xuất.", "Có lỗi");
                return;
            }

            string baseName = viewModel.FamilyTree.GiaphaName;
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "PhaDo";
            }

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                baseName = baseName.Replace(c, '_');
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "XPS (*.xps)|*.xps",
                DefaultExt = "xps",
                FileName = baseName + ".xps",
                Title = "Xuất phả đồ ra XPS"
            };

            if (saveDialog.ShowDialog() != true)
            {
                return;
            }

            var progress = await this.ShowProgressAsync("Xuất XPS...", "Đang tạo file vector (in ấn)");
            progress.SetIndeterminate();
            try
            {
                var options = BuildPhaDoRenderOptions(forPrint: true);

                string outPath = saveDialog.FileName;
                var result = _phaDoRenderedLayout
                    ?? await GiaPhaRenderService.ComputeLayoutAsync(root, options).ConfigureAwait(true);
                await Dispatcher.InvokeAsync(
                    () => GiaPhaRenderService.ExportResultToXps(outPath, result),
                    DispatcherPriority.Background).Task.ConfigureAwait(true);

                viewModel.AddUserAction("Xuất XPS phả đồ: " + outPath + " (" + result.SizeSummary + ")");
                MessageBox.Show(
                    "Đã xuất phả đồ ra XPS:\n" + outPath
                    + "\n\nMở bằng XPS Viewer hoặc Word để in.",
                    "Xong");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi xuất XPS: " + ex.Message, "Có lỗi");
                log.Error("xuất xps phả đồ", ex);
            }

            await progress.CloseAsync();
        }

        private async void ExportPhaDoSvg_Click(object sender, RoutedEventArgs e)
        {
            var root = viewModel.FamilyTree?.Family?.RootPerson;
            if (root == null)
            {
                MessageBox.Show("Chưa có dữ liệu gia phả để xuất.", "Có lỗi");
                return;
            }

            string baseName = viewModel.FamilyTree.GiaphaName;
            if (string.IsNullOrWhiteSpace(baseName))
            {
                baseName = "PhaDo";
            }

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                baseName = baseName.Replace(c, '_');
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "SVG (*.svg)|*.svg",
                DefaultExt = "svg",
                FileName = baseName + ".svg",
                Title = "Xuất phả đồ ra SVG"
            };

            if (saveDialog.ShowDialog() != true)
            {
                return;
            }

            var progress = await this.ShowProgressAsync("Xuất SVG...", "Đang tạo file vector");
            progress.SetIndeterminate();
            try
            {
                SyncPhaDoBoxStylesFromGiaPhaFile();
                var options = BuildPhaDoRenderOptions(forPrint: true);

                string outPath = saveDialog.FileName;
                var result = _phaDoRenderedLayout
                    ?? await GiaPhaRenderService.ComputeLayoutAsync(root, options).ConfigureAwait(true);
                await Task.Run(() => GiaPhaRenderService.ExportResultToSvg(
                    outPath,
                    result,
                    GetBoxStyleForFamily)).ConfigureAwait(true);

                viewModel.AddUserAction("Xuất SVG phả đồ: " + outPath + " (" + result.SizeSummary + ")");
                MessageBox.Show(
                    "Đã xuất phả đồ ra SVG:\n" + outPath
                    + "\n\nMở bằng trình duyệt, Inkscape hoặc Illustrator để xem/in.",
                    "Xong");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi xuất SVG: " + ex.Message, "Có lỗi");
                log.Error("xuất svg phả đồ", ex);
            }

            await progress.CloseAsync();
        }
        
        private async void ExportToHhtmlWeb(object sender, RoutedEventArgs e)
        {
            var _progressDialogController = await this.ShowProgressAsync("Đợi...", "Đang tính toán và xuất file WEB");
            _progressDialogController.SetProgress(0);
            _progressDialogController.SetIndeterminate();
            try
            {
                _progressDialogController.SetProgress(1);
                viewModel.ExportHtmlMxFile();
                viewModel.AddUserAction("Xuất file web xong..." + viewModel.FamilyTree.GiaphaWebHtml);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi vẽ phả hệ: " + ex.Message, "Có Lỗi");
                log.Error("vẽ phả hệ");
                log.Error(ex);
            }
            //BtnDownloadGiaPha.IsEnabled = true;
            await _progressDialogController.CloseAsync();

        }

        private void Hyperlink_RequestNavigate_2(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                if (Convert.ToInt32(viewModel.FamilyTree.GiaphaId) > 0)
                {
                    Process.Start(new ProcessStartInfo("file://"+ viewModel.FamilyTree.GiaphaWebHtml));
                    e.Handled = true;
                    return;
                }
            }
            catch (Exception ex)
            {

            }
            e.Handled = false;
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            // Check Update 
            AutoUpdater.Start("http://download.vietnamgiapha.com/files/autoupdate.xml");
        }

        private async void ExportToVectorMxFile(object sender, RoutedEventArgs e)
        {
            var _progressDialogController = await this.ShowProgressAsync("Đợi...", "Đang tính toán và xuất file Vector MX");
            _progressDialogController.SetProgress(0);
            _progressDialogController.SetIndeterminate();
            try
            {
                _progressDialogController.SetProgress(1);
                viewModel.ExportDrawioFile();
                viewModel.AddUserAction("Xuất file mx xong..." + viewModel.FamilyTree.GiaphaDrawIo);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi vẽ phả hệ: " + ex.Message, "Có Lỗi");
                log.Error("vẽ phả hệ");
                log.Error(ex);
            }
            //BtnDownloadGiaPha.IsEnabled = true;
            await _progressDialogController.CloseAsync();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {

        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T match)
                {
                    return match;
                }
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private void PhaDoFullscreenToggle_Click(object sender, RoutedEventArgs e)
        {
            TogglePhaDoImmersive();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (tabControl?.SelectedIndex != PhaDoTabIndex)
            {
                return;
            }

            if (e.Key == Key.F11)
            {
                TogglePhaDoImmersive();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape && _phaDoImmersive)
            {
                SetPhaDoImmersive(false);
                e.Handled = true;
            }
        }

        private void TogglePhaDoImmersive()
        {
            SetPhaDoImmersive(!_phaDoImmersive);
        }

        private void SetPhaDoImmersive(bool immersive)
        {
            if (_phaDoImmersive == immersive)
            {
                return;
            }

            _phaDoImmersive = immersive;

            if (immersive)
            {
                _phaDoImmersivePaneWasOpen = SimpleSplitview?.IsPaneOpen ?? true;
                if (SimpleSplitview != null)
                {
                    SimpleSplitview.IsPaneOpen = false;
                }

                if (mainStatusBar != null)
                {
                    mainStatusBar.Visibility = Visibility.Collapsed;
                }

                SetPhaDoTabStripVisible(false);
            }
            else
            {
                if (SimpleSplitview != null)
                {
                    SimpleSplitview.IsPaneOpen = _phaDoImmersivePaneWasOpen;
                }

                if (mainStatusBar != null)
                {
                    mainStatusBar.Visibility = Visibility.Visible;
                }

                SetPhaDoTabStripVisible(true);
            }

            UpdatePhaDoFullscreenButton();
            phaDoScrollViewer?.InvalidateMeasure();
        }

        private void SetPhaDoTabStripVisible(bool visible)
        {
            var strip = GetPhaDoTabStripElement();
            if (strip != null)
            {
                strip.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private FrameworkElement GetPhaDoTabStripElement()
        {
            if (_phaDoCachedTabStrip != null)
            {
                return _phaDoCachedTabStrip;
            }

            if (tabControl == null)
            {
                return null;
            }

            tabControl.ApplyTemplate();
            if (tabControl.Template?.FindName("HeaderPanel", tabControl) is FrameworkElement headerPanel)
            {
                _phaDoCachedTabStrip = headerPanel;
                return headerPanel;
            }

            _phaDoCachedTabStrip = FindVisualChild<System.Windows.Controls.Primitives.TabPanel>(tabControl);
            return _phaDoCachedTabStrip;
        }

        private static FrameworkElement FindVisualChild<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null)
            {
                return null;
            }

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T && child is FrameworkElement fe)
                {
                    return fe;
                }

                var found = FindVisualChild<T>(child);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void UpdatePhaDoFullscreenButton()
        {
            if (phaDoFullscreenIcon == null || phaDoFullscreenLabel == null || phaDoFullscreenBtn == null)
            {
                return;
            }

            if (_phaDoImmersive)
            {
                phaDoFullscreenIcon.Text = "\uE73F";
                phaDoFullscreenLabel.Text = "Thoát";
                phaDoFullscreenBtn.ToolTip = "Thoát toàn màn hình (Esc)";
            }
            else
            {
                phaDoFullscreenIcon.Text = "\uE740";
                phaDoFullscreenLabel.Text = "Toàn màn hình";
                phaDoFullscreenBtn.ToolTip = "Toàn màn hình (F11)";
            }
        }

        private void PhaDoZoomIn_Click(object sender, RoutedEventArgs e)
        {
            SetPhaDoZoom(_phaDoZoom + PhaDoZoomStep);
        }

        private void PhaDoZoomOut_Click(object sender, RoutedEventArgs e)
        {
            SetPhaDoZoom(_phaDoZoom - PhaDoZoomStep);
        }

        private void PhaDoZoomReset_Click(object sender, RoutedEventArgs e)
        {
            ResetPhaDoZoom();
        }

        private void PhaDoScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                double delta = e.Delta > 0 ? PhaDoZoomStep : -PhaDoZoomStep;
                SetPhaDoZoom(_phaDoZoom + delta);
                e.Handled = true;
            }
        }

        private void ResetPhaDoZoom()
        {
            SetPhaDoZoom(1.0);
        }

        private void SetPhaDoZoom(double zoom)
        {
            _phaDoZoom = Math.Max(PhaDoZoomMin, Math.Min(PhaDoZoomMax, zoom));
            theCanvas.LayoutTransform = new ScaleTransform(_phaDoZoom, _phaDoZoom);
            if (phaDoZoomLabel != null)
            {
                phaDoZoomLabel.Text = ((int)(_phaDoZoom * 100)) + "%";
            }
            phaDoScrollViewer?.InvalidateMeasure();
        }
        public ICommand DeletePersonFromFamilyClick { get; set; }
        private void DeletePersonFromFamilyClickFunc()
        {
            // XOA
            if (ListView_ListNguoiTrongGiaDinh.SelectedItem != null)
            {
                PersonInfo obj = (PersonInfo)ListView_ListNguoiTrongGiaDinh.SelectedItem;
                if (MessageBox.Show("Xóa [" + obj.MANS_NAME_HUY + "] ra khỏi gia đình này ?", "Xác Nhận", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    if (obj._familyInfo.ListPerson.IndexOf(obj) > -1)
                    {
                        obj._familyInfo.ListPerson.Remove(obj);
                        obj._familyInfo.OnPropertyChanged("Name");
                        log.Info("Xóa [" + obj.MANS_NAME_HUY + "] ra khỏi gia đình");
                        viewModel.AddUserAction("Xóa [" + obj.MANS_NAME_HUY + "] ra khỏi gia đình " + obj._familyInfo.Name0);
                    }
                }
            }
        }
        //
        private void Button_Click_Add_Person(object sender, RoutedEventArgs e)
        {
            if (ListView_ListNguoiTrongGiaDinh.Items.Count>0)
            {
                PersonInfo obj = (PersonInfo)ListView_ListNguoiTrongGiaDinh.Items.GetItemAt(0);
                var person = new PersonInfo("Người mới", obj._familyInfo);
                int countIsmain = 0;
                foreach (var item in obj._familyInfo.ListPerson)
                {
                    if (item.IsMainPerson == 1)
                    {
                        countIsmain++;
                        if (item.IsGioiTinhNam == 1)
                        {
                            // person - gt = nu
                            person.MANS_GENDER = "Nữ";
                        }
                        else
                        {
                            // person - gt = nam
                            person.MANS_GENDER = "Nam";
                        }
                    }
                }
                if (countIsmain == 0)
                {
                    obj._familyInfo.ListPerson[0].IsMainPerson = 1;
                    foreach (var item in obj._familyInfo.ListPerson)
                    {
                        if (item.IsMainPerson == 1)
                        {
                            countIsmain++;
                            if (item.IsGioiTinhNam == 1)
                            {
                                // person - gt = nu
                                person.MANS_GENDER = "Nữ";
                            }
                            else
                            {
                                // person - gt = nam
                                person.MANS_GENDER = "Nam";
                            }
                        }
                    }
                }
                obj._familyInfo.ListPerson.Add(person);
                log.Info("Thêm [" + obj.MANS_NAME_HUY + "] vào gia đình");
                viewModel.AddUserAction("Thêm [" + obj.MANS_NAME_HUY + "] vào gia đình " + obj._familyInfo.Name0);
            }
        }
        private void Button_Click_Delete_Person(object sender, RoutedEventArgs e)
        {
            if (ListView_ListNguoiTrongGiaDinh.SelectedItem != null)
            {
                PersonInfo obj = (PersonInfo)ListView_ListNguoiTrongGiaDinh.SelectedItem;
                if(obj._familyInfo.ListPerson.Count>1)
                {
                    if (MessageBox.Show("Xóa [" + obj.MANS_NAME_HUY + "] ra khỏi gia đình này ?", "Xác Nhận", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                    {
                        if (obj._familyInfo.ListPerson.IndexOf(obj) > -1)
                        {
                            obj._familyInfo.ListPerson.Remove(obj);
                            obj._familyInfo.OnPropertyChanged("Name");
                            log.Info("Xóa [" + obj.MANS_NAME_HUY + "] ra khỏi gia đình");
                            viewModel.AddUserAction("Xóa [" + obj.MANS_NAME_HUY + "] ra khỏi gia đình " + obj._familyInfo.Name0);
                        }
                    }
                }
            }
        }
        
        //
    }
}
