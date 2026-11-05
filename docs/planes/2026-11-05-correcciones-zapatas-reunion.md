# Plan: correcciones del add-in de zapatas después de la reunión del 05-nov

## Contexto

- **Qué se pide:** lo que salió al probar `acero-zapatas-v0.1.0` en la reunión del 05-nov-2026 (Carlos, Juan y
  Santiago), más lo que agregó Luciano ese mismo día. Fuentes:
  - transcripción `C:\Users\Jose Bello\Desktop\josetp\Reunión Eqpo Programadores (3).txt`, de 01:04 a 01:22;
  - comentario de Juan: «no cambian las formas»;
  - Luciano (05-nov): «los ganchos tampoco cambian en la vista previa, estos dos botones están mal; revisa cómo lo hago
    en acero en vigas. No importa que se crucen: en vigas muestro en la vista previa los puntos donde pasa eso. El nombre
    puede llevar el eje (`Z-12 · Eje 9-A`). Respetar los colores de las barras (issue #154). Los ganchos hoy llegan hasta
    el recubrimiento: dejar las dos opciones»;
  - [issue #154](https://github.com/utilidadcqeing-prog/Add-in-Acero-en-columnas-2027/issues/154), la tabla de colores
    por diámetro de columnas y vigas.
- **Parte de:** `docs/planes/2026-11-04-acero-zapatas-246.md` (decisiones 1 a 27). La numeración sigue desde la 28.
- **Fuera de alcance:**
  - las vistas automáticas con zapatas y sin muros ni CAD (son del add-in de Santiago);
  - resolver los choques solo: se muestran como puntos y el modelador decide (igual que en vigas);
  - «aquí debería ser por letras» (Juan, 01:05): no se entiende a qué se refiere. Se le pregunta antes de tocarlo.
- **Entorno:** Revit 2026.5.10 (y 2027, que solo compila), modelo `Prueba 080720sfsd26`, rama `zapatas/pruebas-revit`.

## Criterios de aceptación

- [ ] **Formas:** al elegir otra forma en el catálogo, cambian la vista 3D, la sección y la barra en Revit. Si una forma
      no sirve para la parrilla, el tablero lo dice antes de generar.
- [ ] **Ganchos:** al elegir 90°, 135°, 180° o sin gancho, cambian la vista 3D y la sección, con el dibujo real del gancho.
- [ ] **Largo de la pata, dos opciones:** «según el cuadro» (por defecto, el mismo largo que vigas y columnas) o «hasta el
      recubrimiento» (como hoy). Si la de recubrimiento queda más corta que la del cuadro, avisa que no cumple.
- [ ] **Parrilla inferior y superior por separado:** cada una con sus ganchos, y se ven separadas en la sección de una
      zapata alta (h = 0.80 m, la que probó Carlos).
- [ ] **Puntos de choque:** donde dos barras se tocan aparece un punto rojo en la vista 3D y en la sección, y al pasar el
      mouse dice qué barra toca a cuál y cuánto se mete. Una capa «Choques» los apaga, y «Aplicar» con choques pregunta
      antes, como vigas (decisiones 35 y 36). Reemplaza los avisos de texto de la decisión 27.
- [ ] **Colores:** cada barra con el color de su diámetro, la misma tabla de #154 (`#5` rojo `#EA4335`, etc.), en la
      vista 3D, la sección y la planta.
- [ ] **Nombre con eje:** la planta y el encabezado dicen `Z-12 · Eje 9-A`, igual que la lista.
- [ ] **Cambiar de modelo** con el tablero abierto no cierra Revit: el tablero se recarga con el modelo nuevo.
- [ ] `dotnet test CQEING-Addins.sln -c Release` en verde y la prueba automática (`PruebaAutomatica.cs`) sin fallos nuevos.

## Hallazgos del código y de Revit real

Del código en `02a8bf3`. Lo marcado **por medir** no se ha comprobado en Revit.

- **H1. La vista previa no usa la forma ni el gancho.** `dibujar3d` y `dibujar2d` (`Tablero/web/zapatas.js:318` y `:413`)
  dibujan una U si el conjunto tiene `pata` y una recta si no. No leen `cfg.x.forma` ni el tipo de gancho: con 135° o 180°
  la barra sale recta, sin gancho. Por eso «no cambian» los dos botones.
- **H2. En Revit la forma tampoco cambia nada visible.** `GeneradorZapatas.cs:136` usa la forma elegida solo con la pata
  de 90°, y siempre le pasa las mismas tres curvas de la U. Si la forma admite la U, la barra es igual y solo cambia el
  nombre de la forma. Si no la admite, vuelve a la automática con un aviso.
- **H3. La pata llega siempre al recubrimiento opuesto** (decisiones 12 y 16 del plan de #246). Carlos (01:11 a 01:13):
  «el gancho tiene que ir con la misma longitud que en vigas y columnas; la mínima es la del cuadro». El largo del cuadro
  ya existe: `Reglas.LargoGancho(regla, TipoGancho.De90)`, enlazado desde vigas (`ReglasZapata.cs:14`).
- **H4. Vigas ya resuelve los choques** con `DetectorChoques.Detectar(trazos)`
  (`acero-vigas/calculo/CQEING.Vigas.Calculo/Generacion/DetectorChoques.cs`). Trabaja sobre `Trazo` (puntos 3D, número y
  diámetro), sin Revit. Para enlazarlo hace falta `Trazo` y `TipoTrazo`, que viven en `PreviaAcero.cs` junto con código
  que zapatas no necesita, y `Punto3`, que vive en `MarcoPortico.cs`.
- **H5. Los colores son por parrilla y dirección, no por diámetro:** `colorBarra` en `zapatas.js:316` usa naranja/azul.
  La tabla de #154 está en `acero-vigas/.../Modelo/ColoresDiametro.cs`, sin dependencias.
- **H6. El nombre con eje ya existe en la lista** (`LectorZapatas.cs:31`, «Z-1 · Eje 1-A»), pero la planta
  (`zapatas.js:137`) solo escribe `z.nombre`.
- **H7. Causa probable del cierre de Revit:** `PuenteTablero` guarda el `Document` del momento en que se abrió el tablero
  (`PuenteTablero.cs:17`) y no escucha `DocumentClosing` ni `ViewActivated`. Si se cambia o cierra el modelo, el tablero
  sigue usando un documento que ya no es válido. Vigas lo maneja en `acero-vigas/Tablero/PuenteTablero.cs:40` y `:211-257`.
  **Por medir:** repetirlo en Revit con el registro de errores (`AccionesRevit.RutaErrores`).

### Medido en Revit, paso 1 (2026-11-05)

Copia `Prueba 080720sfsd26.0028 - copia.rvt`, Revit 2026.5.10, con el MCP (`send_code_to_revit`). Zapata 780919
(1.80 × 1.80 × 0.35, fondo −1000, tope −650, sin acero), barra `#5 CIM` (d 15.9, doblado de gancho 95.2), recubrimientos
de 75 y una barra en X en y = 12000. La plantilla del MCP ya abre una transacción y la confirma, así que todo se hizo
dentro de una `SubTransaction` que se deshizo: al terminar, la zapata quedó con 0 barras. Medido sobre el eje de la barra
(`GetCenterlineCurves`), x y z en mm del modelo.

- **H8. Qué formas acepta `CreateFromCurvesAndShape`** con curvas recta, en L (una pata hasta el recubrimiento) y en U:

  | Geometría | Formas que la aceptan | Automática (`CreateFromCurves`) |
  |---|---|---|
  | Recta | 00, 01, 02, 08, 18, 24, 00 2 | 00 |
  | L | 17A | 17A |
  | U | 17, 26, 13 2 | 13 2 |

  Las otras 17 formas de parrilla del catálogo (10, 15, 20, 05, 06, 07, 11, 12, 13, 16, 19, 64, 65…) devuelven null con
  las tres geometrías: tienen ángulos o tramos que no son los de una parrilla. Con las formas que sí acepta, la barra
  sale idéntica (mismo eje y largo: recta 1634.1, L 1794.4, U 1954.6); solo cambia el nombre de la forma. Esto confirma
  H2 y la decisión 28.
- **H9. Las formas rectas «con gancho» (01, 02, 08, 18, 24) no ponen gancho solas.** Sin tipo de gancho en
  `BarTerminationsData`, salen sin gancho (`-/-`). Con un tipo de gancho, todas (00, 01, 02 y la automática) dan la
  misma barra con ese gancho: el gancho lo decide el tipo de gancho, no la forma.
- **H10. El tipo de gancho «Estándar - 90°.» no sirve para la pata.** Para la #5 mide 300 mm fijos
  (`GetHookLength`): la punta queda en z −625, o sea 25 mm **por encima del tope** de la zapata (−650). Por eso la pata
  de 90° se hace con curvas (decisión 23), y su largo lo pone el add-in.
- **H11. Ganchos de 135° y 180° en la parrilla inferior** (`Estándar - 135°.` y `Estándar - 180°.`, orientados hacia
  arriba): la punta del 135° llega a z −766.1, 72 mm hacia adentro del borde, y la del 180° queda horizontal en z −805.9,
  135 mm hacia adentro (`Standard - 180 deg.`: 195 mm). Los dos quedan dentro de la zapata. Largos: 1943.6, 2030.9 y
  2150 mm.
- **H12. El cuadro de 90° de la #5 pide 278 mm** (`Reglas.Generales`, B del `Gancho90`). Con h 350 y recubrimientos de
  75 caben unos 184 mm (decisión 16 del plan de #246), así que con «según el cuadro» la pata se corta en el recubrimiento
  y avisa (decisión 29). En la zapata de 0.80 m de Carlos sí cabe.
- **H13. Geometría de los ganchos de 135° y 180° en Revit**, medida con `#4`, `#5`, `#6` y `#8 CIM` y los tipos
  `Estándar - 135°.`, `Estándar - 180°.` y `Standard - 180 deg.` (12 casos, todos cumplen al 0.1 mm):
  - la línea que se le da a Revit termina en la cara exterior del doblez: el tramo recto empieza `D/2 + d` hacia
    adentro (#5: 2675 → 2738.5);
  - el doblez es un arco de radio `D/2 + d/2` sobre el eje (#5: 55.56), con `D` = `StandardHookBendDiameter` del tipo
    de barra;
  - la cola recta mide `GetHookTangentLength(gancho) − D/2 − d` (#5 con `Estándar - 180°.`: 142.88 − 47.63 − 15.88 =
    79.37). No sale del multiplicador: hay largos fijados por barra (#5 con `Standard - 180 deg.`: 138.95, no 4 d).
  - Así, la vista previa usa la cola que da Revit para el tipo de gancho y la barra elegidos, no la del cuadro.
- **H14. Un gancho de estribo en una barra `Standard` hace fallar a Revit** (`Autodesk.Revit.Exceptions.InternalException`
  en `CreateFromCurves` con `Estribo/Tirante - 135°.`). El catálogo ya los deja fuera (`TiposZapata.cs`,
  `h.Style == RebarStyle.Standard`).
  Ojo para repetir la medición: la excepción salió por fuera de la `SubTransaction` y la plantilla del MCP confirmó 3
  barras de prueba (817620 a 817622). Se borraron enseguida y la zapata volvió a 0 barras. Hay que poner el `RollBack`
  en un `finally`.
- **H15. Los choques del cálculo contra la prueba automática de #246 en Revit** (2026-11-05, paso 5). Comparado con
  `%APPDATA%\CQEING\zapatas\prueba.txt`. La prueba de Revit cuenta **pares de barras** que se meten más de 0.5 mm. El
  detector de vigas cuenta **lugares** (un par puede chocar en más de un lugar). #5 CIM, D 95.25, recubrimiento 75,
  separación 150, las dos parrillas:

  | Caso | Revit (pares, peor) | Detector (lugares, peor) |
  |---|---|---|
  | L h 500 (817679) | 14, −8.9 mm | 14, 8.9 mm |
  | Trapecio h 500 (817715) | 18, −14.8 mm | 22, 14.1 mm |
  | Rectangular larga h 500 (817776) | 0 | 0 |
  | 900×2000 h 350, 135° (780868) | 4, −2.1 mm | 4, 2.2 mm |
  | 900×2000 h 350, 180° (780868) | 8, −14.3 mm | 4, 15.4 mm |

  Los 4 pares que faltan en 180° se midieron barra por barra: son la primera y la última barra Y de la parrilla
  inferior contra el gancho de 180° de la primera barra X de la superior. En el cálculo quedan a +0.1 mm (se rozan sin
  meterse); en Revit quedan apenas por debajo de −0.5 mm. Es un caso justo en el límite, no un error del detector. Se
  deja la holgura de vigas (0.5 mm) sin cambiar. En el trapecio el detector marca 22 lugares contra 18 pares de Revit. Es
  probable que algún par choque en más de un lugar, pero no se midió barra por barra: queda por confirmar en la
  prueba del paso 8.
- **H16. La clasificación por geometría coincide con H8** (2026-11-05, paso 6, con el MCP sobre la copia). Se leyeron las
  curvas de `GetCurvesForBrowser` de todas las formas `Standard`:
  - 1 tramo recto: 00, 00 2, 00 3, 00 4, 00 5, 01, 02, 08, 18, 24. H8 no había probado 00 3, 00 4 ni 00 5; se probaron
    con `CreateFromCurvesAndShape` y una barra recta, y Revit acepta las cinco formas 00.
  - 2 tramos a 90°: solo 17A.
  - 3 tramos a 90° con las dos patas del mismo lado: 13 2, 17 y 26. La 20 tiene 90° y 90° pero con las patas a lados
    opuestos (una Z), así que no entra.
  - Las demás tienen arcos, ángulos de 45°, 56° o 63°, o más de 3 tramos.
  Al revisar, apareció una barra más en la zapata 780919: la 817623 (#4 CIM, forma 00). Es una cuarta barra sobrante de
  la falla de H14 (va seguida de 817620 a 817622) y no se había visto. Se borró y la zapata volvió a 0 barras.

### Referencia: cómo lo hace vigas (revisado el 05-nov, a pedido de Luciano)

Luciano pidió tomar vigas, que ya está terminado, como modelo del flujo. Lo que se copia:

- **V1. Una sola geometría para todo.** C# calcula los trazos de cada barra con sus ganchos reales
  (`PreviaAcero.Trazos`), y de ahí salen la vista previa 3D, el corte del tablero (`PreviaAcero.EnElCorte`) y los
  choques. La página no calcula geometría: dibuja lo que le manda C#. Al cambiar un gancho, la página manda la opción,
  C# recalcula y la página redibuja.
- **V2. Choques.** `ArmadorChoques` corre `DetectorChoques.Detectar` sobre esos trazos y arma, por choque, el punto, el
  texto («La barra S1 (#5) se mete 2.6 mm en la barra S2 (#5)»), una pista y cuántas veces se repite
  (`acero-vigas/calculo/.../Tablero/ArmadorChoques.cs`). El alzado pone un punto rojo y, al pasar el mouse, abajo dice
  texto · pista (`alzado.js:206-216`, `:349`). Hay una capa «Choques» para apagarlos (`alzado.js:53`).
- **V3. Generar con choques:** si hay choques, C# no genera y el tablero pregunta «Revisar» o «Generar de todos modos»
  (`tablero.js:320-327`, `:867-875`).
- **V4. Colores:** C# pone `ColoresDiametro.De(diametro)` en cada barra de la vista (`ArmadorVista.cs:75`); la página
  solo usa `b.color`.
- **V5. Cambio de modelo:** `PuenteTablero` lee el documento activo en cada recarga, escucha `ViewActivated` y
  `DocumentClosing`, y los manejadores nunca dejan escapar una excepción (`acero-vigas/Tablero/PuenteTablero.cs:165-258`).
- **No se copia:** la vista 3D dentro de Revit (`PreviaAcero3D`, DirectContext3D) ni los arreglos propuestos de cada
  choque (`ArreglosChoque`). Ver decisiones 34 y 35.
- **V6. Reglas en vigas** (revisado el 05-nov, segundo pedido de Luciano):
  - El botón «Reglas» de la barra de arriba abre «Reglas generales» (`acero-vigas/Tablero/web/tablero.js:1129` y
    `:1608`). Siempre abre bloqueada; hay que desbloquear para editar.
  - Campos: múltiplo de corte, distancia libre, varilla de 6 o 12 m y redondeo de ganchos. Por diámetro (#3 a #8):
    traslapo, gancho de 90°, de 180°, estribo de 135° y kg/m.
  - «Guardar y bloquear» manda `guardarReglas`. Si se sale con cambios, avisa con «Guardar y bloquear», «Descartar» y
    «Seguir editando».
  - El archivo es `%AppData%\CQEING\vigas\reglas.json` (`Modelo/AlmacenReglas.cs`, sin dependencias). Sin archivo se
    usan `Reglas.Generales`, que son los valores de los cuadros de las imágenes del 04-nov
    (`Desktop\josetp\WhatsApp Image 2026-11-04 at 7.14.51 PM*.jpeg`: «Dimensiones mínimas para ganchos estándar» y
    «Cuadro de ganchos y traslapos de varillas»; #5: 90° 0.278, 180° 0.238, estribo 0.189).
  - Pasar de reglas a textos de pantalla y de vuelta (`ArmadorVista.Reglas` y `ArmadorVista.ReglasDe`), con
    `VistaReglas` y `ReglaVista` (`VistaTablero.cs`), está mezclado con el resto del tablero de vigas.
  - Zapatas hoy usa `Reglas.Generales` fijas: no lee lo guardado en vigas.
- **V7. La ventana se aparta en vigas:** `VentanaTablero.Apartar()` minimiza la ventana (que siempre queda encima de
  Revit) para que se vea lo que pasa en Revit; `Volver()` la deja como estaba (`VentanaTablero.xaml.cs:84-99`). Se
  aparta en «Ver en Revit» y al empezar a seleccionar, y vuelve al terminar la selección o con el botón de la cinta.
  Zapatas solo tiene `Volver()`, que además siempre vuelve a `Normal`.

## Decisiones

28. **La forma decide en qué puntas hay gancho** (2026-11-05, Claude; aprobada por Luciano el 05-nov): de las formas de
    parrilla (estilo `Standard`, hasta 3 curvas), una recta no lleva patas, una L lleva pata en una punta y una U en las
    dos. La forma elegida cambia las curvas que se le pasan a Revit y lo que dibuja la vista previa. Una forma que no sea
    de esas tres se muestra en gris y no se puede elegir. **Por medir en el paso 1** qué formas del modelo acepta
    `CreateFromCurvesAndShape` con cada geometría.
29. **Dos opciones para el largo de la pata de 90°** (2026-11-05, Carlos, Santiago y Luciano): un botón «Largo de la pata»
    con «Según el cuadro» (por defecto) y «Hasta el recubrimiento». Con «según el cuadro», si el largo del cuadro no cabe
    en la altura, la pata se corta en el recubrimiento y avisa. Con «hasta el recubrimiento», si queda más corta que el
    cuadro, avisa «no cumple el largo del cuadro». Reemplaza la decisión 12 como valor por defecto.
30. **Las dos parrillas eligen gancho por separado** (2026-11-05, Carlos, 01:11): la inferior y la superior tienen su
    propio botón de gancho, en las dos direcciones. Por defecto las dos llevan 90° según el cuadro.
31. **Choques como puntos, no como avisos** (2026-11-05, Luciano, igual que vigas): se enlaza el `DetectorChoques` de
    vigas. Para no arrastrar `PreviaAcero.cs`, se pasan `Trazo`, `TipoTrazo` y `Punto3` a un archivo propio en vigas
    (`Generacion/Trazo.cs`), sin cambiar su código. Los avisos de texto de la decisión 27 se quitan.
32. **Colores por diámetro de #154** (2026-11-05, Luciano): se enlaza `ColoresDiametro.cs` de vigas. El contorno de las
    barras es casi negro (`RgbContorno`), como en vigas.
33. **El tablero sigue al modelo activo** (2026-11-05, Claude, por H7): mismo manejo que vigas. Al cambiar de modelo se
    recarga, y si se cierra el modelo, el tablero lo dice y no hace nada hasta que haya otro.
34. **La vista 3D sigue en el tablero** (2026-11-05, Luciano): no se agrega la vista 3D dentro de Revit de vigas. La 3D
    y la sección del tablero se dibujan con los trazos de C# (V1), con colores (V4) y puntos rojos (V2).
35. **Generar con choques pregunta, como vigas** (2026-11-05, Luciano; cambia «no bloquea» del criterio de choques): si
    hay choques, «Aplicar» pregunta «Revisar» o «Aplicar de todos modos» (V3). No se proponen arreglos.
36. **Capa «Choques»** (2026-11-05, Luciano): un interruptor, prendido por defecto, oculta los puntos (V2).
37. **La forma solo cuenta con la pata de 90°** (2026-11-05, Luciano, por H8 a H11; precisa la decisión 28):
    - con pata de 90°, la forma elige recta (sin patas), L (una pata, **al inicio**: la punta de menor X o de menor Y en
      los ejes de la zapata, como la 17A) o U (dos patas). El catálogo muestra 17A, 17, 26 y 13 2, más las rectas;
    - con 135°, 180° o sin gancho, la barra es recta, con el gancho en las dos puntas o sin gancho, y el catálogo de formas
      muestra solo las rectas (00, 01, 02, 08, 18, 24, 00 2);
    - las formas que Revit no acepta (H8) no se muestran. Se decide por la geometría de cada forma, no por su nombre:
      recta = 1 tramo; L = 2 tramos a 90°; U = 3 tramos a 90° con las dos patas del mismo lado.
38. **Botón «Reglas» como en vigas, con las reglas compartidas** (2026-11-05, Luciano):
    - Zapatas lee y guarda el mismo `%AppData%\CQEING\vigas\reglas.json` con `AlmacenReglas` enlazado. Lo que se
      guarde en un add-in vale para el otro.
    - La pantalla tiene los mismos campos, candado y aviso de cambios sin guardar que vigas (V6). Así, guardar desde
      zapatas no borra lo que solo usa vigas.
    - Zapatas calcula, genera y arma el catálogo con las reglas cargadas, no con `Reglas.Generales`.
38a. **La pantalla de Reglas muestra también las dos imágenes** (2026-11-05, Luciano), debajo de la tabla, como
    referencia de qué son A, B, D y E. Se copian a `Tablero/web/img/`.
39. **Separar la pantalla de reglas en vigas y enlazarla** (2026-11-05, Claude, como la decisión 31):
    - `VistaReglas`, `ReglaVista` y las conversiones pasan sin cambios a `acero-vigas/.../Tablero/PantallaReglas.cs`
      (`PantallaReglas.De` y `PantallaReglas.ReglasDe`).
    - `ArmadorVista.Reglas` y `ArmadorVista.ReglasDe` quedan como llamadas a esas dos, para no tocar a quien las usa.
40. **«Seleccionar en Revit» y «Mostrar en Revit» apartan el tablero** (2026-11-05, Luciano, como V7):
    - Se agrega `Apartar()` a la ventana de zapatas, y `Volver()` la deja como estaba (no siempre en `Normal`).
    - Mostrar: se aparta y muestra las zapatas en Revit; el tablero vuelve con el botón «Acero en zapatas» de la cinta.
    - Seleccionar: se aparta mientras se eligen las zapatas y vuelve al pulsar Terminar. Si ya había zapatas
      seleccionadas en Revit, también se aparta un momento y vuelve con ellas.
41. **Entrega** (2026-11-05, Luciano):
    - Commit y push de la rama `zapatas/pruebas-revit` a `origin` (fork `luffyjpl`), y PR a la rama `acero-zapatas` de
      `upstream`, como el PR #267.
    - El merge espera la revisión visual de Luciano del botón Reglas y del apartado.

## Plan paso a paso

1. **Medir las formas en Revit** — archivo: `Revit/ComandoDiagnosticoZapata.cs` (solo Debug) — con el MCP de Revit o el
   diagnóstico, sobre una copia del modelo: para cada forma de parrilla, crear una barra recta, una L y una U con
   `CreateFromCurvesAndShape`, anotar cuáles acepta y medir la geometría. También el gancho de 135° y 180° (pendiente del
   plan de #246). Anotar todo en Hallazgos antes de seguir. **Si no coincide con la decisión 28, se corrige el plan.**
2. **Cierre de Revit al cambiar de modelo** — archivos: `Tablero/PuenteTablero.cs`, `Tablero/ComandoTablero.cs` — se
   prueba abriendo dos modelos, cambiando entre ellos y cerrando uno con el tablero abierto.
3. **Separar `Trazo` en vigas y enlazar** — archivos: `acero-vigas/.../Generacion/Trazo.cs` (nuevo, con lo que sale de
   `PreviaAcero.cs` y `MarcoPortico.cs`), `CQEING.Zapatas.Calculo.csproj` (enlaza `Trazo.cs`, `DetectorChoques.cs` y
   `ColoresDiametro.cs`) — se prueba con las pruebas de vigas en verde, sin cambios en su resultado.
4. **Motor: formas, largo de pata y ganchos por parrilla** — archivos: `Modelo.cs`, `MotorParrillas.cs`,
   `ReglasZapata.cs` y sus pruebas — se prueba con: pata según el cuadro en h 0.35 y h 0.80, pata hasta el recubrimiento
   con aviso, forma recta, L y U, y ganchos distintos arriba y abajo.
5. **Trazos y choques en el cálculo** — archivo nuevo `TrazosZapata.cs` — cada conjunto da sus trazos 3D con el gancho
   real (90°, 135°, 180°, con el diámetro de doblado del tipo de barra del modelo) y se pasan a `DetectorChoques`. Se
   prueba con los casos medidos en el plan de #246: L con 14 choques y trapecio con 18.
6. **Protocolo y generador** — archivos: `ProtocoloZapatas.cs`, `Revit/GeneradorZapatas.cs`, `Tablero/PuenteTablero.cs`
   — la forma y el gancho de cada parrilla llegan a Revit, y el resultado lleva trazos, colores y choques. En detalle:
   - catálogo (`Revit/TiposZapata.cs`): cada forma lleva su `Clase` (`recta`, `L` o `U`, por la geometría de H16), y
     solo se mandan las que tienen clase. Cada barra lleva la cola de cada gancho de 135° y 180°
     (`GetHookTangentLength − D/2 − d`, H13);
   - el puente pasa la elección a la configuración antes de calcular o aplicar. Con la pata de 90° y una forma elegida,
     la forma decide las patas (recta: ninguna, L: al inicio, U: las dos). Con 135° o 180°, la cola sale del catálogo
     según el gancho elegido;
   - el generador arma las curvas de la recta, la L o la U según `Patas`, y usa `TiposElegidos` por parrilla;
   - `Resultado` agrega `trazos` (pieza, número, color de `ColoresDiametro.De` y puntos [x, y, z]) y `choques` (punto,
     las dos piezas y el texto);
   - `AplicarZapatas` lleva `Forzar`, como `Generar` de vigas: sin él, si hay choques no se genera y se contesta
     `{tipo:"choques", cantidad}` para que la página pregunte.
7. **Tablero** — archivos: `Tablero/web/index.html`, `Tablero/web/zapatas.js` — vista 3D y sección dibujadas con los
   trazos del cálculo (no con su propio dibujo), colores por diámetro, puntos rojos de choque con su texto, botones de
   gancho por parrilla, botón «Largo de la pata» y nombre con eje en la planta.
8. **Prueba automática** — archivo: `Revit/PruebaAutomatica.cs` — agregar: pata según el cuadro, forma L, ganchos
   distintos por parrilla y que los choques del tablero coincidan con los medidos en Revit.
9. **Cerrar el plan** — Estado, criterio por criterio, y la pregunta a Juan sobre «por letras».
10. **Separar la pantalla de reglas en vigas** (decisión 39) — archivos: `acero-vigas/.../Tablero/PantallaReglas.cs`
    (nuevo), `VistaTablero.cs` y `ArmadorVista.cs`. Se prueba con las pruebas de vigas en verde, sin cambios.
11. **Reglas en el cálculo y en el puente** (decisión 38) — archivos: `CQEING.Zapatas.Calculo.csproj` (enlaza
    `AlmacenReglas.cs` y `PantallaReglas.cs`), `ProtocoloZapatas.cs` (`reglas` en `Datos` y el mensaje
    `guardarReglas`) y `Tablero/PuenteTablero.cs` (carga al recargar; al guardar, guarda, avisa y recarga). Se prueba
    con pruebas del protocolo y con un archivo de reglas temporal.
12. **Pantalla de reglas** (decisiones 38 y 38a) — archivos: `Tablero/web/index.html`, `zapatas.js` y
    `Tablero/web/img/` — se prueba en el navegador: abre bloqueada, desbloquear, editar, avisar al salir con cambios,
    guardar y ver las imágenes.
13. **Apartar el tablero** (decisión 40) — archivos: `Tablero/VentanaTablero.cs` y `Tablero/PuenteTablero.cs` — se
    prueba en Revit a mano (letra M de la guía).
14. **Entrega** (decisión 41) — pruebas, prueba automática en Revit, commit, push y PR; el merge, con el OK de Luciano.

## Cómo probarlo

```powershell
dotnet test addins/acero-zapatas/calculo/CQEING.Zapatas.Calculo.Tests -c Release
dotnet test CQEING-Addins.sln -c Release
dotnet build addins/acero-zapatas/CQEING.Zapatas.csproj -c Release -p:RevitVersion=2026
dotnet build addins/acero-zapatas/CQEING.Zapatas.csproj -c Release -p:RevitVersion=2027

Get-Process -Name Revit -ErrorAction SilentlyContinue   # no debe devolver nada
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin acero-zapatas -Revit 2026
```

La prueba automática (paso 8) se corre con la variable `CQEING_ZAPATAS_PRUEBA` apuntando a una **copia** del modelo,
en una build Debug, y deja el informe en `%APPDATA%\CQEING\zapatas\prueba.txt` (ver Estado, paso 8).

### Guía de prueba visual (a mano, en Revit)

Sirve para ver cada cambio con los propios ojos y para depurar. En cada punto: **qué hacer**, **qué debe verse** y
**qué anotar si no se ve**. Las capturas van a `docs/planes/imagenes/2026-11-05-zapatas-<letra>.png`. Si algo falla
dentro de Revit, el error completo queda en `%APPDATA%\CQEING\zapatas\errores.log`.

**Preparación**

1. Cerrar Revit sin guardar y comprobar que no queda abierto: `Get-Process -Name Revit -ErrorAction SilentlyContinue`
   no debe devolver nada.
2. Desplegar la versión de esta rama:
   `powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin acero-zapatas -Revit 2026`.
3. Abrir Revit 2026 y la **copia** `Descargas\Prueba 080720sfsd26.0028 - copia.rvt`, en la vista 3D ACEROS.
4. Pestaña **CQEING** → panel **Zapatas** → **Acero en zapatas**.

**A. Nombre con eje en la planta**
- Hacer: mirar la planta del tablero.
- Debe verse: cada zapata como «Z-12 · Eje 9-A», igual que en la lista de arriba. Sin eje, solo «Z-12».

**B. Colores por diámetro (#154)**
- Hacer: con una zapata elegida, dejar X en #5 y cambiar Y a #4.
- Debe verse: barras #5 en rojo y #4 en azul, en la vista 3D y en la sección. La leyenda dice «#4» y «#5» con su
  color. Ya no se pinta por parrilla (naranja y azul).

**C. Forma con pata de 90° (decisión 37)**
- Hacer: gancho «90° · pata» y, en Forma (Dir. X), probar Automática, **00**, **17A** y **17**.
- Debe verse: el catálogo muestra solo formas de parrilla, cada una con su clase («recta, sin patas», «L, pata al
  inicio», «U, dos patas»). La vista 3D y la sección cambian:
  - Automática o 17: U, pata en las dos puntas.
  - 17A: L, pata solo en la punta de menor X (o de menor Y).
  - 00: recta, sin patas.
- Si no cambia: anotar la forma elegida y capturar la vista.

**D. Ganchos de 135° y 180°**
- Hacer: en Gancho (Dir. X), elegir «Estándar - 135°.», después «Estándar - 180°.» y después «Sin gancho».
- Debe verse:
  - Con 135° o 180°, la barra se dibuja con el gancho doblado, hacia arriba en la parrilla inferior.
  - Con un gancho elegido, el catálogo de Forma muestra solo las rectas (00, 01, 02…), con la nota que lo explica.
  - Si antes había una L o una U elegida, la forma vuelve a «Automática».

**E. Parrilla superior con su propio gancho (decisión 30)**
- Hacer: marcar «Incluir parrilla superior». Dejar la inferior en 90° y poner la superior en 135°.
- Debe verse:
  - Aparece la tabla Forma/Gancho de la superior.
  - En la vista 3D, arriba los ganchos de 135° van hacia abajo; abajo, las patas de 90° van hacia arriba.
  - Al desmarcar la superior, la tabla se oculta.

**F. Largo de la pata (decisión 29)**
- Hacer: en una zapata de 0.35 m, alternar «Según el cuadro» y «Hasta el recubrimiento».
- Debe verse:
  - Según el cuadro: el aviso «la pata del cuadro (278 mm) no cabe en la altura; se cortó en el recubrimiento».
  - Hasta el recubrimiento: el aviso «no cumple el largo del cuadro».
  - En 0.35 m la pata mide lo mismo con las dos opciones (no cabe más). La diferencia se ve en una zapata alta: en
    0.80 m, «según el cuadro» deja 278 mm y la otra llega al recubrimiento. El modelo no trae zapatas de 0.80 m; la
    prueba automática lo comprobó con una losa de prueba (caso 19).

**G. Choques como puntos rojos (decisiones 31 y 36)**
- Hacer: elegir la zapata rectangular 0.90 × 2.00 (h 0.35) y poner 135° en las dos direcciones.
- Debe verse:
  - 4 puntos rojos en las esquinas de la vista 3D.
  - Al pasar el mouse por un punto, abajo aparece «La barra 1 de la parrilla inferior en Y (#5) se mete 2.2 mm en la
    barra 1 de la parrilla inferior en X (#5)».
  - La casilla «Choques» de la leyenda los oculta y los vuelve a mostrar.
- Con 90° en esa zapata no debe haber puntos rojos.

**H. Aplicar con choques pregunta antes (decisión 35)**
- Hacer: con la configuración de G, pulsar «Aplicar refuerzo en Revit».
- Debe verse:
  - Se abre «Hay barras que chocan» con la cantidad.
  - «Revisar» cierra y no crea nada en Revit.
  - «Aplicar de todos modos» crea el acero, y Ctrl+Z en Revit lo deshace.

**I. Lo que se crea en Revit es lo que muestra el tablero**
- Hacer: aplicar sin choques, por ejemplo con 17A y 90°. En Revit, seleccionar una barra y ver Propiedades.
- Debe verse:
  - La forma es la elegida (17A).
  - En una sección de Revit, la barra tiene la misma forma que en el tablero (pata solo al inicio).
  - La pata llega a la altura que dice «Pata (m)».

**J. Cambio de modelo con el tablero abierto (decisión 33; el cierre de Revit de la reunión)**
- Hacer: con el tablero abierto, abrir otro modelo, volver al primero y cerrar uno de los dos. Al final, cerrar todos.
- Debe verse:
  - Revit **no se cierra**.
  - El tablero cambia la lista de zapatas al modelo activo.
  - Sin modelo abierto, dice «No hay un modelo de proyecto abierto».
- Si Revit se cae: anotar los pasos exactos y adjuntar `errores.log`. Este punto es el más importante, porque nunca se
  probó a mano.

**K. Recordar lo elegido**
- Hacer: elegir formas, ganchos y largo de pata, cerrar el tablero y volver a abrirlo.
- Debe verse: la misma configuración, también la de la parrilla superior.

**L. Botón Reglas (decisiones 38 y 38a)**
- Hacer: pulsar «Reglas» arriba a la derecha.
- Debe verse:
  - «Reglas generales» bloqueada, con los valores del cuadro (#5: 90° 0.278, 180° 0.238, estribo 0.189) y debajo
    las dos imágenes.
  - Desbloquear, cambiar el gancho de 90° del #5 a 0.300 y pulsar «Guardar y bloquear»: aparece el aviso de reglas
    guardadas y la vista previa recalcula (la pata del cuadro pasa a 300 mm).
  - Abrir el tablero de vigas: el #5 a 90° también dice 0.300.
  - Volver a dejar 0.278.
- Cambiar un valor y cerrar sin guardar: avisa «Hay cambios sin guardar».

**M. Seleccionar y Mostrar apartan el tablero (decisión 40)**
- Hacer: pulsar «Mostrar en Revit».
- Debe verse: el tablero se minimiza y Revit muestra y selecciona la zapata. El botón «Acero en zapatas» de la cinta
  lo trae de vuelta, del mismo tamaño que tenía.
- Hacer: sin nada seleccionado en Revit, pulsar «Seleccionar en Revit», elegir zapatas y pulsar Terminar.
- Debe verse: el tablero se minimiza mientras se eligen y vuelve solo con las zapatas elegidas.

### Qué está hecho, qué falta y qué no se hará

| Tema | Estado |
|---|---|
| Forma y gancho cambian la vista previa (Juan y Luciano) | Hecho. Probado en el navegador con datos reales; falta verlo en Revit (C, D) |
| Forma L, recta y U en Revit | Hecho y probado con la prueba automática (casos 9 y 16) |
| Ganchos por parrilla (Carlos) | Hecho y probado con la prueba automática (caso 17); falta verlo en el tablero (E) |
| Largo de pata, dos opciones | Hecho y probado con la prueba automática (casos 18 y 19); falta verlo en el tablero (F) |
| Colores por diámetro (#154) | Hecho; falta verlo en Revit (B) |
| Choques como puntos y pregunta al aplicar | Hecho; el tablero coincide con Revit en todos los casos; falta verlo a mano (G, H) |
| Nombre con eje en la planta | Hecho; falta verlo (A) |
| Cierre de Revit al cambiar de modelo | Arreglado en código, **sin probar** (J) |
| 180°: Revit cuenta 8 pares y el tablero 4 lugares | Se queda así: los 4 de más se rozan a +0.1 mm, en el límite (H15) |
| Trapecio: 2 barras corridas 147 mm | Viene del 04-nov, no de este trabajo; sin resolver (plan de #246) |
| Choques en la sección 2D | No se hace: los puntos van en la vista 3D, como el alzado de vigas |
| Vista 3D dentro de Revit (DirectContext3D) | No se hace (decisión 34) |
| Arreglos propuestos para cada choque | No se hace (decisión 35) |
| «Aquí debería ser por letras» | Pendiente: preguntar a Juan (paso 9) |
| Commit y PR | No hechos: esperan el visto bueno de Luciano |

### Cómo reportar un problema

Para cada problema anotar:
- la letra de la guía y la zapata (nombre y eje);
- la configuración: barras, separación, forma, gancho, superior y largo de pata;
- qué se esperaba y qué se vio;
- una captura en `docs/planes/imagenes/`;
- si Revit dio error, las últimas líneas de `%APPDATA%\CQEING\zapatas\errores.log`.

Con eso se agrega un Hallazgo al plan y se corrige siguiendo la regla del repo: primero el plan, después el código.

## Estado

- [x] Plan revisado con el usuario (05-nov): aprobó el plan, la decisión 28 y las 34 a 36, y pidió tomar vigas de modelo.
- [x] Paso 1 medido en Revit (05-nov): H8 a H14 y la decisión 37. Al principio el MCP contestaba «Method
      'send_code_to_revit' not found». Se arregló activando los comandos en el complemento del MCP dentro de Revit.
- [x] Paso 2 (05-nov): `PuenteTablero` recibe `UIApplication`, lee el modelo activo en cada recarga, escucha
      `ViewActivated` y `DocumentClosing` como vigas, y antes de seleccionar, mostrar o aplicar comprueba que el modelo
      leído siga siendo el activo; si no, recarga. La página (`recibirDatos`) borra lo elegido y el resultado al llegar
      datos de otro modelo. Compila en 2026 y 2027. **Falta probarlo en Revit** (prueba 6 de «Cómo probarlo»).
      Hallazgo: el puente ya revisaba `doc.IsValidObject`, así que cerrar el modelo no era el problema. Lo que fallaba era
      cambiar a otro modelo: el puente seguía usando el primero, pero tomaba la selección y la vista activa del segundo.
- [x] Paso 3 (05-nov): `Punto3`, `TipoTrazo` y `Trazo` pasaron sin cambios a `acero-vigas/.../Generacion/Trazo.cs`.
      El cálculo de zapatas enlaza `Trazo.cs`, `DetectorChoques.cs` y `ColoresDiametro.cs`.
      `dotnet test CQEING-Addins.sln -c Release`: 1577 correctas en 7 proyectos (1085 de vigas, 50 de zapatas). Vigas y
      zapatas compilan en Release para 2026 y 2027.
- [x] Paso 4 (05-nov): `Modelo.cs` suma `Patas`, `LargoPata`, `Extremos` y los extremos de la parrilla superior
      (`ConfigDireccion.Superior`, `En(parrilla)`). `MotorParrillas` calcula la pata según el cuadro (278 mm en #5) o
      hasta el recubrimiento, con sus avisos, la L con pata al inicio y la recta sin patas, y quita los avisos de texto
      de choques (decisión 31). `ProtocoloZapatas` lleva `TiposElegidos` con forma y gancho por parrilla, y `LargoPata`.
- [x] Paso 5 (05-nov): `TrazosZapata.cs` arma un trazo por barra con el doblez de H13 y pasa los trazos al detector de
      vigas. Comparado con Revit en H15. `TrazosZapataTests` (9 pruebas) fija la L de Revit: 14 choques, 8.9 mm.
      `dotnet test addins/acero-zapatas/calculo/CQEING.Zapatas.Calculo.Tests -c Release`: 66 correctas.
      El proyecto de Revit (`CQEING.Zapatas.csproj`) **no compila** hasta el paso 6, porque todavía usa `EleccionTipos`.
- [x] Paso 6 (05-nov):
      - `ProtocoloZapatas.ConTipos` pasa la elección a la configuración (patas de la forma, cola del gancho, doblados).
        La usan el puente (vista previa y «Aplicar») y el generador, así que los dos calculan lo mismo.
      - `Resultado` lleva `trazos` (con `color`) y `choques`. `AplicarZapatas.Forzar` y `ProtocoloZapatas.Choques` siguen
        el modelo de vigas.
      - El catálogo manda solo las formas con `Clase` (H16) y las colas por gancho (H13).
      - El generador arma recta, L o U, y usa la forma elegida también en las rectas y con ganchos (H9). Se quitaron
        `EleccionTipos` y `TiposZapata.ConDoblados`.
      - `PruebaAutomatica` usa `TiposElegidos` y mide contra `ConTipos`; sus casos nuevos quedan para el paso 8.
      - Pruebas: `dotnet test CQEING-Addins.sln -c Release`, 1601 correctas (74 de zapatas). `CQEING.Zapatas.csproj`
        compila en Release 2026 y 2027 y en Debug 2027.
      - Hasta el paso 7 la página todavía lee `f.parrilla`, que ya no existe: muestra todas las formas atenuadas y
        dibuja con su propio código.
- [x] Paso 7 (05-nov), en `Tablero/web/index.html` y `zapatas.js`:
      - Estado por dirección con la parrilla inferior y `sup` (forma y gancho propios), y `largoPata`. Lo guardado con la
        versión anterior se completa (`completar`).
      - `configMensaje` manda los dos juegos de tipos. La forma y el gancho automáticos de la superior van como `''`,
        porque `null` en C# quiere decir «los de la inferior».
      - Catálogo según la decisión 37: con 90° muestra recta, L y U con su clase; con 135°, 180° o sin gancho, solo las
        rectas. Si se cambia el gancho, una L o U elegida vuelve a «Automática». Se quitó el filtro «Mostrar todas».
      - Botones de forma y gancho de la parrilla superior (se ocultan sin superior) y grupo «Largo de la pata de 90°».
      - Vista 3D y sección dibujadas con `trazos` (en la sección, la barra X más cercana al corte; las Y como puntos),
        con el color de cada diámetro. La leyenda lista los diámetros.
      - Puntos rojos de choque encima de todo, con su texto abajo al pasar el mouse, y la capa «Choques».
      - La planta escribe «Z-12 · Eje 9-A».
      - `{tipo:"choques"}` abre «Hay barras que chocan» con «Revisar» y «Aplicar de todos modos» (`forzar: true`).
      - Probado en el navegador con datos reales de C# (`Datos` y `Resultado` de una 1.80 × 1.80 h 350 con 135°):
        46 barras, 4 puntos de choque con su texto, «#5» en la leyenda, la sección con los ganchos de 135°, el catálogo
        filtrado (135°: Automática y 00; 90°: 00, 17A y 17 con su clase) y el mensaje de calcular con los tipos de la
        superior. **Falta verlo dentro de Revit.**
- [x] Paso 8, código (05-nov), en `Revit/PruebaAutomatica.cs`:
      - `Medir` revisa en cada punta con pata o gancho que vaya hacia adentro y que haya las patas de la forma (la L
        lleva una, al inicio), y que la pata mida `Pata − d/2` del motor (tolerancia 1 mm).
      - `Medir` también compara los choques del tablero (`TrazosZapata`) con los pares que mide en Revit.
      - Casos nuevos: 16 forma L (17A), 17 abajo pata de 90° y arriba 135°, 18 hasta el recubrimiento y 19 una losa de
        prueba de 0.80 m con la pata según el cuadro. `CrearLosa` recibe la altura.
      - Compila en Debug 2027. En el primer intento de despliegue, Revit 2026 seguía abierto (proceso 16452) y tenía
        tomada `bin\Debug\2026`. Se esperó a que se cerrara.
- [x] Paso 8 corrido en Revit 2026.5.10 (05-nov):
      - Primero `Get-Process -Name Revit` vacío; después `tools/desplegar-local.ps1 -Addin acero-zapatas -Revit 2026`
        (Debug).
      - Modelo: una copia nueva de `Descargas\Prueba 080720sfsd26.0028 - copia.rvt` en el scratchpad
        (`modelo-prueba\prueba-zapatas.rvt`), abierta con `CQEING_ZAPATAS_PRUEBA`.
      - Informe: `%APPDATA%\CQEING\zapatas\prueba.txt`; el del 04-nov quedó como `prueba.txt.anterior`.
      - **Resultado: 190 pasan, 4 fallan.**
      - Casos nuevos, todos pasan:
        - 16, forma L: Revit usó la 17A; una pata al inicio, con el largo del plan.
        - 17: abajo pata de 90°, arriba 135°.
        - 18: hasta el recubrimiento, con el aviso «no cumple el largo del cuadro».
        - 19: la losa de 0.80 m con la pata del cuadro sin cortar, 0 choques.
      - «El tablero marca choques cuando Revit los tiene» pasa en todos los casos:
        - 135°: tablero 4, Revit 4.
        - 180°: tablero 4, Revit 8 (H15).
        - Trapecio: tablero 18, Revit 16.
        - Losa en L: tablero 0, Revit 0. Con la pata según el cuadro (278 mm, ahora el valor por defecto) ya no
          llega a la parrilla superior. El 04-nov, con la pata hasta el recubrimiento, daba 14 pares.
      - Las 4 fallas son de antes y ya se conocían:
        - 135° y 180°: choques reales de los ganchos con la barra del borde; ahora se ven como puntos rojos.
        - Trapecio: «2 corridas, la peor 147,3 mm» y 16 pares de choques. Lo mismo que en el informe del 04-nov
          (allí 18 pares), sin cambios por este trabajo.
- [x] Paso 10 (05-nov): `VistaReglas` y `ReglaVista` pasaron sin cambios a `acero-vigas/.../Tablero/PantallaReglas.cs`,
      junto con `PantallaReglas.De` y `PantallaReglas.ReglasDe`. `ArmadorVista.Reglas` y `ArmadorVista.ReglasDe` solo
      delegan. Las 1085 pruebas de vigas siguen en verde.
- [x] Paso 11 (05-nov):
      - El cálculo de zapatas enlaza `AlmacenReglas.cs` y `PantallaReglas.cs`.
      - `Datos` manda `reglas`, y el mensaje `guardarReglas` se valida con `ReglasAGuardar`.
      - `PuenteTablero` carga `%AppData%\CQEING\vigas\reglas.json` en cada recarga. Al guardar, guarda, recarga y
        avisa «Reglas guardadas para vigas y zapatas en este PC.».
      - Pruebas nuevas: reglas en `Datos` (#5: gancho 0.278, 180° 0.238, estribo 0.189), validación al guardar, y que
        una regla guardada (#5 a 0.300, archivo temporal) cambia la pata del cuadro. Zapatas: 77 pruebas en verde.
- [x] Paso 12 (05-nov), botón «Reglas» en la barra y pantalla `mReglas`:
      - Imágenes en `Tablero/web/img/`: `ganchos-estandar.jpeg` y `cuadro-ganchos-traslapos.jpeg`.
      - Probado en el navegador (`python -m http.server 8765` en `Tablero/web`, con reglas de ejemplo inyectadas):
        - Abre bloqueada, con los 30 campos de la tabla deshabilitados.
        - «Desbloquear» habilita los campos; «Bloquear sin guardar» vuelve a las guardadas.
        - Cerrar con cambios muestra el aviso. «Guardar y bloquear» manda `guardarReglas` con el #5 en 0.300 y la
          varilla de 12 m.
        - Al llegar las reglas nuevas, queda bloqueada. «Descartar» cierra y deja las guardadas.
        - Las dos imágenes cargan (1401 y 1461 px de ancho).
- [x] Paso 13, código (05-nov): `VentanaTablero.Apartar()` minimiza el tablero y `Volver()` lo deja como estaba.
      «Seleccionar en Revit» y «Mostrar en Revit» llaman a `Apartar()`. Falta probarlo a mano en Revit (letra M).
- [x] Paso 14, pruebas (05-nov):
      - Zapatas 77/77 y vigas 1085/1085.
      - `CQEING.Zapatas.csproj` y `CQEING.Vigas.csproj` compilan en Debug 2027 sin errores.
      - Desplegados los dos add-ins en Revit 2027 (Debug), con `Get-Process -Name Revit` vacío antes.
      - Commit, push y PR: ver el PR a `acero-zapatas`. El merge espera el OK de Luciano.
- [ ] Probado en Revit a mano (guía A a M), incluidos Reglas y el apartado del tablero.
- [ ] Preguntar a Juan qué quiso decir con «aquí debería ser por letras».
