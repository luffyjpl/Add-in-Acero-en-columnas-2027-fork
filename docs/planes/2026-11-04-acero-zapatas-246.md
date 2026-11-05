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
- **MCP de Revit (2026-11-04):** el usuario pidió conectar `mcp-servers-for-revit` v1.0.0 para leer el modelo y probar
  código en Revit real. Todavía no está instalado: lo bloqueó el control de permisos de Claude Code. El diagnóstico del
  paso 5 mide lo mismo sin el MCP.
- **Por medir, con el diagnóstico del paso 5:** cómo crear una barra con patas ajustadas a la altura, si Revit la corre al
  regenerar por el recubrimiento, y si formas y ganchos tienen imagen disponible por la API pública.

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
9. **Release de respaldo para Juan** (2026-11-04, usuario) — solo después de terminar y probar. Se decide con el usuario
   cómo publicarla: `docs/guia-addins-cqeing.md` pone la etiqueta sobre `main` después de unir el PR, y `-rc.N` publica una
   prueba que el gestor no muestra.

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

- [ ] Pasos implementados.
- [ ] Probado en Revit.
