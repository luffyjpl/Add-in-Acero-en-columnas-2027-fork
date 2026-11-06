namespace practicacq.Modelo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Alturas de los estribos de un tramo, sin Revit (#256,
/// docs/planes/2026-11-05-largos-y-estribos-columnas-245-256.md, decisiones 2
/// y 9): la misma regla que vigas (<c>DistribuidorEstribos.Repartir</c>,
/// commit e993767). El primero y el último a 5 cm del borde del paquete, y
/// entre medio cada zona a su separación exacta: lo que sobra queda en un
/// último espacio más corto, nunca más largo. Si ese espacio da menos de 5 cm,
/// los dos últimos se igualan para que no queden dos estribos casi juntos.
///
/// Las confinadas van exactas desde su nudo; la central, exacta desde el
/// último confinado de abajo, con el espacio corto contra el primero del
/// confinado de arriba (como el centro de una luz en vigas).
///
/// Antes cada grupo era un <c>SetLayoutAsMaximumSpacing</c> de Revit, que
/// reparte parejo sin pasarse: pedido cada 0.14 salía cada 0.13.
///
/// La usan la vista previa (para mostrar lo mismo que se va a generar) y
/// <c>GeneradorArmado</c>, que crea un conjunto de Revit por cada
/// <see cref="Corrida"/>: el estribo del espacio corto, si no es el borde de
/// una zona confinada, queda en un conjunto aparte, como en vigas.
/// </summary>
public static class RepartoEstribos
{
    /// <summary>Del borde del tramo al borde del paquete, en cada punta.</summary>
    public const double SeparacionExtremosMetros = 0.05;

    /// <summary>
    /// El espacio más corto que se deja entre dos estribos, en metros: si lo
    /// que sobra al final de una zona es menos, los dos últimos espacios se
    /// igualan (como <c>EspacioMinimo</c> de vigas, 50 mm).
    /// </summary>
    public const double EspacioMinimoMetros = 0.05;

    private const double Tolerancia = 1e-6;

    /// <summary>Dos separaciones que difieren menos que esto (0,1 mm) son la misma corrida.</summary>
    private const double ToleranciaCorrida = 1e-4;

    /// <param name="Nombre">El rótulo del grupo: «uniforme», «confinado inferior», «zona central», «confinado superior» o «confinado (sin zona central)».</param>
    /// <param name="ZDesde">Donde empieza la zona configurada (en las confinadas, L1 desde su punta).</param>
    /// <param name="Espaciado">La separación pedida: la real entre todos, salvo el último espacio, que puede ser más corto.</param>
    /// <param name="Alturas">Los estribos de esta zona, de abajo hacia arriba.</param>
    public sealed record Zona(string Nombre, double ZDesde, double ZHasta, double Espaciado, IReadOnlyList<double> Alturas)
    {
        public int Cantidad => Alturas.Count;
    }

    /// <summary>
    /// Estribos seguidos a la misma separación: un conjunto de Revit.
    /// <paramref name="Separacion"/> es 0 cuando hay uno solo.
    /// </summary>
    /// <param name="Nombre">El de la <see cref="Zona"/> a la que pertenece.</param>
    public sealed record Corrida(string Nombre, double ZPrimero, int Cantidad, double Separacion)
    {
        public IEnumerable<double> Alturas => Enumerable.Range(0, Cantidad).Select(i => ZPrimero + i * Separacion);
    }

    /// <param name="Alturas">Eje del estribo principal de cada posición, de abajo hacia arriba, en metros.</param>
    /// <param name="Corridas">Las mismas alturas, partidas en conjuntos de separación constante, de abajo hacia arriba.</param>
    /// <param name="Aviso">El mismo aviso que daría el generador para este tramo, o null.</param>
    public sealed record Resultado(IReadOnlyList<double> Alturas, IReadOnlyList<Zona> Zonas, IReadOnlyList<Corrida> Corridas, string? Aviso);

    public static Resultado Calcular(double zInferior, double zSuperior, ConfigTramo cfg, PaqueteEstribos.Paquete paquete)
    {
        (double zInf, double zSup) = Limites(zInferior, zSuperior, paquete);
        double altura = zSup - zInf;

        if (altura <= Tolerancia)
            return new Resultado(Array.Empty<double>(), Array.Empty<Zona>(), Array.Empty<Corrida>(),
                $"no deja altura libre para los {SeparacionExtremosMetros * 100:0} cm de extremo en ambos bordes; se omite.");

        var alturas = new List<double>();
        var zonas = new List<Zona>();
        var corridas = new List<Corrida>();

        void Agregar(string nombre, double desde, double hasta, double espaciado, IReadOnlyList<double> posiciones)
        {
            if (posiciones.Count == 0) return;
            alturas.AddRange(posiciones);
            zonas.Add(new Zona(nombre, desde, hasta, espaciado, posiciones));
            corridas.AddRange(Partir(nombre, posiciones));
        }

        string? aviso = null;
        if (cfg.Modo == ModoEstribo.Uniforme)
        {
            if (cfg.EspaciadoUniforme > Tolerancia)
                Agregar("uniforme", zInf, zSup, cfg.EspaciadoUniforme, Repartir(zInf, zSup, cfg.EspaciadoUniforme));
        }
        else if (cfg.LongitudConfinamiento * 2 >= altura - Tolerancia)
        {
            aviso = "la longitud de confinamiento ×2 no deja zona central; se armó confinado de punta a punta.";
            if (cfg.EspaciadoConfinado > Tolerancia)
                Agregar("confinado (sin zona central)", zInf, zSup, cfg.EspaciadoConfinado, Repartir(zInf, zSup, cfg.EspaciadoConfinado));
        }
        else if (cfg.EspaciadoConfinado > Tolerancia)
        {
            double sc = cfg.EspaciadoConfinado, l1 = cfg.LongitudConfinamiento;
            int espacios = (int)Math.Floor(l1 / sc + Tolerancia);
            double zFinInferior = zInf + espacios * sc, zInicioSuperior = zSup - espacios * sc;

            List<double> inferior = Enumerable.Range(0, espacios + 1).Select(i => zInf + i * sc).ToList();
            List<double> superior = Enumerable.Range(0, espacios + 1).Select(i => zSup - (espacios - i) * sc).ToList();
            List<double> central = cfg.EspaciadoNoConfinado > Tolerancia
                ? Repartir(zFinInferior, zInicioSuperior, cfg.EspaciadoNoConfinado).Skip(1).SkipLast(1).ToList()
                : new List<double>();

            Agregar("confinado inferior", zInf, zInf + l1, sc, inferior);
            Agregar("zona central", zInf + l1, zSup - l1, cfg.EspaciadoNoConfinado, central);
            Agregar("confinado superior", zSup - l1, zSup, sc, superior);
        }

        return new Resultado(alturas, zonas, corridas, aviso);
    }

    /// <summary>
    /// Eje del estribo principal de la primera y la última posición del tramo,
    /// en metros: 5 cm al borde del paquete, y el punto medio del paquete
    /// corrido lo que el principal se aleja de él (sección 2 del diseño de
    /// estribos menores del 20-oct). Sin menores da 5 cm más medio diámetro.
    /// La usan este reparto y <c>GeneradorArmado.GenerarTramo</c>.
    /// </summary>
    public static (double ZInf, double ZSup) Limites(double zInferior, double zSuperior, PaqueteEstribos.Paquete paquete) =>
        (zInferior + SeparacionExtremosMetros + paquete.Debajo + paquete.DesplazamientoPrincipal,
         zSuperior - SeparacionExtremosMetros - paquete.Encima + paquete.DesplazamientoPrincipal);

    /// <summary>
    /// De <paramref name="desde"/> a <paramref name="hasta"/> (los dos con
    /// estribo) cada <paramref name="separacion"/>, con lo que sobra en el
    /// último espacio. Si sobra menos de <see cref="EspacioMinimoMetros"/>,
    /// los dos últimos espacios se igualan para que no queden dos estribos
    /// casi juntos: ninguno pasa de la separación. Copia de
    /// <c>DistribuidorEstribos.Repartir</c> de vigas, en metros.
    /// </summary>
    public static IReadOnlyList<double> Repartir(double desde, double hasta, double separacion)
    {
        double largo = hasta - desde;
        if (largo <= Tolerancia)
            return new[] { desde };
        int espacios = (int)Math.Ceiling(largo / separacion - Tolerancia);
        List<double> distancias = Enumerable.Range(0, espacios).Select(i => i * separacion).ToList();
        double sobra = largo - (espacios - 1) * separacion;
        if (espacios > 1 && sobra < EspacioMinimoMetros - Tolerancia)
            distancias[^1] = largo - (separacion + sobra) / 2;
        distancias.Add(largo);
        return distancias.Select(x => desde + x).ToList();
    }

    /// <summary>
    /// Parte posiciones ordenadas en corridas de separación constante (como
    /// <c>CorridasEstribos.Partir</c> de vigas): la separación pareja va en una
    /// corrida y el espacio que sobra abre otra.
    /// </summary>
    private static IEnumerable<Corrida> Partir(string nombre, IReadOnlyList<double> posiciones)
    {
        int i = 0;
        while (i < posiciones.Count)
        {
            int fin = i;
            if (i + 1 < posiciones.Count)
            {
                double separacion = posiciones[i + 1] - posiciones[i];
                fin = i + 1;
                while (fin + 1 < posiciones.Count && Math.Abs(posiciones[fin + 1] - posiciones[fin] - separacion) <= ToleranciaCorrida)
                    fin++;
            }

            int cantidad = fin - i + 1;
            yield return new Corrida(nombre, posiciones[i], cantidad, cantidad > 1 ? (posiciones[fin] - posiciones[i]) / (cantidad - 1) : 0);
            i = fin + 1;
        }
    }
}
