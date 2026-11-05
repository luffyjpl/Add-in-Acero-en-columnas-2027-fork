using CQEING.Vigas.Calculo.Modelo;

namespace CQEING.Zapatas.Calculo;

/// <summary>
/// Calcula las parrillas de una zapata, sin Revit (#251). Todo en mm y en los ejes de la zapata. Reglas de los planes de
/// #247 y #246: la principal va hacia afuera en cada parrilla (decisión 10), la superior corre media separación (11 y 21),
/// las patas llegan al recubrimiento opuesto (12 y 16) y las barras corridas no entran en la franja de patas (17 y 26).
/// </summary>
public static class MotorParrillas
{
    /// <summary>Tolerancia al comparar medidas, en mm: 1 mm de diferencia en el modelo no cambia nada.</summary>
    private const double Tolerancia = 1;

    /// <summary>La dirección principal: la larga que une las columnas, o la del lado largo; X si es cuadrada (decisión 10).</summary>
    public static Direccion Principal(ZapataEntrada z)
    {
        if (z.Columnas.Count >= 2)
        {
            double dx = z.Columnas.Max(c => c.X) - z.Columnas.Min(c => c.X);
            double dy = z.Columnas.Max(c => c.Y) - z.Columnas.Min(c => c.Y);
            if (Math.Abs(dx - dy) > Tolerancia)
                return dx > dy ? Direccion.X : Direccion.Y;
        }
        (double ancho, double alto) = Geometria.Caja(z.Contorno.ToList());
        return alto > ancho + Tolerancia ? Direccion.Y : Direccion.X;
    }

