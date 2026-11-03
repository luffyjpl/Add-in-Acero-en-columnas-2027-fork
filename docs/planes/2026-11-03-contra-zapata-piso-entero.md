# Plan: un piso entero queda como "contra zapata" y sale sin estribos (hotfix columnas)

## Contexto

- Uno de los ingenieros reporta que a veces el add-in de columnas toma un
  tramo libre completo (todo el primer piso) como si fuera el tramo
  "contra zapata". Ese tramo no lleva estribos (decisión del 20-oct,
  `2026-10-20-grapas-segun-modelo-de-referencia.md`), así que el piso
  entero queda sin estribos.
- No hay modelo para reproducirlo: el ingeniero no tuvo tiempo de pasar
  uno. El análisis sale solo de leer el código (pedido de Luciano,
  3-nov).
- Fuera de alcance: el anclaje del longitudinal en la zapata y la pestaña
  «Vista previa» (planta y 3D).
- Entorno: Revit 2027, .NET 10, rama `hotfix-contra-zapata-piso-entero`
  (desde `main` en `d2949cf`).

## Criterios de aceptación

- [ ] Ningún estribo queda dentro de la zapata. Todo lo que está por encima
      de su cara superior lleva estribos, aunque esté cerca de la zapata,
      enterrado o en el mismo elemento de Revit que sigue hacia arriba
      (decisión 8).
- [ ] Una viga de amarre o de cimentación modelada en la categoría
      *Cimentación estructural* (no en *Armazón estructural*) ya no se toma
      como "la zapata". Hace de nudo, como una viga normal.
- [ ] El longitudinal sigue anclando a 7,5 cm del fondo de la zapata.
- [ ] `dotnet run --project tools/PruebasTraslapos` con 0 fallas, y
      `dotnet build` del add-in (y de `tools/PreviewUI`) sin advertencias.

## Hallazgos del código y de Revit real

Solo lectura de código (sin Revit), 3-nov:

1. **Regla anterior** (`Logica/LectorContexto.cs`, `ArmarTramos`): el
   primer sub-tramo libre del elemento de más abajo se marcaba
   `ContraZapata` (sin estribos) siempre que `sobreZapata` fuera verdadero.
   No se miraba cuánto medía ni dónde terminaba. Sin una viga que lo
   cortara, el tramo iba de la zapata a la viga del primer piso.
2. **Causa probable A, viga de amarre en la categoría Cimentación:**
   - `BandasDeViga` solo recorre `OST_StructuralFraming`, así que una
     viga de amarre en `OST_StructuralFoundation` no forma nudo.
   - `TopeDeZapata` se quedaba con la cara superior **más alta** de todas
     las cimentaciones que tocan la columna en planta, que es la de la
     viga de amarre. El arranque de los estribos subía a la cara superior
     de esa viga, y el dado de abajo quedaba sin estribos.
3. **Causa probable B, la viga de amarre no cuenta como nudo:** se
   descarta si mide menos de 20 cm de ancho o de alto (criterio de
   vigueta) o si en planta no toca la columna. Resultado: el mismo que en A.
4. **Un mismo elemento puede tener una parte contra zapata y otra normal**
   (Luciano, 3-nov). En el modelo de referencia el dado 781598 (−0,65 a 0)
   es un elemento aparte, que termina en el nivel `N+0.00 Piso 1` (planes
   `2026-10-07-plan-barra-continua-41-42.md` y `2026-10-16-leer-portico-revit-81.md`).
   Si el dado y la columna están modelados como uno solo, todo el elemento
   quedaba contra zapata.
5. **Detalle aparte:** `TopeDeZapata` comparaba solo en planta, no en
   altura. Tomaba como "su" zapata cualquier cimentación que coincidiera en
   planta con la columna, aunque estuviera lejos del pie de la columna.
6. **Los estribos ya arrancan en la cara superior de la zapata.**
   `zArranque = Math.Max(el.ZInf, zZapata)`: lo que queda dentro de la zapata
   ni siquiera es un tramo. Por eso, con la decisión 8, basta con que
   ningún tramo se marque `ContraZapata`.

## Decisiones

