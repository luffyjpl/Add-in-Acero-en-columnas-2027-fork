# Plan: contornos y vacíos por puntos, con el suelo, en «Crear viguetas» (#954)

## Contexto

- Issue [#954 «Arreglo addin de viguetas»](https://github.com/utilidadcqeing-prog/CQEING_BaseDeDatos/issues/954) (Juan,
  03-nov): «al generar no se está respetando las reglas que deben ser aprobadas en la revisión».
- Reunión del 04-nov (Carlos, Juan, Santiago, José, Edgeryz), min 02:53 a 36:21:
  - **Por puntos, no por paneles** (min 08:03, 11:45, 13:49): los puntos son las columnas, los extremos de vigas o
    viguetas y las intersecciones. El usuario los va marcando y, al cerrar el contorno, el add-in sabe la zona de
    viguetas. «Nos ahorramos todas las opciones de perímetro, un panel, columna entera y varios».
  - Ejemplo de Carlos: una zona con P1, P2, P3, P6 y P7 sin P4 ni P5. Con los modos de hoy no se puede.
  - **Puntos atípicos** (min 15:36 a 21:42): donde termina una vigueta de borde sobre el eje 3 no hay columna ni
    extremo. Para que el punto exista, **se modela un suelo** en Revit (Santiago, min 28:30: «Estructura → Suelo», desde
    los centros de columna y los extremos). La vista previa **muestra el suelo en un color muy claro** y sus vértices se
    pueden elegir (Juan, min 31:31: «que la preview muestre los suelos y reutilizar la selección de aristas, y permitir
    cerrar el cuadrado»).
  - **Contorno y vacío son cosas distintas** (min 21:42 a 24:48): primero el contorno; después, dentro, los puntos del
    vacío. Las viguetas de borde van **pegadas por la cara hacia adentro de la losa** (min 32:18 a 35:19). Juan (min
    35:50): «que genere toda la vigueta y dejar la opción de eliminar esas secciones»; Carlos: «traigámoslo de esa
    manera».
  - **Varias zonas sin perder lo anterior** (min 24:48 a 26:42): armar P1 y después P5, aunque no se toquen, y seguir
    trabajando sin perder lo de P1.
  - **Un botón para terminar** (min 21:42) y que se creen las viguetas con ese contorno.
- Pedido escrito del usuario (04-nov): selección de vértices independiente y precisa; contornos por perímetros e
  intersecciones; zonas atípicas; suelo visible en color claro; vacíos por puntos; vista previa con aristas y cierre;
  no perder selecciones; no romper lo que funciona.
- Rama `viguetas-puntos-954` desde `main` (`d25b54a`, ya con `crear-viguetas` 1.0.0). En la carpeta había trabajo sin
  commit de la decisión 31 del plan de #947 (vigueta en la punta de la viga corta y reparto parejo): se conserva.
- Fuera de alcance: dibujar el suelo desde el add-in; elegir a mano de qué lado va cada vigueta de borde (va hacia
  adentro de la losa; si hace falta otra cosa, se quita la pieza).

## Criterios de aceptación

- [ ] El suelo del piso se ve en la planta, en un color claro, debajo de todo.
- [x] En el modo «Puntos» se ven los puntos que se pueden elegir: columnas, extremos, cruces de ejes y vértices del
      suelo. Solo queda elegido el punto en el que se hace clic. (Probado en Revit sin suelo; los vértices del suelo,
      pendientes.)
- [x] Al cerrar los puntos (clic en el primero o «Cerrar contorno») se arma una zona (Z1, Z2…) con sus viguetas.
- [ ] Se pueden armar varias zonas, separadas o no, sin perder las anteriores; cada una se puede quitar.
- [ ] «＋ Vacío»: los puntos de un vacío dentro de una zona lo restan; las viguetas paran en él.
- [x] Los lados del contorno o del vacío que no tienen viga llevan una vigueta de borde con la cara al ras del lado,
      hacia adentro de la losa. Se puede quitar como cualquier pieza. (Probado en Revit en el contorno.)
