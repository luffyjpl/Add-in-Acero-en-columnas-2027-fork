# Plan: la elevación muestra dónde arrancan los estribos y su forma, textos más legibles y zoom en los dibujos

## Contexto

- Pedido de Luciano (3-nov), después del hotfix de contra zapata (PR #240,
  `2026-11-03-contra-zapata-piso-entero.md`):
  1. En la elevación del paso «Estribos» de la ventana web, que se vea de
     dónde arrancan los estribos (sobre la zapata, nunca dentro) y la forma
     que toman (captura: `docs/planes/imagenes/2026-11-03-elevacion-estribos-antes.png`,
     pendiente de guardar).
  2. Lo que le dijo el ingeniero: "las letras en gris en las medidas y
     algunas letras no se pueden ver mucho", y "sería mejor que se pueda
     hacer zoom en las previsualizaciones para ver los números también".
- La ventana es la web (`addins/armado-columnas/Tablero/`, #205 a #212),
  no la WPF vieja.
- Fuera de alcance:
  - la sección chica del paso «Estribos» por tramo (sigue mostrando la
    sección común);
  - la vista 3D;
  - el tablero de vigas.
- Entorno: Revit 2027, .NET 10, worktree `C:/Proyecto/addins-240`, rama
  `columnas/vista-previa-legible`, que sale de `hotfix-contra-zapata-piso-entero`
  ya mezclada con `origin/main` (`ec9484d`).

## Criterios de aceptación

- [ ] En la elevación, cada tramo se dibuja con su ancho real en proporción.
      Por ejemplo, un dado de 40 cm se ve más ancho que la columna de 30 cm,
      y sus estribos también.
- [ ] Se ve dónde está la cara superior de la zapata y a cuánto de ella
      queda el primer estribo (medida real, del mismo reparto que usa el
      generador).
- [ ] La zapata dice que adentro no van estribos.
- [ ] Los textos grises de los dibujos y de los paneles son más oscuros, y
      los de los dibujos son más grandes.
- [ ] Los dibujos de los pasos Barras, Estribos, Grapas y Traslapos tienen
      zoom:
      - botones − / + / «Ajustar»;
      - la rueda del mouse o pellizcar en el touchpad, hacia donde está el
        mouse (decisión 11);
      - con zoom el dibujo se mueve arrastrando con clic, sin barras de
        desplazamiento (decisión 11).
- [ ] Con zoom, poner y mover grapas en el paso «Grapas» sigue cayendo en la
      barra correcta.
- [ ] `tools/PruebasTableroColumnas` y `tools/PruebasTraslapos` con 0 fallas,
      `dotnet build` sin advertencias y `dotnet test CQEING-Addins.sln` sin
      fallos.

## Hallazgos del código

Lectura de código, 3-nov:

1. **`Elevacion` (`Tablero/web/dibujos.js`)** dibuja todos los tramos con
   el mismo ancho fijo (`cw = 54` px). Por eso no se ve que el dado es más
   ancho que la columna.
   - `TramoVista` (`Tablero/VistaColumnas.cs`) no lleva el ancho de cada
     tramo, aunque `Tramo.LadoX/LadoY` sí lo tienen (en metros, 22-oct).
2. **Los estribos de la elevación están separados de más a propósito**
   cuando la altura es poca (`f = max(1, 3.2 / minimo)`), así que su
   posición no sirve para medir. La altura real de cada estribo está en
   `RepartoEstribos.Resultado.Alturas`, que `ArmadorVistaColumnas.Zonas` ya
   calcula y no manda a la página.
3. **Zapata:** `ZapataVista(ZInf, ZSup, Ancho)`, con `ZSup` igual a la cara
   superior (`columna.ZInferior`), que es donde arrancan los estribos.
4. **Grises:** `C.suave = #5E6670` en los dibujos y `--suave` igual en el
   CSS, con textos de 9 a 11 px en los SVG. `--tenue = #8A94A0`.
5. **Zoom:**
   - La página entera se achica para entrar en la ventana y nunca se agranda
     (`CalculadoraZoom`).
   - El zoom propio de WebView2 está apagado (`IsZoomControlEnabled = false`,
     `VentanaColumnas.xaml.cs`).
   - Los dibujos son SVG dentro de `.medida`, que mide su tamaño para
     dibujar (`medir`).
6. **Paso «Grapas»:** `posicionEnSvg` (`Seccion`, `dibujos.js`) usa
   `clientX - rect.left` en píxeles de pantalla. Con zoom, esos píxeles ya
   no coinciden con los del dibujo, así que hay que dividir por la escala
   (`rect.width / w`).
7. **`demo.js`** hace de C# fuera de Revit (con `#normal`, `#simple` y
   `#varias`). Tiene que traer los mismos campos nuevos para que la página
   se pueda probar en un navegador.

