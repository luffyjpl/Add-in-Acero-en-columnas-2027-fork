# Plan: observaciones de Santiago al despiece del 09-11-2026 (#317: #313, #314, #315, #316)

## Contexto

- Santiago revisó el despiece y dejó 4 observaciones en su planilla (puntos 8 a 11). Issue padre #317 (parte de #217),
  con las sub-issues:
  - #313 Sección de viga: sin cantidades sobre las barras, con leyenda de colores (como columnas); recubrimiento acotado
    como cota y no como texto (también en columnas); despiece del estribo cerrado, con `Cantidad`, `L=` y `G=` en negro.
  - #314 Sección de viga: sale un cuadro negro que cruza la sección; falta decir dónde se cortó (`x= 1.50`).
  - #315 Vista larga de viga: la piel toda junta de un lado, con un texto `2x piel` y flechas.
  - #316 Vista larga de columna: sin los textos de los estribos menores.
- Fuera de alcance: el `Tipo I` que se ve sobre la sección en la referencia de Santiago (no se pidió, queda como
  pregunta); el despiece de viguetas.
- Entorno: Revit 2027, .NET 10. Rama `despiece/santiago-317`, worktree `C:\Proyecto\addins-317`, salida de
  `origin/main` en `472888a`.

## Criterios de aceptación

- [ ] #313 La sección de viga no tiene etiquetas con cantidad sobre las barras; tiene leyenda de color y diámetro como
      columnas (la piel se distingue en la leyenda).
- [ ] #313 Viga y columna: el recubrimiento va como cota de Revit en cadena (`0.04 | 0.27 | 0.04`), primero la cadena y
      más afuera la cota total, en horizontal y en vertical.
- [ ] #313 El estribo aparte sale cerrado (como dibuja Revit por defecto), con `Cantidad: 84`, `L= 1.24 m`, `G= 0.08`
      encima y `#3` debajo, todo en negro.
- [ ] #314 La sección no tiene el cuadro negro que la cruza y dice `x= <distancia>` encima.
- [ ] #315 La piel sale toda debajo de la viga, pegada a ella, con su etiqueta de siempre (`5 #5 L= …`) y además un
      texto `2x piel` a la izquierda con flechas a cada línea de piel.
- [ ] #316 La vista larga de columna no escribe a la izquierda los textos de los estribos menores; el dibujo no cambia.
- [ ] `dotnet test CQEING-Addins.sln` sin fallos y compilación sin advertencias.
- [ ] Probado en Revit 2027.

## Hallazgos del código y de Revit real

1. **Sección de viga**: `GeneradorPrevia.Seccion` (`addins/despiece/Revit/GeneradorPrevia.cs`, ~línea 820). Las
   etiquetas `7 #7` / `10 #5` con flechas salen de la función local `Etiquetar` (~883-909). La piel tiene sus etiquetas
   aparte a la izquierda.
2. **Leyenda de columna**: `SeccionColumna.Crear` (~133-155): un `■` pintado en el color del diámetro y el texto
   `N #k` en negro, una fila por diámetro, debajo de la columna.
