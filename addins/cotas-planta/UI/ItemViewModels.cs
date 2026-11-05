using System.Collections.Generic;
using System.Linq;
using CQEING.CotasPlanta.Configuration;
using CQEING.CotasPlanta.Revit;

namespace CQEING.CotasPlanta.UI
{
    /// <summary>Una fila de la lista de plantas.</summary>
    public sealed class PlanItemViewModel
    {
        public PlanItemViewModel(PlanEntry entry)
        {
            Entry = entry;
        }

        public PlanEntry Entry { get; }
        public long Id => Entry.Id;
        public string Name => Entry.Name;
        public string Level => Entry.Level;
        public string Meta => Entry.Meta;
    }

    /// <summary>Una fila de la matriz "Cotas por lado".</summary>
    public sealed class SideRowViewModel : ObservableObject
    {
        private bool _libres;
        private bool _ejeAEje;
        private bool _voladizo;
        private bool _total;

        public SideRowViewModel(PlanSide side, string label, IReadOnlyCollection<ChainKind> chains)
        {
            Side = side;
            Label = label;
            _libres = chains.Contains(ChainKind.Libres);
            _ejeAEje = chains.Contains(ChainKind.EjeAEje);
            _voladizo = chains.Contains(ChainKind.Voladizo);
            _total = chains.Contains(ChainKind.Total);
        }

        public PlanSide Side { get; }
        public string Label { get; }

        public bool Libres { get => _libres; set => Set(ref _libres, value); }
        public bool EjeAEje { get => _ejeAEje; set => Set(ref _ejeAEje, value); }
        public bool Voladizo { get => _voladizo; set => Set(ref _voladizo, value); }
        public bool Total { get => _total; set => Set(ref _total, value); }

        /// <summary>Cadenas marcadas, de adentro hacia afuera.</summary>
        public SideLayout ToLayout()
        {
            var chains = new List<ChainKind>();
            if (Libres) chains.Add(ChainKind.Libres);
            if (EjeAEje) chains.Add(ChainKind.EjeAEje);
            if (Voladizo) chains.Add(ChainKind.Voladizo);
            if (Total) chains.Add(ChainKind.Total);
            return new SideLayout(Side, chains);
        }
    }
}
