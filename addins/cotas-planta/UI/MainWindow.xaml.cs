using System;
using System.Windows;
using System.Windows.Interop;

namespace CQEING.CotasPlanta.UI
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseRequested += Close;

            // Que la ventana quepa en pantallas pequeñas.
            Height = Math.Min(Height, SystemParameters.WorkArea.Height - 40);
        }

        /// <summary>
        /// La ventana queda encima de Revit pero no lo bloquea: se puede minimizar sola
        /// y Revit sigue visible y utilizable. Se recupera con su ícono en la barra de tareas.
        /// </summary>
        public void SetOwner(IntPtr revitWindow) => new WindowInteropHelper(this).Owner = revitWindow;
    }
}