Probando con la demo en Chrome (3-nov, `http://127.0.0.1:8765/index.html#normal`):

8. **La barra de zoom abajo tapaba la zapata y la cota del primer estribo.**
   Va arriba a la derecha. En «Traslapos», donde la franja de arriba tiene
   los títulos de las varillas, va abajo (`abajo=${true}`).
9. **Ctrl + rueda varias veces seguidas se quedaba en 125 %** (cada paso leía
   el zoom viejo), y el desplazamiento se aplicaba antes de que el SVG
   creciera, así que el navegador lo dejaba en 0. Ahora:
   - `actual()` lee el zoom pedido;
   - el desplazamiento se aplica en `componentDidUpdate`, después de
     `escalar()`.
   - Con 6 pasos de rueda sobre la zapata llegó a 400 %, con la zapata a la
     vista.
10. **El rótulo de un tramo muy bajo** (el dado de 0,20 m mide unos 6 px a
    escala) se montaba con la cota del primer estribo. Los tramos de menos de
    13 px no llevan rótulo, pero lo muestran al pasar el mouse (`<title>`).
    La cota del primer estribo va en una sola línea.
11. **`tools/PruebasVistaPrevia` fallaba en `lindero_alcance_gancho_90`**
    (también en `main`). La prueba es del #139 y espera el alcance viejo
    (B + 2,5 diámetros). En #177 (`822ffb2`, 28-oct) `Lindero.AlcanceGancho90`
    pasó a B + medio diámetro a propósito, y la prueba no se actualizó. Esta
    herramienta no corre con `dotnet test`, así que nadie lo vio. Se
    actualizó la prueba, no el código.
12. **Ya pasaba antes:** los niveles negativos se rotulaban "N+-0.65" (se ve
    en la captura de Luciano). Ahora salen como "N-0.65".

## Decisiones

1. **Ancho por tramo** (3-nov, Claude): `TramoVista` suma `LadoX` y `LadoY`
   en metros. La elevación es "de frente", así que usa `LadoX`.
   - El tramo más ancho ocupa los 54 px de siempre y los demás van en
     proporción, con un mínimo de 30 px.
   - Las barras longitudinales siguen en la zona común (`BaseX`), como las
     pone el generador. En el dado se ve el estribo más ancho alrededor de
     las mismas barras.
2. **Primer estribo** (3-nov, Claude): `ColumnaVista` suma `PrimerEstribo`
   (metros, o `null`). Es la altura del primer estribo del tramo más bajo
   que lleva estribos, sacada de `reparto.Alturas`.
   - En la elevación se dibuja con una línea propia en su altura real.
   - Va con una cota desde la cara de la zapata: "1.er estribo a X cm de la
     zapata".