- [ ] Lo de antes sigue funcionando: «Por vigas» (el perímetro de hoy) y «Paneles» (un panel, Ctrl + clic y el aviso de
      la columna), seguir las existentes, quitar por tramos, tope y vigueta en la punta de la viga corta.
- [x] `dotnet test CQEING-Addins.sln` sin fallos; compila para 2026 y 2027 sin advertencias.

## Hallazgos del código

- La selección vive en `web/viguetas.js` (`MODOS`, `elegir`, `enLinea`, el clic en `mouseup`) y los perímetros en
  `PisoViguetas.Perimetro`, que usa `DetectorPaneles` con los elementos elegidos: elige **elementos enteros**, no
  puntos, y cada perímetro nuevo **reemplaza** al anterior.
- Los paneles de «Un panel», «Columna entera» y «Varios» salen de `DetectorPaneles` con todas las vigas: por eso no se
  puede armar una zona que no sea unión de esos paneles (el ejemplo de Carlos).
- `LectorPiso` no lee suelos; los puntos que no son extremo ni cruce no existen para el add-in.
- `Panel` es un polígono sin huecos; `Geometria.Cortes` trabaja con un solo anillo.
- **04-nov, al escribir las pruebas del paso 2:** la primera versión de `PisoViguetas.Zonas` tomaba un lado como «sobre
  viga» solo si **un** elemento cubría más de la mitad. Un lado de 8,00 m sobre dos vigas de 4,00 m (una por vano)
  quedaba como borde libre y llevaba una vigueta de borde de más (prueba `UnLadoSobreVariasVigasSeguidasNoEsLibre`).
  Ahora se suma lo que cubren todos los elementos en la línea del lado (decisión 4).
- **04-nov, primera prueba del usuario en Revit (PROY_JULIO_BETO, N+3.70, sin suelo):**
  - Se pusieron viguetas de borde **encima** de una viga (lado izquierdo de Z1) y de la vigueta de borde del modelo
    (lado de arriba). Los puntos elegidos (extremos de otras vigas) no caen justo en el eje de ese elemento: quedan a
    más de 30 mm, o el lado sale apenas inclinado, y el lado se tomaba como libre. Arreglo en la decisión 4.
  - Al pulsar «Crear» se reiniciaban las viguetas eliminadas: el piso se vuelve a leer, la página vuelve a mandar las
    zonas (o el perímetro) y la respuesta llamaba a `elegir`, que borra las marcas. Decisión 8.
  - Los puntos se ven grandes, todos del mismo color y, sin acercar, se enciman (dos extremos a pocos cm). Decisión 9.
  - Con una zona que abarca dos paneles lado a lado, una vigueta cae **encima de la viga del medio** (paralela a las
    viguetas, dentro de la zona). El reparto solo miraba las vigas que cruzan las viguetas; las paralelas no contaban,
    y en el modo Paneles nunca pasaba porque esa viga era borde del panel. Decisión 10.

## Decisiones

1. **Modo «Puntos», el primero y el que viene marcado.** Los modos quedan en tres: **Puntos** (nuevo), **Por vigas** (el
   perímetro de hoy) y **Paneles** (un panel, con Ctrl + clic para sumar y el aviso de la columna, que reemplaza a
   «Columna entera» y «Varios» sin perder lo que hacían). Carlos pidió quitar los modos viejos, y el pedido escrito
   pide no romper lo que funciona: quedan, pero agrupados.
2. **Puntos que se pueden elegir** (`PisoViguetas.Puntos`): centro de cada columna, extremos del armazón, cruces de
   ejes del armazón y vértices de los suelos. A menos de 30 mm son el mismo punto. Las creadas por el add-in no dan
   puntos. Cada punto lleva su tipo para pintarlo de un color (decisión 9): `columna`, `cruce` (de dos vigas), `viga`
   (extremo de una viga), `vigueta` (extremo de una vigueta o cruce con una) y `suelo`.
