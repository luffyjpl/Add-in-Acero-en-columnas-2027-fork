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

## Plan paso a paso

1. **#263 CAD** — archivos: nuevo `addins/despiece/Revit/Importados.cs` con
   `Importados.Ocultar(Document doc, View vista)`, que junta los `ImportInstance`, `RevitLinkInstance` y
   `PointCloudInstance` visibles en la vista y los oculta con `HideElements` (solo los que `CanBeHidden`). Se llama:
   - en `GeneradorPlanta.Generar`, después de quitar la plantilla de la copia;
   - al final de `GeneradorPrevia.Limpiar`, `GeneradorColumna.Limpiar` y `SeccionColumna.Limpiar`.
   Cómo se prueba: build sin advertencias, `dotnet test CQEING-Addins.sln`, y en Revit 2027 (ver "Cómo probarlo").
2. **#261 Texto más grande** — `GeneradorPrevia.cs` (`TamanoTexto` y separaciones que dependen del texto), tipos de cota
   `CQ Despiece - metros`/`corta`, columnas (`GeneradorColumna.cs`, `SeccionColumna.cs`) y `web/pasos.js` (`altoLetra`).
   Antes de programar: elegir el tamaño con Luciano (propuesta 3.5 mm a escala 1:15/1:50). Detalle al llegar a este paso.
3. **#260 Título de la viga** — copiar el título que ya tiene columnas (`GeneradorColumna.cs`), con un tipo de texto
   propio más grande. Detalle al llegar a este paso.
4. **#264 Cotas en una línea** — primero una maqueta (artifact) con 2 o 3 formas; el equipo elige; se anota aquí y
   se implementa en `GeneradorPrevia.cs` (`CotaLuces`, `CotaTotal`, `CotaColumnas`, `CotaViga`, `AjustarEjes`).
5. **#262 Columnas en los nudos** — solo después de que Juan Felipe y Santiago confirmen el cambio de regla. Patrón
   de `GeneradorColumna.Limpiar` (vigas con `BoundingBoxIntersectsFilter`), sin el acero de la columna.

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
- [ ] Pasos 2 a 5.
