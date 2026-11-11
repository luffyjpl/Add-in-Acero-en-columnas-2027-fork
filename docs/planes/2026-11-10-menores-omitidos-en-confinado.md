# Plan: estribos menores que faltan en los tramos confinados a 5 cm (columna 189817)

## Contexto

- Lo encontró Luciano el 10-nov probando el despiece (otra sesión, worktree
  `C:\Proyecto\addins-317`, rama `despiece/carlos-10-11`): en la columna
  **189817** del modelo "edificio tevening" las grapas (estribos menores) solo
  aparecen en los nudos. Palabras de Luciano: "no se están generando los
  menores bien, porque se supone están para toda la columna".
- El despiece no tiene la culpa: dibuja lo que hay en el modelo. El problema
  está en armado-columnas.
- Código afectado: el que está desplegado, de la rama
  `columnas/observaciones-c2` (worktree `C:\Proyecto\addins-c2`, último commit
  `f814bad`). `main` (d2949cf) está atrás y todavía no pone menores en los nudos.
- Fuera de alcance: el despiece, y cualquier otro cambio en el reparto de los
  estribos principales.
- Entorno: Revit 2027, .NET 10. Rama de trabajo `columnas/menores-separacion`,
  creada desde `columnas/observaciones-c2` (f814bad), en el worktree
  `C:\Proyecto\addins-menores` (así no se toca `addins-c2`).

## Criterios de aceptación

Ajustados a la decisión 1 (opción D):

- [ ] Una columna con estribos menores y una separación más chica que el
      paquete (189817: 5 cm < 5,7 cm) ya no se genera a medias: «Generar en
      Revit» muestra un diálogo que dice qué tramo, cuánto mide el paquete y
      qué separación hay, y solo deja «Volver a revisar» (no hay «Generar igual»).
- [ ] El mismo aviso aparece en la columna mientras se configura (avisos del
      resumen), antes de pulsar «Generar».
- [ ] Subiendo la separación a 6 cm (o quitando grapas) la columna se genera
      con menores en toda la columna (tramos libres y nudos).
- [ ] La columna B-3 (190961, confinado a 6 cm) se genera igual que hoy.
- [ ] `dotnet test CQEING-Addins.sln` sin fallos, `tools/PruebasVistaPrevia`
      en 0 fallas y compilación sin advertencias.

## Hallazgos del código y de Revit real

1. **Dónde están los menores (traza del despiece).** Archivo
   `%AppData%\CQEING\despiece\traza.log`, 10-nov 14:51:
   - `vigas a la derecha -0,90 a 0,00, 3,86 a 4,36, 7,51 a 8,01, 11,76 a 12,26, 15,61 a 16,11, 19,46 a 19,96`
   - `primeras piezas 0,083; 3,946; 3,946; 3,984; 3,984; 7,596; 7,596; 7,634; 7,634; 11,846; ...`
   - O sea, el principal arranca a 0,083 m, pero los menores aparecen recién a
     3,946/3,984 m, 7,596/7,634 m…: **todos dentro de las franjas de viga
     (los nudos)**. En los tramos libres no hay ninguno.

2. **Diferencia con B-3 (la que sí funciona).** Misma traza:
   - 189817: `zonas de estribos 17 #3 @0.05 en 0,48; 21 #3 @0.10 en 1,98; 16 #3 @0.05 ...; 4 #3 @0.10 en 4,11 (nudo) ...`
   - 190961 (B-3): `13 #3 @0.06 en 0,44; 19 #3 @0.12 en 1,99; 12 #3 @0.06 ...; 3 #3 @0.12 en 4,11 (nudo) ...`
   - La única diferencia que importa: el confinado de 189817 va a **5 cm** y el
     de B-3 a **6 cm**.

3. **Alto del paquete (cálculo, `Modelo/PaqueteEstribos.cs`, `Calcular`).**
   Con barra #3 (9,5 mm) en el principal y en las grapas, y una grapa X debajo
   y una grapa Y encima:
   - eje de la grapa X = −(1,5·9,5) − 9,5/2 = **−19 mm**; eje de la Y = 9,5/2 + 1,5·9,5 = **+19 mm**
     (coincide con la traza: 3,946 – 3,965 – 3,984).
   - cara de abajo = −19 − 1,5·9,5 = −33,25 mm; cara de arriba = 19 + 4,75 = 23,75 mm.
   - **Alto del paquete = 57 mm = 5,7 cm.** El "piso extra" de 1,5 diámetros
     por abajo es el que eligió Luciano en #130 (Revit baja un diámetro el
     gancho de cierre).

