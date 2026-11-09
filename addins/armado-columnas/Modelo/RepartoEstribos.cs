namespace practicacq.Modelo;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Alturas de los estribos de un tramo, sin Revit. Regla del 07-nov
/// (docs/planes/2026-11-07-estribos-centrados-y-fallas-santiago.md, decisiones
/// 2 y 3; reemplaza la decisión 9 del #256): todos los espacios a la
/// separación exacta, sin espacio corto. Los 5 cm al borde del paquete son un
/// mínimo: van los estribos que entren y el grupo se centra, así que los
/// bordes pueden quedar a más de 5 cm.
///
/// Las confinadas van exactas desde 5 cm de su punta; la central, exacta y
/// centrada entre el último confinado de abajo y el primero de arriba, sin
/// quedar a menos de la separación del confinado de ninguno.
///
/// Columnas ya no sigue a vigas (<c>DistribuidorEstribos.Repartir</c> deja el
/// último espacio más corto): decisión 1 del plan del 07-nov.
///
/// La usan la vista previa (para mostrar lo mismo que se va a generar) y
/// <c>GeneradorArmado</c>, que crea un conjunto de Revit por cada
/// <see cref="Corrida"/>: con esta regla, uno por zona.
/// </summary>
public static class RepartoEstribos
{
    /// <summary>Del borde del tramo al borde del paquete, en cada punta.</summary>
    public const double SeparacionExtremosMetros = 0.05;

    private const double Tolerancia = 1e-6;

    /// <summary>Dos separaciones que difieren menos que esto (0,1 mm) son la misma corrida.</summary>
    private const double ToleranciaCorrida = 1e-4;

    /// <param name="Nombre">El rótulo del grupo: «uniforme», «confinado inferior», «zona central», «confinado superior» o «confinado (sin zona central)».</param>
    /// <param name="ZDesde">Donde empieza la zona configurada (en las confinadas, L1 desde su punta).</param>
    /// <param name="Espaciado">La separación pedida: la real entre todos los estribos de la zona.</param>
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
                Agregar("uniforme", zInf, zSup, cfg.EspaciadoUniforme, Centrados(zInf, zSup, cfg.EspaciadoUniforme));
        }
        else if (cfg.LongitudConfinamiento * 2 >= altura - Tolerancia)
        {
            aviso = "la longitud de confinamiento ×2 no deja zona central; se armó confinado de punta a punta.";
            if (cfg.EspaciadoConfinado > Tolerancia)
                Agregar("confinado (sin zona central)", zInf, zSup, cfg.EspaciadoConfinado, Centrados(zInf, zSup, cfg.EspaciadoConfinado));
        }
        else if (cfg.EspaciadoConfinado > Tolerancia)
        {
            // Decisión 3: los confinados fijos desde 5 cm de cada punta, tantos
            // como entren en L1; la central centrada entre ellos.
            double sc = cfg.EspaciadoConfinado, sn = cfg.EspaciadoNoConfinado, l1 = cfg.LongitudConfinamiento;
            int espacios = (int)Math.Floor(l1 / sc + Tolerancia);
            double ultimoInferior = zInf + espacios * sc, primeroSuperior = zSup - espacios * sc;

            List<double> inferior = Enumerable.Range(0, espacios + 1).Select(i => zInf + i * sc).ToList();
            List<double> superior = Enumerable.Range(0, espacios + 1).Select(i => primeroSuperior + i * sc).ToList();
            List<double> central = sn > Tolerancia
                ? Centrados(ultimoInferior + sc, primeroSuperior - sc, sn, vacioSiNoEntra: true).ToList()
                : new List<double>();

            Agregar("confinado inferior", zInf, zInf + l1, sc, inferior);
            Agregar("zona central", zInf + l1, zSup - l1, sn, central);
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
    /// Entre <paramref name="desde"/> y <paramref name="hasta"/>, todos los
    /// estribos que entren cada <paramref name="separacion"/> exacta, con el
    /// grupo centrado: lo que sobra (menos de una separación) se reparte mitad
    /// en cada punta. Si el largo es menor que una separación va uno solo, en
    /// el medio; con <paramref name="vacioSiNoEntra"/> y un largo negativo no
    /// va ninguno (la zona central sin lugar). Decisión 2 del plan del 07-nov.
    /// </summary>
    public static IReadOnlyList<double> Centrados(double desde, double hasta, double separacion, bool vacioSiNoEntra = false)
    {
        double largo = hasta - desde;
        if (largo < -Tolerancia)
            return vacioSiNoEntra ? Array.Empty<double>() : new[] { (desde + hasta) / 2 };
        if (largo <= Tolerancia)
            return new[] { (desde + hasta) / 2 };

        int espacios = (int)Math.Floor(largo / separacion + Tolerancia);
        double primero = desde + (largo - espacios * separacion) / 2;
        return Enumerable.Range(0, espacios + 1).Select(i => primero + i * separacion).ToList();
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
