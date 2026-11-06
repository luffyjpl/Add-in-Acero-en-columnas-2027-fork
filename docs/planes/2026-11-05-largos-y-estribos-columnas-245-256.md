# Plan: largos redondos por cotas y estribos a su separación exacta en columnas (#245, #256)

## Contexto

- **#245**: en el despiece de columnas (#227), modelo `Prueba 08072026`, columna de los ejes 1 y 6, sale `4 #4 L= 3.31/3.53`: 3.31 m de tramo recto + gancho de 0.22 = 3.53. Con el redondeo de la ventana se esperaba un largo redondo.
- **#256**: misma prueba, columna circular 781574. Las zonas que deberían ir a **0.14** salen `15 #3 @0.13` (centradas a 1.66 y 5.16 m).
- Los dos issues son de Luciano (04-nov). Los dos se resuelven **igual que vigas** (Luciano, 05-nov): vigas ya tuvo los mismos dos problemas y los arregló en el commit `e993767` (Juan, 30-oct, por observaciones de Santiago del 28 y 30-oct).
- Fuera de alcance: el despiece (`addins/despiece`) **no se toca**. Ya mide "por cotas" desde `e993767` y es compartido con vigas.
- Entorno: Revit 2027, rama `columnas/largos-estribos-245-256` (worktree `C:\Proyecto\addins-245`, creada desde `origin/main` 3671e88).

## Criterios de aceptación

- [ ] #245: en una columna con gancho sobre zapata, el despiece muestra recto + pata que **suman un múltiplo del redondeo** elegido (por ejemplo `3.28/3.50` con redondeo 0.50).
- [ ] #245: la pata del gancho sigue midiendo el B del cuadro por fuera del doblez (lo que arregló #177: 0.222 en la #4).
- [ ] #256: cada zona de estribos va **a la separación pedida exacta**, con todos los espacios iguales. Lo que sobra va al borde de arriba (decisión 6).
- [ ] #256: el despiece de la columna 781574 muestra las zonas centrales `@0.14`.
- [ ] La vista previa (ventana / tablero) muestra los mismos estribos que se generan.
- [ ] `dotnet test CQEING-Addins.sln` sin fallos y compilación sin advertencias.

## Hallazgos del código y de Revit real

### #245: el largo redondo es el de corte, no el de las cotas