3. **Suelos** (`LectorPiso`): los `Floor` del nivel, con su contorno del boceto (`Floor.SketchId` → `Sketch.Profile`),
   documentado y público. Se dibujan con un relleno muy claro.
4. **Zona por puntos** (`PisoViguetas.Zonas`): el contorno es el polígono de los puntos (por ejes). Cada lado:
   - si va sobre armazón, el libre llega a la **cara de adentro** de ese elemento y el apoyo, a su eje. Cuenta un
     elemento paralelo al lado (2° o menos) con sus dos puntas a no más de **medio ancho + 30 mm** de la línea del lado,
     o sea, si el lado pasa por el cuerpo del elemento aunque no por su eje (prueba en Revit del 04-nov: los puntos
     elegidos no siempre caen en el eje). Entre todos tienen que cubrir, **sumados**, más de la mitad del lado (en los
     modelos las vigas van una por vano, hallazgo del 04-nov). Si son varios, manda el de la cara más adentro;
   - si no, es un **borde libre**: lleva una vigueta de borde con la cara de afuera al ras del lado (su eje a medio
     ancho de vigueta hacia adentro) y el libre se corre un ancho de vigueta. Las viguetas del reparto que llegan a
     ese lado se crean en Revit **hasta el eje de la vigueta de borde**, no hasta el lado (decisión del usuario, 04-nov:
     así no se enciman). Para eso el `Panel` lleva sus anillos de apoyo (`Apoyos`): el contorno por los ejes de lo que
     sostiene cada lado (viga o vigueta de borde).
5. **Vacío por puntos:** un polígono dentro de una zona. Se resta de ella como un hueco (los cortes de las viguetas
   usan todos los anillos, regla par-impar). Sus lados siguen la misma regla: sobre armazón, medio ancho; libre, vigueta
   de borde hacia afuera del vacío, que es hacia adentro de la losa.
6. **Varias zonas a la vez:** la página guarda todas las zonas y las manda juntas cada vez (`zonas`); la propuesta y
   «Crear» trabajan con todas. Cada zona se puede quitar con su ×. Como el libre de los bordes libres depende del ancho
   de vigueta, las zonas se vuelven a armar al cambiarlo. Las zonas y los perímetros «Por vigas» usan los mismos ids
   (Z1, Z2…): armar unos borra los otros (04-nov, paso 6), para que un perímetro viejo no tape a una zona.
7. **Viguetas de borde en la propuesta** (`PropuestaViguetas.Bordes`): ids `Z1:B1`, `Z1:B2`…; se quitan con «Eliminar
   viguetas» como las demás y se crean junto con ellas.
8. **Las eliminadas no se reinician al crear** (pedido del usuario, 04-nov). Cuando vuelven las zonas o el perímetro, la
   página conserva las eliminadas de las zonas que siguen (las de una zona quitada se van) y los tramos del modelo
   marcados; la propuesta se pide de nuevo con ellas. Solo se reinician al elegir otra cosa (otro modo, otros paneles)
   o al cambiar separación o ancho, como antes.
9. **Puntos chicos y por color** (pedido del usuario, 04-nov): un radio fijo de pantalla (unos 3,5 px, no crece al
   alejar), un color por tipo con su leyenda en la barra lateral, y sin encimarse: si dos quedan a menos de unos
   9 px en pantalla se dibuja solo el de más prioridad (columna, cruce, viga, vigueta, suelo); al acercar aparecen los
   demás. Los puntos del trazo se dibujan siempre. (Se probó una variante con todos los puntos visibles y los del
   centro más grandes; el usuario pidió volver a esta, 04-nov.)
10. **Las vigas paralelas dentro de una zona parten el reparto** (pedido del usuario, 04-nov): una viga (no una vigueta
    del modelo) paralela a las viguetas, con su cuerpo dentro del ancho libre del panel y que corre a lo largo de él,
    divide ese ancho en tramos entre sus caras. Cada tramo se reparte por su cuenta (fórmula del Excel, con la vigueta
    en la punta de la viga corta si la hay), así ninguna vigueta queda encima de la viga. Las líneas alineadas o las
    que siguen viguetas existentes que caerían sobre una de esas vigas no se proponen. `PlanificadorViguetas.Planificar`
    recibe esas vigas en `divisiones` (`PisoViguetas.Vigas`: el armazón propio que no es vigueta).