3. **Zapata en el dibujo:** el rótulo pasa a "Zapata" y, debajo, "sin
   estribos adentro". Va también una línea fina "cara de la zapata"
   (Santiago, 3-nov: "no importa que los estribos estén cerca de la zapata,
   mientras que no estén dentro de ella").
4. **Grises más oscuros:**
   - `suave` pasa de #5E6670 a #3F4650 y `tenue` de #8A94A0 a #6B7480, tanto
     en `dibujos.js` como en `columnas.css`.
   - En los SVG, los textos de 9 a 11 px pasan a 11,5 a 12,5 px.
5. **Zoom por dibujo, no de la página entera:**
   - Un componente `Ampliable` en `dibujos.js` envuelve el SVG y lo
     redibuja con ancho y alto × zoom, con el mismo `viewBox`. Así queda
     vectorial y nítido.
   - Se recorre con scroll.
   - Tiene botones − / + / «Ajustar» (vuelve a 1) y Ctrl + rueda.
   - El zoom va de 1 a 4, en pasos de 25 %.
   - Se recuerda por paso mientras la ventana está abierta, y no se guarda.
   - `posicionEnSvg` se corrige con la escala (hallazgo 6).
6. **Con zoom, cada estribo en su altura real** (3-nov, Luciano, al ver la
   captura al 400 %: "¿la línea azul que aparece arriba de la línea negra es
   un estribo? Porque está mucho más ancho que los demás").
   - Sí, era un estribo: el del dado, que en la demo mide 55 cm contra 40 cm
     de la columna.
   - Lo engañoso era otra cosa: la separación que se agrega para que se lean
     (hallazgo 2) se calculaba siempre al 100 %, así que con zoom tampoco se
     veían en su lugar.
   - Ahora `ZonaVista` trae las `Alturas` reales (las de `reparto.Alturas`
     que caen en la zona), y `Elevacion` recibe el zoom.
   - Si a ese zoom los estribos quedan a 3 px o más, se dibujan en su altura
     real. Si no, se separan como antes, pero con la cuenta hecha con el zoom.
   - Las líneas de los estribos no engordan con el zoom
     (`vector-effect: non-scaling-stroke`).
7. **De frente o de costado** (3-nov, pendiente que señaló Claude): en el
   panel derecho del paso «Estribos» se elige «De frente (X)» o «De costado
   (Y)». La elevación usa `LadoX/BaseX` o `LadoY/BaseY`. Arranca de frente y
   se recuerda mientras la ventana está abierta.
8. **La sección del tramo elegido** (3-nov, mismo origen):
   - `ColumnaVista` suma `SeccionesTramo`: una sección por tramo cuya medida
     es distinta de la de la columna (más de 0,5 cm), con el estribo de ese
     tramo (su barra y su medida).
   - Las barras y las piezas son las de la zona común, corridas a su lugar
     dentro del tramo (`Tramo.ComunDesdeX/Y`), como en la ventana vieja
     (`PanelVistaPrevia`).
   - Con un tramo elegido, la sección chica del paso «Estribos» muestra la
     suya; en un nudo, sin grapas ni cerrados, como en `GeneradorArmado`.
   - El título dice de qué tramo es.

9. **La elevación a escala real** (3-nov, Luciano, con la captura del dado:
   "si la línea azul es un estribo, pues está mal. El dado no se logra
   apreciar y, teniendo en cuenta la regla de Carlos de que todo se vea con
   tamaños y distancias lo más realistas posible, sería bueno que se viera").
   Reemplaza el ancho proporcional con mínimo de 30 px de la decisión 1, y
   la separación "para que se lean" de la decisión 6:
   - Es una sola escala `k` (px por m) para el ancho y el alto. Cada tramo
     se dibuja con su ancho real (`LadoX` o `LadoY`), en su lugar respecto de
     la zona común (`TramoVista.DesdeX/DesdeY`, que se agregan).
   - Las barras van en su posición real (la de la sección) y con su
     diámetro a escala, con un mínimo de 1 px.
   - Cada estribo es una línea en su altura real, de recubrimiento a
     recubrimiento del tramo y con su diámetro a escala.
     - Si al zoom actual dos estribos quedan a menos de 3 px, la zona se ve
       como una franja de su color, sin líneas: al 100 % y en una columna de
       varios pisos son demasiados para separarlos.
     - Con zoom aparecen uno por uno.
   - Las vigas siguen recortadas a los costados (no tienen largo real en
     este dibujo) y la zapata, sin ancho conocido, se dibuja más ancha que el
     tramo de abajo.
   - La zona para hacer clic en un tramo mide por lo menos 24 px de ancho,
     aunque el tramo dibujado sea más angosto.

10. **Los estribos tienen la misma medida en toda la columna** (3-nov,
    Luciano: "los estribos deberían generarse con las mismas medidas en toda
    la columna, ya pasamos por esto antes"). Corrige las decisiones 1 y 8:
    - El generador ya lo hace desde el 8-oct (`GeneradorArmado.cs`,
      comentarios en las líneas ~156 y ~309): si una pieza es más ancha que
      la zona común (el dado), su estribo se arma con la zona común, con el
      mismo recubrimiento. En Revit estaba bien; el error era solo del dibujo
      nuevo.
    - El plan `2026-10-22-seccion-por-tramo-dado.md` decía "cada estribo sale
      con la forma de su propio elemento". Luciano confirmó (3-nov) que fue
      una equivocación: los estribos van como en el resto de la columna, con
      la misma medida, el mismo recubrimiento y la misma posición en planta
      que los de arriba. Ese plan quedó corregido (tachado y con la nota).
    - En la elevación, el hormigón de cada tramo va con su medida, pero los
      estribos de todos los tramos van con el ancho de la zona común y en su
      lugar (de recubrimiento a recubrimiento de la zona común).
    - En `SeccionesTramo`, el estribo es el de la columna corrido a su lugar
      dentro del tramo, no uno con la medida del tramo. Igual en `demo.js`.

11. **El zoom como en vigas** (3-nov, Luciano, después de probar en Revit:
    "sería mejor que el zoom fuera como en vigas, que con la rueda normal (o
    expandiendo en touchpad) se hace el zoom y el movimiento no sea con
    scroll sino moviendo con el clic"). Corrige la decisión 5:
    - La rueda sola acerca o aleja hacia donde está el mouse, sin Ctrl. El
      pellizco del touchpad llega como rueda con Ctrl y hace lo mismo. Sobre
      el dibujo, la rueda ya no desplaza la página.
    - El paso de la rueda es continuo (`exp(-deltaY · 0,0015)`, y 0,01 para
      el pellizco, que manda saltos chicos), como en `despiece/web/pasos.js`.
      Los botones − / + siguen en pasos de 25 %. El rango sigue de 1 a 4.
    - Con zoom, el dibujo se mueve arrastrando con el botón izquierdo o el
      del medio (cursor de mano). `.zoom-area` pasa a `overflow: hidden`: el
      desplazamiento lo maneja el arrastre, no hay barras.
    - Un clic sin mover (menos de 4 px) sigue eligiendo tramos, barras y
      grapas. Si hubo arrastre, el clic que le sigue se descarta, para no
      elegir algo al soltar. La captura del puntero se toma recién al pasar
      los 4 px, así el clic normal llega a lo que está debajo.
    - El arrastre de formas desde la paleta del paso «Grapas» es de HTML
      (`draggable`), no pasa por el puntero, y no cambia.

## Plan paso a paso

1. **Datos nuevos** — `Tablero/VistaColumnas.cs`, `Tablero/ArmadorVistaColumnas.cs`:
   `TramoVista.LadoX/LadoY` y `ColumnaVista.PrimerEstribo`. Prueba en
   `tools/PruebasTableroColumnas`: los lados del tramo llegan a la vista y
   `PrimerEstribo` es el primero de `reparto.Alturas` del tramo de abajo.
2. **`demo.js`:** los mismos campos, con un dado más ancho en `#normal`.
3. **Elevación** (`dibujos.js`, `Elevacion`): ancho por tramo, la línea de
   la cara de la zapata, el primer estribo con su cota y el rótulo de la
   zapata (decisiones 1 a 3).
4. **Grises y tamaños** (`dibujos.js`, `columnas.css`): decisión 4.
5. **Zoom** (`dibujos.js` `Ampliable`, `columnas.js` en los cuatro lienzos,
   `columnas.css`): decisión 5, más la corrección de `posicionEnSvg`.
5b. **Decisiones 6 a 8:**
   - C#: `ZonaVista.Alturas` y `ColumnaVista.SeccionesTramo`, con su prueba en
     `tools/PruebasTableroColumnas`;
   - `demo.js`: lo mismo;
   - `dibujos.js`: `Elevacion` con `zoom` y `cara`;
   - `columnas.js`: el selector de cara y la sección del tramo.
6. **Probar en el navegador con la demo:**
   - servir `addins/armado-columnas/Tablero/web` y abrir `index.html#normal`
     en Chrome;
   - recorrer los pasos y probar zoom, Ctrl + rueda y grapas con zoom;
   - guardar capturas en `docs/planes/imagenes/`.
7. **Build y pruebas** — add-in para 2027 y 2026, `tools/PruebasTableroColumnas`,
   `tools/PruebasTraslapos` y `dotnet test CQEING-Addins.sln`.

## Cómo probarlo

```
dotnet run --project tools/PruebasTableroColumnas
dotnet run --project tools/PruebasTraslapos
dotnet build addins/armado-columnas/practicacq.csproj -c Debug -p:RevitVersion=2027
dotnet test CQEING-Addins.sln
python -m http.server 8765 --directory addins/armado-columnas/Tablero/web   # y abrir http://localhost:8765/index.html#normal
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin armado-columnas -Revit 2027
```

En Revit (pendiente): la columna del modelo de referencia en el paso
«Estribos»:

1. El dado se ve más ancho, con estribos desde la zapata.
2. La cota del primer estribo coincide con lo que se genera.
3. Con zoom se leen las medidas.

## Estado

- [x] Plan revisado con el usuario (3-nov, pedido directo de Luciano).
- [x] Pasos 1 a 7 implementados (3-nov), sin commit, en `columnas/vista-previa-legible`:
  - C#: `TramoVista.LadoX/LadoY` y `ColumnaVista.PrimerEstribo`
    (`VistaColumnas.cs`, `ArmadorVistaColumnas.cs`), con su prueba en
    `tools/PruebasTableroColumnas`.
  - Web: elevación (`dibujos.js`), zoom `Ampliable`, grises y tamaños,
    demo (`demo.js`) y el texto de la zapata en el panel (`columnas.js`).
  - `tools/PruebasVistaPrevia`: prueba vieja corregida (hallazgo 11).
- Verificado sin Revit (3-nov):
  - Demo en Chrome: los cuatro pasos, sin errores de consola.
    - Ctrl + rueda hasta 400 % sobre la zapata.
    - Una grapa en C puesta con zoom al 150 % cayó en la fila 1, en la barra
      del clic.
    - Capturas: `imagenes/2026-11-03-elevacion-estribos-100.jpg` y
      `imagenes/2026-11-03-elevacion-estribos-zoom-400.jpg`.
  - `tools/PruebasTableroColumnas`, `tools/PruebasTraslapos` y
    `tools/PruebasVistaPrevia`: 0 fallas.
  - Build para 2027 y 2026: 0 advertencias. `dotnet test CQEING-Addins.sln`:
    0 fallas.
- Desplegado en Revit 2027 desde `C:/Proyecto/addins-240` (3-nov), con Revit
  cerrado y comprobado con `Get-Process`.
- Segunda vuelta (3-nov, decisiones 6 a 10), sin commit:
  - C#: `ZonaVista.Alturas`, `ColumnaVista.SeccionesTramo` (el estribo es el
    de la columna, corrido) y `TramoVista.DesdeX/DesdeY`. Pruebas:
    `Elevacion_TraeElPrimerEstriboYLosLadosDeCadaTramo` y
    `SeccionTramo_ElDadoMasAnchoTraeSuSeccionCorrida`.
  - Web:
    - `Elevacion` a escala real, con la cara X o Y y el zoom;
    - estribos uno por uno desde 3 px y, más chicos, como franja;
    - selector «Frente (X) / Costado (Y)» y sección del tramo elegido en el
      panel derecho.
  - La decisión 6 decía que el grosor de los estribos no cambiaba con el
    zoom. Con la decisión 9, estribos y barras van con su diámetro a
    escala; solo el borde de selección no engorda (`non-scaling-stroke`).
  - Demo en Chrome:
    - el dado de 55 cm se ve más ancho que la columna de 40, con el mismo
      estribo alrededor de las barras;
    - la sección del tramo 1 dice "55 × 55 cm";
    - de costado y con zoom al 400 %, sin errores de consola.
    - Captura: `imagenes/2026-11-03-elevacion-escala-real-dado-400.jpg`.
  - Las tres herramientas de pruebas: 0 fallas. Build para 2027 y 2026: 0
    advertencias. `dotnet test CQEING-Addins.sln`: 0 fallas.
  - Desplegado de nuevo en Revit 2027 (Revit cerrado, comprobado).
- Commit `9ac07cd` en `hotfix-contra-zapata-piso-entero` (3-nov). Luciano
  pidió sumarlo a la misma rama del PR #240, así que la rama
  `columnas/vista-previa-legible` se juntó con ella y se borró. El PR #240
  ahora cubre los dos planes.
- [x] Probado en Revit por Luciano (3-nov): "Funciona bien". Pidió el zoom
  como en vigas (decisión 11).
- Decisión 11 implementada (3-nov), sin commit: `Ampliable` en `dibujos.js`
  (rueda continua, arrastre con umbral de 4 px, clic descartado después de
  un arrastre) y `.zoom-area` en `columnas.css` (`overflow: hidden`, cursor
  de mano con zoom).
  - Demo en Chrome (`index.html#normal`), paso «Estribos»: 5 pasos de rueda
    llevaron a 212 %; arrastrar movió el dibujo sin elegir nada; un clic
    quieto eligió el tramo 4; 5 pellizcos simulados (rueda con Ctrl,
    deltaY −10) llevaron de 212 % a 349 %; «−» bajó a 325 % y a 300 %; la
    rueda hacia abajo volvió a 100 %. En «Traslapos», la rueda también
    acerca (182 %). Sin errores de consola.
  - `dotnet test CQEING-Addins.sln`: 0 fallas.
  - Luciano lo aprobó ("ya todo bien") y pidió commit y push (3-nov). No se
    desplegó en Revit 2027 antes del commit porque Revit seguía abierto.
  - [ ] Falta probar en Revit, con el mouse y con el touchpad (sobre todo
    poner grapas con zoom).
