# Plan: ajustes al despiece pedidos tras ver las primeras vistas (#266)

## Contexto

- El equipo revisó las primeras vistas de despiece (vigas y columnas) y pidió cinco ajustes, cada uno en su
  issue. Se agruparon el 04-nov-2026 en la issue padre #266, que a su vez cuelga de la épica #217:
  - #263 No mostrar los CAD importados o vinculados (bug).
  - #261 Números de las etiquetas más grandes en vigas y columnas.
  - #260 Nombre de la viga en grande arriba, como en columnas.
  - #264 Cotas de abajo de la viga en una sola línea (primero maqueta y elección del equipo).
  - #262 Mostrar las columnas en los nudos de la viga (cambia una regla de #217: confirmar antes).
- Fuera de alcance: #225, #229 y #230 (siguen aparte en #217).
- Entorno: Revit 2027 (.NET 10), add-in `addins/despiece`. Rama `despiece/ajustes-266`, worktree
  `C:\Proyecto\addins-266`, creada desde `origin/main` en `50d0db3` (ya incluye el despiece de columnas, PR #265).

## Criterios de aceptación

**#263 CAD**
- [ ] Con un CAD importado o vinculado en la planta, el tablero muestra las vigas y columnas, no el CAD.
- [ ] Las vistas de despiece creadas (viga, sección de viga, columna, sección de columna) tampoco muestran el CAD.

**#261 Texto más grande**
- [ ] En la vista completa de una viga y de una columna, los números se leen sin hacer zoom.
- [ ] Nada se encima (etiquetas, cotas, ganchos, estribos).
- [ ] Sale así al crear la vista, sin pasos manuales.

**#260 Título de la viga**
- [ ] Al crear el despiece de una viga, arriba aparece su nombre en grande y legible.
- [ ] Mismo estilo que en columnas.

**#264 Cotas en una línea**
- [ ] Debajo de la viga queda una sola línea de cotas, ordenada y legible, con la forma elegida en la maqueta.
- [ ] Las medidas coinciden con Revit.

**#262 Columnas en los nudos**
- [ ] En la vista de una viga se ven las columnas reales en los nudos, sin su acero.
- [ ] No aparece nada más que la viga, su acero y esas columnas.

## Hallazgos del código y de Revit real

**#263 (04-nov, solo leyendo el código; todavía no se probó con un CAD en Revit)**
- `addins/despiece/Revit/GeneradorPlanta.cs`, `Generar`: duplica la planta, le quita la plantilla
  (`ViewTemplateId = InvalidElementId`) y muestra armazón y columnas. No oculta nada de CAD. Si la plantilla era la
  que escondía el CAD, en la copia vuelve a aparecer y tapa la imagen que se exporta.
- Las cuatro limpiezas de las vistas de despiece ocultan solo elementos con `Category.CategoryType == Model`:
  - `GeneradorPrevia.Limpiar` (viga longitudinal y sección de viga),
  - `Columnas/GeneradorColumna.Limpiar` (vista completa de columna),
  - `Columnas/SeccionColumna.Limpiar` (sección superior de columna).
  Un CAD (`ImportInstance`) tiene la categoría de su `.dwg`; no está claro que entre en ese filtro, y los vínculos de
  Revit (`RevitLinkInstance`) y nubes de puntos (`PointCloudInstance`) tampoco son seguros. Las cuatro vistas también
  quitan la plantilla, así que pasa lo mismo que en la planta.
- API a usar, toda pública y documentada: `FilteredElementCollector(doc, vista.Id).OfClass(typeof(...))`,
  `ImportInstance` (importados y vinculados, `IsLinked`), `RevitLinkInstance`, `PointCloudInstance`,
  `Element.CanBeHidden(View)`, `View.HideElements(ICollection<ElementId>)`.

## Decisiones

1. **Un solo plan para las cinco sub-issues, un paso por issue y en el orden de #266** (04-nov, Claude con Luciano):
   #263 es un error que impide probar con modelos de otros ingenieros; #261 va antes que #260 y #264 porque el tamaño
   del texto cambia los espacios; #264 y #262 esperan una decisión del equipo.
2. **#263: ocultar, además del CAD, los vínculos de Revit y las nubes de puntos** (04-nov, Claude): el despiece trabaja
   solo con elementos del modelo propio, así que nada de eso tiene que verse, y tapan la planta igual que un CAD.
   Se ocultan por elemento (`HideElements`), no por categoría, porque las categorías de los CAD dependen del nombre
   de cada archivo.

3. **#261: el tamaño de letra se elige en el tablero, con un número que se sube y se baja** (04-nov, Luciano): nada de
   opciones fijas. Un solo número para vigas y columnas, junto a "Textos en negro", con botones − / +.
   Detalles (Claude, 04-nov): pasos de 0.5 mm, entre 2.0 y 5.0 mm, 3.5 mm por defecto. El tablero recuerda el último
   (en el almacenamiento de la página). Cambiarlo vuelve a generar la vista previa en Revit, medio segundo después del
   último clic, para no encolar una generación por cada clic.
4. **#261: un estilo de texto por tamaño** (04-nov, Luciano): `CQ Despiece etiqueta 3.5 mm`, y lo mismo para los otros
   tipos que llevan texto (`CQ Despiece - metros 3.5 mm`, `... corta`, y los detalles de barra
   `CQ Despiece sin etiqueta 3.5 mm`, porque apuntan a un tipo de cota). Así crear vistas con otro tamaño no cambia las
   vistas ya creadas. Los tipos viejos, sin tamaño en el nombre, no se tocan.
5. **#261: las separaciones que dependen del texto crecen en proporción** (04-nov, Claude): se multiplican por
   `tamaño / 2.5`, porque se midieron con 2.5 mm. Las que no dependen del texto (holguras, desfase del traslapo,
   tolerancias, ejes) no cambian. El tamaño elegido se fija al empezar cada vista previa o creación (`Letra.Usar`) y las
   separaciones lo leen de ahí; Revit atiende una orden a la vez, así que no hay dos tamaños mezclados.

6. **#260: el nombre en grande va en el tablero, no en el dibujo de Revit** (05-nov, Luciano: "no quiero que salga en
   el dibujo sino en el addin como tal cuando se esté configurando"). Hallazgo: ni vigas ni columnas mostraban el nombre
   en grande, ni en Revit ni en el tablero; desde el paso 2 el encabezado solo decía "Revisa el despiece". Se hace en
   vigas y columnas (Luciano, 05-nov): desde el paso 2, el encabezado de la página es el nombre del grupo
   (`V Cim A-B-C - (0.50x0.90)`) y debajo, más chico, el paso y su ayuda. No se crea ningún tipo de texto en Revit.

7. **#262: la vista larga de la viga muestra las columnas de sus nudos** (05-nov, Luciano: "el mismo Juan fue el que me
   mandó a hacerlo"). Reemplaza la regla de #217 "Vista de viga sin columnas" (Juan, 30-oct). Se muestran las columnas
   reales, sin su acero, con el mismo filtro que ya usa `CotarColumnas` (caja de la viga con 5 cm de holgura). En una
   vigueta, lo mismo con las vigas en las que se apoya, porque `CotarColumnas` ya las trata como sus apoyos (decisión 3
   del plan de viguetas). El recorte no cambia (va justo al alto de la viga): de la columna se ve solo el nudo, y así no
   se encima con las barras sacadas, que empiezan justo arriba y abajo de la viga. La sección transversal no cambia.

8. **#264: opción A de la maqueta, con el largo total arriba de la viga** (05-nov, el equipo vía Luciano: "La A" y "que
   el de la longitud total salga arriba"; Luciano aclaró que es arriba de la viga, sobre las barras de arriba). Queda:
   - **Abajo, una sola línea**: la cadena de caras (columna, luz libre, columna…) con el texto arriba de la línea, y la
     cadena de ejes en la misma línea con el texto debajo (`DimensionSegment.TextPosition` / `Dimension.TextPosition`,
     públicas desde 2015 según `RevitAPI.xml`). Al final de la línea, debajo, una nota `Ejes 9.03` con el total entre
     ejes (Claude: así no se pierde ninguna medida, que lo pide #264).
   - **Arriba de todo**, entre las barras de arriba y las burbujas de los ejes: la cota del largo completo de la viga,
     a los mismos planos de las caras extremas, con el tipo de líneas de referencia cortas para que no crucen las barras.
   - Desaparecen la fila de total entre ejes y la del largo debajo.
   - Riesgo conocido: si en el paso 2 el usuario sube una barra de arriba por encima de la cota del largo, se enciman.

9. **#262 corregida: la columna asoma un poco y no tapa el acero** (05-nov, Luciano, viendo la vista previa de
   `V A - (0.30x0.35)` con letra 5.0 mm: "no tiene que tapar el acero, solo mostrar un poquito de la columna arriba y
   abajo, así como se hace en columnas"). Reemplaza la parte de la decisión 7 que dejaba el recorte justo al alto de la
   viga. Hallazgo: con el recorte justo, la columna se veía como un bloque gris en el nudo que tapaba las barras de la
   viga. Ahora:
   - El recorte sube y baja `Asomo` = 0.20 m sobre la viga cuando hay columnas (columnas usa 0.30 a los lados, `Lado`;
     aquí "un poquito"). Las etiquetas de estribos y las barras sacadas empiezan después del asomo.
   - Las columnas se dibujan solo con su contorno, en medio tono, sin relleno y con la superficie 100 % transparente
     (`SetHalftone`, `Set...PatternVisible(false)`, `SetSurfaceTransparency(100)`), para que el acero de la viga se vea.
10. **#264: los números de ejes, más abajo** (05-nov, misma captura: "3.60" quedaba pegado sobre "4.00"). Medido en
    `%APPDATA%\CQEING\despiece\traza.log`: Revit pone el punto de arrastre del texto 0.002 m sobre la línea, en la base
    del texto. Reflejarlo dejaba la parte de arriba del número tocando la línea. Ahora la base baja 1.5 veces el alto de
    la letra más esa distancia: queda medio alto de letra de aire entre la línea y el número.

11. **#262: el contorno de las columnas en negro, no en medio tono** (05-nov, Luciano, viendo la vista previa: con
    medio tono casi no se distinguían). Sigue sin relleno y transparente.
    Hallazgo (05-nov, medido en el modelo con el MCP de Revit): `V A - (0.30x0.35)` está en el nivel 0.00 (vigas
    782227, 782229, 782230, Z de -0.35 a 0.00 m). Abajo la tocan pedestales de 0.40 (781597, 781585, 781573, 781265, Z de
    -0.65 a 0.00) y arriba columnas de 0.30 que nacen en su cara de arriba (782103, 782106, 782107, 782108, Z de 0.00 a
    3.50). En la vista previa solo se veía asomar lo de abajo. Diagnóstico temporal en el log (05-nov, 07:23): las 8
    entran en `Apoyos`, ninguna oculta (`IsHidden = false`), recorte de Z −0.555 a +0.205 m. O sea, las de arriba sí se
    dibujaban, pero en medio tono no se distinguían. Con el contorno en negro se ven las dos: la columna de 0.30 arriba y
    el pedestal de 0.40 abajo, y las barras atraviesan el nudo sin taparse
    (`docs/planes/imagenes/despiece-262-nudo-columnas.png`, recorte de la imagen exportada de la vista previa). El
    diagnóstico se quitó; queda solo la línea `longitudinal: N apoyos visibles: ...` en el log.

## Plan paso a paso

1. **#263 CAD** — archivos: nuevo `addins/despiece/Revit/Importados.cs` con
   `Importados.Ocultar(Document doc, View vista)`, que junta los `ImportInstance`, `RevitLinkInstance` y
   `PointCloudInstance` visibles en la vista y los oculta con `HideElements` (solo los que `CanBeHidden`). Se llama:
   - en `GeneradorPlanta.Generar`, después de quitar la plantilla de la copia;
   - al final de `GeneradorPrevia.Limpiar`, `GeneradorColumna.Limpiar` y `SeccionColumna.Limpiar`.
   Cómo se prueba: build sin advertencias, `dotnet test CQEING-Addins.sln`, y en Revit 2027 (ver "Cómo probarlo").
2. **#261 Texto más grande, elegido en el tablero** (decisiones 3 a 5):
   1. `calculo/CQEING.Despiece.Calculo/TamanoLetra.cs`: límites, redondeo a 0.5 mm, factor y nombre del tipo, con
      pruebas en `TamanoLetraTests.cs` (sin Revit).
   2. `Revit/Letra.cs`: el tamaño en uso (`Letra.Usar(mm)`, `Letra.Factor`, `Letra.Pies`, `Letra.Nombre(tipo)`).
   3. `GeneradorPrevia.cs`, `Columnas/GeneradorColumna.cs`, `Columnas/SeccionColumna.cs`: las separaciones de texto
      pasan de `const` a propiedades `× Letra.Factor`. Escalan: viga `SeparacionViga`, `AltoTexto`, `EspacioCotas`,
      `MedioAnchoEtiqueta`, `HuecoFilas`, `HuecoEnFila`, `FilaEstribos`, `BajaNota`, `SubeNota`, `AnchoNotaGirada`,
      `CotaAnchoSeccion`, `TextoAnchoSeccion`, `CotaAltoSeccion`, `TextoAltoSeccion`, `CotaLuces`, `CotaTotal`,
      `CotaColumnas`, `CotaViga`; columna `AltoTexto`, `BajaNota`, `AnchoNotaGirada`, `FilaEstribos`, `CotaElementos`,
      `CotaTotal`; sección de columna `FilaArriba`, `FilaAbajo`, `PasoEtiquetas`, `CotaAncho`, `TextoAncho`, `CotaAlto`,
      `TextoAlto`, `Leyenda`, `EstriboAparte`, `FilaTexto`.
   4. `TiposPropios.Crear`: nombres con el tamaño y `TEXT_SIZE = Letra.Pies` en notas y cotas. `Capa`: `altoLetra` con
      `Letra.Pies`.
   5. `Interfaz/PuenteDespiece.cs`: leer `letra` de los mensajes `previa` y `crearVistas` y llamar `Letra.Usar`.
   6. `web/pasos.js`: control "Letra − 3.5 mm +", en el estado y en la clave de la previa; guardado en `localStorage`.
   Cómo se prueba: pruebas de `TamanoLetra`, build sin advertencias, y en Revit con 2.0, 3.5 y 5.0 mm en una viga y una
   columna: que nada se encime y que las vistas creadas antes no cambien.
3. **#260 Nombre en grande en el tablero** (decisión 6) — archivo: `web/pasos.js`, el bloque del encabezado (`titulo`
   / `ayuda`, en `render`). Desde el paso 2 y con un grupo elegido: el nombre a 24 px y en negrita; debajo, en gris,
   "Revisa el despiece · <ayuda>". En el paso 1 queda como está. Cómo se prueba: abrir el despiece, elegir una viga y una
   columna y pasar por todos los pasos: el nombre se ve arriba en grande y no tapa nada.
