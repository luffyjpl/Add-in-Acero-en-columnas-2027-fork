# Plan: observaciones 14 y 15 de Santiago al despiece (10-11-2026)

## Contexto

- Santiago dejó dos filas nuevas en su planilla de revisión (capturas en `docs/planes/imagenes/2026-11-10-santiago-14.png`
  y `…-15.png`), más lo que Luciano agregó al pasarlas (10-11):
  - **14** (sección de columna): "grapas verticales no aparecen, acomodar las cotas, quitar la cantidad de las varillas
    en la leyenda y también el nombre piel, añadir la longitud de la grapa".
  - **15** (viga `V A-B (0.50x0.90)`): "rectificar la forma de la sección de la viga, arreglar la visual de las varillas".
  - Luciano: el nombre `piel` es el de la leyenda de la sección de **viga**; las grapas ya están resueltas (decisión 44
    del plan de #317, sin commitear todavía); en su prueba la grapa sí lleva su medida y en la de Santiago no; la cota
    **vertical** del estribo aparte tiene las rayitas de los lados hacia afuera y deben ir hacia el estribo.
  - Luciano, sección de viga: "sigue apareciendo incompleta" (una losa o columna le come un pedazo). Que salga completa,
    como si la viga no estuviera atravesada, solo en el dibujo.
  - Luciano, varillas de la vista larga: la separación que usamos está bien, pero hay casos donde una barra atraviesa a
    otra. Las distancias por defecto tienen que ser lo más dinámicas posible: lo más cerca posible sin tocar a las otras.
  - Luciano: que la cota de los ejes y la de abajo también se puedan mover en el paso 2, en la viga y en la columna.
  - Luciano, columnas: textos más cerca de sus barras; barras acomodadas solas en buenas posiciones (que sirva para la
    mayoría de las columnas sin mover nada); cuando a la derecha aparecen vigas, las barras salen encima de ellas: que
    salgan justo después.
- Fuera de alcance: el despiece de viguetas; el `Tipo I` de la referencia de Santiago; las vigas recortadas por la losa en
  el add-in de acero de vigas (lo tiene Juan).
- Entorno: Revit 2027 y 2026, .NET 10. Worktree `C:\Proyecto\addins-317`, rama `despiece/grapas-seccion` (sale de
  `4bd90aa`; `origin/main` avanzó a `3e63f59` sin tocar `addins/despiece`, se mezcla antes del PR). Modelo de prueba:
  "edificio tevening" (`Col B-3`, `Col C-2`, `V C`, `V A-B`).

## Criterios de aceptación

- [ ] Sección de columna: se ven las grapas de las caras X y de las Y del primer paquete (decisión 44 del plan de #317).
- [ ] Leyendas (viga y columna): solo color y diámetro (`#6`), sin cantidad y sin `piel`; un diámetro aparece una vez.
- [ ] Estribo aparte (viga y columna): las líneas de referencia de sus cotas van del estribo a la línea de cota, no hacia
      afuera.
- [ ] La grapa aparte lleva su cota de largo también en la columna de la captura de Santiago (o se sabe por qué no).
- [ ] Sección de viga: el contorno completo de la viga (ancho × alto de la viga) aunque una losa o columna la corte.
- [ ] Vista larga de viga: ninguna barra, gancho ni texto se cruza con los de otra fila, y las filas quedan lo más
      pegadas posible.
- [ ] Alzado de columna: las barras no caen encima de las vigas que asoman a la derecha; arrancan justo después de ellas
      solo a la altura de esas vigas y, donde no hay viga, siguen pegadas a la columna.
- [ ] Alzado de columna: las etiquetas de las barras quedan pegadas a su barra.
- [ ] Paso 2: las cotas de la vista larga (ejes abajo, total arriba) y las del alzado de columna (elementos, total) se
      arrastran; lo movido sale igual en "Crear vistas".
- [ ] `dotnet test CQEING-Addins.sln` sin fallos y compilación sin advertencias.
- [ ] Probado en Revit 2027 (y 2026 si Luciano lo tiene abierto).

## Hallazgos del código y de Revit real

1. **Leyenda de viga**: `GeneradorPrevia.Seccion` (`addins/despiece/Revit/GeneradorPrevia.cs` ~1040-1058) agrupa por
   (diámetro, piel, nombre) y escribe `#5 piel` en la fila de la piel. **Leyenda de columna**: `SeccionColumna.Crear`
   (`Revit/Columnas/SeccionColumna.cs`, ~133-155) escribe `N #k`.
2. **Cotas del estribo aparte**: `GeneradorPrevia.AcotarEstribo` (~1157) usa `CadenaPorPlanos` (~1356), que pone cada
   plano de referencia **centrado** en la línea de cota (±5 cm a cada lado). La línea de referencia que dibuja Revit
   asoma hacia los dos lados y se ve como si apuntara hacia afuera. Hay que probar en Revit si con el plano entre la
   línea de cota y el estribo la línea sale hacia el estribo.
3. **Medida de la grapa**: la misma `AcotarEstribo` acota el ancho de una forma chata. Si `CadenaPorPlanos` falla,
   queda en el registro `estribos: ancho de <id>: cota en cadena no se pudo`. No sabemos todavía por qué en la de
   Santiago no salió (puede ser otra versión instalada o esa columna).
4. **Sección de viga incompleta**: la viga se dibuja con su corte real (`gris` sobre `viga.Vigas`, ~999-1009). Si una
   losa la corta (viga 193085 y losa 203035, hallazgo del plan de #317), el corte de Revit ya viene sin ese pedazo. En
   la vista larga esto se resolvió con `ContornoCompleto` (~1582, decisión 40 de #317): la viga invisible y su contorno
   en líneas negras.
5. **Filas de barras de la viga**: `ColocarFilas` (~883) apoya cada fila en la anterior usando solo el **mayor** de lo
   que sobresale de las dos (decisión 39 de #317). Si el gancho de una fila cae en la misma x que el texto o el gancho
   de la otra, se cruzan: no mira dónde está cada cosa a lo largo de la viga, y solo mira la fila anterior.
6. **Barras de la columna**: `GeneradorColumna.Sacar` (~376-488) reparte las líneas en carriles con
   `AcomodadorFilas.Acomodar` desde la cara derecha de la columna (`InicioSacadas`, decisión 13 de #317). No sabe de las
   vigas que se dejan ver a la derecha (`Lado` = 0.30 m): por eso caen encima.
7. **Etiquetas de la columna**: van giradas en `Izquierda + Recto + AireNota` (~479). Hay que medir en Revit cuánto
   aire queda de verdad entre la barra y la caja del texto (la nota tiene su propio margen).
8. **Lo que se arrastra en el paso 2**: `Separar` (viga ~534, columna ~853) oculta de la imagen lo movible y lo manda a
   la página (`Capa`, ~193); `capaMovible` en `web/pasos.js` (~585) lo dibuja y `arrastrar` guarda `{dz, tx, tz, dx}`
   por pieza. Las cotas hoy quedan en la imagen, fijas: `Cotar`, `CotarColumnas` (viga) y `Cotar` (columna).

## Decisiones

1. **Seguimos en la rama `despiece/grapas-seccion`** (2026-11-10, Claude): ya tiene el arreglo de las grapas (punto 14)
   sin commitear; todo va en un solo PR.
2. **Leyenda: un renglón por diámetro, sin cantidad ni `piel`** (2026-11-10, Santiago/Luciano). La piel del mismo
   diámetro que otra barra ya no lleva fila propia.
3. **Sección de viga completa como en la vista larga** (2026-11-10, Claude, a confirmar con Luciano): la viga de Revit
   invisible y su contorno completo en líneas negras, con el ancho y el alto de la viga sin cortes
   (`FamilyInstance.GetOriginalGeometry`, API pública: la geometría antes de uniones y cortes). Un relleno gris taparía
   las barras: en Revit una región rellena siempre se dibuja encima del modelo. Queda **sin gris**, como la vista larga.
   Confirmado por Luciano el 10-11: "blanco está bien".
4. **Un solo acomodador "lo más cerca sin tocar"** (2026-11-10, Claude): clase nueva en `calculo` con pruebas. Cada
   grupo (una fila de la viga, una línea de la columna) es un conjunto de rectángulos (tramo, ganchos, etiqueta, medidas)
   que se mueven juntos; cada grupo se coloca en el primer lugar desde la viga/columna hacia afuera donde ninguno de sus
   rectángulos toca a los ya puestos ni a los obstáculos (las vigas de la derecha en la columna). Mira **todos** los ya
   puestos, no solo el anterior. No usa medidas de ninguna viga ni columna en particular.
5. **Cotas que se arrastran** (2026-11-10, Luciano: "la cota de los ejes y la de abajo"): en la viga se arrastran
   arriba/abajo; en la columna, a los lados. Cada cota se mueve entera (línea, rayitas y números). En Revit se mueven con
   `ElementTransformUtils.MoveElement`. Los ejes (círculos) no se mueven. Confirmado por Luciano el 10-11: la cota se
   mueve entera (línea, rayitas de los extremos y números), arriba/abajo en la viga y a los lados en la columna.
6. **Etiquetas de columna pegadas**: se corrige con lo que se mida en Revit (hallazgo 7), no a ojo.

## Plan paso a paso

1. **Grapas del primer paquete (punto 14)** — ya implementado (`SeccionColumna.cs`, `PrimerPaquete.cs` + pruebas).
   Se prueba en Revit con `Col B-3` y la columna de la captura. Registro: `sección de columna: primer paquete de … a …`.
2. **Leyendas** — viga (`GeneradorPrevia.Seccion`) y columna (`SeccionColumna.Crear`): un renglón por diámetro, texto
   `TextosDespiece.Nombre`. Prueba: `dotnet test` + ver las dos secciones.
3. **Cotas del estribo aparte** — `CadenaPorPlanos` recibe hacia dónde está el dibujo y hasta dónde llega: el plano va
   de la línea de cota hasta el borde del estribo. Se prueba en Revit; si la línea sigue saliendo hacia afuera, se anota
   aquí y se prueba el ajuste de las líneas de referencia del tipo de cota.
4. **Medida de la grapa** — en Revit, previa de la columna de la captura (0.55 × 0.55, `30 #6`, grapa `412`): leer el
   registro `estribos: ancho de …`. Se anota el hallazgo y se arregla según lo que diga.
5. **Sección de viga completa** — `GeneradorPrevia.Seccion`: rectángulo de `GetOriginalGeometry` (transformado al
   modelo) cortado en el plano de la sección; viga invisible y contorno en líneas negras. El registro anota
   `sección: contorno completo b × h`. Se prueba con `V A-B` (losa encima) y `V C`; las cotas siguen siendo las de la viga.
6. **Acomodador "lo más cerca sin tocar"** — `calculo/CQEING.Despiece.Calculo/Empacador.cs` + `EmpacadorTests.cs`,
   pruebas primero: dos grupos sin cruce en x se pegan a la base; un gancho debajo de una etiqueta empuja; un grupo no
   choca con uno de dos filas atrás; un obstáculo empuja solo a lo que está a su altura; el hueco se respeta.
7. **Viga con el acomodador** — `ColocarFilas`: cada fila arma sus rectángulos (tramo recto, cada gancho con su medida
   girada, etiqueta, medidas de traslapo) y el acomodador da la línea. Se prueba con `V A-B` (la de la captura 15) y las
   vigas de las pruebas del 09-11.
8. **Columna con el acomodador** — `Sacar`: las líneas pasan por el acomodador con las vigas de la derecha como
   obstáculos (de la cara de la columna hasta `der + Lado`, a la altura de cada viga). Se mantiene el orden de los
   grupos de gancho (#277) con su hueco.
9. **Etiquetas de columna más pegadas** — medir en Revit (registro con la caja de la nota y la cara del tramo recto) y
   corregir `AireNota`/el punto de la etiqueta con el valor medido.
10. **Cotas que se arrastran** — Revit: `Separar` (viga y columna) mide cada cota creada (línea, posiciones de cada
    marca, números) y la oculta de la imagen; `AplicarMovimientos` las mueve con `MoveElement` (clave `cota-…`). Página:
    `capaMovible` dibuja las cotas (línea, rayitas a 45°, líneas de referencia, números) y `arrastrar` las mueve en un
    solo sentido. Se prueba en el paso 2 y con "Crear vistas".
11. **Cierre** — `dotnet test CQEING-Addins.sln`, compilación sin advertencias, mezclar `origin/main`, prueba en Revit,
    Estado.

## Cómo probarlo

```
cd C:\Proyecto\addins-317
dotnet build addins/despiece/CQEING.Despiece.csproj -c Debug -p:RevitVersion=2027
dotnet test addins/despiece/calculo/CQEING.Despiece.Calculo.Tests/CQEING.Despiece.Calculo.Tests.csproj
dotnet test CQEING-Addins.sln
Get-Process -Name Revit -ErrorAction SilentlyContinue   # tiene que no devolver nada
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin despiece -Revit 2027
```

En Revit, modelo "edificio tevening":

1. `Col B-3` y la columna 0.55 × 0.55 de la captura: secciones con todas las grapas del primer paquete, leyenda `#6`
   sin cantidad, estribo y grapa aparte con sus cotas hacia el dibujo.
2. `V A-B (0.50x0.90)`: sección completa, leyenda sin `piel`, vista larga sin barras cruzadas.
3. Alzado de columna con vigas a la derecha: barras después de las vigas a esa altura, etiquetas pegadas.
4. Paso 2: arrastrar las cotas de la viga y de la columna; "Crear vistas" y comparar con la previa.

## Estado

- [x] Plan escrito (10-11); Luciano pidió empezar en el mismo mensaje.
- [x] Paso 2 (10-11): leyendas de viga (`GeneradorPrevia.Seccion`) y columna (`SeccionColumna.Crear`) con un renglón
      por diámetro (`#6`), sin cantidad ni `piel`.
- [x] Paso 3 (10-11): `CadenaPorPlanos` con `haciaDibujo`/`hastaDibujo`: el plano va desde 0.3 de la distancia hasta el
      borde del estribo (`PlanoDesde`). `AcotarEstribo` lo usa en el ancho (hacia arriba) y en el alto (hacia la
      derecha). **Pendiente ver en Revit** hacia dónde dibuja Revit la línea de referencia.
- [x] Paso 5 (10-11): `GeneradorPrevia.SeccionCompleta` (viga invisible + contorno de `GetOriginalGeometry`) y
      `Acotar` acota ese contorno contra planos cuando existe. Registro: `sección: contorno completo b × h … la caja de
      Revit …`. **Pendiente ver en Revit** con `V A-B`.
- [x] Compila sin errores; pruebas del despiece 148/148. Desplegado en 2027 y 2026 desde `C:\Proyecto\addins-317`.
- [x] Paso 6 (10-11): `Empacador` + `EmpacadorTests` (13 pruebas: pegado a la base, gancho hacia la viga, hueco a lo
      largo y hacia afuera, solo empuja lo que está a la misma altura, el gancho sobre el texto de la otra fila, mira
      todos los ya puestos, en orden, hueco que dejan otros, obstáculo, mínimo, base). Despiece 161/161.
- [x] Paso 7 (10-11): `GeneradorPrevia.Pila` guarda todo lo puesto de cada lado (hacia afuera desde la cara de la viga)
      y empieza con los obstáculos: el asomo de las columnas (`apoyos`) y las etiquetas de los estribos (su caja real).
      `ColocarFilas` arma las partes de cada fila con `Partes` (cada tramo de la barra con 1 cm de grueso, la etiqueta,
      la medida girada de cada gancho) más las medidas de traslapo, y la línea la da `Empacador` (en orden, al menos
      `HuecoFilas` más afuera que la anterior). Reemplaza a las decisiones 30, 39 y 42 del plan de #317. Se borraron
      `SeparacionViga` y `EspacioCotas`. Registro: `longitudinal: fila de N a X de la viga (arriba|abajo), P partes`.
- [x] Paso 8 (10-11): `GeneradorColumna.Sacar` usa `Empacador` por grupo de gancho (sin orden: una línea corta entra en
      un hueco) con las vigas de la derecha como obstáculos (`VigasALaDerecha`: de la cara hasta `Lado + InicioSacadas`,
      entre su z mínima y máxima). Registro: `columna: vigas a la derecha …`.
- [x] Paso 9, medición (10-11): el registro anota `columna: etiqueta de <id>: aire X m, caja de Y m`. Falta corregir
      con lo medido.
- [x] Compila sin errores; desplegado en 2027 y 2026.
- [x] Prueba de Luciano del 10-11 (09:42-10:11, Revit 2027, `traza.log` e imágenes exportadas en
      `%TEMP%\…\CQEING\despiece\`):
  - Paso 1 ✅ `Col B-3`: `primer paquete de 0,064 a 0,102, corte a 0,112`; en la imagen se ven las grapas de las caras X
    y de las Y.
  - Paso 2 ✅ leyendas `#7, #6, #4, #3` (viga) y `#7, #6, #3` (columna).
  - Paso 4 ✅ la grapa aparte sale con su cota `0.48` (`estribos: ancho de 446055: cota en cadena de 0 tramos` = una cota
    de un solo tramo, creada). En la captura de Santiago no salía: tenía una versión anterior.
  - Paso 5 ✅ `V A-B`: `contorno completo 0,500 × 0,900`, cotas `0.90` y `0.04 | 0.82 | 0.04`, en blanco.
  - Paso 3 ❌ las líneas de referencia del estribo aparte siguen asomando solo hacia afuera. Causa: con el tipo de cota
    normal ("hasta el elemento") Revit deja un espacio entre lo acotado y la línea de referencia, y el estribo está a
    `CotaDetalle` (0.08 × Factor), más cerca que ese espacio: solo queda visible lo que sigue más allá de la línea de
    cota. Mover los planos no cambia nada. **Corrección**: tipo propio `CQ Despiece - metros estribo`
    (`TiposPropios.CotaEstribo`): `DIM_WITNS_LINE_CNTRL` = 1 (largo fijo desde la línea de cota hacia el elemento),
    `DIM_WITNS_LINE_EXTENSION_BELOW` = 2.4 mm y `WITNS_LINE_EXTENSION` = 0 (nada hacia afuera). Nombres de parámetros
    sacados de `RevitAPI.xml` 2027 (públicos). Pendiente de probar.
  - Paso 7: `V A-B` arriba, filas a 0.707 y 1.194 m de la viga; abajo a 0.30, 0.61, 1.08, 1.73 y 2.20 m. Los ganchos de
    cada fila caen en la misma x (todas terminan en la misma columna) y bajan ~0.45 m: para no cruzarse, cada fila queda
    a un gancho de la otra. Hay que verlo en la página (la imagen exportada no trae las barras sacadas).
  - Paso 9: la caja que da Revit para una nota girada no sirve para medir (0.402 m de ancho y "aire" −0.068 m en todas):
    hace falta una captura del alzado.
- [x] Segunda prueba de Luciano (10-11, 10:25-10:28; capturas de `V C`, `Col B-3` sección y alzado):
  - Paso 3 ✅ con `CotaEstribo`: en la imagen exportada (recorte ampliado) las líneas de referencia van de la cota hacia
    el estribo y hacia afuera solo queda la rayita inclinada.
  - Paso 7 ✅ en `V C` las filas quedan pegadas y sin cruces entre filas. Dentro de una fila, la medida del traslapo
    caía sobre la etiqueta (`3 #7 L= 2.60/3.001.34`, `2 #6 L= 2.64/2.991.54`): se corría con un ancho fijo de etiqueta
    (`MedioAnchoEtiqueta`). **Corrección**: se corre con el ancho real de cada etiqueta y de la medida (por su texto,
    `MedioAnchoTexto`), y si al correrse cae sobre otra etiqueta se vuelve a correr. Helper `TextoEtiqueta`; se borró
    `MedioAnchoTraslapo`. Pendiente de probar.
  - Paso 8 ✅ en `Col B-3` (vigas a la derecha en cada piso) las barras arrancan después de las vigas.
  - Paso 9: en la página, la etiqueta girada de la columna se dibujaba en el centro de la caja de Revit, que para una
    nota girada mide 0.40 m: quedaba ~0.2 m más a la derecha que en Revit. **Corrección**: `GeneradorColumna.Separar`
    usa el punto de la nota girada (`Coord`) más `AnchoNotaGirada / 2`. Pendiente de probar.
- [x] Tercera prueba (10-11, 10:36-10:38): etiquetas de columna pegadas a su barra ✅. La medida del traslapo ya no cae
      sobre la etiqueta pero queda tocándola: `MedioAnchoTexto` subestimaba el texto. Medido en la captura de `V C`
      (letra 2.0 mm, 1:50): `3 #7 L= 2.60/3.00` mide 1.09 m, 0.64 del alto de letra por carácter. **Corrección**: 0.55 →
      0.65 (también lo usan las filas de etiquetas de estribos sobre la viga). Pendiente de probar.
- [x] Paso 10 (10-11): cotas que se arrastran.
  - Revit: las cotas se crean ya corridas (no se mueven después): `GeneradorPrevia.Corrimiento(movible, clave,
    horizontal)`. Viga: claves `cota-abajo` (cadena de ejes de `Cotar` + cadena de caras de `CotarColumnas`, en la misma
    línea) y `cota-arriba` (largo total + nota `Ejes …`), con `Dz`; `AjustarEjes` alarga los ejes si las cotas se
    alejan. Columna: `cota-elementos` y `cota-total`, con `Dx`; los planos de referencia llegan hasta la más alejada.
  - Lo creado se junta en `CotasCreadas` (Abajo/Arriba) y `Separar` (viga y columna) lo mide con
    `GeneradorPrevia.MedirCota` (línea, una línea de referencia de 2.4 mm de papel y una rayita por marca, y cada número
    en `TextPosition` pasado a su centro) en `CotaMovible`, y lo oculta de la imagen. `Capa` lo manda como `cotas`.
  - Página (`web/pasos.js`): `capaMovible` dibuja cada cota en negro y la arrastra entera con `arrastrar(…, true, …)`
    (dz en la viga, dx en la columna); `limitesCapa` las cuenta.
  - Compila; `dotnet test CQEING-Addins.sln` en verde en lo que corrió (Despiece 161, Vigas 1156, Gestor 256…); la
    compilación de la solución falló solo al copiar la DLL de 2026 porque Revit 2026 estaba abierto. Pendiente de probar.
- [x] Paso 11 (10-11): con Revit cerrado, `dotnet build CQEING-Addins.sln --no-incremental`: 0 advertencias, 0 errores;
      `dotnet test CQEING-Addins.sln` en verde (Despiece 161, Vigas 1156, Gestor.Core 256, Gestor 37, Viguetas 72,
      Zapatas 92, CotasPlanta 16, Publicador 8). Prueba final de Luciano en Revit (10:51-10:54): "ya todo bien"
      (`imagenes/2026-11-10-prueba-final-vc.png`, `…-col-b3.png`): `1.34`/`1.54` separadas de su etiqueta, cotas que se
      arrastran, secciones completas. Se mezcla `origin/main` antes del PR.

### Criterios de aceptación

- ✅ Sección de columna con las grapas X e Y del primer paquete (paso 1, decisión 44 del plan de #317).
- ✅ Leyendas con solo color y diámetro, sin cantidad ni `piel` (paso 2).
- ✅ Cotas del estribo aparte con sus líneas de referencia hacia el estribo (paso 3, tipo `CotaEstribo`).
- ✅ La grapa aparte lleva su cota de largo (paso 4: en la de Santiago faltaba por tener una versión anterior).
- ✅ Sección de viga con el contorno completo, en blanco (paso 5, decisión 3).
- ✅ Vista larga sin cruces entre filas y lo más pegada posible (pasos 6 y 7; medidas de traslapo con el ancho real).
- ✅ Columna: barras después de las vigas de la derecha solo a su altura (paso 8).
- ✅ Columna: etiquetas pegadas a su barra (paso 9: la página usaba la caja inflada de la nota girada).
- ✅ Cotas que se arrastran en viga y columna, iguales en "Crear vistas" (paso 10).
- ✅ `dotnet test` en verde y compilación sin advertencias.
- ✅ Probado en Revit 2027 por Luciano.

### Pendiente

- Nada de este plan. Fuera de alcance: despiece de viguetas, `Tipo I`, vigas recortadas por la losa en el add-in de
  acero de vigas (Juan).
