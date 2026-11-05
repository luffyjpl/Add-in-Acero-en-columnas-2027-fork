using System.Collections.Concurrent;
using System.IO;
using Autodesk.Revit.UI;

namespace CQEING.Zapatas.Tablero;

/// <summary>
/// Cola de trabajos para el hilo de Revit: la ventana no tiene modo, así que todo pasa por un <see cref="ExternalEvent"/>.
/// Se crea dentro de un comando, donde la API es válida. Igual que la de los otros add-ins.
/// </summary>
internal sealed class AccionesRevit : IExternalEventHandler, IDisposable
{
    private readonly ConcurrentQueue<Action<UIApplication>> cola = new();
    private readonly ExternalEvent evento;

    public AccionesRevit() => evento = ExternalEvent.Create(this);

    /// <summary>Un trabajo que falló, con su mensaje, para mostrarlo en la página.</summary>
    public event Action<string>? Fallo;

    /// <summary>Corre <paramref name="trabajo"/> en el hilo de Revit, cuando Revit quede libre.</summary>
    public void Encolar(Action<UIApplication> trabajo)
    {
        cola.Enqueue(trabajo);
        evento.Raise();
    }

    public void Execute(UIApplication app)
    {
        while (cola.TryDequeue(out Action<UIApplication>? trabajo))
        {
            // Nada se escapa hacia Revit: un error se muestra en la página y la cola sigue.
            try
            {
                trabajo(app);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Registrar(e);
                Fallo?.Invoke(e.Message);
            }
        }
    }

    /// <summary><c>%AppData%\CQEING\zapatas\errores.log</c>: el error completo, para saber dónde falló.</summary>
    public static string RutaErrores { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CQEING", "zapatas", "errores.log");

    private static void Registrar(Exception e)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RutaErrores)!);
            File.AppendAllText(RutaErrores, $"{DateTime.Now:s} {e}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Si no se puede escribir, se sigue igual.
        }
    }

    public string GetName() => "CQEING Acero en zapatas";

    public void Dispose() => evento.Dispose();
}