4. **Por qué se omiten (causa).** `addins-c2`,
   `addins/armado-columnas/Logica/GeneradorLongitudinal.cs:941-949`: si la
   separación MÍNIMA entre estribos de un tramo es menor que el alto del
   paquete, se omiten los menores de **todo el tramo** (solo queda un aviso al
   final de la generación):
   - Tramo libre confinado a 5 cm: 5 cm (o menos: `SetLayoutAsMaximumSpacing`
     reparte a una separación ≤ 5 cm) < 5,7 cm → **sin menores en todo el
     tramo**, incluida la zona central a 10 cm, donde sí entrarían.
   - Nudo uniforme a 10 cm: 10 > 5,7 → con menores. Desde el commit `3de0195`
     (6-nov, "grapas en los nudos") los nudos llevan menores; antes tampoco
     aparecían ahí.
   - B-3, confinado a 6 cm: 6 > 5,7 → con menores en toda la columna.

5. **La ventana no lo avisa antes.** Nadie fuera del generador compara
   `Paquete.Alto` con la separación (grep de `.Alto` en `addins/armado-columnas`
   del worktree c2: solo `GeneradorLongitudinal.cs:944`).

6. **Pendiente de medir en Revit real:** que en el modelo los Rebar de grapa
   de 189817 estén solo en los nudos (los ids de la traza, 442838/442881/442859,
   ya no existen: se volvió a generar), y leer en los avisos de la generación
   el texto "el paquete de estribos menores mide 5,7 cm y la separación es de
   5 cm; se omiten los estribos menores de este tramo". Se hace con el paso 1.

7. **Falla vieja en `tools/PruebasVistaPrevia` (10-nov).** Ya en
   `observaciones-c2` (f814bad), antes de este cambio:
   `FALLA  choques_tramo_dentro_de_viga: en el nudo no van menores`. La prueba
   espera la regla vieja; el commit 3de0195 (6-nov) puso menores en los
   nudos. No es de este plan: se deja como está y se avisa a Luciano.

## Decisiones

1. **Qué hacer cuando el paquete no entra en la separación** (2026-11-10,
   Luciano): **opción D**. No se genera; la ventana avisa antes de generar y
   pide subir la separación o cambiar las grapas. Opciones que se evaluaron:
   - A. Por zona y no por tramo: en la zona confinada a 5 cm no van menores,
     en la zona central a 10 cm sí. Arreglo chico, pero la zona confinada
     (la que más los necesita) queda sin grapas.
   - B. En la zona confinada, menores en un estribo sí y otro no (cada 10 cm).
   - C. Paquete más bajo cuando no entra: sacar el "piso extra" de #130 (el
     paquete bajaría a ~2,9 cm y entraría a 5 cm), aceptando que la punta de
     la grapa quede a la altura del gancho de cierre del principal.
   - D. No generar y pedir en la ventana que se suba la separación (o se
     cambien las grapas) antes de generar.
   La que se elija tiene que ser una regla de obra; esto no lo decide el
   código.

## Plan paso a paso

Diseño (opción D): la comprobación es una sola función sin Revit, la misma
cuenta del generador (`GeneradorLongitudinal.cs:941-944`: separación mínima
entre posiciones seguidas contra `Paquete.Alto`), aplicada a las alturas de
`RepartoEstribos.Calcular`, que son las mismas que usa `GeneradorArmado`.
La usan la vista de la columna (aviso) y «Generar» (bloqueo). El chequeo del
generador queda como está, de respaldo: con el bloqueo no debería volver a
dispararse.

1. **Confirmar en Revit real** (Luciano, con el add-in de hoy) — regenerar
   189817 y anotar acá el aviso «el paquete de estribos menores mide 5,7 cm y
   la separación es de 5 cm; se omiten…». Puede hacerse junto con el paso 6.
