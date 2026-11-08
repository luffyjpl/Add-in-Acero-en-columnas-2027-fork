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
- [x] Se pueden armar varias zonas, separadas o no, sin perder las anteriores; cada una se puede quitar.
- [x] «＋ Vacío»: los puntos de un vacío dentro de una zona lo restan; las viguetas paran en él.
- [x] Los lados del contorno o del vacío que no tienen viga llevan una vigueta de borde con la cara al ras del lado,
      hacia adentro de la losa. Se puede quitar como cualquier pieza. (Probado en Revit en el contorno.)
- [x] Lo de antes sigue funcionando: «Por vigas» (el perímetro de hoy) y «Paneles» (un panel, Ctrl + clic y el aviso de
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
- **05-nov, reunión con Santiago y Juan** (transcripción `Downloads/Reunión Ing. Juan_Santiago.txt`, 11:35, 40 min) con
  la captura de Z1 en PROY_JULIO_BETO, N+3.70, ↕ Y: una zona sobre los paneles de la izquierda y la derecha, con un
  vacío en L arriba a la izquierda. Lo que se vio y por qué pasaba en el código:
  - «Lo está separando cada 33, habíamos establecido 80» (17:55), «crea cuatro barras donde debe crear máximo dos, no
    respeta la lógica matemática» (38:50); en la captura, eje a eje 0,23 y cotas 0,10 · 0,20 · 0,23. **Causa:** en una
    zona de varios paneles las vigas van una por vano y terminan en la viga del medio, que queda dentro de la zona.
    `PlanificadorViguetas.Adentro` solo miraba el contorno, así que cada una de esas puntas contaba como «viga que
    termina dentro del panel» (decisiones 30 y 31 de #947) y obligaba una vigueta con reparto parejo: se amontonaban.
    Decisiones 16 y 17.
  - «Esa y la de arriba es uno solo… voy a tener dos finales en esa viga» (31:41 a 33:21): una vigueta que cruza una
    viga salía en dos tipos. **Causa:** la misma punta falsa ponía un tope en la viga que cruza; y además los tipos se
    contaban por pieza (entre vigas), no por vigueta creada. Decisiones 16 y 19.
  - «Faltan viguetas abajo… los nervios de arriba no son los mismos de abajo… abajo tengo que separarlo a una distancia
    que pueda» (13:28 a 16:31, 27:59 a 29:51). **Causa:** la vigueta de borde del vacío (decisión 11) y las vigas
    paralelas (decisión 10) partían el reparto de **toda** la zona, también donde ya no están (abajo del vacío). Abajo
    no se repartía por su cuenta. Decisión 18.
  - «Si hay 90 se pone a 45… si es menor a 80 no se pone» (04:20 a 05:43). **Causa:** `RepartoViguetas.Calcular`
    pone siempre al menos una vigueta; la fórmula del Excel da 0 cuando el libre cabe sin ninguna. Decisión 15.
  - «No existen diagonales… la vigueta debe ir derechita» (23:25). **Causa:** un punto corrido unos centímetros deja un
    lado apenas inclinado y su vigueta de borde sale en diagonal. Decisión 20.
  - «Deberías cambiar… seguir las que ya están creadas, no; fórmula» (18:35): al reemplazar, las nuevas seguían las
    líneas de las que el add-in ya había creado en la misma zona (las que se van a reemplazar), y repetían su error.
    Decisión 21.
  - Reglas que Santiago repitió y que ya se cumplen: las viguetas van dentro de la losa, nunca sueltas, con dos apoyos
    (07:57, 11:48); el reparto va centrado, con los dos extremos iguales (20:09).
- **05-nov, prueba del usuario en Revit con las decisiones 15 a 21** (captura: Z1 sobre las dos columnas de paneles del
  N+3.70, ↕ Y, vacío 1 arriba a la izquierda y vacío 2, chico, en el panel de arriba a la derecha; 19 viguetas, eje a
  eje 0,33–0,80, cotas 0,58 · 0,30 · 0,37):
  - Las viguetas de borde verticales del vacío 2 bajaban por **toda** la zona, cruzando todas las vigas hasta el borde
    de abajo, y partían en franjas angostas el reparto de todos los paneles de abajo. Santiago (27:11): «los nervios de
    la parte de abajo no se deben mover». **Causa:** la decisión 11 alargaba esa vigueta hasta el perímetro de la zona.
    Decisión 22.
  - En una franja angosta salían dos viguetas a 0,30–0,33 y extremos distintos (0,60 y 0,68). **Causa:** el reparto
    parejo con la vigueta en la punta de una viga (decisión 31 de #947) aceptaba cualquier separación desde dos anchos;
    y si no había reparto parejo, sumaba la de la punta a la fórmula y descuadraba los extremos. Decisión 23.

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

11. **Las viguetas de borde de un vacío que van en el sentido de las viguetas llegan hasta el perímetro y parten el
    reparto** (Santiago, reunión del 05-nov). **Corregida por la decisión 22: llegan hasta la primera viga que las
    cruza, no hasta el perímetro.** Constructivamente se arma por líneas: esas viguetas son las que
    condicionan el resto. Cada vigueta de borde de un vacío paralela a la dirección elegida se alarga, por su misma
    línea, de un lado y del otro, hasta el apoyo del contorno de la zona (o hasta otro vacío). Así no queda una vigueta
    arriba del vacío y otra corrida abajo, «conectadas» sin estar en línea. Esa línea divide el ancho de la zona como
    una viga paralela (decisión 10): cada tramo, de esa vigueta al perímetro o a otra, se reparte por su cuenta con la
    fórmula del Excel (caben 1, 2, 3…). Si se elimina esa vigueta de borde, deja de partir el reparto. Las de los
    lados libres del contorno y las de un vacío que cruzan las viguetas no cambian. `Panel.BordesVacios` las separa
    de las del contorno (mismo orden de ids, `Z1:B…`) y el planificador alarga y usa como banda las paralelas.
12. **Lo eliminado no se ve al terminar** (Santiago, 05-nov). Al tocar «Listo», las viguetas eliminadas (propuestas y
    de borde) y los tramos del modelo que se quitan dejan de dibujarse: la planta queda como va a quedar. Vuelven a
    verse, punteadas, solo en «Eliminar viguetas», para poder restaurarlas. El pie sigue diciendo «Eliminadas × n».

13. **Un punto que cae dentro del cuerpo de una viga va sobre su eje** (usuario, captura del 05-nov). Al marcar un
    vértice del suelo que queda a unos centímetros del eje de la viga, los lados de la zona salían inclinados, cruzaban
    la viga y llevaban una vigueta de borde encima de ella. Ahora, al armar la zona, cada vértice del contorno o de un
    vacío que cae dentro del cuerpo de un elemento del armazón (a medio ancho + 30 mm de su eje, o menos) se corre a
    ese eje (al más cercano). Así el lado va sobre la viga y no lleva vigueta de borde. `PisoViguetas.AlEje`.
14. **El contorno se puede cerrar sin volver al primer punto** (usuario, 05-nov): además de clic en el primero y del
    botón «Cerrar contorno», se cierra con **Enter**, con **clic derecho** o con **doble clic** en el último punto.
    El aviso lo dice.
15. **La fórmula del Excel tal cual: si el libre cabe sin vigueta, no va ninguna** (Santiago, 05-nov). n =
    REDONDEAR.MENOS(libre / eje a eje), sin forzar un mínimo de 1; si el libre de extremo pasa del máximo (eje a eje −
    ancho) se suma una. Con 0,80 y 0,10: hasta 0,70 de libre, ninguna; 0,90, una al centro (0,40 a cada lado).
    `RepartoViguetas.Calcular` devuelve `Cantidad` 0 en ese caso (y `LibreExtremos` = el libre).
16. **Una viga «termina dentro del panel» solo si su punta queda suelta** (05-nov). La punta tiene que estar dentro de la
    zona (no en un vacío), a más de 5 cm de su contorno y de sus vacíos, y **no apoyada en otro elemento** del armazón
    (a medio ancho + 3 cm de su eje, o menos). Así las vigas de cada vano que llegan a la viga del medio no cuentan: no
    obligan viguetas (decisión 31 de #947) ni ponen topes (decisión 30). Sigue valiendo para la viga corta de verdad.
    `PlanificadorViguetas.Suelta`.
17. **El reparto parejo con la vigueta en la punta pone a lo sumo una vigueta más que la fórmula** (05-nov): si para
    caer en la punta hace falta más, se usa la fórmula. Antes probaba hasta seis más y podía dejar 0,23 entre ejes.
    El caso de Carlos (4 → 5 a 0,69) sigue igual.
18. **El reparto va por luces** (Santiago, 05-nov: «los nervios de arriba no son los mismos de abajo»). Cuando una zona
    tiene vacíos o vigas paralelas dentro, se parte en **regiones**: franjas en la dirección del reparto donde cada
    vigueta va entre los mismos apoyos (mismo tramo libre a lo largo). Cada región se reparte por su cuenta con la
    fórmula (o siguiendo las existentes que caen en ella), y sus viguetas solo van por ella. Así, abajo de un vacío que
    toma todo el ancho de la luz se reparte el ancho completo de abajo, aunque arriba la vigueta de borde del vacío lo
    parta. Cómo se arman: se corta la zona en franjas por cada vértice de su libre y de sus vacíos y por cada cara de
    una viga paralela o vigueta de borde de vacío; en cada franja, los tramos libres a lo largo (sin los vacíos y sin
    esas vigas donde están); las franjas vecinas con el mismo tramo se juntan. Una zona sin vacíos ni vigas paralelas
    sigue como antes (una sola región, alineada con las demás de su fila o columna). Ids `Z1:1`, `Z1:2`… por región y
    cota, sin `.1`/`.2`. La propuesta trae un reparto por región (`RepartoPanel.Region`) y la página dibuja las cotas
    de cada región en su lugar.
19. **Los tipos van por vigueta creada: mismo largo y mismos apoyos** (Santiago, 05-nov, 33:21 a 37:50). Las piezas
    que se crean juntas (una vigueta continua de eje a eje) llevan un solo tipo. Dos viguetas son del mismo tipo si
    empiezan y terminan en el mismo lugar a lo largo (al cm): mismo largo y mismas vigas de apoyo. Si se elimina una
    pieza, la vigueta queda partida y cada parte toma su tipo. El largo del tipo es el libre de punta a punta. Se
    numeran del más largo al más corto. Las de borde siguen aparte («De borde × n»).
20. **Sin diagonales** (Santiago, 05-nov): al armar una zona o un vacío, cada lado que va casi derecho (5° o menos) se
    endereza: los lados casi horizontales seguidos toman la misma y, los casi verticales la misma x (el promedio). Los
    lados con más inclinación se dejan como están. `PisoViguetas.Enderezar`, después de `AlEje`.
21. **Las creadas dentro de lo elegido no son guía** (Santiago, 05-nov, 18:35): «Crear» las va a reemplazar, así que
    seguirlas repetía el reparto viejo. Siguen siendo guía las viguetas del modelo y las creadas fuera de lo elegido
    (para alinear con lo de al lado, decisiones 16 y 28 de #947). `PisoViguetas.Guias(direccion, elegidos)`.
22. **La vigueta de borde de un vacío se alarga solo hasta las vigas que la cruzan** (prueba del 05-nov; Santiago:
    «los nervios de abajo no se deben mover»). Corrige la decisión 11: sigue por su línea, de los dos lados, hasta la
    primera viga que cruza esa línea (a su eje), o hasta el contorno o otro vacío si llega antes. Así un vacío solo
    cambia el reparto del panel donde está; los de arriba y abajo de esas vigas quedan como sin vacío.
    `PlanificadorViguetas.BordesDe` con las vigas.
23. **El reparto parejo con la vigueta en la punta no achica ni descuadra** (prueba del 05-nov; Santiago, 20:09:
    distancias iguales y centradas). Se acepta solo con separación de al menos la mitad del eje a eje (0,40 con 0,80) y
    a lo sumo una vigueta más que la fórmula (decisión 17). Si no lo hay, va la fórmula sola, pareja y centrada: ya no se
    suma aparte la vigueta de la punta. Sigue el caso de Carlos (5 a 0,69).
24. **Las viguetas que creó el add-in también dan puntos en sus puntas** (usuario, 05-nov: «punto en la vigueta del
    vértice»). Cambia la decisión 2, que las dejaba afuera: ahora sus dos puntas se pueden elegir (tipo `vigueta`, mismo
    color), por ejemplo para armar una zona o un vacío desde las esquinas de lo ya creado. Van después de los puntos del
    modelo: si caen a menos de 30 mm de uno de ellos, queda ese. No cuentan para los cruces ni para saber si un lado
    va sobre armazón (decisión 4).
25. **A través de una viga, las viguetas son las mismas: manda el panel que tiene algo particular** (usuario, 05-nov,
    dos capturas: «la T6 es la que pone el orden… debe llegar hasta arriba con el mismo tipo» y «la que manda es la de
    arriba, debería seguir hacia abajo… entender en qué momento se desliga una de la otra»). Con la decisión 18 cada
    panel entre vigas se repartía por su cuenta y las líneas de un lado y del otro de una viga quedaban casi en línea
    pero no (T8 arriba, T6 abajo). Regla:
    - Dos regiones que se tocan a través de una viga que cruza las viguetas (una de cada cara) van juntas. Entre ellas
      **manda** la que tiene algo particular: es más angosta que la otra (su ancho lo fija un vacío o una viga
      paralela), lleva la vigueta en la punta de una viga suelta (decisión 31) o sigue viguetas que ya están.
    - La otra **sigue**: usa las mismas líneas (así cada vigueta cruza la viga de punta a punta, un solo tipo), y
      también la de cada **vigueta de borde de un vacío** que llega a esa viga: sigue por la misma línea, del otro lado,
      como una sola vigueta en Revit (prueba del 05-nov: «hay una que no alinea»). Entre dos líneas muy separadas y
      entre la última y el borde, la fórmula centrada (no el ritmo hasta donde entre, que dejó una a 0,09 de la viga).
    - **Se desligan solo en un vacío**: lo de arriba y lo de abajo de un vacío no se tocan a través de una viga, así
      que cada lado se reparte por su cuenta (decisión 18, Santiago: «al crear un vacío los nervios de arriba y de abajo
      dejan de ser los mismos»). Si las dos regiones no tienen nada particular, cada una con la fórmula (salen iguales).
    - Corrige lo que decía la prueba de la decisión 22 de que «los paneles de abajo quedan como sin vacío»: si el
      panel de al lado, a través de la viga, tiene un vacío que fija sus líneas, el de abajo las sigue (como en la
      captura 1). Lo que no vuelve a pasar es que la vigueta de borde del vacío cruce las vigas.
    `PlanificadorViguetas.PorRegion` (vecinas, `Seguir`).
26. **Las viguetas nuevas siguen a las que ya están, y rellenan lo que falta** (usuario, 06-nov, capturas del N+8.41 P3
    de PROYECTO THEVENIN 2: el voladizo de abajo, creado después, no quedaba en línea con las creadas arriba). Dos
    causas: la opción «Dónde van las viguetas» se guardaba en el equipo y había quedado en «Fórmula del Excel» desde la
    reunión del 05-nov; y siguiendo, si las que ya están cubrían solo parte del ancho, el resto quedaba sin viguetas.
    Ahora:
    - Al abrir la ventana o cambiar de piso, la opción vuelve a **«Seguir las que ya están»**; ya no se recuerda de una
      vez a otra (se puede cambiar a «Fórmula del Excel» en el momento).
    - Siguiendo, se usan las líneas de las que ya están (del modelo, o creadas fuera de lo elegido: decisión 21) y, entre
      ellas y hasta los bordes, la fórmula centrada donde el libre pasa del máximo (como `Seguir`, decisión 25). Vale
      para paneles y para zonas.
27. **No hay lados en diagonal: la zona se cierra en cuadrado** (usuario, 06-nov: «que no deje hacer viguetas en
    triángulo»). En una losa de viguetas, cada vigueta va paralela a las demás y apoyada en sus dos puntas sobre vigas
    o viguetas de borde que la cruzan en escuadra; un paño en triángulo deja viguetas de largos distintos que terminan
    en un borde inclinado, que no es un apoyo de verdad. Al marcar una zona o un vacío, un punto que no queda en línea
    (en X o en Y, 5° de tolerancia, como la decisión 20) con el anterior **no se agrega**: el aviso pide marcar primero
    la esquina. Tampoco se cierra si el último y el primero no quedan en línea. No se completa la esquina solo: con dos
    puntos en diagonal hay dos esquinas posibles y el add-in no sabe cuál es la buena (el usuario lo aceptó así).
28. **En Paneles, la vigueta sigue por su línea hasta donde llegue** (revisión del 08-nov, fila 6: «por qué el voladizo
    superior solo crea una parte y no hasta abajo»; el usuario: que se haga lo que dijeron). Al elegir **un** panel en el
    modo Paneles (con varios elegidos no, porque ahí no está el botón «Solo…»), cada vigueta propuesta se prolonga, de los dos lados, por los paneles vecinos no elegidos de esa misma línea,
    a través de las vigas, panel por panel. Para en el primer panel donde ya hay una vigueta en esa línea (a 3 cm o menos:
    ya sigue) o donde quedaría a menos de medio eje a eje de otra que ya está. Las creadas que «Crear» va a reemplazar no
    cuentan. Con **«Solo P6»** del aviso se crea solo ese panel. Por vigas y Puntos no se prolongan: ahí el contorno lo
    marca el usuario. `PlanificadorViguetas.Prolongar`, `PisoViguetas.LineasExistentes`, mensaje `prolongar`.
29. **«＋ Vacío» también en Por vigas y Paneles** (revisión del 08-nov, fila 7: la vigueta atraviesa voladizos y huecos al
    elegir la columna entera, y había que eliminarlas a mano). El add-in no puede adivinar un hueco que el modelo no tiene
    dibujado: se marca con puntos, igual que en el modo Puntos (sin diagonales, decisión 27). Cada vacío se aplica al
    panel o perímetro elegido que contiene su centro, con las mismas reglas de las decisiones 5, 11, 18, 22 y 25. La
    página los manda en cada pedido (`vacios`) y C# los aplica con `PisoViguetas.ConVacios`. Se quitan con su ×; al
    cambiar de modo o de piso se borran.

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
      - [x] 04-nov: el usuario confirmó que también probó en Revit el vacío, varias zonas a la vez y que «Por vigas» y
            «Paneles» siguen igual (criterios 4, 5 y 7).
- [ ] Probado en Revit: todo menos el suelo (criterio 1 y los vértices del suelo del 2).
- [x] 05-nov: arreglos de la reunión (Santiago, video del piso 2): decisiones 11 (vigueta de borde del vacío hasta el
      perímetro, partiendo el reparto) y 12 (lo eliminado no se ve después de «Listo»). Rama
      `viguetas-arreglos-954-05nov` desde `main` (`3671e88`).
      - [x] Decisión 11: `Panel.BordesVacios` y `BordesDelContorno`; `PisoViguetas.Zona` pone ahí las de los vacíos;
            `PlanificadorViguetas.BordesDe` alarga las paralelas a la dirección hasta el apoyo del contorno (o hasta
            otro vacío) y `Bandas` las suma como divisiones si no se eliminaron; `ViguetaBorde.DeVacio`. Prueba
            `LaViguetaDeBordeDelVacioEnElSentidoDeLasViguetasLlegaAlPerimetroYParteElReparto` (el caso del video: vacío
            pegado a la viga de la derecha y apoyado en la del medio). La prueba del vacío de antes sigue pasando.
      - [x] Decisión 12: en `viguetas.js`, las eliminadas (propuestas, de borde y tramos del modelo) solo se dibujan con
            «Eliminar viguetas» activo.
      - [x] `dotnet test CQEING-Addins.sln`: 0 fallos (43 de viguetas). Compila para 2026 y 2027 sin advertencias.
            Desplegado para 2027 (Revit cerrado, comprobado).
      - [x] Probar en Revit con el piso 2 del modelo de Santiago (o PROY_JULIO_BETO con un vacío pegado a una viga). Probado por el usuario el 05-nov: «ya todo bien».
      - La demo de la página (fuera de Revit) no alarga las viguetas de borde: es solo una copia simple de las zonas.
- [x] 05-nov, segunda prueba del usuario (captura con el suelo arriba a la izquierda): decisiones 13 (punto dentro de
      una viga va a su eje: sin vigueta de borde encima de la viga) y 14 (cerrar con Enter, clic derecho o doble clic).
      - [x] Decisión 13: `PisoViguetas.AlEje`, prueba `UnPuntoDentroDelCuerpoDeLaVigaVaASuEjeYNoLlevaViguetaDeBordeEncima`.
      - [x] Decisión 14: en `viguetas.js` (`contextmenu`, `dblclick`, Enter) y el aviso del trazo.
      - [x] 44 pruebas de viguetas, 0 fallos; compila para 2026 sin advertencias.
      - [x] Desplegado para 2027 (Revit cerrado, comprobado con `Get-Process`).
      - [ ] Falta aclarar con el usuario «delimitar el límite de la losa para que la vigueta pase por ahí».
- [x] 05-nov, reunión con Santiago y Juan (transcripción y captura de Z1 con el vacío en L): decisiones 15 a 21.
      - [x] 15: `RepartoViguetas.Calcular` devuelve 0 viguetas si el libre no pasa de eje a eje − ancho. Prueba
            `SiElLibreCabeSinViguetaNoVaNinguna` (0,60 y 0,70 → 0; 0,75 → 1 con 0,325; 0,90 → 1 con 0,40).
      - [x] 16: `PlanificadorViguetas.Suelta` y `Adentro` con los vacíos; usados en `Puntas` y en el tope de `Partir`.
            Prueba `LasVigasDeCadaVanoQueLleganALaDelMedioNoAmontonanViguetasNiLasCortan` (dos luces de 4,00 m, vigas
            por vano hasta la cara de la del medio): 4 + 4 a 0,80, sin topes, 8 viguetas de punta a punta, un solo
            tipo. **Sin el arreglo la prueba falla** con viguetas de más en 3,80 y 4,20 m (comprobado sacándolo).
            `UnaVigaCortaSueltaSiguePoniendoSuViguetaEnLaPunta`: la viga corta de verdad sigue igual.
      - [x] 17: `MasViguetas` = 1.
      - [x] 18: `Regiones`, `PorRegion`, `Banda` con su largo, `Piezas` limitada a la región, `RepartoPanel.Region`
            y `region` en el mensaje `propuesta`; en la página, cotas por región (cada cota dice lo que mide) y «Eje a
            eje» de menor a mayor si difieren. Prueba `AbajoDeUnVacioQueTomaTodaLaLuzSeReparteElAnchoCompletoDeAbajo`
            (vacío en L de viga a viga): abajo 4 a 0,80 sobre los 3,70 m, arriba 2 y 1 a cada lado de la vigueta de
            borde del vacío, la luz derecha 4; abajo y arriba, tipos distintos. Se ajustó
            `UnVacioParteLasViguetasYLlevaViguetasDeBordeHaciaLaLosa` (ids sin `.1`/`.2`).
      - [x] 19: `Tipificar` con `PropuestaViguetas.Cadenas` (las mismas que `Lineas`). Se ajustó
            `DosPanelesSeguidosDanViguetasContinuasDePuntaAPunta`: 3 viguetas de 7,70 m, un tipo (antes 6 piezas).
      - [x] 20: `PisoViguetas.Enderezar`. Prueba `UnLadoCasiDerechoSeEnderezaYSuViguetaDeBordeNoVaEnDiagonal`.
      - [x] 21: `PisoViguetas.Guias(direccion, elegidos)`, y `PuenteViguetas` le pasa los elegidos. Prueba
            `LasCreadasDentroDeLoElegidoNoSonGuia`.
      - [x] En el navegador, con los mensajes reales de C# del caso de la prueba de la decisión 18 (volcados con una
            prueba temporal, ya borrada): 15 líneas, eje a eje 0,80, libres 0,60 a 0,68, tipos T1 5,70 × 4, T2 3,25 × 4,
            T3 1,75 × 2, T4 1,25 × 1 y 4 de borde; cotas de cada región en su lugar.
      - [x] `dotnet test CQEING-Addins.sln`: 0 fallos (53 de viguetas). Compila para 2026 y 2027 sin advertencias.
            Desplegado para 2027 (Revit cerrado, comprobado).
      - [x] Probar en Revit con el mismo caso de la reunión: Z1 sobre las dos luces del N+3.70 de PROY_JULIO_BETO, ↕ Y, Probado por el usuario el 05-nov: «ya todo bien».
            con el vacío en L.
- [x] 05-nov, prueba del usuario con las decisiones 15 a 21 (captura con el vacío 2 en el panel de arriba a la derecha):
      decisiones 22 y 23.
      - [x] 22: `BordesDe` recibe las vigas y para la vigueta de borde del vacío en el eje de la primera viga que cruza su
            línea (también si esa viga va justo en la punta del vacío); `Regiones` parte además por las vigas que cruzan
            las viguetas de lado a lado (no las que terminan sueltas, que siguen con su tope y su vigueta en la punta):
            cada panel entre vigas es su propia luz, y las viguetas siguen siendo una sola a través de una viga cuando
            los dos paneles las ponen en la misma línea. Pruebas `UnVacioChicoSoloCambiaElPanelDondeEstaYLosDeAbajoNoSeMueven`
            y `SiElLadoDelVacioVaSobreUnaVigaSuViguetaDeBordeNoPasaDeEsaViga`. Se ajustó la prueba de la decisión 11
            (`LaViguetaDeBordeDelVacioEnElSentidoDeLasViguetasLlegaAlPerimetroYParteElReparto`): ahora va de la viga del
            medio a la de arriba y el panel de abajo se reparte entero.
      - [x] 23: `Parejo` exige separación ≥ eje a eje / 2; `Repartir` sin la vigueta de la punta sumada aparte. Prueba
            `EnUnaFranjaAngostaLaPuntaDeUnaVigaNoApretaNiDescuadraElReparto` (antes, dos a 0,27 en 0,87 m; ahora una al
            centro). `UnaVigaCortaSueltaSiguePoniendoSuViguetaEnLaPunta` con una punta donde el parejo sí entra (4 a 0,75).
      - [x] Página: las regiones con el mismo reparto llevan sus cotas una sola vez (la de más arriba o más a la
            izquierda).
      - [x] Con los mensajes reales de C# de un caso como la captura (dos columnas de paneles de 2,50 m, vigas por vano,
            la viga corta suelta, el vacío 1 contra la viga izquierda y el vacío 2 chico a la derecha): eje a eje 0,80 en
            todas las regiones, extremos iguales en todas, los paneles de abajo iguales que sin vacíos, y las viguetas de
            borde de cada vacío solo de viga a viga.
      - [x] `dotnet test CQEING-Addins.sln`: 0 fallos (56 de viguetas). Compila para 2026 sin advertencias; para 2027 el
            código compila pero no se pudo copiar la DLL porque Revit estaba abierto (proceso 7400).
      - [x] Desplegar para 2027 con Revit cerrado y probar en Revit el mismo Z1 de la captura. Probado por el usuario el 05-nov: «ya todo bien».
- [x] 05-nov, captura del usuario (todavía con la versión anterior: Revit seguía abierto y no se pudo desplegar): las
      viguetas de abajo del vacío 1 a 0,32 (0,68 · 0,32 · 0,68) son el caso de la decisión 23, ya arreglado; y pidió
      «punto en la vigueta del vértice», que aclaró como las puntas de las viguetas creadas: decisión 24.
      - [x] 24: `PisoViguetas.ArmarPuntos` suma las puntas de las creadas (tipo `vigueta`). Se ajustó
            `LosPuntosSonColumnasCrucesExtremosYVerticesDelSuelo` (12 puntos, la creada da sus dos puntas). 56 pruebas
            de viguetas, 0 fallos; compila para 2026 sin advertencias.
      - [x] Desplegado para 2027 (Revit cerrado, comprobado), con las decisiones 22, 23 y 24.
      - [x] Probar en Revit. Probado por el usuario el 05-nov: «ya todo bien».
- [x] 05-nov, dos capturas del usuario (T8 arriba y T6 abajo de la misma viga; y las de abajo que no seguían a las de
      arriba con la viga corta): decisión 25.
      - [x] `PlanificadorViguetas.PorRegion` con `Grupos` (vecinas a través de una viga de `cortan`), quién manda
            (existentes, vigueta en la punta o más angosta que una vecina del grupo) y `Seguir` (mismas líneas, el ritmo
            eje a eje hacia afuera, una al centro si sobra más que el máximo, la fórmula entre dos muy separadas);
            `RepartirConPunta`; la guía sin líneas repetidas. En la página, el origen dice «los paneles siguen las líneas
            del vecino que manda».
      - [x] Pruebas `ElPanelDeArribaSigueLasLineasDelDeAbajoQueTieneElVacio` (captura 1: 4,825 abajo al lado del vacío,
            arriba 4,825 · 5,625 · 6,425 · 7,225, una sola vigueta de 0 a 6,00 m y un solo tipo) y
            `ElPanelDeAbajoSigueLasLineasDelDeArribaQueTieneLaVigaCorta` (captura 2: abajo las mismas que arriba). Se
            ajustaron a la regla nueva `UnVacioChicoNoCruzaLasVigasYElPanelDeAbajoSigueSusLineas` (abajo cinco a
            0,6125), `SiElLadoDelVacioVaSobreUnaVigaSuViguetaDeBordeNoPasaDeEsaViga` y la de la decisión 11.
      - [x] Con los mensajes reales de C# del caso de la captura anterior (dos columnas, dos vacíos, viga corta): cada
            columna con sus líneas de abajo arriba (0,80 · 1,375 · 1,95 y 3,01 · 3,81 · 4,61), continuas a través de
            todas las vigas; 20 líneas.
      - [x] `dotnet test CQEING-Addins.sln`: 0 fallos (58 de viguetas). Compila para 2026 sin advertencias. Desplegado
            para 2027 (Revit cerrado, comprobado).
      - [x] Probar en Revit los dos casos de las capturas. Probado por el usuario el 05-nov: «ya todo bien».
- [x] 05-nov, captura del usuario («hay una que no alinea»): en el panel de arriba, una vigueta quedaba a ~0,15 de la
      línea de la vigueta de borde del vacío de abajo, y la última a 0,09 de la viga. Ajuste de la decisión 25.
      - [x] `BordesQueLlegan`: la línea de cada vigueta de borde de un vacío que llega a una viga es guía de todo el
            grupo de regiones vecinas (así la columna entera la sigue, viga tras viga); `Seguir` rellena entre líneas y
            hasta el borde con la fórmula centrada (ya no el ritmo hasta donde entre); `PropuestaViguetas.Lineas` une la
            vigueta de borde con la que la sigue del otro lado de la viga en una sola línea de Revit.
      - [x] Prueba `ElPanelDeArribaSigueLasLineasDelDeAbajoQueTieneElVacio` con el caso de la captura: arriba 4,825 ·
            5,55 (en la línea de la vigueta de borde) · 6,325 · 7,125; la de borde y la de arriba, una sola de 0 a 6,00 m;
            ninguna a menos de 0,30 de la viga. Ajustadas a la regla las otras tres de la decisión 25.
      - [x] Con los mensajes reales de C# de un caso como la captura (vacío en el panel del medio de la luz izquierda,
            contra la viga del medio): toda la luz izquierda en 0,61 · 1,41 · 1,92, de abajo arriba, la última en la
            línea de la vigueta de borde del vacío; sin líneas a 15 mm una de otra.
      - [x] `dotnet test CQEING-Addins.sln`: 0 fallos (58 de viguetas). Compila para 2026 sin advertencias. Desplegado
            para 2027 (Revit cerrado, comprobado).
      - [x] Probar en Revit el caso de la captura. Probado por el usuario el 05-nov: «ya todo bien».

- [x] 05-nov: el usuario probó en Revit 2027 (PROY_JULIO_BETO, N+3.70) los arreglos de las decisiones 11 a 25 y
      dijo «ya todo bien». Se publica como `crear-viguetas-v1.2.0`. Sigue pendiente la lectura de suelos con un modelo
      que tenga suelo (criterio 1) y aclarar «delimitar el límite de la losa para que la vigueta pase por ahí».
- [ ] 06-nov, capturas del usuario (N+8.41 P3 de PROYECTO THEVENIN 2): decisiones 26 y 27. Rama
      `viguetas-seguir-sin-diagonales-954` desde `main` (`a2738bb`).
      - [x] 26: `PlanificadorViguetas` usa `Seguir` también con las existentes (paneles y regiones): sus líneas y la
            fórmula centrada donde no llegan. En la página, «Seguir las que ya están» ya no se guarda en el equipo y se
            marca al abrir o cambiar de piso. Prueba `LasNuevasSiguenALasCreadasDeAlLadoYRellenanLoQueFalta`; se
            ajustaron `ConViguetasExistentesEnLaColumnaLasNuevasLasSiguen` y
            `EnElPerimetroLasNuevasSiguenLasDelModeloYSoloRellenanLosTramosQueFaltan` (antes dejaban huecos de 0,80 a
            2,00 m sin viguetas).
      - [x] 27: en `viguetas.js`, `enLineaRecta` (5°): `clicPunto` no agrega un punto en diagonal con el anterior y avisa;
            `cerrarTrazo` no cierra si el último y el primero no quedan en línea; el botón «Cerrar contorno», la línea
            punteada de cierre y el punto verde del primero solo aparecen cuando se puede cerrar en cuadrado.
      - [x] En el navegador (demo): el punto en diagonal no se agrega (aviso), Enter no cierra sin la esquina, con la
            esquina cierra; con «Fórmula» guardado de antes, al abrir queda marcado «Seguir las que ya están».
      - [x] `dotnet test CQEING-Addins.sln`: 0 fallos (59 de viguetas). Compila para 2026 sin advertencias. Desplegado para
            2027 (Revit cerrado, comprobado).
      - [ ] Probar en Revit (el usuario pidió publicarlo el 06-nov: se publica como `crear-viguetas-v1.2.1`; la prueba en Revit queda a su cargo).
- [ ] 08-nov, revisión (filas 6 y 7): decisiones 28 y 29. Rama `viguetas-prolongar-y-vacios-954` desde `main`.
      - [x] 28: `PlanificadorViguetas.Prolongar`, `PisoViguetas.LineasExistentes`, `PedirPropuesta.Prolongar`; la página
            manda `prolongar` en Paneles salvo con «Solo P…». Pruebas `ElegidoUnPanelLaViguetaSigueHastaAbajoPorLosDemas` y
            `LaViguetaQueSigueParaDondeYaHayOtraMuyCerca`.
      - [x] 29: `PisoViguetas.ConVacios` y `ConHuecos` (el mismo cálculo que las zonas), `PedirPropuesta.Vacios`; en la
            página, «＋ Vacío» con su lista en Por vigas y Paneles. Prueba `UnVacioTambienSeAplicaAUnPanelOPerimetro`.
      - [x] En el navegador (demo, viendo los mensajes que manda la página): en Paneles `prolongar` va en true y con «Solo
            P1» en false; los puntos aparecen solo al marcar el vacío; un clic en otro panel no cambia lo elegido; el vacío
            se dibuja, va en el pedido y se quita con su ×; en Por vigas también, sin prolongar. La demo no calcula ni
            la prolongación ni los vacíos: eso lo cubren las pruebas de C#.
      - [x] `dotnet test CQEING-Addins.sln`: 0 fallos (62 de viguetas). Compila para 2026 sin advertencias. Desplegado
            para 2027 (Revit cerrado, comprobado).
      - [ ] Probar en Revit (el usuario pidió publicarlo el 08-nov sin esa prueba: se publica como `crear-viguetas-v1.3.0`).
