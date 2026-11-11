# Plan: ocultar las líneas verdes (planos de referencia) en las vistas del despiece

## Contexto

- Pedido de Luciano (11-11): en las vistas del despiece aparecen líneas verdes. Son **planos de
  referencia** (categoría de Revit "Planos de referencia", `OST_CLines`) que crearon versiones
  anteriores del despiece para colgar las cotas. Los planos son elementos del modelo: quedan aunque
  se borre la vista donde se crearon y se ven en cualquier vista que los corte.
- Desde la 0.8.0 (plan `2026-11-10-despiece-carlos.md`) el despiece ya no crea planos: las cotas se
  apoyan en líneas de detalle invisibles de la propia vista (`GeneradorPrevia.ReferenciaEnVista`).
  Pero los planos viejos siguen en los modelos y se ven en las vistas nuevas.
- Fuera de alcance: borrar los planos viejos del modelo (es destructivo y puede tocar planos que
  puso el proyectista; si se hace, va aparte y con confirmación de Luciano). Las vistas ya creadas
  no se tocan: el cambio vale para las vistas que se generen desde ahora.
- Entorno: Revit 2026 y 2027, modelo "edificio tevening", rama `despiece/ocultar-planos` (desde
  `origin/main` 687414d), worktree `C:\Proyecto\addins-317`.

## Criterios de aceptación

- [x] En una vista nueva del despiece (viga larga, sección de viga, alzado de columna, sección de
      columna) no se ve ninguna línea verde.
- [x] Las cotas de esas vistas salen igual que en la 0.8.0 (ninguna desaparece ni cambia de lugar).
- [x] Los planos de referencia siguen existiendo en el modelo y se ven en las demás vistas (no se
      borra nada).

## Hallazgos del código y de Revit real

- Medido con MCP Revit (solo lectura) en "edificio tevening" el 10-11: 28 planos de referencia,
  todos sin nombre. 206907–206912 (6, probablemente del proyecto) y 22 desde 449309 (probablemente
  del despiece).
- `grep NewReferencePlane addins/despiece` (11-11): ninguna coincidencia. Ninguna cota del despiece
  depende de un plano de referencia, así que ocultar la categoría en la vista no puede romper cotas.
  Las referencias de las cotas son líneas de detalle (categoría Líneas, `OST_Lines`, estilo líneas
  invisibles), que no es la categoría que se oculta.
- Las cuatro vistas pasan por una función `Limpiar`:
  - `GeneradorPrevia.Limpiar` (viga larga, `GeneradorPrevia.cs:385`, y sección de viga, `:1219`).
  - `GeneradorColumna.Limpiar` (alzado de columna, `GeneradorColumna.cs:250`).
  - `SeccionColumna.Limpiar` (sección de columna, `SeccionColumna.cs:108`).
- Ocultar una categoría con `View.SetCategoryHidden` es API pública y ya se usa en esas mismas
  funciones para secciones, niveles y rejillas.

## Decisiones

1. **Ocultar la categoría en la vista, no borrar planos** (11-11, confirmada por Luciano tras la
   prueba): no destruye nada y sirve también para planos que existan por otros motivos.

## Plan paso a paso

1. **Agregar `OST_CLines` a las categorías ocultas** en los tres `Limpiar` — archivos:
   `addins/despiece/Revit/GeneradorPrevia.cs`, `Columnas/GeneradorColumna.cs`,
   `Columnas/SeccionColumna.cs` — con `CanCategoryBeHidden` antes, como ya hacen las de columna.
   Cómo se prueba: build sin advertencias.
2. **Pruebas automáticas**: `dotnet test CQEING-Addins.sln` sin fallos.
3. **Despliegue y prueba en Revit** (2026 y 2027) en "edificio tevening": generar despiece de una
   viga y de una columna, revisar los tres criterios.

## Cómo probarlo

```
dotnet build addins/despiece/CQEING.Despiece.csproj -c Debug -p:RevitVersion=2027
Get-Process -Name Revit -ErrorAction SilentlyContinue   # tiene que no devolver nada
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin despiece -Revit 2027
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin despiece -Revit 2026
```

1. Abrir "edificio tevening", generar el despiece de una viga que tenía líneas verdes y de una columna.
2. Revisar que no haya líneas verdes y que las cotas estén todas.
3. Abrir una planta o un corte normal y ver que los planos siguen ahí.

## Estado

- [x] Paso 1 (11-11): `OST_CLines` agregado a los tres `Limpiar`. Build 2027 sin advertencias ni
      errores. El build 2026 compila, pero no pudo copiar la DLL porque Revit (proceso 16316) estaba
      abierto y la tenía tomada; se repite con Revit cerrado.
- [x] Paso 2 (11-11): `dotnet test CQEING-Addins.sln` sin fallos (Despiece 161, Vigas 1156,
      Gestor.Core 256, Gestor 37, Viguetas 79, Zapatas 92, CotasPlanta 16, Publicador 8).
- [x] Build 2026 repetido con Revit cerrado (11-11): sin advertencias ni errores.
- [x] Desplegado en 2026 y 2027 con `desplegar-local.ps1` (11-11).
- [x] Paso 3 (11-11): Luciano lo probó en Revit con "edificio tevening": "funciona bien".
