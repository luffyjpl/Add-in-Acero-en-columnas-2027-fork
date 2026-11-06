# Plan: observaciones del sheet sobre el add-in de columnas (puntos 1, 5, 6 y 7)

## Contexto

- El equipo anotó observaciones del add-in de armado de columnas en una hoja de cálculo
  (capturas que pasó Luciano el 05-nov). Este plan cubre cuatro puntos:
  - **1.** «No concuerdan las medidas de algunas varillas con lo que pone el add-in de columna»:
    en un detalle de plegado de una grapa #3 CQEING, Revit acota 0.09 en el gancho y el
    cuadro de varillas dice E = 0,057.
  - **5.** «Estribos menores (grapas) en nudos: no se generan al ejecutar el add-in».
  - **6.** «Muchos decimales al escoger el tipo de barra»: la lista muestra
    `#4 CQEING · 12.699999999999998 mm`. El ingeniero pidió redondear: `12.70`, con 2 decimales como máximo.
  - **7.** «En el visor de planta (Columnas) poder hacer zoom y desplazarse dentro del panel
    izquierdo»: con muchas columnas en el proyecto no se alcanza a elegir.
- Fuera de alcance: cambiar los valores del cuadro, el cálculo de los ganchos o los tipos de gancho de Revit.
- Entorno: Revit 2027, rama `columnas/observaciones-sheet` desde `origin/main` (a2738bb), worktree
  `C:\Proyecto\addins-sheet`.

## Criterios de aceptación

- [ ] **1.** El cuadro de varillas explica por qué la cota del gancho en el dibujo de Revit sale mayor que la E, con un ejemplo.
- [ ] **5.** Con estribos menores configurados, los nudos llevan grapas y cajas con las mismas reglas que los
      tramos libres (paquete encima y debajo del principal, aviso y sin menores si los paquetes se pisan).
      Sin estribos menores configurados, nada cambia.
- [ ] **5.** La vista previa (sección del tramo, alzado, 3D) y los avisos de choque muestran lo mismo que se genera.
- [ ] **6.** La lista de varillas muestra el diámetro con 2 decimales (`#4 CQEING · 12.70 mm`).
- [ ] **7.** La planta del panel izquierdo tiene zoom (rueda, pellizco, botones − / + / Ajustar) y se desplaza
      arrastrando con clic, como los otros dibujos; un clic sin arrastrar sigue eligiendo la columna.
- [ ] `dotnet test CQEING-Addins.sln` sin fallos y compilación sin advertencias.

## Hallazgos del código y de Revit real

### Punto 1: es una diferencia de lectura, no del acero

- La tabla web del cuadro (`addins/armado-columnas/Tablero/web/columnas.js:790-796`) muestra para el gancho de
  180° y el estribo de 135° la **E** (largo recto después del doblez) y la **D** (diámetro de doblado).
- La cota de Revit en el detalle de plegado es la **pata medida por fuera**: E + D/2 + diámetro de la barra.
  Para el #3 a 135°: 0,057 + 0,038/2 + 0,0095 = 0,0855 ≈ **0,09**, lo que muestra la captura del sheet.
- Es lo mismo que pasó en vigas el 28-oct (observaciones 3 y 4 de Santiago,
  `docs/planes/2026-10-28-reglas-largo-total-gancho.md`): allí se midió en Revit y los ganchos cumplían.
- La captura del sheet es del cuadro viejo (WPF, «Gancho 90° E / D»). Desde #203 el de 90° ya muestra la B por fuera.

### Punto 5: los nudos se excluyen a propósito en cinco lugares

La exclusión es una decisión del 20-oct (`2026-10-20-diseno-estribos-menores-visual-y-64.md`, línea 42:
«Estribos menores en los nudos: hoy no se generan y sigue igual»). Los lugares que la aplican:

| Archivo | Qué hace hoy |
|---|---|
| `Logica/GeneradorArmado.cs:356` | `conMenores = tramo.Tipo != DentroDeViga && piezas.Count > 0`: el paquete del nudo es solo el principal. |
| `Logica/GeneradorLongitudinal.cs:914` | Salta los tramos `DentroDeViga` y `ContraZapata` al crear los menores. |
| `Modelo/ChoquesSeccion.cs:148` | Los avisos de choque no cuentan los menores en nudo ni contra zapata. |
| `Tablero/ArmadorVistaColumnas.cs:246` | El alzado de la vista previa reparte el nudo sin paquete de menores. |
| `Tablero/web/columnas.js:451` | La sección del tramo elegido en un nudo se dibuja sin menores. |

