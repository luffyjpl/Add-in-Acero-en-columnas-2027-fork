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
20. **Ejes de la zapata** (2026-11-04, Claude): salen del rectángulo más pequeño que envuelve el contorno, y X es el lado
    más cercano a la X del proyecto. Así se respeta «X abajo» en las cuadradas, también si están giradas.
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

26. **La franja de patas incluye el doblez** (2026-11-04, Claude, por la prueba automática; corrige la decisión 17): en la
    capa de arriba de cada parrilla, la barra de borde va dentro de la curva del doblez de la capa vecina:
    `recubrimiento + d de la pata + D de doblado / 2`. Con solo el grosor de la pata, en la `v0.1.0` el arco cruza esa barra
    (eje a eje 0.75 mm con #5). `D` es el del tipo de barra del proyecto, no el del cuadro: en el #3 son 63.5 y 57 mm.

### Hallazgos de la prueba automática (2026-11-04, Revit 2026.5.10)

Sobre una copia de `Prueba 080720sfsd26`. **Pasaron:** cantidad y punto de cada barra (0 mm de diferencia), patas hacia
adentro, volver a generar sin duplicar, barras a mano intactas, zapatas iguales (3 y 5) en una llamada, recubrimientos
60/70/50 sin cambiar el tipo `CIM`, forma 17 usada, y todo deshecho al final. **Falló:** el choque de la decisión 26. El
fallo de «dentro del recubrimiento» era de la prueba, que restaba medio diámetro en la punta de la barra. Las losas no se
probaron porque su tipo mide 150 mm.

Con la decisión 26 aplicada, tercera corrida (137 pasan, 5 fallan), con losas de prueba de 500 mm que se deshacen:

- **Sin choques:** cuadrada, rectangular y **combinada rectangular de 4.00 × 1.20 con 2 columnas**, y barras rectas. También
  pasan en esas formas el punto del plan, los recubrimientos y las patas.
- **Siguen chocando, sin resolver:**
  - **Forma en L:** 14 pares, hasta −8.9 mm. La franja de patas solo cubre los bordes extremos: en la esquina entrante, las
    patas de las barras que terminan ahí cruzan las de la otra dirección.
  - **Trapecio:** 18 pares, hasta −14.8 mm, y 2 barras muy cortas no se pudieron comparar con el plan (hasta 147 mm).
  - **Ganchos de 135° y 180° en h 350:** 4 pares (−2.1 mm) y 8 pares (−14.3 mm), entre la barra del borde y el gancho de la
    otra dirección. Falta medir la geometría real de esos ganchos.
- **Decisión 27** (2026-11-04, Claude): hasta resolverlos, el tablero avisa en esos casos y no los bloquea, porque el
  modelador puede corregirlos a mano. `v0.1.0` tiene además el choque de la decisión 26, así que este arreglo necesita una
  versión nueva para llegar a quienes la instalaron.

No probado en esta corrida: la interfaz del tablero, una combinada real del proyecto, y Revit 2027.

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

10. **Prueba automática en Revit** (2026-11-04, usuario pidió hacer las pruebas faltantes) — archivos:
    `Revit/PruebaAutomatica.cs`, solo en Debug — con la variable `CQEING_ZAPATAS_PRUEBA` apuntando a una copia del modelo,
    Revit la abre, genera, mide cada barra y lo deshace todo; informe en `%AppData%\CQEING\zapatas\prueba.txt`. No prueba la
    interfaz. Los arreglos van por PR a la rama `acero-zapatas` de upstream, no a `main`.

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