2. **Decisión 1** — hecha (opción D, 10-nov).
3. **Prueba que falla** — `tools/PruebasVistaPrevia/Program.cs`: caso 189817
   (#3, confinado L1 0,50 / 0,05 / central 0,10, grapa C de fila + grapa C de
   columna) → `PaqueteEstribos.SeparacionQueNoEntra` da 0,05; caso B-3
   (0,06 / 0,12) → null; sin menores → null. No compila hasta el paso 4.
4. **Función compartida** — `Modelo/PaqueteEstribos.cs`:
   `SeparacionQueNoEntra(alturas, paquete)` y `MotivoNoEntra(separacion, paquete)`
   (texto: mide X cm, separación Y cm, «sube la separación a N cm o más, o
   cambia las grapas»). Las pruebas del paso 3 pasan.
5. **Aviso en la columna** — `Tablero/ArmadorVistaColumnas.cs`, `Zonas`:
   después de `RepartoEstribos.Calcular`, si no entra, `Tramo N: <motivo>` en
   los avisos de la columna. Además `Bloqueos(...)`, pública, con la misma
   cuenta para todos los tramos de un plan.
6. **Bloqueo al generar** — `Tablero/SesionColumnas.cs` (`Generar`: antes de
   los choques y también con «Generar igual»; `cancelarGenerar` lo limpia),
   `Tablero/VistaColumnas.cs` (`GeneracionVista.Bloqueos`),
   `Tablero/web/columnas.js` (diálogo «No se puede generar todavía» con solo
   «Volver a revisar»), `Tablero/web/demo.js` (`bloqueos: []`).
7. **Build, pruebas, deploy y prueba en Revit** (ver abajo), con 189817 y B-3.

## Cómo probarlo

```
dotnet build addins/armado-columnas/practicacq.csproj -c Debug -p:RevitVersion=2027
dotnet test CQEING-Addins.sln
Get-Process -Name Revit -ErrorAction SilentlyContinue   # tiene que no devolver nada
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin armado-columnas -Revit 2027
```

```
dotnet run --project tools/PruebasVistaPrevia   # tiene que terminar en "0 fallas"
```

1. Modelo "edificio tevening", columna 189817 con el confinado a 5 cm: la
   columna muestra el aviso; «Generar en Revit» abre el diálogo de bloqueo y
   no crea nada.
2. Subir el confinado a 6 cm: el aviso desaparece, se genera, y hay menores
   en los tramos libres y en los nudos.
3. B-3 (190961): se genera igual que antes.
4. Despiece de 189817: "primeras piezas" tiene que arrancar cerca del pie
   (como B-3: 0,064; 0,064; 0,083; 0,102; 0,102).

## Estado

- [x] Causa encontrada en el código y en la traza (Hallazgos 1-5), 10-nov.
- [ ] Confirmado en Revit real (paso 1): no se hizo por separado; lo cubre
      la prueba del 11-nov (a 5 cm la ventana ahora bloquea).
- [x] Decisión 1 tomada (opción D), 10-nov.
- [x] Pasos 3 a 6 implementados (10-nov), en la rama `columnas/menores-separacion`:
      `Modelo/PaqueteEstribos.cs`, `Tablero/ArmadorVistaColumnas.cs`,
      `Tablero/SesionColumnas.cs`, `Tablero/VistaColumnas.cs`,
      `Tablero/web/columnas.js`, `Tablero/web/demo.js`,
      `tools/PruebasVistaPrevia/Program.cs`.
- [x] Build Debug 2027: 0 advertencias. `dotnet test CQEING-Addins.sln`:
      todo correcto. `PruebasVistaPrevia`: las 4 pruebas nuevas OK; sigue solo
      la falla vieja del hallazgo 7. `node --check` de `columnas.js` y `demo.js`: OK.
- [x] Deploy y prueba en Revit (paso 7): el 10-nov no se desplegó porque
      Revit 2026 estaba abierto. 11-nov: desplegado en 2027 y 2026 desde
      `C:\Proyecto\addins-menores` (Revit cerrado, comprobado con
      `Get-Process`; build sin advertencias). Luciano lo probó en Revit el
      11-nov: "funciona".
- Criterios: ✅ los cinco (los de la ventana y las pruebas, sin Revit; los de
  generar con 6 cm y B-3 igual, con la prueba de Luciano en Revit del 11-nov).
- Pendiente fuera de este plan: la falla vieja `choques_tramo_dentro_de_viga`
  de `tools/PruebasVistaPrevia` (hallazgo 7).