- La regla «si los paquetes se pisan, ese tramo va sin menores y se avisa» (`GeneradorLongitudinal.cs:926-934`)
  aplica igual en el nudo, sin tocarla.
- `ClasificadorPrevia3D.cs:227` lee `PaquetePorTramo`, que pasa a tener también los nudos: no hay que cambiarlo.
- `ContraZapata` sigue sin estribos ni menores (ya no se produce desde el 3-nov, pero el código lo maneja).

### Punto 6

- `Tablero/web/columnas.js:31`: `${o.nombre} · ${o.diametroMm} mm`, con el diámetro tal como viene de Revit
  (pies pasados a mm: 12.699999999999998).

### Punto 7

- `Ampliable` (`Tablero/web/dibujos.js:340-460`) es el zoom «como en vigas» del 03-nov. Ya envuelve sección,
  estribos, grapas y traslapos, pero no la `Planta` (`columnas.js:317-319`, 270 × 230 px).
- `.ampliable` es `position: absolute; inset: 0` (`columnas.css:104`), así que `.planta` necesita
  `position: relative` y un alto fijo.
- `Ampliable` ya descarta el clic que sigue a un arrastre, así que arrastrar no elige una columna.
- El zoom máximo es 4×. Con 270 px de ancho y muchas columnas puede quedar corto: se agrega una prop `max`
  y la planta usa 8×.

### Prueba de Luciano en Revit (06-nov)

- Los decimales de la lista, la nota del cuadro y el zoom de la planta funcionan.
- **Zoom:** la barra − / 100 % / + / Ajustar tapa columnas de la planta, que es chica. Pide que quede afuera.
  Se agregó la prop `marco` a `Ampliable`: la barra va encima del dibujo, fuera del recuadro de la planta
  (`dibujos.js`, `columnas.js`, `columnas.css`: `.ampliable-afuera`, `.zoom-barra.afuera`).
- **Nudo:** el estribo de más arriba del nudo sale sin grapa; los de abajo del mismo nudo sí la tienen
  (captura `Captura de pantalla 2026-11-06 154812.png`). Causa sin confirmar. En `GenerarMenores`
  (`GeneradorLongitudinal.cs:1169-1201`) las grapas se crean de abajo hacia arriba y, si una falla, se dejan
  de intentar las demás alturas de esa pieza (`if (creadas == antes) break;`) con un aviso. Hace falta el texto de los
  avisos de esa generación y el «Diagnóstico de Rebar» del estribo de arriba.
- **Grapa #3:** el detalle de plegado de una grapa #3 en C con 180° en las dos puntas da 0.22 de largo y
  **0.11** en cada gancho (captura `Captura de pantalla 2026-11-06 155106.png`). Con el 180° doblado como el
  estribo (E = 0,065, D = 0,038) se esperaba ≈ 0,065 + 0,019 + 0,0095 = 0,094 → 0.09; con el 180° del cuadro
  (D = 0,057, el de los traslapos) ≈ 0,103 → 0.10. Ninguno da 0.11: falta medir la grapa real con
  «Diagnóstico de Rebar» (gancho, extensión y largo de cada tramo) antes de tocar nada.

### Causa del estribo sin grapa: un error de #256 que ya está publicado (06-nov)

Avisos de la generación de la columna 1-6 (copiados por Luciano):

```
1-6 tramo 1: This rebar element represents a single bar (the layout rule is Single). || at
  Autodesk.Revit.DB.Structure.RebarShapeDrivenAccessor.get_ArrayLength() <- at
  practicacq.Logica.GeneradorArmado.CrearGrupo(...) GeneradorArmado.cs:line 500
1-6 tramo 3: (lo mismo)
1-6 tramo 5: (lo mismo)
```

- Desde #256 (7d86f0c, que ya está en **`armado-columnas-v1.7.1`**) cada zona se reparte en corridas
  (`RepartoEstribos.Partir`). El espacio que sobra al final de una zona queda como un estribo **suelto**
  (`SetLayoutAsSingle`, `GeneradorArmado.cs:486`).
