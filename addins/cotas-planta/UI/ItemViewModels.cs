using System.Collections.Generic;
using System.Linq;
using CQEING.CotasPlanta.Configuration;
using CQEING.CotasPlanta.Revit;

namespace CQEING.CotasPlanta.UI
{
    /// <summary>
    /// Una fila de la lista de plantas. Primero se eligen las plantas que van a salir en planos
    /// y luego se acotan una por una, cada una con su escala.
    /// </summary>
    public sealed class PlanItemViewModel : ObservableObject
    {
        private bool _isChosen;
        private bool _isGenerated;
        private int _scale;

        public PlanItemViewModel(PlanEntry entry)
        {
            Entry = entry;
            _isGenerated = entry.HasCotasView;
            _scale = entry.Scale;
        }

        public PlanEntry Entry { get; }
        public long Id => Entry.Id;
        public string Name => Entry.Name;
        public string Level => Entry.Level;
        public string Meta => Entry.Meta;

        /// <summary>Esta planta va a salir en planos.</summary>
        public bool IsChosen { get => _isChosen; set => Set(ref _isChosen, value); }

        /// <summary>Ya tiene su vista de cotas.</summary>
        public bool IsGenerated
        {
            get => _isGenerated;
            set { if (Set(ref _isGenerated, value)) OnPropertyChanged(nameof(StatusText)); }
        }

        public string StatusText => IsGenerated ? "Generada" : "Pendiente";

        /// <summary>Escala de su vista de cotas. Arranca con la de la planta.</summary>
        public int Scale { get => _scale; set => Set(ref _scale, value); }
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