## Plan paso a paso

1. `Geometria.CortesAnillos` + `Panel` con huecos y viguetas de borde — tests.
2. `PisoViguetas.Puntos` y `PisoViguetas.Zonas` — tests: cuadrado de columnas, lado sin viga con vigueta de borde,
   vacío que parte las viguetas, dos zonas separadas.
3. `PlanificadorViguetas`: cortes con huecos y viguetas de borde en la propuesta y en `Lineas` — tests.
4. `ProtocoloViguetas`: suelos y puntos en `piso`, mensaje `zonas`, bordes en `propuesta` — test.
5. `LectorPiso`: suelos del nivel. `PuenteViguetas`: mensaje `zonas`.
6. `viguetas.js` / `index.html`: modo Puntos, suelo claro, puntos, trazo, cerrar, vacío, lista de zonas.
7. Pruebas, compilar 2026 y 2027, desplegar y probar en Revit con un modelo con suelo (Santiago lo manda).

## Cómo probarlo

```
dotnet test CQEING-Addins.sln
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin crear-viguetas -Revit 2027
```

1. Abrir el modelo con suelo, CQEING → Viguetas → Crear viguetas, piso N+3.70.
2. Modo Puntos: el suelo se ve claro; marcar los puntos de P1, P2, P3, P6 y P7 y cerrar: sale una sola zona.
3. Armar otra zona separada (P5): la primera sigue.
4. ＋ Vacío: cuatro puntos dentro de la zona; las viguetas paran en él y aparecen sus viguetas de borde.
5. Crear; revisar en Revit; Ctrl+Z.

## Estado

- [x] Pasos 1 y 2 (y la parte de cálculo del 3) escritos el 04-nov: `Panel` con huecos y bordes,
      `Geometria.CortesAnillos`, `PisoViguetas.Puntos` y `PisoViguetas.Zonas`, viguetas de borde en la propuesta y en
      `Lineas`. El proyecto de cálculo compila sin advertencias.
- [x] Pruebas de los pasos 2 y 3 (04-nov), en `ViguetasTests.cs`: `LosPuntosSonColumnasCrucesExtremosYVerticesDelSuelo`,
      `UnaZonaSobreCuatroVigasEsComoUnPanel`, `UnLadoSinVigaLlevaUnaViguetaDeBordeHaciaAdentro`,
      `UnLadoSobreVariasVigasSeguidasNoEsLibre` (encontró el error del hallazgo del 04-nov, ya corregido),
      `UnVacioParteLasViguetasYLlevaViguetasDeBordeHaciaLaLosa`, `DosZonasSeparadasSeProponenJuntas` y
      `LasViguetasDeBordeSeCreanConLasDemasYSePuedenQuitar`. `dotnet test CQEING-Addins.sln`: 1490 pruebas, 0 fallos
      (38 de viguetas).
- [x] Decidido el 04-nov: la línea de Revit de una vigueta que llega a un lado libre termina en el eje de la vigueta de
      borde (decisión 4), con `Panel.Apoyos`; probado en `LasViguetasDeBordeSeCreanConLasDemasYSePuedenQuitar` y
      `UnVacioParteLasViguetasYLlevaViguetasDeBordeHaciaLaLosa`.