    public static ResultadoZapata Calcular(ZapataEntrada z, ConfigParrillas c, Reglas reglas)
    {
        var errores = new List<string>();
        Recubrimientos rec = c.Recubrimientos;
        foreach ((Direccion direccion, ConfigDireccion config) in new[] { (Direccion.X, c.X), (Direccion.Y, c.Y) })
        {
            if (!reglas.PorDiametro.ContainsKey(config.Numero))
                errores.Add($"El cuadro de ganchos no trae la barra #{config.Numero} de la dirección {direccion}.");
            if (config.Separacion <= 0)
                errores.Add($"La separación de la dirección {direccion} debe ser mayor que 0.");
        }
        if (rec.Inferior < 0 || rec.Lateral < 0 || rec.Superior < 0)
            errores.Add("Los recubrimientos no pueden ser negativos.");
        List<Punto> dentro = Geometria.HaciaAdentro(z.Contorno, rec.Lateral);
        if (dentro.Count == 0)
            errores.Add("La zapata no da para el recubrimiento de los lados.");
        double disponible = z.Altura - rec.Inferior - rec.Superior;
        if (errores.Count == 0)
        {
            double d = reglas.PorDiametro[c.X.Numero].Diametro + reglas.PorDiametro[c.Y.Numero].Diametro;
            double necesario = c.IncluirSuperior ? 2 * d : d;
            if (disponible < necesario)
                errores.Add($"La altura no alcanza: quedan {disponible:0} mm entre recubrimientos y las capas necesitan {necesario:0} mm.");
        }
        Direccion principal = errores.Count == 0 ? Principal(z) : Direccion.X;
        if (errores.Count > 0)
            return new ResultadoZapata([], new ResumenAcero(0, 0, 0), principal, [], errores);

        var conjuntos = new List<ConjuntoBarras>();
        var avisos = new List<string>();
        Direccion otra = principal == Direccion.X ? Direccion.Y : Direccion.X;
        // Los choques ya no se avisan con texto (decisión 27 del plan de #246): se marcan como puntos (TrazosZapata.Choques).
        foreach (Parrilla parrilla in c.IncluirSuperior ? new[] { Parrilla.Inferior, Parrilla.Superior } : new[] { Parrilla.Inferior })
        {
            conjuntos.AddRange(Capa(parrilla, principal, 1, 0));
            conjuntos.AddRange(Capa(parrilla, otra, 2, reglas.PorDiametro[Config(principal).Numero].Diametro));
        }
        var resumen = new ResumenAcero(conjuntos.Sum(b => b.Cantidad), conjuntos.Sum(b => b.Cantidad * b.LongitudCorte), Math.Round(conjuntos.Sum(b => b.Kilos), 1));
        return new ResultadoZapata(conjuntos, resumen, principal, avisos, []);

        ConfigDireccion Config(Direccion d) => d == Direccion.X ? c.X : c.Y;

        IEnumerable<ConjuntoBarras> Capa(Parrilla parrilla, Direccion direccion, int capa, double debajo)
        {
            ConfigDireccion config = Config(direccion);
            Extremos extremos = config.En(parrilla);
            ReglaDiametro regla = reglas.PorDiametro[config.Numero];
            double diametro = regla.Diametro;
            // La cara de la barra pegada a su recubrimiento, medida desde ese lado.
            double cara = (parrilla == Parrilla.Inferior ? rec.Inferior : rec.Superior) + debajo;
            double z0 = parrilla == Parrilla.Inferior ? cara + diametro / 2 : z.Altura - cara - diametro / 2;
            // La forma decide cuántas patas lleva una barra de 90°; sin ninguna es una barra recta (decisión 37).
            int patas = CuantasPatas(extremos);
            Terminacion terminacion = extremos.Terminacion == Terminacion.Pata90 && patas == 0 ? Terminacion.Recta : extremos.Terminacion;
            Patas dondePatas = terminacion switch { Terminacion.Recta => Patas.Ninguna, Terminacion.Pata90 => extremos.Patas, _ => Patas.Ambas };
            double pata = patas > 0 ? LargoDePata(parrilla, direccion, regla, disponible - debajo) : 0;
            if (patas > 0 && pata < ReglasZapata.DiametrosMinimosPata * diametro)
                Avisar($"Parrilla {Nombre(parrilla)}, dirección {direccion}: la pata de {pata:0} mm queda por debajo de 12 diámetros ({ReglasZapata.DiametrosMinimosPata * diametro:0} mm). Debe revisarlo el diseñador.");
            int largoGancho = ReglasZapata.LargoGancho(reglas, config.Numero, terminacion);

            List<double> posiciones = Posiciones(direccion, config, parrilla, capa);
            // Una línea paralela a X para las barras en X; para las de Y, el contorno se gira con los ejes intercambiados.
            IReadOnlyList<Punto> figura = direccion == Direccion.X ? dentro : dentro.Select(p => new Punto(p.Y, p.X)).ToList();
            var barras = new List<(double Posicion, double Desde, double Hasta)>();
            foreach (double posicion in posiciones)
                barras.AddRange(Geometria.Cortes(figura, posicion).Select(t => (posicion, t.Desde, t.Hasta)));
            if (barras.Count == 0)
                Avisar($"Parrilla {Nombre(parrilla)}, dirección {direccion}: no cabe ninguna barra.");

            // Las barras seguidas con los mismos extremos van en un conjunto; las otras, con su propio largo.
            return barras.OrderBy(b => Math.Round(b.Desde)).ThenBy(b => Math.Round(b.Hasta)).ThenBy(b => b.Posicion)
                .GroupBy(b => (Math.Round(b.Desde), Math.Round(b.Hasta)))
                .SelectMany(g => Seguidas(g.Select(b => (b.Posicion, b.Desde, b.Hasta)).ToList(), posiciones))
                .Select(g =>
                {
                    double largo = g[0].Hasta - g[0].Desde;
                    double neto = largo + patas * pata + 2 * largoGancho;
                    double corte = ReglasZapata.LongitudCorte(neto);
                    return new ConjuntoBarras(parrilla, direccion, capa, config.Numero, diametro, z0, g[0].Desde, g[0].Hasta, g.Select(b => b.Posicion).ToList(),
                        terminacion, pata, neto, corte, g.Count * corte / 1000 * regla.KgPorMetro, dondePatas,
                        terminacion is Terminacion.Gancho135 or Terminacion.Gancho180 ? extremos.Cola : null);
                });
        }

        // El largo de la pata de 90° (decisión 29): el B del cuadro, que no se sale del recubrimiento opuesto, o hasta él.
        double LargoDePata(Parrilla parrilla, Direccion direccion, ReglaDiametro regla, double hastaRecubrimiento)
        {
            int delCuadro = Reglas.LargoGancho(regla, TipoGancho.De90);
            if (c.LargoPata == LargoPata.Recubrimiento)
            {
                if (hastaRecubrimiento < delCuadro - Tolerancia)
                    Avisar($"Parrilla {Nombre(parrilla)}, dirección {direccion}: la pata hasta el recubrimiento ({hastaRecubrimiento:0} mm) es más corta que la del cuadro ({delCuadro} mm): no cumple el largo del cuadro.");
                return hastaRecubrimiento;
            }
            if (delCuadro <= hastaRecubrimiento + Tolerancia)
                return delCuadro;
            Avisar($"Parrilla {Nombre(parrilla)}, dirección {direccion}: la pata del cuadro ({delCuadro} mm) no cabe en la altura; se cortó en el recubrimiento, en {hastaRecubrimiento:0} mm.");
            return hastaRecubrimiento;
        }

        // Cuánto deja libre la otra dirección en una punta. La capa pegada al recubrimiento pasa por fuera de los dobleces de
        // la otra, que salen hacia adentro; la de encima tiene que entrar en la curva de la capa vecina: su eje queda en
        // recubrimiento + d de la pata + D / 2 (decisión 26). Sin pata ni gancho en esa punta, nada (decisión 37).
        double Franja(int numero, Extremos cruzada, int capa, double d, bool alFinal)
        {
            if (cruzada.Terminacion is Terminacion.Recta or Terminacion.Pata90 && CuantasPatas(cruzada) == 0)
                return 0;
            if (alFinal && cruzada.Terminacion == Terminacion.Pata90 && cruzada.Patas == Patas.Inicio)
                return 0;
            ReglaDiametro regla = reglas.PorDiametro[numero];
            if (capa == 1)
                return regla.Diametro;
            TipoGancho tipo = cruzada.Terminacion switch
            {
                Terminacion.Gancho135 => TipoGancho.De135,
                Terminacion.Gancho180 => TipoGancho.De180,
                _ => TipoGancho.De90,
            };
            double doblado = c.Doblados?.GetValueOrDefault(numero) is > 0 and double delTipo ? delTipo : Reglas.DobladoGancho(regla, tipo);
            return regla.Diametro + doblado / 2 - d / 2;
        }

        void Avisar(string texto)
        {
            if (!avisos.Contains(texto))
                avisos.Add(texto);
        }

        // Las coordenadas transversales: entre las franjas de patas de la otra dirección, sin pasarse de la separación pedida.
        List<double> Posiciones(Direccion direccion, ConfigDireccion config, Parrilla parrilla, int capa)
        {
            ConfigDireccion cruzada = Config(direccion == Direccion.X ? Direccion.Y : Direccion.X);
            double d = reglas.PorDiametro[config.Numero].Diametro;
            IEnumerable<double> coordenadas = dentro.Select(p => direccion == Direccion.X ? p.Y : p.X);
            double inicio = coordenadas.Min() + Franja(cruzada.Numero, cruzada.En(parrilla), capa, d, false) + d / 2;
            double fin = coordenadas.Max() - Franja(cruzada.Numero, cruzada.En(parrilla), capa, d, true) - d / 2;
            if (fin < inicio)
                return [];
            double util = fin - inicio;
            int espacios = Math.Max(0, (int)Math.Ceiling(Math.Round(util / config.Separacion, 6)));
            if (espacios == 0)
                return [(inicio + fin) / 2];
            double real = util / espacios;
            if (parrilla == Parrilla.Superior)
            {
                if (real / 2 < d)
                    Avisar($"Dirección {direccion}: con media separación ({real / 2:0} mm) los ganchos de las dos parrillas se pueden tocar.");
                return Enumerable.Range(0, espacios).Select(i => inicio + (i + 0.5) * real).ToList();
            }
            return Enumerable.Range(0, espacios + 1).Select(i => inicio + i * real).ToList();
        }
    }

    /// <summary>Separa las barras iguales en grupos de posiciones seguidas, sin saltarse ninguna de la capa.</summary>
    private static IEnumerable<List<(double Posicion, double Desde, double Hasta)>> Seguidas(List<(double Posicion, double Desde, double Hasta)> barras, List<double> posiciones)
    {
        var grupo = new List<(double Posicion, double Desde, double Hasta)>();
        int anterior = int.MinValue;
        foreach (var b in barras)
        {
            int indice = posiciones.IndexOf(b.Posicion);
            if (grupo.Count > 0 && indice != anterior + 1)
            {
                yield return grupo;
                grupo = [];
            }
            grupo.Add(b);
            anterior = indice;
        }
        if (grupo.Count > 0)
            yield return grupo;
    }

    /// <summary>Las patas de 90° de una barra: 2 en U, 1 en L y ninguna en una recta o con otro gancho.</summary>
    private static int CuantasPatas(Extremos e) =>
        e.Terminacion != Terminacion.Pata90 ? 0 : e.Patas switch { Patas.Ambas => 2, Patas.Inicio => 1, _ => 0 };

    private static string Nombre(Parrilla p) => p == Parrilla.Inferior ? "inferior" : "superior";
}
