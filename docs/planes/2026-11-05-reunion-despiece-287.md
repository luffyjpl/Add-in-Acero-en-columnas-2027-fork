# Plan: observaciones de la reunión del 05-11-2026 al despiece (#287)

## Contexto

- En la reunión diaria del 05-nov-2026 (Carlos, Santiago, Juan Felipe, Edgeryz, Luciano) se revisó el despiece de
  vigas y columnas. Las observaciones están anotadas, con cita y minuto, en
  `docs/planes/2026-11-05-observaciones-reunion-despiece-columnas.md` (en esta misma rama). Se agruparon en
  la issue padre #287 (sub-issue de la épica #217), con un sub-issue por punto: #272 a #286.
- **Fuera de alcance:**
  - #275, elegir qué niveles se muestran en las plantas (Luciano, 05-nov: "tengo que confirmar si sí se hará").
  - Lo de otros add-ins: cotas de planta, viguetas y zapatas.
- Entorno: Revit 2027 (.NET 10), add-in `addins/despiece`. Rama `despiece/reunion-287`, worktree
  `C:\Proyecto\addins-287`, creada desde `origin/main` en `0980379` (ya incluye #266, PR #269).

## Criterios de aceptación

Copiados de cada sub-issue, en forma corta.

- [ ] **#285** Con el modelo de Santiago, ni la planta del tablero ni las vistas muestran CAD ni marcas de sección.
- [ ] **#286** Con un modelo con muros y ventanas, la planta del tablero muestra solo vigas, viguetas, columnas, losa y
      zapatas.
- [ ] **#283** Las separaciones de estribos del despiece coinciden con las del acero en Revit, medidas en el modelo.
- [ ] **#284** Los largos de barra del despiece coinciden con el largo de la barra en Revit.
- [ ] **#279** Se sabe si los ganchos en direcciones distintas vienen del acero (add-in de columnas) o del dibujo, y
      salen en la misma dirección.
- [ ] **#272** Se elige la escala de la vista. Al cambiarla, el dibujo cambia y el texto impreso no. El control de
      letra sigue como opción. Probado poniendo la vista en una hoja (vigas y columnas).
- [ ] **#273** Al abrir el despiece, los textos y las cotas salen en negro; la opción de color sigue funcionando.
- [ ] **#274** Cambiar el color del texto se ve en el momento, sin esperar a que se vuelva a generar la vista.
- [ ] **#282** En la sección de la columna, primero (más cerca del dibujo) el recubrimiento `0.04 · 0.22 · 0.04` y
      después la cota total.
- [ ] **#280** La sección de la columna se ve como al comienzo de la columna, con la barra de esquina en su lugar.
- [ ] **#281** Las secciones de la columna no llevan etiqueta por barra: llevan una leyenda de colores.
- [ ] **#276** Todas las etiquetas de las barras sacadas quedan pegadas a su barra, a la misma distancia corta.
- [ ] **#277** Las barras sacadas quedan agrupadas: primero las de gancho a la derecha, un espacio chico, después las
      de gancho a la izquierda; los ganchos en espejo. El despiece queda más angosto que hoy.
- [ ] **#278** Toda barra con gancho se dibuja con el gancho visible.

## Hallazgos del código y de Revit real

*05-nov, solo leyendo el código (todavía nada probado en Revit):*

1. **Lo que ya se oculta (#285, #286).** `Revit/GeneradorPlanta.cs`, `Generar`: la copia de la planta del tablero
   quita la plantilla, oculta CAD, vínculos y nubes de puntos (`Importados.Ocultar`, de #263) y la categoría
   `OST_Sections`. **No oculta nada más**: muros, ventanas, marcas de alzado (`OST_Elev`), llamadas, etc. siguen
   viéndose. Las cuatro vistas del despiece (`GeneradorPrevia.Limpiar`, `GeneradorColumna.Limpiar`,
   `SeccionColumna.Limpiar`) ya ocultan todo elemento de modelo que no sea el propio, más secciones, niveles, ejes y
   alzados. O sea, **#286 es sobre todo la planta del tablero**.
   - Por qué sigue el CAD en el modelo de Santiago: no se sabe. Puede ser un CAD dentro de una familia (no es un
     `ImportInstance` suelto) o algo que no es un CAD. **Hace falta su modelo o una captura.**
2. **API para ocultar por grupos, verificada como pública** en
   `%USERPROFILE%\.nuget\packages\nice3point.revit.api.revitapi\2027.0.0\ref\net10.0-windows7.0\RevitAPI.xml`:
   `View.AreImportCategoriesHidden`, `View.ArePointCloudsHidden`, `View.AreAnnotationCategoriesHidden`,
   `View.AreModelCategoriesHidden`. Falta confirmar que se pueden asignar (no solo leer), por reflexión sobre la DLL.
3. **Tamaño de letra y escala (#272).** `TamanoLetra.cs` y `Revit/Letra.cs` (de #261): 2.0 a 5.0 mm, 3.5 por defecto.
   Las separaciones que dependen del texto se multiplican por `Letra.Factor` = letra / 2.5, porque se midieron con
   2.5 mm **a 1:15**. La escala es una constante: `Escala = 15` en `GeneradorColumna.cs` y en `SeccionColumna.cs`, y la
   de la viga en `GeneradorPrevia.cs` (asignada en la línea 1096, `vista.Scale = escala`). El texto de Revit ya mide lo
   mismo impreso a cualquier escala. Lo que falta: que la escala se pueda elegir y que las separaciones crezcan
   también con ella (factor × escala / 15).
4. **Textos en negro (#273, #274).** `web/pasos.js:311`: `negros: false` por defecto. Cambiarlo vuelve a pedir la
   vista previa a Revit (`elegirTextos`, línea 450). Los textos que se pueden mover ya los dibuja la página
   (línea 533, `fill=${negros ? '#000000' : color}`): para esos no haría falta Revit. Los textos que quedan en la
   imagen (las secciones, las cotas) sí la necesitan.
5. **Cotas de la sección (#282).** `SeccionColumna.cs`: la cota de Revit del ancho va en `CotaAncho` = 0.42 m y el
   texto del recubrimiento más afuera, en `TextoAncho` = 0.46 m (lo mismo en alto: `CotaAlto` 0.15 y `TextoAlto`
   0.30). Es al revés de lo pedido: hay que intercambiarlos.
6. **Dónde se corta la sección (#280).** `SeccionColumna.cs`: planta propia cortada cerca del **tope** de la pila,
   3 cm debajo de donde empiezan los ganchos de arriba (decisión 17 del plan
   `docs/planes/2026-10-31-despiece-columnas-226-228.md`). Ahí las barras ya están corridas por el traslapo.
7. **Etiquetas de las barras sacadas (#276).** `GeneradorColumna.Sacar`: la etiqueta va en
   `s.Izquierda + s.Ancho + AireNota`, o sea a la derecha de **toda la caja del detalle**, ganchos incluidos. Una barra
   con el gancho a la derecha deja su etiqueta lejos del tramo recto; una con el gancho a la izquierda, cerca. Sería la
   causa de que la distancia cambie (falta verlo en Revit).
8. **Orden de las barras sacadas (#277).** `GeneradorColumna.Lineas` las ordena por altura (`OrderBy(... Abajo)`) y
   `AcomodadorFilas.Acomodar` las reparte en carriles. Nada mira hacia dónde va el gancho.
9. **Ganchos que no se ven (#278).** El detalle es `RebarBendingDetail.Create(..., rotación 0)`: la barra vista de
   frente. Un gancho que apunta hacia quien mira se ve como un punto. Hoy se elige, entre las iguales, la que tenga los
   ganchos más de lado (hallazgos 25 y 32 del plan de columnas). Si ninguna los tiene de lado, sale sin gancho.

*05-nov, leyendo la API (`RevitAPI.xml` de 2027, sin probar todavía):*

10. **Opción nativa para #278.** `RebarBendingDetailType` (público) tiene `DisplayMode` (desde 2025: detalle realista o
    esquemático; `RebarBendingDetailType.CreateRealistic` / `CreateSchematic`) y `RepresentaionOf3DShapes` (desde 2024,
    enum `BendingDetailFor3DShapesOptions`: `PlanView` = "la barra como en una planta paralela a su plano", o
    `IsometricView`). Hay que probar en Revit si el esquemático, o `PlanView`, dibuja el gancho que apunta hacia quien
    mira. Ojo: en la vista previa la barra la dibuja la página con los puntos proyectados (`PiezaMovible.Puntos`), así
    que ahí también hay que girar la pata.

*05-nov, medido con el MCP de Revit en `Prueba 08072026.rvt` (columna circular de la traza, varillas #6):*

11. **#284: el despiece escribe a propósito el largo "por cotas", no el de Revit.** Barra 817570 (`CQ Recta 0-90`, #6,
    un gancho de 90° `CQ Gancho fin`): `Bar Length` = **3.50**, tramo recto `CQ A` = 3.21 y gancho 0.33, que suman
    **3.54**. `LectorDespiece.LargoBarra` convierte el largo de Revit con `TextosDespiece.LargoPorCotas` (cada gancho
    de 90° suma `2 (R + d/2) − R·π/2`, la misma regla que `GanchoPropio.DiferenciaCotas` de acero en vigas, commit
    `f1d8c2b`). O sea: no es un error de lectura, es una regla. El "3.05 donde Revit dice 3.00" de la reunión es esto.
    Se relaciona con #245 (que en columnas el recto + la pata cierren en el múltiplo). Hay que decidir cuál va.
12. ~~**#279: el acero está bien, es el orden del dibujo.**~~ (corregido por el hallazgo 14). Las barras de abajo de una misma cara abren el gancho hacia
    afuera, en espejo: 817570 (X 3.08–3.42 m, gancho hacia −X) y 817611 (X 3.59–3.93 m, gancho hacia +X), las dos en
    Y ≈ 7.93 m, una a cada lado del centro (X ≈ 3.50). En las barras sacadas se dibujan una al lado de la otra sin
    orden, y por eso se ve "una para adentro y una para afuera". Lo corrige #277 (agrupar por gancho). 817584 y 817597
    tienen el gancho hacia −Y, o sea hacia quien mira el alzado: eso es #278.
13. **#283: falta saber qué separación se vio mal.** La traza de la columna 781599 da zonas
    `2 #3 @0.14` seguidas de un `1 #3` suelto (en −0.23 y −0.05 m) y no aparece ningún 0.30. Hace falta la captura o
    la columna que mostró Luciano.

14. **#279 es del add-in de columnas: el gancho de abajo sale hacia afuera** (05-nov, medido con el MCP). En las seis
    varillas de la traza, la pieza de abajo dobla hacia afuera y la de arriba hacia adentro (cajas de Revit):
    817570 hacia −X y su pieza de arriba 817576 hacia +X; 817611 hacia +X y 817617 hacia −X; 817584 hacia −Y y 817590
    hacia +Y. Las dos piezas llevan el gancho en su extremo final (`Hook At End`); la de abajo está dibujada de arriba
    hacia abajo (empieza en el traslapo), la de arriba al revés. Causa en
    `addins/armado-columnas/Logica/BarraContinua.cs`, `Orientacion(bool deArribaAAbajo, ...)` (de `db1f929`, 21-oct):
    a una barra dibujada de arriba hacia abajo le **invierte** Left/Right, porque supone que Revit los mide según el
    sentido de la barra. El modelo muestra lo contrario: justo las piezas invertidas quedan hacia afuera. La regla de
    "hacia adentro" (`OrientacionesHaciaAdentro`) está bien; lo que sobra es la inversión.

15. **#279 probado en Revit: las dos piezas de cada varilla miran al mismo lado** (06-nov, Luciano regeneró la pila
    781599 / 782105 / 782736 con el código de esta rama; medido con el MCP, cajas de las 24 piezas 819470 a 819625, y
    la traza del add-in `%LOCALAPPDATA%\Temp\bb06a6d2-…\practicacq-longitudinal.log`). Esa columna está junto a una
    línea de propiedad: 5 de las 12 varillas tienen `LINDERO: gancho girado …` y en esas **las dos** piezas abren
    hacia afuera (p. ej. 819470 y 819476, en X = 6.912 m, las dos hacia −X). Las otras 7, **las dos** hacia adentro
    (p. ej. 819484 y 819490, en Y = −0.093 m, las dos hacia +Y; 819605 y 819611, en X = 6.906 m, las dos hacia +X).
    Antes (código viejo, columna 781574) la pieza de abajo iba al revés que la de arriba (hallazgo 14).

## Decisiones

1. **Sin #275 por ahora** (05-nov, Luciano): espera confirmación de que se hará.
2. **#273: negro por defecto, el color queda como opción** (05-nov, Luciano).
3. **#280: la sección es la del comienzo de la columna** (05-nov, Luciano: "que sea como si se mirara al comienzo de
   la columna, como se ve en el generador cuando se está configurando"). Se corta en la parte baja de la pila, donde
   las barras todavía no se corren, mirando hacia abajo como hoy. Se compara con la sección del tablero del add-in de
   columnas.
4. **#281: en las secciones se quitan las etiquetas por barra y va una leyenda de colores** (05-nov, Luciano). En la
   de traslapos ya hay leyenda A/B; en la de varillas, una por diámetro (color, `#6`, cantidad).
5. **#272: letra 2.5 mm y escala 1:50 por defecto, y se puede elegir cualquier escala** (05-nov, Luciano: "más escalas
   para elegir, todas las posibles"). Una lista con las escalas de Revit (1:1, 1:2, 1:5, 1:10, 1:20, 1:25, 1:50,
   1:100, 1:200, 1:500) más un campo para escribir otra. Se suma 1:15, la de hoy. El control de letra sigue, como
   opción. Las separaciones crecen con letra / 2.5 × escala / 15.
   - Ojo: a 1:50 una columna de 0.30 m mide 6 mm en el papel y el texto 2.5 mm. Las etiquetas van a ocupar mucho
     más, comparadas con el dibujo, que hoy a 1:15.
7. **#285/#286: la planta del tablero deja de ser una imagen de Revit y la dibuja la página** (05-nov, Luciano:
   "sigue igual que antes, muestra los CAD o a veces la vista con todos los números. Necesitamos que lo muestre como se
   muestra en el generador de vigas"). Hallazgo: el tablero de vigas (`acero-vigas`, `Revit/LectorModelo.cs` y
   `calculo/.../Tablero/ArmadorPlanta.cs`) también va por nivel, pero **no exporta ninguna vista**: lee ejes, vigas y
   columnas del modelo y la página los dibuja. Así nunca puede salir un CAD, una cota ni una marca, porque solo se
   dibuja lo que se lee. El despiece hace lo mismo: ejes, columnas y vigas/viguetas del nivel. Sin losas, como vigas.
8. **Los pasos 2 y 3 (medir en Revit) van después del 4** (05-nov, Claude): el MCP de Revit no conectaba
   (`ECONNREFUSED 127.0.0.1:5578`; Revit estaba cerrado). Se adelanta el paso 4, que no necesita Revit, y se mide
   apenas el MCP conecte.
9. **#272: qué crece con la escala** (05-nov, Claude). Todo se midió a 1:15 con letra de 2.5 mm.
   - **Con letra y escala** (`Letra.Factor` = letra / 2.5 × escala / 15): lo que ya crecía con la letra, más lo que
     reserva lugar para textos y estaba fijo: recortes y filas de etiquetas de la sección de la viga
     (`GeneradorPrevia`, `Seccion`) y de la columna (`SeccionColumna.Crear`, recorte y leyenda).
   - **Solo con la escala** (`Letra.Papel` = escala / 15), aire y huecos entre dibujos: `DesfaseTraslapo`,
     `AireNota`, `HuecoEstribos`, `ColaEje`, `CabezaEje` (viga); `InicioSacadas`, `HuecoEnCarril`, `HuecoCarriles`,
     `DesfaseTraslapo`, `AireNota`, `AireEstribos` (columna); `AireNota`, `HuecoEstribos` (sección de columna).
   - **No cambian** (son del modelo): holguras, tolerancias, asomo de columnas, `Lado`, profundidades y cortes.
   - La letra guardada en el tablero cambia de clave (`despiece.letra.2`), para que el 3.5 de antes no tape los 2.5.
10. **#282: solo en la sección de la columna** (05-nov, Claude): es lo que pidió Santiago. La sección de la viga
    tiene el mismo orden (`CotaAnchoSeccion` 0.42 antes de `TextoAnchoSeccion` 0.46); se pregunta a Luciano si se
    cambia también.
11. **Orden de los pasos 5 a 11** (05-nov, Claude): con Revit sin MCP, se hicieron primero los que se pueden
    programar sin medir (#274, #282, #281, #276, #277). #280 y #278 esperan a Revit: el corte de #280 hay que verlo y
    #278 tiene una opción nativa por probar (hallazgo 10).
12. **#284: se deja el largo por cotas, como hoy** (05-nov, Luciano, hallazgo 11). Igual que vigas. El número
    redondo lo resuelve #245 en el add-in de columnas: #284 se cierra como parte de #245. No se toca código.
13. **#283: era 0.13, no 0.30** (05-nov, Luciano: "creo que fue error, se refería a 13"). Es lo de #256 (estribos a
    0.13 donde van 0.14), que es del add-in de columnas: #283 se cierra como duplicada de #256. No se toca código.
14. ~~**#279: el acero está bien; lo resuelve #277**~~ (reemplazada por la 15).
15. **#279: los ganchos siempre hacia adentro; con línea de propiedad, los dos hacia afuera** (05-nov, Luciano:
    "tiene que mirar hacia dentro siempre, exceptuando cuando haya una línea de esas de limitación, pero ahí ambas
    irían para afuera"). Es la regla que ya tiene el add-in (`OrientacionesHaciaAdentro` + `LadoLejosDelLindero`, que
    gira las dos puntas juntas: `OrientacionAbajo` y `OrientacionArriba`). Se arregla en esta rama (Luciano: "en esta
    misma rama"): quitar la inversión de `Orientacion` (hallazgo 14), y probar en Revit regenerando la columna.
    Reemplaza la decisión 14.
16. **Se trae `main` con #245 y #256** (05-nov, Luciano: "ya está la corrección de columnas en main"). La rama no tenía
    commits: se adelantó a `origin/main` en `8e02055` (fast-forward, sin tocar los cambios sin commit; ningún archivo en
    común). Con eso quedan resueltas, del lado de columnas, #284 (decisión 12) y #283 (decisión 13). Se desplegaron
    `armado-columnas` y `despiece` desde `C:\Proyecto\addins-287`.
17. **Prueba del 06-nov (Luciano, con la pila 1-6 y las dos del medio, captura de la sección):** lo que queda bien:
    barras agrupadas por gancho y etiquetas pegadas a la barra. Lo que se cambia (decisiones 18 a 23).
18. **#277: el gancho dibujado lo decide el grupo de traslapo** (06-nov, Luciano, de Carlos: "si hay una azul mirando
    hacia la derecha, la otra va a salir mirando a la izquierda… muestras uno de tipo A mirando a la izquierda y el
    otro de tipo B mirando a la derecha"). Reemplaza el criterio de la decisión 11 / paso 10 (dirección real del
    gancho): las líneas del grupo **A** van juntas con el gancho a la **izquierda**, las del **B** juntas con el gancho
    a la **derecha**. De las barras iguales se dibuja la que tenga el gancho hacia ese lado; si ninguna lo tiene, se
    usa la dirección real y se anota en la traza. Sin grupo conocido, la dirección real, como ahora. Aclaración (06-nov,
    Luciano: "si ninguna mira a cierto lado, no se va a forzar que mire a ese lado, solo si hay alguna que mire a ese"):
    el orden de las líneas sale del gancho que de verdad se dibujó, no del grupo.
19. **Columnas iguales: también por el lado del gancho** (06-nov, Luciano: la 1-6, de esquina y con ganchos girados,
    salió igual que la 5-7 y la 5-8). La firma de `AgrupadorIguales` compara tipo, forma, cantidad, largo y altura de
    cada barra, pero no hacia dónde va el gancho. En columnas se suma a cada barra si su gancho va hacia adentro o
    hacia afuera de la sección (`BarraLeida.Gancho`).
20. **Escala propia para la sección** (06-nov, Luciano: "que ambas cosas manejen escalas diferentes… el alzado se ve
    bien pero la sección se ve horrible"). Dos selectores: "Escala alzado" y "Escala sección", cada uno recordado.
    Vale para vigas (vista larga / sección) y columnas (alzado / secciones).
21. **Medida del traslapo pegada a la barra** (06-nov: "algunas de traslapo aún salen lejos"). Se ubica a la izquierda
    del tramo recto más a la izquierda de las dos piezas, no del borde de la línea (que incluye ganchos).
22. **Leyenda de la sección: una debajo de otra, texto negro y un cuadrito de color** (06-nov). En las dos secciones
    (traslapos y varillas). El cuadrito es una nota `■` en el color; el texto, aparte, en negro.
23. **Estribos menores con la misma medida, una sola cuenta** (06-nov: salían `27 #3`, `14 #3` y `23 #3` todos
    `L= 0.44 G= 0.11`). Se agrupan por tipo, largo y gancho, no por la forma de Revit (eran formas distintas con la
    misma medida).
24. **Prueba del 06-nov, 00:43 (captura del alzado de la 781574 a 1:50):** la sección quedó bien. En el alzado, de
    izquierda a derecha: verde (gancho a la derecha), roja, roja (sin gancho de lado: miran hacia quien ve), azul
    (derecha), verde (izquierda), azul (izquierda). La traza: `ganchos por línea 0:Derecha, 1:Izquierda, 2:Ninguno,
    3:Ninguno, 4:Derecha, 5:Izquierda`. Causa: `GruposGancho.Separar` metía las "Ninguno" con las de la derecha y el
    acomodador ordena por altura. **Tres grupos, en este orden: derecha, sin gancho de lado, izquierda** (06-nov,
    Luciano: la azul tenía que ir junto a la verde).
25. **Más pegadas** (06-nov, Luciano: "falta que estén más pegadas las varillas"; es parte de #277). A 1:50 los huecos
    medidos a 1:15 crecen 3.3 veces. Nuevos valores (× `Letra.Papel`): `InicioSacadas` 0.25 → 0.10, `HuecoEnCarril`
    0.10 → 0.05, `HuecoCarriles` 0.12 → 0.04, `HuecoGrupos` 0.25 → 0.10, `DesfaseTraslapo` 0.08 → 0.05.
26. **Medida del traslapo todavía lejos** (06-nov, misma captura: `1.41` a ~20 px de su barra). `AnchoNotaGirada` =
    0.10 m × `Letra.Factor` (0.33 m a 1:50) es casi el doble del grueso real de la nota. Pasa a medirse con la letra y
    la escala: `Letra.Mm / 1000 × Escala × 1.3` (0.16 m con 2.5 mm a 1:50).
27. **Escalas por defecto: alzado 1:50, sección 1:20** (06-nov, Luciano). Para vigas y columnas (es un solo
    selector). La sección se recuerda con una clave nueva (`despiece.escala.seccion.2`) para que el 1:15 guardado en
    la prueba no tape el 1:20.
28. **La fila de controles pasa a dos líneas cuando no entra** (06-nov, Luciano: con las escalas, "Volver a la
    posición automática" casi no se veía, "literal solo una letra"). `web/pasos.js`, `selectorTextos`: `flex-wrap`,
    `white-space: nowrap` y el botón sin achicarse.
29. **Prueba del 06-nov (decisiones 24 a 28): todo bien** (Luciano: "ya todo bien"). Sigue #280 y #278.
30. **#280: cómo se corta la sección al comienzo de la columna** (06-nov, Claude, con la decisión 3). Reemplaza el corte
    de la decisión 17 del plan de columnas (cerca del tope, bajo los ganchos de arriba):
    - El elemento es el más bajo de la pila con la sección de la columna (la común, `ElementoLeido.AnchoMm × AltoMm`,
      al centímetro); no el dado, que es más ancho. Sin ninguno así, el más bajo.
    - El corte va 0.15 m sobre su base y nunca por encima de 3 cm bajo el primer traslapo (el comienzo de la segunda
      pieza de cualquier varilla): ahí las barras están en su lugar, sin correr.
    - El rango de vista baja solo hasta la base de ese elemento (+5 mm): no se ven los ganchos de la zapata ni el dado.
    - Las cotas y el recorte, con las caras de ese elemento. Los textos de la página dicen "sección" a secas.
31. **#278: primero se prueba el detalle esquemático nativo** (06-nov, Claude; regla del CLAUDE.md, hallazgo 10). La
    documentación dice que el esquemático se dibuja en una caja de alto y ancho fijos (`SchematicHeight` /
    `SchematicWidth`), o sea sin la altura real de la barra, que en la columna importa. Antes de descartarlo se mira en
    Revit: prueba **temporal**, solo en Debug, en `GeneradorColumna.Sacar`: a las barras sin gancho de lado se les pone
    un tipo `RebarBendingDetailType.CreateSchematic(doc)` y se anota en la traza (`PRUEBA #278`). Se quita después.
    Si no sirve, la otra vía: dibujar esa barra con líneas de detalle siguiendo sus curvas, con la pata girada al lado
    de su grupo (se decide con Luciano).
32. **#278: la barra con el gancho hacia quien mira se dibuja girada, con líneas de detalle** (06-nov, Luciano: "en
    vez de ponerlo esquemático, ¿no te las podrías inventar o tomarlas como si estuvieras viéndolas desde la otra
    perspectiva y generarlas así de lado?"). Reemplaza la prueba de la decisión 31, que se quitó sin probarse. El
    `RebarBendingDetail` nativo solo gira en el plano de la vista (`Create(..., rotación)` / `SetRotation`); no hay forma
    nativa de verla de lado. Cómo:
    - Si de las iguales ninguna tiene los ganchos de lado (`GanchosDeLado` < 0.5), se toman las curvas de la barra y se
      giran alrededor de la vertical de su tramo recto (`Transform.CreateRotationAtPoint`) hasta que la pata quede hacia
      el lado de su grupo (A izquierda, B derecha; sin grupo, derecha).
    - Se llevan al plano de la vista y se dibujan con `doc.Create.NewDetailCurve`, en el color del diámetro
      (`GeneradorPrevia.Pintar`, grosor 2 como el detalle). Todas las APIs son públicas (`RevitAPI.xml` 2027).
    - El resto (etiqueta, medida del gancho, traslapo, arrastre, orden por grupo) usa esas curvas giradas en lugar de las
      de la barra; moverlas es `ElementTransformUtils.MoveElements` en lugar de `RebarBendingDetail.SetPosition`.
33. **Prueba del 06-nov, 01:06 (columna 1-6):** las barras giradas del alzado se ven bien. Dos problemas:
    - Aviso de Revit "Can't keep elements joined" (2 errores) al terminar la vista previa: la traza muestra 9 s entre
      `columna: deshaciendo` y `trabajo: termina` (el aviso esperando el clic). Causa probable: las líneas de detalle de
      la barra girada, que Revit une por sus puntas, se movían después de crearlas (`MoveElements`). Arreglo: se borran
      y se vuelven a crear en su lugar, sin moverlas; y las vistas previas se deshacen con
      `RollBack(GetFailureHandlingOptions().SetClearAfterRollback(true))`, que según `RevitAPI.xml` descarta los
      errores sin mostrarlos (las vistas previas no dejan nada en el modelo).
    - La sección al comienzo muestra ganchos (Luciano: "justo no la quería con ganchos"). Corte a 0.15 m en 782105
      (base 0.00), bien. Causa probable: en una planta, lo que está debajo del corte se dibuja entero si toca el rango,
      y los ganchos de la zapata cuelgan de barras que cruzan el corte. Arriba no pasaba porque los ganchos quedaban
      sobre el corte. Arreglo: "Depth Clipping" (`BuiltInParameter.VIEW_BACK_CLIPPING`, público) en "recortar sin
      línea", para que corte en el fondo de la vista. El valor se anota en la traza para confirmarlo en Revit.
34. **Prueba del 06-nov, 01:16 (1-6 y 5-7/5-8):** sin avisos; las rojas giradas bien en el alzado; la sección ya sin
    ganchos (el recorte en profundidad funcionó). Quedan dos cosas:
    - **Sección: las esquinas siguen corridas.** No depende de la altura: en cada varilla que traslapa, una pieza está
      corrida hacia adentro (en unas la de abajo, en otras la de arriba), así que a cualquier altura salen algunas
      corridas. **Decisión (Luciano): dibujarlas en su lugar.** En la sección, cada barra cortada que no es la pieza
      en su lugar se oculta y se dibuja un círculo relleno (`FilledRegion`, público) del diámetro de la barra en la
      posición de la pieza que está en su lugar: la de su varilla más lejos del centro de la sección (la corrida va
      hacia adentro). Con su color (grupo o diámetro). Contra: es un dibujo y no la barra de Revit (Carlos, 21-oct,
      "nada esquemático"); Luciano lo eligió igual.
    - **Las barras giradas no salen en la vista creada.** Causa: las líneas de detalle las recorta el marco de la vista
      (el recorte), y las sacadas van fuera; el detalle nativo no se recorta porque es anotación. En el paso 2 se veían
      porque las dibuja la página. Luciano no quiere agrandar el marco ("pensar otra forma"). Se prueba otra vez lo
      nativo, que es anotación: un tipo de detalle con `RepresentaionOf3DShapes = PlanView` y uno esquemático con la
      caja del tamaño real (`SchematicHeight` = largo recto, `SchematicWidth` = largo de la pata). **Prueba temporal**
      solo en Debug: los dos al lado de cada barra girada, para verlos en el paso 2; se quita después.
35. **Prueba del 06-nov, 01:30:** (a) la esquina de arriba a la derecha seguía corrida: la traza dice
    `12 piezas corridas dibujadas en su lugar` en las dos secciones, o sea que la regla "la pieza más lejos del centro"
    falla cuando la pieza se corre a lo largo de la cara (no hacia el centro). El modelo no guarda cuál quedó en su
    lugar. **Nueva regla, la del add-in de columnas** (`SeccionPlanta.PosicionIntermedia`): las esquinas, la pieza más
    metida en su esquina; las de cada cara, la pieza más afuera en lo perpendicular y repartidas en partes iguales entre
    sus dos esquinas a lo largo. Nuevo `calculo/.../PosicionesIdeales.cs` (+ 2 pruebas). Solo en columnas rectangulares
    (con caras planas en X); en las demás no se dibuja nada. (b) La prueba de los detalles nativos falló: los dos salieron
    acostados (PlanView 0.278 × 6.722 m pero dibujado de lado con su etiqueta; esquemático 16 m de ancho). Se quitó.
36. ~~**Corrección: el marco de la vista no recorta las barras giradas**~~ (equivocada, ver la 38) (06-nov, captura de Luciano de la vista final
    de la 5-7/5-8, 01:34: las rojas giradas salen con sus ganchos de 0.28). La causa que se anotó en la decisión 34
    (líneas de detalle recortadas por el marco) estaba equivocada. En la captura de las 01:18 faltaban porque esa
    versión todavía movía las líneas después de crearlas (decisión 33). No hace falta agrandar el marco.
37. **Una barra girada no salía en la vista final** (06-nov, Luciano: "que salgan en generar vistas igual que como
    están en el alzado"; captura de las 01:34: el carril 2, `2 #5 L= 2.22/2.50` y `6.72/7.00`, con sus textos pero sin
    la barra; la del carril 5 sí, pero con el gancho hacia el lado del carril 2). La traza dice que las cuatro se
    dibujaron (3 líneas cada una). Causa probable: todas las giradas se creaban primero en el mismo punto y después se
    borraban y volvían a crear en su carril; encimadas, Revit las une o las mezcla. Arreglo: las líneas de una barra
    girada se crean una sola vez, ya en su carril (`Colocar`); antes, su caja sale de la geometría de las curvas, sin
    crear nada.
38. **Sí: el marco recorta las barras giradas** (06-nov, 01:41, imagen exportada de la vista final de la 1-6,
    `%TEMP%\…\CQEING\despiece\…\e17dc270….png`): las cuatro rojas se dibujaron (traza) pero no aparece ninguna,
    solo sus textos. La roja con gancho de la captura de las 01:34 era el detalle de la prueba temporal (decisión 34),
    no las líneas. O sea, la decisión 34 tenía razón y la 36 no. Lo único que el marco no recorta son las anotaciones
    (textos, cotas, el detalle nativo), y el detalle nativo no muestra el gancho de lado. Pendiente: decidir con Luciano.
    En la misma prueba, la sección: la esquina de arriba a la derecha sigue unos milímetros adentro, y la traza decía
    `13 piezas corridas` con 12 varillas: se contaban como cortadas las piezas de arriba, que empiezan por encima del
    corte. Ahora solo las que cruzan la altura del corte, y la traza anota cada pieza y el lugar calculado.
39. **Marco más ancho solo con barras giradas, y el arranque de las vigas dibujado** (06-nov, Luciano: las vigas
    "tienen que salir así sea un poquito. Si se puede hacer que se recorten un poquito pero aún así salga lo otro,
    bienvenido sea"). En `GeneradorColumna.Alzado`, después de aplicar lo movido y solo si hay barras giradas:
    - El recorte llega por la derecha hasta la última barra girada (más 0.10 m × `Letra.Papel`).
    - Las vigas que con eso se verían más allá del recorte de antes (`bordeDer`) se ocultan, y su arranque se dibuja
      con una región rellena gris (tipo propio `CQ Despiece viga`, `FilledRegion`, público) en lo que antes se veía: a
      cada lado de la columna, hasta `Lado` = 0.30 m. Las demás vigas siguen siendo las reales.
    - Sin barras giradas no cambia nada.
40. **Prueba del 06-nov, 01:49 (5-7/5-8, columna 781574):** las rojas ya salen en la vista final, pero sin los ganchos
    de abajo; y la esquina de arriba a la derecha sigue corrida. Medido en la traza:
    - Esquina: varilla 4, piezas en (3.570, 8.088) y (3.550, 8.088); la otra esquina de la derecha (varilla 2) está en
      x = 3.588 y la de arriba a la izquierda en y = 8.088. **Las dos piezas de esa esquina están corridas**, así que "la
      pieza más metida en la esquina" no alcanza. Además se confirma el reparto: las de la cara de arriba cortadas están
      en x = 3.470 y 3.530, justo en partes iguales entre 3.412 y 3.588. Arreglo en `PosicionesIdeales`: cada esquina
      toma, en cada eje, la coordenada más afuera de su lado entre las piezas de las dos esquinas de ese lado (las
      esquinas llevan la misma barra, la más gruesa, como en el add-in de columnas).
    - Ganchos de abajo: el recorte agrandado iba hasta abajo de la pila (el dado), pero las barras bajan a la zapata
      (−0.93 m) y sus ganchos quedaban afuera. Arreglo en `AbrirParaGiradas`: el recorte también llega arriba y abajo
      hasta las barras giradas (más el mismo margen).
6. **Un solo plan para las 14 sub-issues, un commit por issue** (05-nov, Claude), en el orden de abajo: primero lo que
   impide probar con modelos reales y las medidas que no coinciden con Revit; después la escala, porque cambia todos
   los espacios; al final el dibujo.

## Plan paso a paso

Antes de cada paso que toca Revit: `Get-Process -Name Revit -ErrorAction SilentlyContinue` (vacío) y desplegar.

1. **#285 y #286: la planta del tablero se dibuja en la página, como en vigas** (decisión 7). ~~Ocultar categorías en
   la copia de la planta de Revit~~ (reemplazado el 05-nov).
   1. `calculo/.../ArmadorPlanta.cs` (nuevo, sin Revit, con pruebas en `ArmadorPlantaTests.cs`): recibe los ejes
      (nombre y dos puntos) y los elementos del nivel (id, si es columna, puntos de su geometría en planta) en metros.
      Devuelve en píxeles (60 px por metro, Y hacia abajo, 2 m de margen): el ancho y alto del dibujo, los ejes
      recortados al dibujo con su nombre, y el contorno de cada elemento (envolvente convexa de sus puntos).
   2. `Revit/LectorPlanta.cs` (nuevo, reemplaza a `Revit/GeneradorPlanta.cs`): sin transacción ni imagen. Del nivel
      toma las vigas cuyo nivel de referencia es ese (el mismo criterio que `LectorDespiece.Plantas`) y las columnas
      que lo tocan (de su cota menos 5 cm a su cota más 5 cm, como `ArmadorPlanta` de vigas). Los puntos salen de la
      geometría de los sólidos, como `LectorModelo.Puntos` de vigas.
   3. `Interfaz/PuenteDespiece.cs`, `GenerarPlanta`: manda `{ tipo: 'planta', id, ancho, alto, ejes, elementos }`
      (cada elemento: `viga` = id, `columna`, `puntos`). Ya no hace falta la cola en Revit para exportar, pero se deja:
      leer la geometría también usa la API.
   4. `web/pasos.js`, `planta()`: en lugar de `<image>`, un SVG con los ejes (línea discontinua gris y el nombre en un
      círculo), las columnas (gris relleno) y las vigas (contorno). Los elementos del tipo elegido se pueden clickear y
      se pintan como hoy (azul elegido, verde hecho).
   5. Borrar `Revit/GeneradorPlanta.cs` y `ExportadorImagen.AnchoPlanta` si nadie más los usa.
   - Las cuatro vistas del despiece no cambian en este paso: ya ocultan todo lo que no es el elemento y su acero
     (hallazgo 1).
   - Cómo se prueba: pruebas sin Revit; en Revit, un modelo con un muro, una ventana, un CAD importado y uno vinculado,
     y una marca de alzado: la planta del tablero muestra solo ejes, columnas y vigas, en todos los niveles, y se puede
     elegir una viga, una vigueta y una columna como antes. Pedir a Santiago que lo pruebe con su modelo para cerrar
     #285.
2. **#283 y #284: medir contra Revit.** Con el MCP de Revit (`get_parameter_value_for_element_ids`,
   `get_boundingboxes_for_element_ids`), en la columna que mostró Luciano: separación real de cada zona de estribos y
   largo real (`BAR_LENGTH` / largo total) de cada barra, contra lo que escribe el despiece (`traza.log`,
   `%APPDATA%\CQEING\despiece\traza.log`).
   - Si el error es del despiece: corregir `ZonasEstribos` / `LectorDespiece.LargoBarra` con una prueba sin Revit
     que lo reproduzca.
   - Si es del acero (add-in de columnas): anotarlo en #256 / #245 y cerrar estas como duplicadas.
3. **#279: ganchos en direcciones distintas.** En la misma sesión, leer con el MCP la dirección de los ganchos de las
   primeras barras (curvas de la barra, `GetHookOrientation`). Decidir si es del acero o del dibujo y anotarlo aquí
   antes de corregir.
4. **#272 y #273: escala elegible, letra 2.5 y negro por defecto** (decisiones 2 y 5).
   1. `calculo/.../TamanoLetra.cs`: `PorDefecto = 2.5`. Nuevo `calculo/.../EscalaVista.cs`: escala por defecto 50,
      lista de escalas, límites para la que se escribe a mano, factor `escala / 15`; con pruebas.
   2. `Revit/Letra.cs`: `Factor` = letra / 2.5 × escala / 15; `Letra.Usar(mm, escala)`.
   3. `GeneradorPrevia.cs`, `GeneradorColumna.cs`, `SeccionColumna.cs`: la escala sale de `Letra`, no de la
      constante. Las separaciones que no dependen del texto (holguras de la geometría) no cambian. Las que son espacio
      de papel (`Lado`, `InicioSacadas`, `HuecoCarriles`…) se revisan una por una.
   4. `Interfaz/PuenteDespiece.cs`: leer `escala` de `previa` y `crearVistas`.
   5. `web/pasos.js`: el selector de escala junto a la letra, guardado como la letra; `negros: true` por defecto.
   - Cómo se prueba: pruebas sin Revit. En Revit, una viga y una columna a 1:15, 1:25 y 1:50, cada una puesta en una
     hoja: el texto impreso mide igual y nada se encima.
5. **#274: el color del texto cambia al instante.** `web/pasos.js`: al cambiar "Textos en negro", repintar en el
   momento los textos que dibuja la página. La imagen anterior queda a la vista mientras Revit genera la nueva; solo
   se pide la nueva si en pantalla hay una imagen con textos de Revit (las secciones, las cotas).
   - Cómo se prueba: cambiar el color en el paso 2 de una columna: el cambio se ve sin esperar.
6. **#282: cotas de la sección** — `SeccionColumna.cs`: intercambiar `CotaAncho` con `TextoAncho` y `CotaAlto` con
   `TextoAlto`, para que el recubrimiento quede más cerca de la columna y la total afuera.
7. **#280: sección al comienzo de la columna** (decisión 3) — `SeccionColumna.cs`: el corte va en la parte baja de la
   pila, encima de donde empieza el tramo recto de las barras que nacen abajo, y el rango de vista no baja hasta sus
   ganchos. Primero probar en Revit dónde queda el corte, con la traza, y compararlo con el tablero de columnas.
8. **#281: leyenda en lugar de etiquetas** (decisión 4) — `SeccionColumna.cs`: sin la fila de etiquetas `1 #4`; en
   la de varillas, leyenda por diámetro (cuadradito de color, `#6`, cantidad), como la de traslapos. Revisar después
   qué espacios de la sección sobran (`FilaArriba`, `FilaAbajo`, `PasoEtiquetas`).
9. **#276: etiqueta pegada a la barra** — `GeneradorColumna.Sacar`: medir la etiqueta desde el tramo recto
   (`GeneradorPrevia.Mayor`), no desde toda la caja del detalle. Confirmar primero en Revit, con la traza, que la
   causa es la del hallazgo 7.
10. **#277: agrupar por dirección del gancho** — `GeneradorColumna.Lineas` y `Sacar`: saber para cada línea hacia
    dónde mira su gancho en la vista; primero todas las de la derecha, un hueco, después las de la izquierda, con los
    ganchos en espejo. Con pruebas sin Revit para el orden (en `calculo`).
11. **#278: gancho siempre visible** — primero investigar (regla del CLAUDE.md: buscar lo nativo antes de hacer a
    mano). Mirar en la documentación si `RebarBendingDetail` permite girar la barra fuera del plano de la vista o
    mostrarla en su propio plano. Si no, ver con Luciano: dibujar esa barra con líneas de detalle siguiendo sus
    curvas, giradas para que el gancho quede a la derecha. Probar en Revit antes de elegir.
12. **Cierre** — build sin advertencias (2026 y 2027), `dotnet test CQEING-Addins.sln`, prueba completa en Revit,
    Estado de este plan, PR.

## Cómo probarlo

```
cd C:\Proyecto\addins-287
dotnet build addins/despiece/CQEING.Despiece.csproj -c Debug -p:RevitVersion=2027
dotnet test CQEING-Addins.sln
Get-Process -Name Revit -ErrorAction SilentlyContinue   # tiene que no devolver nada
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin despiece -Revit 2027
```

Traza del despiece: `%APPDATA%\CQEING\despiece\traza.log`.

## Estado

Cerrado el 06-nov-2026. Luciano probó todo en Revit 2027 con `Prueba 08072026.rvt` (columnas 1-6 y 5-7/5-8): "Perfecto,
todo bien". `dotnet test CQEING-Addins.sln` sin fallos (138 pruebas del despiece); `despiece` y `armado-columnas` en
Debug 2026 y 2027 sin advertencias.

**Commits** (la decisión 6 decía uno por issue; como varias issues tocan los mismos archivos y se ajustaron juntas en
las pruebas, quedaron agrupados): el arreglo de columnas (#279) aparte, todo el despiece en uno y el plan en otro.

| Issue | Estado | Dónde |
|---|---|---|
| #285, #286 | ✅ | Planta del tablero dibujada por la página (`ArmadorPlanta`, `LectorPlanta`, `pasos.js`), decisión 7. Falta que Santiago la pruebe con su modelo. |
| #272, #273 | ✅ | Escala alzado 1:50 y sección 1:20, letra 2.5 mm, negro por defecto (decisiones 5, 9, 20, 27). |
| #274 | ✅ | El color cambia al instante; la vista anterior queda mientras llega la nueva. |
| #276 | ✅ | Etiqueta pegada al tramo recto. |
| #277 | ✅ | Tres grupos (derecha, sin gancho de lado, izquierda), lado según el grupo de traslapo si hay alguna así; más pegadas (decisiones 18, 24, 25). |
| #278 | ✅ | Barras con el gancho hacia quien mira dibujadas giradas con líneas; con ellas, marco más ancho y arranque de vigas dibujado (decisiones 32, 37 a 40). |
| #279 | ✅ | Add-in de columnas: sin la inversión de `BarraContinua.Orientacion` (hallazgos 14 y 15). |
| #280 | ✅ | Sección al comienzo de la columna, con recorte en profundidad y varillas en su lugar (decisiones 30, 33 a 35, 40). |
| #281 | ✅ | Leyenda con cuadrito de color, sin etiquetas por barra (decisiones 4 y 22). |
| #282 | ✅ | Recubrimiento antes que la cota total. |
| #283 | resuelta en columnas | Era 0.13 (decisión 13): lo arregló #256 (PR #293). |
| #284 | resuelta en columnas | Largo por cotas (decisión 12): lo arregló #245 (PR #293). |
| #275 | fuera de alcance | Espera confirmación (decisión 1). |

**Pendiente / conocido**

- La sección dibuja en su lugar las piezas corridas con círculos (`FilledRegion`): es un dibujo, no la barra de Revit
  (Luciano lo eligió; Carlos había pedido el 21-oct que no hubiera nada esquemático).
- Fuera del repo: en el conector de Revit para Claude se desactivó la búsqueda de actualizaciones (`CheckUrl` vacío en
  `%APPDATA%\Autodesk\Revit\Addins\2027\RevitClaudeConnector\plugin_settings.json`; respaldo `.respaldo-2026-11-05`),
  porque con ella el puente del MCP no arrancaba.
