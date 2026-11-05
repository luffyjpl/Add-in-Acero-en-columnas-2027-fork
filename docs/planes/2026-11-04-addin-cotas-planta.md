# Plan: add-in «Cotas de planta» (reunión Carlos/Juan, 4-nov-2026)

## Contexto

- **Qué se pide:** en la reunión del 4-nov-2026 (Carlos Quintero y Juan Felipe), Carlos pidió que la planta
  estructural se acote sola y quede limpia y fácil de leer: cotas eje a eje y libres (cara a cara), los
  voladizos medidos siempre desde el eje hasta el extremo, nombres cortos de vigas con su leyenda,
  columnas con color, losa tenue y una X en los vacíos. Referencia visual: la planta de 3 ejes × 6 ejes
  que mandó (cotas 3.01 / 2.99 arriba, 0.30 / 2.71 / 0.30 libres).
- **Vista que se genera:** un plano estructural duplicado llamado **«Cotas &lt;planta&gt;»** (por ejemplo
  «Cotas N+3.70»). Ese nombre fijo es lo que usará el futuro add-in de armar planos para encontrarla,
  igual que el despiece deja «&lt;nombre&gt;» y «Sección &lt;nombre&gt;».
- **Fuera de alcance:** colocar la vista en una hoja, cubiertas (pendiente en %), ejes girados (solo se
  leen ejes alineados con X o Y), y el «trazo diagonal largo» en Revit (por ahora solo se ve en la vista
  previa; en Revit manda el tipo de cota base).
- **Entorno:** Revit 2026 (net8.0-windows) y 2027 (net10.0-windows). Modelo de prueba: el de 3 × 4 ejes
  con viguetas `VT_10X35`, vigas `VIG_25X35` / `VIG_20X35` y vigas de borde `VB_10X35`, planta N+3.70.
  Rama `cotas-planta`.

## Criterios de aceptación

- [ ] Botón «Cotas de planta» en la pestaña CQEING, panel «Planos», con licencia y bloqueo por versión
      como los demás add-ins.
- [ ] Ventana con las plantas a la izquierda, vista previa (con zoom) en el centro y configuración a la
      derecha; la vista previa coincide con lo que se crea en Revit.
- [ ] La ventana no bloquea Revit: se puede minimizar sola sin minimizar Revit.
- [ ] Genera «Cotas &lt;planta&gt;» de la planta seleccionada con: cotas libres, eje a eje, voladizos (una
      cota por borde, cada una desde el eje), separación de viguetas (al eje de cada vigueta) y longitud
      total, cada tipo con su color.
- [ ] Rótulos de vigas uno por vano (V1, VA, VI-1, VBD o la sección), leyenda a la derecha, columnas
      naranjas, losa al 80 % de transparencia y X solo en recuadros sin losa ni viguetas.
- [ ] La vista no muestra armadura, planos de referencia, marcas de sección ni CAD importados.
- [ ] Si la vista ya existe no se toca (no se pisan cambios hechos a mano).

## Hallazgos del código y de Revit real

Medido en el modelo de prueba, planta N+3.70, con el servidor MCP de Revit (comandos de solo lectura y
cotas de prueba en una subtransacción que se deshizo):

1. **La vista muestra elementos de otros niveles.** `FilteredElementCollector(doc, view.Id)` trae vigas del
   N+6.70 (Z 6.35–6.70). Se filtran vigas por `INSTANCE_REFERENCE_LEVEL_PARAM` y losas por `LevelId`
   (`Revit/PlanGeometryReader.cs`).
2. **La línea de ubicación de una viga puede ir por un borde.** La viga de borde `VB_10X35` del eje 1 tiene
   caras a 1.35 y 1.45 del eje, pero su línea no va por el centro. Igual que `acero-vigas/Revit/LectorModelo.cs`,
   la huella y la sección se sacan de la caja de la geometría, no de la línea ni de los parámetros `b`/`h`.
3. **Convención de nombres de tipo:** `VIG_` = viga, `VT_` = vigueta, `VB_` = viga de borde. Si un tipo no la
   sigue, una viga de 12 cm o menos se toma como vigueta (`RoleOf`).
4. **Voladizo abajo del eje 1 (franja A–B):** caras de la viga de borde a 1.35 y 1.45 y extremo de la viga del
   eje A a 2.07. Coincide con el croquis de Carlos (tres cotas desde el eje). Para no mezclar el otro lado,
   un elemento cuenta en la franja solo si se mete en ella más que la tolerancia (no basta con tocar el borde).
5. **Viguetas: hay que acotar su eje con `FamilyInstanceReferenceType.CenterFrontBack`.** La familia
   `Hormigón-Viga rectangular` expone `Left`, `Right`, `CenterFrontBack` y `CenterElevation`, no
   `CenterLeftRight`. `CenterFrontBack` es el plano a lo largo de la vigueta por su centro: una cota de prueba
   con él midió 0.63 · 0.55 · 0.50 desde el eje A y quedó en la posición pedida (y = 14.06). Con caras sacadas
   de la geometría del símbolo la cadena salía desplazada al lado opuesto.
6. **Sin plantilla de vista aparece de todo:** armadura (líneas verdes), planos de referencia, marcas de
   sección, cámara y caja de sección. La vista generada oculta toda categoría que no sea ejes, columnas,
   vigas, cimentación, losas, muros, cotas, textos y líneas de detalle.
