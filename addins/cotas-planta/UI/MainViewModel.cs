using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using CQEING.CotasPlanta.Configuration;
using CQEING.CotasPlanta.Domain;
using CQEING.CotasPlanta.Revit;

namespace CQEING.CotasPlanta.UI
{
    /// <summary>Todo lo que muestra y edita la ventana. No toca Revit directamente.</summary>
    public sealed class MainViewModel : ObservableObject
    {
        private static readonly (PlanSide Side, string Label)[] SideOrder =
        {
            (PlanSide.Top, "Arriba"), (PlanSide.Left, "Izquierda"), (PlanSide.Right, "Derecha"), (PlanSide.Bottom, "Abajo"),
        };

        private readonly Func<long, PlanGeometry> _loadGeometry;
        private readonly Dictionary<long, PlanGeometry> _geometryCache = new();

        private PlanItemViewModel? _selectedPlan;
        private string _searchText = "";
        private double _firstOffsetMm;
        private double _spacingMm;
        private bool _markVoids;
        private bool _showReferenceLines;
        private bool _includeJoists;
        private bool _tintSlab;
        private bool _longTicks;
        private bool _useShortNames;
        private string? _selectedDimensionType;
        private PreviewScene? _scene;
        private IReadOnlyList<LegendEntry> _legend = Array.Empty<LegendEntry>();
        private IReadOnlyList<string> _beamLegend = Array.Empty<string>();
        private string _chainsText = "";
        private string _segmentsText = "";

        public MainViewModel(
            string projectName,
            IEnumerable<PlanEntry> plans,
            IReadOnlyList<string> dimensionTypes,
            Func<long, PlanGeometry> loadGeometry,
            Action<long, CotasSettings> generate,
            long? activeViewId)
        {
            _loadGeometry = loadGeometry;
            var defaults = new CotasSettings();

            ProjectSubtitle = "CQEING · Proyecto: " + projectName;
            Plans = new ObservableCollection<PlanItemViewModel>(plans.Select(p => new PlanItemViewModel(p)));
            PlansView = CollectionViewSource.GetDefaultView(Plans);
            PlansView.Filter = item => item is PlanItemViewModel p
                && (_searchText.Length == 0 || p.Name.Contains(_searchText, StringComparison.CurrentCultureIgnoreCase));

            Sides = SideOrder
                .Select(s => new SideRowViewModel(s.Side, s.Label,
                    defaults.Sides.FirstOrDefault(l => l.Side == s.Side)?.Chains.ToList() ?? new List<ChainKind>()))
                .ToList();

            _firstOffsetMm = defaults.FirstOffsetMm;
            _spacingMm = defaults.SpacingMm;
            _markVoids = defaults.MarkVoids;
            _showReferenceLines = defaults.ShowReferenceLines;
            _includeJoists = defaults.IncludeJoists;
            _tintSlab = defaults.TintSlab;
            _longTicks = defaults.LongTicks;
            _useShortNames = defaults.BeamNaming == BeamNaming.Short;
            DimensionTypes = dimensionTypes;
            _selectedDimensionType = dimensionTypes.FirstOrDefault();

            foreach (SideRowViewModel row in Sides)
            {
                row.PropertyChanged += (_, _) => Refresh();
            }

            GenerateCommand = new RelayCommand(() => generate(SelectedPlan!.Id, BuildSettings()), () => SelectedPlan is not null);
            CloseCommand = new RelayCommand(() => CloseRequested?.Invoke());
            SelectedPlan = Plans.FirstOrDefault(p => p.Id == activeViewId) ?? Plans.FirstOrDefault();
        }

        public event Action? CloseRequested;

        public string ProjectSubtitle { get; }
        public ObservableCollection<PlanItemViewModel> Plans { get; }
        public ICollectionView PlansView { get; }
        public IReadOnlyList<SideRowViewModel> Sides { get; }
        public IReadOnlyList<string> DimensionTypes { get; }
        public ICommand GenerateCommand { get; }
        public ICommand CloseCommand { get; }

