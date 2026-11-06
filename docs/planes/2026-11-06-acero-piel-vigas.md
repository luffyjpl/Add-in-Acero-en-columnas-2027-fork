# Plan: acero de piel en vigas (reunión de programadores 6, pedido de Santiago y Carlos)

## Contexto

- **Qué se pide.** En la reunión de programadores 6 (transcripción `Desktop\addin vigas\Reunión Eqpo. Programadores6.txt`,
  min 53:51–57:46) Santiago pidió que el add-in de acero en vigas pueda poner **piel**: barras en los dos costados de
  la viga, además de las de arriba y abajo. Carlos lo confirmó: es para control a torsión y, como no se sabe de antemano
  cuándo se necesita, es una **opción adicional en todas las vigas** (aéreas y de cimentación, min 56:03).
- **Por qué ahora.** Es lo único de vigas de esa reunión que sigue sin hacer (los bastones al límite y su largo
  escrito ya están en la rama `vigas/bastones-al-limite`, commit `3f7d239`).
- **Fuera de alcance.**
  - Un lado distinto del otro (cantidad o diámetro): los dos lados son siempre espejo.
  - Cantidad de piel distinta por tramo: es una para todo el pórtico.
  - Decidir cuándo hace falta piel (torsión): lo decide el usuario.
  - Más de un diámetro en la piel ("+ Otro diámetro"): un solo grupo.
- **Entorno.** Revit 2026 (2027 no está instalado), add-in `addins/acero-vigas`, cálculo en
  `addins/acero-vigas/calculo/CQEING.Vigas.Calculo`, tablero web en `addins/acero-vigas/Tablero/web`.
  Compilar e instalar: `tools/desplegar-local.ps1 -Addin acero-vigas -Revit 2026` con Revit cerrado.

## Lo que dijeron (fuente de verdad)

- Santiago (53:51): "hay algo que se llama piel… barras que van de este lado… en cimentación o dependiendo la
  construcción también necesitamos barras a los lados".
- Carlos (55:21): repartidas "de manera equidistante".
- Carlos (55:35): el traslapo, "como si fuera… el mismo traslapo de la barra… de arriba".
- Carlos y Santiago (56:31–57:37): el número es **por lado** y sale igual en los dos costados: "cinco un lado y cinco
  del otro". Se escribe 5, no 10. Nadie dijo que cada lado se ponga a mano.
- Carlos (56:45): se elige la cantidad "y el tipo de barra, más nada".

## Criterios de aceptación

- [ ] En el paso **Barras** del tablero hay una tercera fila, **"Barras en los lados"**, debajo de "Barras abajo",
      siempre visible, con los mismos controles que las otras dos (− n +, "por lado", punto de color, diámetro).
      Arranca en **0 = sin piel**.
- [ ] La fila dice cuántas caben por lado (como "caben N por fila") y, en gris, "Las mismas a los dos lados,
      repartidas a igual distancia entre arriba y abajo".
- [ ] Con n por lado salen **2n barras**: n en cada costado, mismo diámetro, a la misma altura una frente a la otra.
- [ ] En el **corte 1-1**, la piel va pegada al estribo por dentro en los dos costados, repartida a igual distancia
      entre la fila más baja de arriba y la más alta de abajo. Círculo del color de su diámetro, sin letra propia;
      al pasar el cursor dice "Piel #4 · izquierda 1" (o derecha).
- [ ] En el **alzado**, una línea delgada por cada altura de piel (los dos lados caen en la misma), del color de su
      diámetro, más delgada que las corridas. Un solo rótulo para toda la piel: "2#4 c/lado L=…". Si con varias
      alturas el alzado queda apretado, el alzado crece en alto para que se lea.
- [ ] La piel **corre todo el pórtico** y **llega al límite** en las dos puntas, como los bastones del borde tras
      el commit `3f7d239`: la punta queda en `InicioBarras`/`FinBarras` y, si lleva gancho, baja por la línea del
      gancho de las corridas (no se anida por dentro).
- [ ] Cada punta tiene los botones de gancho de los bastones (**Sin · 90° · 135° · 180°**). **Por defecto: sin
      gancho.** Lo que se elige en una punta vale para los dos lados.
