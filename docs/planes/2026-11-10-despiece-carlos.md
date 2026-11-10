# Plan: observaciones del Ing. Carlos al despiece 0.7.0 (10-11-2026)

## Contexto

- Luciano probó el despiece 0.7.0 con el Ing. Carlos (10-11, tarde) y pasó estas observaciones, con capturas en
  `docs/planes/imagenes/2026-11-10-carlos-*.png`:
  1. **Líneas verdes punteadas** que salen desde la izquierda en la vista larga ya creada (`…-planos-verdes.png`): "no
     deberían aparecer".
  2. **Leyenda a la derecha** de la sección, para que ocupe menos espacio (`…-leyenda.png`); y, en general, que en las
     secciones (viga y columna, con estribos menores y demás) todo se acomode ocupando el menor espacio posible.
  3. **Cota y recubrimiento en la misma línea** en las secciones: hoy salen dos líneas (la cadena `0.04 | 0.42 | 0.04` y
     más afuera el total `0.50`).
  4. **Sección angosta**: los números chicos (`0.04`) tapan el borde de la viga y se enciman (`…-angosta.png`).
  5. **Una columna no muestra los estribos menores** en la sección.
  6. **Barras con aristas**: un borde negro fino alrededor de cada barra, en columnas y vigas (`…-aristas-hueco.png`).
  7. **Huecos en la columna** (en los nudos con vigas o losas, `…-aristas-hueco.png`): que se vea como si la columna
     estuviera completa.
- Fuera de alcance: lo que ya dejaron las versiones anteriores en proyectos donde se crearon vistas (los planos verdes
  ya creados se borran a mano; ver hallazgo 1).