- [x] Paso 4 (04-nov), `ProtocoloViguetas`:
      - `piso` suma `suelos` (`{id, contornos: [[[x, y]…]…]}`) y `puntos` (`{xy, tipo}`, tipo `columna`, `cruce`,
        `extremo` o `suelo`).
      - Mensaje de la página `{"tipo":"zonas","ancho":100,"zonas":[{"id":"Z1","contorno":[[x,y]…],"vacios":[[[x,y]…]]}]}`
        → `ArmarZonas`; siempre trae todas las zonas (vacío las quita todas); los puntos rotos se saltan.
      - Respuesta `ProtocoloViguetas.Zonas`: `{"tipo":"zonas","paneles":[…]}`, como la de `perimetro` más `huecosEje`,
        `huecosLibre` y `bordes` (`{desde, hasta}`).
      - `propuesta` suma `bordes`: `{id, panel, desde, hasta, largo, borrada}`.
      - Prueba `ElProtocoloLlevaSuelosPuntosZonasYBordes`; `dotnet test CQEING-Addins.sln`: 1491 pruebas, 0 fallos.
- [x] Paso 5 (04-nov):
      - API verificada en la referencia oficial antes de usarla: `Floor.SketchId` («Returns id of the sketch», desde
        Revit 2022, sin nota de uso interno) y `Sketch.Profile` (`CurveArrArray`, sin nota de uso interno).
      - `LectorPiso.Suelos`: los `Floor` con `LevelId` del nivel; cada lazo del boceto se encadena curva a curva (se da
        vuelta la que viene al revés), los arcos van en tramos (`Tessellate`) y se quitan los vértices en línea. El
        contorno de afuera va primero (el de más área).
      - `PuenteViguetas`: el mensaje `zonas` se contesta enseguida con `ProtocoloViguetas.Zonas` (no usa la API).
        «Crear» y «Quitar» ya funcionan con zonas: sus ids (`Z1`…) se buscan con `PisoViguetas.PanelesDe`.
      - Al crear o quitar, el piso se vuelve a leer y las zonas se pierden en el add-in: **la página tiene que volver
        a mandar `zonas` al recibir `piso`**, como ya hace con el perímetro (paso 6).
      - Compila para 2026 y 2027 sin advertencias; `dotnet test CQEING-Addins.sln`: 1491 pruebas, 0 fallos. La lectura
        de suelos **no se probó en Revit todavía** (paso 7).
- [x] Paso 6 (04-nov), `web/viguetas.js`, `web/index.html` y `web/viguetas-demo.js`:
      - Modos: **Puntos** (el que viene marcado), **Por vigas** y **Paneles** (un panel; Ctrl + clic suma o quita; el
        aviso «¿toda la columna?» sigue). Se quitaron «Columna entera» y «Varios». La clave guardada pasó a
        `cqeing-viguetas-3`: de la anterior se toman separación, ancho, tipo, dirección y «seguir», no el modo.
      - Suelo en un relleno muy claro, **encima de los paneles** (son opacos: debajo no se veía) y debajo de vigas,
        columnas y viguetas; no recibe clics.
      - Puntos: un círculo por punto (`data-pt`); el clic suma solo ese punto al trazo. El trazo se ve con sus lados y,
        desde 3 puntos, el cierre punteado al primero (que se agranda en verde). Se cierra con clic en el primero o
        «Cerrar contorno»; «Quitar el último», «Cancelar» y Esc.
      - Zonas en la barra lateral (Z1, Z2…, con puntos, viguetas de borde y creadas), cada zona y cada vacío con su ×.
        «＋ Vacío»: el trazo siguiente va como vacío de la zona que contiene su centro; si no cae en ninguna, se avisa.
      - La página manda `zonas` al cerrar, al quitar, al cambiar el ancho de vigueta y al volver a recibir el piso
        (después de crear o quitar). Cambiar de modo empieza de nuevo.
      - Viguetas de borde dibujadas en la propuesta (más claras) y quitables con «Eliminar viguetas»; en el pie,
        «De borde × n».
      - La demo (fuera de Revit) suma un suelo que sale de las vigas arriba a la izquierda, sus puntos y una copia simple
        de las zonas.
      - Probado en el navegador con la demo (`python -m http.server 8765` en `web/`): zona de 6 puntos con 2 viguetas
        de borde, segunda zona separada, vacío fuera de zona (avisa) y dentro, quitar una de borde, quitar Z2 (Z1
        sigue), y los modos Paneles (Ctrl + clic, aviso de la fila) y Por vigas. Sin errores en la consola.
      - C#: armar zonas borra los perímetros y al revés (decisión 6); prueba en `DosZonasSeparadasSeProponenJuntas`.
        1491 pruebas, 0 fallos; compila para 2026 y 2027 sin advertencias.
