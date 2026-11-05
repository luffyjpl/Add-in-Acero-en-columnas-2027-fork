# Plan: despiece de columnas (#226, #227, #228)

> **Para quien lo ejecute:** commit y push solo si Luciano lo pide en ese momento (CLAUDE.md), sin firma de Claude.
> Este plan se sigue en orden. Si algo cambia, se anota primero en Decisiones o Hallazgos y después se programa.

## Contexto

- **Qué se pide:** la parte de columnas del add-in de despiece (épica #217, reunión del 30-oct con Santiago). El
  usuario elige una columna y obtiene sus vistas de despiece en el navegador de Revit; él las arrastra al plano.
  - **#226** Elegir la columna y encontrar las iguales (un despiece por tipo de acero).
  - **#227** Vista completa de la columna con las barras sacadas, etiquetas, cotas y color.
  - **#228** Sección superior, dos veces: una con los colores de traslapo y otra con los colores de cada varilla.
- **Punto de partida:** el despiece de **vigas** ya está en `main` (#231, #233, #235, versión `despiece-v0.1.0`),
  hecho por Juan Felipe. Todo lo de "Cómo se hace" de #217 se probó **en una viga, nunca en una columna**.
- **Fuera de alcance:** viguetas y zapatas; cuadro de acero (pregunta abierta en #218); colocar vistas en hojas (#229);
  cambiar el comportamiento de las vistas de vigas.
- **Entorno:** Revit **2027** (el que usa Luciano; 2026 solo sirve para comprobar que compila); .NET del
  monorepo. **Rama:** `columnas/despiece-226-228`, creada desde `origin/main` (04e0368) en el worktree
  `C:\Proyecto\addins-despiece-col`. Ojo: el `main` local de `C:\Proyecto\addins` está atrasado y no tiene el
  despiece; no trabajar ahí.
- **Falta información:** no hay captura de una columna terminada de Santiago (#218). Todo lo de columnas sale de su
  explicación en la reunión. Se avanza con eso y se ajusta cuando llegue la captura.

## Criterios de aceptación

De #226:
- [x] Se elige la columna en el paso 1 de la interfaz (hoy "Columnas · pronto" está deshabilitado).
- [x] Un solo despiece por tipo de acero: si A2 y B2 llevan el mismo acero, salen juntas; C1, única, sale sola.
- [x] El nombre de la vista dice a cuáles aplica: `Col 1-6, 2-6 - (0.30x0.30)`, con la marca de ubicación de Revit (decisiones 2 y 15).

De #227:
- [x] Una vista por columna (no una por X y otra por Y), con la columna completa y solo columnas y vigas.
- [x] Una barra de cada tipo sacada al lado y alineada, "exactamente lo mismo" que en la viga.
- [x] Etiqueta `4 #6 L= 6.50` en el color de la barra, cotas en metros y longitud total.
- [x] Etiquetas de estribos por zona, una por cada cambio de separación: `25 #4 @0.10`.
- [x] Color por número de barra (`ColoresDiametro`).

De #228:
- [x] Sección superior con barras y estribo; concreto gris. Sin ganchos por pedido de Luciano (decisión 17): confirmar con Santiago.
- [x] Cotas de dimensiones (`0.40 x 0.40`) y de recubrimiento (en la columna circular no hay cotas: no tiene caras planas).
- [x] Estribo sacado aparte, solo con sus medidas, y también los menores (decisión 18); una barra de cada tipo.
- [x] Dos secciones por columna: una con colores de traslapo (intercalados) y otra, igual, con colores por varilla.
- [x] Los colores de traslapo no coinciden con ningún color de varilla de la paleta.

Generales (#217): todo lo hace el add-in; vistas reales y editables; sin rellenos ni líneas que imiten nada; nunca se
editan tipos existentes del proyecto; `dotnet test CQEING-Addins.sln` sin fallos y compilación sin advertencias.

## Hallazgos del código y de Revit real

**Del código (leído el 31-oct en `origin/main`, sin probar todavía en Revit):**

1. **Cómo está armado el despiece de vigas** (`addins/despiece/`):
   - `Revit/LectorDespiece.cs`: junta vigas seguidas en una `VigaContinua` y lee sus barras como `BarraLeida`.
   - `calculo/CQEING.Despiece.Calculo/AgrupadorIguales.cs`: agrupa por "firma" (sección, largo, barras) y arma el nombre.
     El nombre está fijo con `V ...`; el record `ElementoLeido` ya dice "Una viga continua (o una columna)".
   - `Revit/GeneradorPrevia.cs` (794 líneas): crea la vista longitudinal y la sección de **una viga**, la exporta como
     imagen y deshace (`Generar`), o la deja en el navegador (`Crear`). Todo está escrito para vigas (corte en el
     centro de la viga, filas S/I/BS/BI, ejes horizontales).
   - `Revit/GeneradorPlanta.cs`: la planta del paso 1 con las cajas de las vigas para hacer clic.
   - `Interfaz/PuenteDespiece.cs` y `web/pasos.js`: los 3 pasos. En `pasos.js:11` "Columnas · pronto" está apagado.
   - `Revit/MarcaVigas.cs`: lee la marca invisible del add-in de vigas (S1, BI1…). **Columnas no tiene una marca así
     en las barras longitudinales**: el add-in de columnas solo marca estribos (`MarcaEstribos`, con el id de la
     columna) y guarda la configuración por columna en `RegistroGeneradosRevit` (clave: `UniqueId` del elemento de
     abajo de la pila).
2. **Una "columna" son varios elementos de Revit**: el add-in de columnas la lee como una **pila** de
   `FamilyInstance`, uno por piso, en la misma posición en planta (tolerancia 5 cm), en
   `addins/armado-columnas/Logica/LectorContexto.cs` (`AgruparPilas`, línea ~362). El despiece tiene que leerla igual.
3. **Los "dos traslapos" de #228 son los grupos A y B** del add-in de columnas
   (`addins/armado-columnas/Modelo/PlanTraslapos.cs`): "A traslapa en los tramos libres pares y B en los impares".
   Las varillas A y B van intercaladas en la sección. Hipótesis a confirmar: esto es lo que Santiago llama barra
   "interna" y "externa". Como no hay marca, el grupo de cada varilla se deduciría **por la altura de sus traslapos**
   (las del mismo grupo traslapan a la misma altura). Se verifica en el paso 1.
4. **Paleta** (`acero-vigas/.../ColoresDiametro.cs`): #3 amarillo, #4 azul, #5 rojo, #6 verde, #7 morado, #8 marrón,
   #9 rosado, #10 negro, #11 naranja. El rojo y azul de Santiago chocan con #5 y #4.
5. `addins/armado-columnas/Logica/DetallesPlegado.cs` y `ComandoDiagnosticoDetallePlegado.cs` ya usan
   `RebarBendingDetail` en columnas: revisar qué se aprendió ahí antes del diagnóstico.

**De Revit real** (`ComandoDiagnosticoColumna`, Revit 2027, `PROY_JULIO_BETO`):

*1ª corrida, 31-oct 10:06, columna 387984 (`COL_25X30`, ejes C y 2), que resultó **sin acero**:*

6. **En este modelo la columna es un solo elemento**, de 0.00 a 6.70 m (no uno por piso como en el modelo donde se
   validó `AgruparPilas`). El lector tiene que aceptar las dos cosas: pila de uno o de varios.
7. **Ejes:** los dos ejes que la cruzan están a 0.000 m del centro (`C` en Y y `2` en X). El nombre `C2` sale directo.
8. **El modelo no tiene registro del add-in de columnas** ("no hay"): ninguna columna de este modelo se generó con
   la versión que guarda la configuración. Para probar la decisión 4 hace falta una columna generada con el add-in
   de columnas actual (1.6.0).
9. **Alzado con `CreateSection`** (base X, Z, X×Z): `RightDirection` = (−1, 0, 0), `ViewDirection` = (0, 1, 0). Se
   confirma lo de #217: la derecha sale contraria al `BasisX`.
10. **Sección horizontal con `CreateSection` sí se puede crear**, pero con base X, Y, Z sale **mirando desde abajo**
    (`ViewDirection` = (0, 0, −1), derecha −X): espejada. Para la 2ª corrida se agregó la variante con BasisX = −X y
    BasisZ = −Z, que debería verse desde arriba con el norte arriba.
11. **Sección horizontal con planta propia** (`ViewPlan.Create` + rango de vista): funciona, derecha +X (normal). Usa
    el nivel de abajo (`N+0.50`) con el corte a 2.85 m sobre él.
12. **Cota de ancho con las caras de la columna** (`ComputeReferences` + vista): funciona en las dos secciones
    horizontales, da 0.250 m.

*2ª corrida, 31-oct 10:17, modelo `Prueba 08072026`, columna en ejes 1 y 6, generada con el add-in de columnas:*

13. **Pila de 3 elementos**: dado 781599 `(0.40 x 0.40)` de −0.65 a 0.00, y 782105 / 782736 `(0.30 x 0.30)` por piso.
    Ejes `1` y `6` a 0.000 m. 16 conjuntos verticales (#4, uno por pieza, cantidad 1) y 5 de estribos (#3,
    `CQ Estribo`, @0.20; zonas de 3 y 17 por piso).
14. **8 varillas, 2 piezas cada una** (piezas de la misma varilla a menos de 3.5 cm en planta). Formas `CQ Recta 90-0`
    abajo y `CQ Recta 0-90` arriba.
15. **Grupo A/B por geometría: coinciden 8 de 8.** El anillo (desde la esquina inferior izquierda, contra reloj; pares
    A) da lo mismo que la altura de los traslapos: A traslapa en 1.19–2.39 m y B en 4.69–5.89 m. Intercaladas.
16. **El registro existe (2124 caracteres) pero ninguna clave coincidió con los 3 elementos de la pila.** Según
    `LectorContexto.ListarColumnas` la clave debería ser el `UniqueId` del dado (`pila[0]`). Para la 3ª corrida se
    agregó listar cada clave y a qué elemento apunta.
17. **Barra vertical sacada en el alzado: rotación 0 la deja parada** (detalle 0.231 x 3.319, la barra 0.222 x
    3.310). Con 90° queda acostada. Igual que la viga, sin rotar.
18. **Estribo aparte:** 0.239 x 0.239 con 0° y con 90° (es cuadrado): la medida no dice hacia dónde queda el gancho.
    Hay que mirarlo en la vista.
19. **Sección horizontal:**
    - `CreateSection` con base X, Y, Z: mirando desde abajo (espejada), como en la 1ª corrida.
    - `CreateSection` con BasisX = −X, BasisZ = −Z: mira desde arriba (`ViewDirection` +Z) pero **girada 180°**
      (derecha −X, arriba −Y). Revit no respeta la base pedida tal cual.
    - **Planta propia (`ViewPlan.Create` + rango de vista): derecha +X, normal.** Corte a 1.775 m sobre `N+0.00 Piso 1`.
    - En las tres: 13 barras visibles de 21 y la cota de ancho con las caras da 0.300 m.

*3ª corrida, 03-nov 08:50, misma columna, con capturas (`imagenes/despiece-col-diag-alzado.png` y
`imagenes/despiece-col-diag-planta.png`):*

20. **El registro solo tiene una columna**: la 781598 (en x = 3.50, generada el 29-oct). La de los ejes 1 y 6 (781599)
    no está, aunque su acero lo hizo el add-in de columnas: se generó antes de que existiera el registro, o con otra
    versión. **El registro no cubre todas las columnas generadas**; la geometría sí (8 de 8).
21. **Estribo aparte con rotación 0: queda igual que en la sección** (gancho abajo a la izquierda en los dos). Con 90°
    queda girado. Se usa 0, como en la viga.
22. **Planta propia, se ve bien**: columna gris con barras, estribo y grapas; cota de ancho debajo. Pero sale con
    cosas que sobran y hay que quitar: el **rayado del concreto** (triángulos y puntos encima del gris), el **eje 1**
    cruzando la sección y los detalles, y la **marca de la sección del alzado** (círculo azul) y la de alzado.
23. **Alzado**: la barra sacada con rotación 0 sale parada al lado de la columna, con su gancho abajo. Sobran: el
    **acero de las vigas** (estribos y barras de la viga del piso 2), la **cimentación** (relleno marrón) y las marcas
    de sección. El recorte es angosto: las barras sacadas casi no caben. Las barras aún no tienen color (el
    diagnóstico no las pinta).

*Prueba del paso 3, 04-nov 07:44, `Prueba 08072026`:*

24. **Una barra de columna no tiene caja** (`get_BoundingBox(null)` da null): el despiece no cargó ("Object reference
    not set…", `errores.log`, `LectorColumnas.cs:78`). En vigas nunca pasó. Corrección: la altura de la barra se mide
    con su eje (`GetTransformedCenterlineCurves` de la primera y la última barra del conjunto), que siempre existe, y
    los nombres de tipo y forma toleran vacíos. Las barras sin caja se anotan en la traza (`columnas: barra … sin
    caja`) para saber cuáles son. Además, por la decisión 7, **una columna que falle al leerse se salta** (queda en
    la traza `columnas: no se pudo leer la columna …`): un error en columnas nunca vuelve a impedir el despiece de vigas.

*Primera vista previa del alzado, 04-nov 08:10, `Prueba 08072026` (capturas de Luciano):*

25. **Se ve bien, pero en algunas barras sacadas no se dibuja el gancho**, aunque su largo (`0.22`) sí sale: el detalle
    es la barra vista de frente, y la barra elegida para dibujar el grupo tenía el gancho apuntando hacia quien mira
    (cara de adelante o de atrás de la columna); de frente se ve como un punto. Corrección: de las iguales se dibuja
    la que tenga los ganchos más de lado (a lo largo de la derecha de la vista). Si ninguna los tiene de lado, se
    anota en la traza (`columna: ningún gancho de lado …`).
26. **Los largos no salen redondos con el redondeo elegido en columnas** (`4 #4 L= 3.31/3.53`). No es del despiece:
    lee y escribe el largo real a dos decimales, y la suma da exacta. Se dejó la issue #245 para el add-in de columnas.
27. **Las dos piezas de una misma varilla salían separadas** (Luciano, 04-nov: "las varillas que se supone van juntas
    porque van traslapadas salen separadas"): los carriles solo evitaban encimarse, y la pieza de arriba caía en otro
    carril, lejos de la de abajo. Corrección en la decisión 14.
28. **El zoom de la columna era incómodo** (Luciano, 04-nov: "se hace zoom al centro sin importar dónde esté y hay un
    punto donde no puedo ir más al lado"). Causa en `Visor.limitar` (`pasos.js`): el límite de cuánto se mueve el
    dibujo dependía del tamaño de la imagen, no de lo que se ve. Con una imagen alta y angosta (la columna) sobra
    mucho espacio a los lados: al acercar con el cursor ahí, el límite corregía el desplazamiento (parecía acercar al
    centro) y no dejaba llevar el dibujo hacia los costados. Corrección: el límite se mide sobre lo que se ve en la
    ventana; siempre queda a la vista al menos un cuarto de ella con dibujo (o el dibujo entero, si es más chico).
    Afecta también a la planta y a la vista de vigas: solo deja moverse más, nunca menos.

*Segunda vista del alzado, 04-nov 16:24, otra columna de varios pisos (captura de Luciano). En `Prueba 08072026` todo
bien (nombre, ganchos, traslapos y zoom):*

29. **Una etiqueta `1 #3` por cada estribo**: en esa columna cada estribo es un elemento de Revit suelto (cantidad 1),
    no un conjunto, y la etiqueta se hacía por conjunto. Corrección: los estribos se agrupan en **zonas** con
    `ZonasEstribos` (calculo, con pruebas): se toma la altura de cada estribo (de cada barra del conjunto), por
    diámetro; los que están a la misma altura (estribo y grapa) cuentan una vez; los seguidos con la misma separación
    (al centímetro) forman una zona `15 #3 @0.14`; una separación mayor que 0.40 m corta la zona. Cada zona lleva una
    etiqueta y se mueve sola (clave `e<barra>-<índice>` de su primer estribo). Medido al probarlo (04-nov): Revit lanza "This rebar
    element represents a single bar (the layout rule is Single)" al leer `IncludeFirstBar` de un estribo suelto;
    esas dos propiedades solo se leen si `LayoutRule` no es `Single`.
30. **Líneas mal armadas en esa columna**: medidas de traslapo de 2.64 y 4.80 m (no son traslapos) y textos de ganchos
    encimados arriba (dos piezas de la "misma" línea con el gancho a la misma altura). Señal de que la lectura junta
    en una varilla piezas que no lo son (barras en paquete a menos de 3.5 cm, o piezas que corren paralelas). **No se
    corrige a ciegas**: se agrega a la traza cada varilla con sus piezas (`columna: varilla … piezas …`) y cada línea
    (`columna: línea …`), se repite la prueba y se decide con esos datos. La columna es la **781574** de
    `Prueba 08072026` (102 barras). La traza de esa corrida dice además `ningún gancho de lado` en 4 grupos (817584,
    817590, 817597, 817603): ahí todas las iguales tienen el gancho hacia quien mira y no se dibuja; se decide junto
    con lo de las líneas.

*Traza de la columna 781574, 04-nov 16:41 (`columna: varilla …` y `columna: línea …`):*

31. **Causa de las líneas mal armadas (hallazgo 30): la columna es circular**, de 12 varillas a ~5 cm una de otra
    (centro ≈ 3.50, 8.00; radio ≈ 0.094 m). Cada varilla tiene 2 piezas: A de −0.92 a 2.29 y de 0.79 a 7.00; B de
    −0.92 a 5.84 y de 4.74 a 7.00. `LectorColumnas.Varillas` juntaba por cercanía (3.5 cm a la primera pieza) y la
    pieza de arriba, corrida por el traslapo, caía junto a la vecina: varillas de 3 piezas (817570 + 817576 + 817725,
    con la de arriba de una B) y otras de 1. De ahí los "traslapos" de 4.80 m y los ganchos encimados.
    **Corrección:** `CadenasVarillas.Unir` (calculo, con pruebas armadas con estas medidas) une una pieza con la
    siguiente solo si **traslapa** con ella (empieza dentro y termina más arriba) y está a no más de 5 cm en planta;
    se emparejan primero las más cercanas y cada pieza tiene a lo sumo una abajo y una arriba. Así la pieza propia
    (1.5 cm) gana siempre a la vecina (≥ 3.5 cm). Dos barras en paquete de la misma altura quedan como dos varillas.
33. **Más etiquetas de estribos que antes en la columna circular** (04-nov 17:10, Luciano: "ahora aparecen aún más
    estribos que antes. Las barras creo que ya salen bien"). Traza `columna: zonas de estribos`: decenas de zonas de 2
    (`2 #3 @0.04`, `2 #3 @0.12`). Causa: a cada altura hay dos piezas de estribo distintas (estribo y grapa o caja)
    a 1–4 cm una de otra; las zonas se armaban con todas las #3 juntas, y las alturas alternadas nunca daban una
    separación constante. **Corrección:** las zonas se arman por separado para cada forma y largo de estribo
    (`tipo|forma|largo al cm`), y después la etiqueta con el mismo texto a menos de 0.10 m de otra se escribe una vez
    (como la grapa en la viga).
34. **"Hay casos donde el zoom aún no me deja mover"** (Luciano, 04-nov 17:17, columna circular acercada, "no puedo
    mover más a la derecha"). Reproducido fuera de Revit con la misma `pasos.js` y una imagen de 1800 × 2730 px
    (página de prueba servida en `127.0.0.1`, eventos de rueda y arrastre simulados): el límite nuevo funciona; el
    dibujo llega hasta 3/4 de la ventana por la izquierda y 1/4 por la derecha, sin trabarse antes. Hipótesis, sin
    confirmar: el arrastre empezó sobre una barra o un texto (en la columna se arrastran a los lados, y la zona que
    agarra una barra crece con el zoom), y se movió la pieza en vez de la vista. Se le pregunta a Luciano antes de
    cambiar nada.
    También se confirmó que la vista de 17:09 era la versión anterior (DLL de 16:52): el arreglo del hallazgo 33 no
    estaba instalado.
    **Respuesta de Luciano (04-nov, tarde):** no se mueve ninguna pieza; "por momentos el zoom va solo al medio y
    llega a un punto que no deja mover". La hipótesis de la pieza queda descartada. También se descartó que WebView2
    sirviera la `pasos.js` vieja desde su caché (la caché de `%LocalAppData%\CQEING\despiece\WebView2` no guarda
    las páginas del host virtual). **Siguiente paso, diagnóstico temporal:** cuando `Visor.limitar` corrige lo
    pedido, la página manda `{ tipo: 'traza', texto }` con el zoom, lo pedido, lo que quedó, el tamaño de la imagen y
    lo que se ve; `PuenteDespiece` lo escribe en la traza (`página: …`). Se borra al cerrar este punto.
    **Resultado (04-nov, Luciano con el touchpad: "entre más acercaba más iba al medio… no podía moverlo más a la
    derecha pero sí a la izquierda"):** la imagen de la columna circular mide 1800 × 7544 px; en una ventana de
    1139 × 662 px lo que se ve a lo ancho equivale a ~12 978 px de imagen, unas 7 veces su ancho. La regla "siempre a la
    vista un cuarto de la ventana con dibujo" exigía ~3244 px de imagen dentro: el límite actuaba casi en cada paso
    (`limitar k 4.55 pedido (-10751, …) quedó (-10540, …)`), el zoom crecía desde el borde frenado y no desde el
    cursor ("va al medio"), y el arrastre se frenaba de un lado. **Corrección:** basta con que quede a la vista una
    franja fija de **80 px de pantalla** de dibujo (o el dibujo entero, si es más chico), convertida a unidades del
    dibujo con lo que se ve; así el límite solo actúa cuando el dibujo está por perderse.
    **2ª prueba (04-nov 18:26, Luciano: "sigue igual, no sé si es que lo toma como si estuviera en una vista más
    alejada… ahora se hace a más nivel de zoom"):** la traza muestra el límite nuevo frenando con el borde derecho de
    la imagen a 80 px del borde izquierdo de la ventana (`k 3.05 … quedó (-10168, …)`), es decir, Luciano empujaba la
    imagen casi fuera. **Causa real:** en el paso 2 las barras sacadas y sus textos se ocultan de la imagen (para
    arrastrarlos, `Separar`) y la página los dibuja encima. Al ocultarlos, Revit exporta solo la columna, los estribos
    y las cotas (1800 px de ancho), y **las barras quedan dibujadas fuera de la imagen**, a su derecha. El visor solo
    conocía la imagen: no dejaba traer a la vista lo de más a la derecha ni lo encuadraba con "Todo". **Corrección:**
    `Visor` recibe los límites del dibujo completo (la imagen más lo de la capa movible: puntos de las barras y cajas
    aproximadas de los textos) y los usa para el encuadre (`viewBox`) y para el límite. En la viga pasa lo mismo arriba
    y abajo; el cambio la cubre igual.
32. **Ganchos que miran hacia quien ve el alzado, sin alternativa** (grupos 817584, 817590, 817597, 817603 de la
    columna circular): en una columna circular los ganchos apuntan al centro, y en algunos grupos todas las iguales
    lo tienen hacia adelante o atrás; el detalle de frente no lo dibuja. **Pendiente**, sin decidir: verlo en la
    captura con las líneas ya bien armadas antes de proponer algo.

35. **Los números de las cotas salen diminutos** (04-nov 20:22, Luciano, en el estribo aparte de la sección: "los
    números de cota están saliendo súper pequeños, mira si en algún otro lado es igual"). Pasa en todas las cotas del
    despiece: las del detalle del estribo, las de ancho y alto de la sección y las del alzado (en las capturas se ve la
    línea pero casi no el número), y en la viga, que usa los mismos tipos. Causa: `TiposPropios.EnMetros` duplica el
    tipo de cota del proyecto y hereda su tamaño de texto; las notas del despiece usan 2.5 mm. **Corrección:** los
    tipos de cota propios (`CQ Despiece - metros` y su versión corta) llevan siempre el texto a 2.5 mm, como las notas
    (son tipos del despiece: no se toca ninguno del proyecto). La traza anota el tamaño que traía el original
    (`tipos: texto de cota …`). Cambia también en la viga: solo se agranda el número.
36. **Con el número legible, las medidas de los ganchos tapan el dibujo del estribo aparte** (04-nov 20:39, Luciano:
    "está el número tapando los ganchos"; en la columna de los ejes 1 y 6, los `0.08` del estribo y los `0.11` de la
    grapa). Primero se pensó apagar esas medidas (`SegmentLengthDimensionsForHooksEnabled = false`), pero Luciano
    prefiere que se vean sin tapar ("¿no hay alguna forma de hacer que aparezcan sin tapar lo otro?"). **Corrección:**
    el tipo `CQ Despiece sin etiqueta` pone el número del lado de afuera de la línea de cota
    (`SegmentLengthDimensionTextPosition = BendingDetailDimensionTextPosition.OutsideOfDimensionLine`, pública en la
    API 2027). La separación de la cota al tramo (`SegmentLengthDimensionsOffset`) no dice su unidad en la
    documentación: no se toca a ciegas; la traza anota la que trae el tipo (`tipos: separación de las medidas …`) y,
    si con el número afuera no alcanza, se ajusta con ese dato. **Probado (04-nov 20:45): igual que antes.** La traza
    dio `separación de las medidas del detalle 0,0098` pies = **3 mm**, el valor de papel que trae Revit por defecto:
    la unidad es de papel. Con el número a 2.5 mm, 3 mm no lo saca del dibujo. Se sube a **6 mm** (0.006 / 0.3048
    pies). El apilado de estribos mide cada detalle con `get_BoundingBox`, así que el espacio extra se respeta.
    **Probado (04-nov 20:59): siguen encimadas.** Los dos ganchos del estribo están en la misma esquina y Revit pone
    sus medidas solas, una sobre otra. **Solución final, la de la viga:** el detalle no muestra la medida de los
    ganchos (`SegmentLengthDimensionsForHooksEnabled = false`; en la viga ya no la mostraba, probado el 3-nov) y su
    largo va en texto con `GeneradorPrevia.EtiquetarGancho`, que en la viga lo pone donde hay espacio (el de adentro
    del otro lado de las patas, el de afuera al costado). Se generalizó para usar el "arriba" de la vista
    (`vista.UpDirection`) en vez de Z: en la sección de la viga "arriba" es Z y da lo mismo; en la planta es Y. Se
    deshicieron el número afuera y los 6 mm. Solo el estribo principal lleva estos textos, como en la viga; el gancho
    de la grapa va en su texto (`G= 0.11`).

## Decisiones

<!-- Las marcadas "por confirmar" las decide Luciano al revisar este plan. -->

1. **Detección de iguales: las detecta el add-in y el usuario confirma** (2026-10-31, Luciano): igual que en vigas,
   al hacer clic en una se marcan solas las del mismo acero, y el paso 2 lo muestra.
2. **Nombre: `Col A2-B2 - (0.40x0.40)`** (2026-10-31, Luciano). Cada columna se nombra por el cruce de sus ejes
   (`A2`). Si dos grupos se llamaran igual, se agrega lo que los distinga, como en vigas.
3. **Colores de traslapo: cian `#00ACC1` para A y gris `#9E9E9E` para B** (2026-10-31, Luciano), que no están en la
   paleta. Queda avisar a Santiago/Carlos (#218).
4. **Grupo A/B: se calcula con lo que guarda el add-in de columnas, y se comprueba con la altura de los traslapos**
   (2026-10-31, Luciano preguntó si se podía tomar de la configuración guardada). Lo que se encontró:
   - El add-in de columnas guarda en el modelo, por columna, con qué se generó (`RegistroGeneradosRevit`: esquema
     `3F1A7C52-6B0E-4D2A-9E8B-71C4D5A2F0E3`, un `DataStorage` llamado "CQEING - columnas generadas", JSON con
     clave `UniqueId` del elemento de abajo de la pila). Incluye barras por cara X e Y y el número de barra por
     posición (`ConfigLongitudinal`).
   - **No guarda el grupo de cada varilla**, pero sí la regla para sacarlo: `ConfigLongitudinal.Anillo()` recorre el
     perímetro desde la esquina inferior izquierda (cara inferior de izquierda a derecha, cara derecha hacia
     arriba, cara superior de derecha a izquierda, cara izquierda hacia abajo); las posiciones pares son A y las
     impares B (`BarraPrevia.cs:31`).
   - Cómo se usa: el despiece **solo lee** ese JSON (no escribe nada), ordena las varillas por ese mismo recorrido y
     les asigna A/B. Luego comprueba con la geometría: las del mismo grupo deben traslapar a la misma altura. Si no
     coincide, o la columna no tiene registro (acero hecho a mano), se usa solo la altura de los traslapos, y si
     tampoco se puede, la sección de traslapos no se crea y se avisa en el paso 2.
   - No se copia ni se referencia código del add-in de columnas: el despiece lee solo los dos números que necesita
     (`BarrasPorCaraX`, `BarrasPorCaraY`) con su propio record. Así un cambio en columnas no rompe el despiece, y
     viceversa.
   - **Se mantiene tras el diagnóstico** (2026-11-03, Luciano eligió esta opción frente a "solo geometría"): el
     registro no cubre todas las columnas (hallazgo 20), pero cuando la columna está se usa; si no está, o su número
     de varillas no coincide con el que guarda, se usa la altura de los traslapos (8 de 8 en el hallazgo 15). Si las
     dos fuentes no coinciden, manda el registro y se anota en la traza.
   - Detalle de la regla, medido: la esquina de partida es la de menor X + Y, y se avanza contra reloj visto desde
     arriba; la primera es A. La clave del registro es el `UniqueId` del elemento de más abajo de la pila (el dado).
5. **Código de columnas en carpeta propia**, `addins/despiece/Revit/Columnas/`, y lo de vigas no se cambia salvo lo
   mínimo para compartir (puente, página, planta, agrupador). Cualquier cambio a un archivo compartido se avisa a
   Juan Felipe. Lo que resulte igual (tipos propios, colores, etiquetas) se mueve a un archivo común solo cuando se
   use en los dos (#220).
6. **Sin ensamblajes** (#219, #227): la ruta de `CreateSection` funcionó sin ellos en vigas y la lista de partes no
   agrupa barras iguales. Se descartan salvo que el paso 1 muestre que hacen falta.
7. **No se daña nada de lo ya hecho** (2026-10-31, Luciano):
   - `addins/armado-columnas/` y `addins/acero-vigas/` no se tocan.
   - En el despiece, lo de vigas solo cambia donde hay que sumar columnas (puente, página, planta, agrupador), y
     siempre sumando: con "Vigas" elegido todo sale igual que hoy. Lo comprueban las pruebas de
     `AgrupadorIgualesTests` existentes (deben seguir pasando sin cambiarlas) y, en Revit, crear de nuevo las vistas
     de la viga 392698 y compararlas con las de `main`.
   - Los datos del modelo se leen, no se escriben (salvo las vistas y tipos propios `CQ Despiece …` que ya crea vigas).
8. **Se sigue el camino de viguetas para sumar un tipo de elemento** (2026-11-03, Claude, al mezclar `main`): Juan
   Felipe ya agregó viguetas con un campo `EsVigueta` en `ElementoLeido` (con valor por defecto), el prefijo `Vigueta`
   en `AgrupadorIguales.Nombre` y `tipo: 'vigueta'` en `pasos.js`. Columnas suma lo mismo: `EsColumna` (por
   defecto `false`, así vigas y viguetas no cambian) y prefijo `Col`.
9. **Sección superior con planta propia** (`ViewPlan.Create` + rango de vista), no con `CreateSection` (2026-11-03,
   Claude, por los hallazgos 10, 19 y 22): es la única que sale derecha, con el norte arriba. Se oculta el rayado del
   concreto (queda solo el gris), los ejes, niveles, marcas de sección y alzado.
10. **Alzado: solo la columna, sus vigas (sin su acero) y el acero de la columna** (2026-11-03, Claude, hallazgo 23):
    sin cimentación, sin acero de vigas, sin marcas de sección. Recorte con espacio para las barras sacadas.
11. **Columnas con arrastre de barras y textos, sin vista 3D** (2026-11-04, Luciano: "que sí se pueda mover pero no la
    vista 3D"; reemplaza la propuesta del 03-nov de dejar las dos cosas fuera). `main` agregó a vigas mover barras y
    textos con el mouse, vista 3D y "Textos en negro". Con "Columnas":
    - Se puede **mover** igual que en vigas: la página ya guarda lo movido por el primer elemento del grupo
      (`g.vigas[0]`) y lo manda en `previa` y `crearVistas`; `GeneradorColumna` devuelve su capa movible (paso 4) y
      aplica lo movido al crear las vistas (paso 6).
    - **No hay botón 3D**: la página no lo muestra si el grupo es de columnas, y el puente no atiende `ver3d` para una
      columna.
    - "Textos en negro" se mantiene (es solo el color de los textos).

12. **Nombre de los ejes de una columna** (2026-11-03, Claude; 2026-11-04, Luciano lo aprobó): la letra primero y sin
    separar (`C2`), como en la issue. Si los dos ejes son números (ejes `1` y `6` del modelo `Prueba 08072026`), con
    barra: `6/1`, para no leer "61"; primero el que corre de sur a norte. Falta ver en Revit que acepte la barra en el
    nombre de la vista (paso 6).

13. **El paso 3 conecta la elección; las vistas llegan en el paso 4** (2026-11-04, Claude, al empezar el paso 3):
    `GeneradorColumna` todavía no existe, así que en el paso 3 el puente **no** le pasa una columna al generador de
    vigas (`Continua` fallaría): si la página pide la previa o crear vistas de una columna, responde con un aviso
    ("las vistas de columnas todavía no están") y no hace nada en el modelo. El paso 4 reemplaza ese aviso por la
    llamada a `GeneradorColumna`.
    - En la planta del paso 1 se fuerza a mostrar las columnas estructurales en la copia temporal (como ya se hace con
      el armazón): si la planta original las oculta, no se podrían elegir. La planta original no se toca y con
      "Vigas" el clic funciona igual (las cajas de columnas solo se dibujan con "Columnas" elegido).
    - En la planta, la caja de cada elemento de la pila que se ve en ese nivel apunta al grupo de su columna, así que
      el clic en cualquiera elige la columna entera.

14. **Cómo se arma el alzado de la columna** (2026-11-04, Claude, al empezar el paso 4; se ajusta cuando llegue la
    captura de Santiago, #218). Lo que en la viga va arriba y abajo, en la columna va a los lados:
    - **Vista:** sección vertical a 1:15, como la viga (`CreateSection` con la base corregida del hallazgo 9), mirando
      hacia el norte, con la derecha al este (+X). Detalle medio, colores planos, sin niveles, ejes ni marcas.
    - **Qué se ve:** los elementos de la pila, las vigas que tocan la columna (sin su acero) y el acero de la columna,
      pintado por diámetro y visible a través del concreto. Lo demás del modelo se oculta en la vista (cimentación,
      otras columnas, acero de vigas). Recorte justo al alto de la pila y 0.30 m a cada lado, para que se vea el
      arranque de las vigas.
    - **Barras sacadas a la derecha**, cada una **a su altura real** (alineada con la columna). Iguales en diámetro,
      forma, largo y altura (al centímetro) se dibujan una vez, con la cantidad sumada: con 8 varillas en dos piezas y
      A/B salen 4. De las iguales se dibuja la que tiene los ganchos de lado (hallazgo 25). Cada una con su etiqueta
      `4 #4 L= 3.50` girada, a su derecha, y el largo de cada gancho en texto junto a él (como en la viga).
    - **Las piezas que traslapan van juntas, en una línea** (hallazgo 27, como `AcomodarLinea` en la viga): la pieza
      que sigue a otra en una misma varilla (`ColumnaLeida.Varillas`, de abajo hacia arriba) va en la misma línea,
      corrida 0.08 m a la derecha para que se vea el traslapo, y alternando. La medida del traslapo (de los tramos
      rectos de las dos piezas) va en texto girado a la izquierda de la línea, a la altura del traslapo, y se mueve
      con la pieza de arriba. Con A/B salen dos líneas (A y B). Las líneas se reparten en carriles sin encimarse con
      `AcomodadorFilas` (ya probado en vigas, aquí sobre la altura en vez del largo).
    - **Estribos:** una etiqueta por conjunto, `17 #3 @0.20`, a la izquierda, a la altura del centro de su zona. Si dos
      quedan muy cerca suben para no encimarse. La grapa con el mismo texto en la misma zona se escribe una vez.
    - **Cotas a la izquierda**, en metros (`CQ Despiece - metros corta`): la cadena de cada elemento de la pila (dado,
      piso 1, piso 2…) y, más afuera, el alto total. Con un solo elemento, solo el total. Como en la viga, contra planos
      de referencia en blanco, y si Revit no las acepta la vista sale sin ellas.
    - **Arrastre (decisión 11):** en la columna las barras se mueven **a los lados** (en la viga, arriba y abajo),
      con su etiqueta y sus ganchos; los textos se mueven libres. `Movimiento` suma `Dx` (por defecto 0, así lo que ya
      manda la página de vigas sigue valiendo) y la capa avisa `horizontal` para que la página arrastre en X.
    - **Para compartir con vigas sin duplicar**, en `GeneradorPrevia.cs` pasan de `private` a `internal` (sin cambiar
      lo que hacen): `Preparar`, `Recortar`, `Colorear`, `Pintar`, `Solido`, `Capa`, `Marcar`, `Curvas`, `Mayor` y el
      dato de "textos en negro". Y en `PuenteDespiece` la sección de la previa puede venir vacía (la columna no la
      tiene hasta el paso 5).

15. **El nombre de cada columna es su marca de ubicación de Revit** (2026-11-04, Luciano: "en la cuadrícula las
    columnas salen con la estructura 1-6, pero luego en el despiece se menciona como 6/1. ¿No se podría hacer para
    que salga igual?"). **Reemplaza la decisión 12 y ajusta el formato de la 2.**
    - Se lee el parámetro "Column Location Mark" (`BuiltInParameter.COLUMN_LOCATION_MARK`, público y documentado en
      la API 2027) del elemento más bajo de la pila que lo tenga. Revit lo arma con los ejes que la cruzan: `1-6`,
      `C-2`.
    - Si la columna no está justo en el eje, Revit le agrega la distancia entre paréntesis (`C(-0.15)-2`): **se deja
      tal cual** (2026-11-04, Luciano: "lo de la columna fuera del eje sí me parecía bien"; al principio se quitaba).
      Solo se quitan los espacios de los extremos.
    - Si Revit no da marca (sin ejes cerca), se arma con los ejes como antes, pero con guion, igual que Revit.
    - Varias columnas con el mismo acero se separan con **coma**: `Col 1-6, 2-6 - (0.30x0.30)`. Con guion
      (`Col 1-6-2-6`) no se sabría dónde termina cada una. Vigas y viguetas siguen con guion (`V A-B`).

16. **Cómo se arma la sección superior** (2026-11-04, Claude, al empezar el paso 5; #228 pide "sección superior con
    barras, ganchos y estribo"; se ajusta con la captura de Santiago, #218):
    - **Dónde se corta:** "superior" es el extremo de arriba, donde las varillas terminan con su gancho doblado hacia
      adentro. Planta propia (decisión 9), con el corte **1 cm debajo del tope de la pila**, para que el concreto
      salga cortado (gris) y los ganchos, que están debajo del corte, se vean desde arriba. El rango de vista baja
      0.50 m: alcanza los estribos de arriba. El nivel de la planta es el más alto que está por debajo del corte.
    - **Qué se ve:** solo la pila y su acero; el acero visible a través del concreto (`SetUnobscuredInView`). Las
      barras verticales que terminan más de 0.15 m debajo del corte (las piezas de abajo) se ocultan, para no ver
      dos círculos por varilla. Detalle **fino**: con detalle medio una barra vertical vista desde arriba es solo un
      punto (en la viga se cortaba, aquí se mira). Sin rayado del concreto (solo el gris), sin ejes, niveles ni marcas.
    - **Etiquetas:** una por barra, `1 #4`, con flecha, como en la sección de la viga: las de la mitad de arriba en una
      fila encima de la columna y las de abajo en una fila debajo.
    - **Cotas:** ancho y alto con cotas de Revit contra las caras del elemento de arriba (`ComputeReferences`,
      hallazgo 12), y el recubrimiento en texto (`0.04 · 0.22 · 0.04`), igual que la viga. En una columna circular no
      hay caras planas: se omiten y se anota en la traza.
    - **Estribo aparte**, a la derecha, con sus medidas (tipo `CQ Despiece sin etiqueta`, rotación 0, hallazgo 21) y
      debajo `46 #3 L= 0.99 G= 0.08` (cantidad total de la pila). Es el de más tramos que se vea en la planta.
    - **Dos veces:** la misma planta duplicada. En la **primera**, barras y etiquetas en el color de su grupo de
      traslapo (A cian `#00ACC1`, B gris `#9E9E9E`, decisiones 3 y 4) y debajo una leyenda `Traslapo A` / `Traslapo B`
      en esos colores. En la **segunda**, el color de cada diámetro. Si la columna no tiene grupos (decisión 4: no se
      pudo saber), la primera no se crea y la página lo avisa. Un conjunto de Revit con barras de los dos grupos
      (hecho a mano) se pinta con el grupo de la mayoría y se anota en la traza.
    - **En la página:** a la derecha, las dos imágenes una debajo de otra, con su título ("Traslapos" y "Varillas").
      `PreviaGenerada` suma la segunda sección (por defecto vacía, la viga sigue igual). *Reemplazado por la
      decisión 17.*

17. **Sección sin ganchos y en un paso propio** (2026-11-04, Luciano, al ver la primera sección: "siento que se está
    tomando mucho espacio, creo que estaría bien si se agrega un paso más en columna, para que se vea en otra pantalla
    y quepa todo bien. También sería mejor que se muestre sin ganchos"). Cambia dos puntos de la decisión 16:
    - **Sin ganchos:** el corte baja hasta debajo de donde empiezan los ganchos de arriba: 3 cm debajo del final del
      tramo recto más bajo de las barras que llegan arriba (`GeneradorPrevia.Mayor`), y nunca más arriba que 1 cm
      bajo el tope. Las barras salen cortadas (círculos) y los ganchos, que quedan sobre el corte, no se ven. #228
      pedía "barras, ganchos y estribo"; queda anotado para confirmar con Santiago (#218).
    - **Un paso más con columnas:** `Elegir · Alzado · Sección · Crear vistas`. El paso 2 muestra el alzado y la
      lista de barras; el 3, las dos secciones lado a lado, grandes; el 4 es el de crear vistas. Con vigas y viguetas
      siguen los 3 pasos de siempre. La sección usa la misma vista previa del paso 2 (no se vuelve a generar).

18. **También los estribos menores aparte** (2026-11-04, Luciano, al ver la sección: "todo bien, pero falta que
    muestre los estribos menores también"). Como `ResumirEstribos` en la viga: cada forma de estribo de la columna
    (tipo, forma y largo), primero el principal y después las demás de más a menos tramos, se dibuja aparte a la
    derecha, apiladas hacia abajo, cada una con su texto `N #3 L= … G= …` (cantidad en toda la columna). Si ninguna de
    una forma se ve en la planta, va solo el texto (Revit no dibuja el detalle de una barra que no se ve).

19. **En "Crear vistas" se elige cuál ver** (2026-11-04, Luciano: "ahí al final no se pueden ver las de traslapos ni
    varillas, propongo que donde dice los nombres de las vistas a la derecha se pueda dar clic y se vea esa"). Con
    columnas, los tres nombres de "Vistas que se crearán" son botones: el elegido se ve en grande a la izquierda (el
    alzado, con lo movido aplicado, o una de las dos secciones). Al entrar al paso queda elegido el alzado. Con vigas
    no cambia (la sección ya se ve al costado).

## Plan paso a paso

0. **Preparar la rama** — en `C:\Proyecto\addins-despiece-col`: `git fetch` y mezclar `origin/main` si avanzó;
   compilar y correr pruebas para partir en verde. — cómo se prueba: `dotnet test CQEING-Addins.sln` sin fallos.

1. **Diagnóstico en Revit real (regla del CLAUDE.md)** — comando temporal `ComandoDiagnosticoColumna` dentro de
   `#if DEBUG` en `addins/despiece/Interfaz/`, que sobre una columna elegida (de `PROY_JULIO_BETO`, generada con el
   add-in de columnas) escribe en el registro y deja vistas de prueba. Medir y anotar en Hallazgos:
   - a. La pila: cuántos elementos, ejes que la cruzan, ancho y alto de la sección, niveles.
   - b. Vista de alzado: `ViewSection.CreateSection` vertical a través de la columna; qué `RightDirection` da.
   - c. `RebarBendingDetail.Create` con clave `0` en una barra **vertical**: qué rotación la deja parada y alineada
     a su posición real. Igual con un estribo.
   - d. **Sección superior (horizontal):** probar si `CreateSection` acepta mirar hacia abajo; si no, una planta
     estructural propia con rango de vista y recorte, o un callout. Elegir la que dé una vista real, limpia y con
     `SetCutBackgroundPatternId`.
   - e. Grupo A/B (decisión 4): leer el registro de la columna, calcular A/B por el anillo y comparar con las
     alturas de los traslapos de cada varilla. Deben coincidir en todas.
   - f. Cotas: `ComputeReferences` sobre las caras de la columna en la sección horizontal.
   - cómo se prueba: valores y capturas en este plan (`docs/planes/imagenes/despiece-col-*.png`). **Al terminar se
     actualizan Hallazgos y Decisiones antes de seguir.**

   - **Estado:** hecho (31-oct a 03-nov), hallazgos 6 a 23. El comando se queda hasta el paso 2 (cambio del
     03-nov): su lectura de varillas y del registro pasa a `LectorColumnas`, y ahí se borra.

2. **Leer columnas y agrupar iguales (#226, sin Revit primero)** — archivos:
   - `calculo/CQEING.Despiece.Calculo/AgrupadorIguales.cs`: `EsColumna` en `ElementoLeido` (decisión 8); en la firma,
     una columna no se compara "al revés" (abajo y arriba no son intercambiables); nombre `Col A2-B2 - (0.40x0.40)`.
   - `calculo/CQEING.Despiece.Calculo/GruposTraslapo.cs` (nuevo): A/B por el anillo y por la altura de los traslapos
     (decisión 4), sin Revit.
   - Pruebas primero, en `CQEING.Despiece.Calculo.Tests/`: A2 y B2 iguales → `Col A2-B2 - (0.40x0.40)`; C1 distinta →
     sola; una viga y una columna con el mismo acero no se juntan; las 8 varillas del hallazgo 15 dan A/B
     intercalado por las dos fuentes; sin registro, por altura. Las pruebas de vigas existentes no se tocan.
   - `Revit/Columnas/LectorColumnas.cs`: pilas por posición en planta (5 cm, como `AgruparPilas`; de uno o varios
     elementos, hallazgos 6 y 13), sección = la menor de la pila (el dado más ancho no cuenta), ejes por el cruce
     (`C2`), barras con su posición en la altura, varillas y registro. Al terminar se borra el comando de diagnóstico.
   - cómo se prueba: `dotnet test` en verde, sin tocar las pruebas de vigas.

3. **Elegir la columna en la interfaz (#226)** — archivos:
   - `web/pasos.js`: activar "Columnas" (`k: 'columna'`) como se activó "Viguetas"; `delTipo` con los tres tipos;
     textos con "columna"; sin botón 3D para columnas (decisión 11); el arrastre queda igual.
   - `Revit/GeneradorPlanta.cs`: desde `main` hace una planta por nivel; suma las cajas de las columnas estructurales
     que se ven en ese nivel (y las incluye en el recorte), con las columnas visibles en la copia (decisión 13).
   - `Interfaz/PuenteDespiece.cs`: lee las columnas con `LectorColumnas` y las agrupa junto con las vigas; cada
     grupo lleva `esColumna` y, en `vigas`, los ids de todos los elementos de sus pilas. `previa` y `crearVistas` de
     una columna responden con el aviso de la decisión 13 hasta el paso 4; `ver3d` no se atiende para columnas.
   - cómo se prueba: compilar contra 2027 y `dotnet test`; en Revit, con "Columnas", clic en una columna marca
     también sus iguales y el recuadro muestra `Col ...`; con "Vigas" y "Viguetas" todo sigue igual.

4. **Vista completa de la columna (#227)** — archivo nuevo `Revit/Columnas/GeneradorColumna.cs`, conectado en
   `PuenteDespiece` en lugar del aviso del paso 3 para la **previa** (crear vistas sigue con el aviso hasta el paso 6),
   con su capa movible (decisión 11), armado como dice la decisión 14. Pruebas primero en `calculo/` para lo que no
   necesita Revit (`EtiquetasEstribos.SinEncimar`: alturas de las etiquetas de estribos sin encimarse; `Movimiento`
   con `Dx`). Con lo aprendido en el paso 1: sección vertical a 1:15, detalle medio, colores coherentes, solo la columna, sus vigas y su acero,
   barras visibles (`SetUnobscuredInView`), una barra de cada tipo sacada al lado y alineada, etiqueta `TextNote`
   `4 #6 L= 6.50`, cotas en metros (tipo propio `CQ Despiece - metros`), longitud total, etiquetas de estribos por
   zona (`EtiquetaEstribos` de `LectorDespiece`), colores con `SetElementOverrides`. — cómo se prueba: vista previa
   en el paso 2 y comparación contra lo que Santiago describió.

5. **Sección superior, dos veces (#228)** — en `GeneradorColumna.cs`: planta propia (decisión 9),
   concreto gris, cotas de dimensiones, recubrimiento en texto (como en vigas, punto 18 de #217), estribo aparte sin
   etiqueta y con la medida del gancho. Duplicar: la primera con colores de traslapo A/B (decisión 3 y 4), la
   segunda con colores por varilla. — cómo se prueba: las dos secciones con las mismas cotas; A y B intercalados.

6. **Crear las vistas en el navegador** — `Crear` para columnas: 3 vistas por grupo (alzado + 2 secciones), con el
   nombre del grupo; si ya existen, no se crean (igual que vigas). — cómo se prueba: aparecen en el navegador con el
   nombre correcto; volver a crearlas no duplica. Detalle (04-nov, al empezar el paso):
   - **Nombres:** el alzado `Col 1-6 - (0.30x0.30)` (el del grupo); las secciones `Sección Col 1-6 - (0.30x0.30) -
     traslapos` y `… - varillas`, los mismos que ya muestra el paso 4. Sin grupos de traslapo, la de traslapos no se
     crea. Las comas y los paréntesis de los nombres los acepta Revit (lo prohibido es `\ : { } [ ] | ; < > ? ` ~`);
     la barra de la decisión 12 ya no se usa (decisión 15).
   - **`GeneradorColumna.Crear`**, como `GeneradorPrevia.Crear`: si ya existe alguna de las vistas con esos nombres
     (de cualquier tipo de vista), no crea nada; si no, en una transacción que se confirma, con los tipos propios
     definitivos (`TiposPropios.Crear(doc, true)`), el alzado con lo movido aplicado y las secciones.
   - **`PuenteDespiece`:** `crearVistas` de una columna va a `GeneradorColumna.Crear` (se quita el aviso de la
     decisión 13) y responde con la lista de nombres creados (`nombres`); la página la muestra. `irAVista` de una
     columna recibe cuál (`alzado`, `traslapos` o `varillas`) y busca la vista por nombre entre todas las vistas.
   - **`pasos.js`:** el mensaje "Vistas creadas" usa `nombres` si viene; en "Ver en Revit" de una columna, tres botones
     (Alzado, Traslapos, Varillas).

7. **Cierre** — build sin advertencias, `dotnet test CQEING-Addins.sln`, deploy, prueba completa en Revit, Estado de
   este plan, comentario en #226–#228 con capturas. Publicar versión solo si Luciano lo pide (etiqueta
   `despiece-vX.Y.Z`, skill `publicar-version-addin-cqeing`).

## Cómo probarlo

```
cd C:\Proyecto\addins-despiece-col
dotnet build addins/despiece/CQEING.Despiece.csproj -c Debug -p:RevitVersion=2027
dotnet test CQEING-Addins.sln
Get-Process -Name Revit -ErrorAction SilentlyContinue   # debe salir vacío antes de desplegar
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin despiece -Revit 2027
```

1. Abrir `PROY_JULIO_BETO`, abrir Despiece, elegir "Columnas".
2. Clic en una columna con acero generado por el add-in de columnas: se marcan sus iguales.
3. Paso 2: vista previa del alzado y de las dos secciones.
4. Paso 3: crear vistas; revisar nombres y contenido en el navegador.
5. Repetir con una columna única y con una de sección distinta.
6. Abrir el despiece de una viga y confirmar que no cambió nada.

## Estado

- [x] Plan revisado con Luciano (31-oct): decisiones 1 a 3 aprobadas; 4 ajustada a su pregunta; 7 por su pedido.
- [x] Paso 0 (31-oct): rama al día con `origin/main`; `dotnet test CQEING-Addins.sln` en verde (1408 pruebas).
- [x] Paso 1: diagnóstico en Revit 2027 (31-oct a 03-nov), tres corridas: `PROY_JULIO_BETO` (columna sin acero) y
  `Prueba 08072026` (columna de ejes 1 y 6). Resultados en hallazgos 6 a 23 y capturas en `imagenes/despiece-col-*`.
- [x] 03-nov: mezclado `origin/main` (eec09a6, avance directo, sin conflictos); decisiones 8 a 11 por lo que trajo.
- [x] Paso 2 (03-nov): `GruposTraslapo.cs` y `EsColumna` en `AgrupadorIguales.cs` (calculo), con 11 pruebas nuevas en
  `AgrupadorColumnasTests.cs` y `GruposTraslapoTests.cs` (primero fallaron, luego en verde; las de vigas sin tocar);
  `Revit/Columnas/LectorColumnas.cs`. Borrado el comando de diagnóstico y su botón. `dotnet test CQEING-Addins.sln`:
  1479 pruebas en verde; compila sin advertencias (contra 2026: el DLL de 2027 estaba tomado por Revit abierto).
  `LectorColumnas` todavía no se llama desde ningún lado: se conecta en el paso 3 y ahí se prueba en Revit.
- [x] 04-nov: Luciano decidió que columnas sí lleva arrastre pero no 3D (decisión 11) y aprobó `6/1` (decisión 12).
- [x] Paso 3 (04-nov): implementado. `pasos.js` con "Columnas" activo, sin botón 3D para columnas;
  `GeneradorPlanta.cs` muestra las columnas en la copia y suma sus cajas (el recorte ahora también las abarca);
  `PuenteDespiece.cs` lee `LectorColumnas`, agrupa vigas y columnas juntas, manda `esColumna` y los ids de las pilas,
  y responde con el aviso de la decisión 13 a `previa`/`crearVistas` de una columna. Compila contra 2027 sin
  advertencias, `dotnet test CQEING-Addins.sln`: 1479 en verde, desplegado en 2027. 1ª prueba en Revit: no cargó
  (hallazgo 24); corregido y desplegado de nuevo. **2ª prueba (04-nov 07:50, `Prueba 08072026`): carga y la columna
  se elige** (Luciano). La traza muestra 3 barras sin caja (810012, 810040, 810409) y ninguna columna saltada; sin
  errores nuevos en `errores.log`. **Pendiente:** la regresión de vigas (ese modelo no tiene vigas con acero): se
  hace en `PROY_JULIO_BETO` con la viga 392698 en el cierre (paso 7).
- [x] Paso 4 (04-nov): implementado como dice la decisión 14. `Revit/Columnas/GeneradorColumna.cs` (nuevo);
  `EtiquetasEstribos.cs` y `Dx` en `Movimiento.cs` (calculo) con 5 pruebas nuevas (`EtiquetasEstribosTests`,
  `MovimientoTests`; primero fallaron, luego en verde); en `GeneradorPrevia.cs` solo `private` → `internal` de los
  ayudantes listados y `horizontal` en la capa; `PuenteDespiece.cs` manda la previa de columnas a `GeneradorColumna`
  (crear vistas sigue con aviso) y lee `dx`; `pasos.js` arrastra a los lados si la capa dice `horizontal`. No se usó
  `RebarBendingDetail.GetPosition` (la documentación dice "relativa a su vista"): se guarda el punto de creación,
  como en la viga. Compila contra 2027 sin advertencias; `dotnet test CQEING-Addins.sln`: 1484 en verde; desplegado
  en 2027. 1ª vista en Revit (04-nov 08:10): se ve bien; se corrigieron los ganchos que no se dibujaban (hallazgo
  25), las piezas traslapadas que salían separadas (hallazgo 27, ahora en líneas con la medida del traslapo) y el
  zoom (hallazgo 28, `Visor.limitar`). Issue #245 por los redondeos (hallazgo 26). Compila sin advertencias (contra
  2026: Revit abierto); pruebas de despiece en verde (82). Desplegado en 2027 (04-nov).
- [x] 04-nov: nombre de la columna con la marca de ubicación de Revit (decisión 15): `NombreColumna.cs` (calculo),
  coma entre columnas en `AgrupadorIguales.Nombre`, `LectorColumnas.Nombre`. Pruebas de `AgrupadorColumnasTests`
  ajustadas al formato nuevo y 6 nuevas para la marca (fallaron primero, luego en verde). Ese mismo día, a pedido de
  Luciano, la distancia al eje entre paréntesis se deja (dos pruebas cambiadas: fallaron, luego en verde; 88 en
  verde) y se desplegó de nuevo. `dotnet test
  CQEING-Addins.sln`: 1490 en verde; compila contra 2027 sin advertencias; desplegado en 2027. Visto en Revit
  (04-nov 16:24): en la columna de los ejes 1 y 6 todo bien.
- [x] 04-nov: zonas de estribos (hallazgo 29): `ZonasEstribos.cs` con 6 pruebas (`ZonasEstribosTests`, fallaron
  primero, luego en verde; 94 en verde) y `EtiquetarEstribos` las usa; traza de varillas, líneas y zonas (hallazgo
  30). Al probarlo falló por `IncludeFirstBar` en estribos sueltos (anotado en el hallazgo 29, corregido). Con la
  traza de esa corrida se encontró la causa del hallazgo 30 (hallazgo 31): `CadenasVarillas.cs` con 5 pruebas
  (`CadenasVarillasTests`; se comprobó que fallaban sin el código, luego en verde; 99 en verde) y
  `LectorColumnas.Varillas` lo usa. `dotnet test CQEING-Addins.sln` en verde (5 proyectos, 1501 pruebas); compila
  contra 2027 sin advertencias; desplegado en 2027. Visto en Revit (04-nov 17:10, columna 781574): las barras ya
  salen bien y todas muestran su gancho en la captura (el hallazgo 32 puede haberse resuelto al elegir bien entre las
  iguales; se confirma con la traza de la próxima corrida); los estribos salieron peor (hallazgo 33), corregido
  agrupando por forma. `dotnet test CQEING-Addins.sln` en verde; compila contra 2027 sin advertencias; desplegado en
  2027 (04-nov, tarde). Visto en Revit: estribos bien. Issue #256 por la separación 0.13/0.14 del generador de
  columnas. Zoom (hallazgo 34): diagnóstico temporal en la traza, causa encontrada, límite cambiado a una franja fija
  de 80 px; probado fuera de Revit con 1800 × 7544 px (el punto bajo el cursor no se mueve tras 40 pasos de pellizco)
  y desplegado en 2027. 2ª prueba: seguía igual; causa real encontrada con la traza (la capa movible queda fuera de la
  imagen exportada); `Visor` ahora usa los límites de la imagen más la capa (`limitesCapa`). Probado fuera de Revit
  con 6 barras fuera de la imagen ("Todo" las muestra, el zoom queda a 4 px del cursor, el arrastre llega a la
  última) y desplegado en 2027. Probado por Luciano con el touchpad (04-nov): "ya todo bien". Borrado el diagnóstico
  temporal (`tipo: 'traza'` en `pasos.js` y `PuenteDespiece.cs`).
- [x] **Paso 4 cerrado (04-nov)**, probado en Revit con la columna de los ejes 1 y 6 y la circular 781574. Commit en
  la rama `columnas/despiece-226-228` a pedido de Luciano (sin push). Quedan issues #245 (largos no redondos) y #256
  (estribos a 0.13 en vez de 0.14) para el add-in de columnas, y la regresión de vigas para el paso 7.
- [x] Paso 5 (04-nov): implementado como dice la decisión 16. `Revit/Columnas/SeccionColumna.cs` (nuevo);
  `GeneradorColumna.Generar` exporta las dos secciones; `PreviaGenerada` suma `SeccionVarillas` (vacía en la viga);
  `PuenteDespiece` manda `seccionVarillas`; `pasos.js` muestra las dos con título en la columna (la viga igual). En
  `GeneradorPrevia.cs` pasan a `internal` `Gancho`, `Cadena` y `Caras`. Miembros nuevos de la API comprobados en la
  documentación de 2027 (`SetCutForegroundPatternVisible`, `SetSurfaceForegroundPatternVisible`, `ViewPlan.Create`,
  `PlanViewRange.SetOffset/SetLevelId`, `TextNote.AddLeader`, `Rebar.SetUnobscuredInView`). Compila contra 2026 sin
  advertencias (Revit abierto). Desplegado y visto en Revit (04-nov 19:38): funciona; Luciano pidió un paso propio
  para la sección y sin ganchos (decisión 17). Hecho: `SeccionColumna.Corte` (debajo de los ganchos) y en
  `pasos.js` `PASOS` con 4 pasos para columnas y `secciones(g)` (las dos lado a lado). La pantalla nueva se probó
  fuera de Revit (página de prueba en `127.0.0.1`). Desplegado y visto en Revit: "todo bien, pero falta que muestre
  los estribos menores también" (decisión 18): `SacarEstribo` apila cada forma de estribo. En "Crear vistas" los
  nombres se pueden elegir para ver cada vista (decisión 19, `vistaFinal` en `pasos.js`); probado fuera de Revit
  (clic en cada nombre cambia la vista grande). Desplegado y visto en Revit (04-nov): "ya todo bien".
- [x] Paso 6 (04-nov): implementado como dice su detalle en el plan paso a paso: `GeneradorColumna.Crear`,
  `Nombres` y `VistaCreada`; `PuenteDespiece` (`crearVistas` y `irAVista` de columnas; se quitó el aviso de la
  decisión 13); `pasos.js` (mensaje con `nombres`, "Ver en Revit" con Alzado, Traslapos y Varillas). Compila contra
  2026 sin advertencias (Revit abierto). Además, números de cota a 2.5 mm (hallazgo 35, `ConTextoLegible` en
  `GeneradorPrevia.cs`) y largo de los ganchos del estribo aparte en texto, como en la viga (hallazgo 36). Probado
  por Luciano en Revit 2027 (04-nov, "ya todo bien"): cotas legibles, crear las vistas, volver a crear sin duplicar e
  ir a cada vista desde "Ver en Revit".
- [~] Paso 7 (cierre, 04-nov): Luciano probó todo en Revit 2027 ("ya todo lo probé"). Dice que los números de las
  cotas **siguen pequeños** y dejó una issue aparte (#261, "números de las etiquetas más grandes en vigas y
  columnas"); el hallazgo 35 queda como mejora parcial. Commit, mezcla de `origin/main` (solo cambió
  `CQEING-Addins.sln`, sin conflictos en el despiece), push y PR a pedido de Luciano.
  **Pendiente:** comentar en #226–#228 con capturas; confirmar con Santiago (#218) la sección sin ganchos (decisión
  17) y si A/B es lo que él llama barra interna/externa; #245 y #256 quedan para el add-in de columnas.
- [x] Probado en Revit 2027 por Luciano (04-nov), columnas de los ejes 1 y 6 y circular 781574 de `Prueba 08072026`.