- Entorno: Revit 2027 y 2026, .NET 10. Worktree `C:\Proyecto\addins-317`, rama `despiece/carlos-10-11` desde
  `origin/main` en `0ac4ac7` (ya con el despiece 0.7.0, PR #325). Modelo "edificio tevening".

## Criterios de aceptación

- [ ] Ninguna vista del despiece deja líneas verdes (planos de referencia) en otras vistas del proyecto.
- [ ] Secciones de viga y columna: la leyenda a la derecha de la sección y el conjunto (sección, leyenda, estribo y
      menores) ocupando el menor espacio posible.
- [ ] Secciones: una sola línea de cota por lado, con la cadena del recubrimiento y el total.
- [ ] Sección angosta: ningún número tapa el borde ni se encima con otro.
- [ ] La columna `189817` muestra sus estribos menores en la sección (o se sabe por qué no y se resuelve).
- [ ] Barras con un borde negro fino en las vistas de viga y columna (lo que Revit permita; ver hallazgo 6).
- [ ] La columna se ve completa en el alzado, sin huecos en los nudos.
- [ ] `dotnet test CQEING-Addins.sln` sin fallos y compilación sin advertencias.
- [ ] Probado en Revit 2027.

## Hallazgos del código y de Revit real

1. **Líneas verdes = planos de referencia del despiece** (10-11, leyendo el código). Para acotar contra algo que no es
   una cara, el despiece crea `ReferencePlane` (`CadenaPorPlanos`, `Acotar.Planos`, `CotarColumnas`, y `Cotar` de la
   columna) y los pinta de blanco **solo en su vista**. Un plano de referencia es del modelo: se ve en toda vista que lo
   corte. Los horizontales de la sección de la viga (a lo largo de la viga, en la cara de arriba y la de abajo) salen
   en la vista larga como las líneas de la captura. En la vista previa no se veían porque la vista larga se arma antes
   que la sección; con "Crear vistas" quedan los dos. También pueden aparecer en otras vistas del proyecto.
   Solución propuesta: acotar contra **líneas de detalle** (en blanco), que solo existen en su vista
   (`DetailCurve.GeometryCurve.Reference`, API pública). Hay que probar en Revit que la cota las acepte.
2. **Leyenda**: viga `GeneradorPrevia.Seccion` (debajo de la sección); columna `SeccionColumna.Crear` (debajo). El
   estribo aparte y los menores van apilados a la derecha (`ApilarEstribos`).
3. **Dos líneas de cota**: cadena a `CadenaSeccion` y total a `TotalSeccion` (viga, `Acotar`); igual en la columna
   (`SeccionColumna.Acotar`). En la vista larga ya hay dos cotas en la misma línea, una con los números arriba y otra
   abajo (`PasarTextoAbajo`, decisión 10 del plan de #266): se puede hacer lo mismo.
4. **Columna 189817 sin menores** (registro del 10-11, 13:26): `primer paquete de 0,083 a 0,083, corte a 0,093`, y las
   piezas `442838` y `442881` "no se ven en la vista": no pasan por el corte. `PrimerPaquete` toma el estribo y lo que
   aparece hasta 0.15 m más arriba; en esta columna los menores aparecen más arriba o no en el primer nivel. Falta saber
   sus alturas: se anotan en el registro.
5. **Hueco en los nudos** (código): cuando hay barras dibujadas giradas, `AbrirParaGiradas` oculta las vigas y dibuja su
   arranque gris solo a los lados de la columna, "sin pasar por ella"; el tramo de la columna dentro del nudo queda en
   blanco. En la vista larga de la viga eso se resolvió con el contorno completo (decisión 40 del plan de #317).
6. **Aristas**: las barras de la vista son líneas de color con grosor (`Pintar`, `SetProjectionLineWeight`). Las barras
   sacadas son detalles de plegado (`RebarBendingDetail`) o líneas de detalle. Hay que probar en Revit si con el nivel
   de detalle "Fino" la barra se dibuja con su espesor real (dos bordes) y se le puede dar relleno de color y borde
   negro. Para las sacadas, en la página el borde se dibuja fácil; en Revit hay que ver qué permite el detalle de plegado.

## Decisiones

1. **Rama nueva `despiece/carlos-10-11` desde `origin/main`** (10-11, Claude): la anterior ya se mezcló (PR #325).
2. **Cotas contra líneas de detalle, no planos de referencia** (10-11, Claude): si Revit las acepta (hallazgo 1).
3. **Una línea por lado: la cadena con sus números hacia la sección y el total con su número hacia afuera** (10-11,
   Claude). Un número que no entra en su tramo (`0.04`) va hacia afuera, al lado de su tramo, sin tocar el borde.
4. **Disposición de la sección** (10-11, Claude): sección a la izquierda, leyenda a su derecha arriba, y el estribo y
   los menores a la derecha en columnas que no pasan del alto del bloque de la sección (un menor que no entra abajo va a
   la columna siguiente). El reparto en columnas va en `calculo` con pruebas.
5. **Columna completa en el alzado** (10-11, confirmado por Luciano: "blanca con borde negro"): como la viga en la
   vista larga, la columna en blanco con su contorno completo en negro, de arriba abajo sin cortes; las vigas de los
   nudos siguen grises a los lados.
6. **Aristas solo en las barras de dentro** (10-11, Luciano): las barras que se ven dentro de la viga o la columna; las
   sacadas al costado quedan como están.

## Plan paso a paso

1. **Líneas de detalle en vez de planos** — prueba en Revit de una cota contra dos líneas de detalle; si sirve, se
   cambian `CadenaPorPlanos`, `Acotar.Planos`, `CotarColumnas` y `GeneradorColumna.Cotar`. Se comprueba creando vistas
   y mirando la vista larga (sin líneas verdes) y una planta.
2. **Menores de la columna 189817** — registro con la huella y la altura de cada pieza horizontal de las primeras;
   según lo que diga, se ajusta `PrimerPaquete` (con pruebas).
3. **Una línea de cota por lado** — viga y columna: cadena y total en la misma línea; números chicos hacia afuera.
4. **Leyenda a la derecha y disposición compacta** — clase de reparto en `calculo` + pruebas; viga y columna.
5. **Columna completa en el alzado** — contorno completo como `ContornoCompleto` de la viga.
6. **Aristas** — prueba en Revit del nivel de detalle "Fino" en el alzado y la vista larga; página: borde negro bajo
   cada barra sacada. Se anota lo que Revit permite.
7. **Cierre** — `dotnet test`, compilación sin advertencias, prueba en Revit, Estado.

## Cómo probarlo

```
cd C:\Proyecto\addins-317
dotnet build addins/despiece/CQEING.Despiece.csproj -c Debug -p:RevitVersion=2027
dotnet test CQEING-Addins.sln
Get-Process -Name Revit -ErrorAction SilentlyContinue   # tiene que no devolver nada
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin despiece -Revit 2027
```

En Revit: "Crear vistas" de `V C` y mirar la vista larga y una planta (sin líneas verdes); secciones de `V C`,
`Col B-3` y la columna `189817`; alzado de `Col B-3` (sin huecos, barras con borde).

## Estado

- [x] Plan escrito (10-11). Luciano eligió la columna blanca con borde negro y aristas solo en las barras de dentro.
- [x] Paso 1 (10-11): `GeneradorPrevia.ReferenciaEnVista` (línea de detalle con el estilo de `OST_InvisibleLines`, o en
      blanco si no existe; referencia `GeometryCurve.Reference`) reemplaza los cuatro `NewReferencePlane`
      (`CotarColumnas`, `Acotar.Planos`, `CadenaPorPlanos`, `GeneradorColumna.Cotar`). Pendiente de probar en Revit.
- [x] Paso 2, diagnóstico (10-11): el registro anota `sección de columna: primeras piezas z1; z2… de N huellas` (la
      primera altura de cada huella). Pendiente leerlo con la columna `189817`.
- [x] Paso 5 (10-11): `GeneradorColumna.ContornoCompleto`: elementos de la columna invisibles y contorno negro, cada
      elemento hasta donde empieza el siguiente. Registro: `columna: contorno completo con N líneas de M elementos`.
- [x] Paso 6 (10-11): vista larga y alzado en nivel de detalle Fino; `Colorear(…, aristas: true)` →
      `PintarConArista` (relleno en el color del diámetro, líneas negras de grosor 1). Las sacadas no cambian.
      Pendiente ver en Revit si en Fino la barra sale con su grosor real.
- [x] Primera prueba de Luciano (10-11, 13:53-14:01; capturas en el chat):
  - Las líneas verdes siguen en la vista larga creada: son **planos de las versiones anteriores**, que quedan en el
    modelo aunque se borren las vistas (un plano de referencia no es de la vista). Esta versión ya no crea planos (en
    el registro no hay ningún fallo de cota con las líneas invisibles). Los viejos se borran a mano.
  - Aristas: "muy gruesas, se ve todo negro" (vista larga y alzado). Con el grosor más fino de Revit (1), una barra a
    1:50-1:75 es más angosta que sus dos bordes. **Corrección**: el borde va en el color de la barra oscurecido
    (`Oscuro` = 0.6), no en negro.
  - Columna (`Col B-3` es **un solo elemento** de 20.86 m: `contorno completo con 4 líneas de 1 elementos`): se veía
    gris dentro de ella donde la atraviesan las vigas, porque ahora la columna va en blanco. **Corrección**:
    `ArranquesDeVigas` (antes solo con barras giradas, dentro de `AbrirParaGiradas`) va siempre: oculta las vigas que
    tocan la columna y dibuja su arranque gris solo a los costados.
  - La columna `189817` no se volvió a generar: queda pendiente leer `primeras piezas`.
- [x] Paso 3 (10-11): `CadenaPorPlanos` devuelve la cota (`Dimension?`). Viga (`Acotar`) y columna
      (`SeccionColumna.Acotar`): por lado, el total y la cadena del recubrimiento en la misma línea, a
      `LineaCotaSeccion` (2.2 × alto de letra) de la cara, contra líneas invisibles. `UnaLinea` refleja los números: los
      de la cadena del lado de la sección, el del total hacia afuera; uno que no entra en su tramo va hacia afuera y se
      corre a lo largo hasta no pisar el del total. Se borraron `CadenaSeccion`, `TotalSeccion`, `LeyendaSeccion`,
      `TextoAncho`, `CotaAncho`, `TextoAlto`, `CotaAlto`, `Leyenda`, `EstriboAparte`.
- [x] Paso 4 (10-11): leyenda a la derecha de la sección, de arriba hacia abajo (`HuecoLeyenda`); el estribo aparte
      después de la leyenda. `ApilarEstribos(…, pisoMinimo, izquierda)` apila en columnas: una forma que no entra sobre
      la línea de cota de abajo empieza otra columna a la derecha, arriba de todo; devuelve también hasta dónde llega a
      la derecha, para el recorte. Sin clase aparte en `calculo` (el reparto depende de lo que mide Revit cada detalle).
- [x] Segunda prueba (10-11, 14:51-14:57): la posición de la leyenda, el alzado y la vista larga ✅. Luciano: el total
      afuera y **toda** la cadena adentro (los `0.04` salían afuera junto al total), y quitar el `#3` debajo del estribo y
      del menor (ya está en la leyenda). **Corrección** (`UnaLinea`): todos los números de la cadena del lado de la
      sección; uno que no entra en su tramo se corre a lo largo, alejándose del centro, hasta no pisar al vecino. El
      total, hacia afuera. `ApilarEstribos` ya no escribe el diámetro.
- [x] Paso 2, hallazgo (10-11, registro 14:51): en la columna `189817` el estribo aparece a 0.083 m y los menores recién
      a 3.946, 7.596, 11.846 m… (`primeras piezas 0,083; 3,946; 3,946; 3,984…`): al pie no hay menores. Luciano: "no se
      están generando los menores bien, porque se supone están para toda la columna". **No es del despiece**: es del
      armado de columnas (add-in `armado-columnas`). Medido con el MCP de Revit (solo lectura): las barras `442838`,
      `442881`, `442859`, `442568` ya no están en el modelo abierto; la columna `189817` va de −0.90 a 19.96 m. Queda
      como trabajo aparte, con su propio plan: se pasó a la sesión `addins-dd`, que encontró la causa (10-11): el paquete
      de estribo y grapas con #3 mide 5.7 cm de alto y en el confinado de 189817 la separación es 0.05 m, así que el
      armado omite los menores de todo ese tramo (`GeneradorLongitudinal.cs:941-949`, rama `columnas/observaciones-c2`).
      Plan: `C:\Proyecto\addins\docs\planes\2026-11-10-menores-omitidos-en-confinado.md`.
- [x] Viga angosta (10-11, captura de `V Piso 2 - (0.20x0.50)` con la versión anterior): los tres tramos
      `0.04 | 0.12 | 0.04` son más angostos que su número y se pisaban con el total. **Corrección** (`UnaLinea`): la
      cadena va toda adentro; el número más cercano al medio queda quieto y los demás se separan hacia las puntas lo
      justo para no tocarse (0.3 del alto de letra de aire). Reemplaza la regla de "los chicos hacia afuera".
- [x] Tercera prueba (10-11, 15:27-15:30; `V C`, `Col B-3`, `V Piso 2 - (0.20x0.50)`): cadena adentro y total afuera
      en los dos lados, también en la viga de 0.20 (`0.04 0.12 0.04` legibles); sin `#3` bajo el estribo ni la grapa.
- [x] `dotnet build CQEING-Addins.sln` sin errores ni advertencias de código (las únicas, de copiar la DLL con Revit
      abierto); `dotnet test CQEING-Addins.sln` en verde (Despiece 161, Vigas 1156, Gestor.Core 256, Gestor 37,
      Viguetas 79, Zapatas 92, CotasPlanta 16, Publicador 8).

### Criterios de aceptación

- ✅ Sin planos de referencia nuevos: las cotas van contra líneas invisibles de su vista (los que dejaron versiones
  anteriores se borran a mano).
- ✅ Leyenda a la derecha y estribo y menores en columnas que no bajan más que la sección (viga y columna).
- ✅ Una sola línea de cota por lado: la cadena adentro, el total afuera.
- ✅ Sección angosta: números legibles, sin taparse ni tapar el borde (`V Piso 2` de 0.20).
- ⏳ Columna `189817` sin menores: no es del despiece; es del armado de columnas (sesión `addins-dd`, plan
  `2026-11-10-menores-omitidos-en-confinado.md`), pendiente de que Luciano elija la regla.
- ✅ Barras de dentro con borde en un tono más oscuro de su color (en negro se veían todas negras).
- ✅ Columna completa en el alzado, sin huecos ni grises en los nudos.
- ✅ Pruebas en verde y compilación sin advertencias de código.
- ✅ Probado en Revit 2027 por Luciano.
- [ ] Probado en Revit.