- [ ] Paso 7: desplegar y probar en Revit con el modelo con suelo (Santiago lo manda).
      - [x] 04-nov: desplegado para Revit 2027 con `tools/desplegar-local.ps1 -Addin crear-viguetas -Revit 2027`
            (compila sin advertencias; Revit se cargará desde `addins/crear-viguetas/bin/Debug/2027`, con la página
            nueva en `web/`). Antes, `Get-Process` no mostró Revit abierto: solo `RevitAccelerator`, el proceso de
            fondo de Autodesk, que no carga add-ins.
      - [x] 04-nov: primera prueba del usuario en PROY_JULIO_BETO (sin suelo). Salieron tres arreglos (hallazgos del
            04-nov y decisiones 4, 8 y 9): viguetas de borde encima de vigas, eliminadas reiniciadas al crear y puntos
            grandes, de un solo color y encimados.
      - [x] 04-nov: arreglos hechos. `PisoViguetas.Debajo` (lado sobre el cuerpo del elemento, libre a su cara y apoyo a
            su eje), tipos de punto `columna`/`cruce`/`viga`/`vigueta`/`suelo`, `volvieron` en la página (conserva las
            eliminadas), puntos de 3,5 px por color con leyenda y sin encimarse. Pruebas nuevas
            `UnLadoQuePasaPorElCuerpoDeLaVigaSinSuEjeNoLlevaViguetaDeBorde` y `UnElementoQueNoVaPorElLadoNoLoSostiene`,
            y tipos en `LosPuntosSonColumnasCrucesExtremosYVerticesDelSuelo`; 1493 pruebas, 0 fallos; compila 2026 y
            2027. En el navegador (demo): dos eliminadas siguen después de «Crear»; dos puntas a 4 cm se ven como una
            sin acercar y como dos al acercar. Vuelto a desplegar para 2027 (Revit cerrado, comprobado).
      - [x] 04-nov: se probó una variante de los puntos (todos visibles, los del centro de 5 px y los demás de 2,5 px)
            y el usuario pidió volver a la anterior: quedó como dice la decisión 9.
      - [x] 04-nov: vigueta encima de la viga del medio en una zona de dos paneles (decisión 10). `Bandas` y
            `Repartir` por tramos en `PlanificadorViguetas`, `PisoViguetas.Vigas`, `PuenteViguetas` las pasa en
            `divisiones`. Prueba `UnaVigaParalelaDentroDeLaZonaParteElRepartoYNingunaViguetaQuedaEncima` (también
            comprueba que sin el arreglo sí caía una encima). 1494 pruebas, 0 fallos; compila 2026 y 2027; vuelto a
            desplegar para 2027 (Revit cerrado, comprobado).
      - [x] 04-nov: publicado `crear-viguetas-v1.1.0` (PR #257 unido, etiqueta sobre `2d3225a`; release en
            `utilidadcqeing-prog/CQEING-releases` con los ZIP de 2026 y 2027 y `manifest.json`).
      - [x] 04-nov: el usuario volvió a probar en Revit 2027 (PROY_JULIO_BETO, N+3.70) los cuatro arreglos: viguetas
            de borde sobre vigas, eliminadas al crear, puntos por color sin encimarse y viga del medio. Todo bien.
      - [ ] Probar la lectura de suelos con un modelo que tenga suelo (criterio 1 y vértices del suelo del 2).
      - [ ] Probar en Revit el vacío, varias zonas a la vez y que «Por vigas» y «Paneles» sigan igual (criterios 4, 5 y
            7): en el navegador y en las pruebas están bien, pero el usuario no los mencionó.
- [ ] Probado en Revit: los arreglos del 04-nov sí; falta el suelo.