4. **#264 Cotas en una línea** (decisión 8) — archivo: `Revit/GeneradorPrevia.cs`.
   1. Constantes: `CotaLuces`, `CotaTotal`, `CotaColumnas` y `CotaViga` se reemplazan por `CotaLinea` (de lo más bajo
      a la línea), `BajoLinea` (lugar para los números de ejes) y `CotaArriba` (de lo más alto a la cota del largo),
      todas `× Letra.Factor`.
   2. `Cotar` (ejes): una sola cadena en `fondo - CotaLinea`, con el texto de cada tramo pasado debajo de la línea,
      y la nota `Ejes …` al final. Se registra en el log la posición del texto antes y después, para medirla en Revit.
   3. `CotarColumnas` (caras): la cadena en la misma línea y el largo completo en `techo + CotaArriba`, con el tipo
      `CotaCorta`. Los planos de referencia bajan solo hasta la línea.
   4. `AjustarEjes`: los ejes suben hasta encima de la cota del largo y bajan hasta debajo de los números de ejes.
   Cómo se prueba: build sin advertencias; en Revit, una viga de 3 ejes y una de 2: una sola línea abajo con caras
   arriba y ejes abajo, el largo arriba de todo, nada encimado; con letra 2.0 y 5.0 mm.
5. **#262 Columnas en los nudos** (decisión 7) — archivo: `Revit/GeneradorPrevia.cs`.
   1. Sacar de `CotarColumnas` la búsqueda de apoyos a un método `Apoyos(doc, viga, derecha, min, max)`, que usan
      `CotarColumnas` y `Longitudinal`.
   2. `Limpiar` recibe además los ids que se dejan visibles; `Longitudinal` le pasa los apoyos y la sección nada. El
      acero de la columna sigue oculto porque no está en la lista.
   3. Actualizar la regla en el cuerpo de #217.
   Cómo se prueba: build sin advertencias; en Revit, vista previa y vista creada de una viga de 3 ejes y de una vigueta:
   se ven las columnas (o vigas de apoyo) en los nudos, sin su acero, y nada más.