7. **Revit no acota un eje más allá de donde termina**, y las burbujas quedaban dentro de las cotas. Los ejes se
   alargan en la vista (`DatumExtentType.ViewSpecific`) hasta quedar por fuera de todas las cotas.
8. **Mezclar las caras de columnas con las viguetas en una sola cadena da tramos ilegibles** («0.5 0.0 0.5…»).
   Las viguetas van en su propia cadena (eje → viguetas → eje), justo después de las libres.

## Decisiones

1. **Plano duplicado, no sección** (2026-11-04, Juan Felipe): una sección es un corte vertical y no muestra la
   planta con sus ejes. Lo que permite encontrarla es el nombre fijo «Cotas &lt;planta&gt;».
2. **Un color y un tipo de cota por clase de cadena** (2026-11-04): «CQEING Cota libre (cara a cara)» negro,
   «CQEING Cota eje a eje» azul, «CQEING Voladizo (eje a extremo)» ámbar, «CQEING Longitud total» morado,
   «CQEING Separación de viguetas» verde azulado. Se duplican del tipo de cota que se elige en la ventana.
3. **Voladizos: una cota por borde, cada una desde el eje y en su propia línea** (2026-11-04, Juan Felipe). Se
   probó ponerlos en una sola cadena sobre la línea eje a eje y se descartó: «todo muy junto, es mejor como
   estaba antes».
4. **Viga de borde «VBD»** (2026-11-04): «VB» chocaba con la viga del eje B.
5. **Rótulo por vano** (2026-11-04): con uno solo en el medio de la viga caía sobre la columna central.
6. **Textos a 2.5 mm** (2026-11-04, Juan Felipe).
7. **Se genera solo la planta seleccionada** (2026-11-04, Juan Felipe): se quitaron las casillas de la lista.
8. **Ventana sin modo con `ExternalEvent`** (2026-11-04, Juan Felipe): al minimizar no debe minimizarse Revit.
   Las plantas se leen todas al abrir la ventana, porque una ventana sin modo no puede leer el modelo; la
   generación se pide con `ExternalEvent` (`Revit/GenerateRequest.cs`).
9. **Si «Cotas &lt;planta&gt;» ya existe, no se regenera** (2026-11-04): mismo criterio que el despiece.

## Plan paso a paso

1. **Dominio sin Revit** — `Domain/PlanGeometry.cs`, `Domain/ChainPlanner.cs`, `Domain/LegendContent.cs`,
   `Configuration/CotasSettings.cs`: modelo de la planta en metros y planificador de cadenas. La vista previa
   y Revit usan el mismo resultado.
2. **Lectura de Revit** — `Revit/PlanCatalog.cs`, `Revit/PlanGeometryReader.cs`: plantas, ejes, columnas,
   vigas y losas del nivel.
3. **Ventana** — `UI/*`: lista de plantas, vista previa con zoom (`UI/PlanPreview.cs`) y configuración.
4. **Escritura en Revit** — `Revit/PlanViewGenerator.cs`, `Revit/PlanWriters.cs`, `Revit/AnnotationStyles.cs`,
   `Revit/FaceReferences.cs`: duplica la planta, oculta categorías y CAD, alarga ejes, crea cotas, rótulos,
   X, leyenda y colores. El resumen final dice qué cadena no se creó y por qué.
5. **Integración** — `AppCotasPlanta.cs` (cinta, licencia y bloqueo comunes), `addin.json`, `.addin`, solución.

## Cómo probarlo

```
dotnet build addins/cotas-planta/CQEING.CotasPlanta.csproj -c Debug -p:RevitVersion=2026
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin cotas-planta -Revit 2026
```

1. Abrir el modelo de prueba, pestaña CQEING → Planos → «Cotas de planta».
2. Elegir N+3.70; comprobar la vista previa (zoom con la rueda, arrastrar, doble clic para ajustar).
3. Minimizar la ventana: Revit no debe minimizarse.
4. «Generar cotas de N+3.70»: se abre «Cotas N+3.70» y un resumen sin problemas.
5. En la vista: voladizo abajo a la izquierda 1.35 / 1.45 / 2.07 desde el eje 1; cadena de viguetas arriba al
   eje de cada vigueta (0.63 · 0.55 · 0.50 …); libres de la izquierda con la VI-1 (1.94 / 0.20 / 1.54); sin
   armadura ni planos de referencia; leyenda completa fuera de las burbujas.
6. Volver a generar: debe avisar que la vista ya existe y no tocarla.

## Estado

- [x] Plan revisado con el usuario (iterado en la conversación del 4-nov-2026).
- [x] Pasos implementados. Compila para 2026 y 2027 sin advertencias; `dotnet test CQEING-Addins.sln` en verde;
      el publicador arma `cotas-planta-0.1.0-revit2026.zip` y `-revit2027.zip` en local.
- [x] Probado en Revit 2026 en el modelo de prueba: se crean las 13 cadenas planificadas (verificado con el
      servidor MCP). Hallazgo 5 (eje de viguetas) verificado con una cota de prueba.
- [ ] Pendiente de retest en Revit: la versión final con viguetas al eje, ventana sin modo y zoom.
- [ ] Revit 2027: compila, sin probar en Revit.