- [ ] Si la piel es más larga que la varilla, **traslapa como la capa de arriba**: misma referencia de traslapo que
      la capa superior (centro de la luz en aérea, junto a la cara en cimentación, ver
      `GeometriaPortico.ReferenciaDe`) y mismo largo de traslapo por diámetro. El traslapo se ve con el rectángulo
      gris de siempre y se puede mover igual.
- [ ] Las barras de piel se pueden seleccionar en el alzado como las demás (halo, panel de la pieza).
- [ ] Los kg de la piel suman al total del pórtico y salen en el resumen.
- [ ] "Generar en Revit" crea la piel en las dos caras con sus ganchos y traslapos, del color de su diámetro, como el
      resto del acero.
- [ ] Con 0 por lado, nada cambia respecto de hoy (mismos resultados, mismas pruebas en verde).

## Diseño visual acordado (maquetas del 06-nov, opción A en panel y A en alzado)

Maqueta: `Desktop\addin vigas\.superpowers\brainstorm\312-1793984024\content\piel-vistas.html`.

- **Panel:** opción A, fila siempre visible, igual a las de arriba y abajo. Debe verse como si siempre hubiera estado:
  mismos estilos inline de `tablero.js` (bordes `#C9CFD6`, gris `#5E6670`, fuente IBM Plex), nada de colores nuevos.
- **Corte:** círculos un poco más chicos si el diámetro es menor, pegados al estribo; sin cotas nuevas obligatorias.
- **Alzado:** opción A, una línea por altura, trazo fino (como los bastones, ~1.2–1.7), color del diámetro.
- Que descanse la vista: la piel nunca debe competir con las corridas; va más delgada y su rótulo es uno solo.

## Hallazgos del código

- Las capas hoy son `Posicion.Superior` / `Posicion.Inferior` (`Modelo/Resultado.cs`, `SolucionCapa`); la entrada es
  `EntradaVigas` (`Modelo/EntradaVigas.cs`) con `Superior`, `Inferior` y `Bastones`.
- El reparto en el corte está en `Seccion/` (`CalculadoraCorte.cs`, `RepartidorFilas.cs`, `UbicadorBastones.cs`).
- Las puntas en el borde, ganchos y el "llega al límite" de los bastones: `Vigas/CalculadorBastones.cs` y
  `Generacion/PlanificadorAcero.cs` (`AnidarGanchos` con `alLimite`).