## Cómo probarlo

```
dotnet build addins/despiece/CQEING.Despiece.csproj -c Debug -p:RevitVersion=2027
dotnet test CQEING-Addins.sln
Get-Process -Name Revit -ErrorAction SilentlyContinue   # tiene que no devolver nada
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin despiece -Revit 2027
```

**#263**
1. En un modelo de prueba con vigas y columnas, en la planta de un nivel: Insertar → Importar CAD (cualquier DWG) y
   Insertar → Vincular CAD (otro DWG), los dos sobre la zona de las vigas.
2. Abrir el despiece: la planta del paso 1 tiene que mostrar las vigas y columnas sin el CAD.
3. Repetir con una plantilla de vista en la planta que esconda el CAD (la copia le quita la plantilla).
4. Crear el despiece de una viga y de una columna: ninguna de las vistas creadas muestra el CAD.

## Estado

- [x] Issue padre #266 creada y #260–#264 movidas debajo (04-nov).
- [x] Paso 1 (#263) implementado (04-nov): `Revit/Importados.cs` y sus cuatro llamadas. Build Debug 2026 y 2027
      sin advertencias; `dotnet test CQEING-Addins.sln` sin fallos.
- [ ] Paso 1 (#263) probado en Revit.
- [x] Paso 2 (#261) implementado (04-nov), sin commit todavía: `TamanoLetra.cs` + 9 pruebas, `Letra.cs`, separaciones
      con `Letra.Factor`, tipos con el tamaño en el nombre, `letra` en los mensajes y control − / + en `pasos.js`. Build
      Debug 2026 y 2027 sin advertencias; `dotnet test CQEING-Addins.sln` sin fallos. Desplegado en Revit 2027.
- [ ] Paso 2 (#261) probado en Revit: 2.0, 3.5 y 5.0 mm en una viga y una columna; nada encimado; las vistas viejas
      no cambian.
- [x] Paso 2 (#261) en commit `fdbfcec` (05-nov).
- [x] Paso 3 (#260) implementado (05-nov): encabezado con el nombre en `pasos.js`. Build 2027 sin advertencias.
- [ ] Paso 3 (#260) visto en Revit.
- [x] Paso 3 (#260) en commit `e15fbf9` (05-nov).
- [x] Paso 4 (#264): maqueta publicada (05-nov), https://claude.ai/artifact/JgQLJPRf6cfxEkbxz2CdXY, con la viga de
      ejemplo A-B-C (columnas 0.40, luces libres 5.60 y 2.63) y tres opciones: **A** caras arriba y ejes abajo en la
      misma línea; **B** cadena mixta, el eje parte la columna; **C** igual a la imagen del equipo y los totales en una
      nota. Falta que el equipo elija.
- [x] Paso 4 (#264) implementado (05-nov), sin commit: `CotaLinea`/`BajoLinea`/`CotaArriba`, `PasarTextoAbajo`, nota
      `Ejes …`, largo arriba. Build Debug 2026 y 2027 sin advertencias; `dotnet test CQEING-Addins.sln` sin fallos.
      Desplegado en Revit 2027.
- [x] 05-nov, vista previa de `V A - (0.30x0.35)` en Revit 2027 con letra 5.0 mm: la línea de abajo y el largo arriba
      salen; los números de ejes pegados a la línea y las columnas tapando el acero → decisiones 9 y 10, corregidas en
      el código (build 2026 sin advertencias; la de 2027 esperaba a que se cerrara Revit).
- [ ] Paso 4 (#264) probado en Revit. A medir: dónde queda el texto de ejes al pasarlo abajo (el log dice a qué
      distancia estaba sobre la línea) y si Revit le pone una línea guía al moverlo.
- [x] Paso 5 (#262) implementado (05-nov), sin commit: `Apoyos` y `Limpiar(..., tambien)` en `GeneradorPrevia.cs`. Build
      Debug 2026 y 2027 sin advertencias. Desplegado en Revit 2027.
- [ ] Paso 5 (#262): actualizar la regla en el cuerpo de #217 (pendiente del permiso de Luciano para editar la issue).
- [x] Paso 5 (#262) visto en Revit 2027 (05-nov) en la vista previa de `V A - (0.30x0.35)`: columna arriba, pedestal
      abajo, contorno negro, acero visible (decisiones 9 y 11).
- [ ] Paso 5 (#262): probar con una vigueta y en las vistas creadas.
