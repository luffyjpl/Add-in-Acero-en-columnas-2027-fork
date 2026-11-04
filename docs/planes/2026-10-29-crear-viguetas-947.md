# Plan: crear viguetas por panel desde la planta del piso (#947)

## Contexto

- Se pide (29-oct) pasar la maqueta aprobada de la ventana de viguetas
  ([#945](https://github.com/utilidadcqeing-prog/CQEING_BaseDeDatos/issues/945),
  [#946](https://github.com/utilidadcqeing-prog/CQEING_BaseDeDatos/issues/946)) al add-in real: **ver todo el piso
  modelado en Revit, elegir un panel, ver sus viguetas, eliminar las que no van y crearlas**.
- Tarea: [#947](https://github.com/utilidadcqeing-prog/CQEING_BaseDeDatos/issues/947), con las sub-issues #948 a #953
  (Viguetas 1 a 6).
- Maqueta de referencia: https://claude.ai/artifact/YSc4UodwgYrHrmV3L44LtR (copia en
  `trabajo1\evidencia-945\maqueta-viguetas.html`, fuera de este repo).
- Reparto: Excel de Carlos «CANTIDAD Y LONG EXTREMOS POR PANEL» y reunión del 24-oct.
- **Solo local**: se compila y se despliega con `tools/desplegar-local.ps1`. Sin commit, push, PR ni versión hasta que
  el usuario lo pida.
- Fuera de alcance: detectar vacíos desde el modelo (el vacío se forma eliminando viguetas), paneles que no son
  polígonos cerrados por vigas, publicar una versión.
- Entorno: Revit 2027.3, .NET 10 (`-p:RevitVersion=2027`), modelo `PROY_JULIO_BETO.rvt`, rama `viguetas-crear-947`
  (desde `main` en `d2949cf`).

## Criterios de aceptación

- [ ] Botón «Crear viguetas» en el panel *Vigas* de la pestaña CQEING, que cumple la guía obligatoria de add-ins
      (licencia y bloqueo por versión antes de crear botones; `PushButton` de la misma DLL).
- [ ] Al abrir, se elige el **piso** (niveles del modelo que tienen vigas) y se ve la **planta completa**: vigas,
      columnas y paneles.
- [ ] Clic en un panel: se ven sus viguetas con **dirección X/Y**, **separación eje a eje** y **ancho**, y los tres
      datos (cantidad, eje a eje, libre en extremos) con la fórmula del Excel.
- [ ] **Eliminar viguetas** de la propuesta (con Deshacer) antes de crear.
- [ ] **Crear** las viguetas en Revit con un tipo de vigueta del modelo, en el nivel del panel; se deshacen con Ctrl+Z.
- [ ] **Quitar** las viguetas creadas por el add-in en un panel.
- [ ] Pruebas unitarias de reparto, paneles y planificación; `dotnet test` sin fallos.

## Hallazgos del código y de Revit real

- `addins/acero-vigas/AppVigas.cs` ya empieza con `Licencia.Vigente` y `BloqueoPorVersion.AlDia` y crea su botón con
  `CintaCqeing`: un segundo botón en la misma DLL cumple la guía sin otro `addin.json`.
- El tablero de vigas es un WebView2 (`Tablero/VentanaTablero.xaml.cs`) que sirve `Tablero/web/` con el host virtual
  `tablero.cqeing` y habla por JSON; lo que toca Revit va por un `ExternalEvent` (`Tablero/AccionesRevit.cs`).
- `Revit/LectorModelo.cs` ya lee vigas (eje, ancho por geometría, nivel de referencia, nombre del tipo) y columnas
  (huella en planta, cotas). Todo en mm.
- La regla de vigueta (#939) está en `Porticos/AgrupadorPorticos.cs` (`NombreDeVigueta`, ancho < 200 mm), privada.
- Pendiente de medir en Revit: cota de la línea de ubicación de las vigas de PROY_JULIO_BETO y si las viguetas creadas
  se unen solas con las vigas.

## Decisiones

1. **Mismo add-in de vigas, botón nuevo** (29-oct, por la reunión del 24-oct: Carlos prefería que estuviera junto al
   de vigas). Reusa licencia, bloqueo, WebView2 y despliegue.
2. **La misma ventana WebView2 del tablero**, con otra página (`web/viguetas.html`): `VentanaTablero` recibe la página
   y el título en vez de copiar la ventana.
3. **Los paneles se arman con los ejes de las vigas del nivel.** Por defecto solo cuentan las vigas (no las viguetas,
   según la regla de #939), para que las viguetas que ya existen no partan los paneles. Un interruptor deja usar
   también las viguetas como borde (viguetas de borde de la reunión). El contorno libre descuenta medio ancho de cada
   viga.
4. **Reparto del Excel**: n = REDONDEAR.MENOS(L / d); extremos e = (L − ((n − 1)·d + b)) / 2; si e > d − b se suma una
   vigueta. L es el ancho libre del panel en la dirección del reparto.
5. **Dirección X/Y del proyecto.** Cada vigueta es una línea a la cota del reparto recortada al contorno libre (para el
   largo y el tipo) y al contorno por ejes (para crearla en Revit, de eje a eje de viga).
6. **El cálculo vive en C#** (`calculo/…/Viguetas`), con pruebas; la página solo dibuja y pide. La propuesta se calcula
   sin pasar por el hilo de Revit (no usa la API).
7. **Marca de las viguetas creadas**: el parámetro *Comentarios* dice `CQEING viguetas`. «Quitar» borra las que tienen
   esa marca y caen dentro del panel.
8. **Tipo de vigueta**: lista de tipos de armazón estructural del modelo; se preselecciona uno que cumpla la regla de
   vigueta (VT, VB o «vigueta» en el nombre).

9. **Crear en un panel que ya tiene viguetas del add-in las reemplaza** (29-oct): primero borra las marcadas del
   panel y después crea, todo en la misma transacción. El botón dice «Reemplazar viguetas».
10. **Holgura de 300 mm** para unir vigas que terminan en la cara de la columna o de otra viga
    (`DetectorPaneles.Holgura`). Pendiente de comprobar en PROY_JULIO_BETO que no une vigas que no se tocan.
11. **Página fuera de Revit**: `web/viguetas-demo.js` trae un piso de ejemplo de 2 × 2 paneles para ver la página en un
    navegador, como `demo.js` del tablero. En Revit no se carga.

### Correcciones de la revisión (30-oct)

Pedidas en la reunión con la captura de P4 (viguetas en ↕ Y):

12. **Elegir varios paneles.** Tres modos: **Un panel**, **Fila o columna** (con un clic se eligen todos los paneles
    alineados en la dirección de las viguetas: en ↕ Y la columna, en ↔ X la fila) y **Varios** (cada clic suma o quita
    un panel). Cada panel se reparte con la fórmula del Excel sobre su propio ancho libre; los tipos se agrupan entre
    todos los paneles elegidos. Motivo: las viguetas van de arriba abajo o de lado a lado completas.
13. **Viguetas continuas.** Al crear, las piezas que están en la misma línea y se tocan en el eje de una viga (paneles
    seguidos o una viga que cruza el panel) se crean como **una sola vigueta** de punta a punta. Se puede apagar con
    «Viguetas continuas a través de las vigas».
14. **Vigueta que atraviesa una viga.** Si el eje de cualquier elemento de armazón del piso (que no sea creado por el
    add-in) cruza una vigueta dentro del panel, la vigueta se parte ahí en dos piezas (a y b), cada una hasta la cara
    de esa viga. Cada pieza se elimina por separado: así se elige de qué lado de la viga se quita.
15. **Ids de vigueta con el panel**: `P4:3`, `P4:3a`, `P4:3b` (y `P4:3.2` si el panel parte la línea en dos tramos),
    porque ahora la propuesta tiene varios paneles.

### Correcciones de la segunda revisión (02-nov)

Con la captura de P6 en ↕ Y: las viguetas propuestas no caían en la línea de las que ya están en P1 y P3, y no se
encontraba cómo elegir varios paneles.

16. **Seguir las viguetas que ya están.** Si en la misma fila o columna hay viguetas paralelas (del modelo o creadas
    por el add-in) fuera de los paneles elegidos, las viguetas nuevas van en **esas mismas líneas**. Es lo que se usa
    por defecto cuando las hay; se puede cambiar a «Fórmula del Excel». Ubicación: «Seguir las que ya están».
17. **Paneles en la misma línea, viguetas alineadas.** Si se eligen varios paneles de la misma fila o columna, se
    reparte con la fórmula del Excel el **primero** y los demás usan esas mismas líneas, para que las viguetas sean
    continuas. Un panel queda en la misma línea si su ancho en la dirección del reparto se superpone más de la mitad.
    Cada panel muestra de dónde salen sus viguetas (fórmula, alineadas con P1 o las existentes) y sus dos libres de
    extremo, que pueden no ser iguales.
18. **Elegir varios paneles, fácil:**
    - Al hacer clic en un panel que tiene otros en su columna o fila, aparece un aviso con dos botones: «Solo P6» y
      «Toda la columna (3 paneles)».
    - **Ctrl + clic** suma o quita paneles en cualquier modo, y el modo «Varios» lo hace sin Ctrl.
    - El modo del medio dice «Columna entera» o «Fila entera», según la dirección.
19. **Quitar viguetas del modelo** (02-nov, captura de P4): con «Eliminar viguetas» también se puede hacer clic en una
    vigueta que ya está en el modelo (azul oscuro) dentro de los paneles elegidos. Queda marcada y se borra de Revit al
    **Crear**, en la misma transacción (Ctrl+Z la devuelve). Las marcadas dejan de servir de guía para «Seguir las que
    ya están». Solo se aceptan elementos de armazón que cumplen la regla de vigueta; las vigas no se pueden quitar.
    ~~Se borraba el elemento entero.~~ Ver la decisión 21.
21. **Solo se quita el tramo dentro de los paneles elegidos** (02-nov): una vigueta del modelo suele ser un solo
    elemento que cruza varios paneles (P1, P4 y P6). Al quitarla, se recorta: el elemento queda con el tramo de un lado
    (su línea de ubicación se acorta hasta el eje de la viga del borde del panel) y, si queda también un tramo del otro
    lado, se crea una copia del mismo elemento con ese tramo. Si no queda nada afuera, se borra. Los tramos se calculan
    con el contorno por ejes de los paneles (`PisoViguetas.Restos`). La página dibuja punteado solo lo que se quita.
22. **Las viguetas del modelo se quitan por piezas, como las propuestas** (02-nov): dentro de los paneles elegidos,
    cada vigueta del modelo se parte donde la cruza otro elemento de armazón (una viga), y cada pieza se marca por
    separado: se puede quitar solo la de arriba o la de abajo de la viga. Al crear, el elemento queda con lo que no se
    marcó (decisión 21: se acorta y, si hacen falta más tramos, se copia). Ids de pieza: `<id del elemento>#<n>`.
### Correcciones de la reunión del 03-nov

23. **La vigueta llega al eje de la viga y no se corta en cada viga.** Se dibuja y se crea de eje a eje (hasta la mitad
    de la viga de apoyo). Las piezas en la misma línea siempre se unen al crear, a través de las vigas que cruzan: se
    quita la casilla «Viguetas continuas» (~~decisión 13, se podía apagar~~). Solo se corta donde el usuario quita una
    pieza.
24. **Seleccionar por perímetro** (modo nuevo, el primero y el que viene marcado): el usuario hace clic en los elementos
    de armazón de la planta (vigas, viguetas de borde y **voladizos**) y con ellos se arma el perímetro de trabajo
    (`PisoViguetas.Perimetro`, con `DetectorPaneles` sobre solo esos elementos). Las vigas de adentro que no se
    eligieron no parten el perímetro: cruzan las viguetas, que se parten ahí en piezas (decisión 14) para poder quitar
    de un solo P, y se crean continuas. Los perímetros se llaman Z1, Z2…
25. **Quitar para el vacío en un solo P**: al quitar una pieza (propuesta o del modelo) solo se quita la de ese P; lo que
    sigue en la misma línea se queda y termina en el eje de la viga. Con prueba.
28. **Las viguetas del modelo de adentro también son guía** (03-nov, prueba en Revit con el perímetro Z1 de la
    columna P1-P4-P6: las nuevas no quedaban en línea con las que ya estaban en P1 y P6). Cambia la decisión 16:
    - Guías: todas las viguetas paralelas del modelo y las creadas, estén dentro o fuera de lo elegido.
    - Donde ya hay una vigueta del modelo (una pieza de `PiezasDelModelo`, quitada o no) no se propone otra encima: la
      pieza propuesta que cae sobre ella, en la misma línea, se omite. Así las nuevas solo rellenan los tramos que faltan,
      y donde el usuario quitó una del modelo para el vacío tampoco aparece una nueva.
29. **Las viguetas se ven pasar las vigas** (03-nov): en la planta, las piezas de las viguetas del modelo ya no llevan
    las vigas dibujadas encima; van de eje a eje, como las propuestas, y se ven continuas. Cuando se quita una pieza de
    un panel, las que quedan terminan en el eje de la viga, porque cada pieza llega justo hasta ahí. Solo cambia el
    dibujo (`viguetas.js`); el cálculo y lo que se crea en Revit siguen igual.
30. **Las viguetas terminan en las vigas que acaban dentro del panel** (03-nov, P4: al borrar todas y crearlas de
    nuevo, pasaban por encima de la viga corta, y al crear solo las que faltaban quedaban pegadas a ella). Cambia la
    decisión 23 solo para esas vigas:
    - Viga que cruza el panel o el perímetro de lado a lado (sus dos puntas en el borde): las viguetas la atraviesan y
      se crean continuas.
    - Viga que termina dentro del panel (alguna punta adentro, a más de 50 mm del borde, como la viga corta de P4): la
      vigueta se parte ahí, cada pieza llega a su eje y al crear no se unen (`Vigueta.TopeDesde` / `TopeHasta`).
26. ~~**Release**: publicar `acero-vigas-v1.5.0`.~~ Ver la decisión 27.
27. **Add-in propio, `crear-viguetas`** (03-nov, pedido del usuario: «que sea libre, que aparezca en el gestor para
    descargar y no se descargue junto con otro»). Cambia la decisión 1: deja de ser un botón de «Acero en vigas».
    - Carpeta `addins/crear-viguetas/` como `despiece`: `addin.json` (id `crear-viguetas`), `CQEING.Viguetas.csproj`,
      `CQEING.Viguetas.addin` con su propio ClientId, `AppViguetas.cs` (licencia y bloqueo por versión, panel
      «Viguetas»), `Interfaz/` (comando, puente, ventana WebView2 con host `viguetas.cqeing` y su cola de Revit),
      `Revit/` (lector del piso y creador), `web/` (la página) y `calculo/` (`CQEING.Viguetas.Calculo` en net8.0 puro,
      con sus pruebas).
    - Los archivos de viguetas se mueven desde «Acero en vigas» (con `git mv`, para no perder la historia). Se enlazan,
      sin copiarlos, `ReglaVigueta.cs` y `CalculadoraZoom.cs` del cálculo de vigas, como hace `despiece`.
    - «Acero en vigas» vuelve a quedar como antes de #947: sin el botón y sin el parámetro de página de su ventana. La
      regla de vigueta pública (`ReglaVigueta.cs`) se queda allí, porque la usan los pórticos.
    - Release: `crear-viguetas-v1.0.0` (add-in nuevo) y `acero-vigas-v1.5.0` (se le quita el botón, para que no
      aparezca dos veces). Las dos etiquetas van de a una, sobre `main` y después de unir el PR.
20. **Aviso de libre grande**: si un panel queda con un libre de extremo mayor que el máximo (eje a eje − ancho), por
    ejemplo al seguir pocas viguetas existentes, se avisa debajo del dibujo.

## Plan paso a paso

1. **Regla de vigueta pública** — `Porticos/ReglaVigueta.cs`, `AgrupadorPorticos.cs` — los tests de pórticos siguen en
   verde.
2. **Reparto del Excel** (#950) — `Viguetas/RepartoViguetas.cs` + tests con 2,00 / 3,57 / 2,84 / 2,66 / 3,12 m.
3. **Paneles** (#949) — `Viguetas/DetectorPaneles.cs` + tests: una grilla 2 × 1, un voladizo que no cierra, viguetas
   que no parten el panel.
4. **Planificación** (#950) — `Viguetas/PlanificadorViguetas.cs` + tests: posiciones, largos, tipos, borradas.
5. **Protocolo** — `Viguetas/ProtocoloViguetas.cs` (JSON de ida y vuelta) + test.
6. **Lectura del piso** (#948) — `Revit/LectorPiso.cs`.
7. **Crear y quitar** (#952) — `Revit/CreadorViguetas.cs`.
8. **Ventana** (#951) — `Viguetas/ComandoViguetas.cs`, `Viguetas/PuenteViguetas.cs`, `Tablero/VentanaTablero.xaml.cs`
   (página y título), `Tablero/web/viguetas.html`, `Tablero/web/viguetas.js`, botón en `AppVigas.cs`.
9. **Compilar, pruebas y despliegue** — comandos de abajo.
10. **Prueba en Revit** (#953) — lista de abajo, con capturas en `docs/planes/imagenes/`.
11. **Correcciones del 30-oct** (decisiones 12 a 15) — `PlanificadorViguetas.cs` (varios paneles, cortes en vigas,
    líneas continuas), `PisoViguetas.cs` (cruces), `ProtocoloViguetas.cs` (lista de paneles), `PuenteViguetas.cs`,
    `CreadorViguetas.cs` (crea líneas), `viguetas.js` y `viguetas.html` (modos de selección, continuas) + tests:
    pieza partida por una viga, dos paneles seguidos dan una sola línea continua, y sin continuas dan dos.

## Cómo probarlo

```
dotnet test addins/acero-vigas/calculo/CQEING.Vigas.Calculo.Tests/CQEING.Vigas.Calculo.Tests.csproj
dotnet build addins/acero-vigas/CQEING.Vigas.csproj -c Debug -p:RevitVersion=2027
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin acero-vigas -Revit 2027
```

1. Abrir PROY_JULIO_BETO en Revit 2027 → CQEING → Vigas → **Crear viguetas**.
2. Elegir el piso: se ve la planta con vigas, columnas y paneles.
3. Clic en un panel: viguetas en azul, cotas y los tres datos. Cambiar ↔ X / ↕ Y, separación y ancho.
4. **Eliminar viguetas**: quitar dos, Deshacer una.
5. **Crear**: aparecen en Revit en el nivel del panel. Ctrl+Z las quita.
6. **Quitar creadas** del panel: se borran solo las del add-in.

## Estado

- [x] Pasos 1 a 9 implementados (29-oct), en la rama local `viguetas-crear-947`, sin commit.
- [x] `dotnet test CQEING-Addins.sln`: 0 fallos (Vigas.Calculo 1067, Gestor.Core 256, Gestor 37, Publicador 8). Las
      pruebas nuevas están en `ViguetasTests.cs`: los cinco casos del Excel, paneles de una grilla, voladizo, viga que
      termina en la cara, viguetas que no parten el panel, reparto en X e Y, panel en L y el mensaje de crear.
- [x] Compilación para Revit 2027 sin advertencias y desplegada con `desplegar-local.ps1`.
- [x] Página revisada en el navegador con el piso de ejemplo: piso, panel, X/Y, eliminar y cotas (2,00 m da 2 viguetas,
      0,80 y 0,55).
- [x] En Revit con PROY_JULIO_BETO (29-oct): N+3.70 da 7 paneles; P1 (libre 3,13 × 3,73 m) da 4 viguetas, 0,80 y 0,62.
      «Eliminar viguetas» quedaba desactivado porque la propuesta llega después de elegir el panel; se corrigió en
      `viguetas.js` (`pie()` actualiza el botón) y el usuario confirmó que ya elimina.
- [x] En Revit (29-oct, prueba del usuario): crear, Ctrl+Z, reemplazar y quitar creadas funcionan. Las viguetas quedan a
      la altura de las vigas, de viga a viga y con el tipo elegido; «Quitar» solo borra las del add-in.

### Criterios de aceptación

- ✅ Botón «Crear viguetas» en *Vigas*, con licencia y bloqueo por versión (`AppVigas.cs`).
- ✅ Elegir el piso y ver la planta completa con vigas, columnas y paneles.
- ✅ Panel con viguetas, X/Y, eje a eje, ancho y los tres datos del Excel.
- ✅ Eliminar viguetas de la propuesta, con Deshacer.
- ✅ Crear en Revit con el tipo elegido, en el nivel del panel; Ctrl+Z las deshace.
- ✅ Quitar las creadas por el add-in en un panel.
- ✅ Pruebas unitarias y `dotnet test CQEING-Addins.sln` sin fallos.

### Correcciones del 30-oct (decisiones 12 a 15)

- [x] Implementadas: modos Un panel / Fila o columna / Varios, viguetas continuas a través de las vigas y viguetas
      partidas donde las cruza una viga (cada lado se elimina por separado).
- [x] `dotnet test CQEING-Addins.sln` sin fallos (Vigas.Calculo 1070). Pruebas nuevas: pieza partida por una viga,
      borrar un lado deja la vigueta hasta la viga, dos paneles seguidos dan tres viguetas continuas de 0 a 8 m.
- [x] Página revisada con el piso de ejemplo: la columna de P3 en ↕ Y elige P1 y P3; Varios suma P4; en ↔ X la vigueta
      1 de P3 sale partida en P3:1a y P3:1b por la viga corta y se elimina solo el lado b.
- [x] Compilado y desplegado para Revit 2027.
- [ ] Probado en Revit con PROY_JULIO_BETO: falta.

### Correcciones del 02-nov (decisiones 16 a 18)

- [x] Implementadas: seguir las viguetas que ya están en la fila o columna, alinear los paneles de una misma línea con
      el primero, aviso «Solo P… / Toda la columna» al elegir un panel, Ctrl + clic para sumar o quitar y el modo
      «Columna entera» / «Fila entera».
- [x] `dotnet test CQEING-Addins.sln` sin fallos (Vigas.Calculo 1072). Pruebas nuevas: una columna con un panel más
      angosto usa las líneas del primero, y un panel sigue las viguetas que ya están (1,00 y 2,20 m, eje a eje 1,20).
- [x] Página revisada con el piso de ejemplo: P3 en ↕ Y sigue las viguetas de P1, el aviso ofrece la columna y Ctrl +
      clic suma P4.
- [x] Desplegado para Revit 2027 (02-nov).
- [x] Decisiones 19 y 20 (quitar viguetas del modelo y aviso de libre grande): implementadas, con prueba
      (`LasViguetasDelModeloQueSeQuitanNoSonGuiaYLasVigasNoSeQuitan`); `dotnet test` sin fallos (Vigas.Calculo 1073).
      Página revisada: en P1 del ejemplo se marcan dos viguetas del modelo y salen como «Del modelo × 2».
- [x] Desplegado de nuevo para Revit 2027 (02-nov).
- [x] En Revit, el usuario vio que quitar una vigueta del modelo borraba la vigueta entera de P1, P4 y P6: se cambió a
      recortar (decisión 21), con prueba `QuitarUnaViguetaDelModeloSoloQuitaElTramoDelPanelElegido`; `dotnet test` sin
      fallos (Vigas.Calculo 1074).
- [x] Desplegado con el recorte para Revit 2027 (02-nov).
- [x] En Revit, el usuario pidió quitar también por piezas las viguetas del modelo (decisión 22): implementado, con
      pruebas `UnaViguetaDelModeloSeQuitaPorPiezasDeCadaLadoDeLaViga` y
      `LasViguetasDelModeloConPiezasQuitadasNoSonGuiaYLasVigasNoSonPiezas`; `dotnet test` sin fallos (Vigas.Calculo
      1074). Página revisada con el piso de ejemplo y desplegado para Revit 2027.
- [x] Probado en Revit con PROY_JULIO_BETO (02-nov, prueba del usuario): en P4 se quita solo el tramo de un lado de la
      viga corta y el resto de la vigueta del modelo se queda. Todas las correcciones del 30-oct y del 02-nov
      (decisiones 12 a 22) quedan probadas en Revit.

### Correcciones del 03-nov (decisiones 23 a 27)

- [x] Viguetas de eje a eje y siempre continuas; modo Perímetro (por defecto) con vigas, viguetas de borde y voladizos;
      quitar en un solo P. Pruebas nuevas: `ElPerimetroConLasVigasDeAfueraEsUnoYLasDeAdentroSoloPartenLasViguetas`,
      `QuitarUnaViguetaEnUnSoloPDejaLasQueSiguenHastaElEje`, `ElPerimetroPuedeCerrarseConVoladizosYUnaViguetaDeBorde`.
- [x] Página revisada con el piso de ejemplo: el perímetro con cuatro vigas arma Z1, los elementos elegidos se ven en
      naranja y las viguetas van de eje a eje.
- [x] Add-in propio `addins/crear-viguetas/` (decisión 27). «Acero en vigas» vuelve a como estaba antes de #947.
      `dotnet test CQEING-Addins.sln` sin fallos (Viguetas.Calculo 26, Vigas.Calculo 1051, Gestor.Core 256 con
      `TodosLosAddinsExigenLicenciaTests`, Gestor 37, Publicador 8). Los dos compilan para 2027 sin advertencias y
      están desplegados con `desplegar-local.ps1`.
- [x] Probado en Revit por el usuario (03-nov): todo bien, salvo que las nuevas no quedaban en línea con las del modelo
      dentro del perímetro. Corregido con la decisión 28, con prueba
      `EnElPerimetroLasNuevasSiguenLasDelModeloYSoloRellenanLosTramosQueFaltan`; `dotnet test` sin fallos
      (Viguetas.Calculo 27).
- [x] Decisión 28 desplegada para Revit 2027 (03-nov). El usuario la probó: todo bien.
- [x] Decisiones 29 (las viguetas se ven pasar las vigas en la planta) y 30 (terminan en las vigas que acaban dentro
      del panel, también al crearlas todas de nuevo). Pruebas nuevas o cambiadas: `UnaVigaQueCruzaParteLaViguetaEnDosLados`
      y `LaVigaQueCruzaElPanelDeLadoALadoNoParaLasViguetas`; `dotnet test` sin fallos (Viguetas.Calculo 28). Desplegado
      para Revit 2027.
- [ ] Probar la decisión 30 en Revit, en P4: falta.
- [ ] Unir `main` en la rama, commit, PR, y publicar `crear-viguetas-v1.0.0` y `acero-vigas-v1.5.0`.

Pendiente: commit, PR y versión (`acero-vigas` 1.2.0) cuando el usuario lo pida; detectar vacíos desde el modelo
queda fuera de esta tarea.