- Después, `CrearGrupo` lee `acceso.ArrayLength` (`GeneradorArmado.cs:500`). Con un estribo suelto, Revit tira
  excepción: «This rebar element represents a single bar». El estribo queda creado, pero:
  - no se llama a `RegistrarPosiciones`, así que el estribo suelto no tiene grapas. Es lo que se ve en el nudo;
  - la excepción corta `GenerarTramo`: el tramo cuenta como omitido, sale ese aviso en inglés y **las corridas
    que siguen no se crean**. En un tramo confinado, si la zona de abajo termina en un estribo suelto, la central y
    la de arriba quedan **sin estribos**.
- Afecta también a los tramos libres, no solo a los nudos. Pasa en la versión publicada.
- Arreglo: con un estribo suelto (`posiciones <= 1`) no se lee `ArrayLength`; el reparto vale 0. Lo mismo en la
  traza de diagnóstico (`GeneradorArmado.cs:542`).

### La cota del gancho: corrección de la nota del cuadro (06-nov)

«Diagnóstico de Rebar» de la columna 1-6 (host 782736):

| Barra | Gancho | Tramo recto real | Radio del doblez (eje) | Largo del gancho de Revit (`GetHookLength`) | Cota del dibujo |
|---|---|---|---|---|---|
| Estribo #3 (820947), «CQ Estribo» | «CQ Estribo 135°» | **5,70 cm** | 2,38 cm | 8,4 cm | 0.08 (vigas, 28-oct) |
| Grapa C #3 (821184), 22,0 cm | «CQ Grapa 180° estribo» | **6,50 cm** | 2,38 cm | 11,1 cm | **0.11** (captura) |

- El acero está bien: el tramo recto de cada gancho es exactamente la E del cuadro (0,057 y 0,065), y el doblez es
  el del estribo (D = 0,038).
- La cota del detalle de plegado es el **largo del gancho como lo cuenta Revit** (curva + recto, `GetHookLength`),
  no «E + D/2 + Ø» como decía la nota. Esa fórmula da 0,094 para la grapa, y Revit acota 0.11. La nota se
  corrige con los valores medidos.

## Decisiones

1. **Punto 1: solo una nota en el cuadro, sin cambiar valores** (2026-11-06, Luciano), igual que en vigas.
2. **Punto 5: grapas y cajas en los nudos con las mismas condiciones y reglas que en los tramos libres, y solo si
   hay estribos menores configurados** (2026-11-06, Luciano). Esto reemplaza la línea 42 del diseño del 20-oct.
3. **Punto 6: diámetro con 2 decimales** (2026-11-06, ingeniero vía Luciano): `12.70 mm`.
4. **Punto 7: zoom máximo 8× para la planta**, 4× para los demás (decisión mía, por lo dicho arriba).

## Plan paso a paso

1. **Punto 6: diámetro con 2 decimales** — archivo: `Tablero/web/columnas.js:31` → `o.diametroMm.toFixed(2)`.
   Cómo se prueba: abrir `Tablero/web/index.html` con `demo.js` en el navegador y la lista dice `12.70 mm`.
2. **Punto 1: nota en el cuadro** — archivo: `Tablero/web/columnas.js` (nota bajo la tabla). Texto: «En el
   detalle de plegado de Revit la cota del gancho es la pata medida por fuera del doblez (E + D/2 + Ø de la barra),
   por eso sale mayor que la E: #3 a 135° = 0,057 aquí y 0,09 en el dibujo.» Cómo se prueba: abrir el cuadro en la demo.
3. **Punto 7: zoom en la planta** — archivos: `Tablero/web/dibujos.js` (prop `max` en `Ampliable`),
   `Tablero/web/columnas.js:317-319` (envolver `Planta` en `Ampliable clave="planta" max=8`),
   `Tablero/web/columnas.css` (`.planta { position: relative; height: 230px; overflow: hidden }`).
   Cómo se prueba en la demo: rueda acerca hacia el mouse, arrastrar mueve, clic sin arrastre elige, Ctrl + clic suma.