        public PlanItemViewModel? SelectedPlan
        {
            get => _selectedPlan;
            set
            {
                if (Set(ref _selectedPlan, value))
                {
                    OnPropertyChanged(nameof(PreviewTitle));
                    OnPropertyChanged(nameof(ScaleText));
                    OnPropertyChanged(nameof(GenerateLabel));
                    CommandManager.InvalidateRequerySuggested();
                    Refresh();
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (Set(ref _searchText, value ?? ""))
                {
                    PlansView.Refresh();
                }
            }
        }

        public double FirstOffsetMm
        {
            get => _firstOffsetMm;
            set { if (Set(ref _firstOffsetMm, Math.Round(value))) { OnPropertyChanged(nameof(FirstOffsetText)); Refresh(); } }
        }

        public double SpacingMm
        {
            get => _spacingMm;
            set { if (Set(ref _spacingMm, Math.Round(value))) { OnPropertyChanged(nameof(SpacingText)); Refresh(); } }
        }

        public bool MarkVoids { get => _markVoids; set { if (Set(ref _markVoids, value)) Refresh(); } }
        public bool ShowReferenceLines { get => _showReferenceLines; set { if (Set(ref _showReferenceLines, value)) Refresh(); } }
        public bool IncludeJoists { get => _includeJoists; set { if (Set(ref _includeJoists, value)) Refresh(); } }
        public bool TintSlab { get => _tintSlab; set { if (Set(ref _tintSlab, value)) Refresh(); } }
        public bool LongTicks { get => _longTicks; set { if (Set(ref _longTicks, value)) Refresh(); } }

        public bool UseShortNames
        {
            get => _useShortNames;
            set
            {
                if (Set(ref _useShortNames, value))
                {
                    OnPropertyChanged(nameof(UseSectionNames));
                    Refresh();
                }
            }
        }

        /// <summary>Pareja de <see cref="UseShortNames"/> para el segundo botón del selector.</summary>
        public bool UseSectionNames
        {
            get => !_useShortNames;
            set { if (value) UseShortNames = false; }
        }

        public string? SelectedDimensionType
        {
            get => _selectedDimensionType;
            set => Set(ref _selectedDimensionType, value);
        }

        public PreviewScene? Scene { get => _scene; private set => Set(ref _scene, value); }
        public IReadOnlyList<LegendEntry> Legend { get => _legend; private set => Set(ref _legend, value); }
        public IReadOnlyList<string> BeamLegend { get => _beamLegend; private set { if (Set(ref _beamLegend, value)) OnPropertyChanged(nameof(HasBeamLegend)); } }
        public bool HasBeamLegend => BeamLegend.Count > 0;
        public string ChainsText { get => _chainsText; private set => Set(ref _chainsText, value); }
        public string SegmentsText { get => _segmentsText; private set => Set(ref _segmentsText, value); }

        public string PreviewTitle => SelectedPlan is null ? "Vista previa" : $"Vista previa · {SelectedPlan.Name} ({SelectedPlan.Level})";
        public string ScaleText => SelectedPlan is null ? "" : "Escala 1:" + SelectedPlan.Entry.Scale;
        public string FirstOffsetText => $"{FirstOffsetMm:0} mm";
        public string SpacingText => $"{SpacingMm:0} mm";

        public string PlanCountText => Plans.Count == 1 ? "1 planta" : $"{Plans.Count} plantas";

        /// <summary>Se genera solo la planta que se está viendo.</summary>
        public string GenerateLabel => SelectedPlan is null ? "Generar cotas" : "Generar cotas de " + SelectedPlan.Name;

        public CotasSettings BuildSettings() => new()
        {
            FirstOffsetMm = FirstOffsetMm,
            SpacingMm = SpacingMm,
            MarkVoids = MarkVoids,
            ShowReferenceLines = ShowReferenceLines,
            IncludeJoists = IncludeJoists,
            TintSlab = TintSlab,
            LongTicks = LongTicks,
            BeamNaming = UseShortNames ? BeamNaming.Short : BeamNaming.Section,
            BaseDimensionTypeName = SelectedDimensionType,
            Sides = Sides.Select(s => s.ToLayout()).ToList(),
        };

        /// <summary>Recalcula la vista previa con la planta y la configuración actuales.</summary>
        private void Refresh()
        {
            if (SelectedPlan is null)
            {
                Scene = null;
                return;
            }

            PlanGeometry geometry = GeometryOf(SelectedPlan.Id);
            CotasSettings settings = BuildSettings();
            IReadOnlyList<ChainSpec> chains = ChainPlanner.Plan(geometry, settings);
            IReadOnlyList<LegendEntry> legend = LegendContent.Build(geometry, chains, settings);

            Scene = new PreviewScene(geometry, chains, settings, SelectedPlan.Entry.Scale);
            Legend = legend.Where(e => e.Symbol != LegendSymbol.Beam).ToList();
            BeamLegend = legend.Where(e => e.Symbol == LegendSymbol.Beam).Select(e => e.Label).ToList();
            ChainsText = chains.Count == 1 ? "1 cadena" : $"{chains.Count} cadenas";
            int segments = chains.Sum(c => c.SegmentCount);
            SegmentsText = segments == 1 ? "1 cota" : $"{segments} cotas";
        }

        private PlanGeometry GeometryOf(long viewId)
        {
            if (!_geometryCache.TryGetValue(viewId, out PlanGeometry? geometry))
            {
                geometry = _loadGeometry(viewId);
                _geometryCache[viewId] = geometry;
            }

            return geometry;
        }
    }
}
