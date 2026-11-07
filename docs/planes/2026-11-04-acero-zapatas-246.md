# Plan: Acero en zapatas, de la lectura al acero en Revit (#248 a #254, épica #246)

## Contexto

- **Qué se pide:** completar la [épica #246](https://github.com/utilidadcqeing-prog/Add-in-Acero-en-columnas-2027/issues/246)
  hasta una versión prefinal para depurar. El usuario pidió trabajarla completa y resolver con opciones lo que surja.
- **Parte de:** `docs/planes/2026-11-04-addin-zapatas-247.md`, donde están las decisiones 1 a 15 que este plan cumple.
- **Fuera de alcance:** publicar y probar con Santiago (#255), crear zapatas, despiece y cuadro de cantidades.
- **Entorno:** Revit 2026.5.1, rama `zapatas/246-acero-zapatas`, commit local por issue. Push y PR solo por pedido.

## Criterios de aceptación

- [ ] #248: en cuatro casos reales, el add-in devuelve bien contorno, `h`, columnas y tipo.
- [ ] #249: zapatas y vigas sacan el largo de gancho de la misma clase, y las pruebas pasan.
- [ ] #250: el tablero muestra las formas y ganchos reales del modelo, con dibujo, y la elección llega al motor.
- [ ] #251: el motor calcula cantidad, posición, largo, capas y resumen, con pruebas y sin Revit.
- [ ] #252: el tablero sigue la maqueta aprobada, con los cambios de Juan.
- [ ] #253: A1, A2 y A3 iguales se arman con un clic; las distintas no se mezclan.
- [ ] #254: en una individual y una combinada, el acero queda como el manual de Santiago, en segundos.

## Hallazgos del código y de Revit real

Del código de `main` en `6de33cb`. Lo marcado **por medir** aún no se ha comprobado en Revit.

- **Forma de la ventana:** `crear-viguetas` es el patrón más nuevo: página web, `PuenteViguetas`, `AccionesRevit` y un
  protocolo JSON en el cálculo, probable sin Revit.
- **Revit y los ganchos:** la línea que se le da a `CreateFromCurves` termina en la cara exterior del doblez
  (`GanchoPropio.cs`). `CreateFromCurves` devuelve null si no puede, y la normal debe ser perpendicular
  (`CreadorBarras.cs`).
- **Conjuntos:** vigas reparte con `SetLayoutAsFixedNumber`, que deja la primera y la última barra en su punto.
- **Tres recubrimientos:** `RecubrimientoTemporal.cs` usa `CLEAR_COVER_TOP`, `CLEAR_COVER_BOTTOM` y `CLEAR_COVER_OTHER`.
  **Por medir** que existan y se puedan escribir en una zapata.
- **Marca del add-in:** `MarcaAcero.cs` usa Extensible Storage con esquema propio para regenerar sin duplicar.
- **MCP de Revit (2026-11-04):** el usuario instaló `mcp-servers-for-revit` v1.0.0 para leer el modelo y probar código en
  Revit real. Revit escucha en el puerto 8080 y `claude mcp list` dice «Connected». Falta activar sus comandos en Revit y
  usarlo desde una sesión nueva de Claude, con una copia del modelo. Dos detalles al instalar: con `cmd /c npx` el servidor
  no responde a tiempo, así que se registra con `node <caché de npx>\mcp-server-for-revit\build\index.js`; y en Node 26
  `better-sqlite3` no carga, lo que solo afecta a las herramientas que guardan datos. Si no conecta, el diagnóstico del
  paso 5 mide lo mismo.
- **Medido con el diagnóstico (2026-11-04, `Prueba 080720sfsd26`, Revit 2026.5.10, barra #5, h 350, recubrimientos de 75):**
  - La lectura dio bien las 12 zapatas: 8 de 900 × 2000 y 4 de 1800 × 1800, con contorno, altura, columna y eje correctos.
  - **El modelo no tiene combinadas ni formas atípicas:** esos casos siguen sin probar en Revit.
  - Una barra en U por curvas toma una forma existente (`13 2`), sin crear tipos. Una recta con gancho propio (`02`) da la
    misma geometría: x −367.1 a 367.1 y h 82.9 a 275, tal como el plan, con largo 1070.6 mm. Revit no la corrió al regenerar.
  - La punta queda exacta en el recubrimiento opuesto (350 − 75 = 275). Esto confirma las decisiones 12 y 16.
  - `SetLayoutAsFixedNumber` repartió 5 barras. Falta comprobar el sentido del reparto al generar.
  - `CLEAR_COVER_TOP`, `CLEAR_COVER_BOTTOM` y `CLEAR_COVER_OTHER` existen y se pueden escribir. Las zapatas traen el tipo
    `CIM` de 75 mm, que coincide con el valor por defecto de la decisión 8.
  - `GetCurvesForBrowser` devuelve las curvas de cada forma, así que el catálogo las puede dibujar. El modelo tiene 288
    formas, en su mayoría estribos; las que sirven para una parrilla son las de estilo `Standard` y hasta 3 curvas.
  - El proyecto tiene barras por uso, como `#5` y `#5 CIM`.
  - Cuatro zapatas ya traen 2 barras hechas a mano (780849, 780858, 780869 y 780981).

## Decisiones

Continúan la numeración del plan de #247.

16. **Longitud de la pata según la capa** (2026-11-04, Claude, a partir de la decisión 12): la pata termina en el
    recubrimiento opuesto. En la capa pegada al recubrimiento mide `h − rec. inferior − rec. superior`, y en la segunda
    capa se resta el diámetro de la primera. Si no, la punta se saldría del recubrimiento.
17. **Franja de patas** (2026-11-04, Claude, para cumplir la decisión 9): las patas de cada dirección ocupan una franja
    contra el recubrimiento, tan ancha como la barra más gruesa de esa dirección. Las barras de la otra dirección empiezan
    después de esa franja, así que una barra corrida nunca atraviesa una pata. Esto resuelve el punto abierto de la barra
    de borde, y queda por confirmar midiendo en Revit.
18. **El redondeo no mueve el acero** (2026-11-04, Claude, a partir de la decisión 13): los 5 cm se aplican a la longitud
    de corte del resumen y al peso. La barra en Revit conserva su largo: si creciera la pata, se saldría del recubrimiento.
19. **Reglas enlazadas, no copiadas** (2026-11-04, Claude): el cálculo enlaza `Modelo/Reglas.cs` de vigas, como hacen
    `crear-viguetas` y `despiece` con otros archivos.
20. **Ejes de la zapata** (2026-11-04, Claude; corregida el 2026-11-07 por el usuario): salen de la columna que apoya en
    la zapata, con X el lado más cercano a la X del proyecto. Una zapata en rombo bajo una columna recta lleva las
    barras en X e Y del proyecto, recortadas contra el rombo; antes salían paralelas a sus lados, como un cuadrado
    girado. Sin columna, salen del rectángulo más pequeño que envuelve el contorno. La vista 3D del tablero dibuja la
    zapata girada como en el modelo, y «Vista superior» la muestra como la planta de Revit (Y hacia arriba).
21. **Parrilla superior** (2026-11-04, Claude, a partir de la decisión 11): cada barra va media separación real corrida.
    Son `n − 1` barras, para no salirse del recubrimiento. Queda marcada por defecto y se puede apagar.
22. **Aviso de tabla** (2026-11-04, usuario): compara el tipo y el ángulo del gancho elegido, no el largo de la pata.
23. **La pata de 90° va en las curvas** (2026-11-04, Claude, por lo medido): se crea la barra en U sin crear tipos de
    gancho, y así no se llena el proyecto con un tipo por altura. Si el modelador elige una forma, se usa esa; si Revit no
    la acepta, se usa la automática y se avisa. Los ganchos de 135° y 180° usan el tipo del proyecto que se elija.
24. **Barra de cimentación primero** (2026-11-04, Claude, por lo medido): de los tipos con el diámetro de la regla, se
    prefiere el que tiene `CIM` en el nombre, luego `#n`.
25. **Solo el acero del add-in se reemplaza** (2026-11-04, Claude, según #254): al volver a generar se borra lo que lleva
    la marca, previa confirmación. Lo hecho a mano no se toca, y el tablero avisa cuántas barras tiene esa zapata.

## Plan paso a paso

1. **#249, reglas** — archivos: `calculo/CQEING.Zapatas.Calculo.csproj`, `ReglasZapata.cs` — cómo se prueba: mismos largos
   que vigas del #3 al #8 en 90°, 135° y 180°, y avisos de tabla.
2. **#251, motor** — archivos: `Modelo.cs`, `Geometria.cs`, `MotorParrillas.cs` — cómo se prueba: 1.80 × 1.80, h 0.50,
   rec. 0.075, #5 @ 0.15; rectangular; combinada rectangular; combinada en L.
3. **#253, zapatas iguales** — archivo: `AgrupadorZapatas.cs` — cómo se prueba: agrupa iguales y separa distintas.
4. **#248, lector** — archivo: `Revit/LectorZapatas.cs` — cómo se prueba: el diagnóstico del paso 5.
5. **Diagnóstico temporal** — archivo: `Revit/ComandoDiagnosticoZapata.cs`, solo en Debug — cómo se prueba: sobre zapatas
   reales escribe un informe y lo deshace todo. **Antes de programar #254.**
6. **#254, generar** — archivos en `Revit/` — se escribe con lo medido en el paso 5.
7. **#250 y #252, catálogo y tablero** — archivos en `Tablero/` — cómo se prueba: en Revit, con el modelo abierto.
8. **Cerrar el plan** — actualizar **Estado** y dejar la lista de depuración.
9. **Release para Juan** (2026-11-04, usuario, a pedido de Juan) — unir el PR #267 con el build en verde y publicar
   `acero-zapatas-v0.1.0`, para que Juan lo instale desde el gestor. Se adelanta a la prueba con Santiago de #255, que
   sigue pendiente: lo que salga se corrige con una versión nueva.

## Cómo probarlo

```powershell
dotnet test addins/acero-zapatas/calculo/CQEING.Zapatas.Calculo.Tests -c Release
dotnet build addins/acero-zapatas/CQEING.Zapatas.csproj -c Release -p:RevitVersion=2026
dotnet build addins/acero-zapatas/CQEING.Zapatas.csproj -c Release -p:RevitVersion=2027
dotnet test CQEING-Addins.sln -c Release

Get-Process -Name Revit -ErrorAction SilentlyContinue   # no debe devolver nada
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin acero-zapatas -Revit 2026
```

Con Revit abierto, la DLL de `bin\Debug\2026` queda bloqueada: para revisar que compila, usar `-c Release`.

## Estado

- [x] Pasos 1 a 7 implementados (2026-11-04), versión prefinal.
- [ ] Probado en Revit, en parte. El usuario confirmó (2026-11-04) que el tablero se ve y que los cambios se aplican en el
      modelo. La lista de depuración de abajo no se confirmó punto por punto, y las combinadas siguen sin probar.
- [ ] Paso 9, release `acero-zapatas-v0.1.0`: en curso; es prefinal y la prueba con Santiago (#255) sigue pendiente.

Comprobado (2026-11-04), sobre `main` en `50d0db3`: compila en Release para 2026 y 2027 con 0 advertencias;
`dotnet test CQEING-Addins.sln -c Release` da 1573 pruebas correctas en 7 proyectos, 46 de ellas de zapatas; `node --check`
pasa en `zapatas.js`.

### Lista de depuración en Revit

1. Abrir el tablero: lista, planta, detalles y catálogo con los datos del modelo.
2. Una zapata cuadrada de 1800 × 1800: generar y medir cantidad, separación, recubrimiento y patas en una sección.
3. Una rectangular de 900 × 2000: la barra paralela al lado largo va abajo, y arriba hacia afuera.
4. Parrilla superior: gancho al revés, media separación corrida y sin choques con la inferior ni con las patas.
5. Sentido del reparto en X y en Y, y que el tablero no reporte barras corridas.
6. Volver a generar: reemplaza sin duplicar, y no toca las barras a mano de 780849, 780858, 780869 y 780981.
7. Elegir iguales: aplicar a varias de un clic; con zapatas distintas debe impedirlo.
8. Cambiar el recubrimiento global y por cara: la zapata y las barras lo reflejan, y los tipos existentes no cambian.
9. Una forma elegida del catálogo (17) y los ganchos de 135° y 180°.
10. Ctrl+Z deshace la generación completa de una vez.

Riesgos conocidos, sin resolver:

- **Combinadas y formas atípicas:** el motor las prueba, pero este modelo no las trae; hace falta otro modelo.
- **Ganchos de 135° y 180°:** el resumen usa el largo del cuadro y Revit el del tipo elegido, así que pueden diferir.
- **Barras de la parrilla con h pequeña:** con h 350 y #5, la segunda capa queda con pata de 184 mm, bajo los 191 mm de 12
  diámetros, y avisa. Es lo que pide la decisión 12; hay que confirmar con Juan si ese aviso es el esperado.
- **Revit 2027:** compila, sin probar.