4. **Punto 5: menores en los nudos** — archivos: los cinco de la tabla de arriba. La condición pasa a ser «no
   `ContraZapata`» (en `ArmadorVistaColumnas`, «`Libre` o `DentroDeViga`»), y se actualizan los comentarios que
   dicen que en los nudos no van. Si hay pruebas que esperan nudos sin menores, se actualizan a la regla nueva.
   Cómo se prueba: `dotnet test CQEING-Addins.sln`, y en Revit (abajo).
5. **Build, pruebas, despliegue y prueba en Revit** — comandos abajo.

## Cómo probarlo

```
cd C:\Proyecto\addins-sheet
dotnet build addins/armado-columnas/practicacq.csproj -c Debug -p:RevitVersion=2027
dotnet test CQEING-Addins.sln
Get-Process -Name Revit -ErrorAction SilentlyContinue   # tiene que no devolver nada
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin armado-columnas -Revit 2027
```

En Revit 2027:

1. Abrir el add-in y elegir el #4: la lista dice `#4 CQEING · 12.70 mm`.
2. Abrir el cuadro de varillas: está la nota del gancho por fuera del doblez.
3. En la planta del panel izquierdo: rueda para acercar, arrastrar para moverse, clic para elegir, «Ajustar» para volver.
4. Una columna con nudo y grapas configuradas: generar. El nudo tiene grapas encima y debajo de cada estribo,
   como los tramos libres, y la vista previa lo mostraba antes de generar.
5. Una columna sin grapas: generar. El nudo queda igual que antes, solo con estribos.
6. Nudo con estribos muy juntos (separación menor que el alto del paquete): sale el aviso y el nudo va sin menores.
7. Detalle de plegado de una grapa #3 en un nudo: medir el gancho y anotar la cota (se espera 0,09).

## Estado

- [x] Plan revisado con el usuario (decisiones del 06-nov).
- [x] Pasos 1 a 4 implementados (06-nov), sin commit todavía:
  - Punto 6: `columnas.js:31`, `diametroMm.toFixed(2)`.
  - Punto 1: segunda nota bajo la tabla del cuadro (`columnas.js`).
  - Punto 7: `Ampliable` con prop `max` (`dibujos.js`), planta envuelta con `clave="planta" max=8`
    (`columnas.js`), `.planta` con `position: relative; height: 230px; overflow: hidden` (`columnas.css`).
  - Punto 5: la condición pasó a «no `ContraZapata`» en `GeneradorArmado.cs`, `GeneradorLongitudinal.cs`,
    `ChoquesSeccion.cs` y `ArmadorVistaColumnas.cs`. En `columnas.js` se quitó la línea que vaciaba los menores
    de la sección del nudo. Comentarios actualizados.
- [x] `dotnet build CQEING-Addins.sln -c Debug -p:RevitVersion=2027`: 0 advertencias, 0 errores.
- [x] `dotnet test CQEING-Addins.sln`: todas pasan (no hay proyecto de pruebas de columnas; ninguna prueba
      usaba nudos sin menores). `node --check` OK en `columnas.js` y `dibujos.js`.
- [x] Desplegado en Revit 2027 desde `C:\Proyecto\addins-sheet` (Revit cerrado, comprobado con `Get-Process`).
- [x] Primera prueba en Revit de Luciano (06-nov): decimales, nota y zoom OK; los tres hallazgos de arriba.
- [x] Arreglos de la segunda vuelta (06-nov): barra de zoom afuera de la planta, `ArrayLength` solo con más de un
      estribo (`GeneradorArmado.cs`), nota del cuadro corregida con los valores medidos. Compila (sin `error CS`; la
      copia a `bin` falló porque Revit tenía la DLL abierta). Falta desplegar con Revit cerrado.
- [x] Segunda prueba en Revit (Luciano, 06-nov): «todo bien». Regenerada la 1-6 sin avisos «single bar», el
      último estribo de cada nudo con su grapa, la barra de zoom afuera de la planta y la nota nueva del cuadro.

Criterios de aceptación: ✅ los seis (puntos 1, 5, 6 y 7, vista previa de los nudos, build y pruebas). Además
quedó arreglado el estribo suelto de #256.
- [ ] **Publicar una versión nueva de armado-columnas** (la 1.7.1 tiene el error del estribo suelto): decisión de
      Luciano después de probar.