- `Modelo/PlanTraslapos.cs:225`: el plan calcula con `varilla.AporteGancho90`, lo que el gancho suma al **largo de corte** (por el eje de la barra). En la #4 son **0.1903 m**.
- `Modelo/CuadroVarillas.cs:75` (`AporteGancho90`, de #177): arco por el eje + cola − radio − d/2, para que la pata **por fuera del doblez** mida el B del cuadro (0.222 en la #4).
- Entonces el plan deja un corte de 3.31 + 0.19 = **3.50**, que sí es redondo. El despiece muestra **cotas**: recto + pata por fuera = 3.31 + 0.222 = 3.53. La diferencia es la esquina del doblez: `2 (R + d/2) − R·π/2`, unos 3 cm por gancho.
- Vigas, `e993767`: *"A 90 grados el gancho suma la B redondeada y la pata por fuera la mide: 2.70 + 0.30 = 3.00, antes 2.77 + 0.27 = 3.04 (Santiago, 28-oct). El largo de Revit queda 3 cm por debajo por la esquina, así que el aviso de diferencias y la etiqueta del despiece miden por cotas."* (`Reglas.LargoGancho`, `GanchoPropio.DiferenciaCotas`, `despiece/Revit/LectorDespiece.cs:LargoBarra`).
- Lo que se crea en Revit sale de `Pieza.ZDesde/ZHasta` (`Logica/GeneradorLongitudinal.cs:529`), no de `LargoCorte`. El gancho real lo calibra `Logica/GanchoLongitudinal.cs:84` contra `AporteGancho90`, y eso **no cambia**: la pata sigue midiendo B.

### #256: Revit reparte parejo "sin pasarse"

- `Logica/GeneradorArmado.cs:503`: cada grupo (uniforme, o confinado inferior / central / superior) es **un** `Rebar` con `SetLayoutAsMaximumSpacing`. Revit pone n = ⌈L/s⌉ + 1 estribos parejos de borde a borde, así que la separación real es L/⌈L/s⌉ ≤ s. Con s = 0.14 da entre 0.131 y 0.14.
- `Modelo/RepartoEstribos.cs` repite esa cuenta sin Revit para la vista previa (`ClasificadorPrevia3D.cs:234`, `Tablero/ArmadorVistaColumnas.cs:248`). Las posiciones que usan los estribos menores salen de `GeneradorArmado.RegistrarPosiciones` → `PaqueteEstribos.PosicionesDelGrupo` (también parejas).
- Vigas, `e993767` (`DistribuidorEstribos.Repartir`): *"cada separación exacta, con lo que sobra en el último espacio. Si sobra menos de `EspacioMinimo` (50 mm), los dos últimos espacios se igualan para que no queden dos estribos casi juntos: ninguno pasa de la separación."* Las zonas junto a la columna van exactas desde su columna, y el centro va exacto desde el último estribo de la zona de un lado, con el sobrante del otro lado. En Revit, cada tramo de separación constante es un conjunto con `SetLayoutAsNumberWithSpacing` y uno solo con `SetLayoutAsSingle` (`acero-vigas/Revit/CreadorEstribos.cs:55`). Los dos métodos son públicos y documentados, y ya se probaron en Revit con vigas.
- **armado-columnas no tiene proyecto de pruebas.** La lógica pura (`Modelo/`) se comprueba compilando y en Revit. El proyecto de pruebas queda para el issue #270 (decisión 4).

### Chequeo numérico sin Revit (2026-11-05)

Programa temporal en el scratchpad (no queda en el repo; las pruebas de verdad son el #270) que enlaza `Modelo/*.cs`:

- `Repartir(0, 1.89, 0.14)`: 13 espacios de 0.14 y uno de 0.07 (antes 14 de 0.135). `Repartir(0, 1.85, 0.14)`: sobraban 0.03 → los dos últimos quedan en 0.085.
- Tramo confinado de 3.00 m (L1 0.50, 0.06 / 0.14), sin menores: 9 + 13 + 9 = 31 estribos, todos distintos y en orden. Las corridas son `9 @0.06`, `13 @0.14`, `9 @0.06`. El último espacio central es de 0.11, junto al confinado de arriba. El espacio más grande es de 0.14.
- Uniforme de 2.70 @0.20: `13 @0.20` + 1 suelto, a 0.19 del anterior.
- `PlanTraslapos`, #4 con gancho sobre zapata, redondeo 0.10 y 0.50, alturas 3.00 y 6.37: en todas las piezas, recto + 0.222 = `LargoCorte` = múltiplo del redondeo (por ejemplo 2.278 + 0.222 = 2.50).
- Ojo: con redondeo 0.50 y una columna de un solo piso, cambia **qué** alturas no tienen solución, porque la varilla mide 3 cm más por gancho. Antes fallaban 2.80, 3.00 y 3.10. Ahora fallan 3.00 y 3.50. No falla más seguido que antes, y la ventana ya muestra el motivo, como siempre.

### Segunda ronda, con las decisiones 6 y 7 (2026-11-05)

Mismo programa temporal.

**Estribos**, sin menores, con los bordes a 5 cm:

| Tramo | Corridas | Último estribo al borde |
|---|---|---|
| Confinado, 3.00 m (L1 0.50, 0.06 / 0.14) | `9 @0.06` · `13 @0.14` · `9 @0.06` | 0.101 m |
| Uniforme, 2.70 m @0.20 | `13 @0.20` | 0.241 m |
| Confinado sin central, 0.90 m @0.10 | `8 @0.10` | 0.141 m |
| Uniforme, 0.14 m @0.10 (no entra ni un espacio) | dos estribos, uno en cada borde | 0.05 m |

- Dentro de cada zona todos los espacios son iguales, sin espacios más cortos.
- **Ojo:** en un tramo uniforme, lo que sobra puede llegar a casi una separación (hasta 0.20 con 0.20). Es lo que se eligió ("Uniforme: el sobrante queda arriba"), pero conviene confirmar que Santiago lo acepta así de grande.

**Estribos con la decisión 6b** (arrancar hasta 5 cm antes), mismos casos. Distancias a la cara del tramo:

| Tramo | Con la 6 | Con la 6b |
|---|---|---|
| Confinado, 3.00 m (L1 0.50, 0.06 / 0.14) | 31 estribos · abajo 5 cm · arriba 10.1 cm | 32 (`9 @0.06` · `13 @0.14` · `10 @0.06`) · abajo 4.1 cm · arriba 5 cm |
| Uniforme, 2.70 m @0.20 | 13 · abajo 5 · arriba 24 | 14 · abajo 4.1 · arriba 5 |
| Uniforme, 2.55 m @0.20 | 13 · abajo 5 · arriba 9 | igual (cerrar pedía adelantarse 16 cm) |
| Confinado sin central, 0.90 m @0.10 | 8 · abajo 5 · arriba 14.1 | 9 · abajo 4 · arriba 5 |

Barrido de 21 160 tramos:
- separaciones 0.10 a 0.25, confinadas de 0.05 a 0.10, alturas de 0.30 a 4.00 m cada 7 mm, uniforme y confinado;
- en ninguno se rompe nada: todos los espacios iguales dentro de cada zona y una sola corrida por zona;
- el primer estribo queda entre 0 y 5 cm de la cara de abajo;
- arriba, el peor caso deja el último a una separación del borde (0.25 con 0.25), cuando adelantar más de 5 cm no alcanza.

**Estribos con la decisión 9 (regla de vigas), y cómo los etiqueta el despiece.** El programa temporal enlaza también `despiece/calculo/ZonasEstribos.cs` y le pasa las alturas que salen:

| Tramo | Conjuntos de Revit | Lo que escribe el despiece |
|---|---|---|
| Confinado 3.00 (0.06 / 0.14) | `9 @0.06` · `13 @0.14` · `9 @0.06` | `9 #3 @0.06 · 13 #3 @0.14 · 9 #3 @0.06` |
| Confinado 3.03 (a la central le sobran 3 cm → se igualan los dos últimos) | `9 @0.06` · `13 @0.14` · 1 suelto · `9 @0.06` | `9 @0.06 · 13 @0.14 · **2 #3 @0.07 · 8 #3 @0.06**` |
| Uniforme 2.70 @0.20 (último espacio de 0.191) | `13 @0.20` · 1 suelto | `14 #3 @0.20` (lo junta: difiere menos de 1 cm) |
| Uniforme 2.55 @0.20 (sobraban 4 cm → se igualan) | `12 @0.20` · `2 @0.12` | `12 #3 @0.20 · 2 #3 @0.12` |

Hallazgos:
- El despiece de columnas no etiqueta por conjunto de Revit: junta estribos seguidos con la misma separación, ±1 cm. Vigas sí etiqueta por conjunto (plan #224).
- Cuando se igualan los dos últimos espacios de la zona central, el estribo suelto se junta en el despiece con el primero del confinado de arriba. Sale `2 #3 @0.07 · 8 #3 @0.06` en vez de `1 #3 · 9 #3 @0.06`. Es un efecto de `ZonasEstribos.Agrupar`, no del reparto: en Revit cada estribo está donde corresponde.

**Traslapos**: se compararon 745 casos contra la versión que solo tiene el cambio de #245, sin el reintento del B:
- redondeos 0.10, 0.25, 0.50 y 1.00;
- columnas de 1, 2 y 3 pisos, de 2.50 a 4.00 m cada 5 cm;
- con y sin gancho;
- #4.

Resultados:
- sin solución: de 120 casos a 67;
- **0 planes que ya tenían solución cambiaron**, y 0 casos que antes tenían solución ahora fallan;
- los 67 que quedan son casi todos columnas de un piso con redondeo 0.50 o 1.00: el caso de la decisión 8 (una sola varilla con las patas más largas).
- Ejemplo: un piso de 2.50 con redondeo 0.10 antes no tenía solución para el A. Ahora el B queda en 1.00 + 2.70, con el traslapo desde 0.02 m, y el A en 1.80 + 1.90, con el traslapo desde 0.82 m.

## Decisiones

1. **#245 igual que vigas: el múltiplo lo cierran las cotas** (2026-11-05, Luciano). Recto + pata por fuera (B) = múltiplo del redondeo. El largo de corte real queda unos 3 cm por gancho por debajo. El despiece no se toca. *Antes, en esta misma conversación, Luciano había elegido que el despiece mostrara el largo de corte. Lo cambió al saber que vigas y Santiago decidieron lo contrario y que el despiece es compartido.*
2. **#256 igual que vigas: separación exacta + sobrante en un último espacio más corto** (2026-11-05, Luciano). *Se reemplazó por la 6 y la 6b, y Luciano volvió a esta el mismo día (decisión 9).*
3. **Cada tramo de separación constante es un conjunto de Revit** (2026-11-05, Claude, como vigas): `NumberWithSpacing` si tiene más de uno, `Single` si es uno. Así el despiece lee cantidad y separación de cada conjunto.
9. **Vuelta a la regla de vigas (decisión 2)** (2026-11-05, Luciano: "mejor hazlo como vigas y dejar el último con espacio más corto"). Todos a la separación exacta desde 5 cm. Lo que sobra queda en un último espacio más corto, y si es menor a 5 cm se igualan los dos últimos espacios.
   - Confinado de abajo: exacto desde abajo, hasta donde entre en L1.
   - Confinado de arriba: exacto desde arriba, hasta donde entre en L1.
   - Zona central: exacta desde el último del confinado de abajo; el espacio corto queda contra el primero del confinado de arriba.
   - **Cómo se ve en el despiece** (`despiece/calculo/ZonasEstribos.Agrupar` junta estribos seguidos con la misma separación, ±1 cm):
     - confinado: el espacio corto queda **entre** dos etiquetas (`N @0.14` y `M @0.06`), sin etiqueta propia;
     - uniforme y confinado sin central: el último estribo sale con etiqueta propia (`1 #3`), o `2 #3 @x` si se igualaron los dos últimos. En Revit es un conjunto aparte (`Single`), como en vigas.
10. **El despiece no empareja un estribo suelto con la zona de al lado** (2026-11-05, Luciano: "prefiero el del despiece", "que funcione solo para columnas").
   - El problema: cuando se igualan los dos últimos espacios de la central, `ZonasEstribos.Agrupar` juntaba ese estribo suelto con el primero del confinado de arriba y escribía `2 #3 @0.07 · 8 #3 @0.06` en vez de `1 #3 · 9 #3 @0.06`.
   - No se vuelve a "una etiqueta por conjunto de Revit": eso es lo que #227 (hallazgo 29) cambió, porque en modelos con cada estribo suelto llenaba la vista de `1 #3`.
   - El arreglo: al empezar una zona con dos estribos, si el que sigue no continúa esa separación pero sí arranca una zona pareja con el de después, el primero queda solo.
   - Solo afecta a columnas: `ZonasEstribos.Agrupar` lo usa únicamente `despiece/Revit/Columnas/GeneradorColumna.cs` (verificado con grep el 05-nov); el despiece de vigas etiqueta por conjunto y no pasa por aquí.
11. **El estribo suelto se queda como en vigas** (2026-11-05, Luciano: "dejémoslo como está"). Se miraron otras formas de agruparlo:
   - sumarlo a la central en el despiece (`14 #3 @0.14`), aunque el último espacio real sea más corto;
   - repartir lo que falta en los últimos espacios: no sirve, porque en la mitad de los casos casi toda la central queda a 0.13, que es el error de #256;
   - llenar lo que sobra con estribos de confinamiento y llevar el resto a las puntas (hasta 8 cm del borde).
   Vigas (`main` al 05-nov) tiene el mismo comportamiento: un conjunto aparte en Revit, `1 #3` en su despiece y la cuenta sumada a la central en el tablero. Se deja igual para que los dos add-ins hagan lo mismo; si se cambia, que sea para vigas y columnas a la vez, con Santiago.
4. **Pruebas automáticas, después** (2026-11-05, Luciano: "por ahora no hagas los test, pero déjalo anotado"): queda en el issue #270 (proyecto xUnit sin Revit para `Modelo/`). En este trabajo se comprueba compilando y en Revit.
5. **Cada zona lleva sus propios estribos** (2026-11-05, Claude): `RepartoEstribos.Zona` tiene `Alturas`. Los rangos `ZDesde`/`ZHasta` siguen siendo los configurados (L1 en cada punta), para que la etiqueta "L1" de la vista previa no cambie. La vista previa (`ArmadorVistaColumnas`) toma los estribos de cada zona directo de ahí. `ClasificadorPrevia3D` ubica cada estribo leído en la zona del estribo del reparto más cercano, porque con el sobrante las zonas ya no cortan justo en L1. Antes usaba el rango con 3 cm de holgura.
6. ~~**#256: todos los espacios iguales; el sobrante va al borde de arriba, chico**~~ *(descartada, decisión 9)* (2026-11-05, Luciano). Santiago le dijo a Juan que no importa que el último estribo quede a más de 5 cm del borde, con tal de que todos queden con la misma separación. Ese cambio **todavía no está en vigas**: en `main`, `DistribuidorEstribos.Repartir` deja el último espacio más corto. Queda así:
   - uniforme y "confinado sin zona central": desde el de abajo, cada `s`, tantos como entren; lo que sobra (< `s`) va al borde de arriba;
   - confinado inferior: desde el de abajo, cada `sc`, tantos como entren en L1;
   - zona central: desde el último del confinado inferior, cada `sn`, tantos como entren dejando lugar para el confinado de arriba;
   - confinado superior: desde el último de la central, cada `sc`, tantos como entren hasta el borde. Lleva al menos los de L1, y si sobra lugar se agregan confinados. Así, lo que sobra arriba es menor que `sc` (con 0.06, el último queda a menos de 11 cm del borde);
   - no quedan espacios distintos dentro de una zona, así que cada zona es un solo conjunto de Revit;
   - si el tramo es más corto que una separación, van dos estribos, uno en cada borde, como antes.
6b. ~~**Corrección de la 6: se arranca un poco antes de los 5 cm**~~ *(descartada, decisión 9)* (2026-11-05, Luciano: "en realidad se refiere a que empiece un poco antes de los 5 cm"). Sobre el reparto de la decisión 6: si con un estribo más (a la separación de la última zona) el reparto cierra justo a 5 cm del borde de arriba bajando todo **5 cm o menos**, se agrega ese estribo y todo baja. El primero queda entre 0 y 5 cm de la cara de abajo. Si haría falta bajar más, queda como en la 6, con el sobrante arriba. Vigas no tiene esto: no hay cambios en `DistribuidorEstribos` después de `e993767`, en ninguna rama, al 05-nov.
7. **Correr el traslapo del B cuando el A no entra** (2026-11-05, Luciano: "si en otros casos es posible hacer lo que dijiste pues mejor, mientras que no rompa lo que ya hay"). Cuando el A no tiene tramos propios y no entra en los huecos que deja el B centrado, se prueba el B con el traslapo lo más abajo posible y después lo más arriba posible, y se queda el primero en que entran los dos. Solo se usa cuando hoy no hay solución, así que los planes que ya salen no cambian.
8. **Columna de un piso en una sola varilla, subida al múltiplo y con el sobrante repartido en las dos patas** (2026-11-05, Luciano). Ejemplo: 3.00 + 2 × 0.222 = 3.44 → 3.50, cada pata 0.25. Cambia la regla del 17-oct (traslapo forzado). **Queda fuera de esta rama**: en Revit el largo de la pata se fija por tipo de barra y tipo de gancho (`RebarBarType.SetHookLength`, `GanchoLongitudinal`), no por varilla, así que hay que verificar en la API y en Revit cómo alargar la pata de una sola varilla antes de programarlo. Issue #271.

## Plan paso a paso

1. **#245: el plan de traslapos usa B** — archivo: `addins/armado-columnas/Modelo/PlanTraslapos.cs`. En `Resolver`, la `Busqueda` recibe `varilla.Gancho90` (B) en vez de `varilla.AporteGancho90`. Se actualizan los comentarios: `Pieza.LargoCorte` pasa a ser "largo por cotas (recto + pata por fuera)", con referencia a #245 y `e993767`. `CuadroVarillas.AporteGancho90` y `GanchoLongitudinal` quedan igual. Cómo se prueba: compila. A mano, en la #4 con redondeo 0.50, la pieza de abajo de la columna del issue da recto 3.28 + 0.222 = 3.50.
2. **#256: reparto exacto sin Revit** — archivo: `addins/armado-columnas/Modelo/RepartoEstribos.cs`. *Hecho primero con la decisión 2 y rehecho con la 6.*
   - `Repartir(desde, hasta, s)`: desde `desde`, cada `s`, tantos como entren hasta `hasta`. El sobrante queda arriba, y si no entra ni un espacio van los dos bordes.
   - `record Corrida(Nombre, ZPrimero, Cantidad, Separacion)`, partida en tramos de separación constante; el generador crea un conjunto por corrida.
   - `Calcular` usa las reglas de la decisión 6 y devuelve `Alturas`, `Zonas` (con sus `Alturas`, decisión 5) y `Corridas`.
   - Se borra `CantidadEnGrupo` y su cuenta ⌈L/s⌉.
   - Cómo se prueba: compila, y el chequeo numérico da todos los espacios iguales a `s` (o a `sc` en los confinados) y un sobrante arriba menor que `sc`.
3. **#256: el generador crea las corridas** — archivo: `addins/armado-columnas/Logica/GeneradorArmado.cs`.
   - `GenerarTramo` llama a `RepartoEstribos.Calcular` y, por cada corrida, a `CrearGrupo`.
   - `CrearGrupo` pasa de `SetLayoutAsMaximumSpacing` a `SetLayoutAsNumberWithSpacing(cantidad, s, true, true, true)` o a `SetLayoutAsSingle()`.
   - `RegistrarPosiciones` recibe las alturas reales de la corrida.
   - El aviso de "la vista previa calculó X y Revit puso Y" compara contra `Corrida.Cantidad`.
   - Los rótulos de grupo siguen siendo los de hoy (`confinado inferior`, `zona central`...).
   - Cómo se prueba: compila sin advertencias. `dotnet test CQEING-Addins.sln` sin fallos.
4. **Revisar a los demás que usan el reparto** — archivos: `Logica/ClasificadorPrevia3D.cs`, `Tablero/ArmadorVistaColumnas.cs`, `Modelo/PaqueteEstribos.cs`. Que sigan funcionando con las zonas nuevas y no queden usos de `CantidadEnGrupo` ni de `PosicionesDelGrupo` sin sentido. Cómo se prueba: compila. La vista previa del tablero muestra los mismos estribos que se generan.
4b. **Correr el traslapo del B** (decisión 7) — archivo: `Modelo/PlanTraslapos.cs`. `Busqueda` recibe una preferencia (centrado, que es la de hoy; abajo; arriba) que cambia el orden del costo después de la cantidad de traslapos. `CalcularCon` reintenta el B con abajo y con arriba solo si algún A falla. Cómo se prueba: compila, y en el chequeo numérico, una columna de un piso de 3.00 m con #4 y redondeo 0.50 tiene solución. Las columnas que ya tenían solución dan el mismo plan que antes.
4c. **Estribo suelto en el despiece** (decisión 10) — archivos: `addins/despiece/calculo/CQEING.Despiece.Calculo/ZonasEstribos.cs` y `.../CQEING.Despiece.Calculo.Tests/ZonasEstribosTests.cs`. En `Agrupar`, antes de emparejar un estribo solo con el siguiente, se mira el de después. Prueba nueva con central @0.14, un suelto con los dos últimos espacios iguales y confinado @0.06: tiene que dar `13 @0.14 · 1 · 9 @0.06`. Cómo se prueba: `dotnet test CQEING-Addins.sln` con las pruebas viejas de `ZonasEstribosTests` sin cambios.
5. **Prueba en Revit** (abajo) y cierre del plan con evidencia en `docs/planes/imagenes/`.

## Cómo probarlo

```
dotnet build addins/armado-columnas/practicacq.csproj -c Debug -p:RevitVersion=2027
dotnet test CQEING-Addins.sln
Get-Process -Name Revit -ErrorAction SilentlyContinue   # tiene que no devolver nada
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin armado-columnas -Revit 2027
```

1. Abrir `Prueba 08072026` y regenerar la columna de los ejes 1 y 6 con el redondeo de 0.50. En el despiece, la #4 de abajo tiene que verse `L= 3.28/3.50` (o recto + 0.22 = múltiplo de 0.50) y la pata tiene que seguir midiendo 0.222.
2. Regenerar la columna circular 781574. En el despiece, las zonas centrales tienen que verse `@0.14`, con una sola etiqueta por zona (sin estribos sueltos). Medir en Revit, con 3 decimales, la separación de una zona central y la distancia del último estribo al borde de arriba.
3. Probar una columna de un piso que antes daba "sin solución" en la pestaña «Traslapos» (por ejemplo, 2.50 m con redondeo 0.10): ahora tiene que dar un plan, y una de varios pisos tiene que dar el mismo plan que antes.
4. Comprobar que la vista previa (ventana o tablero) muestra la misma cantidad de estribos que se generó, y que no aparece el aviso "la vista previa calculó…".
5. Comprobar que los estribos menores y las grapas siguen a la altura de su estribo principal, incluido el último de cada tramo.
6. En una columna donde se igualaron los dos últimos espacios de la zona central, el despiece tiene que mostrar el estribo suelto como `1 #3` y el confinado de arriba completo (`9 #3 @0.06`), no `2 #3 @0.07`.

## Estado

- [x] Plan revisado con el usuario (2026-11-05: "dale arranca"; pruebas automáticas al #270).
- [x] Pasos 1 a 4 implementados (2026-11-05, sin commit):
  - `Modelo/PlanTraslapos.cs`: el gancho cuenta su B;
  - `Modelo/RepartoEstribos.cs`: reescrito, con `Repartir`, `Corrida` y `Zona.Alturas`;
  - `Logica/GeneradorArmado.cs`: una corrida = un `Rebar` con `NumberWithSpacing` o `Single`;
  - `Modelo/PaqueteEstribos.cs`: `PosicionesDelGrupo` sin `incluirUltimo`;
  - `Logica/ClasificadorPrevia3D.cs` y `Tablero/ArmadorVistaColumnas.cs`: usan `Zona.Alturas`;
  - comentarios en `ConfigLongitudinal.cs` y `GeneradorLongitudinal.cs`.
- [x] Compila con 2026 y 2027, 0 advertencias. `dotnet test CQEING-Addins.sln`: todo en verde (1582 pruebas; columnas no tiene pruebas propias).
- [x] Decisiones 6 y 7 implementadas (05-nov):
  - `RepartoEstribos.cs` rehecho con la regla 6 (`Repartir` hacia arriba, el confinado de arriba se completa hasta el borde);
  - `PlanTraslapos.cs`: `Preferencia`, `Busqueda.Comparar` y el reintento del B en `CalcularCon`.
  - Compila con 2026 y 2027 sin advertencias, `dotnet test` en verde, y está desplegado en 2027 desde `C:\Proyecto\addins-245`.
- [x] Issue de la decisión 8: #271.
- [x] Decisión 6b implementada (`RepartoEstribos.Cerrar`) y después quitada al volver a la regla de vigas (decisión 9).
- [x] Probado en Revit 2027 (paso 5, 05-nov): Luciano hizo los pasos 1 a 6 de «Cómo probarlo» y todo salió bien. Sin capturas.
- [x] Decisión 10 implementada (paso 4c, 05-nov): `ZonasEstribos.Agrupar` con `EmpiezaOtra` y dos pruebas nuevas en `ZonasEstribosTests`. La prueba del suelto falla con el agrupador anterior y pasa con el nuevo. Simulación de la columna de 3.03: `9 #3 @0.06 · 13 #3 @0.14 · 1 #3 · 9 #3 @0.06`. Los otros tres casos de la tabla de la decisión 9 no cambian. `dotnet test CQEING-Addins.sln` en verde (despiece: 110 pruebas). El despiece compila con 2026 y 2027 sin advertencias y está desplegado en 2027 desde `C:\Proyecto\addins-245`.