- Colores por diámetro: `Modelo/ColoresDiametro.cs` (#4 azul `#4285F4`, #5 rojo `#EA4335`…).
- Panel del paso Barras: `Tablero/web/tablero.js` (bloque `v.isBarras`, capas en `layers`, ~línea 551). Alzado:
  `Tablero/web/alzado.js` (barras ~línea 142, bastones ~línea 173).
- Recubrimiento de los lados ya existe (`recSide`, "Lados" en "más opciones").
- Todo el pórtico usa una sola sección: `CalculoPortico.Seccion` toma `p.Tramos[0]` y `MarcoPortico` un solo `ZAbajo`.
  Por eso la piel con tramos de distinta altura (decisión 8) se ubica con `p.Tramos.Min(t => t.Alto)`, conservando la
  distancia de la fila de arriba a la cara de arriba.
- La vigueta es la que lleva estribo abierto: `CalculoPortico` pone `TipoEstribo.Abierto` como principal cuando
  `EsVigueta`. La decisión 10 se cumple bloqueando la piel en vigueta.
- `AnidarGanchos` agrupa por `Capa` y mete por dentro las piezas en la misma X con distinta Y: las piezas de piel
  (izquierda y derecha, varias alturas) se agregan después, para que no se anidaran entre ellas.
- `CreadorBarras.Voltear` decide el sentido del gancho por la capa; la piel necesita uno por altura.

## Decisiones

1. **Cantidad por lado, espejo** (2026-11-06, Carlos y Santiago en reunión 6; confirmado por Juan): un número, sale
   igual en los dos costados.
2. **Fila siempre visible en el paso Barras, en 0** (2026-11-06, Juan): opción A de la maqueta.
3. **Alzado: una línea fina por altura** (2026-11-06, Juan): opción A; el alzado puede crecer en alto si hace falta.
4. **Puntas al límite, como los bastones** (2026-11-06, Juan): gancho por la línea del de las corridas.
5. **Gancho por punta con Sin/90/135/180, por defecto sin gancho** (2026-11-06, Juan).
6. **Traslapo como la capa de arriba** (2026-11-06, Carlos en reunión 6, min 55:35; Juan lo confirma).
7. **Aérea y cimentación** (2026-11-06, Carlos en reunión 6, min 56:03).
8. **Pórtico con tramos de distinta altura** (2026-11-06, Juan): la piel se reparte con la viga **más baja** del
   pórtico y queda a la misma altura en todo él, recta. "Caben N por lado" se cuenta con esa altura.
9. **Dirección del gancho** (2026-11-06, Juan): vertical, en la misma línea del gancho de las corridas; las alturas de
   la mitad de arriba **suben** y las de la mitad de abajo **bajan** (hacia la capa más cercana). Con altura impar, la
   del medio baja.
10. **Vigueta y estribo abierto** (2026-11-06, Juan): con estribo **abierto** la fila se ve pero no sube de 0, con la
    nota gris "No lleva piel con estribo abierto". En vigueta con estribo cerrado, normal.
11. **Traslapo propio con la regla de arriba** (2026-11-06, decisión de Claude): la piel tiene su propio cálculo de
    traslapo (su diámetro, su largo de traslapo), con la misma referencia que la capa superior y la **misma cara que
    quedó elegida para la capa de arriba** cuando la referencia es de cara. Su traslapo se puede mover aparte y se
    guarda como los demás cambios manuales.
12. **Rama** (2026-11-06, decisión de Claude): se trabaja sobre `vigas/bastones-al-limite` (usa el commit `3f7d239`) y
    la PR va contra `main` con todo junto, para no dejar PR apiladas varadas.

## Lo que queda al que lo programa

- Cómo se modela la piel en el cálculo (una `Posicion` nueva, una capa aparte, o un record propio) y en el protocolo
  tablero ↔ C#, mientras se respete lo de arriba y las pruebas actuales sigan en verde.
- Cuántas caben por lado: con la separación libre mínima de las reglas entre la fila de arriba y la de abajo.
- Cómo se nombran los ids de la piel (sugerencia: `PI1`, `PI2`… por altura).
- Cuánto crece el alzado en alto con varias alturas de piel.
- El host en Revit y el lado del estribo de cada barra al generar, siguiendo lo que hoy hace `PlanificadorAcero`.

## Plan paso a paso

> **Para agentes:** usar superpowers:subagent-driven-development o superpowers:executing-plans, tarea por tarea. Los
> pasos usan casillas (`- [ ]`).

**Meta:** que el usuario elija "n por lado" de piel en el paso Barras y la vea en el corte, en el alzado y en Revit.

**Arquitectura:** la piel es una tercera capa del motor (`Posicion.Piel`) que se resuelve con el mismo
`ResolvedorCapa` que las corridas (traslapo con la referencia de la capa de arriba, puntas en la geometría común, que
llega al límite). Su ubicación en el corte la da un repartidor propio (`RepartidorPiel`). El plan de Revit la agrega
como piezas de `Barras`, sin anidar, con el sentido del gancho por altura.

**Stack:** C# .NET 8 (cálculo, xUnit), Revit API 2026 (`Revit/`), página Preact/htm sin build (`Tablero/web`).

### Restricciones globales

- Con piel 0 (o sin el campo en el JSON) todo queda igual: las 1091 pruebas actuales siguen en verde sin tocarlas.
- Ids: barras de piel `P1`, `P2`… (como `S1`, `I1`); traslapos `P1–P2` (mismo formato que los de arriba); ganchos de
  punta `"P.inicio"` / `"P.fin"` en `CambiosManuales.Ganchos`. En el corte: `pi1`, `pi2`… (izquierda) y `pd1`, `pd2`…
  (derecha), de abajo hacia arriba.
- Gancho de la piel por defecto: `TipoGancho.Sin`, y "Sin" es válido (a diferencia de las corridas).
- Color: `ColoresDiametro.De(diámetro)`. Rótulo del alzado: `"2#4 c/lado L=5.40"`; en pulgadas `"2Ø½\" c/lado L=5.40"`.
- Commits sin `Co-Authored-By` ni firma. Mensajes en español, estilo `feat(vigas): …`.
- Probar en Revit, no en Chrome con la demostración.

### Atención en la revisión

1. **Piel con más alturas de las que caben**: el contador no deja pasar de `CabenPorLado` y el motor no revienta si
   le llega más (lo recorta y avisa). Prueba en la tarea 2.
2. **Pórtico con tramos de distinta altura**: las alturas salen con el tramo más bajo, medidas desde arriba como las
   corridas. Prueba en la tarea 2.
3. **Gancho de piel que sube por la misma línea que el gancho de arriba que baja**: el detector de choques lo debe
   reportar como choque (es lo esperado, no un error que esconder). Prueba en la tarea 4.
4. **Vigueta**: aunque el JSON traiga piel, el motor no la pone y avisa. Prueba en la tarea 3.
5. **Estado guardado viejo** (sin `piel`): abre igual, en 0. Prueba en la tarea 3.

---

### Tarea 1: la piel en el motor

**Archivos:**
- Modificar: `calculo/CQEING.Vigas.Calculo/Modelo/Entrada.cs` (enum `Posicion`), `Modelo/EntradaVigas.cs`,
  `Modelo/Resultado.cs` (`ResultadoVigas`), `MotorVigas.cs`.
- Crear prueba: `calculo/CQEING.Vigas.Calculo.Tests/PielTests.cs`.

**Interfaces:**
- Produce: `Posicion.Piel` (tercer valor del enum, al final); `EntradaVigas(..., GrupoBarras? Piel = null)` donde
  `Piel.Cantidad` es **por lado**; `ResultadoVigas(..., SolucionCapa? Piel = null)` (último parámetro) y su `Capas`
  incluye la piel, así `Barras`, `Traslapos`, `Kg` y `ZonasTraslapo` la cuentan. Cada altura de piel es una barra
  por lado, así que con n por lado hay n alturas y 2n barras físicas: las `Barra` de piel llevan
  `Grupos = [new GrupoBarras(2 * n, d)]`.

- [ ] **Paso 1: pruebas que fallan** (`PielTests`, con `Datos.PorticoMaqueta()` y `Datos.ReglasSinRedondeo`, como
  `BastonesTests`):
  - `SinPiel_ElResultadoNoCambia`: `Entrada(piel: null)` y `Entrada(piel: new GrupoBarras(0, 4))` dan `r.Piel == null`
    y el mismo `Kg` que hoy.
  - `ConPiel_CorreTodoElPorticoHastaElLimite`: piel `(2, 4)` → `r.Piel!.Barras.First().Desde == -150` y
    `r.Piel.Barras.Last().Hasta == 13950` (las puntas de `BastonesTests`).
  - `ConPiel_PorDefectoSinGancho`: `TipoInicio` de la primera y `TipoFin` de la última son `TipoGancho.Sin`.
  - `ConPiel_ElGanchoElegidoSeAplica`: `Cambios.Ganchos["P.inicio"] = De90` → la primera barra lleva `De90` y
    `GanchoInicio > 0`; `"P.fin" = Sin` no avisa.
  - `ConPiel_TraslapaConLaReferenciaDeArriba`: aérea → `r.Piel.Referencia == Referencia.Centro`; cimentación →
    `r.Piel.Referencia` es la de `r.Superior.Referencia` (la misma cara que quedó arriba).
  - `ConPiel_LosKgCuentanLosDosLados`: piel `(2, 4)` → `r.Kg - rSinPiel.Kg` igual a la suma de `Largo/1000 * 4 *
    KgPorMetro(#4)` de las barras de piel (2 alturas × 2 lados = 4 barras físicas).
- [ ] **Paso 2:** `rtk dotnet test CQEING.Vigas.Calculo.Tests --filter PielTests` → fallan por compilación.
- [ ] **Paso 3: implementar.**
  - `Posicion.Piel`; revisar los `capa == Posicion.Superior ? … : …` de `Seccion/`, `Estribos/AlineadorEstribos.cs` y
    `Generacion/` para que la piel nunca entre por esas ramas (no llega ahí: el corte y los estribos solo reciben
    `Superior`/`Inferior`).
  - En `MotorVigas.Calcular`, después de calcular las capas: si `entrada.Piel is { Cantidad: > 0 }`, resolver con un
    `Resolver` generalizado: grupos `[new GrupoBarras(2 * Piel.Cantidad, Piel.Diametro)]`, geometría `g` común (llega al
    límite), referencia `GeometriaPortico.ReferenciaDe(Tipo, Posicion.Superior, caraDeArriba)` donde `caraDeArriba` es
    `usada` si la capa de la cara es la superior y `Cara.Inicio` si no, prefijo `"P"`, ganchos con un `GanchoPiel`
    que devuelve el de `Cambios.Ganchos` o `TipoGancho.Sin` (sin aviso). Aplicar `AplicadorCambios` como a las demás.
  - Sin solución → aviso `SinSolucion` con elemento `"Piel"` (ya bloquea generar).
  - `DiametrosSinTraslapo` incluye el diámetro de la piel.
- [ ] **Paso 4:** `rtk dotnet test CQEING.Vigas.Calculo.Tests` → todo en verde.
- [ ] **Paso 5: commit** `feat(vigas): piel en el motor, con traslapo como la capa de arriba`.

### Tarea 2: la piel en el corte

**Archivos:**
- Crear: `calculo/CQEING.Vigas.Calculo/Seccion/RepartidorPiel.cs`; agregar records en `Modelo/Seccion.cs`.
- Crear prueba: `calculo/CQEING.Vigas.Calculo.Tests/RepartidorPielTests.cs`.

**Interfaces:**
- Produce:
  - `public sealed record PielEnSeccion(int Altura, BarraEnSeccion Izquierda, BarraEnSeccion Derecha);` (`Altura` de
    1, la más baja, hacia arriba).
  - `public sealed record UbicacionPiel(IReadOnlyList<PielEnSeccion> Alturas, int CabenPorLado);`
  - `public static UbicacionPiel RepartidorPiel.Ubicar(EntradaSeccion e, ResultadoSeccion s, GrupoBarras piel, double altoMinimo)`
  - `public static bool RepartidorPiel.GanchoArriba(int altura, int alturas) => 2 * altura > alturas + 1;`

- [ ] **Paso 1: pruebas que fallan** (viga 250×500, rec 40, estribo #3, 3#5 arriba y abajo):
  - `DosPorLado_AIgualDistanciaEntreArribaYAbajo`: `Alturas.Count == 2`; las Y son `yAbajo + k·(yArriba − yAbajo)/3`
    (k = 1, 2), con `yAbajo` la Y más alta de las filas de abajo y `yArriba` la más baja de las de arriba.
  - `PegadaAlEstriboPorDentro`: `Izquierda.X == 40 + Ø#3 + Ø#4/2` y `Derecha.X == 250 − Izquierda.X`; misma Y.
  - `CabenPorLado_ConLaDistanciaLibre`: `CabenPorLado` es el mayor n con
    `(yArriba − yAbajo)/(n+1) ≥ Ø piel + Reglas.DistanciaLibre`.
  - `MasDeLasQueCaben_SeRecorta`: piel `(9, 4)` → `Alturas.Count == CabenPorLado`.
  - `ConTramoMasBajo_UsaElMasBajo`: `altoMinimo = 400` con la sección de 500 → `yArriba` baja 100 (la distancia de
    la fila de arriba a la cara de arriba se conserva).
  - `GanchoArriba`: `(1,2)→false`, `(2,2)→true`, `(1,3)→false`, `(2,3)→false`, `(3,3)→true`, `(1,1)→false`.
- [ ] **Paso 2:** correr `--filter RepartidorPielTests` → fallan.
- [ ] **Paso 3:** implementar `RepartidorPiel` (con `s.Superior.Filas`/`s.Inferior.Filas`; si alguna cara tiene
  `Error != Ninguno`, `Alturas` vacía y `CabenPorLado` 0).
- [ ] **Paso 4:** todas las pruebas en verde.
- [ ] **Paso 5: commit** `feat(vigas): reparto de la piel en el corte`.

### Tarea 3: opciones del tablero, protocolo y vigueta

**Archivos:**
- Modificar: `calculo/CQEING.Vigas.Calculo/Tablero/OpcionesTablero.cs`, `Tablero/CalculoPortico.cs`,
  `Tablero/ProtocoloTablero.cs` (`Opciones`), `Modelo/Resultado.cs` (`TipoAviso.PielNoPermitida`).
- Pruebas: `CQEING.Vigas.Calculo.Tests/ProtocoloTableroTests.cs`, `PielTests.cs`.

**Interfaces:**
- Consume: `EntradaVigas.Piel` (tarea 1), `RepartidorPiel.Ubicar` (tarea 2).
- Produce: `OpcionesTablero(..., GrupoBarras? Piel = null)` (último parámetro; JSON `"piel": {"cantidad": 2,
  "diametro": 4}`); `PorticoCalculado(..., UbicacionPiel? Piel = null)`; `TipoAviso.PielNoPermitida` (vigueta).

- [ ] **Paso 1: pruebas que fallan.**
  - `Opciones_SinPiel_EsNull` y `Opciones_ConPiel_LaLee` (JSON con y sin `"piel"`).
  - `Calcular_ConPiel_UbicaLaPielConElTramoMasBajo`: pórtico de dos tramos de 500 y 400 de alto →
    `c.Piel!.Alturas` con la Y de la tarea 2 para `altoMinimo = 400`.
  - `Calcular_Vigueta_NoPonePielYAvisa`: `Elemento.Vigueta` + piel `(2, 4)` → `c.Vigas.Piel == null`, aviso
    `PielNoPermitida`, y `SePuedeGenerar` no cambia por eso.
  - `DiametrosSinReglas_IncluyeLaPiel`: piel `#2` (sin reglas) → la lista trae 2.
- [ ] **Paso 2:** correr → fallan.
- [ ] **Paso 3:** en `Calcular`, antes del motor: armar `seccion`, y si hay piel y no es vigueta,
  `RepartidorPiel.Ubicar(seccion, RepartidorFilas.Repartir(seccion), o.Piel, p.Tramos.Min(t => t.Alto))`. La entrada
  del motor (`EntradaDe`) lleva `Piel` con la cantidad recortada a `Alturas.Count` (null si es 0 o si es vigueta). En
  vigueta con piel pedida, aviso `PielNoPermitida` (no bloquea generar).
- [ ] **Paso 4:** todas en verde.
- [ ] **Paso 5: commit** `feat(vigas): piel en las opciones del tablero`.

### Tarea 4: la piel en lo que se genera

**Archivos:**
- Modificar: `calculo/CQEING.Vigas.Calculo/Generacion/PlanAcero.cs` (`PiezaLongitudinal`),
  `Generacion/PlanificadorAcero.cs`, `Revit/CreadorBarras.cs` (`Voltear`).
- Prueba: `CQEING.Vigas.Calculo.Tests/PlanificadorAceroTests.cs`, `DetectorChoquesTests.cs`.

**Interfaces:**
- Consume: `PorticoCalculado.Piel` (tarea 3), `RepartidorPiel.GanchoArriba`.
- Produce: `PiezaLongitudinal(..., int Fila = 1, bool? GanchoArriba = null)`; null = como hoy, por la capa.

- [ ] **Paso 1: pruebas que fallan.**
  - `ConPiel_DosPiezasPorAlturaYBarra`: piel `(2, 4)` → por cada barra `P*` hay 4 piezas en `plan.Barras` con
    `Capa == Posicion.Piel`, `Fila` 1 y 2, y en cada fila una con `XCorte` de la izquierda y otra de la derecha.
  - `ConPiel_LlegaAlLimiteSinAnidar`: con gancho 90° en `"P.inicio"`, la primera pieza tiene `Desde ==` el `Desde`
    de la corrida de arriba (no se corre hacia adentro).
  - `ConPiel_GanchoArribaPorAltura`: 2 alturas → `Fila 1: GanchoArriba == false`, `Fila 2: true`.
  - `AgrupadorBarras` junta izquierda y derecha de una misma fila en un conjunto de 2.
  - En `DetectorChoquesTests`: `PielConGancho90_ChocaConElGanchoDeArriba` reporta el choque (Atención 3).
- [ ] **Paso 2:** correr → fallan.
- [ ] **Paso 3:** en `Planificar`, armar las piezas de piel de `c.Vigas.Piel.Barras` × `c.Piel.Alturas` × {izquierda,
  derecha}, con `Host(u, …)` como las corridas, y agregarlas a `barras` **después** de `AnidarGanchos` (no se anidan).
  En `CreadorBarras.Voltear`, recibir la pieza y usar `p.GanchoArriba` cuando no es null
  (`true` → la punta debe quedar más alta que la barra).
- [ ] **Paso 4:** todas en verde; `rtk dotnet build` del add-in con `-p:RevitVersion=2026` sin errores.
- [ ] **Paso 5: commit** `feat(vigas): generar la piel en Revit`.

### Tarea 5: la piel en la vista (C#)

**Archivos:**
- Modificar: `Tablero/VistaTablero.cs`, `Tablero/ArmadorVista.cs`, `Tablero/Rotulo.cs`.
- Prueba: `CQEING.Vigas.Calculo.Tests/ArmadorVistaTests.cs`.

**Interfaces:**
- Consume: `ResultadoVigas.Piel`, `PorticoCalculado.Piel`.
- Produce:
  - `BarraVista.Capa == "piel"` para las barras de piel; `TraslapoVista.Capa == "piel"`.
  - `BarraSeccion(..., bool Piel = false)`; las de piel con ids `pi1…`/`pd1…` y `Nombre` `"Piel #4 · izquierda 1"`.
  - `public sealed record PielVista(int Alturas, int CabenPorLado, string Rotulo, string RotuloPulgadas, bool Permitida);`
  - `VistaPortico(..., PielVista? Piel = null)` (último parámetro).
  - `Rotulo.Piel(int porLado, int diametro, int largo)` → `"2#4 c/lado L=5.40"`;
    `Rotulo.PielEnPulgadas(...)` → `"2Ø½\" c/lado L=5.40"`. `largo` es el de la barra más larga de piel.

- [ ] **Paso 1: pruebas que fallan:** `Vista_ConPiel_BarrasConCapaPiel`, `Vista_ConPiel_CorteConPiel`
  (4 `BarraSeccion` con `Piel`, ids `pi1, pi2, pd1, pd2`), `Vista_ConPiel_Rotulo` (`"2#4 c/lado L=…"`),
  `Vista_Vigueta_PielNoPermitida` (`Piel.Permitida == false`), `Vista_SinPiel_PielNull`.
- [ ] **Paso 2:** correr → fallan.
- [ ] **Paso 3:** implementar; `Diametros` de la leyenda incluye el de la piel.
- [ ] **Paso 4:** todas en verde.
- [ ] **Paso 5: commit** `feat(vigas): la piel en la vista del tablero`.

### Tarea 6: la piel en la página (panel, corte y alzado)

**Archivos:**
- Modificar: `Tablero/web/tablero.js` (estado, `opciones`, panel `v.isBarras`, corte), `Tablero/web/alzado.js`.

**Interfaces:**
- Consume: `vista.actual.piel` (`PielVista`), barras con `capa === 'piel'`, `seccion.barras[].piel`.
- Produce en el estado de la página: `pielN: '0'`, `pielD: '#4'`; en las opciones: `piel: n > 0 ? { cantidad, diametro } : null`.

- [ ] **Paso 1: panel.** Debajo de "Barras abajo" (antes de "Recubrimiento"), la fila "Barras en los lados" con los
  mismos estilos inline de las otras: título, "caben N por lado" (de `piel.cabenPorLado`), contador − n + (tope
  `cabenPorLado`), texto "por lado", punto de color, `select` de diámetro (`opcionesDiametro`), y la nota gris
  "Las mismas a los dos lados, repartidas a igual distancia entre arriba y abajo." Con `piel.permitida === false`: "+"
  deshabilitado y la nota "No lleva piel con estribo abierto".
- [ ] **Paso 2: corte.** Las `BarraSeccion` con `piel` se dibujan como las demás (círculo del color), sin número ni
  letra, con `title`/tooltip = `nombre`, y no son elegibles para estribos (como `baston`).
- [ ] **Paso 3: alzado.** Barras `capa === 'piel'`: una polilínea por altura (`piel.alturas`) con `conGanchos`, trazo
  1.2 (más delgado que corridas 2.4 y bastones 1.7), color de su diámetro, repartidas a igual distancia entre y = 76 y
  y = 94; con más de 3 alturas el alzado crece en alto lo necesario para dejar al menos 6 unidades entre líneas. Un
  solo rótulo (`piel.rotulo` o `rotuloPulgadas`) sobre la altura de arriba. Botones de gancho en las puntas del borde
  (Sin · 90° · 135° · 180°, como los bastones) que escriben `"P.inicio"`/`"P.fin"`. Traslapos de piel como los de
  arriba (rectángulo gris, se mueven igual). Selección con halo, como las corridas.
- [ ] **Paso 4: compilar e instalar** `powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin
  acero-vigas -Revit 2026` (Revit cerrado) y revisar en Revit los puntos 1–3 y 6 de "Cómo probarlo".
- [ ] **Paso 5: commit** `feat(vigas): piel en el panel, el corte y el alzado del tablero`.

### Tarea 7: prueba completa en Revit y cierre

- [ ] Pasos 1–6 de "Cómo probarlo" en Revit 2026, con la plantilla correcta de CQEING.
- [ ] Actualizar "Estado" de este documento, criterio por criterio, con el commit de cada cosa.
- [ ] Commit `docs(vigas): estado del plan de piel` y subir la rama a `origin`; PR contra `main`.

## Cómo probarlo

```
cd addins/acero-vigas/calculo && dotnet test CQEING.Vigas.Calculo.Tests
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin acero-vigas -Revit 2026
```

1. En Revit, viga 25×50 con 3#5 arriba y abajo: piel 2#4 por lado → 4 barras en el corte, equidistantes.
2. En el alzado: dos líneas finas azules, un rótulo "2#4 c/lado".
3. Cambiar la punta a 90°: el gancho baja por la línea del de las corridas.
4. Pórtico largo: aparece el traslapo de la piel en la misma zona que el de arriba.
5. Generar: la piel sale en las dos caras con sus ganchos; kg en el resumen.
6. Volver a 0: el pórtico queda como antes.

## Estado

- [x] Diseño revisado con el usuario (2026-11-06).
- [x] Plan paso a paso escrito (2026-11-06).
- [x] Pasos implementados (2026-11-06): tareas 1–6 con revisión por tarea y revisión final de la rama.
  Commits 10adcb6 (motor), 218ebbb (corte), f316eaa + 2d6313a (opciones y vigueta), 0aee641 (Revit), 7a1232b (vista),
  1ed4f46 + b29de42 (página), 83a2f27 (arreglos de la revisión final: botones de gancho de la piel, arrastre de la piel
  y sus traslapos, avisos de la piel, holgura contra corridas más gruesas, recorte del contador). Suite: 1133 en verde.
- [ ] Probado en Revit. Instalado en local para Revit 2026 con 83a2f27; falta la prueba de Juan ("Cómo probarlo").
  Mirar en especial: el gancho de la piel en Revit (`CreadorBarras.Voltear`, sube o baja por altura) y que el gancho de
  abajo de la piel no cruce el de la capa inferior en la esquina.
- Pendientes menores (no bloquean): pruebas más finas de la vista y del choque; el contador de piel puede recortarse
  con el tope anterior al bajar el diámetro hasta el siguiente cálculo; la etiqueta de la piel queda cerca del bastón
  de arriba en el alzado.
