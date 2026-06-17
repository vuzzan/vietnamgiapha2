using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
using System.Windows.Threading;
using vngp21.Draw;
using vietnamgiapha.GiaPhaRender;
using System.Net;
using GalaSoft.MvvmLight.Command;
using Path = System.IO.Path;
using System.Text;
using System.ComponentModel;
using System.Globalization;
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
        /// <summary>Zoom tối thiểu thủ công; zoom-to-fit có thể nhỏ hơn khi phả rất rộng.</summary>
        private const double PhaDoZoomMin = 0.15;
        private const double PhaDoZoomFitMin = 0.01;
        private const double PhaDoZoomMax = 5.0;
        private const double PhaDoZoomStep = 0.15;
        private const int PhaDoTabIndex = 3;

        private readonly GiaPhaUndoStack _giaPhaUndo = new GiaPhaUndoStack();
        private bool _giaPhaUndoRestoring;

        /// <summary>Ánh xạ chỉ số tab cũ (8 tab) sang tab mới sau khi gom Tư liệu.</summary>
        private static int MapLegacyMainTabIndex(int legacyIndex)
        {
            if (legacyIndex >= 2 && legacyIndex <= 5)
            {
                return 2;
            }

            if (legacyIndex >= 6)
            {
                return legacyIndex - 3;
            }

            return legacyIndex;
        }
        /// <summary>Dưới ngưỡng này không tách phả con (một phả đủ nhỏ).</summary>
        private const int PhaDoMinFamilyCountToSplitPhaiCon = 100;
        /// <summary>Mục tiêu mềm: mỗi phả con quanh mức này (cho phép dao động).</summary>
        private const int PhaDoTargetFamilyCountPerSubtree = 200;
        private GiaPhaRenderResult _phaDoRenderedLayout;
        /// <summary>Dialog "Phân tích phả con" đang mở (modeless) — đóng khi phân tích lại.</summary>
        private PhaDoSubtreeMapDialog _phaDoAnalysisDialog;
        /// <summary>Layout cây đầy đủ — chỉ dùng ước lượng đời tách Root0 khi chưa phân tích (không dùng layout đã scope).</summary>
        private GiaPhaRenderResult _phaDoFullTreeLayoutSnapshot;
        private int _phaDoFullTreeLayoutSnapshotRootId;
        private GiaPhaRenderOptions _phaDoCurrentOptions;

        /// <summary>Hai mode tương tác chính trên phả đồ — chọn/di box hoặc kéo cuộn canvas.</summary>
        private enum PhaDoInteractionMode { Select, Pan }
        private PhaDoInteractionMode _phaDoInteractionMode = PhaDoInteractionMode.Select;

        // ── Trạng thái select nhãn "Đời X" ────────────────────────────
        private int _phaDoSelectedGenLevel = -1;   // -1 = không chọn
        private const string GenLabelSelectionTag = "__PhaDoGenLabelSelection";

        // ── Trạng thái select dòng text bên trong title block ──────────
        /// <summary>Dòng text title: 0–3 (tối đa 4 dòng), -1 = chỉ chọn khối title.</summary>
        private int _phaDoTitleSelectedLine = -1;

        // ── Trạng thái select + resize title block ─────────────────────
        private bool _phaDoTitleSelected;
        private bool _phaDoTitleIsResizing;
        private PhaDoResizeCorner _phaDoTitleResizeCorner;
        private Point  _phaDoTitleResizeStartPoint;
        private double _phaDoTitleResizeStartWmm;
        private double _phaDoTitleResizeStartHmm;
        private double _phaDoTitleResizeStartLeftMm;
        private double _phaDoTitleResizeStartTopMm;

        /// <summary>Kéo di chuyển cả khối title khi đã chọn (không phải kéo từng dòng chữ).</summary>
        private bool _phaDoIsDraggingTitleBlock;
        private Point _phaDoTitleBlockDragStartPoint;
        private double _phaDoTitleBlockDragStartLeftMm;
        private double _phaDoTitleBlockDragStartTopMm;
        private bool _phaDoTitleBlockMovedWhileDrag;

        private bool _phaDoIsDragging;
        private bool _phaDoIsMarqueeSelecting;
        private bool _phaDoIsPanning;
        private bool _phaDoPanMoved;
        private MouseButton _phaDoPanMouseButton;
        private Point _phaDoPanStartPoint;
        private double _phaDoPanStartScrollH;
        private double _phaDoPanStartScrollV;
        private int _phaDoDraggingFamilyId;
        private int _phaDoSelectedFamilyId;
        /// <summary>null = chỉ chọn ô; -1 = chữ Đời; 0+ = người chính/phụ.</summary>
        private int? _phaDoSelectedPersonSlot;
        private bool _phaDoIsDraggingPerson;
        private int _phaDoDraggingPersonFamilyId;
        private int _phaDoDraggingPersonSlot;
        private FrameworkElement _phaDoDraggingPersonElement;
        private Point _phaDoPersonDragStartCanvas;
        private double _phaDoPersonNaturalLeftPx;
        private double _phaDoPersonNaturalTopPx;
        private double _phaDoPersonDragStartDeltaXmm;
        private double _phaDoPersonDragStartDeltaYmm;
        /// <summary>Chờ kéo text: mousedown chọn text, move vượt ngưỡng mới bắt drag (1 thao tác).</summary>
        private bool _phaDoPendingPersonDrag;
        private int _phaDoPendingPersonFamilyId;
        private int _phaDoPendingPersonSlot;
        private FrameworkElement _phaDoPendingPersonElement;
        private Point _phaDoPendingPersonDragStart;
        /// <summary>Chờ kéo dòng chữ title — tương tự text trong ô gia đình.</summary>
        private bool _phaDoPendingTitleDrag;
        private int _phaDoPendingTitleLine;
        private FrameworkElement _phaDoPendingTitleElement;
        private Point _phaDoPendingTitleDragStart;
        private bool _phaDoIsDraggingTitleLine;
        private int _phaDoDraggingTitleLine;
        private FrameworkElement _phaDoDraggingTitleElement;
        private Point _phaDoTitleLineDragStartCanvas;
        private double _phaDoTitleLineNaturalLeftPx;
        private double _phaDoTitleLineNaturalTopPx;
        private double _phaDoTitleLineDragStartDeltaXmm;
        private double _phaDoTitleLineDragStartDeltaYmm;
        private Point _phaDoDragStartPoint;
        private double _phaDoDragStartNodeXmm;
        private bool _phaDoMouseMovedWhileDrag;
        private readonly HashSet<int> _phaDoMultiSelectedFamilyIds = new HashSet<int>();
        private readonly Dictionary<int, double> _phaDoDragStartXmmByFamilyId = new Dictionary<int, double>();
        private Rectangle _phaDoMarqueeRect;
        private Point _phaDoMarqueeStartPoint;
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
        private bool _phaDoToolboxExpanded;
        private const double PhaDoToolboxWidthCollapsed = 52;
        private const double PhaDoToolboxWidthExpanded = 150;
        private bool _treePaneImmersive;
        private double _treePaneImmersiveSavedOpenPaneLength = 300;
        private double _treePaneImmersiveSavedCompactPaneLength = 48;
        private double _treePaneImmersiveSavedMaximumOpenPaneLength = 1000;
        private SplitViewDisplayMode _treePaneImmersiveSavedDisplayMode = SplitViewDisplayMode.Inline;
        private GridLength _treePaneImmersiveSavedLogRowHeight = new GridLength(200);
        private FrameworkElement _treePaneSplitViewContentHost;
        private UIElement _treePaneSplitViewResizeThumb;
        private Visibility _treePaneSplitViewResizeThumbSavedVisibility = Visibility.Visible;
        private Grid _treePaneSplitViewRootGrid;
        private GridLength _treePaneSplitViewSavedPaneColumnWidth;
        private GridLength _treePaneSplitViewSavedContentColumnWidth;
        private bool _treePaneSplitViewColumnsCaptured;

        private FamilyViewModel _treeDragSourceFamily;
        private Point _treeDragStartPoint;
        private bool _treeDragStarted;
        private FrameworkElement _phaDoCachedTabStrip;
        private bool _isRestoringWorkspace;
        private bool _personGridIsRefreshing;
        private FamilyViewModel _personGridCacheRoot;
        private List<PersonGridRow> _personGridCachedRows;
        private readonly PersonGridRowCollection _personGridRows = new PersonGridRowCollection();
        private bool _personGridShowAllInFamily = true;
        private bool _personGridViewSortConfigured;
        private string _personSearchLastQuery;

        /// <summary>Cho phép BringIntoView khi cuộn tìm kiếm / focus programmatic — click chuột vẫn giữ scroll.</summary>
        private bool _allowTreeViewBringIntoView;

        /// <summary>Gia đình đang chờ cuộn tới (tìm kiếm) — cho phép BringIntoView mặc định của WPF.</summary>
        private FamilyViewModel _pendingTreeScrollTarget;

        private ScrollViewer _treeViewScrollViewer;
        private ScrollContentPresenter _treeScrollContentPresenter;
        private int _personSearchLastIndex = -1;
        private int _personGridSelectedFamilyId;
        private FamilyViewModel _personGridSelectedFamilyRoot;
        private bool _personGridIsSelectingFamily;
        private static readonly SolidColorBrush PersonFamilyHighlightBrush = new SolidColorBrush(Color.FromRgb(0xE8, 0xF2, 0xFF));
        private static readonly SolidColorBrush PersonFamilyBorderBrush = new SolidColorBrush(Color.FromRgb(0x9C, 0xBE, 0xE8));
        public ICollectionView PersonGridView { get; private set; }
        private readonly ObservableCollection<PhaDoRenderScopeItem> _phaDoRenderScopes = new ObservableCollection<PhaDoRenderScopeItem>();
        private readonly ObservableCollection<PhaDoCardLayoutItem> _phaDoCardLayoutItems = new ObservableCollection<PhaDoCardLayoutItem>();
        private int _phaDoRenderScopeSourceRootId;
        /// <summary>Danh sách scope đã được bổ sung sau Phân tích phả (Root1, Root2...).</summary>
        private bool _phaDoRenderScopesFromAnalyze;
        /// <summary>Đời cao nhất lúc phân tích — dùng chọn mốc tách Root1→Root2… (cùng EffectiveMax như report).</summary>
        private int _phaDoAnalyzeMaxFamilyLevel = 30;
        /// <summary>Đời cao nhất của Root0 khi chưa phân tích — khớp mặc định sau phân tích (1–4).</summary>
        private const int PhaDoDefaultRoot0MaxGeneration = 4;
        private int _phaDoScopeHighlightStartLevel;
        private int _phaDoScopeHighlightRootIndex;
        private string _phaDoScopeHighlightLabel;
        private bool _phaDoScopeExpandSmallBranchesAtStopLevel;
        private int _phaDoScopeMinBranchForStopLevel;
        private HashSet<int> _phaDoScopeStopFamilyIdsAtMaxLevel = new HashSet<int>();
        private bool _phaDoShowScopeSummaryNote;
        private string _phaDoScopeStartFamilyName;
        private readonly HashSet<int> _phaConFamilyIds = new HashSet<int>();

        // AI service — khởi tạo lazy khi người dùng mở cài đặt lần đầu.
        private readonly AI.AiApiService _aiService = new AI.AiApiService();
        /// <summary>Rule-based engine — được build lại mỗi khi load file gia phả mới.</summary>
        private readonly AI.GiaPhaQueryEngine _aiQueryEngine = new AI.GiaPhaQueryEngine();
        /// <summary>Chat dialog AI modeless — giữ 1 instance, reuse khi mở lại.</summary>
        private AI.AiChatDialog _aiChatDialog;
        /// <summary>Help dialog modeless — giữ 1 instance, tự dọn khi đóng.</summary>
        private HelpDialog _helpDialog;
        private readonly HashSet<int> _phaConStopFamilyIds = new HashSet<int>();
        /// <summary>IDs non-STOP đã được gom vào combo đa gốc — dừng ở split level trong scope cha (có trang riêng).</summary>
        private readonly HashSet<int> _phaConNonStopComboFamilyIds = new HashSet<int>();
        private readonly Dictionary<int, (double WidthCm, double HeightCm)> _phaConBoundsCmByFamilyId =
            new Dictionary<int, (double WidthCm, double HeightCm)>();

        /// <summary>Một lựa chọn vẽ ở toolbar: Toàn phả hoặc một nhánh phả con.</summary>
        private sealed class PhaDoRenderScopeItem
        {
            public string Label { get; set; }
            public int FamilyId { get; set; }
            public FamilyViewModel RootFamily { get; set; }
            public bool IsWholeTree { get; set; }
            public int MaxGenerationInclusive { get; set; } = int.MaxValue;
            public int HighlightStartLevel { get; set; }
            public int HighlightStartRootIndex { get; set; }
            public string HighlightStartLabel { get; set; }
            public bool ExpandSmallBranchesAtStopLevel { get; set; }
            public int MinBranchForStopLevel { get; set; }
            public HashSet<int> StopFamilyIdsAtMaxLevel { get; set; }

            /// <summary>Scope Root0 mặc định khi chưa bấm Phân tích phả — cần bổ sung tham số tách trước khi vẽ.</summary>
            public bool IsDefaultRoot0WithoutAnalyze { get; set; }

            /// <summary>Mô tả luồng vẽ: Root0→Root1, Root1→Root2, trang lá…</summary>
            public string RenderPlanSummary { get; set; }

            /// <summary>Thứ tự mục trên combo toolbar (0 = Toàn phả).</summary>
            public int ComboIndexHint { get; set; }

            /// <summary>Số GD ước lượng sau clone scope (khớp ComputeLayoutAsync).</summary>
            public int LayoutFamilyCountEstimate { get; set; }

            /// <summary>
            /// Danh sách FamilyId gốc của các nhánh non-STOP gom thành 1 combo multi-root.
            /// Khi có danh sách này, FamilyId/RootFamily là nhánh đầu tiên (để hiển thị tên).
            /// </summary>
            public List<int> MultiRootFamilyIds { get; set; }

            /// <summary>True khi combo là tập hợp nhiều nhánh non-STOP xếp dọc độc lập.</summary>
            public bool IsMultiRootVerticalStack => MultiRootFamilyIds != null && MultiRootFamilyIds.Count > 1;

            /// <summary>True khi đây là combo "Bản đồ phả con" (root0→root1→root2 multi-level).</summary>
            public bool IsPhaConMap { get; set; }

            /// <summary>Đời tách lần 1 (root1) — dùng khi render bản đồ.</summary>
            public int PhaConMapRoot1SplitLevel { get; set; }

            /// <summary>STOP IDs tại đời root1 (nhánh lớn, sẽ tiếp tục xuống root2 trong bản đồ).</summary>
            public HashSet<int> PhaConMapRoot1StopIds { get; set; }

            /// <summary>STOP IDs tại các đời sâu hơn root1 (root2, root3…) — dừng hẳn tại đây.</summary>
            public HashSet<int> PhaConMapDeepStopIds { get; set; }

            /// <summary>Chỉ số nhóm trong chuỗi multi-root (1-based), để đặt tên "Nhóm non-STOP 1/28".</summary>
            public int MultiRootGroupIndex { get; set; }

            /// <summary>Tổng số nhóm multi-root được tạo (để hiển thị "1/28").</summary>
            public int MultiRootGroupTotal { get; set; }

            /// <summary>
            /// Nhãn tóm tắt từng nhánh trong combo multi-root: "ID X | Tên người | N GD".
            /// Dùng khi in report phân tích để hiển thị đủ thông tin mỗi nhánh.
            /// </summary>
            public List<string> MultiRootBranchLabels { get; set; }

            public override string ToString()
            {
                return Label ?? "";
            }
        }

        /// <summary>Một kiểu chữ ô phả đồ: ngang / dọc / dọc theo từ.</summary>
        private sealed class PhaDoCardLayoutItem
        {
            public int Index { get; set; }
            public string Label { get; set; }

            public override string ToString()
            {
                return Label ?? "";
            }
        }

        /// <summary>Tag marker gắn vào đúng gia đình để kéo/refresh thì text đi theo box.</summary>
        private sealed class PhaDoScopeStartMarkerTag
        {
            public int FamilyId { get; set; }
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

        /// <summary>
        /// Điền dòng 3 (số GĐ · số người) và dòng 4 (W cm × H cm) vào options
        /// dựa trên kết quả layout — phải gọi sau ComputeLayout, trước PaintToCanvas.
        /// </summary>
        private void PopulateTitleAutoLines(GiaPhaRenderResult result)
        {
            if (result?.Options == null) return;

            var nodes = result.Nodes;
            int familyCount = nodes?.Count(n => n?.Family != null) ?? 0;
            int personCount = nodes?.Sum(n => n?.Family?.ListPerson?.Count ?? 0) ?? 0;

            result.Options.TitleLine3 = familyCount > 0
                ? $"{familyCount} gia đình · {personCount} người"
                : "";

            double wCm = result.ContentWidthMm  / 10.0;
            double hCm = result.ContentHeightMm / 10.0;
            result.Options.TitleLine4 = (wCm > 0 && hCm > 0)
                ? $"{wCm:0.#} cm × {hCm:0.#} cm"
                : "";
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

        private void InitPhaDoCardLayoutCombo()
        {
            _phaDoCardLayoutItems.Clear();
            _phaDoCardLayoutItems.Add(new PhaDoCardLayoutItem { Index = 0, Label = "Chữ ngang" });
            _phaDoCardLayoutItems.Add(new PhaDoCardLayoutItem { Index = 1, Label = "Chữ dọc" });
            _phaDoCardLayoutItems.Add(new PhaDoCardLayoutItem { Index = 2, Label = "Dọc theo từ" });
            if (phaDoCardLayoutCombo == null)
            {
                return;
            }

            phaDoCardLayoutCombo.ItemsSource = _phaDoCardLayoutItems;
            phaDoCardLayoutCombo.SelectedIndex = 0;
            UpdatePhaDoCardLayoutComboToolTip();
        }

        /// <summary>Đọc combo kiểu chữ: 0 Ngang, 1 Dọc, 2 Dọc theo từ.</summary>
        private int GetPhaDoCardLayoutListIndex()
        {
            if (phaDoCardLayoutCombo?.SelectedItem is PhaDoCardLayoutItem item)
            {
                return item.Index;
            }

            return 0;
        }

        private void SetPhaDoCardLayoutIndex(int index)
        {
            if (phaDoCardLayoutCombo == null || _phaDoCardLayoutItems.Count == 0)
            {
                return;
            }

            index = Math.Max(0, Math.Min(2, index));
            for (int i = 0; i < _phaDoCardLayoutItems.Count; i++)
            {
                if (_phaDoCardLayoutItems[i].Index == index)
                {
                    phaDoCardLayoutCombo.SelectedIndex = i;
                    UpdatePhaDoCardLayoutComboToolTip();
                    return;
                }
            }
        }

        private void PhaDoCardLayoutCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePhaDoCardLayoutComboToolTip();
        }

        private void UpdatePhaDoCardLayoutComboToolTip()
        {
            if (phaDoCardLayoutCombo == null)
            {
                return;
            }

            if (phaDoCardLayoutCombo.SelectedItem is PhaDoCardLayoutItem item
                && !string.IsNullOrWhiteSpace(item.Label))
            {
                phaDoCardLayoutCombo.ToolTip = "Kiểu chữ: " + item.Label;
            }
            else
            {
                phaDoCardLayoutCombo.ToolTip = "Kiểu chữ trong ô gia đình";
            }
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
                case 1: return "Chữ dọc";
                case 2: return "Dọc theo từ";
                default: return "Chữ ngang";
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

            if (tag is GiaPhaRender.PhaDoBoxZoneTag zoneTag)
            {
                return zoneTag.FamilyId;
            }

            if (tag is PhaDoScopeStartMarkerTag markerTag)
            {
                return markerTag.FamilyId;
            }

            if (tag is GiaPhaRender.PhaDoZoneSvgPreviewTag previewTag
                && !previewTag.IsTitleBlock
                && previewTag.FamilyId > 0)
            {
                return previewTag.FamilyId;
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

        /// <summary>Hit vào nền ô / zone / handle — không tính click trên text người.</summary>
        private bool TryResolveFamilyBoxBackgroundHit(
            DependencyObject source,
            out int familyId,
            out GiaPhaPlacedNode node)
        {
            familyId = 0;
            node = null;
            if (TryResolvePersonElementHit(source, out _, out _, out _))
            {
                return false;
            }

            return TryResolveFamilyFromCanvasHit(source, out familyId, out node);
        }

        /// <summary>Chỉ chọn ô gia đình (viền + toolbar SVG), không chọn text bên trong.</summary>
        private void SelectPhaDoBoxOutline(int familyId)
        {
            if (familyId <= 0)
            {
                return;
            }

            ClearTitleSelection();
            ClearGenLabelSelection();
            CancelPendingPersonDrag();
            ClearPersonSelectionHighlight();

            _phaDoSelectedFamilyId = familyId;
            _phaDoSelectedPersonSlot = null;

            DrawSelectionOverlay(familyId);
            DrawMultiSelectionOverlays();
            DrawDirectChildHighlights(familyId);
            BringScopeStartMarkersToFront();
            UpdatePhaDoSelectedBoxSizeStatus(familyId);
            SyncPhaDoToolbarFromBoxStyle(familyId);
        }

        /// <summary>Chỉ chọn text trong ô — không gọi logic toolbar/viền box đầy đủ.</summary>
        private void SelectPhaDoFamilyText(int familyId, int personSlot)
        {
            if (familyId <= 0)
            {
                return;
            }

            ClearTitleSelection();
            ClearGenLabelSelection();
            CancelPendingPersonDrag();

            _phaDoSelectedFamilyId = familyId;
            _phaDoSelectedPersonSlot = personSlot;

            // Chỉ highlight phần chữ + toolbar font (không bật nhóm SVG box)
            ClearSelectionOverlay();
            DrawMultiSelectionOverlays();
            UpdatePersonSelectionHighlight();
            UpdatePhaDoSelectedBoxSizeStatus(familyId);
            SyncToolbarFromFamilyPersonText(familyId, personSlot);
        }

        /// <summary>Vẽ viền cho các box đang multi-select (trừ box primary đã có viền dashed).</summary>
        private void DrawMultiSelectionOverlays()
        {
            var old = theCanvas.Children
                .OfType<FrameworkElement>()
                .Where(fe => Equals(fe.Tag, "__PhaDoMultiSelectionOverlay"))
                .Cast<UIElement>()
                .ToList();
            foreach (var el in old)
            {
                theCanvas.Children.Remove(el);
            }

            foreach (int familyId in _phaDoMultiSelectedFamilyIds)
            {
                if (familyId <= 0 || familyId == _phaDoSelectedFamilyId)
                {
                    continue;
                }
                if (!TryGetFamilyBackgroundBounds(familyId, out double left, out double top, out double boxW, out double boxH))
                {
                    continue;
                }

                var outline = new Rectangle
                {
                    Width = Math.Max(1, boxW + 3),
                    Height = Math.Max(1, boxH + 3),
                    Stroke = new SolidColorBrush(Color.FromRgb(38, 122, 255)),
                    StrokeThickness = 1.2,
                    Fill = new SolidColorBrush(Color.FromArgb(45, 38, 122, 255)),
                    IsHitTestVisible = false,
                    Tag = "__PhaDoMultiSelectionOverlay"
                };
                Canvas.SetLeft(outline, left - 1.5);
                Canvas.SetTop(outline, top - 1.5);
                Panel.SetZIndex(outline, 998);
                theCanvas.Children.Add(outline);

                // Vẽ 4 góc để user thấy rõ object đang nằm trong nhóm chọn.
                const double corner = 7.0;
                void AddCorner(double x, double y)
                {
                    var c = new Rectangle
                    {
                        Width = corner,
                        Height = corner,
                        Fill = new SolidColorBrush(Color.FromRgb(38, 122, 255)),
                        Stroke = Brushes.White,
                        StrokeThickness = 0.9,
                        RadiusX = 1.2,
                        RadiusY = 1.2,
                        IsHitTestVisible = false,
                        Tag = "__PhaDoMultiSelectionOverlay"
                    };
                    Canvas.SetLeft(c, x - corner / 2.0);
                    Canvas.SetTop(c, y - corner / 2.0);
                    Panel.SetZIndex(c, 999);
                    theCanvas.Children.Add(c);
                }

                double x0 = left - 1.5;
                double y0 = top - 1.5;
                double x1 = left + boxW + 1.5;
                double y1 = top + boxH + 1.5;
                AddCorner(x0, y0);
                AddCorner(x1, y0);
                AddCorner(x0, y1);
                AddCorner(x1, y1);
            }
        }

        /// <summary>Xóa toàn bộ trạng thái chọn box (single + multi) trên canvas phả đồ.</summary>
        private void ClearTitleSelection()
        {
            CancelPendingTitleDrag();
            if (_phaDoIsDraggingTitleLine)
            {
                EndTitleLineDrag();
            }

            if (_phaDoIsDraggingTitleBlock)
            {
                EndTitleBlockDrag();
            }

            _phaDoTitleSelected = false;
            _phaDoTitleSelectedLine = -1;
            ClearTitleLineHighlight();
            ClearTitleSelectionOverlay();
            HideContextToolbar();
        }

        /// <summary>Xóa trạng thái chọn nhãn "Đời X" và overlay tương ứng.</summary>
        private void ClearGenLabelSelection()
        {
            _phaDoSelectedGenLevel = -1;
            ClearGenLabelSelectionOverlay();
        }

        private void ClearPhaDoBoxSelections()
        {
            CancelPendingPersonDrag();
            _phaDoSelectedFamilyId = 0;
            _phaDoSelectedPersonSlot = null;
            _phaDoMultiSelectedFamilyIds.Clear();
            ClearSelectionOverlay();
            DrawMultiSelectionOverlays();
            ClearDirectChildHighlights();
            UpdatePhaDoSelectedBoxSizeStatus(0);
            HideContextToolbar();
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
                if (cursor is FrameworkElement fe)
                {
                    if (fe.Tag is PhaDoBoxBackgroundTag bg
                        && (bg.Family?.familyInfo?.FamilyId ?? 0) == familyId)
                    {
                        return true;
                    }

                    if (fe.Tag is GiaPhaRender.PhaDoZoneSvgPreviewTag p
                        && !p.IsTitleBlock
                        && p.FamilyId == familyId)
                    {
                        return true;
                    }
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

        /// <summary>Vùng nội dung chữ trong ô (padding/header khớp FamilyTreeCanvasRenderer).</summary>
        private bool TryGetFamilyInnerContentBoundsPx(
            int familyId,
            out double leftPx,
            out double topPx,
            out double widthPx,
            out double heightPx)
        {
            leftPx = topPx = widthPx = heightPx = 0;
            if (!TryGetFamilyBackgroundBounds(familyId, out double boxLeft, out double boxTop, out double boxW, out double boxH))
            {
                return false;
            }

            var opt = _phaDoCurrentOptions;
            double padMm = opt?.CardPaddingMm ?? 2.5;
            double padSidePx = MmToPx(padMm);
            double padBottomPx = MmToPx(opt?.CardBottomPaddingMm ?? 2);
            bool vertical = opt != null && GiaPhaRenderOptions.IsVerticalCardLayout(opt.CardLayoutMode);

            double padTopPx;
            if (vertical)
            {
                padTopPx = MmToPx(padMm * 0.5);
            }
            else
            {
                padTopPx = MmToPx(padMm) + MmToPx(opt?.CardHeaderHeightMm ?? 6);
            }

            leftPx = boxLeft + padSidePx;
            topPx = boxTop + padTopPx;
            widthPx = Math.Max(4, boxW - 2 * padSidePx);
            // Dọc: khớp DrawCardVertical (pad*0.5 trên, pad + bottom dưới).
            // Ngang: vùng tên từ sau header — pad*0.5 trước dòng đầu, pad + bottomPad đáy ô.
            if (vertical)
            {
                heightPx = Math.Max(4, boxH - padTopPx - padSidePx - padBottomPx);
            }
            else
            {
                double padTopContentPx = MmToPx(padMm * 0.5);
                topPx = boxTop + MmToPx(opt?.CardHeaderHeightMm ?? 6) + padTopContentPx;
                heightPx = Math.Max(4, boxH - (topPx - boxTop) - padSidePx - padBottomPx);
            }

            return widthPx > 0 && heightPx > 0;
        }

        /// <summary>Bề rộng vùng chữ trong ô (box trừ padding hai bên).</summary>
        private bool TryGetFamilyInnerContentWidthPx(int familyId, out double innerWidthPx, out double contentLeftPx)
        {
            innerWidthPx = 0;
            contentLeftPx = 0;
            if (!TryGetFamilyInnerContentBoundsPx(familyId, out contentLeftPx, out _, out innerWidthPx, out _))
            {
                return false;
            }

            return innerWidthPx > 0;
        }

        /// <summary>Cập nhật Width/MaxWidth của TextBlock người theo bề rộng ô (sau resize).</summary>
        private void SyncFamilyPersonTextWidthsToBox(int familyId)
        {
            if (!TryGetFamilyInnerContentWidthPx(familyId, out double innerW, out _))
            {
                return;
            }

            // Cột dọc / dọc theo từ: không kéo rộng StackPanel = full ô (phá kéo ngang).
            bool verticalCards = _phaDoCurrentOptions != null
                && GiaPhaRenderOptions.IsVerticalCardLayout(_phaDoCurrentOptions.CardLayoutMode);

            foreach (var fe in theCanvas.Children.OfType<FrameworkElement>())
            {
                if (!(fe.Tag is PhaDoBoxVisualTag tag)
                    || (tag.Family?.familyInfo?.FamilyId ?? 0) != familyId)
                {
                    continue;
                }

                if (tag.ElementKind != PhaDoBoxElementKind.Person
                    && tag.ElementKind != PhaDoBoxElementKind.ExtraNote
                    && tag.ElementKind != PhaDoBoxElementKind.GenerationLabel)
                {
                    continue;
                }

                if (fe is StackPanel)
                {
                    if (verticalCards)
                    {
                        continue;
                    }

                    var column = (StackPanel)fe;
                    column.Width = innerW;
                    foreach (var line in column.Children.OfType<TextBlock>())
                    {
                        line.Width = innerW;
                        line.MaxWidth = innerW;
                    }

                    continue;
                }

                if (fe is TextBlock tb)
                {
                    tb.Width = innerW;
                    tb.MaxWidth = innerW;
                }
            }
        }

        private void RefreshFamilySelectionVisuals(int familyId)
        {
            if (_phaDoSelectedFamilyId != familyId)
            {
                return;
            }

            if (_phaDoSelectedPersonSlot.HasValue)
            {
                UpdatePersonSelectionHighlight();
                return;
            }

            DrawSelectionOverlay(familyId);
        }

        private void UpdatePersonSelectionHighlight()
        {
            ClearPersonSelectionHighlight();
            if (_phaDoSelectedFamilyId <= 0 || !_phaDoSelectedPersonSlot.HasValue)
            {
                return;
            }

            var element = FindPersonVisual(_phaDoSelectedFamilyId, _phaDoSelectedPersonSlot.Value);
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

            if (!TryMeasurePersonTextSelectionBounds(element, out left, out top, out double w, out double h))
            {
                GetPersonElementSize(element, out w, out h);
            }

            const double pad = 2;
            double boxW = Math.Max(4, w + pad * 2);
            double boxH = Math.Max(4, h + pad * 2);
            double x0 = left - pad;
            double y0 = top - pad;

            var outline = new Rectangle
            {
                Width = boxW,
                Height = boxH,
                Stroke = new SolidColorBrush(Color.FromRgb(30, 120, 220)),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = new SolidColorBrush(Color.FromArgb(30, 30, 120, 220)),
                IsHitTestVisible = false,
                Tag = "__PhaDoPersonSelection"
            };
            Canvas.SetLeft(outline, x0);
            Canvas.SetTop(outline, y0);
            Panel.SetZIndex(outline, 1003);
            theCanvas.Children.Add(outline);

            // 4 góc vuông — giống chọn text / resize nhỏ
            const double handle = 7;
            AddPersonTextSelectionHandle(x0, y0, handle);
            AddPersonTextSelectionHandle(x0 + boxW, y0, handle);
            AddPersonTextSelectionHandle(x0, y0 + boxH, handle);
            AddPersonTextSelectionHandle(x0 + boxW, y0 + boxH, handle);
        }

        private void AddPersonTextSelectionHandle(double cornerX, double cornerY, double size)
        {
            var h = new Rectangle
            {
                Width = size,
                Height = size,
                Fill = Brushes.White,
                Stroke = new SolidColorBrush(Color.FromRgb(30, 120, 220)),
                StrokeThickness = 1.2,
                RadiusX = 1,
                RadiusY = 1,
                IsHitTestVisible = false,
                Tag = "__PhaDoPersonSelection"
            };
            Canvas.SetLeft(h, cornerX - size / 2.0);
            Canvas.SetTop(h, cornerY - size / 2.0);
            Panel.SetZIndex(h, 1004);
            theCanvas.Children.Add(h);
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

        /// <summary>Đo khung chọn quanh chữ — không dùng Width layout (= bề rộng ô).</summary>
        private static bool TryMeasurePersonTextSelectionBounds(
            FrameworkElement element,
            out double leftPx,
            out double topPx,
            out double widthPx,
            out double heightPx)
        {
            widthPx = 0;
            heightPx = 0;
            leftPx = Canvas.GetLeft(element);
            topPx = Canvas.GetTop(element);
            if (double.IsNaN(leftPx))
            {
                leftPx = 0;
            }

            if (double.IsNaN(topPx))
            {
                topPx = 0;
            }

            if (element is TextBlock tb)
            {
                if (!TryMeasureTextBlockTextSizePx(tb, out widthPx, out heightPx))
                {
                    return false;
                }

                double layoutW = tb.ActualWidth > 1
                    ? tb.ActualWidth
                    : (!double.IsNaN(tb.Width) && tb.Width > 0 ? tb.Width : widthPx);
                if (tb.TextAlignment == TextAlignment.Center)
                {
                    leftPx += Math.Max(0, (layoutW - widthPx) / 2.0);
                }
                else if (tb.TextAlignment == TextAlignment.Right)
                {
                    leftPx += Math.Max(0, layoutW - widthPx);
                }

                return true;
            }

            if (element is StackPanel column)
            {
                double maxW = 0;
                double totalH = 0;
                foreach (var child in column.Children.OfType<TextBlock>())
                {
                    if (!TryMeasureTextBlockTextSizePx(child, out double lineW, out double lineH))
                    {
                        continue;
                    }

                    maxW = Math.Max(maxW, lineW);
                    totalH += lineH;
                }

                if (maxW < 1 || totalH < 1)
                {
                    return false;
                }

                widthPx = maxW;
                heightPx = totalH;
                double colW = column.ActualWidth > 1
                    ? column.ActualWidth
                    : (!double.IsNaN(column.Width) && column.Width > 0 ? column.Width : maxW);
                leftPx += Math.Max(0, (colW - widthPx) / 2.0);
                return true;
            }

            element.UpdateLayout();
            widthPx = element.ActualWidth;
            heightPx = element.ActualHeight;
            if (widthPx < 1 || heightPx < 1)
            {
                element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                widthPx = element.DesiredSize.Width;
                heightPx = element.DesiredSize.Height;
            }

            return widthPx > 0 && heightPx > 0;
        }

        private static bool TryMeasureTextBlockTextSizePx(TextBlock tb, out double widthPx, out double heightPx)
        {
            widthPx = 0;
            heightPx = 0;
            if (tb == null || string.IsNullOrWhiteSpace(tb.Text))
            {
                return false;
            }

            double maxLayoutW = double.PositiveInfinity;
            if (!double.IsNaN(tb.Width) && tb.Width > 0)
            {
                maxLayoutW = tb.Width;
            }
            else if (!double.IsNaN(tb.MaxWidth) && tb.MaxWidth > 0)
            {
                maxLayoutW = tb.MaxWidth;
            }

            double pixelsPerDip = 1.0;
            try
            {
                pixelsPerDip = VisualTreeHelper.GetDpi(tb).PixelsPerDip;
            }
            catch
            {
                pixelsPerDip = 1.0;
            }

            var formatted = new FormattedText(
                tb.Text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight, tb.FontStretch),
                tb.FontSize,
                Brushes.Black,
                pixelsPerDip);
            if (maxLayoutW < double.PositiveInfinity)
            {
                formatted.MaxTextWidth = maxLayoutW;
            }

            // .NET 4.6: Width (không khoảng trắng cuối) — khớp PhaDoTitleBlockMetrics
            widthPx = Math.Ceiling(formatted.Width);
            heightPx = Math.Ceiling(formatted.Height);
            return widthPx > 0.5 && heightPx > 0.5;
        }

        private void ApplyPersonOffsetClamped(
            int familyId,
            int personSlot,
            FrameworkElement element,
            double deltaXmm,
            double deltaYmm)
        {
            bool verticalCards = _phaDoCurrentOptions != null
                && GiaPhaRenderOptions.IsVerticalCardLayout(_phaDoCurrentOptions.CardLayoutMode);

            double newLeft = _phaDoPersonNaturalLeftPx + MmToPx(deltaXmm);
            double newTop = _phaDoPersonNaturalTopPx + MmToPx(deltaYmm);

            if (verticalCards)
            {
                if (!TryGetFamilyInnerContentBoundsPx(familyId, out double areaLeft, out double areaTop, out double areaW, out double areaH))
                {
                    return;
                }

                double elLeft = Canvas.GetLeft(element);
                double elTop = Canvas.GetTop(element);
                if (double.IsNaN(elLeft))
                {
                    elLeft = 0;
                }

                if (double.IsNaN(elTop))
                {
                    elTop = 0;
                }

                double textRelL;
                double textRelT;
                double textW;
                double textH;
                if (TryMeasurePersonTextSelectionBounds(element, out double textLeft, out double textTop, out textW, out textH))
                {
                    textRelL = textLeft - elLeft;
                    textRelT = textTop - elTop;
                }
                else
                {
                    GetPersonElementSize(element, out textW, out textH);
                    textRelL = 0;
                    textRelT = 0;
                }

                double areaRight = areaLeft + areaW;
                double areaBottom = areaTop + areaH;
                double minLeft = areaLeft - textRelL;
                double maxLeft = areaRight - textRelL - textW;
                double minTop = areaTop - textRelT;
                double maxTop = areaBottom - textRelT - textH;

                if (maxLeft >= minLeft)
                {
                    newLeft = Math.Max(minLeft, Math.Min(maxLeft, newLeft));
                }
                else
                {
                    newLeft = minLeft;
                }

                if (maxTop >= minTop)
                {
                    newTop = Math.Max(minTop, Math.Min(maxTop, newTop));
                }
                else
                {
                    newTop = minTop;
                }
            }
            else
            {
                // Chữ ngang: ngang = khung chữ trong vùng padding; dọc = cả ô + chiều cao dòng (wrap/line height).
                if (!TryGetFamilyBackgroundBounds(familyId, out double boxLeft, out double boxTop, out double boxW, out double boxH))
                {
                    return;
                }

                if (!TryGetFamilyInnerContentBoundsPx(familyId, out double areaLeft, out _, out double areaW, out _))
                {
                    double padPx = MmToPx(_phaDoCurrentOptions?.CardPaddingMm ?? 2.5);
                    areaLeft = boxLeft + padPx;
                    areaW = Math.Max(4, boxW - 2 * padPx);
                }

                double elLeft = Canvas.GetLeft(element);
                double elTop = Canvas.GetTop(element);
                if (double.IsNaN(elLeft))
                {
                    elLeft = 0;
                }

                if (double.IsNaN(elTop))
                {
                    elTop = 0;
                }

                double textRelL;
                double textW;
                if (TryMeasurePersonTextSelectionBounds(element, out double textLeft, out _, out textW, out _))
                {
                    textRelL = textLeft - elLeft;
                }
                else
                {
                    GetPersonElementSize(element, out textW, out _);
                    textRelL = 0;
                }

                double minLeft = areaLeft - textRelL;
                double maxLeft = areaLeft + areaW - textRelL - textW;
                if (maxLeft >= minLeft)
                {
                    newLeft = Math.Max(minLeft, Math.Min(maxLeft, newLeft));
                }
                else
                {
                    newLeft = minLeft;
                }

                GetPersonElementSize(element, out _, out double elH);
                newTop = Math.Max(boxTop, Math.Min(boxTop + boxH - elH, newTop));
            }

            Canvas.SetLeft(element, newLeft);
            Canvas.SetTop(element, newTop);

            deltaXmm = PxToMm(newLeft - _phaDoPersonNaturalLeftPx);
            deltaYmm = PxToMm(newTop - _phaDoPersonNaturalTopPx);
            var style = GetBoxStyleForFamily(familyId);
            SetPersonOffset(style, personSlot, deltaXmm, deltaYmm);
            _phaDoBoxStyleByFamilyId[familyId] = style;
        }

        /// <summary>Ghi nhận gesture kéo text — không đổi selection (caller đã chọn text).</summary>
        private void QueuePersonTextDrag(int familyId, int personSlot, FrameworkElement element, Point canvasPoint)
        {
            CancelPendingPersonDrag();

            _phaDoPendingPersonDrag = true;
            _phaDoPendingPersonFamilyId = familyId;
            _phaDoPendingPersonSlot = personSlot;
            _phaDoPendingPersonElement = element;
            _phaDoPendingPersonDragStart = canvasPoint;
        }

        /// <summary>MouseDown trên text trong ô — chỉ luồng text, không lẫn select box.</summary>
        private void HandleFamilyBoxTextMouseDown(
            MouseButtonEventArgs e,
            int familyId,
            int personSlot,
            FrameworkElement element)
        {
            if (!_phaDoMultiSelectedFamilyIds.Contains(familyId))
            {
                _phaDoMultiSelectedFamilyIds.Clear();
                _phaDoMultiSelectedFamilyIds.Add(familyId);
            }

            SelectPhaDoFamilyText(familyId, personSlot);
            QueuePersonTextDrag(familyId, personSlot, element, e.GetPosition(theCanvas));
            e.Handled = true;
        }

        private void QueueTitleLineDrag(int lineIndex, FrameworkElement element, Point canvasPoint)
        {
            CancelPendingTitleDrag();
            _phaDoPendingTitleDrag = true;
            _phaDoPendingTitleLine = lineIndex;
            _phaDoPendingTitleElement = element;
            _phaDoPendingTitleDragStart = canvasPoint;
        }

        /// <summary>MouseDown trên dòng chữ khi khối title đã chọn — chỉ luồng text (chọn dòng + chờ kéo).</summary>
        private void HandleTitleTextMouseDown(MouseButtonEventArgs e, int lineIndex, FrameworkElement element)
        {
            SelectTitleTextLine(lineIndex);
            QueueTitleLineDrag(lineIndex, element, e.GetPosition(theCanvas));
            e.Handled = true;
        }

        private void CancelPendingTitleDrag()
        {
            _phaDoPendingTitleDrag = false;
            _phaDoPendingTitleLine = -1;
            _phaDoPendingTitleElement = null;
        }

        private void TryStartPendingTitleDrag(Point canvasPoint)
        {
            if (!_phaDoPendingTitleDrag || _phaDoIsDraggingTitleLine || _phaDoPendingTitleElement == null)
            {
                return;
            }

            var delta = canvasPoint - _phaDoPendingTitleDragStart;
            if (Math.Abs(delta.X) < 3 && Math.Abs(delta.Y) < 3)
            {
                return;
            }

            int lineIndex = _phaDoPendingTitleLine;
            var element = _phaDoPendingTitleElement;
            Point start = _phaDoPendingTitleDragStart;
            CancelPendingTitleDrag();
            BeginTitleLineDrag(lineIndex, element, start);
            UpdateTitleLineDrag(canvasPoint);
        }

        private PhaDoPersonLayoutOffset GetTitleLineOffset(int lineIndex)
        {
            if (_phaDoTitleStyle?.LineOffsetsByIndex != null
                && _phaDoTitleStyle.LineOffsetsByIndex.TryGetValue(lineIndex, out var offset)
                && offset != null)
            {
                return offset;
            }

            return new PhaDoPersonLayoutOffset();
        }

        private void SetTitleLineOffset(int lineIndex, double deltaXmm, double deltaYmm)
        {
            if (_phaDoTitleStyle == null)
            {
                _phaDoTitleStyle = new PhaDoTitleStyle();
            }

            if (_phaDoTitleStyle.LineOffsetsByIndex == null)
            {
                _phaDoTitleStyle.LineOffsetsByIndex = new Dictionary<int, PhaDoPersonLayoutOffset>();
            }

            if (Math.Abs(deltaXmm) < 0.01 && Math.Abs(deltaYmm) < 0.01)
            {
                _phaDoTitleStyle.LineOffsetsByIndex.Remove(lineIndex);
            }
            else
            {
                _phaDoTitleStyle.LineOffsetsByIndex[lineIndex] = new PhaDoPersonLayoutOffset
                {
                    DeltaXmm = deltaXmm,
                    DeltaYmm = deltaYmm
                };
            }
        }

        private void ApplyTitleLineOffsets()
        {
            if (_phaDoTitleStyle?.LineOffsetsByIndex == null || _phaDoTitleStyle.LineOffsetsByIndex.Count == 0)
            {
                return;
            }

            foreach (var child in theCanvas.Children.OfType<FrameworkElement>())
            {
                if (child.Tag is PhaDoTitleTextLineTag tag
                    && _phaDoTitleStyle.LineOffsetsByIndex.TryGetValue(tag.LineIndex, out var offset)
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

                    Canvas.SetLeft(child, left + MmToPxRender(offset.DeltaXmm));
                    Canvas.SetTop(child, top + MmToPxRender(offset.DeltaYmm));
                }
            }
        }

        private bool TryGetTitleBlockBoundsPx(out double leftPx, out double topPx, out double widthPx, out double heightPx)
        {
            leftPx = topPx = widthPx = heightPx = 0;
            if (theCanvas == null)
            {
                return false;
            }

            // Ưu tiên hit rect trên canvas — khớp vùng kéo/select thực tế
            var hitRect = theCanvas.Children
                .OfType<FrameworkElement>()
                .FirstOrDefault(fe => fe.Tag is PhaDoTitleHitTag);
            if (hitRect != null)
            {
                leftPx = Canvas.GetLeft(hitRect);
                topPx = Canvas.GetTop(hitRect);
                if (double.IsNaN(leftPx))
                {
                    leftPx = 0;
                }

                if (double.IsNaN(topPx))
                {
                    topPx = 0;
                }

                if (TryGetFrameworkElementSizePx(hitRect, out widthPx, out heightPx))
                {
                    return true;
                }
            }

            if (_phaDoCurrentOptions == null || _phaDoRenderedLayout == null)
            {
                return false;
            }

            var layout = PhaDoTitleBlockMetrics.Measure(_phaDoCurrentOptions, GetPhaDoRenderDpi());
            if (layout.WidthMm < 0.1 || layout.HeightMm < 0.1)
            {
                return false;
            }

            leftPx = MmToPxRender(layout.LeftMm);
            topPx = MmToPxRender(layout.TopMm);
            widthPx = MmToPxRender(layout.WidthMm);
            heightPx = MmToPxRender(layout.HeightMm);
            return true;
        }

        private void ApplyTitleLineOffsetClamped(
            int lineIndex,
            FrameworkElement element,
            double deltaXmm,
            double deltaYmm)
        {
            if (!TryGetTitleBlockBoundsPx(out double boxLeft, out double boxTop, out double boxW, out double boxH))
            {
                return;
            }

            GetPersonElementSize(element, out double elW, out double elH);
            double newLeft = _phaDoTitleLineNaturalLeftPx + MmToPxRender(deltaXmm);
            double newTop = _phaDoTitleLineNaturalTopPx + MmToPxRender(deltaYmm);
            newLeft = Math.Max(boxLeft, Math.Min(boxLeft + boxW - elW, newLeft));
            newTop = Math.Max(boxTop, Math.Min(boxTop + boxH - elH, newTop));

            Canvas.SetLeft(element, newLeft);
            Canvas.SetTop(element, newTop);

            deltaXmm = PxToMmRender(newLeft - _phaDoTitleLineNaturalLeftPx);
            deltaYmm = PxToMmRender(newTop - _phaDoTitleLineNaturalTopPx);
            SetTitleLineOffset(lineIndex, deltaXmm, deltaYmm);
        }

        private void BeginTitleLineDrag(int lineIndex, FrameworkElement element, Point canvasStart)
        {
            var offset = GetTitleLineOffset(lineIndex);
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

            _phaDoIsDraggingTitleLine = true;
            _phaDoDraggingTitleLine = lineIndex;
            _phaDoDraggingTitleElement = element;
            _phaDoTitleLineNaturalLeftPx = left - MmToPxRender(offset.DeltaXmm);
            _phaDoTitleLineNaturalTopPx = top - MmToPxRender(offset.DeltaYmm);
            _phaDoTitleLineDragStartCanvas = canvasStart;
            _phaDoTitleLineDragStartDeltaXmm = offset.DeltaXmm;
            _phaDoTitleLineDragStartDeltaYmm = offset.DeltaYmm;
            theCanvas.CaptureMouse();
            theCanvas.Cursor = Cursors.SizeAll;
        }

        private void UpdateTitleLineDrag(Point canvasPoint)
        {
            if (!_phaDoIsDraggingTitleLine || _phaDoDraggingTitleElement == null)
            {
                return;
            }

            var delta = GetPhaDoCanvasDeltaMmRender(canvasPoint, _phaDoTitleLineDragStartCanvas);
            double newDeltaX = _phaDoTitleLineDragStartDeltaXmm + delta.X;
            double newDeltaY = _phaDoTitleLineDragStartDeltaYmm + delta.Y;
            ApplyTitleLineOffsetClamped(
                _phaDoDraggingTitleLine,
                _phaDoDraggingTitleElement,
                newDeltaX,
                newDeltaY);
            DrawTitleLineHighlight(_phaDoDraggingTitleLine);
        }

        private void EndTitleLineDrag()
        {
            if (!_phaDoIsDraggingTitleLine)
            {
                return;
            }

            _phaDoIsDraggingTitleLine = false;
            _phaDoDraggingTitleLine = -1;
            _phaDoDraggingTitleElement = null;
            theCanvas.ReleaseMouseCapture();
            theCanvas.Cursor = null;

            if (_phaDoTitleSelectedLine >= 0)
            {
                DrawTitleLineHighlight(_phaDoTitleSelectedLine);
            }

            SaveWorkspaceSession();
        }

        /// <summary>MouseDown trên nền ô — chỉ luồng box (select/drag cả ô).</summary>
        private void HandleFamilyBoxBackgroundMouseDown(
            MouseButtonEventArgs e,
            int familyId,
            GiaPhaPlacedNode node)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (_phaDoMultiSelectedFamilyIds.Contains(familyId))
                {
                    _phaDoMultiSelectedFamilyIds.Remove(familyId);
                    if (_phaDoSelectedFamilyId == familyId)
                    {
                        _phaDoSelectedFamilyId = _phaDoMultiSelectedFamilyIds.FirstOrDefault();
                    }
                }
                else
                {
                    _phaDoMultiSelectedFamilyIds.Add(familyId);
                    _phaDoSelectedFamilyId = familyId;
                }

                if (_phaDoSelectedFamilyId > 0)
                {
                    SelectPhaDoBoxOutline(_phaDoSelectedFamilyId);
                }
                else
                {
                    ClearPhaDoBoxSelections();
                }

                e.Handled = true;
                return;
            }

            if (!_phaDoMultiSelectedFamilyIds.Contains(familyId))
            {
                _phaDoMultiSelectedFamilyIds.Clear();
                _phaDoMultiSelectedFamilyIds.Add(familyId);
            }

            SelectPhaDoBoxOutline(familyId);

            if (!IsHitOnBoxBackground(e.OriginalSource as DependencyObject, familyId))
            {
                e.Handled = true;
                return;
            }

            CancelPendingPersonDrag();
            _phaDoIsDragging = true;
            _phaDoMouseMovedWhileDrag = false;
            _phaDoDraggingFamilyId = familyId;
            _phaDoDragStartPoint = e.GetPosition(theCanvas);
            _phaDoDragStartNodeXmm = node.Xmm;
            _phaDoDragStartXmmByFamilyId.Clear();
            foreach (var id in _phaDoMultiSelectedFamilyIds)
            {
                var n = FindNodeByFamilyId(id);
                if (n != null)
                {
                    _phaDoDragStartXmmByFamilyId[id] = n.Xmm;
                }
            }

            theCanvas.CaptureMouse();
            e.Handled = true;
        }

        private void CancelPendingPersonDrag()
        {
            _phaDoPendingPersonDrag = false;
            _phaDoPendingPersonFamilyId = 0;
            _phaDoPendingPersonSlot = -1;
            _phaDoPendingPersonElement = null;
        }

        /// <summary>Sau mousedown, chỉ bắt drag khi user kéo quá vài px (tránh nhầm click thường).</summary>
        private void TryStartPendingPersonDrag(Point canvasPoint)
        {
            if (!_phaDoPendingPersonDrag || _phaDoIsDraggingPerson || _phaDoPendingPersonElement == null)
            {
                return;
            }

            var delta = canvasPoint - _phaDoPendingPersonDragStart;
            if (Math.Abs(delta.X) < 3 && Math.Abs(delta.Y) < 3)
            {
                return;
            }

            int familyId = _phaDoPendingPersonFamilyId;
            int personSlot = _phaDoPendingPersonSlot;
            var element = _phaDoPendingPersonElement;
            Point start = _phaDoPendingPersonDragStart;
            CancelPendingPersonDrag();
            BeginPersonDrag(familyId, personSlot, element, start);
            UpdatePersonDrag(canvasPoint);
        }

        private void BeginPersonDrag(int familyId, int personSlot, FrameworkElement element, Point canvasStart)
        {
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
            _phaDoPersonDragStartCanvas = canvasStart;
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

            // Kết thúc kéo text — giữ highlight text, không chuyển sang mode chọn box
            if (_phaDoSelectedPersonSlot.HasValue && _phaDoSelectedFamilyId > 0)
            {
                UpdatePersonSelectionHighlight();
            }
            else if (_phaDoSelectedFamilyId > 0)
            {
                DrawSelectionOverlay(_phaDoSelectedFamilyId);
            }

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
            EnsureFamilyBoxFrameMarkupResolved(style);

            // Đánh dấu trực quan phả con:
            // - scope stop (bắt đầu phả con mới): cam đậm #F57C00
            // - non-STOP (nhánh nhỏ, vẽ tiếp luôn): xanh ngọc #4DD0E1
            if (_phaDoScopeStopFamilyIdsAtMaxLevel != null
                && _phaDoScopeStopFamilyIdsAtMaxLevel.Count > 0
                && _phaDoScopeStopFamilyIdsAtMaxLevel.Contains(familyId))
            {
                // Cam đậm rõ → người dùng nhận ra ngay "đây là bắt đầu phả con".
                style.FillColorHex = "#F57C00";
                style.Main.ForegroundHex = "#FFFFFF";
                style.Spouse.ForegroundHex = "#FFF3E0";
            }
            else if (_phaConStopFamilyIds.Contains(familyId))
            {
                // Xanh ngọc → nhánh nhỏ, vẽ tiếp trong phả cha (không tách scope).
                style.FillColorHex = "#4DD0E1";
                style.Main.ForegroundHex = "#004D40";
                style.Spouse.ForegroundHex = "#00695C";
            }

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
                EnsureFamilyBoxFrameMarkupResolved(style);
                _phaDoBoxStyleByFamilyId[familyId] = style;
            }
        }

        /// <summary>Nạp markup family_* từ thư mục zone khi chỉ có ShapeSvgId (preview dùng zone, catalog GP có thể không có).</summary>
        private void EnsureFamilyBoxFrameMarkupResolved(PhaDoBoxStyle style)
        {
            if (style == null || string.IsNullOrWhiteSpace(style.ShapeSvgId))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(style.CustomShapeSvg))
            {
                return;
            }

            ApplyZoneSvgAsFamilyBoxFrame(style, style.ShapeSvgId);
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
            if (_phaDoSelectedPersonSlot.HasValue)
            {
                SelectPhaDoFamilyText(sourceFamilyId, _phaDoSelectedPersonSlot.Value);
            }
            else
            {
                SelectPhaDoBoxOutline(sourceFamilyId);
            }
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
                () => viewModel != null && viewModel.SaveFileCommandFunc(),
                () => PopulateZoneCombos())
            {
                Owner = this
            };
            win.Show();
        }

        private void OpenHelpDialog_Click(object sender, RoutedEventArgs e)
        {
            // Nếu cửa sổ đã mở thì chỉ đưa lên trước, không tạo thêm
            if (_helpDialog != null)
            {
                _helpDialog.Activate();
                if (_helpDialog.WindowState == WindowState.Minimized)
                    _helpDialog.WindowState = WindowState.Normal;
                return;
            }

            _helpDialog = new HelpDialog { Owner = this };
            // Dọn field khi cửa sổ bị đóng
            _helpDialog.Closed += (s, args) => _helpDialog = null;
            _helpDialog.Show();
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

        // ═══════════════════════════════════════════════════════════════
        //  Thanh công cụ ngữ cảnh (context toolbar) phả đồ
        // ═══════════════════════════════════════════════════════════════

        private enum PhaDoCtxSelType { None, GenLabel, Box, TitleBlock, TitleText, PersonText }
        private PhaDoCtxSelType _phaDoCtxSelType = PhaDoCtxSelType.None;

        /// <summary>Item đơn giản cho ComboBox zone SVG.</summary>
        private sealed class PhaDoZoneComboItem
        {
            public string SvgId { get; }
            public string Display { get; }
            public PhaDoZoneComboItem(string svgId, string display) { SvgId = svgId; Display = display; }
        }

        /// <summary>SVG đọc từ thư mục ZoneSvg (title_* / family_*).</summary>
        private Dictionary<string, GiaPhaRender.PhaDoSvgShape> _phaDoZoneSvgFromFolder
            = new Dictionary<string, GiaPhaRender.PhaDoSvgShape>(StringComparer.OrdinalIgnoreCase);

        private List<PhaDoZoneComboItem> _phaDoTitleZoneComboItems = new List<PhaDoZoneComboItem>();
        private List<PhaDoZoneComboItem> _phaDoFamilyZoneComboItems = new List<PhaDoZoneComboItem>();

        /// <summary>Tránh áp dụng SVG khi đang đồng bộ combo từ style hiện có.</summary>
        private bool _phaDoToolbarZoneComboSyncing;

        /// <summary>Ô gia đình đang xem trước khung SVG (để khôi phục sau khi hủy preview).</summary>
        private int _phaDoZonePreviewFamilyId;

        /// <summary>Đánh dấu loại object, highlight nhóm active trên toolbar bằng Opacity.</summary>
        private void ShowContextToolbar(PhaDoCtxSelType selType)
        {
            _phaDoCtxSelType = selType;
            bool fontActive = selType == PhaDoCtxSelType.GenLabel
                || selType == PhaDoCtxSelType.TitleText
                || selType == PhaDoCtxSelType.PersonText;
            bool svgActive = selType == PhaDoCtxSelType.Box
                || selType == PhaDoCtxSelType.TitleBlock;

            if (phaDoCtxFontGroup != null)
            {
                phaDoCtxFontGroup.Opacity = fontActive ? 1.0 : 0.4;
            }

            if (phaDoCtxSvgGroup != null)
            {
                phaDoCtxSvgGroup.Opacity = svgActive ? 1.0 : 0.4;
            }

            // Khối title: chỉ combo Title Box; ô gia đình: chỉ combo Family Box
            bool titleBlockMode = selType == PhaDoCtxSelType.TitleBlock;
            bool familyBoxMode = selType == PhaDoCtxSelType.Box;
            if (phaDoCtxTitleZoneCombo != null)
            {
                phaDoCtxTitleZoneCombo.IsEnabled = titleBlockMode;
                phaDoCtxTitleZoneCombo.Opacity = titleBlockMode ? 1.0 : 0.4;
            }

            if (phaDoCtxFamilyBoxFrameCombo != null)
            {
                phaDoCtxFamilyBoxFrameCombo.IsEnabled = familyBoxMode;
                phaDoCtxFamilyBoxFrameCombo.Opacity = familyBoxMode ? 1.0 : 0.4;
            }
        }

        private void HideContextToolbar()
        {
            HideZoneSvgToolbarPreview();
            _phaDoCtxSelType = PhaDoCtxSelType.None;
            // Cả 2 nhóm về mờ khi không có gì được chọn
            if (phaDoCtxFontGroup != null) phaDoCtxFontGroup.Opacity = 0.4;
            if (phaDoCtxSvgGroup  != null) phaDoCtxSvgGroup.Opacity  = 0.4;
        }

        /// <summary>Điền font size + màu + bold vào toolbar từ GenLabelStyle hiện tại.</summary>
        private void SyncToolbarFromGenLabel()
        {
            ShowContextToolbar(PhaDoCtxSelType.GenLabel);
            var st = _phaDoCurrentOptions?.GenLabelStyle;
            bool vertical = _phaDoCurrentOptions != null &&
                            GiaPhaRenderOptions.IsVerticalCardLayout(_phaDoCurrentOptions.CardLayoutMode);
            double defPt = vertical
                ? (_phaDoCurrentOptions?.VerticalGenerationLabelFontPt ?? 10)
                : (_phaDoCurrentOptions?.HeaderFontPt ?? 7);

            if (phaDoCtxFontSizeBox != null)
                phaDoCtxFontSizeBox.Text = (st?.FontPt > 0 ? st.FontPt : defPt).ToString("0.#");

            // Chọn font family (null = mặc định theo options)
            if (phaDoCtxFontFamilyCombo != null)
            {
                string family = !string.IsNullOrWhiteSpace(st?.FontFamily) ? st.FontFamily : "(mặc định)";
                phaDoCtxFontFamilyCombo.SelectedItem = family;
                if (phaDoCtxFontFamilyCombo.SelectedItem == null && phaDoCtxFontFamilyCombo.Items.Count > 0)
                {
                    phaDoCtxFontFamilyCombo.SelectedIndex = 0;
                }
            }

            string defColor = vertical ? "#19376A" : "#5A5A5A";
            if (phaDoCtxColorBox != null)
                phaDoCtxColorBox.Text = !string.IsNullOrWhiteSpace(st?.ForegroundHex)
                    ? st.ForegroundHex : defColor;
            PhaDoCtxColorBox_RefreshPreview();

            if (phaDoCtxBoldBtn != null)
                phaDoCtxBoldBtn.IsChecked = st?.Bold ?? vertical;
        }

        /// <summary>Điền toolbar font từ style của phần chữ đang chọn trong ô gia đình.</summary>
        private void SyncToolbarFromFamilyPersonText(int familyId, int personSlot)
        {
            ShowContextToolbar(PhaDoCtxSelType.PersonText);
            var element = FindPersonVisual(familyId, personSlot);
            if (!(element?.Tag is PhaDoBoxVisualTag visualTag))
            {
                return;
            }

            var resolved = ResolvePersonTextStyle(familyId, visualTag, out bool boldDefault);
            double dpi = _phaDoCurrentOptions?.PrintDpi ?? 96;
            double pt = resolved?.FontPt ?? GetDefaultFontPtForVisualTag(visualTag);
            string hex = resolved?.ForegroundHex ?? GetDefaultForegroundHexForVisualTag(visualTag);
            string fontFamily = resolved?.FontFamilyName;
            bool bold = resolved?.Bold ?? boldDefault;

            // Ưu tiên giá trị đang hiển thị trên canvas nếu có
            if (element is TextBlock tb)
            {
                pt = tb.FontSize * 72.0 / dpi;
                bold = tb.FontWeight >= FontWeights.SemiBold;
                try
                {
                    if (tb.Foreground is SolidColorBrush scb)
                    {
                        hex = scb.Color.ToString();
                    }
                }
                catch { }
                if (tb.FontFamily != null)
                {
                    fontFamily = tb.FontFamily.Source;
                }
            }

            if (phaDoCtxFontSizeBox != null)
            {
                phaDoCtxFontSizeBox.Text = pt.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            }

            if (phaDoCtxFontFamilyCombo != null)
            {
                string comboVal = string.IsNullOrWhiteSpace(fontFamily) ? "(mặc định)" : fontFamily;
                phaDoCtxFontFamilyCombo.SelectedItem = comboVal;
                if (phaDoCtxFontFamilyCombo.SelectedItem == null && phaDoCtxFontFamilyCombo.Items.Count > 0)
                {
                    phaDoCtxFontFamilyCombo.SelectedIndex = 0;
                }
            }

            if (phaDoCtxColorBox != null)
            {
                phaDoCtxColorBox.Text = hex;
            }

            PhaDoCtxColorBox_RefreshPreview();

            if (phaDoCtxBoldBtn != null)
            {
                phaDoCtxBoldBtn.IsChecked = bold;
            }
        }

        private double GetDefaultFontPtForVisualTag(PhaDoBoxVisualTag tag)
        {
            if (_phaDoCurrentOptions == null || tag == null)
            {
                return 9;
            }

            switch (tag.ElementKind)
            {
                case PhaDoBoxElementKind.GenerationLabel:
                    return _phaDoCurrentOptions.HeaderFontPt;
                case PhaDoBoxElementKind.ExtraNote:
                    return _phaDoCurrentOptions.NoteFontPt > 0 ? _phaDoCurrentOptions.NoteFontPt : 6.5;
                case PhaDoBoxElementKind.Person:
                    return tag.Role == PhaDoPersonTextRole.Main
                        ? _phaDoCurrentOptions.MainNameFontPt
                        : _phaDoCurrentOptions.SpouseFontPt;
                default:
                    return _phaDoCurrentOptions.MainNameFontPt;
            }
        }

        private string GetDefaultForegroundHexForVisualTag(PhaDoBoxVisualTag tag)
        {
            if (tag == null)
            {
                return "#000000";
            }

            switch (tag.ElementKind)
            {
                case PhaDoBoxElementKind.GenerationLabel:
                    return "#464646";
                case PhaDoBoxElementKind.ExtraNote:
                    return "#5A606C";
                default:
                    return "#000000";
            }
        }

        private PhaDoPersonTextStyle ResolvePersonTextStyle(int familyId, PhaDoBoxVisualTag tag, out bool boldDefault)
        {
            boldDefault = tag?.Role == PhaDoPersonTextRole.Main;
            var boxStyle = GetBoxStyleForFamily(familyId);
            if (boxStyle?.PersonTextStylesBySlot != null
                && tag != null
                && boxStyle.PersonTextStylesBySlot.TryGetValue(tag.PersonSlotIndex, out var custom)
                && custom != null
                && !custom.IsEmpty())
            {
                boldDefault = custom.Bold ?? boldDefault;
                return custom;
            }

            if (tag?.ElementKind == PhaDoBoxElementKind.Person)
            {
                var roleStyle = tag.Role == PhaDoPersonTextRole.Main ? boxStyle.Main : boxStyle.Spouse;
                if (roleStyle != null && !roleStyle.IsEmpty())
                {
                    boldDefault = roleStyle.Bold ?? boldDefault;
                    return roleStyle.Clone();
                }
            }

            return new PhaDoPersonTextStyle
            {
                FontPt = GetDefaultFontPtForVisualTag(tag),
                ForegroundHex = GetDefaultForegroundHexForVisualTag(tag),
                Bold = boldDefault
            };
        }

        private PhaDoPersonTextStyle ReadPersonTextStyleFromToolbar(PhaDoBoxVisualTag tag)
        {
            double.TryParse(phaDoCtxFontSizeBox?.Text,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double pt);

            string hex = phaDoCtxColorBox?.Text?.Trim();
            try { ColorConverter.ConvertFromString(hex); } catch { hex = null; }

            string fontFamily = (phaDoCtxFontFamilyCombo?.SelectedItem as string) == "(mặc định)"
                ? null
                : (phaDoCtxFontFamilyCombo?.SelectedItem as string);

            return new PhaDoPersonTextStyle
            {
                FontPt = pt > 0 ? pt : GetDefaultFontPtForVisualTag(tag),
                ForegroundHex = hex,
                FontFamilyName = fontFamily,
                Bold = phaDoCtxBoldBtn?.IsChecked == true
            };
        }

        private void ApplyToolbarToFamilyPersonText(int familyId, int personSlot)
        {
            var element = FindPersonVisual(familyId, personSlot);
            if (!(element?.Tag is PhaDoBoxVisualTag visualTag))
            {
                return;
            }

            var personStyle = ReadPersonTextStyleFromToolbar(visualTag);
            var boxStyle = GetBoxStyleForFamily(familyId);
            if (boxStyle.PersonTextStylesBySlot == null)
            {
                boxStyle.PersonTextStylesBySlot = new Dictionary<int, PhaDoPersonTextStyle>();
            }

            boxStyle.PersonTextStylesBySlot[personSlot] = personStyle;
            _phaDoBoxStyleByFamilyId[familyId] = boxStyle;

            ApplyPersonVisualStyle(element, visualTag, personStyle);
            UpdatePersonSelectionHighlight();
            SaveWorkspaceSession();
        }

        private void ApplyPersonVisualStyle(FrameworkElement element, PhaDoBoxVisualTag tag, PhaDoPersonTextStyle personStyle)
        {
            double dpi = _phaDoCurrentOptions?.PrintDpi ?? 96;
            string defaultFont = _phaDoCurrentOptions?.FontFamilyName ?? "Segoe UI";
            double defaultPt = GetDefaultFontPtForVisualTag(tag);
            bool boldDefault = tag.Role == PhaDoPersonTextRole.Main;

            if (element is TextBlock tb)
            {
                ApplyPersonTextStyle(tb, personStyle, defaultPt, defaultFont, dpi, boldDefault);
                return;
            }

            if (element is StackPanel column)
            {
                foreach (var line in column.Children.OfType<TextBlock>())
                {
                    ApplyPersonTextStyle(line, personStyle, defaultPt, defaultFont, dpi, boldDefault);
                }
            }
        }

        private void SyncPhaDoToolbarFromBoxStyle(int familyId)
        {
            ShowContextToolbar(PhaDoCtxSelType.Box);
            var style = GetBoxStyleForFamily(familyId);
            PopulateZoneCombos();
            SelectZoneComboItem(phaDoCtxFamilyBoxFrameCombo, GetFamilyBoxFrameSvgIdForToolbar(style));
            // Preview chỉ khi đổi combo — không gọi ở đây để giữ tag nền và viền chọn.
        }

        /// <summary>Điền toolbar khi chọn khối title — combo Title Box = khung title.</summary>
        private void SyncToolbarFromTitleBlock()
        {
            ShowContextToolbar(PhaDoCtxSelType.TitleBlock);
            PopulateZoneCombos();
            SelectZoneComboItem(
                phaDoCtxTitleZoneCombo,
                GetTitleBlockFrameSvgIdForToolbar(_phaDoTitleStyle));
        }

        /// <summary>Id SVG khung title (title_*) cho combo Title Box.</summary>
        private string GetTitleBlockFrameSvgIdForToolbar(PhaDoTitleStyle titleStyle)
        {
            if (titleStyle == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(titleStyle.ShapeSvgId)
                && titleStyle.ShapeSvgId.StartsWith(
                    GiaPhaRender.PhaDoZoneSvgFolderLoader.TitlePrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return titleStyle.ShapeSvgId;
            }

            string markup = titleStyle.CustomShapeSvg;
            if (string.IsNullOrWhiteSpace(markup) || _phaDoZoneSvgFromFolder == null)
            {
                return titleStyle.ShapeSvgId;
            }

            foreach (var kv in _phaDoZoneSvgFromFolder)
            {
                if (!kv.Key.StartsWith(
                        GiaPhaRender.PhaDoZoneSvgFolderLoader.TitlePrefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string candidate = kv.Value?.GetSvgMarkup();
                if (!string.IsNullOrWhiteSpace(candidate) && candidate == markup)
                {
                    return kv.Key;
                }
            }

            return titleStyle.ShapeSvgId;
        }

        /// <summary>Gán title_*.svg làm khung/nền khối tiêu đề.</summary>
        private void ApplyZoneSvgAsTitleBlockFrame(PhaDoTitleStyle titleStyle, string svgId)
        {
            if (titleStyle == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(svgId))
            {
                titleStyle.ShapeSvgId = null;
                titleStyle.CustomShapeSvg = null;
                return;
            }

            if (TryGetZoneSvgShape(svgId, out var shape) && shape != null)
            {
                titleStyle.CustomShapeSvg = shape.GetSvgMarkup();
                titleStyle.CustomShapeViewBoxWidth = shape.ViewBoxWidth > 0 ? shape.ViewBoxWidth : 100;
                titleStyle.CustomShapeViewBoxHeight = shape.ViewBoxHeight > 0 ? shape.ViewBoxHeight : 80;
                titleStyle.ShapeSvgId = svgId;
                return;
            }

            var catalog = viewModel?.FamilyTree?.GP?.SvgShapesById;
            if (catalog != null)
            {
                titleStyle.ShapeSvgId = svgId;
                PhaDoSvgCatalog.ResolveShapeIntoFrame(titleStyle, catalog);
            }
        }

        /// <summary>Áp dụng title_*.svg từ combo Title Box lên khối tiêu đề.</summary>
        private void ApplyTitleBlockToolbarSvg()
        {
            HideZoneSvgToolbarPreview();

            if (_phaDoCurrentOptions == null)
            {
                return;
            }

            string titleFrameId = (phaDoCtxTitleZoneCombo?.SelectedItem as PhaDoZoneComboItem)?.SvgId;
            if (_phaDoTitleStyle == null)
            {
                _phaDoTitleStyle = new PhaDoTitleStyle();
            }

            ApplyZoneSvgAsTitleBlockFrame(_phaDoTitleStyle, titleFrameId);
            ApplyPhaDoTitleStyleToOptions(_phaDoCurrentOptions);
            RedrawTitleBlockOnly();

            if (_phaDoTitleSelectedLine >= 0)
            {
                DrawTitleLineHighlight(_phaDoTitleSelectedLine);
            }
            else if (_phaDoTitleSelected)
            {
                DrawTitleSelectionOverlay();
            }

            SaveWorkspaceSession();
        }

        /// <summary>Id SVG khung ô gia đình (family_*) để hiển thị trên combo Family Box.</summary>
        private string GetFamilyBoxFrameSvgIdForToolbar(PhaDoBoxStyle style)
        {
            if (style == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(style.ShapeSvgId)
                && style.ShapeSvgId.StartsWith(
                    GiaPhaRender.PhaDoZoneSvgFolderLoader.FamilyPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return style.ShapeSvgId;
            }

            string markup = style.CustomShapeSvg;
            if (string.IsNullOrWhiteSpace(markup) || _phaDoZoneSvgFromFolder == null)
            {
                return style.ShapeSvgId;
            }

            foreach (var kv in _phaDoZoneSvgFromFolder)
            {
                if (!kv.Key.StartsWith(
                        GiaPhaRender.PhaDoZoneSvgFolderLoader.FamilyPrefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string candidate = kv.Value?.GetSvgMarkup();
                if (!string.IsNullOrWhiteSpace(candidate) && candidate == markup)
                {
                    return kv.Key;
                }
            }

            return style.ShapeSvgId;
        }

        /// <summary>Gán family_*.svg làm khung/nền cả ô — không gán vào vùng strip Right.</summary>
        private void ApplyZoneSvgAsFamilyBoxFrame(PhaDoBoxStyle style, string svgId)
        {
            if (style == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(svgId))
            {
                style.ShapeSvgId = null;
                style.CustomShapeSvg = null;
                return;
            }

            if (TryGetZoneSvgShape(svgId, out var shape) && shape != null)
            {
                style.CustomShapeSvg = shape.GetSvgMarkup();
                style.CustomShapeViewBoxWidth = shape.ViewBoxWidth > 0 ? shape.ViewBoxWidth : 100;
                style.CustomShapeViewBoxHeight = shape.ViewBoxHeight > 0 ? shape.ViewBoxHeight : 80;
                style.ShapeSvgId = svgId;
                return;
            }

            var catalog = viewModel?.FamilyTree?.GP?.SvgShapesById;
            if (catalog != null)
            {
                style.ShapeSvgId = svgId;
                PhaDoSvgCatalog.ResolveShapeIntoStyle(style, catalog);
            }
        }

        /// <summary>Áp dụng toolbar SVG khi chọn ô gia đình — chỉ family_* (khung ô).</summary>
        private void ApplyFamilyBoxToolbarSvg(int familyId)
        {
            HideZoneSvgToolbarPreview();

            string familyFrameId = (phaDoCtxFamilyBoxFrameCombo?.SelectedItem as PhaDoZoneComboItem)?.SvgId;

            var style = GetBoxStyleForFamily(familyId);
            ApplyZoneSvgAsFamilyBoxFrame(style, familyFrameId);
            // family_* là khung ô — không gán strip vùng phải (dữ liệu cũ có thể còn RightZoneSvgId)
            style.RightZoneSvgId = null;
            _phaDoBoxStyleByFamilyId[familyId] = style;

            SetFamilyPhaDoShapeSvgId(
                familyId,
                string.IsNullOrWhiteSpace(familyFrameId) ? null : familyFrameId);

            ApplyBoxStyleToFamilyVisuals(familyId);
            SaveWorkspaceSession();
        }

        /// <summary>Quét thư mục ZoneSvg — title_* → Left, family_* → Right.</summary>
        private void ReloadPhaDoZoneSvgFromFolder()
        {
            string folder = GiaPhaRender.PhaDoZoneSvgFolderLoader.ResolveFolderPath();
            var loaded = GiaPhaRender.PhaDoZoneSvgFolderLoader.Load(folder);

            _phaDoZoneSvgFromFolder = loaded.ShapesById ?? new Dictionary<string, GiaPhaRender.PhaDoSvgShape>(
                StringComparer.OrdinalIgnoreCase);

            _phaDoTitleZoneComboItems = BuildZoneComboItems(loaded.TitleEntries);
            _phaDoFamilyZoneComboItems = BuildZoneComboItems(loaded.FamilyEntries);

            log.Info(string.Format(
                "Zone SVG: thư mục={0}, tồn tại={1}, title={2}, family={3}, bỏ qua={4}",
                folder,
                Directory.Exists(folder),
                loaded.TitleEntries.Count,
                loaded.FamilyEntries.Count,
                loaded.SkippedFiles.Count));
            if (loaded.SkippedFiles.Count > 0)
            {
                foreach (string skip in loaded.SkippedFiles)
                {
                    log.Warn("Zone SVG bỏ qua: " + skip);
                }
            }
        }

        /// <summary>Gắn danh sách zone lên combobox (gọi lúc khởi động và khi chọn ô).</summary>
        private void BindPhaDoZoneComboItems()
        {
            if (phaDoCtxTitleZoneCombo != null)
            {
                phaDoCtxTitleZoneCombo.ItemsSource = null;
                phaDoCtxTitleZoneCombo.ItemsSource = _phaDoTitleZoneComboItems;
            }

            if (phaDoCtxFamilyBoxFrameCombo != null)
            {
                phaDoCtxFamilyBoxFrameCombo.ItemsSource = null;
                phaDoCtxFamilyBoxFrameCombo.ItemsSource = _phaDoFamilyZoneComboItems;
            }
        }

        private static List<PhaDoZoneComboItem> BuildZoneComboItems(
            List<GiaPhaRender.PhaDoZoneSvgFileEntry> entries)
        {
            var items = new List<PhaDoZoneComboItem>
            {
                new PhaDoZoneComboItem(null, "Không")
            };
            if (entries == null)
            {
                return items;
            }

            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry?.Id))
                {
                    continue;
                }

                items.Add(new PhaDoZoneComboItem(entry.Id, entry.Display ?? entry.Id));
            }

            return items;
        }

        /// <summary>Nạp ComboBox zone: Left = title_*.svg, Right = family_*.svg từ thư mục ZoneSvg.</summary>
        private void PopulateZoneCombos()
        {
            ReloadPhaDoZoneSvgFromFolder();
            BindPhaDoZoneComboItems();
        }

        private bool TryGetZoneSvgShape(string svgId, out GiaPhaRender.PhaDoSvgShape shape)
        {
            shape = null;
            if (string.IsNullOrWhiteSpace(svgId))
            {
                return false;
            }

            if (_phaDoZoneSvgFromFolder != null
                && _phaDoZoneSvgFromFolder.TryGetValue(svgId, out shape)
                && shape != null)
            {
                return true;
            }

            var catalog = viewModel?.FamilyTree?.GP?.SvgShapesById;
            if (catalog != null && catalog.TryGetValue(svgId, out shape) && shape != null)
            {
                return true;
            }

            return false;
        }

        private void SelectZoneComboItem(System.Windows.Controls.ComboBox combo, string svgId)
        {
            if (combo?.ItemsSource == null)
            {
                return;
            }

            _phaDoToolbarZoneComboSyncing = true;
            try
            {
                foreach (PhaDoZoneComboItem item in combo.Items)
                {
                    if (item.SvgId == svgId)
                    {
                        combo.SelectedItem = item;
                        return;
                    }
                }

                if (combo.Items.Count > 0)
                {
                    combo.SelectedIndex = 0;
                }
            }
            finally
            {
                _phaDoToolbarZoneComboSyncing = false;
            }
        }

        /// <summary>Đổi title_* trên combo → xem trước trên khối title (Áp dụng mới ghi).</summary>
        private void PhaDoCtxTitleZoneCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_phaDoToolbarZoneComboSyncing)
            {
                return;
            }

            if (_phaDoCtxSelType != PhaDoCtxSelType.TitleBlock)
            {
                return;
            }

            if (e.AddedItems == null || e.AddedItems.Count == 0)
            {
                return;
            }

            string svgId = (phaDoCtxTitleZoneCombo?.SelectedItem as PhaDoZoneComboItem)?.SvgId;
            ShowZoneSvgToolbarPreview(svgId);
        }

        /// <summary>Đổi family_* trên combo → xem trước trên ô đang chọn.</summary>
        private void PhaDoCtxFamilyBoxFrameCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_phaDoToolbarZoneComboSyncing)
            {
                return;
            }

            if (_phaDoCtxSelType != PhaDoCtxSelType.Box || _phaDoSelectedFamilyId <= 0)
            {
                return;
            }

            if (e.AddedItems == null || e.AddedItems.Count == 0)
            {
                return;
            }

            string svgId = (phaDoCtxFamilyBoxFrameCombo?.SelectedItem as PhaDoZoneComboItem)?.SvgId;
            ShowZoneSvgToolbarPreview(svgId);
        }

        /// <summary>Xóa preview toolbar và khôi phục visual đã lưu.</summary>
        private void HideZoneSvgToolbarPreview()
        {
            if (theCanvas == null)
            {
                _phaDoZonePreviewFamilyId = 0;
                return;
            }

            var previewEls = theCanvas.Children
                .OfType<FrameworkElement>()
                .Where(fe => fe.Tag is GiaPhaRender.PhaDoZoneSvgPreviewTag)
                .Cast<UIElement>()
                .ToList();
            foreach (var el in previewEls)
            {
                theCanvas.Children.Remove(el);
            }

            if (_phaDoZonePreviewFamilyId > 0)
            {
                int fid = _phaDoZonePreviewFamilyId;
                _phaDoZonePreviewFamilyId = 0;
                RemoveFamilyBoxSvgPreviewLayer(fid);
                if (_phaDoSelectedFamilyId == fid)
                {
                    DrawSelectionOverlay(fid);
                }
            }
        }

        /// <summary>Hiển thị khung SVG tạm trên title box hoặc ô gia đình đang chọn.</summary>
        private void ShowZoneSvgToolbarPreview(string svgId)
        {
            if (theCanvas == null || _phaDoRenderedLayout == null)
            {
                return;
            }

            HideZoneSvgToolbarPreview();

            if (_phaDoCtxSelType == PhaDoCtxSelType.TitleBlock)
            {
                ShowTitleBlockZoneSvgPreview(svgId);
                return;
            }

            if (_phaDoCtxSelType == PhaDoCtxSelType.Box && _phaDoSelectedFamilyId > 0)
            {
                ShowFamilyBoxZoneSvgPreview(_phaDoSelectedFamilyId, svgId);
            }
        }

        private void ShowTitleBlockZoneSvgPreview(string svgId)
        {
            if (_phaDoCurrentOptions == null)
            {
                return;
            }

            if (!TryGetTitleBlockBoundsPx(out double left, out double top, out double w, out double h))
            {
                return;
            }

            FrameworkElement previewEl;
            if (string.IsNullOrWhiteSpace(svgId))
            {
                previewEl = new System.Windows.Shapes.Rectangle
                {
                    Width = w,
                    Height = h,
                    Fill = Brushes.Transparent,
                    Stroke = new SolidColorBrush(Color.FromArgb(160, 30, 136, 229)),
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 3 },
                    IsHitTestVisible = false
                };
            }
            else
            {
                if (!TryGetZoneSvgShape(svgId, out var shape) || shape == null)
                {
                    return;
                }

                string markup = shape.GetSvgMarkup();
                if (string.IsNullOrWhiteSpace(markup))
                {
                    return;
                }

                double vbW = shape.ViewBoxWidth > 0 ? shape.ViewBoxWidth : 100;
                double vbH = shape.ViewBoxHeight > 0 ? shape.ViewBoxHeight : 80;
                previewEl = PhaDoBoxSvgWpfRenderer.CreateBackgroundElement(
                    markup,
                    vbW,
                    vbH,
                    w,
                    h,
                    null,
                    _phaDoCurrentOptions.TitleFillColorHex);
                if (previewEl == null)
                {
                    return;
                }

                previewEl.Opacity = 0.92;
                previewEl.IsHitTestVisible = false;
            }

            previewEl.Tag = new GiaPhaRender.PhaDoZoneSvgPreviewTag { IsTitleBlock = true };
            Canvas.SetLeft(previewEl, left);
            Canvas.SetTop(previewEl, top);
            Panel.SetZIndex(previewEl, 6);
            theCanvas.Children.Add(previewEl);

            if (_phaDoTitleSelected && _phaDoTitleSelectedLine < 0)
            {
                DrawTitleSelectionOverlay();
            }
        }

        private void ShowFamilyBoxZoneSvgPreview(int familyId, string svgId)
        {
            var node = FindNodeByFamilyId(familyId);
            if (node?.Family == null)
            {
                return;
            }

            _phaDoZonePreviewFamilyId = familyId;
            RemoveFamilyBoxSvgPreviewLayer(familyId);

            double x = MmToPx(node.Xmm);
            double y = MmToPx(node.Ymm);
            double w = MmToPx(node.Metrics.WidthMm);
            double h = MmToPx(node.Metrics.HeightMm);

            FrameworkElement previewEl;
            if (string.IsNullOrWhiteSpace(svgId))
            {
                previewEl = new System.Windows.Shapes.Rectangle
                {
                    Width = w,
                    Height = h,
                    Fill = Brushes.Transparent,
                    Stroke = new SolidColorBrush(Color.FromArgb(160, 30, 136, 229)),
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 3 },
                    IsHitTestVisible = false
                };
            }
            else
            {
                var previewStyle = GetBoxStyleForFamily(familyId).Clone();
                ApplyZoneSvgAsFamilyBoxFrame(previewStyle, svgId);
                if (!previewStyle.HasCustomShape)
                {
                    return;
                }

                string fillHex = string.IsNullOrWhiteSpace(previewStyle.FillColorHex)
                    ? null
                    : previewStyle.FillColorHex;
                previewEl = PhaDoBoxSvgWpfRenderer.CreateBackgroundElement(
                    previewStyle.CustomShapeSvg,
                    previewStyle.CustomShapeViewBoxWidth,
                    previewStyle.CustomShapeViewBoxHeight,
                    w,
                    h,
                    node.Family,
                    fillHex);
                if (previewEl == null)
                {
                    return;
                }

                previewEl.Opacity = 0.92;
                previewEl.IsHitTestVisible = false;
            }

            previewEl.Tag = new GiaPhaRender.PhaDoZoneSvgPreviewTag { FamilyId = familyId };
            Canvas.SetLeft(previewEl, x);
            Canvas.SetTop(previewEl, y);
            Panel.SetZIndex(previewEl, 12);
            theCanvas.Children.Add(previewEl);

            DrawSelectionOverlay(familyId);
        }

        /// <summary>Xóa lớp preview family_* — không đụng nền ô đang lưu (tag PhaDoBoxBackgroundTag).</summary>
        private void RemoveFamilyBoxSvgPreviewLayer(int familyId)
        {
            if (theCanvas == null || familyId <= 0)
            {
                return;
            }

            var toRemove = theCanvas.Children
                .OfType<FrameworkElement>()
                .Where(fe => fe.Tag is GiaPhaRender.PhaDoZoneSvgPreviewTag p
                    && p.FamilyId == familyId
                    && !p.IsTitleBlock)
                .Cast<UIElement>()
                .ToList();
            foreach (var el in toRemove)
            {
                theCanvas.Children.Remove(el);
            }
        }

        private void PhaDoCtxColorBox_TextChanged(object sender,
            System.Windows.Controls.TextChangedEventArgs e)
        {
            if (phaDoCtxColorPreview == null || phaDoCtxColorBox == null) return;
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(phaDoCtxColorBox.Text.Trim());
                phaDoCtxColorPreview.Background = new SolidColorBrush(c);
            }
            catch { phaDoCtxColorPreview.Background = Brushes.Transparent; }
        }

        private sealed class PhaDoColorComboItem
        {
            public string Hex { get; }
            public string Display { get; }
            public PhaDoColorComboItem(string hex, string display) { Hex = hex; Display = display; }
            public override string ToString() => Display;
        }

        private void InitPhaDoContextFontControls()
        {
            // Danh sách font (đổ 1 lần sau InitializeComponent)
            if (phaDoCtxFontFamilyCombo != null)
            {
                var fonts = Fonts.SystemFontFamilies
                    .Select(f => f.Source)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                fonts.Insert(0, "(mặc định)");
                phaDoCtxFontFamilyCombo.ItemsSource = fonts;
                if (phaDoCtxFontFamilyCombo.SelectedIndex < 0)
                {
                    phaDoCtxFontFamilyCombo.SelectedIndex = 0;
                }
            }

            // Danh sách màu thường dùng (hex)
            if (phaDoCtxColorBox != null)
            {
                var items = new System.Collections.Generic.List<PhaDoColorComboItem>
                {
                    new PhaDoColorComboItem("#000000", "Đen (#000000)"),
                    new PhaDoColorComboItem("#333333", "Xám đậm (#333333)"),
                    new PhaDoColorComboItem("#595959", "Xám (#595959)"),
                    new PhaDoColorComboItem("#888888", "Xám nhạt (#888888)"),
                    new PhaDoColorComboItem("#19376A", "Xanh đậm (#19376A)"),
                    new PhaDoColorComboItem("#1E78DC", "Xanh dương (#1E78DC)"),
                    new PhaDoColorComboItem("#E53935", "Đỏ (#E53935)"),
                    new PhaDoColorComboItem("#FB8C00", "Cam (#FB8C00)"),
                    new PhaDoColorComboItem("#43A047", "Xanh lá (#43A047)")
                };
                phaDoCtxColorBox.DisplayMemberPath = nameof(PhaDoColorComboItem.Display);
                phaDoCtxColorBox.SelectedValuePath = nameof(PhaDoColorComboItem.Hex);
                phaDoCtxColorBox.ItemsSource = items;
            }
        }

        private static bool TryParsePt(string raw, out double pt)
        {
            return double.TryParse(raw,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out pt);
        }

        private void NudgeCtxFontSize(double delta)
        {
            if (phaDoCtxFontSizeBox == null) return;
            if (!TryParsePt(phaDoCtxFontSizeBox.Text, out double pt)) pt = 0;
            pt = Math.Max(1, Math.Min(200, pt + delta));
            phaDoCtxFontSizeBox.Text = pt.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }

        private void PhaDoCtxFontSizeBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Lăn chuột trên ô cỡ chữ: tăng/giảm nhanh, không cần focus chỗ khác
            NudgeCtxFontSize(e.Delta > 0 ? 0.5 : -0.5);
            e.Handled = true;
        }

        private void PhaDoCtxFontSizeUp_Click(object sender, RoutedEventArgs e) => NudgeCtxFontSize(0.5);
        private void PhaDoCtxFontSizeDown_Click(object sender, RoutedEventArgs e) => NudgeCtxFontSize(-0.5);

        private void PhaDoCtxFontFamilyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Không apply ngay — chỉ thay đổi giá trị “đang chọn” trên toolbar
        }

        private void PhaDoCtxColorBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Chọn từ list → tự fill mã hex vào Text
            if (phaDoCtxColorBox?.SelectedValue is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                phaDoCtxColorBox.Text = hex;
            }
            PhaDoCtxColorBox_RefreshPreview();
        }

        private void PhaDoCtxColorBox_KeyUp(object sender, KeyEventArgs e)
        {
            PhaDoCtxColorBox_RefreshPreview();
        }

        private void PhaDoCtxColorBox_RefreshPreview()
        {
            if (phaDoCtxColorPreview == null || phaDoCtxColorBox == null) return;
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(phaDoCtxColorBox.Text.Trim());
                phaDoCtxColorPreview.Background = new SolidColorBrush(c);
            }
            catch { phaDoCtxColorPreview.Background = Brushes.Transparent; }
        }

        /// <summary>Đọc giá trị font từ toolbar vào GenLabelStyle.</summary>
        private GiaPhaRender.PhaDoGenLabelStyle ReadGenLabelStyleFromToolbar()
        {
            double.TryParse(phaDoCtxFontSizeBox?.Text,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double pt);

            string hex = phaDoCtxColorBox?.Text?.Trim();
            try { ColorConverter.ConvertFromString(hex); } catch { hex = null; }

            return new GiaPhaRender.PhaDoGenLabelStyle
            {
                FontPt   = pt > 0 ? pt : 0,
                ForegroundHex = hex,
                FontFamily = (phaDoCtxFontFamilyCombo?.SelectedItem as string) == "(mặc định)"
                    ? null
                    : (phaDoCtxFontFamilyCombo?.SelectedItem as string),
                Bold     = phaDoCtxBoldBtn?.IsChecked == true
            };
        }

        /// <summary>Áp dụng style toolbar cho object đang chọn.</summary>
        private void PhaDoCtxApply_Click(object sender, RoutedEventArgs e)
        {
            HideZoneSvgToolbarPreview();

            switch (_phaDoCtxSelType)
            {
                case PhaDoCtxSelType.GenLabel:
                    if (_phaDoCurrentOptions == null) return;
                    _phaDoCurrentOptions.GenLabelStyle = ReadGenLabelStyleFromToolbar();
                    RedrawGenerationBandsOnly();
                    break;
                case PhaDoCtxSelType.TitleBlock:
                    ApplyTitleBlockToolbarSvg();
                    break;
                case PhaDoCtxSelType.TitleText when _phaDoTitleSelectedLine >= 0:
                    ApplyToolbarToTitleLine(_phaDoTitleSelectedLine);
                    break;
                case PhaDoCtxSelType.PersonText
                    when _phaDoSelectedFamilyId > 0 && _phaDoSelectedPersonSlot.HasValue:
                    ApplyToolbarToFamilyPersonText(_phaDoSelectedFamilyId, _phaDoSelectedPersonSlot.Value);
                    break;
                case PhaDoCtxSelType.Box when _phaDoSelectedFamilyId > 0:
                    ApplyFamilyBoxToolbarSvg(_phaDoSelectedFamilyId);
                    break;
            }
        }

        /// <summary>Áp dụng cho tất cả object cùng đời (generation level).</summary>
        private void PhaDoCtxApplyAllGen_Click(object sender, RoutedEventArgs e)
        {
            switch (_phaDoCtxSelType)
            {
                // Nhãn Đời + Title text: "cùng đời" = tất cả (chỉ 1 title block / 1 bộ nhãn)
                case PhaDoCtxSelType.GenLabel:
                case PhaDoCtxSelType.TitleBlock:
                case PhaDoCtxSelType.TitleText:
                case PhaDoCtxSelType.PersonText:
                    PhaDoCtxApplyAllPha_Click(sender, e);
                    return;
                case PhaDoCtxSelType.Box when _phaDoSelectedFamilyId > 0 &&
                                              _phaDoRenderedLayout?.Nodes != null:
                    var node = FindNodeByFamilyId(_phaDoSelectedFamilyId);
                    if (node == null) return;
                    int level = node.Level;
                    var sameGen = _phaDoRenderedLayout.Nodes
                        .Where(n => n.Level == level && (n.Family?.familyInfo?.FamilyId ?? 0) > 0)
                        .Select(n => n.Family.familyInfo.FamilyId).Distinct().ToList();
                    foreach (int fid in sameGen)
                    {
                        ApplyFamilyBoxToolbarSvg(fid);
                    }
                    break;
            }
        }

        /// <summary>Áp dụng cho toàn bộ object cùng loại trong phả.</summary>
        private void PhaDoCtxApplyAllPha_Click(object sender, RoutedEventArgs e)
        {
            switch (_phaDoCtxSelType)
            {
                case PhaDoCtxSelType.GenLabel:
                    if (_phaDoCurrentOptions == null) return;
                    _phaDoCurrentOptions.GenLabelStyle = ReadGenLabelStyleFromToolbar();
                    RedrawGenerationBandsOnly();
                    break;
                case PhaDoCtxSelType.TitleBlock:
                    ApplyTitleBlockToolbarSvg();
                    break;
                case PhaDoCtxSelType.TitleText:
                    // "Tất cả phả" với title text = áp dụng cho dòng đang chọn (chỉ 1 title block)
                    if (_phaDoTitleSelectedLine >= 0)
                        ApplyToolbarToTitleLine(_phaDoTitleSelectedLine);
                    break;
                case PhaDoCtxSelType.PersonText
                    when _phaDoSelectedFamilyId > 0 && _phaDoSelectedPersonSlot.HasValue:
                    ApplyToolbarToFamilyPersonText(_phaDoSelectedFamilyId, _phaDoSelectedPersonSlot.Value);
                    break;
                case PhaDoCtxSelType.Box when _phaDoRenderedLayout?.Nodes != null:
                    var allIds = _phaDoRenderedLayout.Nodes
                        .Select(n => n.Family?.familyInfo?.FamilyId ?? 0)
                        .Where(id => id > 0).Distinct().ToList();
                    foreach (int fid in allIds)
                    {
                        ApplyFamilyBoxToolbarSvg(fid);
                    }
                    break;
            }
        }

        /// <summary>Xóa visual zone SVG quanh box của 1 gia đình.</summary>
        private void RemoveBoxZonesFromCanvas(int familyId)
        {
            var toRemove = theCanvas.Children
                .OfType<FrameworkElement>()
                .Where(fe => fe.Tag is GiaPhaRender.PhaDoBoxZoneTag zt && zt.FamilyId == familyId)
                .Cast<UIElement>().ToList();
            foreach (var el in toRemove) theCanvas.Children.Remove(el);
        }

        /// <summary>Vẽ 4 vùng SVG trang trí quanh ô gia đình.</summary>
        private void DrawBoxZonesOnCanvas(int familyId, GiaPhaRender.PhaDoBoxStyle boxStyle)
        {
            RemoveBoxZonesFromCanvas(familyId);
            if (boxStyle == null) return;
            var node = FindNodeByFamilyId(familyId);
            if (node == null) return;

            double dpi  = _phaDoCurrentOptions?.PrintDpi ?? 96;
            double mmPx = dpi / 25.4;
            double x    = node.Xmm * mmPx;
            double y    = node.Ymm * mmPx;
            double w    = node.Metrics.WidthMm  * mmPx;
            double h    = node.Metrics.HeightMm * mmPx;
            double sz   = Math.Max(1, boxStyle.ZoneSizeMm) * mmPx;

            DrawOneBoxZone(familyId, GiaPhaRender.PhaDoBoxZone.Top,    boxStyle.TopZoneSvgId,    x, y, w, h, sz, dpi);
            DrawOneBoxZone(familyId, GiaPhaRender.PhaDoBoxZone.Bottom, boxStyle.BottomZoneSvgId, x, y, w, h, sz, dpi);
            DrawOneBoxZone(familyId, GiaPhaRender.PhaDoBoxZone.Left,   boxStyle.LeftZoneSvgId,   x, y, w, h, sz, dpi);
            DrawOneBoxZone(familyId, GiaPhaRender.PhaDoBoxZone.Right,  boxStyle.RightZoneSvgId,  x, y, w, h, sz, dpi);
        }

        private void DrawOneBoxZone(int familyId, GiaPhaRender.PhaDoBoxZone zone,
            string svgId,
            double xPx, double yPx, double wPx, double hPx, double szPx, double dpi)
        {
            if (string.IsNullOrWhiteSpace(svgId)) return;
            if (!TryGetZoneSvgShape(svgId, out var shape) || shape == null) return;

            double elW, elH, elLeft, elTop;
            switch (zone)
            {
                case GiaPhaRender.PhaDoBoxZone.Top:
                    elW = wPx; elH = szPx; elLeft = xPx; elTop = yPx - szPx; break;
                case GiaPhaRender.PhaDoBoxZone.Bottom:
                    elW = wPx; elH = szPx; elLeft = xPx; elTop = yPx + hPx;  break;
                case GiaPhaRender.PhaDoBoxZone.Left:
                    elW = szPx; elH = hPx; elLeft = xPx - szPx; elTop = yPx; break;
                default: // Right
                    elW = szPx; elH = hPx; elLeft = xPx + wPx;  elTop = yPx; break;
            }

            string markup = shape.GetSvgMarkup();
            if (string.IsNullOrWhiteSpace(markup)) return;
            double vbW = shape.ViewBoxWidth  > 0 ? shape.ViewBoxWidth  : 100;
            double vbH = shape.ViewBoxHeight > 0 ? shape.ViewBoxHeight : 100;
            var el = GiaPhaRender.PhaDoBoxSvgWpfRenderer.CreateBackgroundElement(
                markup, vbW, vbH, elW, elH, null, null);
            if (el == null) return;

            el.Tag = new GiaPhaRender.PhaDoBoxZoneTag(familyId, zone);
            Canvas.SetLeft(el, elLeft); Canvas.SetTop(el, elTop);
            Panel.SetZIndex(el, 2);
            theCanvas.Children.Add(el);
        }

        private void RemoveFamilyBackgroundVisual(int familyId)
        {
            var toRemove = theCanvas.Children
                .OfType<FrameworkElement>()
                .Where(fe =>
                {
                    if (fe.Tag is PhaDoBoxBackgroundTag t
                        && (t.Family?.familyInfo?.FamilyId ?? 0) == familyId)
                    {
                        return true;
                    }

                    // Nền đang preview toolbar — tag đã đổi, vẫn phải xóa theo familyId
                    if (fe.Tag is GiaPhaRender.PhaDoZoneSvgPreviewTag p
                        && p.FamilyId == familyId
                        && !p.IsTitleBlock)
                    {
                        return true;
                    }

                    return false;
                })
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
            var bg = FindFamilyBackgroundElement(familyId);
            if (bg != null)
            {
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

                if (TryGetFrameworkElementSizePx(bg, out width, out height))
                {
                    return true;
                }
            }

            // Fallback layout khi chưa có / mất visual nền
            var node = FindNodeByFamilyId(familyId);
            if (node?.Metrics != null)
            {
                left = MmToPx(node.Xmm);
                top = MmToPx(node.Ymm);
                width = MmToPx(node.Metrics.WidthMm);
                height = MmToPx(node.Metrics.HeightMm);
                return width > 0 && height > 0;
            }

            return false;
        }

        /// <summary>Nền ô thật (không tính lớp preview toolbar).</summary>
        private FrameworkElement FindFamilyBackgroundElement(int familyId)
        {
            if (theCanvas == null || familyId <= 0)
            {
                return null;
            }

            return theCanvas.Children
                .OfType<FrameworkElement>()
                .FirstOrDefault(fe => fe.Tag is PhaDoBoxBackgroundTag t
                    && (t.Family?.familyInfo?.FamilyId ?? 0) == familyId);
        }

        /// <summary>Áp dụng kiểu đã lưu lên visual một ô (nền SVG/rect + chữ chính/phụ theo tag).</summary>
        private void ApplyBoxStyleToFamilyVisuals(int familyId)
        {
            var boxStyle = GetBoxStyleForFamily(familyId);
            EnsureFamilyBoxFrameMarkupResolved(boxStyle);

            ReplaceFamilyBackgroundVisual(familyId, boxStyle);
            // Vẽ lại 4 vùng zone SVG trang trí quanh ô
            DrawBoxZonesOnCanvas(familyId, boxStyle);

            double dpi = _phaDoCurrentOptions?.PrintDpi ?? 96;
            string defaultFont = _phaDoCurrentOptions?.FontFamilyName ?? "Segoe UI";

            foreach (var child in theCanvas.Children)
            {
                if (child is TextBlock tb && tb.Tag is PhaDoBoxVisualTag visualTag
                    && (visualTag.Family?.familyInfo?.FamilyId ?? 0) == familyId
                    && (visualTag.ElementKind == PhaDoBoxElementKind.Person
                        || visualTag.ElementKind == PhaDoBoxElementKind.ExtraNote
                        || visualTag.ElementKind == PhaDoBoxElementKind.GenerationLabel))
                {
                    var personStyle = ResolvePersonTextStyle(familyId, visualTag, out bool boldDefault);
                    double defaultPt = GetDefaultFontPtForVisualTag(visualTag);
                    ApplyPersonTextStyle(tb, personStyle, defaultPt, defaultFont, dpi, boldDefault);
                }
                else if (child is StackPanel column && column.Tag is PhaDoBoxVisualTag columnTag
                    && (columnTag.Family?.familyInfo?.FamilyId ?? 0) == familyId
                    && columnTag.ElementKind == PhaDoBoxElementKind.Person)
                {
                    var personStyle = ResolvePersonTextStyle(familyId, columnTag, out bool boldDefault);
                    double defaultPt = GetDefaultFontPtForVisualTag(columnTag);
                    foreach (var line in column.Children.OfType<TextBlock>())
                    {
                        ApplyPersonTextStyle(line, personStyle, defaultPt, defaultFont, dpi, boldDefault);
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
            bool bold = personStyle?.Bold ?? boldByDefault;
            tb.FontWeight = bold ? FontWeights.Bold : FontWeights.Normal;

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
            if (_phaDoRenderedLayout?.Nodes == null)
            {
                return;
            }

            // Áp style cho toàn bộ box đang render để tránh hiện tượng màu "nhảy cóc"
            // khi family chưa có custom style trong _phaDoBoxStyleByFamilyId.
            foreach (var node in _phaDoRenderedLayout.Nodes)
            {
                int familyId = node?.Family?.familyInfo?.FamilyId ?? 0;
                if (familyId <= 0)
                {
                    continue;
                }
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

                if (from.Main.Bold.HasValue)
                {
                    target.Main.Bold = from.Main.Bold;
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

                if (from.Spouse.Bold.HasValue)
                {
                    target.Spouse.Bold = from.Spouse.Bold;
                }
            }

            if (from.PersonTextStylesBySlot != null)
            {
                if (target.PersonTextStylesBySlot == null)
                {
                    target.PersonTextStylesBySlot = new Dictionary<int, PhaDoPersonTextStyle>();
                }

                foreach (var kv in from.PersonTextStylesBySlot)
                {
                    if (kv.Value == null || kv.Value.IsEmpty())
                    {
                        continue;
                    }

                    if (!target.PersonTextStylesBySlot.TryGetValue(kv.Key, out var mergedSlot))
                    {
                        target.PersonTextStylesBySlot[kv.Key] = kv.Value.Clone();
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(kv.Value.FontFamilyName))
                    {
                        mergedSlot.FontFamilyName = kv.Value.FontFamilyName;
                    }

                    if (kv.Value.FontPt.HasValue)
                    {
                        mergedSlot.FontPt = kv.Value.FontPt;
                    }

                    if (!string.IsNullOrWhiteSpace(kv.Value.ForegroundHex))
                    {
                        mergedSlot.ForegroundHex = kv.Value.ForegroundHex;
                    }

                    if (kv.Value.Bold.HasValue)
                    {
                        mergedSlot.Bold = kv.Value.Bold;
                    }
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

        /// <summary>DPI thực khi vẽ phả đồ — khớp GiaPhaTitleBlockRenderer / layout.</summary>
        private double GetPhaDoRenderDpi()
        {
            if (_phaDoRenderedLayout != null && _phaDoRenderedLayout.Dpi > 1)
            {
                return _phaDoRenderedLayout.Dpi;
            }

            return _phaDoCurrentOptions?.PrintDpi ?? 96;
        }

        private double MmToPxRender(double mm)
        {
            return PrintUnits.MmToPixels(mm, GetPhaDoRenderDpi());
        }

        private double PxToMmRender(double px)
        {
            return PrintUnits.PixelsToMm(px, GetPhaDoRenderDpi());
        }

        private Point GetPhaDoCanvasDeltaMmRender(Point currentCanvasPoint, Point startCanvasPoint)
        {
            double dxPx = currentCanvasPoint.X - startCanvasPoint.X;
            double dyPx = currentCanvasPoint.Y - startCanvasPoint.Y;
            return new Point(PxToMmRender(dxPx), PxToMmRender(dyPx));
        }

        private static bool TryGetFrameworkElementSizePx(FrameworkElement element, out double widthPx, out double heightPx)
        {
            widthPx = 0;
            heightPx = 0;
            if (element == null)
            {
                return false;
            }

            widthPx = element.Width;
            heightPx = element.Height;
            if (widthPx > 0 && heightPx > 0)
            {
                return true;
            }

            element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            widthPx = element.DesiredSize.Width;
            heightPx = element.DesiredSize.Height;
            return widthPx > 0 && heightPx > 0;
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
            SyncFamilyPersonTextWidthsToBox(familyId);
            RefreshFamilySelectionVisuals(familyId);

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

        /// <summary>Cây lớn: chỉ mở gốc — tránh TreeView materialize hết nhánh (STA deadlock / OOM).</summary>
        private const int GiaPhaAutoExpandAllMaxFamilies = 600;

        public void ScheduleExpandGiaPhaTreeView()
        {
            ExpandGiaPhaTreeViewForCurrentTree();
            Dispatcher.BeginInvoke(
                new Action(ExpandGiaPhaTreeViewForCurrentTree),
                DispatcherPriority.Loaded);
        }

        private void ExpandGiaPhaTreeViewForCurrentTree()
        {
            var family = viewModel?.FamilyTree?.Family;
            if (family?.RootPerson == null)
            {
                return;
            }

            int familyCount = CountFamiliesInTree(family.RootPerson);
            if (familyCount > GiaPhaAutoExpandAllMaxFamilies)
            {
                family.ExpandRootOnly();
                viewModel?.AddUserAction(
                    "Cây lớn (" + familyCount + " gia đình): chỉ mở gốc — mở từng nhánh khi cần.");
                return;
            }

            family.ExpandAll();
        }

        /// <summary>Gắn callback cuộn cây — gọi lại mỗi khi mở/load file (FamilyTreeViewModel mới).</summary>
        public void BindFamilyTreeSearchScroll()
        {
            var familyTree = viewModel?.FamilyTree?.Family;
            if (familyTree == null)
            {
                return;
            }

            familyTree.RequestScrollToFamilyInTree = family =>
            {
                if (family == null)
                {
                    return;
                }

                _pendingTreeScrollTarget = family;
                ScrollTreeViewToFamily(family, 0);
            };
        }

        private void SelectFamilyInTreeView(FamilyViewModel family)
        {
            if (family == null || viewModel?.FamilyTree?.Family == null)
            {
                return;
            }

            family = ResolveTreeFamilyForScroll(family);
            if (family == null)
            {
                return;
            }

            viewModel.FamilyTree.Family.SelectFamily(family);
            family.IsSelected = true;

            _pendingTreeScrollTarget = family;
            ScrollTreeViewToFamily(family, 0);
        }

        /// <summary>Ánh xạ VM từ Phả đồ (có thể là clone) sang node đúng trên cây gốc file.</summary>
        private FamilyViewModel ResolveTreeFamilyForScroll(FamilyViewModel family)
        {
            if (family == null)
            {
                return null;
            }

            int familyId = family.familyInfo?.FamilyId ?? 0;
            if (familyId <= 0)
            {
                return family;
            }

            var root = viewModel?.FamilyTree?.Family?.RootPerson;
            FamilyViewModel onTree = FindFamilyById(root, familyId);
            return onTree ?? family;
        }

        /// <summary>Cuộn TreeView tới gia đình — mở nhánh, tìm container theo đường dẫn, scroll ScrollViewer.</summary>
        private void ScrollTreeViewToFamily(FamilyViewModel family, int attempt)
        {
            if (treeViewGiaPha == null || family == null)
            {
                ClearPendingTreeScrollTarget();
                return;
            }

            family = ResolveTreeFamilyForScroll(family);
            if (family == null)
            {
                ClearPendingTreeScrollTarget();
                return;
            }

            EnsureTreeAncestorsExpanded(family);
            treeViewGiaPha.UpdateLayout();

            TreeViewItem item = FindTreeViewItemOnPath(treeViewGiaPha, family);
            if (item != null)
            {
                ScrollTreeViewItemIntoView(item);
                ClearPendingTreeScrollTarget();
                return;
            }

            const int maxAttempts = 12;
            if (attempt < maxAttempts)
            {
                DispatcherPriority priority = attempt < 4
                    ? DispatcherPriority.Loaded
                    : DispatcherPriority.ApplicationIdle;
                Dispatcher.BeginInvoke(
                    new Action(() => ScrollTreeViewToFamily(family, attempt + 1)),
                    priority);
                return;
            }

            log.Warn("TreeView: không cuộn được tới \"" + (family.Name ?? "") + "\" sau " + maxAttempts + " lần thử.");
            ClearPendingTreeScrollTarget();
        }

        private void ClearPendingTreeScrollTarget()
        {
            if (_pendingTreeScrollTarget != null)
            {
                _pendingTreeScrollTarget = null;
            }
        }

        private static void EnsureTreeAncestorsExpanded(FamilyViewModel family)
        {
            for (FamilyViewModel parent = family?.Parent; parent != null; parent = parent.Parent)
            {
                parent.IsExpanded = true;
            }
        }

        /// <summary>Đi từ gốc theo chuỗi cha → con, dùng ContainerFromItem (ổn định hơn DFS + FromIndex).</summary>
        private static TreeViewItem FindTreeViewItemOnPath(ItemsControl root, FamilyViewModel target)
        {
            if (root == null || target == null)
            {
                return null;
            }

            var path = new List<FamilyViewModel>();
            for (FamilyViewModel node = target; node != null; node = node.Parent)
            {
                path.Insert(0, node);
            }

            ItemsControl parentControl = root;
            TreeViewItem found = null;
            foreach (FamilyViewModel node in path)
            {
                parentControl.UpdateLayout();
                if (parentControl.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
                {
                    parentControl.ApplyTemplate();
                    parentControl.UpdateLayout();
                }

                found = parentControl.ItemContainerGenerator.ContainerFromItem(node) as TreeViewItem;
                if (found == null)
                {
                    found = FindTreeViewItemByReference(parentControl, node);
                }

                if (found == null)
                {
                    return null;
                }

                if (!ReferenceEquals(node, target))
                {
                    node.IsExpanded = true;
                    found.IsExpanded = true;
                    found.UpdateLayout();
                }

                parentControl = found;
            }

            return found;
        }

        private static TreeViewItem FindTreeViewItemByReference(ItemsControl parent, FamilyViewModel node)
        {
            if (parent == null || node == null)
            {
                return null;
            }

            int targetId = node.familyInfo?.FamilyId ?? 0;
            for (int i = 0; i < parent.Items.Count; i++)
            {
                var candidate = parent.Items[i] as FamilyViewModel;
                if (candidate == null)
                {
                    continue;
                }

                bool sameRef = ReferenceEquals(candidate, node);
                bool sameId = targetId > 0 && (candidate.familyInfo?.FamilyId ?? 0) == targetId;
                if (!sameRef && !sameId)
                {
                    continue;
                }

                parent.UpdateLayout();
                var container = parent.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (container != null)
                {
                    return container;
                }

                if (parent is TreeViewItem parentItem)
                {
                    parentItem.ApplyTemplate();
                    parentItem.UpdateLayout();
                    container = parent.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                    if (container != null)
                    {
                        return container;
                    }
                }
            }

            return null;
        }

        private static TreeViewItem FindTreeViewItem(ItemsControl itemsControl, FamilyViewModel family)
        {
            return FindTreeViewItemOnPath(itemsControl, family);
        }

        private ScrollViewer GetTreeViewScrollViewer()
        {
            if (treeViewGiaPha == null)
            {
                return null;
            }

            if (_treeViewScrollViewer == null)
            {
                if (treeViewGiaPha.Template == null)
                {
                    treeViewGiaPha.ApplyTemplate();
                }

                treeViewGiaPha.UpdateLayout();
                _treeViewScrollViewer = FindTreeViewScrollViewerInChrome(treeViewGiaPha);
            }

            return _treeViewScrollViewer;
        }

        private ScrollContentPresenter GetTreeScrollContentPresenter()
        {
            if (treeViewGiaPha == null)
            {
                return null;
            }

            if (_treeScrollContentPresenter == null)
            {
                GetTreeViewScrollViewer();
                _treeScrollContentPresenter = FindTreeScrollContentPresenterInChrome(treeViewGiaPha);
            }

            return _treeScrollContentPresenter;
        }

        private void InvalidateTreeViewScrollCache()
        {
            _treeViewScrollViewer = null;
            _treeScrollContentPresenter = null;
        }

        /// <summary>ScrollViewer của TreeView (không đi vào TreeViewItem con).</summary>
        private static ScrollViewer FindTreeViewScrollViewerInChrome(DependencyObject root)
        {
            if (root == null)
            {
                return null;
            }

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is ScrollViewer scrollViewer)
                {
                    return scrollViewer;
                }

                if (child is TreeViewItem)
                {
                    continue;
                }

                ScrollViewer nested = FindTreeViewScrollViewerInChrome(child);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static ScrollContentPresenter FindTreeScrollContentPresenterInChrome(DependencyObject root)
        {
            if (root == null)
            {
                return null;
            }

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is ScrollContentPresenter presenter)
                {
                    return presenter;
                }

                if (child is TreeViewItem)
                {
                    continue;
                }

                ScrollContentPresenter nested = FindTreeScrollContentPresenterInChrome(child);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private void ScrollTreeViewItemIntoView(TreeViewItem item, bool focusItem = true)
        {
            if (item == null)
            {
                return;
            }

            try
            {
                _allowTreeViewBringIntoView = true;
                item.IsSelected = true;
                if (focusItem)
                {
                    item.Focus();
                }

                item.UpdateLayout();
                treeViewGiaPha?.UpdateLayout();

                if (!ApplyTreeViewScrollToItem(item))
                {
                    InvalidateTreeViewScrollCache();
                    if (!ApplyTreeViewScrollToItem(item))
                    {
                        item.BringIntoView();
                    }
                }
            }
            finally
            {
                _allowTreeViewBringIntoView = false;
            }
        }

        /// <summary>Cuộn khi dòng ngoài viewport hoặc sát đáy panel; lề dưới ~2 dòng cho dễ nhìn.</summary>
        private bool ApplyTreeViewScrollToItem(TreeViewItem item)
        {
            ScrollViewer scroll = GetTreeViewScrollViewer();
            if (item == null || scroll == null || scroll.ViewportHeight <= 0)
            {
                return false;
            }

            const double marginTop = 12;

            try
            {
                double itemHeight = item.ActualHeight > 0 ? item.ActualHeight : item.DesiredSize.Height;
                if (itemHeight <= 0)
                {
                    itemHeight = 24;
                }

                // MinHeight 22 + font 16 trên TreeView — thêm ~2 dòng trống phía dưới dòng chọn
                double treeLineHeight = Math.Max(itemHeight, 22);
                double marginBottom = marginTop + treeLineHeight * 2;

                ScrollContentPresenter content = GetTreeScrollContentPresenter();
                if (content != null)
                {
                    GeneralTransform toContent = item.TransformToAncestor(content);
                    if (toContent != null)
                    {
                        double itemTopContent = toContent.Transform(new Point(0, 0)).Y;
                        double itemBottomContent = itemTopContent + itemHeight;
                        double viewTop = scroll.VerticalOffset;
                        double viewBottom = viewTop + scroll.ViewportHeight;

                        if (itemTopContent >= viewTop + marginTop
                            && itemBottomContent <= viewBottom - marginBottom)
                        {
                            return true;
                        }

                        if (itemTopContent < viewTop + marginTop)
                        {
                            scroll.ScrollToVerticalOffset(Math.Max(0, itemTopContent - marginTop));
                        }
                        else
                        {
                            scroll.ScrollToVerticalOffset(
                                Math.Max(0, itemBottomContent - scroll.ViewportHeight + marginBottom));
                        }

                        return true;
                    }
                }

                GeneralTransform toScroll = item.TransformToAncestor(scroll);
                if (toScroll == null)
                {
                    return false;
                }

                double yInViewport = toScroll.Transform(new Point(0, 0)).Y;
                double viewport = scroll.ViewportHeight;
                if (yInViewport >= marginTop && yInViewport + itemHeight <= viewport - marginBottom)
                {
                    return true;
                }

                if (yInViewport < marginTop)
                {
                    scroll.ScrollToVerticalOffset(Math.Max(0, scroll.VerticalOffset + yInViewport - marginTop));
                }
                else
                {
                    scroll.ScrollToVerticalOffset(
                        scroll.VerticalOffset + yInViewport + itemHeight - viewport + marginBottom);
                }

                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
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

        /// <summary>Xóa highlight các node con/cháu của box đang chọn.</summary>
        private void ClearDirectChildHighlights()
        {
            var highlights = theCanvas.Children
                .OfType<FrameworkElement>()
                .Where(fe => Equals(fe.Tag, "__PhaDoDirectChildHighlight"))
                .Cast<UIElement>()
                .ToList();
            foreach (var h in highlights)
            {
                theCanvas.Children.Remove(h);
            }
        }

        /// <summary>Tô màu đệ quy 2 mức: con trực tiếp + cháu (không lan sâu hơn).</summary>
        private void DrawDirectChildHighlights(int selectedFamilyId)
        {
            ClearDirectChildHighlights();
            if (selectedFamilyId <= 0)
            {
                return;
            }

            var selectedNode = FindNodeByFamilyId(selectedFamilyId);
            var directChildren = selectedNode?.Family?.Children;
            if (directChildren == null || directChildren.Count == 0)
            {
                return;
            }

            var queue = new Queue<(FamilyViewModel Node, int Depth)>();
            foreach (var child in directChildren)
            {
                queue.Enqueue((child, 1));
            }

            while (queue.Count > 0)
            {
                var item = queue.Dequeue();
                var node = item.Node;
                int depth = item.Depth;
                if (node == null || depth > 2)
                {
                    continue;
                }

                int familyId = node.familyInfo?.FamilyId ?? 0;
                if (familyId <= 0)
                {
                    continue;
                }

                if (!TryGetFamilyBackgroundBounds(familyId, out double left, out double top, out double boxW, out double boxH))
                {
                    continue;
                }

                // Mức 1 đậm hơn mức 2 để phân biệt con/cháu trực quan.
                byte fillAlpha = depth == 1 ? (byte)125 : (byte)80;
                byte strokeAlpha = depth == 1 ? (byte)230 : (byte)200;
                var overlay = new Rectangle
                {
                    Width = Math.Max(2, boxW),
                    Height = Math.Max(2, boxH),
                    Fill = new SolidColorBrush(Color.FromArgb(fillAlpha, 36, 129, 255)),
                    Stroke = new SolidColorBrush(Color.FromArgb(strokeAlpha, 20, 87, 184)),
                    StrokeThickness = depth == 1 ? 1.6 : 1.1,
                    RadiusX = 4,
                    RadiusY = 4,
                    IsHitTestVisible = false,
                    Tag = "__PhaDoDirectChildHighlight"
                };
                Canvas.SetLeft(overlay, left);
                Canvas.SetTop(overlay, top);
                Panel.SetZIndex(overlay, 11);
                theCanvas.Children.Add(overlay);

                if (depth < 2 && node.Children != null)
                {
                    foreach (var child in node.Children)
                    {
                        queue.Enqueue((child, depth + 1));
                    }
                }
            }
        }

        private void RemoveFamilyVisualsFromCanvas(int familyId)
        {
            // Xóa visual ô gia đình — không xóa nhãn phả con (tag riêng, vẽ lại bởi DrawScopeStartMarkers).
            var toRemove = theCanvas.Children
                .OfType<FrameworkElement>()
                .Where(fe => GetFamilyIdFromElementTag(fe.Tag) == familyId
                    && !(fe.Tag is PhaDoScopeStartMarkerTag))
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

                if (_phaConFamilyIds.Contains(familyId) && node.Metrics != null)
                {
                    // Box family-phacon to hơn để user nhìn thấy đây là nhánh sẽ tách phả con.
                    node.Metrics.ApplySizeOverride(
                        node.Metrics.WidthMm * 1.14,
                        node.Metrics.HeightMm * 1.18);
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
            SyncFamilyPersonTextWidthsToBox(familyId);
            RefreshFamilySelectionVisuals(familyId);
            BringScopeStartMarkersToFront();
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

            bool useCurved = _phaDoCurrentOptions?.ConnectorPathType == GiaPhaConnectorPathType.Curved;
            foreach (var fe in theCanvas.Children.OfType<FrameworkElement>())
            {
                var tag = fe.Tag as GiaPhaCanvasConnectorTag;
                if (tag == null || tag.ParentFamilyId != parentFamilyId)
                {
                    continue;
                }

                if (useCurved
                    && tag.LineKind == GiaPhaCanvasConnectorLineKind.CurvedBranch
                    && fe is System.Windows.Shapes.Path curvePath)
                {
                    var child = FindNodeByFamilyId(tag.ChildFamilyId);
                    if (child == null)
                    {
                        continue;
                    }
                    double childCx2 = child.Xmm + child.Metrics.WidthMm / 2.0;
                    double startX = MmToPx(parentCx);
                    double startY = MmToPx(parentBottom);
                    double endX = MmToPx(childCx2);
                    double endY = MmToPx(child.Ymm);
                    double midYpx = (startY + endY) / 2.0;
                    var figure = new PathFigure { StartPoint = new Point(startX, startY), IsClosed = false, IsFilled = false };
                    figure.Segments.Add(new BezierSegment(
                        new Point(startX, midYpx),
                        new Point(endX, midYpx),
                        new Point(endX, endY),
                        true));
                    curvePath.Data = new PathGeometry(new[] { figure });
                    continue;
                }

                if (!(fe is Line line))
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

            // Dừng llama-server sidecar khi thoát app
            Application.Current.Exit += (s, args) => AI.LocalLlamaHost.Instance.Stop();

            phaDoSubtreeListBox.ItemsSource = _phaDoRenderScopes;
            UpdatePhaDoSubtreeListBoxToolTip();
            InitPhaDoCardLayoutCombo();
            InitPhaDoContextFontControls();
            ReloadPhaDoZoneSvgFromFolder();
            BindPhaDoZoneComboItems();

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
            BindFamilyTreeSearchScroll();
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
                if (row.Family != null && row.Family.Parent == null)
                {
                    viewModel?.FamilyTree?.SyncGiaphaNameFromRootThuyTo();
                }
            }

            SafeRefreshPersonGridView();
        }

        /// <summary>Xuất các dòng grid đang hiển thị (sau lọc) ra CSV UTF-8 có BOM.</summary>
        private void ExportPersonGridToCsv_Click(object sender, RoutedEventArgs e)
        {
            if (PersonGridView == null)
            {
                MessageBox.Show("Chưa có danh sách người. Bấm \"Danh sách\" trước.", "Xuất CSV");
                return;
            }

            var rows = PersonGridView.Cast<object>().OfType<PersonGridRow>().ToList();
            if (rows.Count == 0)
            {
                MessageBox.Show("Không có dòng nào để xuất (kiểm tra bộ lọc hoặc tải danh sách).", "Xuất CSV");
                return;
            }

            string baseName = (viewModel?.FamilyTree?.GiaphaName ?? "Nguoi").Trim();
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = "Nguoi";
            }

            baseName = string.Join("_", baseName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
            var dialog = new SaveFileDialog
            {
                Filter = "CSV UTF-8 (*.csv)|*.csv|Tất cả (*.*)|*.*",
                DefaultExt = "csv",
                FileName = baseName + "_Nguoi.csv"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                string csv = BuildPersonGridCsv(rows);
                File.WriteAllText(dialog.FileName, csv, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                viewModel?.AddUserAction("Xuất CSV người: " + dialog.FileName + " (" + rows.Count + " dòng)");
                MessageBox.Show(
                    "Đã xuất " + rows.Count + " dòng.\n\nFile: " + dialog.FileName,
                    "Xuất CSV");
            }
            catch (Exception ex)
            {
                log.Error("Lỗi xuất CSV người.", ex);
                MessageBox.Show("Lỗi xuất CSV: " + ex.Message, "Có lỗi");
            }
        }

        private static string BuildPersonGridCsv(IReadOnlyList<PersonGridRow> rows)
        {
            var sb = new StringBuilder(rows.Count * 96 + 128);
            sb.Append(CsvEscapeField("Gia đình"));
            sb.Append(',');
            sb.Append(CsvEscapeField("ID"));
            sb.Append(',');
            sb.Append(CsvEscapeField("Vai trò"));
            sb.Append(',');
            sb.Append(CsvEscapeField("Đời"));
            sb.Append(',');
            sb.Append(CsvEscapeField("Giới tính"));
            sb.Append(',');
            sb.Append(CsvEscapeField("Tên người"));
            sb.Append(',');
            sb.Append(CsvEscapeField("Ngày sinh"));
            sb.Append(',');
            sb.Append(CsvEscapeField("Ngày mất"));
            sb.Append(',');
            sb.Append(CsvEscapeField("Ở tại"));
            sb.Append(',');
            sb.AppendLine(CsvEscapeField("Chi tiết"));

            foreach (var row in rows)
            {
                string familyLabel = !string.IsNullOrEmpty(row.FamilyGroupLabelForDisplay)
                    ? row.FamilyGroupLabelForDisplay
                    : (row.FamilyGroupLabel ?? "");
                sb.Append(CsvEscapeField(familyLabel));
                sb.Append(',');
                sb.Append(CsvEscapeField(row.PersonId));
                sb.Append(',');
                sb.Append(CsvEscapeField(row.RoleInFamily));
                sb.Append(',');
                sb.Append(row.Generation.ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(CsvEscapeField(row.Gender));
                sb.Append(',');
                sb.Append(CsvEscapeField(row.PersonName));
                sb.Append(',');
                sb.Append(CsvEscapeField(row.BirthDate));
                sb.Append(',');
                sb.Append(CsvEscapeField(row.DeathDate));
                sb.Append(',');
                sb.Append(CsvEscapeField(row.Place));
                sb.Append(',');
                sb.AppendLine(CsvEscapeField(row.Detail));
            }

            return sb.ToString();
        }

        private static string CsvEscapeField(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        /// <summary>Rebuild index rule-based engine sau khi load hoặc sửa cây gia phả.</summary>
        public void RebuildAiQueryIndex()
        {
            var root = viewModel?.FamilyTree?.Family?.RootPerson;
            _aiQueryEngine.BuildIndex(root);

            // Nếu chat dialog đang mở → thông báo file mới và cập nhật root
            if (_aiChatDialog != null && _aiChatDialog.IsVisible)
            {
                var selected = viewModel?.FamilyTree?.Family?.SelectedFamily;
                _aiChatDialog.UpdateFileRoot(root, selected);
            }
        }

        /// <summary>Chat AI vừa sửa gia phả — chọn lại node và cập nhật index tra cứu.</summary>
        private void OnAiChatEditApplied(FamilyViewModel affectedFamily)
        {
            if (affectedFamily != null)
            {
                SelectFamilyInTreeView(affectedFamily);
            }

            // Chỉ rebuild index — không gọi RebuildAiQueryIndex (tránh bubble "tải file mới" trong chat)
            var root = viewModel?.FamilyTree?.Family?.RootPerson;
            _aiQueryEngine.BuildIndex(root);
            UpdateHtmlGiaPha();
        }

        public void UpdateHtmlGiaPha()
        {
            if (viewModel != null)
            {
                htmlEditorTocUoc.SetContentHtml(viewModel.FamilyTree.Tocuoc);
                htmlEditorPhaKy.SetContentHtml(viewModel.FamilyTree.PhaKy);
                htmlEditorHuongHoa.SetContentHtml(viewModel.FamilyTree.HuongHoa);
                htmlEditorThuyto.SetContentHtml(viewModel.FamilyTree.ThuyTo);

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
        private async void htmlEditorPhaKy_LostFocus(object sender, RoutedEventArgs e)
        {
            if (viewModel?.FamilyTree == null)
            {
                return;
            }

            viewModel.FamilyTree.PhaKy = await htmlEditorPhaKy.GetContentHtmlAsync();
        }

        private async void htmlEditorHuongHoa_LostFocus(object sender, RoutedEventArgs e)
        {
            if (viewModel?.FamilyTree == null)
            {
                return;
            }

            viewModel.FamilyTree.HuongHoa = await htmlEditorHuongHoa.GetContentHtmlAsync();
        }

        private async void htmlEditorThuyto_LostFocus(object sender, RoutedEventArgs e)
        {
            if (viewModel?.FamilyTree == null)
            {
                return;
            }

            viewModel.FamilyTree.ThuyTo = await htmlEditorThuyto.GetContentHtmlAsync();
        }

        private async void htmlEditorTocUoc_LostFocus(object sender, RoutedEventArgs e)
        {
            if (viewModel?.FamilyTree == null)
            {
                return;
            }

            viewModel.FamilyTree.Tocuoc = await htmlEditorTocUoc.GetContentHtmlAsync();
        }

        private void HtmlEditorTocUoc_DocumentReady(object sender, EventArgs e)
        {
            if (viewModel != null)
            {
                htmlEditorTocUoc.SetContentHtml(viewModel.FamilyTree.Tocuoc);
            }
            else
            {
                htmlEditorTocUoc.SetContentHtml("");
            }
        }

        private void HtmlEditorThuyto_DocumentReady(object sender, EventArgs e)
        {
            if (viewModel != null)
            {
                htmlEditorThuyto.SetContentHtml(viewModel.FamilyTree.ThuyTo);
            }
            else
            {
                htmlEditorThuyto.SetContentHtml("");
            }
        }

        private void HtmlEditorHuongHoa_DocumentReady(object sender, EventArgs e)
        {
            if (viewModel != null)
            {
                htmlEditorHuongHoa.SetContentHtml(viewModel.FamilyTree.HuongHoa);
            }
            else
            {
                htmlEditorHuongHoa.SetContentHtml("");
            }
        }

        private void HtmlEditorPhaKy_DocumentReady(object sender, EventArgs e)
        {
            if (viewModel != null)
            {
                htmlEditorPhaKy.SetContentHtml(viewModel.FamilyTree.PhaKy);
            }
            else
            {
                htmlEditorPhaKy.SetContentHtml("");
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
            if (leftPaneSearchPanel != null && leftPaneSearchPanel.ActualWidth > 0)
            {
                ApplyLeftPaneSearchScale(leftPaneSearchPanel.ActualWidth);
            }

            // Mặc định toolbox mở rộng khi khởi động.
            SetPhaDoToolboxExpanded(true);
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
            _phaDoSelectedPersonSlot = null;
            _phaDoDraggingFamilyId = 0;
            _phaDoIsDraggingPerson = false;
            _phaDoIsDraggingTitleLine = false;
            CancelPendingTitleDrag();
            ClearTitleSelection();
            ClearSelectionOverlay();
            UpdatePhaDoSelectedBoxSizeStatus(0);
        }

        private PhaDoRenderScopeItem GetSelectedPhaDoScope()
        {
            return phaDoSubtreeListBox?.SelectedItem as PhaDoRenderScopeItem;
        }

        /// <summary>
        /// Render phả con đa gốc: tạo 1 FamilyInfo ảo làm cha,
        /// clone toàn bộ cây từng nhánh non-STOP rồi gắn làm con của gia đình ảo đó,
        /// sau đó layout bình thường từ gốc ảo.
        /// </summary>
        private async Task<GiaPhaRenderResult> BuildMultiRootVerticalLayout(
            PhaDoRenderScopeItem scope,
            GiaPhaRenderOptions options)
        {
            var fileRoot = viewModel?.FamilyTree?.Family?.RootPerson;
            if (scope?.MultiRootFamilyIds == null || scope.MultiRootFamilyIds.Count == 0 || fileRoot == null)
            {
                return null;
            }

            // Resolve từng FamilyId sang FamilyViewModel trên cây file.
            var branches = new List<FamilyViewModel>();
            foreach (int id in scope.MultiRootFamilyIds)
            {
                var vm = FindFamilyById(fileRoot, id);
                if (vm != null)
                {
                    branches.Add(vm);
                }
            }

            if (branches.Count == 0)
            {
                return null;
            }

            // Tạo FamilyInfo ảo làm cha; FamilyLevel = level nhánh - 1 để cây layout đúng đời.
            int childLevel = branches[0].familyInfo?.FamilyLevel ?? 1;
            var virtualInfo = new FamilyInfo
            {
                FamilyId = -1,
                FamilyLevel = Math.Max(1, childLevel - 1)
                // Không có person → box hiển thị trống (nhỏ gọn trong renderer)
            };

            // Clone toàn bộ FamilyInfo của từng nhánh; đánh dấu FamilyUp = -1 để renderer nhận ra branch-head.
            foreach (var branch in branches)
            {
                var cloned = CloneFamilyInfoSubtree(branch, markAsBranchHead: true);
                if (cloned != null)
                {
                    virtualInfo.FamilyChildren.Add(cloned);
                }
            }

            // Truyền nhãn scope vào options để box ảo hiển thị đúng thông tin.
            options.MultiRootScopeLabel = scope.RenderPlanSummary ?? scope.Label ?? "Phả con đa gốc";

            // FamilyViewModel tự dựng toàn bộ cây con từ FamilyInfo.FamilyChildren.
            var virtualRoot = new FamilyViewModel(virtualInfo, null, viewModel.FamilyTree);

            return await GiaPhaRenderService
                .ComputeLayoutAsync(virtualRoot, options)
                .ConfigureAwait(true);
        }

        /// <summary>Clone toàn bộ FamilyInfo và cây con — không cắt đời, không chia sẻ reference với cây gốc.</summary>
        /// <param name="markAsBranchHead">True khi đây là gốc trực tiếp của gia đình ảo — đặt FamilyUp=-1 làm marker renderer.</param>
        private static FamilyInfo CloneFamilyInfoSubtree(FamilyViewModel src, bool markAsBranchHead = false)
        {
            if (src?.familyInfo == null)
            {
                return null;
            }

            var srcInfo = src.familyInfo;
            var clone = new FamilyInfo
            {
                FamilyId = srcInfo.FamilyId,
                // FamilyUp = -1 là sentinel: renderer nhận ra đây là gốc nhánh non-STOP trực tiếp.
                FamilyUp = markAsBranchHead ? -1 : srcInfo.FamilyUp,
                FamilyOrder = srcInfo.FamilyOrder,
                FamilyLevel = srcInfo.FamilyLevel,
                FamilyNew = srcInfo.FamilyNew,
                X = srcInfo.X,
                Y = srcInfo.Y,
                Width = srcInfo.Width,
                Height = srcInfo.Height,
                PhaDoShapeSvgId = srcInfo.PhaDoShapeSvgId
            };

            if (srcInfo.ListPerson != null)
            {
                clone.ListPerson = new ObservableCollection<PersonInfo>(srcInfo.ListPerson);
            }

            // Đệ quy clone con cháu từ Children của ViewModel (đã dựng sẵn, không dùng FamilyInfo.FamilyChildren trực tiếp).
            if (src.Children != null)
            {
                foreach (var child in src.Children)
                {
                    var childClone = CloneFamilyInfoSubtree(child);
                    if (childClone != null)
                    {
                        clone.FamilyChildren.Add(childClone);
                    }
                }
            }

            return clone;
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

                if (tabControl != null && session.SelectedTabIndex >= 0)
                {
                    int tabIndex = MapLegacyMainTabIndex(session.SelectedTabIndex);
                    if (tabIndex < tabControl.Items.Count)
                    {
                        tabControl.SelectedIndex = tabIndex;
                    }
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
                    // Session cũ: đã có vị trí/kích thước thủ công nhưng chưa có cờ ManualPositionSet
                    if (!_phaDoTitleStyle.ManualPositionSet
                        && (_phaDoTitleStyle.ManualWidthMm > 0
                            || _phaDoTitleStyle.ManualHeightMm > 0
                            || _phaDoTitleStyle.ManualLeftMm > 0
                            || _phaDoTitleStyle.ManualTopMm > 0))
                    {
                        _phaDoTitleStyle.ManualPositionSet = true;
                    }
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
            int maxGenerationInclusive = int.MaxValue,
            PhaDoRenderScopeItem scopeForContext = null,
            FamilyViewModel scopeDataRoot = null)
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

                if (scopeForContext != null && scopeDataRoot != null)
                {
                    if (scopeForContext.IsDefaultRoot0WithoutAnalyze)
                    {
                        progress.SetMessage(
                            "Đang chuẩn bị phạm vi Root0 (chưa phân tích)...\n\nĐã chờ: "
                            + (int)sw.Elapsed.TotalSeconds + " giây");
                    }
                    else if (scopeForContext.IsWholeTree)
                    {
                        progress.SetMessage(
                            "Đang vẽ toàn phả...\n\nĐã chờ: "
                            + (int)sw.Elapsed.TotalSeconds + " giây");
                    }

                    await ApplyRenderScopeContextFromSelectionAsync(scopeForContext, scopeDataRoot).ConfigureAwait(true);

                    if (scopeForContext.IsWholeTree)
                    {
                        // Luôn layout từ gốc file — không dùng RootFamily nhánh / không cắt đời.
                        rootOverride = scopeDataRoot;
                        maxGenerationInclusive = int.MaxValue;
                    }
                    else
                    {
                        rootOverride = scopeForContext.RootFamily ?? scopeDataRoot;
                        maxGenerationInclusive = scopeForContext.MaxGenerationInclusive;
                    }
                }

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
                var gp = await Task.Run(() =>
                {
                    var loaded = Database.FromJson(filePath);
                    if (loaded != null)
                    {
                        // Giải phóng bộ nhớ DOM JSON trước khi dựng cây WPF trên STA.
                        GC.Collect(2, GCCollectionMode.Optimized);
                    }

                    return loaded;
                }).ConfigureAwait(true);
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

            // Đã phân tích lần trước → hỏi trước khi chạy lại (tốn thời gian).
            if (_phaDoRenderScopesFromAnalyze && _phaDoRenderScopes.Count > 1)
            {
                var answer = MessageBox.Show(
                    "Phả con đã được phân tích trước đó.\n\nChạy lại phân tích sẽ tính toán lại toàn bộ.\nBạn có muốn tiếp tục không?",
                    "Xác nhận phân tích lại",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes)
                {
                    return;
                }
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
                GiaPhaRenderResult layout = await ComputePhaDoLayoutSnapshotAsync(root).ConfigureAwait(true);
                int analysisMaxLevel = 30;
                if (layout?.Nodes != null && layout.Nodes.Count > 0)
                {
                    int observedFromLayout = layout.Nodes
                        .Max(n => n?.Family?.familyInfo?.FamilyLevel ?? 0);
                    if (observedFromLayout > 0)
                    {
                        analysisMaxLevel = observedFromLayout;
                    }
                }

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

                var map = BuildSubtreeMap(
                    layout,
                    effectiveRoot0Max,
                    splitLevel,
                    subtreeMaxGeneration: analysisMaxLevel,
                    minBranchToSplitDeep: deepSplitMinFamilies);
                // Chỉ 1 phả con (hoặc không tách) → coi như không có phả con, chỉ phả chính Root0.
                int effectiveSplitLevel = HasMeaningfulPhaiConBranches(map) ? splitLevel : 0;
                UpdatePhaConFamilyFlags(
                    root,
                    effectiveSplitLevel,
                    analysisMaxLevel,
                    deepSplitMinFamilies,
                    map);
                // Đổ combo trước — báo cáo ghép kế hoạch vẽ khớp từng mục toolbar.
                UpdatePhaDoRenderScopesFromMap(
                    root,
                    map,
                    effectiveSplitLevel,
                    analysisMaxLevel,
                    deepSplitMinFamilies,
                    effectiveRoot0Max);

                string report = BuildPhaDoSplitAnalysisReport(
                    root,
                    layout,
                    minLevel: 1,
                    maxLevel: analysisMaxLevel,
                    minCutLevel: 3,
                    rootLevelMax: effectiveRoot0Max,
                    minBranchToSplitDeep: deepSplitMinFamilies,
                    renderScopes: _phaDoRenderScopes,
                    effectiveSplitLevel: effectiveSplitLevel,
                    subtreeMap: map);
                var dlg = new PhaDoSubtreeMapDialog(dpi: 96) { Owner = this };
                var subTreesForDialog = HasMeaningfulPhaiConBranches(map)
                    ? (IReadOnlyList<PhaDoSubtreeBranchBlock>)map.SubTrees
                    : Array.Empty<PhaDoSubtreeBranchBlock>();
                dlg.SetContent(report, map.RootBlock, subTreesForDialog, effectiveRoot0Max, effectiveSplitLevel);

                // Đóng dialog chờ trước khi mở cửa sổ phân tích để tránh cảm giác "kẹt" spinner/đếm.
                timer.Stop();
                sw.Stop();
                try { await progress.CloseAsync().ConfigureAwait(true); } catch { }

                // Đóng dialog phân tích cũ nếu còn mở (phân tích lại).
                try { _phaDoAnalysisDialog?.Close(); } catch { }
                _phaDoAnalysisDialog = dlg;
                // Modeless: user có thể đối chiếu với phả đồ chính trong khi dialog vẫn mở.
                dlg.Show();
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
            _phaDoFullTreeLayoutSnapshot = result;
            _phaDoFullTreeLayoutSnapshotRootId = root?.familyInfo?.FamilyId ?? 0;
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

        /// <summary>Chỉ khi có từ 2 phả con trở lên mới coi là đã tách — 1 nhánh = một phả chính Root0.</summary>
        private static bool HasMeaningfulPhaiConBranches(PhaDoSubtreeMap map)
        {
            return map?.SubTrees != null && map.SubTrees.Count > 1;
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

        /// <summary>Nhãn combo/tooltip sau Phân tích phả.</summary>
        private static string FormatPhaiConScopeLabel(PhaDoRenderScopeItem scope, PhaDoSubtreeBranchBlock block)
        {
            string plan = string.IsNullOrWhiteSpace(scope?.RenderPlanSummary)
                ? "Phả con"
                : scope.RenderPlanSummary.Trim();
            int gen = block?.Generation ?? scope?.RootFamily?.familyInfo?.FamilyLevel ?? 0;
            string doi = gen > 0
                ? "Đời " + gen.ToString(CultureInfo.InvariantCulture)
                : "Đời ?";
            string name = block != null
                ? (string.IsNullOrWhiteSpace(block.MainPersonName)
                    ? (block.FamilyName ?? "—").Trim()
                    : block.MainPersonName.Trim())
                : GetFamilyMainPersonName(scope?.RootFamily);
            int gdFile = block != null
                ? Math.Max(0, block.NodeCount)
                : CountSubtreeFamilies(scope?.RootFamily);
            int gdLayout = scope?.LayoutFamilyCountEstimate > 0
                ? scope.LayoutFamilyCountEstimate
                : gdFile;
            string gdPart = gdLayout > 0 && gdLayout != gdFile
                ? ("~" + gdLayout + " GD khi vẽ (file " + gdFile + ")")
                : (gdLayout + " GD");
            string sizePart = block != null
                ? block.WidthCm.ToString("0.#", CultureInfo.InvariantCulture) + "×"
                  + block.HeightCm.ToString("0.#", CultureInfo.InvariantCulture) + " cm"
                : "";

            if (string.IsNullOrWhiteSpace(sizePart))
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} | {1} | {2} | {3}",
                    plan,
                    doi,
                    name,
                    gdPart);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} | {1} | {2} | {3} | {4}",
                plan,
                doi,
                name,
                gdPart,
                sizePart);
        }

        /// <summary>
        /// Đếm GD khi vẽ: tại đời Root kế — STOP chỉ 1 ô; nhánh nhỏ nối dài tới lá (khớp BuildScopedRenderRoot).
        /// </summary>
        private static int CountLayoutFamiliesWithPhaConStopRules(
            FamilyViewModel node,
            int stopGenerationLevel,
            HashSet<int> stopFamilyIdsAtLevel)
        {
            if (node == null)
            {
                return 0;
            }

            int count = 1;
            int level = node.familyInfo?.FamilyLevel ?? 0;
            int id = node.familyInfo?.FamilyId ?? 0;
            if (node.Children == null || node.Children.Count == 0)
            {
                return count;
            }

            if (level == stopGenerationLevel
                && stopFamilyIdsAtLevel != null
                && stopFamilyIdsAtLevel.Count > 0
                && stopFamilyIdsAtLevel.Contains(id))
            {
                return count;
            }

            foreach (var child in node.Children)
            {
                count += CountLayoutFamiliesWithPhaConStopRules(
                    child,
                    stopGenerationLevel,
                    stopFamilyIdsAtLevel);
            }

            return count;
        }

        /// <summary>Đếm GD khi vẽ scope — dùng luật STOP/nối dài, không chỉ subtree file.</summary>
        private int EstimateScopeLayoutFamilyCount(PhaDoRenderScopeItem scope, FamilyViewModel fileRoot)
        {
            if (scope == null || fileRoot == null)
            {
                return 0;
            }

            if (scope.IsWholeTree)
            {
                return CountSubtreeFamilies(fileRoot);
            }

            var sourceRoot = scope.RootFamily ?? fileRoot;
            if (scope.MaxGenerationInclusive == int.MaxValue)
            {
                return CountSubtreeFamilies(sourceRoot);
            }

            if (scope.ExpandSmallBranchesAtStopLevel
                && scope.MaxGenerationInclusive > 0
                && scope.StopFamilyIdsAtMaxLevel != null
                && scope.StopFamilyIdsAtMaxLevel.Count > 0)
            {
                return CountLayoutFamiliesWithPhaConStopRules(
                    sourceRoot,
                    scope.MaxGenerationInclusive,
                    scope.StopFamilyIdsAtMaxLevel);
            }

            bool savedExpand = _phaDoScopeExpandSmallBranchesAtStopLevel;
            int savedMin = _phaDoScopeMinBranchForStopLevel;
            HashSet<int> savedStop = _phaDoScopeStopFamilyIdsAtMaxLevel;
            try
            {
                _phaDoScopeExpandSmallBranchesAtStopLevel = scope.ExpandSmallBranchesAtStopLevel;
                _phaDoScopeMinBranchForStopLevel = Math.Max(0, scope.MinBranchForStopLevel);
                _phaDoScopeStopFamilyIdsAtMaxLevel = scope.StopFamilyIdsAtMaxLevel != null
                    ? new HashSet<int>(scope.StopFamilyIdsAtMaxLevel)
                    : new HashSet<int>();
                FamilyViewModel scoped = BuildScopedRenderRoot(sourceRoot, scope.MaxGenerationInclusive);
                return CountSubtreeFamilies(scoped);
            }
            finally
            {
                _phaDoScopeExpandSmallBranchesAtStopLevel = savedExpand;
                _phaDoScopeMinBranchForStopLevel = savedMin;
                _phaDoScopeStopFamilyIdsAtMaxLevel = savedStop ?? new HashSet<int>();
            }
        }

        /// <summary>Chi tiết STOP vs nối dài tại đời Root kế — cho KẾ HOẠCH VẼ combo Root kế.</summary>
        private static void AppendScopeStopLevelBreakdown(
            StringBuilder sb,
            PhaDoRenderScopeItem scope,
            int minBranchToSplitDeep)
        {
            if (scope == null
                || scope.IsWholeTree
                || !scope.ExpandSmallBranchesAtStopLevel
                || scope.MaxGenerationInclusive <= 0
                || scope.MaxGenerationInclusive == int.MaxValue
                || scope.RootFamily == null)
            {
                return;
            }

            int stopLevel = scope.MaxGenerationInclusive;
            var stopIds = scope.StopFamilyIdsAtMaxLevel ?? new HashSet<int>();
            if (stopIds.Count == 0)
            {
                return;
            }

            var rootsAtStop = CollectRootsAtLevelFromBase(new[] { scope.RootFamily }, stopLevel);
            if (rootsAtStop.Count == 0)
            {
                return;
            }

            var extendRoots = rootsAtStop
                .Where(r => (r?.familyInfo?.FamilyId ?? 0) > 0 && !stopIds.Contains(r.familyInfo.FamilyId))
                .OrderByDescending(CountSubtreeFamilies)
                .ThenBy(r => r?.familyInfo?.FamilyId ?? 0)
                .ToList();
            int extendLineCount = extendRoots.Count;
            int extendGdTotal = extendRoots.Sum(r => CountSubtreeFamilies(r));

            sb.AppendLine("    ── Chi tiết tại đời " + stopLevel + " (trên combo này) ──");
            sb.AppendLine("    A) STOP (≥ " + minBranchToSplitDeep + " GD): " + stopIds.Count
                + " dòng — chỉ 1 box/dòng, không vẽ con; xem trang lá combo riêng.");
            sb.AppendLine("    B) Nối dài (< " + minBranchToSplitDeep + " GD): " + extendLineCount
                + " dòng | Tổng " + extendGdTotal + " GD (vẽ tới hết lá trên combo này):");

            int idx = 1;
            foreach (var r in extendRoots)
            {
                int id = r?.familyInfo?.FamilyId ?? 0;
                int sub = CountSubtreeFamilies(r);
                sb.AppendLine("       " + idx.ToString(CultureInfo.InvariantCulture) + ". ID " + id
                    + " | " + GetFamilyMainPersonName(r) + " | " + sub + " GD");
                idx++;
            }

            // Tóm tắt nhóm non-STOP → phả con đa gốc (multi-root).
            if (extendRoots.Count > 0)
            {
                var groups = GroupNonStopBranchesByThreshold(extendRoots, minBranchToSplitDeep);
                sb.AppendLine("    → Nhánh nối dài gom thành " + groups.Count + " nhóm phả con đa gốc (~"
                    + minBranchToSplitDeep + " GD/nhóm):");
                for (int g = 0; g < groups.Count; g++)
                {
                    var grp = groups[g];
                    int grpGd = grp.Sum(CountSubtreeFamilies);
                    sb.AppendLine("       Nhóm " + (g + 1) + "/" + groups.Count + ": "
                        + grp.Count + " nhánh, ≈" + grpGd + " GD");
                }
            }

            int totalLayout = CountLayoutFamiliesWithPhaConStopRules(
                scope.RootFamily,
                stopLevel,
                stopIds);
            sb.AppendLine("    → Tổng GD khi vẽ combo này (ước lượng): " + totalLayout
                + " = đoạn giữa + " + stopIds.Count + "×STOP(1) + " + extendGdTotal + " GD từ nhánh nối dài.");
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

        /// <summary>
        /// Gom các nhánh non-STOP tại stopLevel thành nhóm, mỗi nhóm tổng GD ≤ threshold (≥ threshold khi thêm nhánh tiếp).
        /// Duyệt theo thứ tự cây (tree order) để các nhóm liên tục địa lý.
        /// </summary>
        private static List<List<FamilyViewModel>> GroupNonStopBranchesByThreshold(
            IEnumerable<FamilyViewModel> nonStopBranches,
            int threshold)
        {
            var groups = new List<List<FamilyViewModel>>();
            var current = new List<FamilyViewModel>();
            int currentSum = 0;

            foreach (var branch in nonStopBranches)
            {
                if (branch == null)
                {
                    continue;
                }

                int gdCount = CountSubtreeFamilies(branch);

                // Nếu thêm vào sẽ vượt ngưỡng VÀ nhóm hiện tại không rỗng → kết nhóm, bắt đầu nhóm mới
                if (currentSum + gdCount > threshold && current.Count > 0)
                {
                    groups.Add(current);
                    current = new List<FamilyViewModel>();
                    currentSum = 0;
                }

                current.Add(branch);
                currentSum += gdCount;
            }

            if (current.Count > 0)
            {
                groups.Add(current);
            }

            return groups;
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

                // Helper: tạo ô tổng hợp non-STOP và thêm vào map.SubTrees.
                void AddNonStopSummary(
                    IEnumerable<FamilyViewModel> nonStopList,
                    int level,
                    int parentFamilyId,
                    int summaryIdSeed)
                {
                    var ns = new List<FamilyViewModel>(nonStopList ?? Enumerable.Empty<FamilyViewModel>());
                    if (ns.Count == 0) { return; }

                    double nsMinX = double.MaxValue, nsMinY = double.MaxValue;
                    double nsMaxX = double.MinValue, nsMaxY = double.MinValue;
                    int nsTotal = 0;
                    string firstName = null;

                    foreach (var r in ns)
                    {
                        if (TryGetSubtreeBoundsMm(
                            result, r, subtreeMaxGeneration,
                            out double minX, out double minY, out double maxX, out double maxY, out int cnt))
                        {
                            if (minX < nsMinX) { nsMinX = minX; }
                            if (minY < nsMinY) { nsMinY = minY; }
                            if (maxX > nsMaxX) { nsMaxX = maxX; }
                            if (maxY > nsMaxY) { nsMaxY = maxY; }
                            nsTotal += cnt;
                        }

                        if (firstName == null)
                        {
                            firstName = GetFamilyMainPersonName(r);
                        }
                    }

                    if (nsTotal <= 0) { return; }

                    map.SubTrees.Add(new PhaDoSubtreeBranchBlock
                    {
                        // ID âm → phân biệt với STOP block thật; unique theo seed.
                        FamilyId = -(Math.Abs(summaryIdSeed) + 1),
                        FamilyName = ns.Count + " nhánh non-STOP",
                        MainPersonName = (firstName ?? "?") + (ns.Count > 1 ? " …" : ""),
                        Generation = level,
                        NodeCount = nsTotal,
                        IsStop = false,
                        NonStopGroupCount = ns.Count,
                        SummaryParentId = parentFamilyId,
                        MinXmm = nsMinX < double.MaxValue ? nsMinX : 0,
                        MinYmm = nsMinY < double.MaxValue ? nsMinY : 0,
                        MaxXmm = nsMaxX > double.MinValue ? nsMaxX : 10,
                        MaxYmm = nsMaxY > double.MinValue ? nsMaxY : 10
                    });
                }

                // Cấp 1: nhánh STOP tách riêng, nhánh non-STOP gộp 1 ô summary.
                var currentLevelRoots = new List<FamilyViewModel>();
                var nonStopRoot1 = new List<FamilyViewModel>();
                foreach (var r in splitRoots)
                {
                    if (splitMetrics.SubtreeSize(r) < minBranchToSplitDeep)
                    {
                        nonStopRoot1.Add(r);
                        continue;
                    }

                    if (TryAddBranch(r, out int _))
                    {
                        // Đánh dấu STOP cho block vừa thêm.
                        var added = map.SubTrees.LastOrDefault(b => b.FamilyId == (r.familyInfo?.FamilyId ?? 0));
                        if (added != null) { added.IsStop = true; }
                        currentLevelRoots.Add(r);
                    }
                }

                // Thêm ô tổng hợp non-STOP tại root1.
                int rootParentId = map.RootBlock?.FamilyId ?? 0;
                AddNonStopSummary(nonStopRoot1, splitLevel, rootParentId, splitLevel);

                // Chỉ nhánh có đoạn đủ dài mới mở rộng cấp sau (cùng logic report).
                var baseRootsForNext = currentLevelRoots
                    .Where(splitMetrics.CanContinueSplit)
                    .ToList();

                while (baseRootsForNext.Count > 0)
                {
                    // Root2+: với mỗi baseRoot, lấy STOP + non-STOP tại level kế.
                    var anyAdded = false;
                    var nextBase = new List<FamilyViewModel>();

                    foreach (var baseRoot in baseRootsForNext)
                    {
                        if (!splitMetrics.TrySelectNextSplitLevel(
                            baseRoot, out int nextLevel, out var eligibleAtLevel, out _))
                        {
                            continue;
                        }

                        // STOP branches tại nextLevel.
                        foreach (var r in eligibleAtLevel)
                        {
                            if (TryAddBranch(r, out int _))
                            {
                                var added = map.SubTrees.LastOrDefault(b => b.FamilyId == (r.familyInfo?.FamilyId ?? 0));
                                if (added != null) { added.IsStop = true; }
                                nextBase.Add(r);
                                anyAdded = true;
                            }
                        }

                        // Non-STOP branches tại nextLevel dưới baseRoot này.
                        var allAtNext = CollectRootsAtLevelFromBase(new[] { baseRoot }, nextLevel);
                        var nonStopAtNext = allAtNext
                            .Where(r => splitMetrics.SubtreeSize(r) < minBranchToSplitDeep)
                            .ToList();
                        int nsIdSeed = (baseRoot.familyInfo?.FamilyId ?? 0) * 100 + nextLevel;
                        AddNonStopSummary(nonStopAtNext, nextLevel, baseRoot.familyInfo?.FamilyId ?? 0, nsIdSeed);
                    }

                    if (!anyAdded) { break; }

                    baseRootsForNext = nextBase
                        .Where(splitMetrics.CanContinueSplit)
                        .ToList();
                }
            }

            // Sắp xếp theo đời tăng dần rồi MinXmm để giữ thứ tự không gian.
            map.SubTrees.Sort((a, b) =>
            {
                int g = a.Generation.CompareTo(b.Generation);
                if (g != 0) { return g; }
                return a.MinXmm.CompareTo(b.MinXmm);
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
            int minBranchToSplitDeep = 500,
            IReadOnlyList<PhaDoRenderScopeItem> renderScopes = null,
            int effectiveSplitLevel = 0,
            PhaDoSubtreeMap subtreeMap = null)
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
            sb.AppendLine("Mục tiêu mềm kích thước phả con: ~" + PhaDoTargetFamilyCountPerSubtree
                + " gia đình (chọn mốc tách — KHÔNG phải số ô trên một trang vẽ)");
            sb.AppendLine("Ngưỡng tách sâu động: ≥ " + minBranchToSplitDeep
                + " gia đình (đủ lớn mới STOP / có box riêng)");
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

                if (renderScopes != null && renderScopes.Count > 0)
                {
                    AppendPhaConRenderPlanToReport(
                        sb,
                        renderScopes,
                        effectiveSplitLevel,
                        minBranchToSplitDeep,
                        subtreeMap,
                        effectiveSplitLevel);
                }

                return sb.ToString().TrimEnd();
            }

            sb.AppendLine();
            if (bestLevel <= 0)
            {
                sb.AppendLine("Không tìm thấy đời phù hợp để chia nhánh (cần đời có ≥ 2 gia đình).");
                sb.AppendLine("Gợi ý: thử tăng giới hạn đời hoặc chọn đời cắt thấp hơn/cao hơn.");
                if (renderScopes != null && renderScopes.Count > 0)
                {
                    AppendPhaConRenderPlanToReport(
                        sb,
                        renderScopes,
                        effectiveSplitLevel,
                        minBranchToSplitDeep,
                        subtreeMap,
                        effectiveSplitLevel);
                }

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
            sb.AppendLine("Quy tắc tách (khớp khi vẽ):");
            sb.AppendLine("- Root đủ ngưỡng → box phả con riêng; nhỏ hơn ngưỡng → gộp vào phả cha.");
            sb.AppendLine("- Tại mốc Root kế: ≥ ngưỡng → STOP; < ngưỡng → vẽ tiếp tới lá (Root0→Root1, Root1→Root2).");
            sb.AppendLine("- Cuối báo cáo: mục KẾ HOẠCH VẼ liệt kê từng combo — chọn rồi bấm Vẽ.");

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

                AppendCapLevelBranchDetailLines(
                    sb,
                    rootsAtCap,
                    splitMetrics,
                    minBranchToSplitDeep,
                    renderScopes);

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

            AppendPhaConCoverageVerification(
                sb,
                root,
                nodes.Count,
                splitMetrics,
                bestLevel,
                nodesByLevel,
                renderScopes,
                minBranchToSplitDeep);

            if (renderScopes != null && renderScopes.Count > 0)
            {
                AppendPhaConRenderPlanToReport(
                    sb,
                    renderScopes,
                    effectiveSplitLevel,
                    minBranchToSplitDeep,
                    subtreeMap,
                    bestLevel);
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>Đối soát: phân vùng nhánh có cộng đúng tổng file; combo lá không cộng chồng với Root kế.</summary>
        private static void AppendPhaConCoverageVerification(
            StringBuilder sb,
            FamilyViewModel root,
            int totalFamilies,
            PhaConSplitMetrics splitMetrics,
            int firstSplitLevel,
            Dictionary<int, List<FamilyViewModel>> nodesByLevel,
            IReadOnlyList<PhaDoRenderScopeItem> renderScopes,
            int minBranchToSplitDeep)
        {
            sb.AppendLine();
            sb.AppendLine("══════════════════════════════════════");
            sb.AppendLine("ĐỐI SOÁT SỐ GIA ĐÌNH");
            sb.AppendLine("══════════════════════════════════════");
            sb.AppendLine("Tổng file: " + totalFamilies + " GD.");
            AppendPhaConIntuitiveTotalSummary(
                sb,
                totalFamilies,
                firstSplitLevel,
                nodesByLevel,
                renderScopes,
                minBranchToSplitDeep);

            if (firstSplitLevel <= 0
                || nodesByLevel == null
                || !nodesByLevel.TryGetValue(firstSplitLevel, out var rootsAtFirstSplit)
                || rootsAtFirstSplit.Count == 0)
            {
                sb.AppendLine("(Không có mốc tách — bỏ qua đối soát phân vùng.)");
                return;
            }

            int sumBranchSubtrees = 0;
            sb.AppendLine();
            sb.AppendLine("① Phân vùng tại đời cắt đầu (đời " + firstSplitLevel
                + ") — các nhánh con KHÔNG chồng nhau:");
            foreach (var r in rootsAtFirstSplit.OrderBy(r => r?.familyInfo?.FamilyId ?? 0))
            {
                int sub = splitMetrics.SubtreeSize(r);
                sumBranchSubtrees += sub;
                sb.AppendLine("    · ID " + (r?.familyInfo?.FamilyId ?? 0) + " | "
                    + GetFamilyMainPersonName(r) + " | subtree = " + sub + " GD");
            }

            int ancestorOnly = totalFamilies - sumBranchSubtrees;
            int checkTotal = ancestorOnly + sumBranchSubtrees;
            sb.AppendLine("    → Tổng subtree các nhánh: " + sumBranchSubtrees
                + " + tổ tiên (đời < " + firstSplitLevel + ", không nằm trong subtree nhánh): "
                + ancestorOnly + " = " + checkTotal
                + (checkTotal == totalFamilies ? " ✓ khớp tổng file." : " ✗ KHÔNG khớp tổng file."));
            sb.AppendLine("    (Nhánh nhỏ gộp Root0 vẫn nằm trong subtree đó — không tách combo riêng.)");

            foreach (var r in rootsAtFirstSplit)
            {
                if (!splitMetrics.CanContinueSplit(r))
                {
                    continue;
                }

                if (!splitMetrics.TrySelectNextSplitLevel(
                        r,
                        out int probeLevel,
                        out List<FamilyViewModel> eligibleAtProbe,
                        out int segment))
                {
                    continue;
                }

                int parentSub = splitMetrics.SubtreeSize(r);
                var allAtProbe = CollectRootsAtLevelFromBase(new[] { r }, probeLevel);
                int sumAllProbeSubtrees = allAtProbe.Sum(splitMetrics.SubtreeSize);
                int sumEligibleOnly = eligibleAtProbe?.Sum(splitMetrics.SubtreeSize) ?? 0;
                int reconstructed = 1 + segment + sumAllProbeSubtrees;
                sb.AppendLine();
                sb.AppendLine("② Trong nhánh ID " + (r?.familyInfo?.FamilyId ?? 0)
                    + " (subtree " + parentSub + " GD), tách tiếp tại đời " + probeLevel + ":");
                sb.AppendLine("    · Đoạn đời " + ((r?.familyInfo?.FamilyLevel ?? 0) + 1) + "–"
                    + (probeLevel - 1) + " (không gồm root đời " + probeLevel + "): " + segment + " GD");
                sb.AppendLine("    · Số dòng đời " + probeLevel + " dưới nhánh này: " + allAtProbe.Count
                    + " | Tổng subtree các dòng (phân vùng, không chồng): " + sumAllProbeSubtrees + " GD");
                sb.AppendLine("    · Chỉ " + (eligibleAtProbe?.Count ?? 0) + " dòng ≥ " + minBranchToSplitDeep
                    + " GD có combo trang lá [3][4][5] — tổng subtree 3 dòng: " + sumEligibleOnly + " GD");
                sb.AppendLine("    · Kiểm tra: 1 + " + segment + " + " + sumAllProbeSubtrees + " = "
                    + reconstructed
                    + (reconstructed == parentSub
                        ? " ✓ khớp subtree cha."
                        : " ✗ lệch subtree cha (" + parentSub + ")."));
                if (sumEligibleOnly > 0 && sumEligibleOnly < parentSub)
                {
                    sb.AppendLine("    → " + sumEligibleOnly + " (3 trang lá) KHÔNG cộng bằng "
                        + parentSub + " hay " + totalFamilies + " — đúng thiết kế:");
                    sb.AppendLine("      · " + sumEligibleOnly + " = chỉ 3 dòng lớn tại đời " + probeLevel
                        + " (zoom riêng).");
                    sb.AppendLine("      · ~" + (parentSub - sumEligibleOnly) + " GD còn lại = đoạn 5–11 + "
                        + (allAtProbe.Count - (eligibleAtProbe?.Count ?? 0)) + " dòng nhỏ đời "
                        + probeLevel + " + mọi hậu duệ — nằm trong combo [2] (đời ≤" + probeLevel + ").");
                }
            }

            if (renderScopes != null && renderScopes.Count > 1)
            {
                sb.AppendLine();
                sb.AppendLine("③ Số GD khi vẽ từng combo (ước lượng clone — KHÔNG cộng để so tổng file):");
                int sumLayoutEst = 0;
                foreach (var s in renderScopes)
                {
                    if (s == null || s.IsWholeTree)
                    {
                        continue;
                    }

                    int c = s.LayoutFamilyCountEstimate > 0
                        ? s.LayoutFamilyCountEstimate
                        : CountSubtreeFamilies(s.RootFamily);
                    sumLayoutEst += c;
                    string role = s.ComboIndexHint == 1
                        ? " (màn hình đầu, ~vài GD)"
                        : (s.MaxGenerationInclusive != int.MaxValue && s.MaxGenerationInclusive < int.MaxValue / 2
                            ? " (phần lớn phả, đến đời " + s.MaxGenerationInclusive + ")"
                            : " (một dòng zoom)");
                    sb.AppendLine("    · Combo #" + s.ComboIndexHint + ": ~" + c + " GD khi vẽ" + role
                        + " | gốc ID " + s.FamilyId);
                }

                sb.AppendLine("    → Cộng thô #" + string.Join("+", renderScopes
                        .Where(s => s != null && !s.IsWholeTree)
                        .Select(s => s.ComboIndexHint.ToString(CultureInfo.InvariantCulture)))
                    + " ≈ " + sumLayoutEst + " GD — KHÔNG được so với " + totalFamilies
                    + " (trùng: [2] đã chứa gần hết; [3][4][5] là tập con của [2]).");
                sb.AppendLine("    → Công thức đúng cho tổng file: xem mục 「TÓM TẮT」 phía trên (①).");
            }
        }

        /// <summary>Giải thích vì sao 10949 ≠ tổng 3 trang lá — tránh nhầm với mục tiêu ~300 GD.</summary>
        private static void AppendPhaConIntuitiveTotalSummary(
            StringBuilder sb,
            int totalFamilies,
            int firstSplitLevel,
            Dictionary<int, List<FamilyViewModel>> nodesByLevel,
            IReadOnlyList<PhaDoRenderScopeItem> renderScopes,
            int minBranchToSplitDeep)
        {
            var leafScopesForHint = renderScopes?
                .Where(s => s != null && !s.IsWholeTree && s.MaxGenerationInclusive == int.MaxValue)
                .ToList();
            int leafSumHint = leafScopesForHint != null && leafScopesForHint.Count > 0
                ? leafScopesForHint.Sum(s => s.LayoutFamilyCountEstimate > 0
                    ? s.LayoutFamilyCountEstimate
                    : CountSubtreeFamilies(s.RootFamily))
                : 0;

            sb.AppendLine();
            sb.AppendLine("── TÓM TẮT (đọc trước) ──");
            sb.AppendLine("• " + totalFamilies + " = TOÀN BỘ cây gia phả (mọi đời 1–23).");
            if (leafSumHint > 0)
            {
                sb.AppendLine("• KHÔNG phải: " + totalFamilies + " = tổng " + leafScopesForHint.Count
                    + " trang lá (~" + leafSumHint + " GD). Đó chỉ là zoom 3 dòng lớn, không phải cả phả.");
            }

            sb.AppendLine("• ~300 / ~295 trong báo cáo = ngưỡng chọn mốc tách, không phải tổng ô vẽ.");

            if (firstSplitLevel <= 0 || nodesByLevel == null
                || !nodesByLevel.TryGetValue(firstSplitLevel, out var rootsAtSplit))
            {
                return;
            }

            int sumBranches = rootsAtSplit.Sum(r =>
            {
                int id = r?.familyInfo?.FamilyId ?? 0;
                return id > 0 ? CountSubtreeFamilies(r) : 0;
            });
            int ancestors = totalFamilies - sumBranches;

            sb.AppendLine();
            sb.AppendLine("Chia toàn phả tại đời " + firstSplitLevel + " (mỗi GD chỉ thuộc 1 nhánh):");
            sb.AppendLine("  " + totalFamilies + " = " + ancestors + " (tổ tiên đời < " + firstSplitLevel + ")"
                + " + tổng subtree " + rootsAtSplit.Count + " nhánh đời " + firstSplitLevel + " (" + sumBranches + ").");

            if (leafScopesForHint != null && leafScopesForHint.Count > 0)
            {
                sb.AppendLine("  " + leafScopesForHint.Count + " trang lá ≈ " + leafSumHint
                    + " GD — TẬP CON của nhánh lớn, không cộng thêm vào " + totalFamilies + ".");
            }

            var mainScope = renderScopes?
                .FirstOrDefault(s => s != null && !s.IsWholeTree
                    && s.MaxGenerationInclusive != int.MaxValue
                    && s.LayoutFamilyCountEstimate > 500);
            if (mainScope != null)
            {
                sb.AppendLine("  Combo [2] ~" + mainScope.LayoutFamilyCountEstimate
                    + " GD khi vẽ — xem chi tiết STOP / nối dài trong mục [2] bên dưới.");
            }

            sb.AppendLine("── Hết tóm tắt ──");
            sb.AppendLine();
        }

        /// <summary>Liệt kê từng nhánh tại một cấp cap: STOP / CONTINUE / gộp và mục combo tương ứng.</summary>
        private static void AppendCapLevelBranchDetailLines(
            StringBuilder sb,
            List<FamilyViewModel> rootsAtCap,
            PhaConSplitMetrics splitMetrics,
            int minBranchToSplitDeep,
            IReadOnlyList<PhaDoRenderScopeItem> renderScopes)
        {
            if (rootsAtCap == null || rootsAtCap.Count == 0)
            {
                return;
            }

            var comboByFamilyId = new Dictionary<int, PhaDoRenderScopeItem>();
            if (renderScopes != null)
            {
                foreach (var s in renderScopes)
                {
                    if (s == null || s.IsWholeTree || s.FamilyId <= 0)
                    {
                        continue;
                    }

                    comboByFamilyId[s.FamilyId] = s;
                }
            }

            sb.AppendLine("  - Chi tiết từng nhánh (STOP / CONTINUE / gộp):");
            foreach (var r in rootsAtCap
                         .OrderByDescending(splitMetrics.SubtreeSize)
                         .ThenBy(r => r?.familyInfo?.FamilyId ?? 0))
            {
                int id = r?.familyInfo?.FamilyId ?? 0;
                if (id <= 0)
                {
                    continue;
                }

                int sub = splitMetrics.SubtreeSize(r);
                int gen = r?.familyInfo?.FamilyLevel ?? 0;
                string name = GetFamilyMainPersonName(r);
                bool hasBox = sub >= minBranchToSplitDeep;
                bool canContinue = hasBox && splitMetrics.CanContinueSplit(r);
                int segment = canContinue ? splitMetrics.SegmentSizeToNextSplit(r) : 0;
                string action;
                if (!hasBox)
                {
                    action = "GỘP vào phả cha (< " + minBranchToSplitDeep + " GD)";
                }
                else if (!canContinue)
                {
                    action = "STOP — có box, không tách cấp sau";
                }
                else
                {
                    action = "CONTINUE — tách cấp sau, đoạn ~" + segment + " GD";
                }

                string comboHint = "";
                if (comboByFamilyId.TryGetValue(id, out var scope) && scope != null)
                {
                    comboHint = " | Combo #" + scope.ComboIndexHint;
                    if (!string.IsNullOrWhiteSpace(scope.RenderPlanSummary))
                    {
                        comboHint += " (" + scope.RenderPlanSummary + ")";
                    }

                }
                else if (!hasBox)
                {
                    comboHint = " | (chỉ trong Root0, không có mục combo riêng)";
                }

                string subLabel = canContinue && comboByFamilyId.TryGetValue(id, out var sc) && sc != null
                    && sc.LayoutFamilyCountEstimate > 0
                    ? (sub + " GD file | ~" + sc.LayoutFamilyCountEstimate + " GD khi vẽ")
                    : (sub + " GD");
                sb.AppendLine("    · Đời " + gen + " | ID " + id + " | " + name
                    + " | " + subLabel + " | " + action + comboHint);
            }
        }

        /// <summary>Kế hoạch vẽ sau Phân tích phả — khớp combo toolbar.</summary>
        private static void AppendPhaConRenderPlanToReport(
            StringBuilder sb,
            IReadOnlyList<PhaDoRenderScopeItem> renderScopes,
            int effectiveSplitLevel,
            int minBranchToSplitDeep,
            PhaDoSubtreeMap subtreeMap,
            int firstSplitGenerationLevel)
        {
            sb.AppendLine();
            sb.AppendLine("══════════════════════════════════════");
            sb.AppendLine("KẾ HOẠCH VẼ (combo trên toolbar)");
            sb.AppendLine("══════════════════════════════════════");
            sb.AppendLine("Sau bước này: chọn mục combo → bấm Vẽ. Không cần phân tích lại.");
            if (effectiveSplitLevel > 0)
            {
                sb.AppendLine("Đời cắt phả con đầu (Root1): đời " + effectiveSplitLevel
                    + " | Ngưỡng STOP: ≥ " + minBranchToSplitDeep + " GD");
            }

            sb.AppendLine();
            foreach (var scope in renderScopes)
            {
                if (scope == null)
                {
                    continue;
                }

                string title = string.IsNullOrWhiteSpace(scope.RenderPlanSummary)
                    ? (scope.IsWholeTree ? "Toàn phả" : "Phả con")
                    : scope.RenderPlanSummary;
                sb.AppendLine("[" + scope.ComboIndexHint + "] " + title);
                if (!string.IsNullOrWhiteSpace(scope.Label))
                {
                    sb.AppendLine("    " + scope.Label);
                }

                if (scope.IsWholeTree)
                {
                    sb.AppendLine("    → Layout toàn file, mọi đời.");
                }
                else
                {
                    string maxGenText = scope.MaxGenerationInclusive == int.MaxValue
                        ? "∞ (full nhánh)"
                        : scope.MaxGenerationInclusive.ToString(CultureInfo.InvariantCulture);
                    if (scope.LayoutFamilyCountEstimate > 0)
                    {
                        sb.AppendLine("    → Ước lượng " + scope.LayoutFamilyCountEstimate
                            + " GD khi vẽ (số node layout ≈ số này)");
                    }

                    sb.AppendLine("    → Gốc ID " + scope.FamilyId
                        + " | max đời (layout): " + maxGenText);
                    if (scope.ExpandSmallBranchesAtStopLevel)
                    {
                        sb.AppendLine("    → Nhánh < " + (scope.MinBranchForStopLevel > 0
                                ? scope.MinBranchForStopLevel
                                : minBranchToSplitDeep)
                            + " GD tại mốc kế: vẽ tiếp xuống lá; nhánh ≥ ngưỡng: STOP (box tách).");
                    }
                    else if (scope.MaxGenerationInclusive != int.MaxValue)
                    {
                        sb.AppendLine("    → Trang lá: layout full subtree đã chọn.");
                    }

                    int stopCount = scope.StopFamilyIdsAtMaxLevel?.Count ?? 0;
                    if (stopCount > 0)
                    {
                        sb.AppendLine("    → " + stopCount + " ID STOP tại đời "
                            + scope.MaxGenerationInclusive + " (không nối con).");
                    }

                    if (scope.HighlightStartLevel > 0)
                    {
                        sb.AppendLine("    → Marker Root kế: đời " + scope.HighlightStartLevel
                            + (string.IsNullOrWhiteSpace(scope.HighlightStartLabel)
                                ? ""
                                : " (" + scope.HighlightStartLabel + ")"));
                    }

                    // Nếu là combo multi-root: in danh sách nhánh kèm tên gia đình.
                    if (scope.IsMultiRootVerticalStack && scope.MultiRootFamilyIds != null)
                    {
                        sb.AppendLine("    → Phả con đa gốc: " + scope.MultiRootFamilyIds.Count
                            + " nhánh xếp dọc độc lập (nhóm "
                            + scope.MultiRootGroupIndex + "/" + scope.MultiRootGroupTotal + "):");
                        if (scope.MultiRootBranchLabels != null && scope.MultiRootBranchLabels.Count > 0)
                        {
                            foreach (string lbl in scope.MultiRootBranchLabels)
                            {
                                sb.AppendLine("       · " + lbl);
                            }
                        }
                        else
                        {
                            foreach (int rid in scope.MultiRootFamilyIds)
                            {
                                sb.AppendLine("       · ID " + rid);
                            }
                        }
                    }

                    AppendScopeStopLevelBreakdown(sb, scope, minBranchToSplitDeep);
                }

                sb.AppendLine();
            }

            if (subtreeMap?.SubTrees != null && effectiveSplitLevel > 0 && firstSplitGenerationLevel > 0)
            {
                var inCombo = new HashSet<int>(
                    renderScopes.Where(s => s != null && !s.IsWholeTree).Select(s => s.FamilyId));
                var mergedOnly = subtreeMap.SubTrees
                    .Where(b => b != null && b.Generation == firstSplitGenerationLevel
                        && b.FamilyId > 0 && !inCombo.Contains(b.FamilyId))
                    .ToList();
                if (mergedOnly.Count > 0)
                {
                    sb.AppendLine("Nhánh đời " + firstSplitGenerationLevel
                        + " gộp vào Root0 (không có combo riêng):");
                    foreach (var b in mergedOnly.OrderBy(b => b.FamilyId))
                    {
                        sb.AppendLine("    · ID " + b.FamilyId + " | "
                            + (string.IsNullOrWhiteSpace(b.MainPersonName) ? b.FamilyName : b.MainPersonName)
                            + " | " + b.NodeCount + " GD");
                    }
                    sb.AppendLine();
                }
            }

            sb.AppendLine("Gợi ý thao tác:");
            sb.AppendLine("- Xem Root0: combo [1] Root0→Root1 → Vẽ.");
            sb.AppendLine("- Xem nhánh lớn có tách tiếp (vd. ID 4880): chọn combo Root1→Root2 tương ứng → Vẽ.");
            sb.AppendLine("- Nhánh đời sâu chỉ STOP (không CONTINUE): chọn Trang lá → Vẽ full nhánh.");
        }

        private async Task<GiaPhaRenderResult> RenderPhaDoCoreAsync(
            bool resetZoom,
            bool resetScroll,
            FamilyViewModel rootOverride = null,
            int maxGenerationInclusive = int.MaxValue)
        {
            var fileRoot = viewModel.FamilyTree?.Family?.RootPerson;
            var root = rootOverride ?? fileRoot;
            if (root == null)
            {
                return null;
            }

            var options = BuildPhaDoRenderOptions();
            options.GetFamilyBoxNotes = BuildFamilyBoxExtraNotes;
            _phaDoCurrentOptions = options;

            // Toàn phả: layout cả file. Phả con: luôn gốc = RootFamily đã chọn (Root0 hoặc nhánh Root1…).
            GiaPhaRenderResult baseResult;
            FamilyViewModel scopedRoot;

            var selectedScope = GetSelectedPhaDoScope();
            if (selectedScope?.IsMultiRootVerticalStack == true)
            {
                // Multi-root vertical stack: layout từng nhánh độc lập rồi xếp dọc.
                baseResult = await BuildMultiRootVerticalLayout(selectedScope, options).ConfigureAwait(true);
                CapturePhaDoBaseLayout(baseResult);
            }
            else if (selectedScope?.IsPhaConMap == true)
            {
                // Bản đồ phả con: root0→root1 (non-STOP mở rộng tới lá; STOP tiếp tục xuống root2)→root2 STOP dừng.
                var mapRoot = BuildMapScopedRenderRoot(
                    root,
                    selectedScope.PhaConMapRoot1SplitLevel,
                    selectedScope.PhaConMapRoot1StopIds ?? new HashSet<int>(),
                    selectedScope.MaxGenerationInclusive,
                    selectedScope.PhaConMapDeepStopIds ?? new HashSet<int>());
                baseResult = await GiaPhaRenderService.ComputeLayoutAsync(mapRoot ?? root, options).ConfigureAwait(true);
                CapturePhaDoBaseLayout(baseResult);
            }
            else
            {
                bool renderWholeFileTree = maxGenerationInclusive == int.MaxValue
                    && (rootOverride == null
                        || fileRoot == null
                        || ReferenceEquals(rootOverride, fileRoot));
                if (renderWholeFileTree)
                {
                    scopedRoot = fileRoot ?? root;
                }
                else if (maxGenerationInclusive == int.MaxValue)
                {
                    // Nhánh lá (không tách Root kế): layout full subtree đã chọn, không clone.
                    scopedRoot = root;
                }
                else
                {
                    // Root0→Root1 / Root1→Root2: clone từ RootFamily (gốc scope), tại đời Root kế STOP hoặc nối dài.
                    scopedRoot = BuildScopedRenderRoot(root, maxGenerationInclusive) ?? root;
                }

                baseResult = await GiaPhaRenderService.ComputeLayoutAsync(scopedRoot, options).ConfigureAwait(true);
                CapturePhaDoBaseLayout(baseResult);
            }

            GiaPhaRenderResult result = GiaPhaManualLayoutService.ApplyManualOffsets(
                baseResult,
                _phaDoOffsetXmmByFamilyId,
                _phaDoOffsetYmmByFamilyId);
            ApplyCustomBoxSizesFromStyles(result);
            // Clone đã cắt tại splitLevel (cả STOP lẫn non-STOP) → trim chỉ cần khi không qua BuildScopedRenderRoot.
            if (!(_phaDoScopeExpandSmallBranchesAtStopLevel && maxGenerationInclusive < int.MaxValue)
                && maxGenerationInclusive > 0
                && maxGenerationInclusive != int.MaxValue)
            {
                TrimRenderResultByMaxGeneration(result, maxGenerationInclusive);
            }
            GiaPhaManualLayoutService.RebuildConnectorsOnly(result);
            GiaPhaRenderBoundsFitter.FitCanvasToContent(result);

            if (result.Nodes == null || result.Nodes.Count == 0)
            {
                await Dispatcher.InvokeAsync(
                    () =>
                    {
                        theCanvas?.Children.Clear();
                        MessageBox.Show(
                            "Layout xong nhưng không còn gia đình nào để vẽ (sau cắt đời / scope).\n"
                            + "Thử chọn scope khác hoặc bấm Phân tích phả lại.",
                            "Phả đồ",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    },
                    DispatcherPriority.Normal).Task.ConfigureAwait(true);
                return result;
            }

            // Populate dòng 3 + 4 tự động: đếm từ layout result trước khi vẽ
            PopulateTitleAutoLines(result);

            await Dispatcher.InvokeAsync(
                () => GiaPhaRenderService.PaintToCanvas(theCanvas, result),
                DispatcherPriority.Background).Task.ConfigureAwait(true);

            _phaDoRenderedLayout = result;
            RefreshAllBoxStylesOnCanvas();
            RefreshAllPersonOffsetsOnCanvas();
            ApplyTitleLineOffsets();
            DrawScopeSummaryNotes(result);
            DrawScopeStartMarkers(result);
            BringScopeStartMarkersToFront();

            if (_phaDoSelectedFamilyId > 0)
            {
                DrawSelectionOverlay(_phaDoSelectedFamilyId);
                DrawMultiSelectionOverlays();
                DrawDirectChildHighlights(_phaDoSelectedFamilyId);
                UpdatePhaDoSelectedBoxSizeStatus(_phaDoSelectedFamilyId);
                // Cập nhật toolbar ngữ cảnh theo box đang chọn
                SyncPhaDoToolbarFromBoxStyle(_phaDoSelectedFamilyId);
            }
            else
            {
                ClearDirectChildHighlights();
                DrawMultiSelectionOverlays();
                HideContextToolbar();
            }

            if (resetZoom)
            {
                await Dispatcher.InvokeAsync(
                    () => FitPhaDoViewToContent(result),
                    DispatcherPriority.Loaded).Task.ConfigureAwait(true);
            }

            if (resetScroll && !resetZoom)
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

        /// <summary>Thu nhỏ zoom + cuộn tới góc nội dung — phả hàng chục nghìn px không nhìn thấy ở 100%.</summary>
        private void FitPhaDoViewToContent(GiaPhaRenderResult result)
        {
            if (result == null || theCanvas == null)
            {
                ResetPhaDoZoom();
                return;
            }

            var scroll = phaDoScrollViewer ?? FindParent<ScrollViewer>(theCanvas);
            scroll?.UpdateLayout();
            theCanvas.UpdateLayout();

            double canvasW = result.CanvasWidthPixels > 0
                ? result.CanvasWidthPixels
                : (theCanvas.Width > 0 ? theCanvas.Width : 1);
            double canvasH = result.CanvasHeightPixels > 0
                ? result.CanvasHeightPixels
                : (theCanvas.Height > 0 ? theCanvas.Height : 1);

            double vpW = scroll?.ViewportWidth ?? 0;
            double vpH = scroll?.ViewportHeight ?? 0;
            if (vpW < 20 || vpH < 20)
            {
                // ScrollViewer chưa đo xong — thử lại sau layout pass.
                Dispatcher.BeginInvoke(new Action(() => FitPhaDoViewToContent(result)), DispatcherPriority.Loaded);
                return;
            }

            const double margin = 0.94;
            double fitZoom = Math.Min(vpW / canvasW, vpH / canvasH) * margin;
            if (double.IsNaN(fitZoom) || double.IsInfinity(fitZoom) || fitZoom <= 0)
            {
                fitZoom = 1.0;
            }

            fitZoom = Math.Max(PhaDoZoomFitMin, Math.Min(1.0, fitZoom));
            // null layout: không chỉnh scroll theo neo — Fit tự cuộn tới góc nội dung bên dưới.
            ApplyPhaDoZoomValue(fitZoom, scroll, layoutForAnchor: null);

            double minXmm = double.MaxValue;
            double minYmm = double.MaxValue;
            foreach (var node in result.Nodes)
            {
                if (node?.Metrics == null)
                {
                    continue;
                }

                minXmm = Math.Min(minXmm, node.Xmm);
                minYmm = Math.Min(minYmm, node.Ymm);
            }

            if (minXmm < double.MaxValue && scroll != null)
            {
                double dpi = result.Dpi > 0 ? result.Dpi : 96;
                double padPx = PrintUnits.MmToPixels(result.Options?.MarginMm ?? 5, dpi);
                double scrollX = Math.Max(0, PrintUnits.MmToPixels(minXmm, dpi) * _phaDoZoom - padPx);
                double scrollY = Math.Max(0, PrintUnits.MmToPixels(minYmm, dpi) * _phaDoZoom - padPx);
                scroll.ScrollToHorizontalOffset(scrollX);
                scroll.ScrollToVerticalOffset(scrollY);
            }
            else if (scroll != null)
            {
                scroll.ScrollToHorizontalOffset(0);
                scroll.ScrollToVerticalOffset(0);
            }
        }

        void searchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                viewModel.FamilyTree.Family.SearchCommand.Execute(null);
            }
        }

        private void LeftPaneSearchPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyLeftPaneSearchScale(e.NewSize.Width);
        }

        /// <summary>Pane trái hẹp thì thu nhỏ chữ ô tìm; nút tìm là icon cố định nên không cần thay chữ.</summary>
        private void ApplyLeftPaneSearchScale(double panelWidth)
        {
            if (searchTextBox == null || panelWidth <= 0)
                return;

            // ~300px: font đầy đủ; dưới ~140px: font tối thiểu
            double t = Math.Max(0, Math.Min(1.0, (panelWidth - 140) / 160.0));
            searchTextBox.FontSize = Math.Round(10 + 6 * t, 1);
        }
        private void TreeViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            var item = sender as TreeViewItem;
            _treeDragSourceFamily = item?.DataContext as FamilyViewModel;
            _treeDragStartPoint = e.GetPosition(null);
            _treeDragStarted = false;
        }

        private void TreeViewItem_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _treeDragSourceFamily == null)
            {
                return;
            }

            Point pos = e.GetPosition(null);
            if (!_treeDragStarted)
            {
                if (Math.Abs(pos.X - _treeDragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
                    && Math.Abs(pos.Y - _treeDragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
                {
                    return;
                }

                _treeDragStarted = true;
            }

            var data = new DataObject(typeof(FamilyViewModel), _treeDragSourceFamily);
            DragDrop.DoDragDrop((DependencyObject)sender, data, DragDropEffects.Move);
            _treeDragSourceFamily = null;
            _treeDragStarted = false;
        }

        private enum TreeFamilyDeleteChoice
        {
            Cancel,
            PromoteChild,
            DeleteBranch
        }

        /// <summary>Shift+Delete — chọn cách xóa trước khi thực hiện.</summary>
        private TreeFamilyDeleteChoice ShowTreeFamilyDeleteChoiceDialog(FamilyViewModel family)
        {
            if (family == null)
            {
                return TreeFamilyDeleteChoice.Cancel;
            }

            var dlg = new Window
            {
                Title = "Xóa gia đình",
                Width = 460,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false
            };

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock
            {
                Text = "Gia đình: " + (family.Name ?? family.Name0),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12),
                FontSize = 14
            });
            panel.Children.Add(new TextBlock
            {
                Text = "Chọn thao tác xóa:",
                Margin = new Thickness(0, 0, 0, 8)
            });

            var choice = TreeFamilyDeleteChoice.Cancel;

            var btnPromote = new System.Windows.Controls.Button
            {
                Content = "1. Xóa GD này, đưa con lên thay (cần đúng 1 con)",
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEnabled = family.Parent != null && family.Children.Count == 1
            };
            btnPromote.Click += (s, ev) =>
            {
                choice = TreeFamilyDeleteChoice.PromoteChild;
                dlg.DialogResult = true;
                dlg.Close();
            };
            panel.Children.Add(btnPromote);

            var btnBranch = new System.Windows.Controls.Button
            {
                Content = "2. Xóa luôn cả nhánh (tất cả con cháu)",
                Margin = new Thickness(0, 0, 0, 6),
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEnabled = family.Parent != null
            };
            btnBranch.Click += (s, ev) =>
            {
                choice = TreeFamilyDeleteChoice.DeleteBranch;
                dlg.DialogResult = true;
                dlg.Close();
            };
            panel.Children.Add(btnBranch);

            var btnCancel = new System.Windows.Controls.Button
            {
                Content = "Hủy",
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Right,
                MinWidth = 80
            };
            btnCancel.Click += (s, ev) =>
            {
                choice = TreeFamilyDeleteChoice.Cancel;
                dlg.DialogResult = false;
                dlg.Close();
            };
            panel.Children.Add(btnCancel);

            dlg.Content = panel;
            dlg.ShowDialog();
            return choice;
        }

        private void HandleTreeFamilyShiftDelete(FamilyViewModel family)
        {
            if (family == null)
            {
                return;
            }

            var choice = ShowTreeFamilyDeleteChoiceDialog(family);
            if (choice == TreeFamilyDeleteChoice.Cancel)
            {
                return;
            }

            FamilyViewModel focusAfter = null;
            if (choice == TreeFamilyDeleteChoice.PromoteChild)
            {
                if (family.TryDeleteFamilyPromoteOnlyChild())
                {
                    focusAfter = viewModel?.FamilyTree?.Family?.SelectedFamily;
                }
            }
            else
            {
                focusAfter = family.TryDeleteFamilyBranch();
            }

            if (focusAfter != null)
            {
                InvalidatePersonGridCache();
                SelectFamilyInTreeView(focusAfter);
            }
        }

        private void TreeViewGiaPha_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (IsTreeFamilyLabelEditFocused(e.OriginalSource as DependencyObject))
            {
                return;
            }

            if (e.Key == Key.F11)
            {
                ToggleFullscreenWithF11();
                e.Handled = true;
                return;
            }

            var family = viewModel?.FamilyTree?.Family?.SelectedFamily;
            var mods = Keyboard.Modifiers;

            if (family != null && mods == ModifierKeys.Control)
            {
                if (e.Key == Key.C)
                {
                    family.CopyBranchToClipboard();
                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.V)
                {
                    var pasted = family.PasteCopiedBranchAsChild();
                    if (pasted != null)
                    {
                        e.Handled = true;
                        FocusNewTreeFamilyForEdit(pasted);
                    }

                    return;
                }
            }

            if (family != null && e.Key == Key.F2)
            {
                family.IsSelected = true;
                family.BeginTreeLabelEdit();
                e.Handled = true;
                Dispatcher.BeginInvoke(
                    new Action(() => FocusTreeFamilyEditTextBox(family)),
                    DispatcherPriority.Input);
                return;
            }

            if (family == null || (mods & ModifierKeys.Shift) != ModifierKeys.Shift)
            {
                return;
            }

            FamilyViewModel added = null;
            if (e.Key == Key.Insert)
            {
                added = family.InsertParentFamilyFromTree();
            }
            else if (e.Key == Key.Delete)
            {
                HandleTreeFamilyShiftDelete(family);
                e.Handled = true;
                return;
            }
            else if (e.Key == Key.Add || e.Key == Key.OemPlus)
            {
                added = family.InsertNewChildFamilyFromTree();
            }
            else if (e.Key == Key.E)
            {
                added = family.InsertNewSiblingEmFromTree();
            }
            else if (e.Key == Key.A)
            {
                added = family.InsertNewSiblingAnhFromTree();
            }
            else if (e.Key == Key.Up)
            {
                if (family.TryMoveSiblingOrderUp())
                {
                    e.Handled = true;
                    SelectFamilyInTreeView(family);
                }

                return;
            }
            else if (e.Key == Key.Down)
            {
                if (family.TryMoveSiblingOrderDown())
                {
                    e.Handled = true;
                    SelectFamilyInTreeView(family);
                }

                return;
            }

            if (added != null)
            {
                e.Handled = true;
                FocusNewTreeFamilyForEdit(added);
            }
        }

        private static bool IsTreeFamilyLabelEditFocused(DependencyObject source)
        {
            var textBox = source as TextBox;
            return textBox != null
                && textBox.DataContext is FamilyViewModel family
                && family.IsTreeLabelEditing;
        }

        /// <summary>Chọn gia đình mới thêm và mở F2 sửa tên ngay.</summary>
        private void FocusNewTreeFamilyForEdit(FamilyViewModel family)
        {
            if (family == null || viewModel?.FamilyTree?.Family == null)
            {
                return;
            }

            viewModel.FamilyTree.Family.SelectFamily(family);
            InvalidatePersonGridCache();

            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    ScrollTreeViewToFamily(family, 0);
                    family.BeginTreeLabelEdit();
                    FocusTreeFamilyEditTextBox(family);
                }),
                DispatcherPriority.Loaded);
        }

        private void FocusTreeFamilyEditTextBox(FamilyViewModel family)
        {
            if (family == null || !family.IsTreeLabelEditing || treeViewGiaPha == null)
            {
                return;
            }

            var item = FindTreeViewItem(treeViewGiaPha, family);
            if (item == null)
            {
                return;
            }

            var textBox = FindVisualChild<TextBox>(item) as TextBox;
            if (textBox != null)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }

        private void TreeFamilyEditTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            var family = textBox?.DataContext as FamilyViewModel;
            if (family?.IsTreeLabelEditing == true && textBox != null)
            {
                textBox.Focus();
                textBox.SelectAll();
            }
        }

        private void TreeFamilyEditTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            var family = (sender as TextBox)?.DataContext as FamilyViewModel;
            if (family == null || !family.IsTreeLabelEditing)
            {
                return;
            }

            if (e.Key == Key.Enter)
            {
                family.CommitTreeLabelEdit();
                e.Handled = true;
                treeViewGiaPha?.Focus();
            }
            else if (e.Key == Key.Escape)
            {
                family.CancelTreeLabelEdit();
                e.Handled = true;
                treeViewGiaPha?.Focus();
            }
        }

        private void TreeFamilyEditTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var family = (sender as TextBox)?.DataContext as FamilyViewModel;
            if (family == null || !family.IsTreeLabelEditing)
            {
                return;
            }

            family.CommitTreeLabelEdit();
        }

        private void TreeViewGiaPha_PreviewDragOver(object sender, DragEventArgs e)
        {
            var source = GetFamilyFromDragData(e);
            var target = GetFamilyFromTreeDragEvent(e);
            if (CanDropFamilyOnTree(source, target))
            {
                e.Effects = DragDropEffects.Move;
                if (target != null)
                {
                    target.IsSelected = true;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void TreeViewGiaPha_DragLeave(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        private void TreeViewGiaPha_Drop(object sender, DragEventArgs e)
        {
            var source = GetFamilyFromDragData(e);
            var target = GetFamilyFromTreeDragEvent(e);
            if (source == null || target == null || !CanDropFamilyOnTree(source, target))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            string message = FamilyViewModel.DescribeTreeDragMove(source, target);
            if (MessageBox.Show(
                    message,
                    "Xác nhận di chuyển gia đình",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question) != MessageBoxResult.OK)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            CaptureGiaPhaUndoSnapshot("Kéo thả cây gia phả");

            bool moved;
            if (source.Parent != null
                && target.Parent != null
                && ReferenceEquals(source.Parent, target.Parent))
            {
                moved = source.TryMoveAsSiblingAfter(target);
            }
            else
            {
                moved = source.TryMoveAsChildOf(target);
            }

            if (!moved)
            {
                MessageBox.Show("Không thể di chuyển gia đình tới vị trí này.", "Cây gia phả");
            }
            else
            {
                InvalidatePersonGridCache();
            }

            e.Effects = moved ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private static FamilyViewModel GetFamilyFromDragData(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(FamilyViewModel)))
            {
                return e.Data.GetData(typeof(FamilyViewModel)) as FamilyViewModel;
            }

            return null;
        }

        private FamilyViewModel GetFamilyFromTreeDragEvent(DragEventArgs e)
        {
            if (treeViewGiaPha == null)
            {
                return null;
            }

            Point pos = e.GetPosition(treeViewGiaPha);
            var hit = VisualTreeHelper.HitTest(treeViewGiaPha, pos);
            if (hit?.VisualHit == null)
            {
                return null;
            }

            var item = VisualUpwardSearch<TreeViewItem>(hit.VisualHit) as TreeViewItem;
            return item?.DataContext as FamilyViewModel;
        }

        private static bool CanDropFamilyOnTree(FamilyViewModel source, FamilyViewModel target)
        {
            if (source == null || target == null || ReferenceEquals(source, target))
            {
                return false;
            }

            // Gốc cây (không có cha) — chưa hỗ trợ kéo thả
            if (source.Parent == null)
            {
                return false;
            }

            if (target.IsDescendantOf(source))
            {
                return false;
            }

            return true;
        }

        private void OnSelected(object sender, RoutedEventArgs e)
        {
            var tvi = sender as TreeViewItem;
            var personModel = tvi?.DataContext as FamilyViewModel;
            if (personModel == null || viewModel?.FamilyTree?.Family == null)
            {
                return;
            }

            viewModel.FamilyTree.Family.SelectedFamily = personModel;
            log.Info("Chọn trên cây: " + viewModel.FamilyTree.Family.SelectedFamily.Name);
            if (tabControl.SelectedIndex != 1)
            {
                tabControl.SelectedIndex = 1;
            }

            // Click trên cây: không auto-cuộn — tránh nhảy scroll mất focus dòng vừa bấm.
            // Cuộn chỉ khi chọn từ Phả đồ / tìm kiếm (SelectFamilyInTreeView, RequestScrollToFamilyInTree).
            tvi?.Focus();

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
        private void Treeview_Family_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2)
            {
                return;
            }

            var family = (sender as FrameworkElement)?.DataContext as FamilyViewModel
                ?? viewModel?.FamilyTree?.Family?.SelectedFamily;
            if (family == null)
            {
                return;
            }

            family.IsSelected = true;
            family.BeginTreeLabelEdit();
            e.Handled = true;

            Dispatcher.BeginInvoke(
                new Action(() => FocusTreeFamilyEditTextBox(family)),
                DispatcherPriority.Input);
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
            SizeChanged += MainWindow_SizeChanged;
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_treePaneImmersive)
            {
                ApplyTreePaneFullscreenLayout();
            }
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
                if (e.Key == Key.Insert)
                {
                    var family = viewModel?.FamilyTree?.Family?.SelectedFamily;
                    if (family?.InsertPerson2FamilyClick != null
                        && family.InsertPerson2FamilyClick.CanExecute(null))
                    {
                        family.InsertPerson2FamilyClick.Execute(null);
                        e.Handled = true;
                    }

                    return;
                }

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
            PersonInfo personInfo = ((ToggleSwitch)sender).DataContext as PersonInfo;
            if (personInfo == null)
            {
                return;
            }

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

            // Nếu đã vẽ ít nhất 1 lần → xác nhận trước khi vẽ lại.
            try
            {
                // Đổi file hoặc list scope lệch (còn Root0 cũ trước phân tích) → reset chỉ còn Toàn phả.
                int currentRootId = root.familyInfo?.FamilyId ?? 0;
                bool needsDefaultScopes = _phaDoRenderScopeSourceRootId != currentRootId
                    || _phaDoRenderScopes.Count == 0
                    || (!_phaDoRenderScopesFromAnalyze && !IsPreAnalyzeOnlyScopeList());
                if (needsDefaultScopes)
                {
                    ResetPhaDoRenderScopes(root);
                }

                // Luôn lấy scope từ combo; chưa phân tích thì chỉ có Toàn phả.
                var selectedScope = phaDoSubtreeListBox?.SelectedItem as PhaDoRenderScopeItem;
                if (selectedScope == null || !_phaDoRenderScopes.Contains(selectedScope))
                {
                    selectedScope = _phaDoRenderScopes.FirstOrDefault();
                    if (phaDoSubtreeListBox != null)
                    {
                        phaDoSubtreeListBox.SelectedItem = selectedScope;
                    }
                }

                var result = await RunPhaDoRenderWithWaitDialogAsync(
                    resetZoom: true,
                    resetScroll: true,
                    scopeForContext: selectedScope,
                    scopeDataRoot: root).ConfigureAwait(true);
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

        /// <summary>Giữ nhãn phả con trên viền chọn box (ZIndex &gt; overlay chọn).</summary>
        private void BringScopeStartMarkersToFront()
        {
            if (theCanvas == null)
            {
                return;
            }

            const int markerZ = 1101;
            foreach (var fe in theCanvas.Children.OfType<FrameworkElement>())
            {
                if (fe.Tag is PhaDoScopeStartMarkerTag)
                {
                    Panel.SetZIndex(fe, markerZ);
                }
            }
        }

        /// <summary>Xóa marker highlight của scope phả con trước khi vẽ lại.</summary>
        private void ClearScopeStartMarkers()
        {
            var markers = theCanvas.Children
                .OfType<FrameworkElement>()
                .Where(fe => fe.Tag is PhaDoScopeStartMarkerTag)
                .Cast<UIElement>()
                .ToList();
            foreach (var marker in markers)
            {
                theCanvas.Children.Remove(marker);
            }
        }

        /// <summary>Xóa ghi chú mô tả scope trên khối tiêu đề.</summary>
        private void ClearScopeSummaryNotes()
        {
            var notes = theCanvas.Children
                .OfType<FrameworkElement>()
                .Where(fe => Equals(fe.Tag, "__PhaDoScopeSummaryNote"))
                .Cast<UIElement>()
                .ToList();
            foreach (var note in notes)
            {
                theCanvas.Children.Remove(note);
            }
        }

        /// <summary>Hiển thị mô tả scope ngay dưới title/OTAI để user biết ngữ cảnh bản vẽ hiện tại.</summary>
        private void DrawScopeSummaryNotes(GiaPhaRenderResult result)
        {
            ClearScopeSummaryNotes();
            if (!_phaDoShowScopeSummaryNote || result == null || _phaDoCurrentOptions == null)
            {
                return;
            }

            double titleBottomMm = PhaDoTitleStyleResolver.TitleBlockHeightMm(_phaDoCurrentOptions) + _phaDoCurrentOptions.MarginMm;
            double x = MmToPx(_phaDoCurrentOptions.MarginMm + 3);
            double y = MmToPx(titleBottomMm + 2);
            string startName = string.IsNullOrWhiteSpace(_phaDoScopeStartFamilyName) ? "(không rõ)" : _phaDoScopeStartFamilyName.Trim();
            string sizeText = (result.ContentWidthMm / 10.0).ToString("0.#") + " cm x " + (result.ContentHeightMm / 10.0).ToString("0.#") + " cm";
            string note = "Đây là phả con bắt đầu từ: " + startName
                + Environment.NewLine + "Kích thước: " + sizeText;
                //+ Environment.NewLine + "Ô xanh dương: sẽ tách phả con | Ô xanh ngọc: nhánh nhỏ (vẽ tiếp trong phả)"
                //+ Environment.NewLine + "Nhãn cam / xanh / tím: điểm bắt đầu phả con kế tiếp";

            double noteFontPx = (_phaDoCurrentOptions.TitleLine2FontPt > 0 ? _phaDoCurrentOptions.TitleLine2FontPt : 12)
                * (_phaDoCurrentOptions.PrintDpi > 0 ? _phaDoCurrentOptions.PrintDpi : 96) / 72.0;

            var textMoTaPhaCon = new TextBlock
            {
                Text = note,
                Foreground = Brushes.DarkSlateGray,
                FontSize = noteFontPx,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = Math.Max(220, MmToPx(Math.Max(80, result.ContentWidthMm * 0.35))),
                IsHitTestVisible = false
            };

            // Một khung viền bao quanh — dễ đọc trên nền phả đồ
            var boxMoTaPhaCon = new Border
            {
                Child = textMoTaPhaCon,
                Background = new SolidColorBrush(Color.FromArgb(245, 255, 255, 248)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x8E, 0xB8)),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 8, 10, 8),
                IsHitTestVisible = false,
                Tag = "__PhaDoScopeSummaryNote"
            };
            Canvas.SetLeft(boxMoTaPhaCon, x);
            Canvas.SetTop(boxMoTaPhaCon, y);
            Panel.SetZIndex(boxMoTaPhaCon, 530);
            theCanvas.Children.Add(boxMoTaPhaCon);
        }

        /// <summary>Ghi chú phần 4 trong ô gia đình (phả con, kích thước cm…).</summary>
        private IReadOnlyList<string> BuildFamilyBoxExtraNotes(int familyId)
        {
            var notes = new List<string>();
            if (familyId <= 0)
            {
                return notes;
            }

            var fullRoot = viewModel?.FamilyTree?.Family?.RootPerson;
            var family = FindFamilyById(fullRoot, familyId);
            int level = family?.familyInfo?.FamilyLevel ?? 0;

            if (_phaDoScopeHighlightStartLevel > 0 && level == _phaDoScopeHighlightStartLevel)
            {
                var fullFamily = FindFamilyById(fullRoot, familyId);
                if (fullFamily?.Children != null && fullFamily.Children.Count > 0)
                {
                    notes.Add("Khởi đầu phả con: " + CountSubtreeFamilies(fullFamily) + " GD");
                }
            }

            // Node bắt đầu phả con mới (scope stop) → ghi rõ nhãn + GD count.
            bool isScopeStop = _phaDoScopeStopFamilyIdsAtMaxLevel != null
                && _phaDoScopeStopFamilyIdsAtMaxLevel.Count > 0
                && _phaDoScopeStopFamilyIdsAtMaxLevel.Contains(familyId);
            if (isScopeStop)
            {
                var fullFamily2 = FindFamilyById(fullRoot, familyId);
                int gdCount = fullFamily2 != null ? CountSubtreeFamilies(fullFamily2) : 0;
                string gdPart = gdCount > 0 ? " — " + gdCount + " GD" : "";
                notes.Add("★ Bắt đầu phả con" + gdPart);
            }

            if (_phaConFamilyIds.Contains(familyId)
                && _phaConBoundsCmByFamilyId.TryGetValue(familyId, out var subSize))
            {
                notes.Add("kích thước phả con: W=" + subSize.WidthCm.ToString("0.#")
                    + "cm H=" + subSize.HeightCm.ToString("0.#") + " cm");
            }

            if (_phaConStopFamilyIds.Contains(familyId))
            {
                notes.Add("(Nhánh nhỏ, vẽ tiếp trong phả con)");
            }

            return notes;
        }

        private static int CountSubtreeFamilies(FamilyViewModel family)
        {
            if (family == null)
            {
                return 0;
            }

            int count = 0;
            var stack = new Stack<FamilyViewModel>();
            stack.Push(family);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur == null)
                {
                    continue;
                }

                count++;
                if (cur.Children == null)
                {
                    continue;
                }

                foreach (var child in cur.Children)
                {
                    stack.Push(child);
                }
            }

            return count;
        }

        /// <summary>Tô màu + nhãn cho các node bắt đầu phả con (ví dụ Root1 khi đang vẽ Root0).</summary>
        private void DrawScopeStartMarkers(GiaPhaRenderResult result)
        {
            ClearScopeStartMarkers();
            if (result?.Nodes == null || _phaDoScopeHighlightStartLevel <= 0)
            {
                return;
            }

            string label = string.IsNullOrWhiteSpace(_phaDoScopeHighlightLabel)
                ? "Khởi đầu phả con"
                : _phaDoScopeHighlightLabel.Trim();

            GetPhaConMarkerColors(_phaDoScopeHighlightRootIndex, out Color textColor, out Color textBgColor, out Color borderColor);

            var targets = result.Nodes
                .Where(n => (n?.Family?.familyInfo?.FamilyLevel ?? 0) == _phaDoScopeHighlightStartLevel)
                .ToList();
            foreach (var node in targets)
            {
                double x = MmToPx(node.Xmm);
                double y = MmToPx(node.Ymm + 5);
                double w = MmToPx(node.Metrics.WidthMm);

                int familyId = node.Family?.familyInfo?.FamilyId ?? 0;
                var fullFamily = FindFamilyById(viewModel?.FamilyTree?.Family?.RootPerson, familyId);

                // Kiểm tra theo cây gốc đầy đủ; scope render có thể đã cắt bớt con cháu.
                if (fullFamily?.Children == null || fullFamily.Children.Count == 0)
                {
                    continue;
                }

                int subtreeCount = CountSubtreeFamilies(fullFamily);
                string extraSizeLine = "";
                if (_phaConFamilyIds.Contains(familyId)
                    && _phaConBoundsCmByFamilyId.TryGetValue(familyId, out var subSize))
                {
                    extraSizeLine = Environment.NewLine
                        + "Kích thước " + subSize.WidthCm.ToString("0.#")
                        + " cm x " + subSize.HeightCm.ToString("0.#") + " cm";
                }
                var textBlockPhaCon = new TextBlock
                {
                    Text = "Khởi đầu phả con: " + subtreeCount + " GD" + extraSizeLine,
                    Foreground = new SolidColorBrush(textColor),
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = Math.Max(160, w - 8),
                    IsHitTestVisible = false
                };
                var markerBoxTextblockPhaCon = new Border
                {
                    Child = textBlockPhaCon,
                    Background = new SolidColorBrush(textBgColor),
                    BorderBrush = new SolidColorBrush(borderColor),
                    BorderThickness = new Thickness(1.2),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 3, 6, 3),
                    IsHitTestVisible = false,
                    Tag = new PhaDoScopeStartMarkerTag { FamilyId = familyId }
                };
                Canvas.SetLeft(markerBoxTextblockPhaCon, x + 4);
                Canvas.SetTop(markerBoxTextblockPhaCon, y + 4);
                Panel.SetZIndex(markerBoxTextblockPhaCon, 1101);
                theCanvas.Children.Add(markerBoxTextblockPhaCon);
            }
        }

        /// <summary>Màu nhãn marker phả con theo Root1/2/3 — tông tươi, đồng bộ chú thích.</summary>
        private static void GetPhaConMarkerColors(int highlightRootIndex, out Color textColor, out Color textBgColor, out Color borderColor)
        {
            switch (highlightRootIndex)
            {
                case 1:
                    textColor = Color.FromRgb(245, 124, 0);
                    textBgColor = Color.FromArgb(245, 255, 248, 225);
                    borderColor = Color.FromRgb(255, 183, 77);
                    break;
                case 2:
                    textColor = Color.FromRgb(21, 101, 192);
                    textBgColor = Color.FromArgb(245, 227, 242, 253);
                    borderColor = Color.FromRgb(100, 181, 246);
                    break;
                case 3:
                    textColor = Color.FromRgb(123, 31, 162);
                    textBgColor = Color.FromArgb(245, 243, 229, 245);
                    borderColor = Color.FromRgb(186, 104, 200);
                    break;
                default:
                    textColor = Color.FromRgb(0, 131, 143);
                    textBgColor = Color.FromArgb(245, 224, 247, 250);
                    borderColor = Color.FromRgb(77, 208, 225);
                    break;
            }
        }

        /// <summary>
        /// Tại đời Root2 (splitGenerationLevel): đủ GD → STOP (2.1); không đủ → nối dài (2.2).
        /// Chỉ áp dụng khi family đang đứng đúng đời mốc tách.
        /// </summary>
        /// <summary>
        /// Clone cây cho "Bản đồ phả con" với logic 2 tầng:
        /// <list type="bullet">
        ///   <item>Trên / tại đời root1: clone bình thường đến root1.</item>
        ///   <item>Tại đời root1 — nhánh non-STOP (&lt; ngưỡng): mở rộng tới hết lá.</item>
        ///   <item>Tại đời root1 — nhánh STOP (≥ ngưỡng): tiếp tục xuống root2 (đến <paramref name="deepMaxLevel"/>).</item>
        ///   <item>Trong nhánh STOP root1, gặp deepStopId hoặc vượt deepMaxLevel: dừng.</item>
        ///   <item>Non-STOP tại root2: mở rộng tới hết lá.</item>
        /// </list>
        /// </summary>
        private FamilyViewModel BuildMapScopedRenderRoot(
            FamilyViewModel sourceRoot,
            int root1SplitLevel,
            HashSet<int> root1StopIds,
            int deepMaxLevel,
            HashSet<int> deepStopIds)
        {
            if (sourceRoot == null)
            {
                return sourceRoot;
            }

            // --- Helper: clone đầy đủ tới hết lá (không cắt) ---
            FamilyInfo CloneFullSubtree(FamilyViewModel src)
            {
                if (src?.familyInfo == null)
                {
                    return null;
                }

                var srcInfo = src.familyInfo;
                var clone = new FamilyInfo
                {
                    FamilyId = srcInfo.FamilyId,
                    FamilyUp = srcInfo.FamilyUp,
                    FamilyOrder = srcInfo.FamilyOrder,
                    FamilyLevel = srcInfo.FamilyLevel,
                    FamilyNew = srcInfo.FamilyNew,
                    X = srcInfo.X, Y = srcInfo.Y,
                    Width = srcInfo.Width, Height = srcInfo.Height,
                    PhaDoShapeSvgId = srcInfo.PhaDoShapeSvgId
                };

                if (srcInfo.ListPerson != null)
                {
                    clone.ListPerson = new ObservableCollection<PersonInfo>(srcInfo.ListPerson);
                }

                if (src.Children != null)
                {
                    foreach (var child in src.Children)
                    {
                        var cc = CloneFullSubtree(child);
                        if (cc != null)
                        {
                            clone.FamilyChildren.Add(cc);
                        }
                    }
                }

                return clone;
            }

            // --- Helper: clone trong nhánh STOP root1 xuống root2 rồi dừng ---
            FamilyInfo CloneInsideStopBranch(FamilyViewModel src)
            {
                if (src?.familyInfo == null)
                {
                    return null;
                }

                var srcInfo = src.familyInfo;
                var clone = new FamilyInfo
                {
                    FamilyId = srcInfo.FamilyId,
                    FamilyUp = srcInfo.FamilyUp,
                    FamilyOrder = srcInfo.FamilyOrder,
                    FamilyLevel = srcInfo.FamilyLevel,
                    FamilyNew = srcInfo.FamilyNew,
                    X = srcInfo.X, Y = srcInfo.Y,
                    Width = srcInfo.Width, Height = srcInfo.Height,
                    PhaDoShapeSvgId = srcInfo.PhaDoShapeSvgId
                };

                if (srcInfo.ListPerson != null)
                {
                    clone.ListPerson = new ObservableCollection<PersonInfo>(srcInfo.ListPerson);
                }

                int level = srcInfo.FamilyLevel;
                int id = srcInfo.FamilyId;

                // Dừng tại root2 STOP ID hoặc vượt deepMaxLevel.
                if ((id > 0 && deepStopIds.Count > 0 && deepStopIds.Contains(id))
                    || level >= deepMaxLevel)
                {
                    return clone;
                }

                if (src.Children == null)
                {
                    return clone;
                }

                foreach (var child in src.Children)
                {
                    var cc = CloneInsideStopBranch(child);
                    if (cc != null)
                    {
                        clone.FamilyChildren.Add(cc);
                    }
                }

                return clone;
            }

            // --- Helper chính: clone từ root0 xuống đến root1 ---
            FamilyInfo CloneBranch(FamilyViewModel src)
            {
                if (src?.familyInfo == null)
                {
                    return null;
                }

                var srcInfo = src.familyInfo;
                var clone = new FamilyInfo
                {
                    FamilyId = srcInfo.FamilyId,
                    FamilyUp = srcInfo.FamilyUp,
                    FamilyOrder = srcInfo.FamilyOrder,
                    FamilyLevel = srcInfo.FamilyLevel,
                    FamilyNew = srcInfo.FamilyNew,
                    X = srcInfo.X, Y = srcInfo.Y,
                    Width = srcInfo.Width, Height = srcInfo.Height,
                    PhaDoShapeSvgId = srcInfo.PhaDoShapeSvgId
                };

                if (srcInfo.ListPerson != null)
                {
                    clone.ListPerson = new ObservableCollection<PersonInfo>(srcInfo.ListPerson);
                }

                int level = srcInfo.FamilyLevel;
                int id = srcInfo.FamilyId;

                if (src.Children == null || src.Children.Count == 0)
                {
                    return clone;
                }

                if (level < root1SplitLevel)
                {
                    // Chưa đến root1: clone bình thường.
                    foreach (var child in src.Children)
                    {
                        var cc = CloneBranch(child);
                        if (cc != null)
                        {
                            clone.FamilyChildren.Add(cc);
                        }
                    }
                    return clone;
                }

                // === Tại đời root1 ===
                bool isStop = id > 0 && root1StopIds.Count > 0 && root1StopIds.Contains(id);
                if (!isStop)
                {
                    // Non-STOP root1: mở rộng tới hết lá (nhỏ < ngưỡng, an toàn).
                    foreach (var child in src.Children)
                    {
                        var cc = CloneFullSubtree(child);
                        if (cc != null)
                        {
                            clone.FamilyChildren.Add(cc);
                        }
                    }
                }
                else
                {
                    // STOP root1: tiếp tục xuống root2, dừng tại root2 STOP.
                    foreach (var child in src.Children)
                    {
                        var cc = CloneInsideStopBranch(child);
                        if (cc != null)
                        {
                            clone.FamilyChildren.Add(cc);
                        }
                    }
                }

                return clone;
            }

            var rootInfo = CloneBranch(sourceRoot);
            if (rootInfo == null)
            {
                return sourceRoot;
            }

            return new FamilyViewModel(rootInfo, null, viewModel.FamilyTree);
        }

        private bool ShouldStopAtPhaConSplitBoundary(FamilyViewModel family, int splitGenerationLevel)
        {
            if (family?.familyInfo == null
                || family.familyInfo.FamilyLevel != splitGenerationLevel)
            {
                return false;
            }

            int id = family.familyInfo.FamilyId;
            if (id <= 0)
            {
                return false;
            }

            // Nhánh STOP lớn (cam, có scope riêng): dừng → 1 box, xem chi tiết qua scope.
            if (_phaConFamilyIds.Contains(id))
            {
                return true;
            }

            // Non-STOP đã được gom vào combo đa gốc: cũng dừng → có trang riêng.
            if (_phaConNonStopComboFamilyIds.Contains(id))
            {
                return true;
            }

            // Non-STOP nhỏ không có combo (stoppedAtCap): vẽ tiếp toàn bộ con cháu trong scope cha.
            return false;
        }

        /// <summary>
        /// Clone cho layout: tại Root kế — STOP (≥ ngưỡng) chỉ 1 box; nhánh nhỏ nối dài tới lá.
        /// </summary>
        private FamilyViewModel BuildScopedRenderRoot(FamilyViewModel sourceRoot, int splitGenerationLevel)
        {
            if (sourceRoot == null)
            {
                return null;
            }

            if (splitGenerationLevel <= 0 || splitGenerationLevel == int.MaxValue)
            {
                return sourceRoot;
            }

            FamilyInfo CloneBranch(FamilyViewModel src, bool extendAllDescendants)
            {
                if (src?.familyInfo == null)
                {
                    return null;
                }

                var srcInfo = src.familyInfo;
                var clone = new FamilyInfo
                {
                    FamilyId = srcInfo.FamilyId,
                    FamilyUp = srcInfo.FamilyUp,
                    FamilyOrder = srcInfo.FamilyOrder,
                    FamilyLevel = srcInfo.FamilyLevel,
                    FamilyNew = srcInfo.FamilyNew,
                    X = srcInfo.X,
                    Y = srcInfo.Y,
                    Width = srcInfo.Width,
                    Height = srcInfo.Height,
                    PhaDoShapeSvgId = srcInfo.PhaDoShapeSvgId
                };

                if (srcInfo.ListPerson != null)
                {
                    clone.ListPerson = new ObservableCollection<PersonInfo>(srcInfo.ListPerson);
                }

                int level = srcInfo.FamilyLevel;

                // Trên đời Root2: chỉ đi tiếp khi chưa tới mốc (hoặc chưa bật nối dài).
                if (level < splitGenerationLevel)
                {
                    if (src.Children == null || src.Children.Count == 0)
                    {
                        return clone;
                    }

                    foreach (var child in src.Children)
                    {
                        int childLevel = child?.familyInfo?.FamilyLevel ?? 0;
                        if (childLevel > splitGenerationLevel)
                        {
                            continue;
                        }

                        var childClone = CloneBranch(child, extendAllDescendants: false);
                        if (childClone != null)
                        {
                            clone.FamilyChildren.Add(childClone);
                        }
                    }

                    return clone;
                }

                // Đúng đời Root kế
                if (level == splitGenerationLevel)
                {
                    if (!extendAllDescendants && ShouldStopAtPhaConSplitBoundary(src, splitGenerationLevel))
                    {
                        return clone;
                    }

                    extendAllDescendants = true;
                }
                else if (!extendAllDescendants)
                {
                    // Dưới Root kế mà không được nối dài từ nhánh nhỏ → không thêm nhánh con.
                    return clone;
                }

                if (src.Children == null || src.Children.Count == 0)
                {
                    return clone;
                }

                foreach (var child in src.Children)
                {
                    if (!extendAllDescendants)
                    {
                        int childLevel = child?.familyInfo?.FamilyLevel ?? 0;
                        if (childLevel > splitGenerationLevel)
                        {
                            continue;
                        }
                    }

                    var childClone = CloneBranch(child, extendAllDescendants);
                    if (childClone != null)
                    {
                        clone.FamilyChildren.Add(childClone);
                    }
                }

                return clone;
            }

            var rootInfoClone = CloneBranch(sourceRoot, extendAllDescendants: false);
            if (rootInfoClone == null)
            {
                return sourceRoot;
            }

            return new FamilyViewModel(rootInfoClone, null, viewModel?.FamilyTree);
        }

        /// <summary>
        /// Đánh dấu memory cho 2 nhóm:
        /// - family-phacon: đủ lớn để tách (vẽ tới đây thì dừng)
        /// - family-phacon-stop: nhánh dừng (tiếp tục vẽ hết con cháu)
        /// </summary>
        private void UpdatePhaConFamilyFlags(
            FamilyViewModel root,
            int splitLevel,
            int subtreeMaxGeneration,
            int minBranchToSplitDeep,
            PhaDoSubtreeMap map)
        {
            _phaConBoundsCmByFamilyId.Clear();
            if (root == null || splitLevel <= 0)
            {
                _phaConFamilyIds.Clear();
                _phaConStopFamilyIds.Clear();
                return;
            }

            _phaDoAnalyzeMaxFamilyLevel = Math.Max(1, subtreeMaxGeneration);

            if (map?.SubTrees != null)
            {
                foreach (var block in map.SubTrees)
                {
                    int id = block?.FamilyId ?? 0;
                    if (id <= 0)
                    {
                        continue;
                    }

                    double wCm = Math.Max(0, block.MaxXmm - block.MinXmm) / 10.0;
                    double hCm = Math.Max(0, block.MaxYmm - block.MinYmm) / 10.0;
                    _phaConBoundsCmByFamilyId[id] = (wCm, hCm);
                }
            }

            PopulatePhaConFlagsAtStopLevel(root, splitLevel, minBranchToSplitDeep, subtreeMaxGeneration);
        }

        /// <summary>Xóa ngữ cảnh tách phả con — dùng trước khi vẽ Toàn phả.</summary>
        private void ApplyWholeTreeRenderScopeContext(FamilyViewModel root)
        {
            _phaConFamilyIds.Clear();
            _phaConStopFamilyIds.Clear();
            _phaConBoundsCmByFamilyId.Clear();
            _phaDoScopeHighlightStartLevel = 0;
            _phaDoScopeHighlightRootIndex = 0;
            _phaDoScopeHighlightLabel = null;
            _phaDoScopeExpandSmallBranchesAtStopLevel = false;
            _phaDoScopeMinBranchForStopLevel = 0;
            _phaDoScopeStopFamilyIdsAtMaxLevel = new HashSet<int>();
            _phaDoShowScopeSummaryNote = false;
            _phaDoScopeStartFamilyName = GetFamilyMainPersonName(root);
        }

        /// <summary>Áp tham số scope lên biến render (gọi trước RunPhaDoRender).</summary>
        private async Task ApplyRenderScopeContextFromSelectionAsync(PhaDoRenderScopeItem scope, FamilyViewModel root)
        {
            if (scope != null && scope.IsWholeTree)
            {
                scope.MaxGenerationInclusive = int.MaxValue;
                scope.ExpandSmallBranchesAtStopLevel = false;
                scope.StopFamilyIdsAtMaxLevel = null;
                ApplyWholeTreeRenderScopeContext(root);
                return;
            }

            if (scope != null && scope.IsDefaultRoot0WithoutAnalyze)
            {
                await ConfigureDefaultRoot0ScopeForRenderAsync(scope, root).ConfigureAwait(true);
            }

            _phaDoScopeHighlightStartLevel = scope?.HighlightStartLevel ?? 0;
            _phaDoScopeHighlightRootIndex = scope?.HighlightStartRootIndex ?? 0;
            _phaDoScopeHighlightLabel = scope?.HighlightStartLabel;
            _phaDoScopeExpandSmallBranchesAtStopLevel = scope?.ExpandSmallBranchesAtStopLevel == true;
            _phaDoScopeMinBranchForStopLevel = Math.Max(0, scope?.MinBranchForStopLevel ?? 0);
            _phaDoScopeStopFamilyIdsAtMaxLevel = new HashSet<int>();
            _phaDoShowScopeSummaryNote = scope != null && !scope.IsWholeTree;
            _phaDoScopeStartFamilyName = GetFamilyMainPersonName(scope?.RootFamily ?? root);
            // Root0→Root1, Root1→Root2…: đánh dấu phacon/phacon-stop tại đúng mốc scope (không dùng cờ Root0 cũ).
            ApplyPhaConFlagsForRenderScope(scope, root);
        }

        /// <summary>
        /// Root0 khi chưa phân tích: splitLevel từ layout cây đầy đủ + mở rộng nhánh nhỏ (cùng luồng sau Phân tích phả).
        /// </summary>
        private async Task ConfigureDefaultRoot0ScopeForRenderAsync(PhaDoRenderScopeItem scope, FamilyViewModel root)
        {
            if (scope == null || root == null || !scope.IsDefaultRoot0WithoutAnalyze)
            {
                return;
            }

            int rootId = root.familyInfo?.FamilyId ?? 0;
            GiaPhaRenderResult layoutHint = null;
            if (_phaDoFullTreeLayoutSnapshot?.Nodes != null
                && _phaDoFullTreeLayoutSnapshotRootId == rootId
                && _phaDoFullTreeLayoutSnapshot.Nodes.Count > 0)
            {
                layoutHint = _phaDoFullTreeLayoutSnapshot;
            }
            else
            {
                // Chưa có snapshot (mở file → Vẽ): layout một lần cây đầy đủ để chọn đời tách — không dùng _phaDoRenderedLayout đã scope.
                layoutHint = await ComputePhaDoLayoutSnapshotAsync(root).ConfigureAwait(true);
            }

            int familyCount = layoutHint?.Nodes?.Count ?? CountFamiliesInTree(root);
            int minBranch = ComputeAdaptiveMinBranchToSplitDeep(familyCount);

            int splitLevel = -1;
            if (layoutHint != null && ShouldSplitPhaiCon(familyCount))
            {
                splitLevel = ResolveSuggestedSplitLevel(
                    root,
                    layoutHint,
                    minLevel: 1,
                    maxLevel: 30,
                    minCutLevel: 3,
                    minBranchToSplitDeep: minBranch);
            }
            else if (ShouldSplitPhaiCon(CountFamiliesInTree(root)))
            {
                int maxLevel = GetMaxFamilyLevelInTree(root);
                if (maxLevel > PhaDoDefaultRoot0MaxGeneration)
                {
                    splitLevel = Math.Min(maxLevel, PhaDoDefaultRoot0MaxGeneration + 1);
                }
            }

            if (splitLevel <= 0)
            {
                scope.MaxGenerationInclusive = PhaDoDefaultRoot0MaxGeneration;
                scope.ExpandSmallBranchesAtStopLevel = false;
                scope.HighlightStartLevel = 0;
                scope.HighlightStartRootIndex = 0;
                scope.MinBranchForStopLevel = 0;
                scope.StopFamilyIdsAtMaxLevel = null;
                scope.Label = "Phả con (Root0, đời 1–" + PhaDoDefaultRoot0MaxGeneration + ")";
                _phaConFamilyIds.Clear();
                _phaConStopFamilyIds.Clear();
                return;
            }

            scope.MaxGenerationInclusive = splitLevel;
            scope.HighlightStartLevel = splitLevel;
            scope.HighlightStartRootIndex = 1;
            scope.HighlightStartLabel = "Khởi đầu phả con";
            scope.ExpandSmallBranchesAtStopLevel = true;
            scope.MinBranchForStopLevel = minBranch;
            scope.Label = "Phả con (Root0 -> Root1, chưa phân tích)";
            scope.StopFamilyIdsAtMaxLevel = BuildStopFamilyIdsAtLevel(root, splitLevel, minBranch, 30);
        }

        private static int CountFamiliesInTree(FamilyViewModel root)
        {
            if (root == null)
            {
                return 0;
            }

            int count = 0;
            var stack = new Stack<FamilyViewModel>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur == null)
                {
                    continue;
                }

                count++;
                if (cur.Children == null)
                {
                    continue;
                }

                foreach (var child in cur.Children)
                {
                    stack.Push(child);
                }
            }

            return count;
        }

        private static int GetMaxFamilyLevelInTree(FamilyViewModel root)
        {
            if (root == null)
            {
                return 0;
            }

            int max = 0;
            var stack = new Stack<FamilyViewModel>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur == null)
                {
                    continue;
                }

                int level = cur.familyInfo?.FamilyLevel ?? 0;
                if (level > max)
                {
                    max = level;
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

            return max;
        }

        /// <summary>ID các nhánh đủ lớn tại một đời tách (dừng tại mốc, nhánh nhỏ vẽ tiếp).</summary>
        private static HashSet<int> BuildStopFamilyIdsAtLevel(
            FamilyViewModel scopeRoot,
            int stopLevel,
            int minBranch,
            int effectiveMaxGeneration)
        {
            var stopIds = new HashSet<int>();
            if (scopeRoot == null || stopLevel <= 0 || stopLevel >= int.MaxValue)
            {
                return stopIds;
            }

            var splitMetrics = new PhaConSplitMetrics(
                effectiveMaxGeneration,
                minBranch,
                PhaDoTargetFamilyCountPerSubtree);
            foreach (var family in CollectRootsAtLevelFromBase(new[] { scopeRoot }, stopLevel))
            {
                int familyId = family?.familyInfo?.FamilyId ?? 0;
                if (familyId > 0 && splitMetrics.SubtreeSize(family) >= minBranch)
                {
                    stopIds.Add(familyId);
                }
            }

            return stopIds;
        }

        /// <summary>Đánh dấu phacon / phacon-stop tại mốc tách của scope đang vẽ.</summary>
        private void PopulatePhaConFlagsAtStopLevel(
            FamilyViewModel scopeRoot,
            int stopLevel,
            int minBranch,
            int effectiveMaxGeneration)
        {
            _phaConFamilyIds.Clear();
            _phaConStopFamilyIds.Clear();

            if (scopeRoot == null || stopLevel <= 0 || stopLevel >= int.MaxValue)
            {
                return;
            }

            var splitMetrics = new PhaConSplitMetrics(
                effectiveMaxGeneration,
                minBranch,
                PhaDoTargetFamilyCountPerSubtree);
            foreach (var family in CollectRootsAtLevelFromBase(new[] { scopeRoot }, stopLevel))
            {
                int familyId = family?.familyInfo?.FamilyId ?? 0;
                if (familyId <= 0)
                {
                    continue;
                }

                if (splitMetrics.SubtreeSize(family) >= minBranch)
                {
                    _phaConFamilyIds.Add(familyId);
                }
                else
                {
                    _phaConStopFamilyIds.Add(familyId);
                }
            }
        }

        /// <summary>
        /// Trước vẽ: đánh dấu phacon/phacon-stop tại đời Root kế (Root2 khi scope bắt đầu từ Root1).
        /// Phải gọi trước BuildScopedRenderRoot — dùng _phaDoScopeStopFamilyIdsAtMaxLevel.
        /// </summary>
        private void ApplyPhaConFlagsForRenderScope(PhaDoRenderScopeItem scope, FamilyViewModel fileRoot)
        {
            if (scope == null || scope.IsWholeTree)
            {
                return;
            }

            // Bản đồ phả con: cần tô màu đặc biệt cho cả root1 stops + root2+ stops.
            if (scope.IsPhaConMap)
            {
                _phaConFamilyIds.Clear();
                _phaConStopFamilyIds.Clear();
                _phaConNonStopComboFamilyIds.Clear();
                if (scope.PhaConMapRoot1StopIds != null)
                {
                    foreach (int id in scope.PhaConMapRoot1StopIds)
                    {
                        _phaConFamilyIds.Add(id);
                    }
                }

                // Tổ hợp root1 + deep stops → dùng làm cờ màu "bắt đầu phả con" trong scope stop.
                var combined = new HashSet<int>(_phaConFamilyIds);
                if (scope.PhaConMapDeepStopIds != null)
                {
                    foreach (int id in scope.PhaConMapDeepStopIds)
                    {
                        combined.Add(id);
                    }
                }

                _phaDoScopeStopFamilyIdsAtMaxLevel = combined;
                return;
            }

            if (!scope.ExpandSmallBranchesAtStopLevel
                || scope.MaxGenerationInclusive <= 0
                || scope.MaxGenerationInclusive == int.MaxValue)
            {
                _phaConFamilyIds.Clear();
                _phaConStopFamilyIds.Clear();
                _phaConNonStopComboFamilyIds.Clear();
                _phaDoScopeStopFamilyIdsAtMaxLevel = new HashSet<int>();
                return;
            }

            var scopeRoot = scope.RootFamily ?? fileRoot;
            int minBranch = scope.MinBranchForStopLevel;
            if (minBranch <= 0 && fileRoot != null)
            {
                minBranch = ComputeAdaptiveMinBranchToSplitDeep(CountFamiliesInTree(fileRoot));
            }

            minBranch = Math.Max(1, minBranch);
            int effectiveMax = _phaDoAnalyzeMaxFamilyLevel > 0
                ? _phaDoAnalyzeMaxFamilyLevel
                : Math.Max(GetMaxFamilyLevelInTree(fileRoot), scope.MaxGenerationInclusive);

            PopulatePhaConFlagsAtStopLevel(
                scopeRoot,
                scope.MaxGenerationInclusive,
                minBranch,
                effectiveMax);
            _phaDoScopeStopFamilyIdsAtMaxLevel = new HashSet<int>(_phaConFamilyIds);

            // Thu thập non-STOP IDs đã được gom vào combo đa gốc — cần dừng vẽ ở split level trong scope cha.
            _phaConNonStopComboFamilyIds.Clear();
            if (_phaDoRenderScopes != null)
            {
                foreach (var s in _phaDoRenderScopes)
                {
                    if (s?.MultiRootFamilyIds == null)
                    {
                        continue;
                    }

                    foreach (int cid in s.MultiRootFamilyIds)
                    {
                        _phaConNonStopComboFamilyIds.Add(cid);
                    }
                }
            }
        }

        /// <summary>Chưa phân tích phả: combo chỉ có đúng một mục Toàn phả.</summary>
        private bool IsPreAnalyzeOnlyScopeList()
        {
            if (_phaDoRenderScopesFromAnalyze || _phaDoRenderScopes.Count != 1)
            {
                return false;
            }

            var only = _phaDoRenderScopes[0];
            return only != null && only.IsWholeTree && !only.IsDefaultRoot0WithoutAnalyze;
        }

        /// <summary>Khởi tạo / đổi file: chưa phân tích thì combo chỉ có Toàn phả (không thêm Root0).</summary>
        private void ResetPhaDoRenderScopes(FamilyViewModel root)
        {
            _phaDoRenderScopes.Clear();
            _phaDoRenderScopesFromAnalyze = false;
            int newRootId = root?.familyInfo?.FamilyId ?? 0;
            if (_phaDoRenderScopeSourceRootId != newRootId)
            {
                _phaDoFullTreeLayoutSnapshot = null;
                _phaDoFullTreeLayoutSnapshotRootId = 0;
            }
            _phaDoRenderScopeSourceRootId = newRootId;
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
                UpdatePhaDoSubtreeListBoxToolTip();
            }
        }

        /// <summary>Đổ list lựa chọn vẽ theo cấp root: Toàn phả, Root0 và các nhánh Root1/Root2...</summary>
        private void UpdatePhaDoRenderScopesFromMap(
            FamilyViewModel root,
            PhaDoSubtreeMap map,
            int splitLevel,
            int subtreeMaxGeneration,
            int minBranchToSplitDeep,
            int rootLevelMax = PhaDoDefaultRoot0MaxGeneration)
        {
            // Sau phân tích: Toàn phả + Root0 (theo splitLevel) + từng nhánh — không dùng Reset mặc định đời 1–4.
            _phaDoRenderScopes.Clear();
            _phaDoRenderScopesFromAnalyze = false;
            _phaDoAnalyzeMaxFamilyLevel = Math.Max(1, subtreeMaxGeneration);
            _phaDoRenderScopeSourceRootId = root?.familyInfo?.FamilyId ?? 0;
            if (root == null)
            {
                return;
            }

            int comboIndex = 0;
            _phaDoRenderScopes.Add(new PhaDoRenderScopeItem
            {
                Label = "Toàn phả",
                FamilyId = _phaDoRenderScopeSourceRootId,
                RootFamily = root,
                IsWholeTree = true,
                MaxGenerationInclusive = int.MaxValue,
                RenderPlanSummary = "Toàn phả",
                ComboIndexHint = comboIndex++
            });

            if (splitLevel <= 0 || !HasMeaningfulPhaiConBranches(map))
            {
                // Không tách / chỉ 1 nhánh → coi như không có phả con, chỉ Toàn phả trong combo.
                _phaDoRenderScopesFromAnalyze = true;
                if (phaDoSubtreeListBox != null)
                {
                    phaDoSubtreeListBox.SelectedIndex = 0;
                    UpdatePhaDoSubtreeListBoxToolTip();
                }

                return;
            }

            // Scope Root0: vẽ từ root gia phả tới toàn bộ Root1.
            var root0Scope = new PhaDoRenderScopeItem
            {
                RenderPlanSummary = "Root0→Root1 (đời 1–" + splitLevel + ")",
                ComboIndexHint = comboIndex++,
                FamilyId = root.familyInfo?.FamilyId ?? 0,
                RootFamily = root,
                IsWholeTree = false,
                MaxGenerationInclusive = splitLevel,
                HighlightStartLevel = splitLevel,
                HighlightStartRootIndex = 1,
                HighlightStartLabel = "Khởi đầu phả con Root1",
                // Root1 nhỏ hơn ngưỡng tách thì vẽ tiếp tới hết nhánh (không cắt ngang tại Root1).
                ExpandSmallBranchesAtStopLevel = true,
                MinBranchForStopLevel = minBranchToSplitDeep,
                StopFamilyIdsAtMaxLevel = new HashSet<int>(_phaConFamilyIds)
            };
            root0Scope.LayoutFamilyCountEstimate = EstimateScopeLayoutFamilyCount(root0Scope, root);
            root0Scope.Label = FormatPhaiConScopeLabel(root0Scope, map.RootBlock);
            _phaDoRenderScopes.Add(root0Scope);

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
                if (level < splitLevel)
                {
                    continue;
                }

                int maxGenerationInclusive = int.MaxValue;
                int highlightStartLevel = 0;
                bool hasNextSplit = false;
                int nextSplitLevel = 0;
                // Root1→Root2: chỉ nhánh Root1 (đời splitLevel) hoặc nhánh báo cáo “đủ đoạn tách tiếp”.
                // Nhánh cấp 2 (vd. đời 12, 0 tách tiếp) → vẽ full nhánh, không gọi TrySelect (tránh nhảy đời 13+).
                if (level == splitLevel || splitMetrics.CanContinueSplit(family))
                {
                    hasNextSplit = splitMetrics.TrySelectNextSplitLevel(
                        family,
                        out nextSplitLevel,
                        out _,
                        out _);
                }

                if (hasNextSplit)
                {
                    maxGenerationInclusive = nextSplitLevel;
                    highlightStartLevel = nextSplitLevel;
                }

                int rootIndex = Math.Max(1, level - splitLevel + 1);
                HashSet<int> stopIds = hasNextSplit
                    ? BuildStopFamilyIdsAtLevel(family, nextSplitLevel, minBranchToSplitDeep, subtreeMaxGeneration)
                    : null;
                string planSummary = hasNextSplit
                    ? ("Root" + rootIndex + "→Root" + (rootIndex + 1)
                        + " (đời ≤" + nextSplitLevel + ", STOP≥" + minBranchToSplitDeep + ")")
                    : ("Phả con: " + level + " (đủ nhánh)");
                var branchScope = new PhaDoRenderScopeItem
                {
                    RenderPlanSummary = planSummary,
                    ComboIndexHint = comboIndex++,
                    FamilyId = familyId,
                    RootFamily = family,
                    IsWholeTree = false,
                    MaxGenerationInclusive = maxGenerationInclusive,
                    HighlightStartLevel = highlightStartLevel,
                    HighlightStartRootIndex = rootIndex + 1,
                    HighlightStartLabel = "Khởi đầu phả con Root" + (rootIndex + 1),
                    // Cùng luồng Root0→Root1: nhánh nhỏ tại mốc kế vẽ tiếp, nhánh lớn dừng để tách Root kế.
                    ExpandSmallBranchesAtStopLevel = hasNextSplit,
                    MinBranchForStopLevel = minBranchToSplitDeep,
                    StopFamilyIdsAtMaxLevel = stopIds
                };
                branchScope.LayoutFamilyCountEstimate = EstimateScopeLayoutFamilyCount(branchScope, root);
                branchScope.Label = FormatPhaiConScopeLabel(branchScope, block);
                _phaDoRenderScopes.Add(branchScope);

                // Gom nhánh non-STOP của branchScope thành các combo multi-root xếp dọc.
                // Gọi kể cả khi không có STOP nào — toàn bộ nhánh đều non-STOP vẫn phải tạo ít nhất 1 combo.
                if (branchScope.ExpandSmallBranchesAtStopLevel
                    && branchScope.StopFamilyIdsAtMaxLevel != null)
                {
                    AppendMultiRootNonStopCombos(
                        ref comboIndex,
                        branchScope,
                        root,
                        minBranchToSplitDeep);
                }
            }

            // Xây dữ liệu bản đồ: tách biệt root1 stops và root2+ stops.
            // root1StopIds = STOP tại splitLevel → nhánh lớn, bản đồ sẽ tiếp tục xuống root2 cho chúng.
            var mapRoot1StopIds = new HashSet<int>(root0Scope.StopFamilyIdsAtMaxLevel ?? new HashSet<int>());
            // deepStopIds = STOP tại root2+ (từ mỗi branchScope) → dừng hẳn trong bản đồ.
            var mapDeepStopIds = new HashSet<int>();
            int mapMaxLevel = splitLevel; // ít nhất là root1 level
            foreach (var s in _phaDoRenderScopes)
            {
                if (s == null || s.IsWholeTree || s.IsPhaConMap || s.IsMultiRootVerticalStack)
                {
                    continue;
                }

                // Chỉ lấy stop IDs từ scopes ở CẤP SAU root1 (branchScope có FamilyId khác root).
                if (s.FamilyId != (_phaDoRenderScopeSourceRootId)
                    && s.StopFamilyIdsAtMaxLevel != null)
                {
                    foreach (int id in s.StopFamilyIdsAtMaxLevel)
                    {
                        mapDeepStopIds.Add(id);
                    }
                }

                if (s.MaxGenerationInclusive > 0 && s.MaxGenerationInclusive != int.MaxValue
                    && s.MaxGenerationInclusive > mapMaxLevel)
                {
                    mapMaxLevel = s.MaxGenerationInclusive;
                }
            }

            if (mapMaxLevel > 0)
            {
                // Estimate: tổng GD map ≥ GD Root0 scope (thêm cả cây con root2 cho STOP root1).
                int mapGdEst = root0Scope.LayoutFamilyCountEstimate > 0
                    ? root0Scope.LayoutFamilyCountEstimate
                    : EstimateScopeLayoutFamilyCount(root0Scope, root);
                var mapScope = new PhaDoRenderScopeItem
                {
                    RenderPlanSummary = "Bản đồ phả con (Root0→Root1→Root2)",
                    Label = "Bản đồ phả con | Đời 1–" + mapMaxLevel
                            + " | ~" + mapGdEst + " GD",
                    FamilyId = root.familyInfo?.FamilyId ?? 0,
                    RootFamily = root,
                    IsWholeTree = false,
                    MaxGenerationInclusive = mapMaxLevel,
                    IsPhaConMap = true,
                    PhaConMapRoot1SplitLevel = splitLevel,
                    PhaConMapRoot1StopIds = mapRoot1StopIds,
                    PhaConMapDeepStopIds = mapDeepStopIds,
                    LayoutFamilyCountEstimate = mapGdEst,
                    ComboIndexHint = -1
                };

                // Chèn ngay sau "Toàn phả" (index 0).
                _phaDoRenderScopes.Insert(1, mapScope);

                // Cập nhật lại ComboIndexHint cho tất cả scopes sau khi chèn.
                for (int i = 0; i < _phaDoRenderScopes.Count; i++)
                {
                    if (_phaDoRenderScopes[i] != null)
                    {
                        _phaDoRenderScopes[i].ComboIndexHint = i;
                    }
                }
            }

            _phaDoRenderScopesFromAnalyze = true;
            if (phaDoSubtreeListBox != null)
            {
                // Sau phân tích: mặc định chọn Bản đồ (index 1) hoặc Root0 (index 2).
                phaDoSubtreeListBox.SelectedIndex = _phaDoRenderScopes.Count > 1 ? 1 : 0;
                UpdatePhaDoSubtreeListBoxToolTip();
            }
        }

        /// <summary>
        /// Tạo các combo multi-root từ nhánh non-STOP của một branchScope có ExpandSmallBranchesAtStopLevel.
        /// Mỗi combo chứa nhiều nhánh gộp cho tới khi tổng GD ≥ threshold.
        /// </summary>
        private void AppendMultiRootNonStopCombos(
            ref int comboIndex,
            PhaDoRenderScopeItem parentScope,
            FamilyViewModel fileRoot,
            int threshold)
        {
            if (parentScope?.RootFamily == null || parentScope.StopFamilyIdsAtMaxLevel == null)
            {
                return;
            }

            int stopLevel = parentScope.MaxGenerationInclusive;
            if (stopLevel <= 0 || stopLevel == int.MaxValue)
            {
                return;
            }

            var stopIds = parentScope.StopFamilyIdsAtMaxLevel;
            var rootsAtStop = CollectRootsAtLevelFromBase(
                new[] { parentScope.RootFamily },
                stopLevel);

            // Chỉ lấy nhánh non-STOP, giữ thứ tự cây (tree-order từ CollectRootsAtLevelFromBase).
            var nonStopBranches = rootsAtStop
                .Where(r => r != null && (r.familyInfo?.FamilyId ?? 0) > 0
                            && !stopIds.Contains(r.familyInfo.FamilyId))
                .ToList();

            nonStopBranches.Reverse(); // Đảo ngược để ưu tiên nhóm nhánh cuối (thường có nhiều GD hơn) khi gộp theo threshold.

            if (nonStopBranches.Count == 0)
            {
                return;
            }

            var groups = GroupNonStopBranchesByThreshold(nonStopBranches, threshold);
            if (groups.Count == 0)
            {
                return;
            }

            int totalGroups = groups.Count;
            for (int g = 0; g < groups.Count; g++)
            {
                var groupBranches = groups[g];
                if (groupBranches.Count == 0)
                {
                    continue;
                }

                var firstBranch = groupBranches[0];
                int firstId = firstBranch?.familyInfo?.FamilyId ?? 0;
                var rootIds = groupBranches
                    .Select(b => b?.familyInfo?.FamilyId ?? 0)
                    .Where(id => id > 0)
                    .ToList();

                int gdSum = groupBranches.Sum(CountSubtreeFamilies);
                int groupNum = g + 1;

                // Tóm tắt tên nhánh để RenderPlanSummary hiển thị ngắn gọn trong report.
                var summaryNames = groupBranches
                    .Take(3)
                    .Select(GetFamilyMainPersonName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();
                string namesSuffix = summaryNames.Count > 0
                    ? " | " + string.Join(", ", summaryNames) + (groupBranches.Count > 3 ? "…" : "")
                    : "";
                string planSummary = "Non-STOP [" + groupNum + "/" + totalGroups + "]"
                    + namesSuffix;

                // Tạo nhãn chi tiết từng nhánh: "ID X | Tên người | N GD" để dùng trong report.
                var branchLabels = groupBranches
                    .Select(b =>
                    {
                        int bid = b?.familyInfo?.FamilyId ?? 0;
                        string bname = GetFamilyMainPersonName(b);
                        if (string.IsNullOrWhiteSpace(bname))
                        {
                            bname = b?.familyInfo?.Name0 ?? b?.familyInfo?.Name ?? ("GĐ " + bid);
                        }

                        int bgd = CountSubtreeFamilies(b);
                        return "ID " + bid + " | " + bname + " | " + bgd + " GD";
                    })
                    .ToList();

                var multiScope = new PhaDoRenderScopeItem
                {
                    RenderPlanSummary = planSummary,
                    ComboIndexHint = comboIndex++,
                    FamilyId = firstId,
                    RootFamily = firstBranch,
                    IsWholeTree = false,
                    MaxGenerationInclusive = int.MaxValue,
                    ExpandSmallBranchesAtStopLevel = false,
                    MultiRootFamilyIds = rootIds,
                    MultiRootGroupIndex = groupNum,
                    MultiRootGroupTotal = totalGroups,
                    LayoutFamilyCountEstimate = gdSum,
                    MultiRootBranchLabels = branchLabels
                };
                multiScope.Label = FormatMultiRootNonStopLabel(multiScope, stopLevel, groupBranches);
                _phaDoRenderScopes.Add(multiScope);
            }
        }

        private static string FormatMultiRootNonStopLabel(
            PhaDoRenderScopeItem scope,
            int stopLevel,
            List<FamilyViewModel> branches = null)
        {
            int branchCount = scope.MultiRootFamilyIds?.Count ?? 1;

            // Gộp tên người chính từng nhánh; hiển thị tối đa 4 cái, thêm "…" nếu còn dư.
            string namespart = "";
            if (branches != null && branches.Count > 0)
            {
                const int maxShow = 4;
                var names = branches
                    .Take(maxShow)
                    .Select(GetFamilyMainPersonName)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();
                if (names.Count > 0)
                {
                    string joined = string.Join(" · ", names);
                    namespart = " | " + joined + (branches.Count > maxShow ? " …" : "");
                }
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "Phả con [{0}/{1}] | Đời {2}{3} | ~{4} GD (gộp {5} nhánh)",
                scope.MultiRootGroupIndex,
                scope.MultiRootGroupTotal,
                stopLevel,
                namespart,
                scope.LayoutFamilyCountEstimate,
                branchCount);
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
            // Pan mode: chuột trái kéo canvas, không select/drag box
            if (_phaDoInteractionMode == PhaDoInteractionMode.Pan)
            {
                BeginPhaDoPan(e, MouseButton.Left);
                e.Handled = true;
                return;
            }

            // Click vào nhãn "Đời X" → mở popup chỉnh style
            if (TryResolveGenLabelHit(e.OriginalSource as DependencyObject, out int genLevel))
            {
                ClearTitleSelection();
                ClearPhaDoBoxSelections();
                _phaDoSelectedGenLevel = genLevel;
                DrawGenLabelSelectionOverlay(genLevel);
                OpenGenLabelStylePopup(genLevel);
                e.Handled = true;
                return;
            }

            // Kiểm tra resize handle của title block trước
            if (TryResolveTitleResizeHandle(e.OriginalSource as DependencyObject, out PhaDoResizeCorner titleCorner))
            {
                BeginTitleResize(e, titleCorner);
                return;
            }

            // Title: 2 cấp — (1) chọn khối, (2) khi khối đã chọn mới chọn/kéo text
            if (TryResolveTitleTextLineHit(
                    e.OriginalSource as DependencyObject,
                    out int titleLineIdx,
                    out FrameworkElement titleLineElement))
            {
                if (_phaDoTitleSelected)
                {
                    HandleTitleTextMouseDown(e, titleLineIdx, titleLineElement);
                }
                else
                {
                    // Click chữ lần đầu = chọn khối (cấp 1), không nhảy thẳng sang text
                    SelectTitleBlockOutline();
                    e.Handled = true;
                }

                return;
            }

            // Khối title đã chọn: kéo nền/khung để di chuyển; lần đầu click = chọn khối
            if (TryResolveTitleBlockDragHit(e.OriginalSource as DependencyObject))
            {
                if (!_phaDoTitleSelected || _phaDoTitleSelectedLine >= 0)
                {
                    SelectTitleBlockOutline();
                }

                BeginTitleBlockDrag(e);
                return;
            }

            if (TryResolveResizeHandle(e.OriginalSource as DependencyObject, out int resizeFamilyId, out PhaDoResizeCorner corner))
            {
                BeginPhaDoResize(e, resizeFamilyId, corner);
                e.Handled = true;
                return;
            }

            // Text trong ô — tách hẳn khỏi luồng chọn box
            if (TryResolvePersonElementHit(
                    e.OriginalSource as DependencyObject,
                    out int personFamilyId,
                    out int personSlot,
                    out FrameworkElement personElement))
            {
                HandleFamilyBoxTextMouseDown(e, personFamilyId, personSlot, personElement);
                return;
            }

            if (!TryResolveFamilyBoxBackgroundHit(e.OriginalSource as DependencyObject, out int familyId, out GiaPhaPlacedNode node))
            {
                ClearTitleSelection();
                ClearGenLabelSelection();
                ClearPhaDoBoxSelections();
                BeginMarqueeSelection(e.GetPosition(theCanvas));
                e.Handled = true;
                return;
            }

            HandleFamilyBoxBackgroundMouseDown(e, familyId, node);
        }

        /// <summary>Khởi tạo quét chuột để chọn nhiều box.</summary>
        private void BeginMarqueeSelection(Point canvasPoint)
        {
            _phaDoIsMarqueeSelecting = true;
            _phaDoMarqueeStartPoint = canvasPoint;
            if (_phaDoMarqueeRect == null)
            {
                _phaDoMarqueeRect = new Rectangle
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(30, 136, 229)),
                    Fill = new SolidColorBrush(Color.FromArgb(40, 30, 136, 229)),
                    StrokeDashArray = new DoubleCollection { 3, 2 },
                    StrokeThickness = 1.2,
                    IsHitTestVisible = false,
                    Tag = "__PhaDoMarqueeSelect"
                };
            }
            Canvas.SetLeft(_phaDoMarqueeRect, canvasPoint.X);
            Canvas.SetTop(_phaDoMarqueeRect, canvasPoint.Y);
            _phaDoMarqueeRect.Width = 0;
            _phaDoMarqueeRect.Height = 0;
            if (!theCanvas.Children.Contains(_phaDoMarqueeRect))
            {
                Panel.SetZIndex(_phaDoMarqueeRect, 1200);
                theCanvas.Children.Add(_phaDoMarqueeRect);
            }
            theCanvas.CaptureMouse();
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
            // Giữ cursor SizeAll nếu vẫn ở Pan mode; chỉ reset khi về Select mode
            theCanvas.Cursor = _phaDoInteractionMode == PhaDoInteractionMode.Pan ? Cursors.SizeAll : null;
            if (_phaDoPanMoved)
            {
                SaveWorkspaceSession();
            }
        }

        // ── Title block: select + resize ─────────────────────────────────────

        private const string TitleSelectionTag = "__PhaDoTitleSelection";

        /// <summary>Vẽ border chọn + 4 góc resize — căn theo hit rect trên canvas (giống ô gia đình).</summary>
        private void DrawTitleSelectionOverlay()
        {
            ClearTitleSelectionOverlay();
            if (!TryGetTitleBlockBoundsPx(out double left, out double top, out double boxW, out double boxH))
            {
                return;
            }

            const double pad = 2.5;
            const double handle = 8;
            double outlineLeft = left - pad;
            double outlineTop = top - pad;
            double outlineW = Math.Max(1, boxW + pad * 2);
            double outlineH = Math.Max(1, boxH + pad * 2);

            var border = new System.Windows.Shapes.Rectangle
            {
                Width = outlineW,
                Height = outlineH,
                Stroke = new SolidColorBrush(Color.FromRgb(30, 136, 229)),
                StrokeThickness = 1.3,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = System.Windows.Media.Brushes.Transparent,
                IsHitTestVisible = false,
                Tag = TitleSelectionTag
            };
            Canvas.SetLeft(border, outlineLeft);
            Canvas.SetTop(border, outlineTop);
            Panel.SetZIndex(border, 1000);
            theCanvas.Children.Add(border);

            double x0 = outlineLeft - handle / 2;
            double y0 = outlineTop - handle / 2;
            double x1 = outlineLeft + outlineW - handle / 2;
            double y1 = outlineTop + outlineH - handle / 2;
            AddTitleResizeHandle(x0, y0, handle, PhaDoResizeCorner.TopLeft);
            AddTitleResizeHandle(x1, y0, handle, PhaDoResizeCorner.TopRight);
            AddTitleResizeHandle(x0, y1, handle, PhaDoResizeCorner.BottomLeft);
            AddTitleResizeHandle(x1, y1, handle, PhaDoResizeCorner.BottomRight);
        }

        private void AddTitleResizeHandle(double x, double y, double size, PhaDoResizeCorner corner)
        {
            var r = new System.Windows.Shapes.Rectangle
            {
                Width = size,
                Height = size,
                Fill = System.Windows.Media.Brushes.White,
                Stroke = new SolidColorBrush(Color.FromRgb(30, 136, 229)),
                StrokeThickness = 1.2,
                IsHitTestVisible = true,
                Cursor = GetResizeCursor(corner),
                Tag = new PhaDoTitleResizeHandleTag(corner)
            };
            Canvas.SetLeft(r, x);
            Canvas.SetTop(r, y);
            Panel.SetZIndex(r, 1002);
            theCanvas.Children.Add(r);
        }

        private void ClearTitleSelectionOverlay()
        {
            var toRemove = theCanvas?.Children
                .OfType<UIElement>()
                .Where(e => {
                    var t = (e as FrameworkElement)?.Tag;
                    return t is string s && s == TitleSelectionTag
                        || t is PhaDoTitleResizeHandleTag;
                }).ToList();
            if (toRemove == null) return;
            foreach (var el in toRemove) theCanvas.Children.Remove(el);
        }

        private bool TryResolveTitleResizeHandle(DependencyObject source, out PhaDoResizeCorner corner)
        {
            corner = PhaDoResizeCorner.BottomRight;
            for (var d = source; d != null; d = VisualTreeHelper.GetParent(d))
            {
                if ((d as FrameworkElement)?.Tag is PhaDoTitleResizeHandleTag tag)
                {
                    corner = tag.Corner;
                    return true;
                }
            }
            return false;
        }

        private bool IsTitleHit(DependencyObject source)
        {
            for (var d = source; d != null; d = VisualTreeHelper.GetParent(d))
                if ((d as FrameworkElement)?.Tag is PhaDoTitleHitTag) return true;
            return false;
        }

        /// <summary>Vùng có thể kéo cả khối title: nền trong suốt hoặc khung SVG khi đã chọn box.</summary>
        private bool TryResolveTitleBlockDragHit(DependencyObject source)
        {
            if (source == null)
            {
                return false;
            }

            for (var d = source; d != null; d = VisualTreeHelper.GetParent(d))
            {
                var tag = (d as FrameworkElement)?.Tag;
                if (tag is PhaDoTitleHitTag)
                {
                    return true;
                }

                if (_phaDoTitleSelected
                    && _phaDoTitleSelectedLine < 0
                    && tag is PhaDoTitleVisualTag)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateTitleHitRectCursor()
        {
            var hit = theCanvas?.Children
                .OfType<FrameworkElement>()
                .FirstOrDefault(fe => fe.Tag is PhaDoTitleHitTag);
            if (hit == null)
            {
                return;
            }

            hit.Cursor = _phaDoTitleSelected && _phaDoTitleSelectedLine < 0
                ? Cursors.SizeAll
                : Cursors.Arrow;
        }

        private void BeginTitleBlockDrag(MouseButtonEventArgs e)
        {
            if (_phaDoCurrentOptions == null || _phaDoRenderedLayout == null)
            {
                return;
            }

            CancelPendingTitleDrag();
            CancelPendingPersonDrag();

            var layout = PhaDoTitleBlockMetrics.Measure(_phaDoCurrentOptions, GetPhaDoRenderDpi());
            _phaDoIsDraggingTitleBlock = true;
            _phaDoTitleBlockMovedWhileDrag = false;
            _phaDoTitleBlockDragStartPoint = e.GetPosition(theCanvas);
            _phaDoTitleBlockDragStartLeftMm = layout.LeftMm;
            _phaDoTitleBlockDragStartTopMm = layout.TopMm;

            if (_phaDoTitleStyle == null)
            {
                _phaDoTitleStyle = new PhaDoTitleStyle();
            }

            if (!_phaDoTitleStyle.ManualPositionSet)
            {
                _phaDoTitleStyle.ManualLeftMm = layout.LeftMm;
                _phaDoTitleStyle.ManualTopMm = layout.TopMm;
                _phaDoTitleStyle.ManualPositionSet = true;
            }

            theCanvas.CaptureMouse();
            theCanvas.Cursor = Cursors.SizeAll;
            e.Handled = true;
        }

        private void UpdateTitleBlockDrag(Point canvasPoint)
        {
            if (!_phaDoIsDraggingTitleBlock || _phaDoCurrentOptions == null)
            {
                return;
            }

            var delta = GetPhaDoCanvasDeltaMmRender(canvasPoint, _phaDoTitleBlockDragStartPoint);
            if (Math.Abs(delta.X) > 0.05 || Math.Abs(delta.Y) > 0.05)
            {
                _phaDoTitleBlockMovedWhileDrag = true;
            }

            if (!_phaDoTitleBlockMovedWhileDrag)
            {
                return;
            }

            if (!TryGetTitleBlockBoundsPx(out _, out _, out double boxWPx, out double boxHPx))
            {
                return;
            }

            double maxLeft = Math.Max(0, _phaDoCurrentOptions.PageWidthMm - PxToMmRender(boxWPx));
            double maxTop = Math.Max(0, _phaDoCurrentOptions.PageHeightMm - PxToMmRender(boxHPx));

            double newLeft = _phaDoTitleBlockDragStartLeftMm + delta.X;
            double newTop = _phaDoTitleBlockDragStartTopMm + delta.Y;
            newLeft = Math.Max(0, Math.Min(maxLeft, newLeft));
            newTop = Math.Max(0, Math.Min(maxTop, newTop));
            ApplyTitleBlockPositionMm(newLeft, newTop, redraw: true);
        }

        private void ApplyTitleBlockPositionMm(double leftMm, double topMm, bool redraw)
        {
            if (_phaDoTitleStyle == null)
            {
                _phaDoTitleStyle = new PhaDoTitleStyle();
            }

            _phaDoTitleStyle.ManualPositionSet = true;
            _phaDoTitleStyle.ManualLeftMm = leftMm;
            _phaDoTitleStyle.ManualTopMm = topMm;

            if (_phaDoCurrentOptions != null)
            {
                _phaDoCurrentOptions.ManualTitlePositionSet = true;
                _phaDoCurrentOptions.ManualTitleLeftMm = leftMm;
                _phaDoCurrentOptions.ManualTitleTopMm = topMm;
            }

            if (redraw)
            {
                RedrawTitleBlockOnly();
            }
        }

        private void EndTitleBlockDrag()
        {
            if (!_phaDoIsDraggingTitleBlock)
            {
                return;
            }

            _phaDoIsDraggingTitleBlock = false;
            theCanvas.ReleaseMouseCapture();
            theCanvas.Cursor = _phaDoInteractionMode == PhaDoInteractionMode.Pan
                ? Cursors.SizeAll
                : null;
            UpdateTitleHitRectCursor();

            if (_phaDoTitleBlockMovedWhileDrag)
            {
                SaveWorkspaceSession();
            }
        }

        private void BeginTitleResize(MouseButtonEventArgs e, PhaDoResizeCorner corner)
        {
            if (_phaDoCurrentOptions == null || _phaDoRenderedLayout == null) return;
            var layout = PhaDoTitleBlockMetrics.Measure(_phaDoCurrentOptions, GetPhaDoRenderDpi());

            _phaDoTitleIsResizing = true;
            _phaDoTitleResizeCorner    = corner;
            _phaDoTitleResizeStartPoint  = e.GetPosition(theCanvas);
            _phaDoTitleResizeStartWmm    = layout.WidthMm;
            _phaDoTitleResizeStartHmm    = layout.HeightMm;
            _phaDoTitleResizeStartLeftMm = layout.LeftMm;
            _phaDoTitleResizeStartTopMm  = layout.TopMm;
            theCanvas.CaptureMouse();
            e.Handled = true;
        }

        private void UpdateTitleResize(Point canvasPoint)
        {
            if (!_phaDoTitleIsResizing || _phaDoCurrentOptions == null) return;

            double dxPx = canvasPoint.X - _phaDoTitleResizeStartPoint.X;
            double dyPx = canvasPoint.Y - _phaDoTitleResizeStartPoint.Y;
            double dxMm = PxToMmRender(dxPx);
            double dyMm = PxToMmRender(dyPx);

            double newW = _phaDoTitleResizeStartWmm, newH = _phaDoTitleResizeStartHmm;
            double newLeft = _phaDoTitleResizeStartLeftMm, newTop = _phaDoTitleResizeStartTopMm;
            const double minMm = 10;

            switch (_phaDoTitleResizeCorner)
            {
                case PhaDoResizeCorner.BottomRight:
                    newW = Math.Max(minMm, _phaDoTitleResizeStartWmm + dxMm);
                    newH = Math.Max(minMm, _phaDoTitleResizeStartHmm + dyMm);
                    break;
                case PhaDoResizeCorner.BottomLeft:
                    newW = Math.Max(minMm, _phaDoTitleResizeStartWmm - dxMm);
                    newLeft = _phaDoTitleResizeStartLeftMm + _phaDoTitleResizeStartWmm - newW;
                    newH = Math.Max(minMm, _phaDoTitleResizeStartHmm + dyMm);
                    break;
                case PhaDoResizeCorner.TopRight:
                    newW = Math.Max(minMm, _phaDoTitleResizeStartWmm + dxMm);
                    newH = Math.Max(minMm, _phaDoTitleResizeStartHmm - dyMm);
                    newTop = _phaDoTitleResizeStartTopMm + _phaDoTitleResizeStartHmm - newH;
                    break;
                case PhaDoResizeCorner.TopLeft:
                    newW = Math.Max(minMm, _phaDoTitleResizeStartWmm - dxMm);
                    newH = Math.Max(minMm, _phaDoTitleResizeStartHmm - dyMm);
                    newLeft = _phaDoTitleResizeStartLeftMm + _phaDoTitleResizeStartWmm - newW;
                    newTop  = _phaDoTitleResizeStartTopMm  + _phaDoTitleResizeStartHmm - newH;
                    break;
            }

            // Lưu vào style → options → redraw title inline (không vẽ lại toàn bộ)
            _phaDoTitleStyle.ManualWidthMm  = newW;
            _phaDoTitleStyle.ManualHeightMm = newH;
            _phaDoTitleStyle.ManualLeftMm   = newLeft;
            _phaDoTitleStyle.ManualTopMm    = newTop;
            _phaDoTitleStyle.ManualPositionSet = true;
            _phaDoCurrentOptions.ManualTitleWidthMm  = newW;
            _phaDoCurrentOptions.ManualTitleHeightMm = newH;
            _phaDoCurrentOptions.ManualTitleLeftMm   = newLeft;
            _phaDoCurrentOptions.ManualTitleTopMm    = newTop;
            _phaDoCurrentOptions.ManualTitlePositionSet = true;

            RedrawTitleBlockOnly();
            DrawTitleSelectionOverlay();
        }

        private void EndTitleResize()
        {
            if (!_phaDoTitleIsResizing) return;
            _phaDoTitleIsResizing = false;
            theCanvas.ReleaseMouseCapture();
            SaveWorkspaceSession();
        }

        /// <summary>Xóa và vẽ lại chỉ phần title block — không ảnh hưởng card/connector.</summary>
        private void RedrawTitleBlockOnly()
        {
            if (theCanvas == null || _phaDoCurrentOptions == null || _phaDoRenderedLayout == null) return;

            // Xóa toàn bộ visual cũ của title (khung + text line + hit rect) để tránh vẽ chồng gây nhòe chữ.
            var old = theCanvas.Children.OfType<FrameworkElement>()
                .Where(e => e.Tag is PhaDoTitleVisualTag
                         || e.Tag is PhaDoTitleHitTag
                         || e.Tag is GiaPhaRender.PhaDoTitleTextLineTag)
                .ToList();
            foreach (var el in old) theCanvas.Children.Remove(el);

            GiaPhaTitleBlockRenderer.DrawToCanvas(theCanvas, _phaDoCurrentOptions, _phaDoRenderedLayout.Dpi, theCanvas.Width);
            ApplyTitleLineOffsets();

            if (_phaDoTitleSelectedLine >= 0)
            {
                DrawTitleLineHighlight(_phaDoTitleSelectedLine);
            }
            else if (_phaDoTitleSelected)
            {
                DrawTitleSelectionOverlay();
            }

            UpdateTitleHitRectCursor();
        }

        // ── Mode toolbox: Select / Pan ────────────────────────────────────────

        private void PhaDoSelectModeBtn_Click(object sender, RoutedEventArgs e)
            => SetPhaDoInteractionMode(PhaDoInteractionMode.Select);

        private void PhaDoPanModeBtn_Click(object sender, RoutedEventArgs e)
            => SetPhaDoInteractionMode(PhaDoInteractionMode.Pan);

        /// <summary>Chuyển mode tương tác và đồng bộ trạng thái 2 toggle button.</summary>
        private void SetPhaDoInteractionMode(PhaDoInteractionMode mode)
        {
            _phaDoInteractionMode = mode;

            if (phaDoSelectModeBtn != null)
                phaDoSelectModeBtn.IsChecked = mode == PhaDoInteractionMode.Select;
            if (phaDoPanModeBtn != null)
                phaDoPanModeBtn.IsChecked = mode == PhaDoInteractionMode.Pan;

            // Cập nhật con trỏ vùng canvas theo mode đang chọn
            if (theCanvas != null)
                theCanvas.Cursor = mode == PhaDoInteractionMode.Pan ? Cursors.SizeAll : null;
        }

        private void TheCanvas_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_phaDoTitleIsResizing)
            {
                UpdateTitleResize(e.GetPosition(theCanvas));
                e.Handled = true;
                return;
            }

            if (_phaDoIsDraggingTitleBlock)
            {
                UpdateTitleBlockDrag(e.GetPosition(theCanvas));
                e.Handled = true;
                return;
            }

            if (_phaDoIsMarqueeSelecting)
            {
                var pos2 = e.GetPosition(theCanvas);
                double left = Math.Min(_phaDoMarqueeStartPoint.X, pos2.X);
                double top = Math.Min(_phaDoMarqueeStartPoint.Y, pos2.Y);
                double width = Math.Abs(pos2.X - _phaDoMarqueeStartPoint.X);
                double height = Math.Abs(pos2.Y - _phaDoMarqueeStartPoint.Y);
                if (_phaDoMarqueeRect != null)
                {
                    Canvas.SetLeft(_phaDoMarqueeRect, left);
                    Canvas.SetTop(_phaDoMarqueeRect, top);
                    _phaDoMarqueeRect.Width = width;
                    _phaDoMarqueeRect.Height = height;
                }
                e.Handled = true;
                return;
            }

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

            var canvasPoint = e.GetPosition(theCanvas);
            TryStartPendingTitleDrag(canvasPoint);
            TryStartPendingPersonDrag(canvasPoint);

            if (_phaDoIsDraggingTitleLine)
            {
                UpdateTitleLineDrag(canvasPoint);
                e.Handled = true;
                return;
            }

            if (_phaDoIsDraggingPerson)
            {
                UpdatePersonDrag(canvasPoint);
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
            if (_phaDoDragStartXmmByFamilyId.Count > 0)
            {
                double deltaMm = newXmm - _phaDoDragStartNodeXmm;
                foreach (var kv in _phaDoDragStartXmmByFamilyId)
                {
                    ApplyDraggedFamilyX(kv.Key, kv.Value + deltaMm);
                }
            }
            else
            {
                ApplyDraggedFamilyX(_phaDoDraggingFamilyId, newXmm);
            }
            e.Handled = true;
        }

        private void TheCanvas_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_phaDoTitleIsResizing)
            {
                EndTitleResize();
                e.Handled = true;
                return;
            }

            if (_phaDoIsDraggingTitleBlock)
            {
                EndTitleBlockDrag();
                e.Handled = true;
                return;
            }

            if (_phaDoIsMarqueeSelecting)
            {
                _phaDoIsMarqueeSelecting = false;
                if (_phaDoMarqueeRect != null)
                {
                    double left = Canvas.GetLeft(_phaDoMarqueeRect);
                    double top = Canvas.GetTop(_phaDoMarqueeRect);
                    double width = _phaDoMarqueeRect.Width;
                    double height = _phaDoMarqueeRect.Height;
                    var area = new Rect(left, top, Math.Max(0, width), Math.Max(0, height));

                    _phaDoMultiSelectedFamilyIds.Clear();
                    if (_phaDoRenderedLayout?.Nodes != null && area.Width > 2 && area.Height > 2)
                    {
                        foreach (var n in _phaDoRenderedLayout.Nodes)
                        {
                            int id = n?.Family?.familyInfo?.FamilyId ?? 0;
                            if (id <= 0)
                            {
                                continue;
                            }
                            if (!TryGetFamilyBackgroundBounds(id, out double bx, out double by, out double bw, out double bh))
                            {
                                continue;
                            }
                            var box = new Rect(bx, by, Math.Max(0, bw), Math.Max(0, bh));
                            if (area.IntersectsWith(box))
                            {
                                _phaDoMultiSelectedFamilyIds.Add(id);
                            }
                        }
                    }

                    if (_phaDoMultiSelectedFamilyIds.Count > 0)
                    {
                        _phaDoSelectedFamilyId = _phaDoMultiSelectedFamilyIds.First();
                        DrawSelectionOverlay(_phaDoSelectedFamilyId);
                        DrawMultiSelectionOverlays();
                        DrawDirectChildHighlights(_phaDoSelectedFamilyId);
                        UpdatePhaDoSelectedBoxSizeStatus(_phaDoSelectedFamilyId);
                    }
                    else
                    {
                        _phaDoSelectedFamilyId = 0;
                        _phaDoSelectedPersonSlot = null;
                        ClearSelectionOverlay();
                        DrawMultiSelectionOverlays();
                        UpdatePhaDoSelectedBoxSizeStatus(0);
                    }

                    theCanvas.Children.Remove(_phaDoMarqueeRect);
                }
                theCanvas.ReleaseMouseCapture();
                e.Handled = true;
                return;
            }

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

            if (_phaDoIsDraggingTitleLine)
            {
                EndTitleLineDrag();
                e.Handled = true;
                return;
            }

            if (_phaDoIsDraggingPerson)
            {
                EndPersonDrag();
                e.Handled = true;
                return;
            }

            CancelPendingTitleDrag();
            CancelPendingPersonDrag();

            if (!_phaDoIsDragging)
            {
                return;
            }

            // Chốt vị trí cuối tại thời điểm nhả chuột.
            if (_phaDoDraggingFamilyId > 0 && _phaDoCurrentOptions != null && _phaDoRenderedLayout != null)
            {
                var pos = e.GetPosition(theCanvas);
                double newXmm = _phaDoDragStartNodeXmm + GetPhaDoDragDeltaMm(pos);
                if (_phaDoDragStartXmmByFamilyId.Count > 0)
                {
                    double deltaMm = newXmm - _phaDoDragStartNodeXmm;
                    foreach (var kv in _phaDoDragStartXmmByFamilyId)
                    {
                        ApplyDraggedFamilyX(kv.Key, kv.Value + deltaMm);
                    }
                }
                else
                {
                    ApplyDraggedFamilyX(_phaDoDraggingFamilyId, newXmm);
                }
            }

            _phaDoIsDragging = false;
            _phaDoDraggingFamilyId = 0;
            _phaDoDragStartXmmByFamilyId.Clear();
            theCanvas.ReleaseMouseCapture();
            DrawSelectionOverlay(_phaDoSelectedFamilyId);
            DrawMultiSelectionOverlays();
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
                options.GetFamilyBoxNotes = BuildFamilyBoxExtraNotes;

                string outPath = saveDialog.FileName;
                GiaPhaRenderResult result = _phaDoRenderedLayout;
                if (result == null)
                {
                    var baseResult = await GiaPhaRenderService.ComputeLayoutAsync(root, options).ConfigureAwait(true);
                    result = GiaPhaManualLayoutService.ApplyManualOffsets(
                        baseResult,
                        _phaDoOffsetXmmByFamilyId,
                        _phaDoOffsetYmmByFamilyId);
                    ApplyCustomBoxSizesFromStyles(result);
                }
                else
                {
                    // Đồng bộ options (title 4 dòng, font…) với màn hình đang chỉnh
                    result.Options = options;
                }

                PopulateTitleAutoLines(result);

                await Task.Run(() => GiaPhaRenderService.ExportResultToSvg(
                    outPath,
                    result,
                    GetBoxStyleForFamily,
                    _phaDoTitleStyle)).ConfigureAwait(true);

                int exportedNodes = result.Nodes?.Count ?? 0;
                string simplifyNote = exportedNodes >= GiaPhaSvgExportService.SimpleBoxExportNodeThreshold
                    ? "\n\n(Lưu ý: phả lớn — khung ô xuất dạng rect đơn giản để tránh hết bộ nhớ; chữ và layout giữ nguyên.)"
                    : "";
                viewModel.AddUserAction("Xuất SVG phả đồ: " + outPath + " (" + result.SizeSummary + ")");
                MessageBox.Show(
                    "Đã xuất phả đồ ra SVG:\n" + outPath
                    + "\n\nMở bằng trình duyệt, Inkscape hoặc Illustrator để xem/in."
                    + simplifyNote,
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

        /// <summary>Double-click vùng nền toolbox — Border không có PreviewMouseDoubleClick, dùng ClickCount.</summary>
        private void PhaDoToolboxPanel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2)
            {
                return;
            }

            if (!IsPhaDoToolboxChromeDoubleClick(e.OriginalSource as DependencyObject))
            {
                return;
            }

            SetPhaDoToolboxExpanded(!_phaDoToolboxExpanded);
            e.Handled = true;
        }

        /// <summary>Chỉ toggle khi double-click nền thanh toolbox, không phải lên nút/combo.</summary>
        private bool IsPhaDoToolboxChromeDoubleClick(DependencyObject source)
        {
            if (source == null || phaDoToolboxPanel == null)
            {
                return false;
            }

            var current = source;
            while (current != null && !ReferenceEquals(current, phaDoToolboxPanel))
            {
                // ButtonBase gồm Button, RadioButton, ToggleButton (Primitives)
                if (current is ButtonBase || current is ComboBox)
                {
                    return false;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return ReferenceEquals(current, phaDoToolboxPanel);
        }

        private void PhaDoToolboxToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            SetPhaDoToolboxExpanded(!_phaDoToolboxExpanded);
        }

        /// <summary>Mở thẳng dialog cài đặt AI — dùng từ menu AI → Cài đặt AI.</summary>
        private void AiMenuSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AI.AiSettingsDialog(_aiService) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.ResultSettings != null)
            {
                _aiService.ApplySettings(dlg.ResultSettings);
                dlg.ResultSettings.Save();

                // Nếu chat đang mở, thông báo chế độ đã đổi
                if (_aiChatDialog != null && _aiChatDialog.IsVisible)
                {
                    _aiChatDialog.UpdateModeIndicator();
                }
            }
        }

        private void AiSettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            // Chỉ hiện settings khi chưa cấu hình gì cả (không có cả key lẫn chế độ nội bộ)
            bool hasAnyConfig = _aiService.IsConfigured
                               || AI.AiBackendModes.IsLocalLlama(_aiService.Settings?.BackendMode)
                               || (_aiService.Settings?.UseLocalRuleEngine == true)
                               || (_aiService.Settings?.IsEnabled == true);

            if (!hasAnyConfig)
            {
                // Lần đầu dùng → mở settings để người dùng chọn chế độ
                var settingsDlg = new AI.AiSettingsDialog(_aiService) { Owner = this };
                if (settingsDlg.ShowDialog() != true)
                {
                    return;
                }
            }

            var fileRoot = viewModel?.FamilyTree?.Family?.RootPerson;
            var selected = viewModel?.FamilyTree?.Family?.SelectedFamily;

            // Build index lazy — phòng khi path load nào đó chưa gọi RebuildAiQueryIndex
            if (!_aiQueryEngine.IsReady && fileRoot != null)
            {
                _aiQueryEngine.BuildIndex(fileRoot);
            }

            // Reuse dialog nếu còn mở, cập nhật gia đình đang chọn
            if (_aiChatDialog != null && _aiChatDialog.IsVisible)
            {
                _aiChatDialog.UpdateCurrentFamily(selected);
                _aiChatDialog.Activate();
                return;
            }

            // Tạo mới dialog modeless — truyền rule engine đã build index
            _aiChatDialog = new AI.AiChatDialog(_aiService, _aiQueryEngine, fileRoot, selected) { Owner = this };
            _aiChatDialog.AfterEditApplied += OnAiChatEditApplied;
            _aiChatDialog.Show();
        }

        private void SetPhaDoToolboxExpanded(bool expanded)
        {
            _phaDoToolboxExpanded = expanded;
            if (phaDoToolboxColumn != null)
            {
                phaDoToolboxColumn.Width = new GridLength(
                    expanded ? PhaDoToolboxWidthExpanded : PhaDoToolboxWidthCollapsed);
            }

            if (phaDoToolboxToolsPanel != null)
            {
                phaDoToolboxToolsPanel.HorizontalAlignment = expanded
                    ? HorizontalAlignment.Stretch
                    : HorizontalAlignment.Center;
            }

            if (phaDoToolboxPanel != null)
            {
                phaDoToolboxPanel.ToolTip = expanded
                    ? "Double-click vùng trống để thu gọn (chỉ icon)"
                    : "Double-click vùng trống để mở rộng (icon + chữ)";
            }

            // Cập nhật icon và caption của nút toggle theo trạng thái.
            if (phaDoToolboxToggleIcon != null)
            {
                // E76C = ChevronRight (thu gọn), E76B = ChevronLeft (mở rộng)
                phaDoToolboxToggleIcon.Text = expanded ? "\uE76B" : "\uE76C";
            }

            ApplyPhaDoToolboxCaptionsVisible(expanded);

            // Cập nhật caption nút toggle sau khi ẩn/hiện.
            if (phaDoToolboxToggleBtn != null)
            {
                var caption = phaDoToolboxToggleBtn.Parent is StackPanel sp
                    ? sp.Children.OfType<TextBlock>().FirstOrDefault(t => t.Tag as string == "ToolboxCaption")
                    : null;
                if (caption != null)
                {
                    caption.Text = expanded ? "Thu gọn" : "Mở rộng";
                }
            }
        }

        private void ApplyPhaDoToolboxCaptionsVisible(bool visible)
        {
            if (phaDoToolboxToolsPanel == null)
            {
                return;
            }

            ApplyToolboxCaptionsVisibleRecursive(phaDoToolboxToolsPanel, visible);
        }

        private static void ApplyToolboxCaptionsVisibleRecursive(DependencyObject root, bool visible)
        {
            if (root == null)
            {
                return;
            }

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is TextBlock textBlock
                    && "ToolboxCaption".Equals(textBlock.Tag as string))
                {
                    textBlock.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                }

                ApplyToolboxCaptionsVisibleRecursive(child, visible);
            }
        }

        private void PhaDoFullscreenToggle_Click(object sender, RoutedEventArgs e)
        {
            TogglePhaDoImmersive();
        }

        private void TreePaneFullscreenToggle_Click(object sender, RoutedEventArgs e)
        {
            ToggleTreePaneImmersive();
        }

        private void ToggleTreePaneImmersive()
        {
            SetTreePaneImmersive(!_treePaneImmersive);
        }

        /// <summary>Toàn màn hình pane cây — ẩn tab chính, cây chiếm gần hết cửa sổ.</summary>
        private void SetTreePaneImmersive(bool immersive)
        {
            if (_treePaneImmersive == immersive)
            {
                return;
            }

            if (immersive && _phaDoImmersive)
            {
                SetPhaDoImmersive(false);
            }

            _treePaneImmersive = immersive;

            if (immersive)
            {
                // Lưu cột SplitView trước khi ép layout fullscreen (tránh lưu nhầm Star/0)
                CaptureSplitViewColumnWidthsForTreeFullscreen();

                if (SimpleSplitview != null)
                {
                    _treePaneImmersiveSavedOpenPaneLength = SimpleSplitview.OpenPaneLength;
                    _treePaneImmersiveSavedDisplayMode = SimpleSplitview.DisplayMode;
                    _treePaneImmersiveSavedCompactPaneLength = SimpleSplitview.CompactPaneLength;
                    _treePaneImmersiveSavedMaximumOpenPaneLength = SimpleSplitview.MaximumOpenPaneLength;

                    SimpleSplitview.IsPaneOpen = true;
                    SimpleSplitview.CanResizeOpenPane = false;
                    // Inline: pane = full width, cột content (phải) = 0
                    SimpleSplitview.DisplayMode = SplitViewDisplayMode.Inline;
                    SimpleSplitview.CompactPaneLength = 0;
                    SimpleSplitview.MaximumOpenPaneLength = 20000;
                }

                if (leftPaneLogRowDef != null)
                {
                    _treePaneImmersiveSavedLogRowHeight = leftPaneLogRowDef.Height;
                    leftPaneLogRowDef.Height = new GridLength(0);
                }

                if (mainTabContentGrid != null)
                {
                    mainTabContentGrid.Visibility = Visibility.Collapsed;
                    mainTabContentGrid.Width = 0;
                    mainTabContentGrid.MinWidth = 0;
                }

                if (mainStatusBar != null)
                {
                    mainStatusBar.Visibility = Visibility.Collapsed;
                }

                ApplyTreePaneFullscreenLayout();
            }
            else
            {
                if (mainTabContentGrid != null)
                {
                    mainTabContentGrid.Visibility = Visibility.Visible;
                    mainTabContentGrid.ClearValue(FrameworkElement.WidthProperty);
                    mainTabContentGrid.ClearValue(FrameworkElement.MinWidthProperty);
                }

                RestoreSplitViewContentHostAfterTreeFullscreen();

                if (SimpleSplitview != null)
                {
                    SimpleSplitview.DisplayMode = _treePaneImmersiveSavedDisplayMode;
                    SimpleSplitview.CompactPaneLength = _treePaneImmersiveSavedCompactPaneLength;
                    SimpleSplitview.MaximumOpenPaneLength = _treePaneImmersiveSavedMaximumOpenPaneLength;
                    SimpleSplitview.OpenPaneLength = _treePaneImmersiveSavedOpenPaneLength;
                    SimpleSplitview.CanResizeOpenPane = true;
                    SimpleSplitview.IsPaneOpen = true;
                }

                if (leftPaneLogRowDef != null)
                {
                    leftPaneLogRowDef.Height = _treePaneImmersiveSavedLogRowHeight;
                }

                if (mainStatusBar != null)
                {
                    mainStatusBar.Visibility = Visibility.Visible;
                }

                ApplyTreePaneNormalLayoutAfterExit();
            }

            UpdateTreePaneFullscreenButton();
            treeViewGiaPha?.InvalidateMeasure();
        }

        /// <summary>Lưu độ rộng cột pane/content của template SplitView (một lần mỗi phiên fullscreen).</summary>
        private void CaptureSplitViewColumnWidthsForTreeFullscreen()
        {
            if (_treePaneSplitViewColumnsCaptured)
            {
                return;
            }

            EnsureSplitViewContentHostCached();
            if (_treePaneSplitViewRootGrid == null)
            {
                _treePaneSplitViewRootGrid = FindSplitViewRootGrid();
            }

            if (_treePaneSplitViewRootGrid == null
                || _treePaneSplitViewRootGrid.ColumnDefinitions.Count < 2)
            {
                return;
            }

            _treePaneSplitViewSavedPaneColumnWidth = _treePaneSplitViewRootGrid.ColumnDefinitions[0].Width;
            _treePaneSplitViewSavedContentColumnWidth = _treePaneSplitViewRootGrid.ColumnDefinitions[1].Width;
            _treePaneSplitViewColumnsCaptured = true;
        }

        /// <summary>Sau thoát fullscreen — layout lại để tab phải hiện đủ.</summary>
        private void ApplyTreePaneNormalLayoutAfterExit()
        {
            RestoreSplitViewColumnWidthsAfterTreeFullscreen();
            SimpleSplitview?.InvalidateMeasure();
            mainTabContentGrid?.InvalidateMeasure();
            tabControl?.InvalidateMeasure();

            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (_treePaneImmersive)
                    {
                        return;
                    }

                    RestoreSplitViewColumnWidthsAfterTreeFullscreen();
                    SimpleSplitview?.UpdateLayout();
                    mainTabContentGrid?.UpdateLayout();
                }),
                DispatcherPriority.Loaded);
        }

        /// <summary>Pane cây = full kích thước vùng SplitView (gọi lại sau layout).</summary>
        private void ApplyTreePaneFullscreenLayout()
        {
            if (!_treePaneImmersive)
            {
                return;
            }

            ForceSplitViewTreeFullscreenLayout();
            leftPaneRootGrid?.InvalidateMeasure();
            treeViewGiaPha?.InvalidateMeasure();

            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (!_treePaneImmersive)
                    {
                        return;
                    }

                    ForceSplitViewTreeFullscreenLayout();
                    leftPaneRootGrid?.InvalidateMeasure();
                    treeViewGiaPha?.InvalidateMeasure();
                }),
                DispatcherPriority.Loaded);
        }

        /// <summary>Pane trái = full SplitView; ép cột content (phải) về 0.</summary>
        private void ForceSplitViewTreeFullscreenLayout()
        {
            if (!_treePaneImmersive || SimpleSplitview == null)
            {
                return;
            }

            double hostW = SimpleSplitview.ActualWidth;
            if (hostW < 1)
            {
                hostW = ActualWidth;
            }

            if (hostW > 1)
            {
                SimpleSplitview.OpenPaneLength = hostW;
            }

            EnsureSplitViewContentHostCached();
            if (_treePaneSplitViewContentHost != null)
            {
                _treePaneSplitViewContentHost.Visibility = Visibility.Collapsed;
                _treePaneSplitViewContentHost.Width = 0;
                _treePaneSplitViewContentHost.MinWidth = 0;
                _treePaneSplitViewContentHost.MaxWidth = 0;
            }

            if (_treePaneSplitViewResizeThumb != null)
            {
                _treePaneSplitViewResizeThumb.Visibility = Visibility.Collapsed;
            }

            CaptureSplitViewColumnWidthsForTreeFullscreen();

            if (_treePaneSplitViewRootGrid != null
                && _treePaneSplitViewRootGrid.ColumnDefinitions.Count >= 2)
            {
                _treePaneSplitViewRootGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                _treePaneSplitViewRootGrid.ColumnDefinitions[1].Width = new GridLength(0);
            }
        }

        private void EnsureSplitViewContentHostCached()
        {
            if (_treePaneSplitViewContentHost != null && _treePaneSplitViewResizeThumb != null)
            {
                return;
            }

            SimpleSplitview.ApplyTemplate();
            if (_treePaneSplitViewContentHost == null && mainTabContentGrid?.Parent is FrameworkElement host)
            {
                _treePaneSplitViewContentHost = host;
            }

            if (_treePaneSplitViewResizeThumb == null
                && SimpleSplitview.Template?.FindName("PART_ResizingThumb", SimpleSplitview) is UIElement thumb)
            {
                _treePaneSplitViewResizeThumb = thumb;
                _treePaneSplitViewResizeThumbSavedVisibility = thumb.Visibility;
            }
        }

        private Grid FindSplitViewRootGrid()
        {
            if (SimpleSplitview == null || mainTabContentGrid == null)
            {
                return null;
            }

            DependencyObject el = mainTabContentGrid.Parent;
            while (el != null && !ReferenceEquals(el, SimpleSplitview))
            {
                if (el is Grid grid && grid.ColumnDefinitions.Count >= 2)
                {
                    return grid;
                }

                el = VisualTreeHelper.GetParent(el);
            }

            return FindVisualChild<Grid>(SimpleSplitview) as Grid;
        }

        private void RestoreSplitViewContentHostAfterTreeFullscreen()
        {
            if (_treePaneSplitViewContentHost != null)
            {
                _treePaneSplitViewContentHost.ClearValue(FrameworkElement.WidthProperty);
                _treePaneSplitViewContentHost.ClearValue(FrameworkElement.MinWidthProperty);
                _treePaneSplitViewContentHost.ClearValue(FrameworkElement.MaxWidthProperty);
                _treePaneSplitViewContentHost.Visibility = Visibility.Visible;
            }

            if (_treePaneSplitViewResizeThumb != null)
            {
                _treePaneSplitViewResizeThumb.Visibility = _treePaneSplitViewResizeThumbSavedVisibility;
            }

            RestoreSplitViewColumnWidthsAfterTreeFullscreen();
        }

        /// <summary>Khôi phục cột content SplitView — fallback * nếu đã lưu 0px.</summary>
        private void RestoreSplitViewColumnWidthsAfterTreeFullscreen()
        {
            Grid grid = _treePaneSplitViewRootGrid;
            if (grid == null)
            {
                grid = FindSplitViewRootGrid();
            }

            if (grid == null || grid.ColumnDefinitions.Count < 2)
            {
                _treePaneSplitViewColumnsCaptured = false;
                return;
            }

            GridLength paneCol = _treePaneSplitViewColumnsCaptured
                ? _treePaneSplitViewSavedPaneColumnWidth
                : GridLength.Auto;
            GridLength contentCol = _treePaneSplitViewColumnsCaptured
                ? _treePaneSplitViewSavedContentColumnWidth
                : new GridLength(1, GridUnitType.Star);

            // Sau fullscreen pane thường bị đặt Star — trả Auto để OpenPaneLength điều khiển
            if (paneCol.GridUnitType == GridUnitType.Star)
            {
                paneCol = GridLength.Auto;
            }

            if (contentCol.GridUnitType == GridUnitType.Pixel && contentCol.Value <= 0.1)
            {
                contentCol = new GridLength(1, GridUnitType.Star);
            }

            grid.ColumnDefinitions[0].Width = paneCol;
            grid.ColumnDefinitions[1].Width = contentCol;

            _treePaneSplitViewRootGrid = null;
            _treePaneSplitViewColumnsCaptured = false;
        }

        private void UpdateTreePaneFullscreenButton()
        {
            if (treePaneFullscreenIcon == null || treePaneFullscreenBtn == null)
            {
                return;
            }

            if (_treePaneImmersive)
            {
                treePaneFullscreenIcon.Text = "\uE73F";
                if (treePaneFullscreenLabel != null)
                {
                    treePaneFullscreenLabel.Text = "Thoát";
                }

                treePaneFullscreenBtn.ToolTip = "Thoát toàn màn hình cây (F11 / Esc)";
            }
            else
            {
                treePaneFullscreenIcon.Text = "\uE740";
                if (treePaneFullscreenLabel != null)
                {
                    treePaneFullscreenLabel.Text = "Toàn màn hình";
                }

                treePaneFullscreenBtn.ToolTip = "Toàn màn hình cây (F11)";
            }
        }

        /// <summary>Gắn callback hoàn tác sau khi thay FamilyTree.</summary>
        public void BindGiaPhaUndoCapture(GiaPhaViewModel tree)
        {
            if (tree == null)
            {
                return;
            }

            tree.RequestUndoSnapshot = CaptureGiaPhaUndoSnapshot;
        }

        public bool IsGiaPhaUndoRestoring()
        {
            return _giaPhaUndoRestoring;
        }

        public void ClearGiaPhaUndoStack()
        {
            _giaPhaUndo.Clear();
        }

        private void CaptureGiaPhaUndoSnapshot(string label)
        {
            if (_giaPhaUndoRestoring || viewModel?.FamilyTree == null)
            {
                return;
            }

            _giaPhaUndo.Push(viewModel.FamilyTree, label);
        }

        /// <summary>Ctrl+Z — khôi phục tối đa 2 bước sửa cây gần nhất.</summary>
        private bool PerformGiaPhaUndo()
        {
            if (viewModel?.FamilyTree == null || _giaPhaUndo.Count == 0)
            {
                return false;
            }

            var entry = _giaPhaUndo.TryPop();
            if (entry?.Snapshot == null)
            {
                return false;
            }

            _giaPhaUndoRestoring = true;
            try
            {
                viewModel.UpdateGiaPhaAsync(entry.Snapshot, saveToJson: false).GetAwaiter().GetResult();
                viewModel.FamilyTree?.AddUserAction("Hoàn tác: " + entry.Label);
                return true;
            }
            finally
            {
                _giaPhaUndoRestoring = false;
            }
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control
                && e.Key == Key.Z)
            {
                if (PerformGiaPhaUndo())
                {
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.Escape && _treePaneImmersive)
            {
                SetTreePaneImmersive(false);
                e.Handled = true;
                return;
            }

            // F11: toàn màn hình cây (tab khác) hoặc phả đồ (tab 4.Phả đồ); F11 lần nữa = thoát
            if (e.Key == Key.F11)
            {
                ToggleFullscreenWithF11();
                e.Handled = true;
                return;
            }

            if (tabControl?.SelectedIndex != PhaDoTabIndex)
            {
                return;
            }

            if (e.Key == Key.Escape && _phaDoImmersive)
            {
                if (_phaDoMultiSelectedFamilyIds.Count > 0 || _phaDoSelectedFamilyId > 0)
                {
                    ClearPhaDoBoxSelections();
                }
                else
                {
                    SetPhaDoImmersive(false);
                }
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape && (_phaDoMultiSelectedFamilyIds.Count > 0 || _phaDoSelectedFamilyId > 0))
            {
                ClearPhaDoBoxSelections();
                e.Handled = true;
            }
        }

        /// <summary>F11 — bật/tắt fullscreen theo ngữ cảnh (cây hoặc phả đồ).</summary>
        private void ToggleFullscreenWithF11()
        {
            if (_treePaneImmersive)
            {
                SetTreePaneImmersive(false);
                return;
            }

            if (_phaDoImmersive)
            {
                SetPhaDoImmersive(false);
                return;
            }

            if (tabControl?.SelectedIndex == PhaDoTabIndex)
            {
                SetPhaDoImmersive(true);
            }
            else
            {
                SetTreePaneImmersive(true);
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
                if (_treePaneImmersive)
                {
                    SetTreePaneImmersive(false);
                }

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
            if (phaDoFullscreenIcon == null || phaDoFullscreenBtn == null)
            {
                return;
            }

            if (_phaDoImmersive)
            {
                phaDoFullscreenIcon.Text = "\uE73F";
                phaDoFullscreenBtn.ToolTip = "Thoát toàn màn hình (F11 / Esc)";
            }
            else
            {
                phaDoFullscreenIcon.Text = "\uE740";
                phaDoFullscreenBtn.ToolTip = "Toàn màn hình (F11)";
            }
        }

        private void PhaDoSubtreeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePhaDoSubtreeListBoxToolTip();
        }

        /// <summary>Cập nhật tooltip ComboBox scope — khi đóng chỉ hiện icon.</summary>
        private void UpdatePhaDoSubtreeListBoxToolTip()
        {
            if (phaDoSubtreeListBox == null)
            {
                return;
            }

            if (phaDoSubtreeListBox.SelectedItem is PhaDoRenderScopeItem item)
            {
                var tip = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(item.RenderPlanSummary))
                {
                    tip.AppendLine(item.RenderPlanSummary);
                }

                if (!string.IsNullOrWhiteSpace(item.Label))
                {
                    tip.Append(item.Label);
                }

                if (tip.Length > 0)
                {
                    phaDoSubtreeListBox.ToolTip = tip.ToString().TrimEnd();
                }
                else
                {
                    phaDoSubtreeListBox.ToolTip = "Chọn Toàn phả hoặc phả con để vẽ";
                }
            }
            else
            {
                phaDoSubtreeListBox.ToolTip = "Chọn Toàn phả hoặc phả con để vẽ";
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
            ResetOrFitPhaDoZoom();
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

        /// <summary>Nút zoom reset: nếu đang 100% thì thu vừa khung; không thì về 100%.</summary>
        private void ResetOrFitPhaDoZoom()
        {
            if (_phaDoRenderedLayout != null
                && Math.Abs(_phaDoZoom - 1.0) < 0.0001)
            {
                FitPhaDoViewToContent(_phaDoRenderedLayout);
            }
            else
            {
                ResetPhaDoZoom();
            }
        }

        private void SetPhaDoZoom(double zoom)
        {
            double targetZoom = Math.Max(PhaDoZoomMin, Math.Min(PhaDoZoomMax, zoom));
            ApplyPhaDoZoomValue(targetZoom, phaDoScrollViewer, _phaDoRenderedLayout);
        }

        /// <summary>Đặt zoom (fit có thể &lt; PhaDoZoomMin) và giữ điểm neo trên viewport khi đang zoom tay.</summary>
        private void ApplyPhaDoZoomValue(double targetZoom, ScrollViewer scroll, GiaPhaRenderResult layoutForAnchor)
        {
            targetZoom = Math.Max(PhaDoZoomFitMin, Math.Min(PhaDoZoomMax, targetZoom));
            if (Math.Abs(targetZoom - _phaDoZoom) < 0.0001)
            {
                return;
            }

            double oldZoom = _phaDoZoom;
            double anchorCanvasX;
            double anchorCanvasY;
            double anchorViewportX;
            double anchorViewportY;

            if (scroll != null)
            {
                var selectedNode = FindNodeByFamilyId(_phaDoSelectedFamilyId);
                if (selectedNode?.Metrics != null)
                {
                    // Khi đã chọn 1 gia đình, giữ đúng vị trí box đó trong khung nhìn khi zoom.
                    anchorCanvasX = MmToPx(selectedNode.Xmm + selectedNode.Metrics.WidthMm / 2.0);
                    anchorCanvasY = MmToPx(selectedNode.Ymm + selectedNode.Metrics.HeightMm / 2.0);
                    anchorViewportX = anchorCanvasX * oldZoom - scroll.HorizontalOffset;
                    anchorViewportY = anchorCanvasY * oldZoom - scroll.VerticalOffset;
                }
                else
                {
                    // Chưa chọn box nào thì giữ tâm viewport để zoom không bị "nhảy".
                    anchorViewportX = scroll.ViewportWidth > 1 ? scroll.ViewportWidth / 2.0 : 0;
                    anchorViewportY = scroll.ViewportHeight > 1 ? scroll.ViewportHeight / 2.0 : 0;
                    anchorCanvasX = oldZoom > 0
                        ? (scroll.HorizontalOffset + anchorViewportX) / oldZoom
                        : 0;
                    anchorCanvasY = oldZoom > 0
                        ? (scroll.VerticalOffset + anchorViewportY) / oldZoom
                        : 0;
                }
            }
            else
            {
                anchorCanvasX = 0;
                anchorCanvasY = 0;
                anchorViewportX = 0;
                anchorViewportY = 0;
            }

            _phaDoZoom = targetZoom;
            theCanvas.LayoutTransform = new ScaleTransform(_phaDoZoom, _phaDoZoom);
            if (phaDoZoomResetBtn != null)
            {
                int zoomPct = (int)Math.Round(_phaDoZoom * 100, MidpointRounding.AwayFromZero);
                phaDoZoomResetBtn.ToolTip = zoomPct == 100
                    ? "Zoom 100% — nhấn để vừa khung"
                    : "Zoom " + zoomPct + "% — nhấn về 100%";
            }

            if (scroll != null && layoutForAnchor != null)
            {
                // Dời scroll theo tỉ lệ zoom mới để điểm neo vẫn nằm đúng chỗ trong viewport.
                double newOffsetX = anchorCanvasX * _phaDoZoom - anchorViewportX;
                double newOffsetY = anchorCanvasY * _phaDoZoom - anchorViewportY;
                scroll.ScrollToHorizontalOffset(Math.Max(0, newOffsetX));
                scroll.ScrollToVerticalOffset(Math.Max(0, newOffsetY));
                scroll.InvalidateMeasure();
            }
            else
            {
                phaDoScrollViewer?.InvalidateMeasure();
            }
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
        
        // ════════════════════════════════════════════════════════════
        //  Title block — chọn dòng text con (2-level selection)
        // ════════════════════════════════════════════════════════════

        private bool TryResolveTitleTextLineHit(DependencyObject source, out int lineIndex, out FrameworkElement lineElement)
        {
            lineIndex = -1;
            lineElement = null;
            var el = source;
            while (el != null)
            {
                if (el is FrameworkElement fe && fe.Tag is GiaPhaRender.PhaDoTitleTextLineTag tag)
                {
                    lineIndex = tag.LineIndex;
                    lineElement = fe;
                    return true;
                }

                el = VisualTreeHelper.GetParent(el);
            }

            return false;
        }

        /// <summary>Chọn khối title (viền + resize), không chọn dòng chữ.</summary>
        private void SelectTitleBlockOutline()
        {
            ClearPhaDoBoxSelections();
            ClearGenLabelSelection();
            CancelPendingTitleDrag();
            CancelPendingPersonDrag();
            _phaDoTitleSelected = true;
            _phaDoTitleSelectedLine = -1;
            ClearTitleLineHighlight();
            DrawTitleSelectionOverlay();
            UpdateTitleHitRectCursor();
            SyncToolbarFromTitleBlock();
        }

        /// <summary>Chỉ chọn dòng chữ (highlight + toolbar font), không vẽ viền resize khối.</summary>
        private void SelectTitleTextLine(int lineIndex)
        {
            ClearPhaDoBoxSelections();
            ClearGenLabelSelection();
            CancelPendingPersonDrag();
            CancelPendingTitleDrag();
            _phaDoTitleSelected = true;
            _phaDoTitleSelectedLine = lineIndex;
            ClearTitleSelectionOverlay();
            DrawTitleLineHighlight(lineIndex);
            UpdateTitleHitRectCursor();
            SyncToolbarFromTitleLine(lineIndex);
        }

        private void DrawTitleLineHighlight(int lineIndex)
        {
            ClearTitleLineHighlight();
            var tb = theCanvas.Children.OfType<FrameworkElement>()
                .FirstOrDefault(fe => fe.Tag is GiaPhaRender.PhaDoTitleTextLineTag t && t.LineIndex == lineIndex)
                as System.Windows.Controls.TextBlock;
            if (tb == null) return;

            double left = Canvas.GetLeft(tb) - 3;
            double top  = Canvas.GetTop(tb)  - 2;
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double w = tb.DesiredSize.Width + 6;
            double h = tb.DesiredSize.Height + 4;

            var hl = new System.Windows.Shapes.Rectangle
            {
                Width = w, Height = h,
                Stroke = new SolidColorBrush(Color.FromRgb(30, 120, 220)),
                StrokeThickness = 1.5,
                StrokeDashArray = new System.Windows.Media.DoubleCollection { 4, 2 },
                Fill  = new SolidColorBrush(Color.FromArgb(30, 30, 120, 220)),
                Tag   = "__TitleLineHighlight",
                IsHitTestVisible = false
            };
            Canvas.SetLeft(hl, left); Canvas.SetTop(hl, top);
            Panel.SetZIndex(hl, 10);
            theCanvas.Children.Add(hl);

            const double handle = 7;
            AddTitleLineSelectionHandle(left, top, handle);
            AddTitleLineSelectionHandle(left + w, top, handle);
            AddTitleLineSelectionHandle(left, top + h, handle);
            AddTitleLineSelectionHandle(left + w, top + h, handle);
        }

        private void AddTitleLineSelectionHandle(double cornerX, double cornerY, double size)
        {
            var h = new System.Windows.Shapes.Rectangle
            {
                Width = size,
                Height = size,
                Fill = Brushes.White,
                Stroke = new SolidColorBrush(Color.FromRgb(30, 120, 220)),
                StrokeThickness = 1.2,
                RadiusX = 1,
                RadiusY = 1,
                IsHitTestVisible = false,
                Tag = "__TitleLineHighlight"
            };
            Canvas.SetLeft(h, cornerX - size / 2.0);
            Canvas.SetTop(h, cornerY - size / 2.0);
            Panel.SetZIndex(h, 11);
            theCanvas.Children.Add(h);
        }

        private void ClearTitleLineHighlight()
        {
            var old = theCanvas.Children.OfType<FrameworkElement>()
                .Where(fe => Equals(fe.Tag, "__TitleLineHighlight"))
                .Cast<UIElement>().ToList();
            foreach (var el in old) theCanvas.Children.Remove(el);
        }

        private void SyncToolbarFromTitleLine(int lineIndex)
        {
            ShowContextToolbar(PhaDoCtxSelType.TitleText);
            if (_phaDoCurrentOptions == null) return;

            double defaultSmallPt = Math.Max(7,
                (_phaDoCurrentOptions.TitleLine2FontPt > 0 ? _phaDoCurrentOptions.TitleLine2FontPt : 12) * 0.78);

            double pt;
            string hex;
            string family;
            if (lineIndex == 0)
            {
                pt  = _phaDoCurrentOptions.TitleFontPt;
                hex = _phaDoCurrentOptions.TitleLine1ForegroundHex ?? "#000000";
                family = _phaDoCurrentOptions.TitleLine1FontFamily;
            }
            else if (lineIndex == 1)
            {
                pt  = _phaDoCurrentOptions.TitleLine2FontPt;
                hex = _phaDoCurrentOptions.TitleLine2ForegroundHex ?? "#333333";
                family = _phaDoCurrentOptions.TitleLine2FontFamily;
            }
            else if (lineIndex == 2)
            {
                pt  = _phaDoCurrentOptions.TitleLine3FontPt > 0
                      ? _phaDoCurrentOptions.TitleLine3FontPt : defaultSmallPt;
                hex = _phaDoCurrentOptions.TitleLine3ForegroundHex ?? "#888888";
                family = _phaDoCurrentOptions.TitleLine3FontFamily;
            }
            else
            {
                pt  = _phaDoCurrentOptions.TitleLine4FontPt > 0
                      ? _phaDoCurrentOptions.TitleLine4FontPt : defaultSmallPt;
                hex = _phaDoCurrentOptions.TitleLine4ForegroundHex ?? "#888888";
                family = _phaDoCurrentOptions.TitleLine4FontFamily;
            }

            if (phaDoCtxFontSizeBox != null) phaDoCtxFontSizeBox.Text = pt.ToString("0.#");
            if (phaDoCtxColorBox    != null) phaDoCtxColorBox.Text    = hex;
            PhaDoCtxColorBox_RefreshPreview();
            if (phaDoCtxFontFamilyCombo != null)
            {
                phaDoCtxFontFamilyCombo.SelectedItem = !string.IsNullOrWhiteSpace(family) ? family : "(mặc định)";
                if (phaDoCtxFontFamilyCombo.SelectedItem == null && phaDoCtxFontFamilyCombo.Items.Count > 0)
                {
                    phaDoCtxFontFamilyCombo.SelectedIndex = 0;
                }
            }
            // Dòng 1 (Tên) mặc định Bold
            if (phaDoCtxBoldBtn     != null) phaDoCtxBoldBtn.IsChecked = lineIndex == 0;
        }

        private void ApplyToolbarToTitleLine(int lineIndex)
        {
            if (_phaDoCurrentOptions == null) return;

            double.TryParse(phaDoCtxFontSizeBox?.Text,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double pt);
            string hex = phaDoCtxColorBox?.Text?.Trim();
            try { ColorConverter.ConvertFromString(hex); } catch { hex = null; }
            string family = (phaDoCtxFontFamilyCombo?.SelectedItem as string) == "(mặc định)"
                ? null
                : (phaDoCtxFontFamilyCombo?.SelectedItem as string);

            switch (lineIndex)
            {
                case 0:
                    if (pt > 0) _phaDoCurrentOptions.TitleFontPt = pt;
                    _phaDoCurrentOptions.TitleLine1ForegroundHex = hex;
                    _phaDoCurrentOptions.TitleLine1FontFamily = family;
                    break;
                case 1:
                    if (pt > 0) _phaDoCurrentOptions.TitleLine2FontPt = pt;
                    _phaDoCurrentOptions.TitleLine2ForegroundHex = hex;
                    _phaDoCurrentOptions.TitleLine2FontFamily = family;
                    break;
                case 2:
                    if (pt > 0) _phaDoCurrentOptions.TitleLine3FontPt = pt;
                    _phaDoCurrentOptions.TitleLine3ForegroundHex = hex;
                    _phaDoCurrentOptions.TitleLine3FontFamily = family;
                    break;
                default:
                    if (pt > 0) _phaDoCurrentOptions.TitleLine4FontPt = pt;
                    _phaDoCurrentOptions.TitleLine4ForegroundHex = hex;
                    _phaDoCurrentOptions.TitleLine4FontFamily = family;
                    break;
            }

            RedrawTitleBlockOnly();
            DrawTitleLineHighlight(lineIndex);   // vẽ lại highlight sau khi title đã redraw
        }

        // ═══════════════════════════════════════════════════════════════
        //  Nhãn "Đời X" — select + popup chỉnh style
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Kiểm tra click có trúng TextBlock nhãn "Đời X" không, trả về level tương ứng.</summary>
        private bool TryResolveGenLabelHit(DependencyObject source, out int level)
        {
            level = -1;
            var el = source;
            while (el != null)
            {
                if (el is FrameworkElement fe && fe.Tag is GiaPhaRender.PhaDoGenLabelTag tag)
                {
                    level = tag.Level;
                    return true;
                }
                el = VisualTreeHelper.GetParent(el);
            }
            return false;
        }

        /// <summary>Click nhãn "Đời X" → chỉ cập nhật toolbar trên, không mở popup.</summary>
        private void OpenGenLabelStylePopup(int level)
        {
            SyncToolbarFromGenLabel();
        }

        private FrameworkElement FindGenLabelVisual(int level)
        {
            FrameworkElement best = null;
            int bestZ = int.MinValue;
            foreach (var child in theCanvas.Children.OfType<FrameworkElement>())
            {
                if (child.Tag is GiaPhaRender.PhaDoGenLabelTag tag && tag.Level == level)
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

        private void ClearGenLabelSelectionOverlay()
        {
            var old = theCanvas.Children.OfType<FrameworkElement>()
                .Where(fe => Equals(fe.Tag, GenLabelSelectionTag))
                .Cast<UIElement>()
                .ToList();
            foreach (var el in old)
            {
                theCanvas.Children.Remove(el);
            }
        }

        /// <summary>Vẽ khung chọn (kèm 4 góc) quanh TextBlock nhãn "Đời X".</summary>
        private void DrawGenLabelSelectionOverlay(int level)
        {
            ClearGenLabelSelectionOverlay();
            if (theCanvas == null)
            {
                return;
            }

            var el = FindGenLabelVisual(level);
            if (el == null)
            {
                return;
            }

            el.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double left = Canvas.GetLeft(el);
            double top = Canvas.GetTop(el);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;
            double w = el.DesiredSize.Width;
            double h = el.DesiredSize.Height;

            const double pad = 3;
            var outline = new Rectangle
            {
                Width = Math.Max(4, w + pad * 2),
                Height = Math.Max(4, h + pad * 2),
                Stroke = new SolidColorBrush(Color.FromRgb(30, 120, 220)),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = new SolidColorBrush(Color.FromArgb(25, 30, 120, 220)),
                IsHitTestVisible = false,
                Tag = GenLabelSelectionTag
            };
            Canvas.SetLeft(outline, left - pad);
            Canvas.SetTop(outline, top - pad);
            Panel.SetZIndex(outline, 1003);
            theCanvas.Children.Add(outline);

            // 4 góc vuông
            const double handle = 7;
            void AddCorner(double x, double y)
            {
                var c = new Rectangle
                {
                    Width = handle,
                    Height = handle,
                    Fill = Brushes.White,
                    Stroke = new SolidColorBrush(Color.FromRgb(30, 120, 220)),
                    StrokeThickness = 1.2,
                    RadiusX = 1,
                    RadiusY = 1,
                    IsHitTestVisible = false,
                    Tag = GenLabelSelectionTag
                };
                Canvas.SetLeft(c, x - handle / 2.0);
                Canvas.SetTop(c, y - handle / 2.0);
                Panel.SetZIndex(c, 1004);
                theCanvas.Children.Add(c);
            }

            double x0 = left - pad;
            double y0 = top - pad;
            double x1 = x0 + outline.Width;
            double y1 = y0 + outline.Height;
            AddCorner(x0, y0);
            AddCorner(x1, y0);
            AddCorner(x0, y1);
            AddCorner(x1, y1);
        }

        private void UpdateGenLabelColorPreview()
        {
            // genLabelColorPreview chưa khởi tạo khi TextChanged bắn lần đầu trong InitializeComponent
            if (genLabelColorPreview == null) return;
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(genLabelColorBox.Text.Trim());
                genLabelColorPreview.Background = new SolidColorBrush(c);
            }
            catch { genLabelColorPreview.Background = Brushes.Transparent; }
        }

        private void GenLabelFontPtBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        { }

        private void GenLabelColorBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            => UpdateGenLabelColorPreview();

        /// <summary>Áp dụng style từ popup cho tất cả nhãn "Đời" — vẽ lại dải mà không render lại toàn bộ.</summary>
        private void GenLabelApplyAll_Click(object sender, RoutedEventArgs e)
        {
            if (_phaDoCurrentOptions == null) return;

            if (!double.TryParse(genLabelFontPtBox.Text, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double pt) || pt <= 0)
                pt = 0;

            string colorHex = genLabelColorBox.Text.Trim();
            // Validate hex — nếu không parse được thì bỏ qua
            try { ColorConverter.ConvertFromString(colorHex); }
            catch { colorHex = null; }

            string family = genLabelFontFamilyBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(family)) family = null;

            _phaDoCurrentOptions.GenLabelStyle = new GiaPhaRender.PhaDoGenLabelStyle
            {
                FontPt = pt,
                ForegroundHex = colorHex,
                FontFamily = family,
                Bold = genLabelBoldCheck.IsChecked == true,
                Italic = genLabelItalicCheck.IsChecked == true
            };

            // Vẽ lại chỉ dải nhãn Đời — không cần full-render lại toàn bộ phả đồ
            RedrawGenerationBandsOnly();
            genLabelStylePopup.IsOpen = false;
        }

        /// <summary>Đặt lại style nhãn Đời về mặc định.</summary>
        private void GenLabelReset_Click(object sender, RoutedEventArgs e)
        {
            if (_phaDoCurrentOptions == null) return;
            _phaDoCurrentOptions.GenLabelStyle = null;
            RedrawGenerationBandsOnly();
            genLabelStylePopup.IsOpen = false;
        }

        /// <summary>Vẽ lại các TextBlock nhãn "Đời X" mà không xóa toàn canvas.</summary>
        private void RedrawGenerationBandsOnly()
        {
            if (_phaDoCurrentOptions == null || _phaDoRenderedLayout == null) return;

            // Xóa label cũ — tìm theo tag PhaDoGenLabelTag
            var toRemove = theCanvas.Children.OfType<FrameworkElement>()
                .Where(fe => fe.Tag is GiaPhaRender.PhaDoGenLabelTag)
                .ToList();
            foreach (var fe in toRemove)
                theCanvas.Children.Remove(fe);

            // Vẽ lại nhãn mới theo style hiện tại
            bool vertical = GiaPhaRenderOptions.IsVerticalCardLayout(_phaDoCurrentOptions.CardLayoutMode);
            var custom = _phaDoCurrentOptions.GenLabelStyle;

            double dpi = _phaDoCurrentOptions.PrintDpi > 0 ? _phaDoCurrentOptions.PrintDpi : 96;

            foreach (var band in _phaDoRenderedLayout.GenerationBands)
            {
                double defaultPt = vertical
                    ? _phaDoCurrentOptions.VerticalGenerationLabelFontPt
                    : _phaDoCurrentOptions.HeaderFontPt;

                double pt = custom?.FontPt > 0 ? custom.FontPt : defaultPt;
                string fontFamily = !string.IsNullOrWhiteSpace(custom?.FontFamily)
                    ? custom.FontFamily
                    : _phaDoCurrentOptions.FontFamilyName;

                FontWeight fw = custom != null
                    ? (custom.Bold ? FontWeights.Bold : FontWeights.Normal)
                    : (vertical ? FontWeights.SemiBold : FontWeights.Normal);

                FontStyle fs = custom?.Italic == true ? FontStyles.Italic : FontStyles.Normal;

                Brush fg;
                if (!string.IsNullOrWhiteSpace(custom?.ForegroundHex))
                {
                    try { fg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(custom.ForegroundHex)); }
                    catch { fg = new SolidColorBrush(Color.FromRgb(90, 90, 90)); }
                }
                else
                {
                    fg = vertical
                        ? new SolidColorBrush(Color.FromRgb(25, 55, 120))
                        : new SolidColorBrush(Color.FromRgb(90, 90, 90));
                }

                double mmPx = dpi / 25.4;
                var labelDoi = new System.Windows.Controls.TextBlock
                {
                    Text = "Đời " + band.Level,
                    FontFamily = new FontFamily(fontFamily),
                    FontSize = pt * dpi / 72.0,
                    FontWeight = fw,
                    FontStyle = fs,
                    Foreground = fg,
                    IsHitTestVisible = true,
                    Cursor = Cursors.Hand,
                    Tag = new GiaPhaRender.PhaDoGenLabelTag(band.Level)
                };
                Canvas.SetLeft(labelDoi, _phaDoCurrentOptions.MarginMm * mmPx);
                Canvas.SetTop(labelDoi, band.Ymm * mmPx + 2);
                Panel.SetZIndex(labelDoi, 1);
                theCanvas.Children.Add(labelDoi);
            }
        }

        //
    }
}