3. **Recubrimiento hoy**: en las dos secciones va en **texto** (`GeneradorPrevia.Acotar` ~1108-1114 y
   `SeccionColumna.Acotar` ~405-409) porque una cota de Revit ignora la referencia del estribo (probado el 30-oct). Pero
   la sección de viga ya acota contra **planos de referencia en blanco** cuando las caras no sirven (`Planos`, ~1074,
   #300). Con planos en las caras exteriores del estribo se puede hacer la cadena como cota real. Hay que probarlo.
4. **Estribo "con el gancho para afuera"**: es el tipo de detalle `CQ Despiece sin etiqueta` con
   `SegmentsRepresentation = Unfold` (`TiposPropios.Crear`, ~1332), puesto el 31-oct. "Como estaba por defecto" es
   `Standard`. El mismo tipo (`tipos.Barra`) lo usan la sección de viga y la de columna. `EtiquetarGancho` (~992) pone
   los textos del gancho pensando en el desplegado.
5. **Texto del estribo en color**: `ResumirEstribos` (viga, ~967) y `SacarEstribo` (columna, ~461) pintan la nota con
   `Pintar(..., b, ...)`, que la deja en el color del diámetro (#3 = amarillo) salvo con "Textos en negro". Las medidas
   del detalle toman el color del detalle (`Pintar(vista, d.Id, estribo, 3)`). **A verificar en Revit**.
6. **Piel**: `Longitudinal` → `Lineas` (~370). Cada lado (arriba/abajo) acomoda primero su piel; el lado sale de
   `EstaArriba`, que para la piel (serie `P`) usa la altura de la barra, así que la piel de arriba sale arriba y la de
   abajo, abajo (decisión 1 del plan `2026-11-06-despiece-vigas-303.md`).
7. **Estribos de la vista larga de columna**: `GeneradorColumna.EtiquetarEstribos` (~721) escribe una etiqueta por
   zona y por forma (tipo + forma + largo), así que las grapas y cajas también salen (`2 #3 @0.04` repetido).
9. **Primera prueba (09-11, 09:21) fue en Revit 2026**, no en 2027: el despliegue solo se hizo para 2027, así que corrió la
   versión anterior (el registro no tiene la línea `columna: estribos principales`). Hay que desplegar también con
   `-Revit 2026`. Lo que se vio en esa prueba (columna con nudos, `traza.log` 09:21:15): a la izquierda salen zonas como
   `2 #3 @0.04` repetidas (grapas a 4 cm de su estribo, lo que arregla #316) y en cada nudo `4 #3 @0.10`, `3 #3 @0.05`,
   `1 #3` (separaciones irregulares en el nudo que `ZonasEstribos` parte en zonas chicas). Se revisa de nuevo con la
   versión nueva.
10. **Pedido nuevo de Luciano (09-11)**: en la vista larga de columna las barras sacadas tienen que ir más pegadas a la
   columna y entre ellas, con el texto legible. Las distancias están en `GeneradorColumna` (`Lado` = 0.30 m de viga
   visible a cada lado, `InicioSacadas`, `HuecoCarriles`, `HuecoGrupos`) y el ancho de cada carril incluye el gancho y la
   etiqueta girada (`Pieza`, ~línea 447). Pendiente de una captura con la versión nueva para medir cuánto achicar.
11. **Segunda prueba (09-11, 09:56, Revit 2026, `Col B-3`)**: los textos de estribos de la izquierda ya salen bien
   (`columna: estribos principales 1 de 3 formas`). Pero en la sección:
   - **Sale en celeste**: la columna estaba seleccionada en Revit y la imagen exportada lleva el resaltado de selección.
   - **Salen los ganchos y no sale el estribo aparte ni la cadena**: el corte cae en `-0,75` dentro del elemento
     `190961` (base `-0,90`, nivel CIMENTACION), que es el primero con la sección de la columna (`SeccionColumna.Comienzo`).
     Ese tramo va contra la zapata y no lleva estribos, así que `Estribo` no encuentra ninguno (no hay líneas de
     `cadena` ni de `estribos` en la traza). Y entre el corte y el fondo de la vista (`base + 0.005`) quedan los ganchos
     de anclaje de las longitudinales (las barras arrancan en `-0,83`), que se ven en proyección: las líneas en cruz.
8. **Cuadro negro**: no se sabe qué elemento es. La sección tiene 0.30 m de fondo y `Limpiar` deja visibles todas las
   vigas de la viga continua (`viga.Vigas`). **Se diagnostica en Revit** antes de tocar nada (paso 1).

## Decisiones

1. **La piel va toda abajo** (2026-11-09, Luciano): pegada a la viga, antes de los bastones de abajo. Reemplaza la
   decisión 1 del plan de #303 ("del lado que dice su altura").
2. **Texto de la piel** (2026-11-09, Luciano): cada línea de piel conserva su etiqueta como las demás barras (cantidad,
   diámetro y largo) y se suma un texto `2x piel` a la izquierda con una flecha a cada línea de piel.
3. **Estribos menores** (2026-11-09, Luciano): solo en la vista larga de **columnas**, y solo el **texto** de la
   izquierda; las barras se siguen dibujando. Un estribo es "menor" si en su tramo de altura hay otro más largo.
4. **Estribo cerrado en viga y columna** (2026-11-09, Claude): se cambia el tipo de detalle compartido a `Standard`,
   así que vale para las dos secciones. Sin desplegar, el largo del gancho va en el encabezado (`G=`) y se dejan de
   poner los textos sueltos de `EtiquetarGancho`.
5. ~~**Leyenda de la viga igual a la de columna**: `■ N #k`.~~ → **Leyenda de la viga con color y diámetro, sin
   cantidad** (2026-11-09, Claude): `■ #7`, porque Santiago pidió textual "quitar la cantidad de varillas y solo poner
   una leyenda que me indique el color de la varra y el tipo". Mismo armado que la de columna (cuadrito en color, texto
   en negro). La piel va en su propia fila (`■ #4 piel`). La leyenda de columna no se toca (sigue con cantidad). **A
   confirmar con Luciano.**
7. **El estribo aparte va en negro, líneas y textos** (2026-11-09, Claude): como la referencia. Las medidas del detalle
   son parte del detalle y toman su color, así que para tener los textos en negro el detalle entero va en negro.
9. **Sin selección al exportar** (2026-11-09, Claude): la vista previa limpia la selección de Revit antes de generar y la
   devuelve al terminar (`PuenteDespiece.VerPrevia`, con `Selection.GetElementIds/SetElementIds`, API pública).
10. **La sección de columna se corta en el primer elemento que tiene estribos** (2026-11-09, Claude): `Comienzo` prefiere
    el elemento más bajo con la sección de la columna **y** con estribos alojados en él; si ninguno los tiene, el de antes.
    Así sale el estribo (con su cadena de cotas y el estribo aparte) y el corte queda lejos de los ganchos de la zapata.
    Ajusta la decisión 30 del plan de #287 ("al comienzo de la columna").
11. **Barras sacadas de la columna más pegadas** (2026-11-09, Claude, a falta de captura sin mover): `InicioSacadas`
    0.10 → 0.03, `HuecoCarriles` 0.04 → 0.02 y `HuecoGrupos` 0.10 → 0.05 (todo × `Letra.Papel`). El ancho de cada carril
    (gancho + texto girado) no cambia, para que el texto siga legible. Se ajusta con la próxima captura de Luciano.
12. **Corte justo sobre el primer estribo** (2026-11-09, Claude): la decisión 10 no alcanzó. En `Col B-3` el elemento
    `190961` sí tiene estribos, pero baja hasta la cimentación (base `-0,90`) y sus estribos empiezan más arriba; el
    corte a 15 cm de la base seguía sin estribo (sin cadena ni estribo aparte) y con ganchos de anclaje a la vista
    (solo los de las barras que llegan a esa altura, por eso "solo salen algunos"). Ahora el corte va por lo menos 3 cm
    sobre el primer estribo del elemento y el fondo de la vista 2 cm debajo de él (`SobreElEstribo`, `BajoElEstribo`).
13. **Barras sacadas desde la cara de la columna** (2026-11-09, Luciano con capturas "se quiere" / "está saliendo",
    misma escala, ~55 px/m): de la cara a la primera barra se quiere ~0.24 m y salía ~0.62 m; entre carriles se quiere
    32/53/41 px y salía 46/67/38 px. Antes la primera barra arrancaba después de los 0.30 m de viga visible (`Lado`) más
    `InicioSacadas`. Ahora: `InicioSacadas` = 0.02 × Papel **desde la cara de la columna** (las barras quedan sobre el
    arranque de las vigas de la derecha, como en la captura de Luciano), `HuecoCarriles` = 0 y `HuecoGrupos` =
    0.02 × Papel. Reemplaza la decisión 11. Si entre carriles sigue lejos, lo que queda es el lugar reservado a la
    izquierda de cada carril para la medida del traslapo (`Izquierdo`, ~0.20 m a 1:50).
14. **Estribo aparte en el color de su diámetro, solo los textos en negro** (2026-11-09, Luciano, sección de `Col B-3`
    10:49). Reemplaza la decisión 7. Para que las medidas no salgan amarillas, el detalle va **sin sus medidas**
    (`SegmentLengthDimensionsEnabled = false` en el tipo `CQ Despiece sin etiqueta`) y se le ponen dos cotas propias en
    negro, como la referencia de Santiago: el **ancho** debajo y el **alto** a la izquierda, una vez cada una ("solo por
    largo y ancho, sin ponerlo todas las veces"), contra planos de referencia (`CadenaPorPlanos`). Una forma chata (grapa)
    lleva solo el ancho. Las medidas salen de los tramos rectos de la barra (sin ganchos) más su diámetro.
15. **Leyenda con los estribos** (2026-11-09, Luciano): la leyenda de la sección suma una fila por diámetro de estribos y
    menores (`■ #3` en amarillo) si ese diámetro no está ya en la leyenda; las filas de las longitudinales quedan como
    están (con cantidad en columna). En viga y columna, también en la de traslapos.
16. **Las grapas de traslapo se suman a las normales** (2026-11-09, Luciano): la grapa con el gancho más grande que va en
    los traslapos (`Cantidad: 142 L= 0.74 G= 0.13`, sin dibujo) no se escribe aparte: su cantidad se suma a la de la
    grapa normal del mismo diámetro y el mismo tramo recto (±3 cm) y con la misma cantidad de tramos. Se muestra la
    forma con más piezas. Vale en viga y columna (`GeneradorPrevia.FormasEstribos`).
17. **Cotas de la sección más juntas** (2026-11-09, Luciano): la cadena del recubrimiento pegada a la sección y la total
    justo debajo de ella, como el sheet de referencia (`0.04 0.27 0.04` y `0.35` debajo). Columna: cadena 0.08, total
    0.17 (antes 0.12 / 0.32 y 0.15 / 0.32). Viga: cadena 0.08, total 0.17 (antes 0.12 / 0.27). Todo × `Letra.Factor`.
18. **Encabezado del estribo más cerca** (2026-11-09, Luciano): sin las medidas del detalle la caja es la del estribo, y
    `AltoEncabezado` baja de 0.36 a 0.30 × Factor.
19. **En la sección, una barra se mueve a su lugar ideal solo si el corte cae en su traslapo** (2026-11-09, Luciano:
    "una de las grapas sale por fuera de la varilla"; aclaró que la buena es la grapa de más abajo, que en el modelo está
    bien puesta). En la traza (10:20) la pieza cortada de la varilla 3 (`445850`) estaba 5 cm de su "lugar" calculado
    (`(-1,050, -1,313) -> lugar (-1,050, -1,363)`): el despiece dibujaba el círculo en ese lugar y la grapa, enganchada a
    la barra real, quedaba por fuera. Como el corte va bajo el primer traslapo (decisión 30 de #287 y 12 de este plan),
    en el corte hay una sola pieza por varilla y es la real: `Corridas` solo mueve las piezas de una varilla que tiene
    dos piezas cortadas. Ajusta la decisión 34 del plan de #287.
20. **La sección de traslapos de la columna va sin estribos** (2026-11-09, Luciano): se ocultan estribos y menores en
    esa vista, sin estribo aparte ni fila `#3` en su leyenda. Las cotas del recubrimiento quedan.
21. **El cuadro negro de la viga (#314) era el corte en el extremo** (2026-11-09, traza 11:40): `PuntoDeCorte` elegía
    entre el 10 % y el 90 % de la primera viga el punto con menos barras, y salió `x= 0,075`, dentro de la columna: la
    viga se ve de canto (una raya negra gruesa) y las barras sueltas al lado. Ahora el corte va en una **luz libre**
    (fuera de las columnas y a más de 0.15 m de sus caras y de los extremos), con la menor cantidad de barras (lejos de
    los traslapos) y, entre esos, lo más cerca del centro de la primera luz. Se quita el diagnóstico del paso 1.
22. **Piel: una línea por cara** (2026-11-09, Luciano): "tienen que ser las 2 barras y las 2 flechas apuntando a cada
    una", cada una con su cuenta (`1 #4 L= …`). Si la piel son dos conjuntos (uno por cara), una línea por conjunto; si es
    uno solo con las dos caras, dos líneas con la mitad cada una. `2x piel` con una flecha a cada línea.
23. **Vista larga de la viga más compacta** (2026-11-09, Luciano: "barras más cerca de la viga, textos más cerca, los ejes
    también"): `SeparacionViga` 0.14 → 0.05, `AltoTexto` 0.11 → 0.08 (× Factor); `HuecoFilas` 0.15 → 0.08, `CotaLinea`
    0.35 → 0.20, `BajoLinea` 0.35 → 0.25, `CotaArriba` 0.35 → 0.20 (× Proporción × Factor); `CabezaEje` 0.8 → 0.4 y
    `ColaEje` 0.3 → 0.15 (× Proporción × Papel).
24. **`Ejes 10.25` arriba, junto al largo total** (2026-11-09, Luciano), al final de la cota de arriba.
25. **Sección de viga más compacta** (2026-11-09, Luciano): cadena 0.08 → 0.06, total 0.17 → 0.12, leyenda 0.45 → 0.30
    (× Factor).
26. **Columnas de la vista larga de la viga en color sólido** (2026-11-09, punto 12 de Santiago: "usar colores sólidos
    en esa parte, sin degradados ni texturas"): relleno gris liso (205, 205, 205) y contorno negro; las barras de la viga
    se siguen viendo porque se dibujan por encima (`SetUnobscuredInView`). Reemplaza "solo contorno" (decisión 9 del plan
    de #266). **A confirmar** que lo que Santiago señala sean las columnas.
27. **Flechas de la piel: una por fila y siguen a la piel** (2026-11-09, Luciano: "las flechas sería mejor que se muevan
    con las pieles"; en `V Piso 2-…` salían 6 flechas cruzando el dibujo). Una flecha por fila (a la pieza de la
    izquierda). Se ponen después de aplicar los movimientos (`AplicarMovimientos` ahora actualiza `Sacada.Final`). En el
    paso de arrastrar, el `2x piel` se oculta de la imagen y lo dibuja la página (`DespieceMovible.Guias` →
    `capa.guias` → `capaMovible` en `web/pasos.js`), con la punta de cada flecha corrida igual que su pieza.
28. **El corte de la viga por el concreto, no por la caja** (2026-11-09, traza 12:11): en `V C` (cimentación) seguía
    `x= 0,069` con el corte "a 0,89 del extremo": la caja de Revit de esa viga no coincide con su concreto, y las luces
    libres se calculaban desde la caja. Ahora `PuntoDeCorte` usa los extremos del concreto (`Extremo` en los dos
    sentidos); si los sólidos dan menos de la mitad del largo de la caja, usa la caja. La traza anota los dos.
29. ~~Decisión 19~~ → **Todas las barras de la sección de columna en el lugar de la configuración, y las grapas corridas
    con ellas** (2026-11-09, Luciano: "las de las esquinas no están en la esquina... me gustaría que apareciera igual que
    cuando lo configuro en el add-in de columnas"). La barra de la esquina está corrida en el modelo porque el add-in de
    columnas la aparta del gancho del estribo. `Corridas` vuelve a mover toda pieza cortada a su lugar ideal, y
    `GrapasCorridas` oculta cada grapa que abraza una barra corrida y la dibuja con líneas de detalle: cada punta corrida
    lo mismo que su barra y el medio repartido, para que siga recta.
30. **Vista larga de la viga como la captura de Luciano** (2026-11-09, "esta sería más o menos la distancia que se
    quiere"): `SeparacionViga` 0; `EspacioCotas` 0.15 → 0.06 × Factor (era lo que separaba las filas con traslapo); arriba
    las barras arrancan sobre el asomo de las columnas y sus ganchos pueden bajar al lado de los textos de los estribos,
    pero su línea va siempre sobre ellos (`lineaMinima` en `ColocarFilas`).
31. **Falla en la previa de la viga** (2026-11-09, 12:43, `V C`): `NullReferenceException` justo después de
    `2x piel` en el paso de arrastrar. La nota recién creada no tenía caja en la vista (faltaba `Regenerate`) y `Separar`
    la medía. Se regenera al crearla y, si aun así no tiene caja, el `2x piel` queda en la imagen. La traza ahora anota
    la pila de la excepción (`AccionesRevit`) para ubicar la próxima falla.
32. **Columna más compacta** (2026-11-09, Luciano: "la leyenda más cerca, la cota de la longitud total más cerca"):
    `Leyenda` 0.50 → 0.32 × Factor (sección); `CotaElementos` 0.45 → 0.42 y `CotaTotal` 0.80 → 0.52 × Factor (alzado).
33. **El corte solo donde la viga tiene concreto entero** (2026-11-09, traza 12:50, `V C`): concreto de -10,69 a -2,08
    y caja de -11,51 a -1,26; el corte cayó a 1,63 de la primera luz y la sección salió "mocha". Es la viga de
    cimentación: las zapatas (unidas a ella) le cortan tramos enteros; son los huecos entre los rectángulos gris oscuro de
    la vista larga. `PuntoDeCorte` ahora pasa una línea por el medio de la sección y se queda con los puntos donde esa
    línea atraviesa concreto (`Solid.IntersectWithCurve`, API pública), a más de 0.15 m de cada borde. La traza anota
    esos tramos (`sección: concreto al medio …`).
34. ~~Decisión 22~~ → **La piel son siempre dos líneas, cada una con la mitad del total** (2026-11-09, Luciano: "5 de
    este lado y 5 del otro: deberían aparecer 2 barras azules que digan 5 #4 L=…"). En la traza de las 12:50 `V C` salía
    con 5 líneas, porque el despiece dibujaba una por cada conjunto de Revit.
35. **El corte de la viga con un estribo a la vista** (2026-11-09, traza 13:10, `V C`): con la decisión 33 el corte cayó
    en `x= 3,07`, dentro del concreto (2,91-8,36), pero al empezar ese tramo: en los 0.30 m que ve la sección hacia atrás
    no había estribo, y salió sin la cadena del recubrimiento ni el estribo aparte. Ahora, entre los candidatos, se
    prefieren los que tienen un estribo entre 0.01 y 0.28 m detrás del corte, en el sentido en que mira la vista
    (`FondoSeccion`). En esa prueba la piel salió bien: `2x piel con 2 flechas (2 piezas)`.
36. **El corte donde la sección está completa** (2026-11-09, captura de Luciano de la prueba de las 13:10, `x= 3.07`):
    solo una franja angosta de la viga salía cortada (gris claro con borde negro) y el resto era la viga vista de fondo
    (gris oscuro): la zapata unida le come parte del ancho. La línea del centro de la decisión 33 no alcanza; ahora son
    cinco (el centro y a 70 % de la mitad del ancho y del alto hacia cada lado) y el corte va donde las cinco atraviesan
    concreto. Si ninguna medida sale, se usa lo que haya (`Interseccion`).
37. **La sección de la viga muestra solo lo cortado** (2026-11-09, prueba de las 13:28, `V C`, `x= 3.37`: la sección ya
    salía completa y con estribo, pero a la derecha quedaba una franja gris oscuro/negra). Era la viga vista detrás del
    corte, dentro de los 0.30 m de fondo. A la viga se le quitan en esa vista las líneas de proyección (en blanco) y la
    superficie (sin relleno, 100 % transparente); el corte queda gris con borde negro. Cierra #314.
38. **`x=` medido desde el comienzo del dibujo de la vista larga** (2026-11-09, Luciano: "¿cómo está hecho lo de x? no
    me gustaría que en otro modelo salga mal"). Antes se medía desde el primer concreto de la viga, que en la de
    cimentación no coincide con lo que se ve. Ahora es desde donde arranca la cota del largo total (lo primero entre la
    caja de la viga y sus columnas, `InicioDelDibujo`, la misma cuenta que `CotarColumnas`), de izquierda a derecha como
    se lee: se puede leer sobre esa cota. Nada depende del modelo: el punto de corte sale de reglas generales (luz
    libre, sección completa, estribo a la vista, sin traslapo, cerca del centro de la primera luz).
39. **Filas de barras de la viga apoyadas una en otra** (2026-11-09, captura de Luciano de `V Piso 2-…` con las barras
    movidas "más o menos como las puse"). Entre dos filas se sumaba lo que ocupa encima la de abajo (su texto, su
    traslapo) y lo que ocupa debajo la de arriba (sus ganchos). Ahora se toma el mayor de los dos más `HuecoFilas`: el
    texto va al medio de la barra y el gancho en la punta, como en la captura. Vale también entre grupos (piel, bastones,
    corridas), con una `Pila` por lado. Además `DesfaseTraslapo` 0.08 → 0.06 × Papel y `EspacioCotas` 0.06 → 0.02 ×
    Factor. Las cotas de arriba y abajo y los ejes se ponen desde la última fila, así que se acercan solas.
40. **La viga de la vista larga con su contorno completo** (2026-11-09, Luciano con `V C`: "me gustaría que el borde
    saliera como si estuviera completa la viga"). En la de cimentación las zapatas le cortan el concreto y la viga salía
    en pedazos gris oscuro (probablemente lo que Santiago llama "texturas" en su punto 12). La viga de Revit no se dibuja
    en esa vista (líneas en blanco, sin relleno) y se dibuja su contorno con líneas de detalle negras: arriba y abajo de
    cada luz entre columnas (de la caja de la viga) y los extremos que no caen en una columna (`ContornoCompleto`).
41. **Viga: etiquetas de estribos más juntas y medidas de traslapo junto a su barra** (2026-11-09, capturas de
    Luciano "así están" / "así podría ser" de `V Piso 2-…`). Las etiquetas de estribos que no caben lado a lado se
    apilaban a `FilaEstribos` (0.13 × Factor) y las barras de arriba tienen que ir sobre todas: ahora
    `FilaEtiquetasEstribos` = 0.06 × Factor, casi el alto de la letra. La medida de un traslapo (`1.50`) que caía sobre una
    etiqueta subía una fila entera ("salen súper lejos"); ahora se corre al costado de esa etiqueta, del lado del
    traslapo, a la altura de su barra.
42. **Viga todavía más compacta** (2026-11-09, capturas de Luciano "pueden quedar incluso más cerca, así" / "así salen
    ahora", misma escala, ~88 px/m). Entre filas la distancia ya coincidía. Lo que sobraba: el escalón del traslapo
    (`DesfaseTraslapo` 0.06 → 0.02 × Papel: se quería ~7 cm y salía ~20); arriba, la primera línea 0.05 × Factor más
    abajo, casi tocando las etiquetas de estribos; abajo, la etiqueta de la primera fila puede ir al lado del asomo de las
    columnas y solo la línea de la barra va debajo de él (`lineaMinima` hace de máximo hacia abajo).
43. **Piel en líneas por cara y etiquetas de estribos por su ancho** (2026-11-09, Luciano: "las de arriba perfectas, las
    de abajo siguen muy lejos"; "el número de los estribos está molestando arriba"). La piel con traslapos (pedazos de
    2.00, 6.00 y 4.50 por cara) salía en 4 filas, un pedazo por fila; ahora cada cara es una línea con sus traslapos,
    como las corridas (`AcomodarLinea`, `Sacada.Cara`), y el `2x piel` apunta una vez por cara. Arriba, la tercera fila de
    etiquetas de estribos salía porque se medían como una etiqueta de barra (`MedioAnchoEtiqueta`); ahora por su texto
    (`MedioAnchoTexto`: 0.55 del alto de la letra por carácter). Y la primera barra vuelve a ir sobre la última fila de
    esas etiquetas (+0.075 × Factor; con −0.05 la línea las cruzaba).
- **Vigas de piso "recortadas"** (2026-11-09, medido con el MCP de Revit, solo lectura): la viga 193085 (`viga 35x50 m`)
  va de 3,86 a 4,36 m y la losa 203035 (`losa de e =0.25m`) de 4,11 a 4,36 m, unida a ella y cortándola: la viga queda
  de 0,25. El add-in de acero de vigas armó para esos 0,25 (estribo de 0,27 × 0,17 por dentro). No es del despiece:
  Luciano se lo pasó a Juan (acero de vigas). La viga `V C` (192238, `viga 50x90 m`) la cortan las zapatas 183263,
  183248 y 187800 (de 1,59, 2,19 y 1,59 m, en toda su altura): por eso su concreto propio son dos tramos.
- **Pregunta de Luciano sin resolver**: en `V Piso 2-Piso 3-Piso 4-Piso 5 C - (0.35x0.50)` el estribo aparte mide 0.27 ×
  0.17 (L= 1.04) y la sección 0.35 × 0.25: el estribo es coherente con lo que hay en el modelo, pero la viga se llama de
  0.50 de alto. Revisar en el modelo si el estribo es el correcto (¿la losa le quita alto?).
8. **Orden de prueba** (2026-11-09, Claude): el diagnóstico del paso 1 se prueba en Revit junto con los pasos 2-7, en una
   sola pasada, para no desplegar dos veces. Con lo que diga el registro se hace el paso 8.
6. **`x=` desde el extremo izquierdo de la viga como se ve en la vista larga** (2026-11-09, Claude), medido sobre la
   caja de concreto de la viga continua, en metros: `x= 1.50`.

## Plan paso a paso

1. **Diagnóstico del cuadro negro (#314)** — `GeneradorPrevia.Seccion`: al final, escribir en el registro cada
   elemento visible en la vista (id, categoría, caja en la vista). Se despliega, se genera la previa de la viga de la
   captura y se lee el registro. Se anota el hallazgo aquí y se decide cómo quitarlo.
2. **Textos nuevos, con pruebas primero** — `calculo/CQEING.Despiece.Calculo/TextosDespiece.cs` y su prueba
   `TextosYCotasTests.cs`: `Corte(1.5)` → `x= 1.50`; `EncabezadoEstribo(84, 1.24, 0.08)` → `Cantidad: 84`, `L= 1.24 m`,
   `G= 0.08` en tres líneas. `dotnet test` en verde.
3. **Estribos principales, con pruebas primero (#316)** — clase nueva `calculo/.../EstribosPrincipales.cs` + prueba:
   de una lista de formas (clave, largo, z mínima, z máxima) devuelve las que no tienen otra más larga que se
   superponga en altura. Después `GeneradorColumna.EtiquetarEstribos` solo escribe las principales.
4. **Piel abajo y `2x piel` (#315)** — `GeneradorPrevia.Longitudinal`: la piel se saca de `deArriba` y va con
   `deAbajo`; después de acomodarla, una nota `2x piel` a la izquierda de la piel con una flecha a cada línea.
5. **Sección de viga: leyenda (#313)** — se quitan las etiquetas `Etiquetar`; leyenda como columnas debajo de la
   sección. Se ajusta el recorte.
6. **Cotas en cadena (#313)** — viga y columna: cadena cara | estribo | estribo | cara contra planos de referencia en
   blanco, más cerca; cota total más afuera. Si Revit no acepta la cadena, queda el texto de hoy (y se anota).
7. **Estribo cerrado y en negro (#313)** — `TiposPropios.Crear`: `Standard`. `ResumirEstribos` y `SacarEstribo`:
   encabezado arriba (paso 2), `#k` debajo, en negro siempre; sin `EtiquetarGancho`. Si las medidas del detalle salen
   en color, el detalle va en negro.
8. **`x=` y cuadro negro (#314)** — nota `x= …` sobre la sección; quitar el cuadro según el hallazgo del paso 1.
9. **Cierre** — `dotnet test CQEING-Addins.sln`, compilación sin advertencias, prueba en Revit, Estado.

## Cómo probarlo

```
dotnet build addins/despiece/CQEING.Despiece.csproj -c Debug -p:RevitVersion=2027
dotnet test addins/despiece/calculo/CQEING.Despiece.Calculo.Tests/CQEING.Despiece.Calculo.Tests.csproj
dotnet test CQEING-Addins.sln
Get-Process -Name Revit -ErrorAction SilentlyContinue   # tiene que no devolver nada
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin despiece -Revit 2027
```

En Revit 2027, con el modelo de la captura (`V C - (0.50x0.90)` y `Col C-2 - (0.50x0.50)`):

1. Previa de la viga: sección sin cuadro negro, con `x=`, leyenda, cotas en cadena, estribo cerrado con su encabezado
   en negro.
2. Vista larga de la viga con piel: toda la piel abajo, pegada a la viga, con `2x piel` y flechas.
3. Previa de la columna: cotas en cadena, estribo cerrado; en la vista larga, a la izquierda solo los estribos
   principales.
4. "Crear vistas" de las dos: las vistas del navegador iguales a la previa.

## Estado

- [x] Plan revisado con el usuario (preguntas del 09-11: piel abajo, `2x piel` + etiqueta, menores solo texto en columna).
- [x] Pasos 1 a 7 implementados (09-11). `dotnet test CQEING-Addins.sln` en verde (Despiece: 143), compilación sin
      advertencias. Desplegado en Revit 2027 desde `C:\Proyecto\addins-317`.
  - Textos nuevos: `TextosDespiece.Corte`, `TextosDespiece.EncabezadoEstribo` (y se borró `TextosDespiece.Estribo`).
  - #316: `EstribosPrincipales.Filtrar` + `GeneradorColumna.EtiquetarEstribos`.
  - #315: `EstaArriba` manda la serie `P` abajo; `GeneradorPrevia.DosVecesPiel`.
  - #313: leyenda en `GeneradorPrevia.Seccion`; `GeneradorPrevia.CadenaPorPlanos` (viga y columna);
    `GeneradorPrevia.ApilarEstribos` reemplaza a `ResumirEstribos`, `EtiquetarGancho` y el armado de `SacarEstribo`;
    `TiposPropios.Crear` pasa a `Standard`.
  - #314: `x=` con `GeneradorPrevia.Extremo`; registro de diagnóstico de lo que se ve en la sección (temporal).
- [x] 09-11, Revit 2026: textos de estribos de la columna bien (#316). Decisiones 9-11 implementadas después
      (selección, corte con estribos, barras más pegadas); compila en 2027, pendiente desplegar en 2026 (Revit abierto).
- [x] Probado en Revit 2026 por Luciano (09-11, varias pasadas hasta las 14:30, modelo "edificio tevening": `Col B-3`,
      `V C`, `V Piso 2-Piso 3-Piso 4-Piso 5 C`). Lo aprobó: "Perfecto".
- [x] Paso 8: el cuadro negro era el corte cayendo en la columna o en un tramo que la zapata le quita a la viga
      (decisiones 21, 28, 33, 35, 36, 37); el diagnóstico temporal se borró.
- [x] `dotnet test CQEING-Addins.sln` en verde y compilación sin advertencias (09-11).

### Criterios de aceptación

- ✅ #313 Sección de viga sin cantidades sobre las barras, con leyenda de color y diámetro (decisiones 5 y 15).
- ✅ #313 Viga y columna: recubrimiento como cota en cadena contra planos de referencia, la total más afuera (6, 17, 25).
- ✅ #313 Estribo aparte cerrado, en su color, con `Cantidad`, `L=`, `G=` encima, ancho y alto acotados y `#3` debajo,
  textos en negro (decisiones 4, 14, 16, 18).
- ✅ #314 Sin cuadro negro y con `x=` medido desde el comienzo del dibujo de la vista larga (21, 28, 33, 35-38).
- ✅ #315 Piel abajo, una línea por cara con sus traslapos y su cuenta, `2x piel` con una flecha por cara que sigue a la
  piel al arrastrarla (1, 2, 27, 34, 43).
- ✅ #316 La vista larga de columna no escribe los estribos menores; el dibujo no cambia (3).
- ✅ `dotnet test` en verde, sin advertencias; probado en Revit (2026).

### Además, pedido en las pruebas

- Columna: corte sobre el primer estribo, barras en el lugar de la configuración con las grapas corridas, sección de
  traslapos sin estribos, barras sacadas más cerca, leyenda y cotas más cerca (10-13, 19-20, 29, 32).
- Viga: vista larga compacta (23, 30, 39, 41-43), `Ejes` arriba (24), columnas en gris liso (26), contorno completo de la
  viga (40), selección de Revit que no se cuela en la imagen (9), falla del `2x piel` en la previa (31).

### Pendiente

- Vigas de piso recortadas por la losa: es del add-in de acero de vigas, lo tiene Juan.
- Medidas de traslapo en la piel: salen como en las demás barras; Luciano decide si se quitan.
