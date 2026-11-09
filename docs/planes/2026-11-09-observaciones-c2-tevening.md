# Plan: observaciones de la prueba de C-2 en «edificio tevening» (09-nov)

## Contexto

- El 09-nov Luciano probó armado-columnas v1.8.1 en Revit 2027 con la columna C-2 del modelo
  «edificio tevening». Se lo contó a otra sesión de Claude (la del despiece, #317), que pasó las
  observaciones a esta. Capturas (en la PC de Luciano):
  `C:\Users\chave\Pictures\Screenshots\Captura de pantalla 2026-11-09 084117.png` (avisos) y
  `...084348.png` (botón apagado con B-3).
- Observaciones:
  1. Aviso «el paquete de estribos menores mide 5,7 cm y la separación es de 5 cm; se omiten los
     estribos menores» en los tramos 0, 2, 4, 6 y 8. Luciano quiere que se generen igual.
  2. Avisos «la barra longitudinal en (-8,809, 2,741) m atraviesa el gancho del estribo (9,1 mm)» y
     «una pieza … quedó en (-8,809, 2,741) m, 8,7 mm por fuera de las esquinas: atraviesa el estribo».
  3. Qué significa «Zona de traslapo no revisada en N varilla(s): llevan más de un traslapo».
  4. Después de generar C-2 se eligió B-3 («Sin configurar», con la misma configuración en el
     panel) y «Generar en Revit» quedó apagado hasta cambiar algún dato.
  5. (Santiago, PROYECTO THEVENIG 2, Revit 2026, captura de Luciano `...2026-11-09 144910.png` y hoja
     «Observaciones Addis», Obs_Add_Col C13) A-1 no se genera: «el elemento 908746 tiene una sección
     de 5 lados, no rectangular -- no soportado por ahora (#11)», repetido en los 10 tramos y en el
     longitudinal, cada vez con el stack trace pegado («|| at practicacq.Logica…»). Dice que solo le
     pasó en esa columna; en la vista previa A-1 se ve normal, 55 × 55.
- Fuera de alcance: la observación 1 queda para consultar con el equipo, en el issue #318
  (decisión 1). El aviso de grapas «fuera de su host» ya lo explica el add-in desde v1.8.1.
- Entorno: Revit 2027 (también compila en 2026), rama `columnas/observaciones-c2`, worktree
  `C:\Proyecto\addins-c2`, sobre `main` 472888a.

## Criterios de aceptación

- [ ] Una varilla con dos traslapos (tres piezas) no deja ninguna pieza fuera de las esquinas si hay
      una manera de correr la cadena que las deje dentro.
- [ ] Las varillas con un solo traslapo se acomodan igual que antes (mismas opciones).
- [ ] La nota de «zona de traslapo no revisada» se entiende sin saber cómo funciona el add-in.
- [ ] Una columna elegida «Sin configurar» se puede generar con lo que muestra el panel, sin cambiar
      ningún dato; las ya generadas no se vuelven a generar si no se tocan.
- [ ] Una columna rectangular con un lado partido en dos (en línea) se genera; una que de verdad no es
      rectangular da un aviso legible, sin stack trace.
- [ ] `dotnet test CQEING-Addins.sln` pasa y compila sin advertencias en 2026 y 2027.

## Hallazgos del código y de Revit real

1. **Observación 1** (`Logica/GeneradorLongitudinal.cs`, «Paquetes que se pisarían»): si la separación
   mínima entre estribos es menor que el alto del paquete de menores, el tramo va sin menores. Ver #318.

2. **Observación 2, medido con el MCP de Revit en el modelo abierto (09-nov)**. Los estribos de C-2
   (ids 442568-442587) van de X −28,975 a −27,597 pies y de Y 8,954 a 10,332 pies (por fuera). La
   barra de la esquina donde cierra el estribo (abajo a la izquierda en la sección) tiene TRES piezas,
   o sea dos traslapos:

   | Pieza | Forma | Centro Y (pies) | Centro Y (m) | Z (pies) |
   |---|---|---|---|---|
   | 442604 | Forma de armadura 2 | 8,993 | 2,741 | −2,69 a 22,30 |
   | 442624 | Forma de armadura 3 | 9,066 | 2,763 | 17,05 a 48,29 |
   | 442618 | Forma de armadura 2 | 8,993 | 2,741 | 43,76 a 65,49 |

   La posición pedida era Y = 2,752 m (9,029 pies). Revit dejó las piezas **a los dos lados** de la
   pedida, ±11 mm (medio diámetro de la #7), y ninguna en la pedida. La cara interior del estribo
   está en Y ≈ 8,985 pies, y la pieza de abajo y la de arriba llegan a 8,956: se meten 8,8 mm en el
   estribo, que es lo que dicen los dos avisos.

   Por qué no se arregla solo: `BarraContinua.Acomodar` (`Logica/BarraContinua.cs`) supone UN
   traslapo: un grupo de piezas en la posición pedida y otro corrido, y como alternativa corre la
   cadena para llevar el corrido a la pedida. Con dos traslapos los dos grupos están a la misma
   distancia de la pedida, la alternativa calculada (−22 mm) deja todo más afuera, se descarta, y
   queda lo que dejó Revit. Si la cadena se corre +11 mm, las piezas quedan en 2,752 y 2,774 m:
   dentro de las esquinas.

   Las otras cinco varillas de C-2 con tres piezas (442648, 442686, 442724, 442762 y 442801, con sus
   piezas del medio) quedan dentro porque tienen lugar hacia el lado al que Revit las corrió.

3. **Observación 3** (`Modelo/ChoquesSeccion.cs`, `NotaSinRevisar`): la vista previa calcula los
   choques de la zona de traslapo solo para las varillas con un traslapo (`unTraslapo`); las de más
   de uno se cuentan en `VarillasSinRevisarEnTraslapo` y se dice esa nota. El texto no dice qué hacer.

4. **Observación 4** (`Tablero/SesionColumnas.cs`): «Generar en Revit» genera solo las columnas en
   `_listas`, y una columna entra ahí solo al cambiarle un dato o aplicarle una configuración
   (`Cambiar`). Elegir una columna sin configurar muestra `_actuales` (`OpcionesDe`) pero no la pone
   en la lista, así que el botón queda apagado (`columnas.js`, `disabled=${!listas.length}`).

5. **Observación 5** (`Logica/GeometriaSeccion.cs`, `ObtenerContornoHost` y `ValidarRectangular`):
   la sección sale de la cara de ABAJO del sólido de la columna, y se exige que tenga exactamente 4
   lados rectos. No se pudo medir A-1 (el modelo de Santiago no está abierto en esta PC: el MCP no
   encuentra 908746). Lo más probable, porque la vista previa la ve rectangular (esa usa la caja de
   los elementos, `LectorContexto.ZonaComun`), es que un lado de esa cara esté partido en dos tramos
   en línea recta: pasa cuando otro elemento unido a la columna toca justo esa arista. Si fuera un
   chaflán o un recorte de verdad, la cara tendría 5 lados reales.
   Además, `GeometriaSeccion.Explicar` pega los primeros renglones del stack trace a TODO mensaje que
   no sea el «internal error» de Revit (era un diagnóstico temporal), y por eso el aviso es ilegible.

## Decisiones

1. **El paquete de menores se consulta con el equipo** (2026-11-09, Luciano): queda en el issue #318,
   con las opciones. No se toca en este plan.
2. **«Generar en Revit» también genera la columna elegida sin configurar** (2026-11-09, Luciano): se
   generan las de «Listas para generar» y además las elegidas (compatibles con la activa) que no están
   generadas ni en la lista, con lo que muestra el panel. Las ya generadas no se regeneran si no se tocan.
3. **Acomodo con más de un traslapo** (2026-11-09, Claude, por el hallazgo 2): las maneras de correr
   una cadena son quedarse como la dejó Revit o llevar cada grupo de piezas a la posición pedida.
   Solo valen las que dejan todas las piezas entre las esquinas; la de Revit se mantiene si ninguna
   otra vale. Con un traslapo son las mismas dos opciones que antes.
4. **Nota de la zona de traslapo** (2026-11-09, Claude): pasa a decir qué significa y qué hacer:
   «N varilla(s) llevan más de un traslapo: en la vista previa no se revisa si sus piezas de traslapo
   chocan con otras barras. Al generar, el add-in avisa si alguna queda tocando el estribo; revísalas
   en el modelo.»

5. **Lados partidos en línea recta se juntan** (2026-11-09, Claude, hallazgo 5): antes de validar,
   los lados seguidos que van en la misma dirección se unen en uno. Una cara de 5 «lados» con dos en
   línea pasa a ser el rectángulo que es; un chaflán o un recorte de verdad sigue sin soportarse, con
   un aviso que diga qué hacer.
6. **Sin stack trace en los avisos de errores propios** (2026-11-09, Claude, hallazgo 5): los
   mensajes que arma el add-in (`InvalidOperationException`) se muestran solos; el stack trace queda
   para errores inesperados de .NET, que son los que hace falta rastrear. Y si varios tramos de una columna fallan
   por el mismo motivo, se avisa una sola vez (`GeneradorArmado`).

## Plan paso a paso

1. **Acomodo con varios grupos** — `Logica/BarraContinua.cs` (`Acomodar`): por varilla, la lista de
   traslados candidatos (0 y uno por grupo de piezas), filtrada por las esquinas; opciones = esos
   traslados; se corre la cadena con el elegido si no es 0. Prueba: build sin advertencias; en Revit,
   regenerar C-2 y que no salgan los avisos de la observación 2.
2. **Nota de la zona de traslapo** — `Modelo/ChoquesSeccion.cs` (`NotaSinRevisar`). Prueba: `dotnet
   test` (si alguna prueba compara el texto, se actualiza).
3. **Generar la columna elegida sin configurar** — `Tablero/SesionColumnas.cs`: un único conjunto
   «para generar» (listas + elegidas sin configurar compatibles) que usan `OpcionesParaGenerar` y la
   vista (`GeneracionVista`). Prueba: en Revit, generar una columna, elegir otra sin configurar y que
   el botón esté prendido y la genere.
4. **Sección con lados en línea** — `Logica/GeometriaSeccion.cs`: juntar lados colineales antes de
   `ValidarRectangular`; texto del aviso de sección no rectangular con qué revisar; `Explicar` sin
   stack trace para `InvalidOperationException`; en `GeneradorArmado`, el mismo motivo una vez por
   columna. Prueba: build; en Revit, que A-1 de THEVENIG 2 genere
   (o, si es un recorte real, que el aviso salga una vez legible).
5. Build 2026 y 2027 sin advertencias y `dotnet test CQEING-Addins.sln`.

## Cómo probarlo

```
dotnet build addins/armado-columnas/practicacq.csproj -c Debug -p:RevitVersion=2027
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin armado-columnas -Revit 2027
```

1. En «edificio tevening», generar C-2: no deben salir los avisos de la barra en (-8,809, 2,741).
   Con el MCP, la varilla de la esquina donde cierra el estribo tiene que quedar con Y ≥ 8,985 pies
   + radio en todas sus piezas.
2. Si sale la nota de la zona de traslapo, que diga el texto nuevo.
3. Después de generar C-2, elegir B-3 (sin configurar): «Generar en Revit» prendido, con la cuenta en 1;
   generarla y que quede con ✓.
4. En PROYECTO THEVENIG 2 (Santiago), generar A-1 (elemento 908746).

## Estado

- [x] Plan revisado con el usuario (decisiones 1 y 2).
- [x] Pasos 1 a 5 implementados (09-nov). Build 2027 con `--no-incremental`: 0 advertencias, 0 errores;
      2026, compilado a una carpeta aparte porque Revit 2026 tenía la DLL abierta: 0 y 0.
      `dotnet test CQEING-Addins.sln`: todo pasa.
- Luciano (09-nov) confirma que lo de A-1 probablemente es por uniones, como ya pasó con vigas; en
  las demás columnas no da problema. Falta medirlo en el modelo de Santiago.
- [ ] Probado en Revit (pruebas 1 a 3 de «Cómo probarlo»). Pendiente: hay que cerrar Revit para
      desplegar en 2027.
- Pendiente aparte: #318 (paquete de menores más alto que la separación).