1. ~~**Red de seguridad de 1,5 m** (Luciano): un tramo contra zapata nunca
   mide más de 1,5 m.~~ Reemplazada (Luciano, 3-nov: "lo del 1,5 no me
   suena").
2. **Cuál es "la zapata"** (2026-11-03, Claude, con el visto bueno del
   arreglo propuesto): es la cimentación que contiene el pie de la columna
   en altura, con una tolerancia de 5 cm: `inf − 5 cm ≤ zPie ≤ sup + 5 cm`.
   Si hay varias, se toma la más profunda (menor `inf`), que además es la
   que da el fondo para anclar el longitudinal. Si ninguna contiene el pie,
   no hay zapata.
3. **Las demás cimentaciones que cruzan la columna** (vigas de amarre en la
   categoría Cimentación) se cuentan como nudo, igual que una viga. No se
   les aplica el filtro de vigueta de 20 cm: en la categoría Cimentación no
   hay viguetas.
4. ~~**El contra zapata termina en el primer nivel de Revit.**~~
   Reemplazada por la 6 y después por la 8.
5. **La lógica que decide va a un archivo sin Revit**
   (`Modelo/TramosColumna.cs`), para poder probarla en
   `tools/PruebasTraslapos`. `LectorContexto` solo lee la geometría y llama.
6. ~~**Contra zapata = lo que está por debajo de la cota del terreno.**~~
   Reemplazada por la 8.
7. ~~**Cota del terreno en 0,00, editable en la ventana, con la línea del
   terreno en el dibujo de elevación.**~~ Se llegó a implementar y se sacó
   (3-nov): con la decisión 8 no hace falta saber dónde está el terreno.
8. **Solo no lleva estribos lo que está dentro de la zapata** (2026-11-03,
   ingeniero Santiago, vía Luciano: "No importa que los estribos estén
   cerca de la zapata, mientras que no estén dentro de ella"):
   - `LectorContexto` ya no marca ningún tramo `ContraZapata`.
   - El dado y todo lo que está por encima de la zapata es tramo libre o
     nudo, con estribos.
   - La parte dentro de la zapata ya no es un tramo (hallazgo 6).
   - Reemplaza a la regla del 20-oct ("de −0,65 a 0 no se debería generar").
   - El tipo `TipoTramo.ContraZapata` y su manejo en la ventana y en los
     generadores se dejan como están (los usa todavía
     `ProveedorContextoFalso`). Limpiarlos es otro trabajo.

## Plan paso a paso

1. **Lógica pura** — `addins/armado-columnas/Modelo/TramosColumna.cs`:
   - `ElegirZapata(candidatas, zPie, tolerancia)`: índice o `null`
     (decisión 2).
   - `Fusionar(bandas, tramoMinimo)`: se mueve desde `LectorContexto`.
   - `Coser(zArranque, zTope, nudos, tramoMinimo)`: lo que hacía
     `ArmarTramos`, solo con tramos libres y nudos (decisión 8).
2. **Pruebas** — `tools/PruebasTraslapos` (enlace al archivo nuevo y
   `Program.cs`):
   - Dado de referencia (−0,65..0, viga −0,35..0): libre y nudo.
   - Dado y columna en un solo elemento: libre y nudo, ningún contra zapata.
   - Pruebas de `ElegirZapata` y `Fusionar`.
   - Cómo se comprueba: `dotnet run --project tools/PruebasTraslapos` con
     0 fallas.
3. **`LectorContexto`** — `addins/armado-columnas/Logica/LectorContexto.cs`:
   - `CimentacionesQueTocan` reemplaza a `TopeDeZapata`. La zapata se
     elige con `ElegirZapata`, y las demás cimentaciones se suman a las
     bandas de viga.
   - `ArmarTramos` usa `Coser`.
   - Cómo se comprueba: build sin advertencias.
4. **Sacar la cota del terreno** (decisión 7): se vuelven a dejar como en
   `main` `Modelo/IProveedorContexto.cs`, `Modelo/ProveedorContextoFalso.cs`,
   `Vistas/VentanaConfiguradorEstribos.xaml` y `.xaml.cs`. Cómo se comprueba:
   `git diff main --stat` no los muestra.
5. **Build y pruebas** — add-in para 2027 y 2026, `tools/PreviewUI` y
   `dotnet test CQEING-Addins.sln`.

## Cómo probarlo

```
dotnet run --project tools/PruebasTraslapos
dotnet build addins/armado-columnas/practicacq.csproj -c Debug -p:RevitVersion=2027
dotnet build tools/PreviewUI
dotnet test CQEING-Addins.sln
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin armado-columnas -Revit 2027
```

En Revit real (pendiente, cuando haya modelo):

1. Modelo de referencia: el dado (−0,65 a 0) ahora sale con estribos desde la
   cara superior de la zapata, y la ventana ya no muestra un tramo "contra
   zapata".
2. Un dado y una columna modelados como un solo elemento: estribos de la
   cara superior de la zapata hacia arriba.
3. Una viga de amarre en la categoría *Cimentación estructural*: forma nudo,
   y el dado de abajo lleva estribos.
4. Ningún estribo queda dentro de la zapata.
5. El longitudinal sigue anclando a 7,5 cm del fondo de la zapata.

## Estado

- [x] Plan revisado con el usuario (3-nov, decisión 8).
- [x] Pasos 1 a 5 implementados (3-nov), sin commit todavía:
  - `Modelo/TramosColumna.cs` (nuevo): `ElegirZapata`, `Fusionar`, `Coser`.
  - `Logica/LectorContexto.cs`:
    - `CimentacionesQueTocan` reemplaza a `TopeDeZapata`, y las demás
      cimentaciones hacen de nudo;
    - `ArmarTramos` usa `Coser` y ya no produce `ContraZapata`;
    - constante `ToleranciaPieSobreZapata` (5 cm).
  - `Modelo/ColumnaContexto.cs`: comentario de `ContraZapata`.
  - `tools/PruebasTraslapos`: 8 pruebas nuevas.
  - La cota del terreno (decisión 7) se sacó. Ventana, interfaz y
    proveedor falso quedaron como en `main`.
- Verificado sin Revit (3-nov):
  - `dotnet run --project tools/PruebasTraslapos`: 0 fallas.
  - Build del add-in para 2027 y 2026, y de `tools/PreviewUI` y
    `tools/PruebasVistaPrevia`: 0 advertencias, 0 errores.
  - `dotnet test CQEING-Addins.sln`: 0 fallas.
  - `tools/PruebasVistaPrevia` da 1 falla (`lindero_alcance_gancho_90`). Ya
    falla igual sobre `main` sin estos cambios (probado en una copia limpia),
    así que no es de este trabajo.
- Efecto esperado en columnas ya generadas (#180):
  - El dado deja de ser "contra zapata" y pasa a ser tramo libre, así que la
    ventana puede avisar que la columna cambió desde que se generó.
  - Hay que volver a generarlas para que el dado tenga estribos.
- Criterios: ⏳ todos pendientes de Revit real.
- [ ] Probado en Revit (sin modelo por ahora; queda pendiente).
