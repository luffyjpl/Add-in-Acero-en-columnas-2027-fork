# Plan: estribos centrados a su separación y fallas que vio Santiago (pedido de Luciano, 07-nov)

## Contexto

- Pedido de Luciano (07-nov), en cuatro partes:
  1. **Estribos a su separación exacta, sin espacio corto.** Los 5 cm del borde son un
     **mínimo**: se ponen todos los estribos que entren a la separación pedida, y si sobra
     lugar el grupo se centra (los bordes quedan a más de 5 cm). Ejemplo: en un nudo donde
     salían 3 estribos y uno más pegado (el "espacio corto" de la decisión 9 del #256), ahora
     salen los que entren a la separación, centrados.
  2. **Traslapos que fallan en el modelo de Santiago** ("ese empalme no se pudo hacer" o algo
     parecido). En nuestros modelos no pasa. Hay que hacer el longitudinal independiente de las
     formas y ajustes del modelo.
  3. **Mensajes raros con varios proyectos abiertos** ("no se pudieron tener los permisos",
     "el elemento que lo hosteaba ya no existe en el modelo; se omite") sin haber movido nada, y
     **la columna quedaba marcada como generada** aunque no se generó nada.
  4. **El acero generado no se ve en la vista 3D**, pero sí en planta.
- Luciano está reinstalando Revit (pasa a 2026) y **no se puede probar en Revit** en este
  trabajo, ni Luciano ni Santiago. Todo lo que dependa de Revit real queda marcado "sin
  verificar" en Estado, y se prueba antes de etiquetar una versión.
- Fuera de alcance: vigas (decisión 1); el issue #270 (proyecto xUnit).
- Rama `columnas/estribos-y-santiago` en el worktree `C:\Proyecto\addins-santiago`, creada desde
  `origin/main` 18bb5e2 (tablero web de columnas ya integrado).

## Criterios de aceptación

- [ ] Uniforme (y nudos) y "confinado sin zona central": todos los espacios iguales a la
      separación pedida; el grupo centrado; cada borde a 5 cm o más.
- [ ] Confinado: confinados exactos desde 5 cm de cada punta, tantos como entren en L1; la
      central exacta y centrada entre ellos; ningún espacio menor que la separación del
      confinado; ningún espacio corto.
- [ ] La vista previa, el 3D y lo generado salen de la misma cuenta (no cambia el circuito).
- [ ] Generar con otro proyecto activo no toca ningún modelo: avisa qué proyecto activar.
- [ ] Guardar configuraciones con otro proyecto activo no escribe en el proyecto equivocado.
- [ ] Una columna en la que no se creó nada no queda marcada como armada ni se guarda como
      generada; el mensaje lo dice.
- [ ] Las longitudinales se crean con una forma recta elegida por el add-in, no con la que
      Revit encuentre primero en el modelo; si no se puede, se sigue como hoy.
- ~~Si el modelo tiene encendido "la forma define los ganchos": se apaga solo cuando el
  modelo no tiene nada de acero~~ — retirado (decisión 9).
- [ ] El acero generado se ve en la vista 3D del add-in después de generar (decisión 10: ya
      no se toca el resto de las vistas 3D).
- [ ] Piezas "completamente fuera de su host": en vez del reporte de Revit, un aviso del
      add-in por columna con la causa probable y el arreglo (decisión 12).
- [ ] En un modelo compartido, si Revit no deja guardar cambios en algún elemento, el aviso
      del add-in dice cuáles, a nombre de quién están y qué hacer; si la generación se
      deshace, nada queda marcado como generado (decisión 11).
- [ ] `dotnet build` de columnas en 2026 y 2027 sin advertencias nuevas; `dotnet test
      CQEING-Addins.sln` sin fallos; pruebas del reparto en `tools/PruebasVistaPrevia`.

## Hallazgos del código y de Revit real

Todo leído en el código (07-nov); nada medido en Revit en este trabajo.

### 1. Estribos

- `addins/armado-columnas/Modelo/RepartoEstribos.cs` es la única cuenta: la usan
  `GeneradorArmado.GenerarTramo` (un conjunto de Revit por `Corrida`), la vista previa del
  tablero (`Tablero/ArmadorVistaColumnas.cs`) y la clasificación 3D
  (`Logica/ClasificadorPrevia3D.cs`). Cambiar `Calcular` cambia los tres a la vez.
- Hoy (#256, decisión 9 del plan `2026-11-05-largos-y-estribos-columnas-245-256.md`):
  `Repartir` arranca a 5 cm, va cada `s` y lo que sobra queda en un último espacio más corto;
  si sobra menos de 5 cm se igualan los dos últimos. Ese espacio corto es el "uno a menos
  distancia" que describe Luciano.
- El generador ya crea cada corrida con `SetLayoutAsNumberWithSpacing` (o `Single` si es un
  solo estribo): no hace falta tocarlo. Con la regla nueva cada zona es una sola corrida.
- `tools/PruebasVistaPrevia/Program.cs` todavía prueba `RepartoEstribos.CantidadEnGrupo`, que
  se borró en el #256: ese proyecto no compila en `main` (no está en la solución).

### 2. Traslapos en el modelo de Santiago

- `BarraContinua.CrearEntera` crea cada varilla con
  `Rebar.CreateFromCurves(..., useExistingShapeIfPossible: true, createNewShape: true)`:
  Revit elige **cualquier** forma del modelo que encaje. Ya pasó en el #176 que una forma que
  Revit eligió sola rompió los empalmes ("The rebar splice connection cannot be maintained",
  memoria `formas-propias-revit-las-reusa`). En nuestros modelos elige la «00» de la
  plantilla; en uno de otra plantilla puede elegir otra.
- Los planes del #64 y #176 midieron `ReinforcementSettings.RebarShapeDefinesHooks = False` en
  nuestros modelos y anotaron "un modelo con True no se probó". Con True, el gancho es parte
  de la forma: una varilla con gancho en una punta usa una forma con gancho, y al partirla en
  los traslapos Revit tiene que cambiarle la forma a cada pieza: es el mismo caso que rompió
  los empalmes en el #176. Es la causa más probable de lo de Santiago (sin confirmar: no
  tenemos el texto exacto del aviso ni el modelo).
- API (documentación oficial, `RevitAPI.xml` 2026 y 2027, miembros públicos):
  `ReinforcementSettings.RebarShapeDefinesHooks` "se puede cambiar si el documento no tiene
  armaduras, armaduras por área ni por camino" (si no, `InvalidOperationException`);
  `Rebar.CreateFromCurvesAndShape(doc, forma, barra, host, normal, curvas, terminaciones)`
  ("con RebarShapeDefinesHooks en false, los ganchos pueden ser distintos de los de la
  forma"); `RebarShape.SimpleLine`, `RebarShape.HasEndTreatment()`, `RebarShape.RebarStyle`.
- **07-nov, más tarde: descartado.** Luciano confirma que el modelo de Santiago tiene
  "Incluir ganchos en la definición de la forma" **apagado**, así que esa no es la causa (ver
  decisión 9). Luciano cree que el aviso que vio Santiago es el de
  `BarraContinua.Crear` "Revit no pudo aplicar los traslapos de una varilla «…» (motivo);
  quedó entera, sin traslapos". Ese aviso sale cuando `Partir` tira una excepción que no
  atrapa adentro. `SpliceRebar` sí está en un try, así que la excepción viene de otra
  llamada de `Partir` sin try: el `doc.Regenerate()` del principio,
  `new RebarSpliceOptions(...)` / `RebarSpliceUtils.CanRebarBeSpliced(...)` en la revisión
  previa, el `doc.Regenerate()` justo después de cada `SpliceRebar`, o
  `RebarSpliceUtils.GetSpliceChain(...)`. El texto entre paréntesis es el mensaje de Revit
  (`GeometriaSeccion.Explicar`): con él se sabe cuál. En la versión publicada no hay traza
  (`BarraContinua.Traza` solo en `#if DEBUG`). Causa todavía **sin encontrar**.

### 3. Varios proyectos abiertos

- `Logica/ManejadorGeneracionArmado.Execute` genera sobre `app.ActiveUIDocument.Document`, el
  proyecto **activo al apretar Generar**, no el del tablero. El tablero es no modal: si el
  usuario pasó a otro proyecto, el plan (ids de columnas del proyecto A) se aplica al B. Los
  ids no existen en B → "el elemento que lo hosteaba ya no existe en el modelo; se omite" en
  cada tramo. Explica lo de Santiago con varios proyectos abiertos.
- `Logica/ManejadorPersistirConfiguraciones.Execute` hace lo mismo: abre la transacción en el
  proyecto activo y escribe con el almacén del proyecto del tablero. Con otro proyecto activo
  Revit rechaza el cambio (modificar un documento sin transacción abierta en él): candidato a
  "no se pudieron tener los permisos" / "no se pudieron guardar las configuraciones".
- `ManejadorVistaPrevia3D` ya comprueba el documento ("Activa en Revit el modelo de la
  ventana…"). Se copia ese criterio.
- Otra causa posible de "permisos" (no confirmada): modelo central con elementos tomados por
  otro usuario. Queda en Pendiente, no se toca.
- Marcado como generado: `SesionColumnas.ResultadoGeneracion` pasa **todas** las columnas del
  plan a generadas y escribe "Se armó la columna X", y `RegistroGeneradosRevit.Registrar`
  guarda todas en el modelo, aunque cada tramo se haya omitido.

### 4. El acero no se ve en 3D

- En una vista 3D sombreada, el concreto de la columna tapa el acero que lleva adentro; en
  planta el plano de corte lo deja ver. Revit lo resuelve con "Ver sin obstrucciones" por
  vista: `Rebar.SetUnobscuredInView(vista, true)` (público en 2026 y 2027). El add-in nunca
  lo usa (grep del 07-nov).
- Además, la vista del add-in «CQEING Columnas 3D» oculta la categoría Armadura a propósito
  (`Logica/Vista3DColumna.Abrir`, para que no se mezcle con la vista previa). Si Santiago miró
  esa vista después de generar, el acero estaba oculto por categoría.
- **08-nov:** Luciano confirma que, en el caso de Santiago, el acero no se veía porque su
  modelo tenía configurado ocultarlo. No era el concreto (ver decisión 10).

### 5. "No se puede obtener permiso para editar el elemento" (08-nov)

- Captura de Santiago (hoja "Observaciones Addis", pestaña "Addin - Acero Columnas", punto 9,
  que dice "viga" pero es columna): proyecto "PROYECTO THEVENIG 2", Revit 2026, diálogo
  "0 Errores, 5 Avisos": "No se puede obtener permiso para editar el elemento. Los cambios
  serán temporales y no se guardarán." El punto 10 dice que "no genera la columna". La
  captura es de antes del cambio del 3D.
- Es un modelo compartido (con central), y según Luciano Santiago es el único que trabaja
  ahí.
- El aviso es `BuiltInFailures.EditingFailures.TemporaryChangesTransparentPermissions`
  (RevitAPI.xml 2026 y 2027: "Permission to edit the element cannot be obtained. Changes will
  be temporary, and will not be saved."). Es una **advertencia**, no un error: Revit deja
  seguir, pero los cambios en esos elementos no se guardan en el central. Suele salir con
  cambios de vista (gráficos), y el add-in sí hace cambios de vista: `ColoresEnVistas.Aplicar`
  pone los colores en **todas** las vistas donde se ve la columna, o en sus plantillas. Con 5
  vistas o plantillas que Santiago no puede editar (a nombre de otro usuario, o de él mismo
  con otro nombre de usuario o desde otra copia local), salen 5 avisos. Es el candidato más
  probable, **sin confirmar** (la captura no dice qué elementos son).
- Si en ese diálogo se pulsa **Cancelar**, Revit deshace toda la transacción. Pero
  `ManejadorGeneracionArmado.Execute` no mira lo que devuelve `t.Commit()` y llama a
  `AlTerminar` igual: la columna queda marcada como generada sin acero. Puede ser el "no
  genera la columna" del punto 10, y también el ✓ falso del punto 3.
- El add-in no revisa permisos de modelo compartido en ningún lado (grep de `Worksharing`,
  `CheckoutStatus` e `IsWorkshared` del 08-nov: nada).

### 6. Grapas "completamente fuera de su host" (08-nov, medido en Revit)

- Luciano generó en "PROYECTO THEVENIG 2" con la versión publicada. Los traslapos salieron
  bien. Revit dio un reporte (`PROYECTO THEVENIG 2_Error Report.html`) con 28 advertencias
  "Rebar is placed completely outside of its host.", todas de la forma «CQ Grapa C», #3.
- Medido con el MCP de Revit (cajas de los elementos, en pies):
  - Las 28 grapas son de la columna 907947, que es una sola, de 0 a 65.49 pies (0 a 20 m), con
    caja X −31.58 a −29.94 e Y 10.36 a 12.00. Hay un grupo por nudo, a Z ≈ 14.3–14.9,
    27.0–27.5, 39.6–40.1, 52.2–52.8 y 64.9–65.2 pies.
  - Las vigas del primer nudo van de Z 13.32 a 14.96. La 909869 / 909889 (en X, Y 10.61 a
    11.75) y la 910005 / 911362 (en Y, X −31.33 a −30.18) entran hasta el centro de la
    columna. Cada viga mide 35 cm de ancho y la columna 50 cm: entre las dos forman una cruz
    dentro de la columna.
  - Cada grapa cae entera dentro de esa cruz: las de X dentro de la viga en X y las de Y
    dentro de la viga en Y. Los estribos van por el borde, fuera de la cruz, y no avisan.
- Conclusión, sin confirmar el orden de unión (el MCP no lo lee): en este modelo las vigas
  cortan a la columna en los nudos, a la columna le queda un hueco en cruz, y Revit ve las
  grapas fuera de ella. Las grapas están bien ubicadas. Se arregla en el modelo con
  Modificar → Unir geometría → Cambiar orden de unión (que la columna corte a la viga).
- La advertencia es `BuiltInFailures.RebarFailures.OutSideOfHost` (RevitAPI.xml 2026 y 2027).

## Decisiones

1. **La regla nueva va solo en columnas** (2026-11-07, Luciano). Vigas sigue con la regla del
   espacio corto. Queda pendiente hablarlo con Santiago (decisión 11 del plan del #256 pedía
   cambiar los dos a la vez).
2. **Reemplaza la decisión 9 del #256** (2026-11-07, Luciano). Uniforme / nudos / "confinado
   sin central": `n = ⌊L/s⌋ + 1` estribos a `s` exacto, centrados en el largo libre `L` (de
   5 cm a 5 cm). Si `L` es menor que `s`, va uno solo, en el medio. *Antes iban dos, uno en
   cada borde: con la regla nueva no puede haber un espacio menor que `s`.*
3. **Confinado: puntas fijas, centro centrado** (2026-11-07, Luciano eligió esta de tres
   opciones):
   - confinado de abajo: desde 5 cm, cada `sc`, `⌊L1/sc⌋ + 1` estribos;
   - confinado de arriba: igual desde arriba;
   - central: entre el último de abajo (`a`) y el primero de arriba (`b`), `m = ⌊(b − a − 2·sc)/sn⌋ + 1`
     estribos cada `sn`, centrados; así el espacio entre la central y cada confinado queda
     entre `sc` y `sc + sn/2`. Si `b − a < 2·sc`, la central no lleva estribos.
   - Los rangos `ZDesde`/`ZHasta` de cada zona no cambian (L1 desde cada punta), para que las
     cotas de la vista previa sigan igual.
4. **"La forma define los ganchos": se apaga si el modelo no tiene acero** (2026-11-07,
   Luciano: "me parece el 1, pero si ya tiene generadas las vigas, ¿habría conflicto?").
   No: Revit no deja cambiarlo con cualquier acero en el modelo (vigas incluidas), así que en
   ese caso no se toca nada y solo se avisa con la explicación y el arreglo a mano. Se mira
   después de borrar el acero anterior del add-in y antes de crear nada.
5. **Forma recta explícita para las longitudinales** (2026-11-07, Claude): con el ajuste
   apagado, se busca en el modelo una forma de un solo tramo recto, estilo estándar, sin
   tratamiento de extremos (prefiere «00» y «M_00»; nunca las «CQ …» propias) y se crea con
   `CreateFromCurvesAndShape`. Es la misma forma que Revit elige en nuestros modelos, así que
   no cambia lo probado. Si no hay una o Revit devuelve null, se cae al `CreateFromCurves` de
   hoy. Con el ajuste encendido no se usa (la forma tendría que llevar los ganchos).
6. **El tablero trabaja solo sobre su proyecto** (2026-11-07, Claude): generar y guardar
   configuraciones comprueban que el proyecto activo sea el del tablero; si no, no hacen nada
   y dicen qué proyecto activar. No se genera en un proyecto que el usuario no está mirando.
7. **"Armada" = se creó algo** (2026-11-07, Claude): una columna cuenta como armada si se creó
   al menos un conjunto de estribos o una longitudinal. Las que no, siguen listas para volver
   a generar, no se guardan en el modelo y el mensaje las nombra. Dentro de una columna
   armada, los tramos omitidos no se guardan como generados (los de contra zapata, que nunca
   llevan estribos, sí).
8. **Acero visible en 3D** (2026-11-07, Claude): después de generar, el acero de las columnas
   generadas queda "sin obstrucciones" en todas las vistas 3D del proyecto; y en «CQEING
   Columnas 3D» se vuelve a mostrar la categoría Armadura y se quita el dibujo de la vista
   previa. La vista previa la vuelve a ocultar cuando se pide de nuevo.
9. **Se quita el apagado automático de la decisión 4** (2026-11-07, Luciano: "quítalo pues").
   Santiago tiene el ajuste apagado, así que no era la causa. Además cambiaba un ajuste del
   modelo del usuario. Se borra `FormaRectaLongitudinal.AjustarGanchosEnForma` y su llamada
   en `GeneradorArmado.Generar`. La decisión 5 (forma recta) se queda: con el ajuste apagado,
   el gancho no es parte de la forma y se pone aparte con `BarTerminationsData`, igual que
   antes. Con el ajuste encendido, `Buscar` devuelve null y todo sigue como antes, sin aviso.
10. **Se quita "sin obstrucciones" de la decisión 8** (2026-11-08, Luciano). En el caso de
    Santiago, el acero no se veía porque su modelo lo tenía oculto. Además, tocar todas las
    vistas 3D del proyecto puede dar este mismo aviso de permisos en un modelo compartido.
    Se borra `Vista3DColumna.SinObstrucciones` y su llamada. Queda solo volver a mostrar la
    armadura en «CQEING Columnas 3D» (`MostrarArmadura` / `OcultarArmadura`), porque esa vista
    la oculta a propósito para la vista previa.
11. **Avisar por qué no se guarda o no se genera** (2026-11-08, Luciano: "estaría bien poner
    solo el aviso de por qué no genera"). Solo el aviso: el add-in no pide permisos por su
    cuenta ni cambia lo que genera.
    - La transacción de generar lleva un `IFailuresPreprocessor` (`AvisosDeRevit`) que
      reconoce las fallas de permisos de modelo compartido (`EditingFailures`
      `TemporaryChangesTransparentPermissions`, `TemporaryChangesOutOfDate`, `OwnedByOther`,
      `OutOfDateElements`, `OwnElementsOutOfDate`, `HaveNoPermissionToEdit`,
      `CannotBorrowBusyContention`).
    - Las advertencias las borra, así ya no sale el diálogo de Revit con "Cancelar", y deja
      un aviso propio: qué elementos (nombre y categoría), a nombre de quién están
      (`WorksharingUtils.GetWorksharingTooltipInfo(...).Owner`) y qué hacer. El acero
      generado queda igual.
    - Los errores no se pueden saltar: se deshace la generación y se dice por qué.
    - Siempre se mira el resultado de `t.Commit()`. Si no quedó `Committed`, se llama a
      `AlFallar` ("no se generó nada" y el motivo), no a `AlTerminar`, así que nada queda
      marcado como generado.
12. **Explicar "fuera de su host"** (2026-11-08, Luciano: "haz que el add-in explique ese
    aviso"; y deja la vuelta a mostrar el acero en «CQEING Columnas 3D»). El mismo
    preprocesador de la decisión 11 reconoce `RebarFailures.OutSideOfHost`: borra esas
    advertencias, así ya no sale el diálogo de Revit, y deja un aviso propio por columna.
    El aviso dice cuántas piezas y de qué forma, la causa más común (las vigas cortan a la
    columna en el nudo; las piezas están en su sitio) y el arreglo (Cambiar orden de unión,
    columna primero, y regenerar). Las piezas no se mueven ni se borran. Como ahora maneja
    más que permisos, la clase pasa a llamarse `AvisosDeRevit` (`Logica/AvisosDeRevit.cs`).

## Plan paso a paso

1. **Reparto centrado** — `addins/armado-columnas/Modelo/RepartoEstribos.cs`:
   `Exacto(desde, hasta, s)` (centrado, decisión 2) en lugar de `Repartir`; `Calcular` con la
   decisión 3; se borra `EspacioMinimoMetros`. Se actualizan las pruebas del reparto en
   `tools/PruebasVistaPrevia/Program.cs` (saca `CantidadEnGrupo`, agrega el nudo del ejemplo).
   Cómo se prueba: `dotnet run --project tools/PruebasVistaPrevia` (si el resto del proyecto
   compila) y build de columnas.
2. **Tablero atado a su proyecto** — `Logica/ManejadorGeneracionArmado.cs`,
   `Logica/ManejadorPersistirConfiguraciones.cs` (propiedad `Documento` y comprobación),
   `Tablero/PuenteColumnas.cs` (la asigna). Cómo se prueba: build; en Revit, con dos
   proyectos abiertos.
3. **Solo se marca lo que se creó** — `Modelo/OpcionesArmado.cs` (`ResultadoArmado`:
   `TramosGenerados`, `ColumnasConLongitudinal`, `SoloLoGenerado(plan)`),
   `Logica/GeneradorArmado.cs` y `Logica/GeneradorLongitudinal.cs` (los llenan),
   `Logica/ManejadorGeneracionArmado.cs` (registra solo lo generado),
   `Tablero/SesionColumnas.ResultadoGeneracion` (marca solo lo generado, nombra lo que no).
   Cómo se prueba: build; en Revit, generando con otro proyecto activo antes del paso 2.
4. **Forma recta** — `Logica/FormaRectaLongitudinal.cs` nuevo (búsqueda de la forma),
   `Logica/BarraContinua.CrearEntera` (usa la forma). *El ajuste de los ganchos se quitó
   (decisión 9), y con él la llamada en `Logica/GeneradorArmado.Generar`.* Cómo se prueba:
   build; en Revit, una columna con traslapo y ganchos.
5. **Acero visible en 3D** — `Logica/Vista3DColumna.cs` (`MostrarArmadura`, `OcultarArmadura`
   y `SinObstrucciones`), `Logica/ManejadorGeneracionArmado.cs` (los llama),
   `Tablero/PuenteColumnas.cs` (quita el dibujo previo), `Logica/ManejadorVistaPrevia3D.cs`
   (vuelve a ocultar al dibujar). Cómo se prueba: build; en Revit, generar y mirar {3D} y
   «CQEING Columnas 3D».
6. **Cierre** — builds 2026/2027, `dotnet test CQEING-Addins.sln`, Estado de este plan.
7. **(08-nov) Sin "sin obstrucciones"** — `Logica/Vista3DColumna.cs` (borra `SinObstrucciones`),
   `Logica/ManejadorGeneracionArmado.cs` (borra la llamada). Cómo se prueba: build.
8. **(08-nov) Aviso de permisos** — `Logica/AvisosDeRevit.cs` nuevo (el preprocesador),
   `Logica/ManejadorGeneracionArmado.cs` (lo pone en la transacción, agrega el aviso y mira
   el resultado de `Commit`). Cómo se prueba: build; en Revit, un modelo compartido con una
   vista tomada por otro usuario.
9. **(08-nov) Explicar "fuera de su host"** — `Logica/AvisosDeRevit.cs` (renombrado desde
   `AvisosDePermisos.cs`, decisión 12) y `Logica/ManejadorGeneracionArmado.cs`. Cómo se
   prueba: build; en Revit, regenerar la columna 907947 de THEVENIG 2 (no sale el reporte
   de Revit; sale el aviso del add-in con 28 grapas). Después, cambiar el orden de unión y
   regenerar: sin aviso.

## Cómo probarlo

```
dotnet build addins/armado-columnas/practicacq.csproj -c Debug -p:RevitVersion=2026
dotnet build addins/armado-columnas/practicacq.csproj -c Debug -p:RevitVersion=2027
dotnet test CQEING-Addins.sln
dotnet run --project tools/PruebasVistaPrevia
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin armado-columnas -Revit 2026
```

En Revit (Revit cerrado antes de desplegar: `Get-Process -Name Revit -ErrorAction SilentlyContinue`):

1. Columna con un nudo de ~45 cm a 15 cm: salen 2 estribos (no 3 más uno pegado), con los dos
   bordes iguales y a más de 5 cm. Medir con cota.
2. Columna confinada (L1 0.50, sc 0.07, sn 0.14): confinados desde 5 cm, central centrada,
   ningún espacio corto; la vista previa del tablero muestra lo mismo.
3. Dos proyectos abiertos: tablero en A, activar B, Generar → aviso de activar A; B intacto;
   la columna sigue lista.
4. Generar con una columna que falle entera (por ejemplo sin recubrimiento): no queda armada.
5. Columna con traslapo y ganchos abajo y arriba: las varillas tienen la forma «00»
   (propiedades de la varilla), los ganchos quedan en las puntas y los traslapos se hacen.
6. Después de generar: el acero se ve en «CQEING Columnas 3D».
7. Modelo compartido: con otro usuario de Revit (Opciones > General > Nombre de usuario),
   tomar una planta donde se ve la columna (moverle algo, sin sincronizar). Volver al
   usuario normal y generar: no sale el diálogo de Revit; el aviso del add-in nombra esa
   planta y al otro usuario; el acero se genera.

## Estado

Al 07-nov, sin commit (rama `columnas/estribos-y-santiago`, en la carpeta `C:\Proyecto\addins-santiago`).

- [x] Plan revisado con el usuario (decisiones 1 a 4, 07-nov).
- [x] Pasos 1 a 6 implementados, en ese orden, como dice el plan. Archivos nuevos:
      `Logica/DocumentoActivo.cs` (paso 2) y `Logica/FormaRectaLongitudinal.cs` (paso 4).
- [x] (08-nov) Pasos 7, 8 y 9: sin "sin obstrucciones"; `Logica/AvisosDeRevit.cs` nuevo.
      Builds 2026 y 2027 con `--no-incremental`: 0 advertencias, 0 errores; `dotnet test
      CQEING-Addins.sln`: todo correcto.
- [ ] Probado en Revit: **nada**. Luciano y Santiago no pueden probar ahora; la lista de
      "Cómo probarlo" queda para antes de etiquetar una versión.

Comprobado sin Revit (07-nov):

- `dotnet build ... -p:RevitVersion=2026` y `2027` con `--no-incremental`: 0 advertencias, 0 errores.
- `dotnet test CQEING-Addins.sln`: todo correcto (columnas no tiene proyecto de pruebas en la
  solución; issue #270).
- `dotnet run --project tools/PruebasVistaPrevia`: las 9 pruebas del reparto pasan (uniforme
  centrado, el nudo del ejemplo de Luciano, tramo más corto que una separación, confinado,
  confinado sin central, central sin lugar, con paquete, espaciado cero, sin altura libre).
  Falla `choques_tramo_dentro_de_viga` ("en el nudo no van menores"), que **no tiene que ver
  con este trabajo**: quedó vieja con 3de0195 ("grapas en los nudos") y no se veía porque el
  proyecto no compilaba en `main` (usaba `CantidadEnGrupo`, borrado en el #256). Se deja como
  está.

Criterios:

- ✅ (sin Revit) reparto uniforme/nudo/sin central centrado y exacto; confinado con puntas
  fijas y central centrada — pruebas del reparto.
- ✅ (sin Revit) vista previa, 3D y generado salen de `RepartoEstribos.Calcular`, que es lo
  único que cambió del reparto.
- ⏳ otro proyecto activo al generar / guardar configuraciones: implementado
  (`DocumentoActivo.DelTablero`), sin probar.
- ⏳ columna sin nada creado no queda armada: implementado (`ResultadoArmado.SoloLoGenerado`,
  `SesionColumnas.ResultadoGeneracion`, recuadro rojo `resultado nada` en la página), sin probar.
- ⏳ forma recta explícita: implementado, sin probar. El ajuste "la forma define los ganchos"
  se descartó como causa y se quitó (decisión 9). **La causa de lo de Santiago sigue sin
  encontrar**: hace falta el texto entre paréntesis del aviso "Revit no pudo aplicar los
  traslapos…".
- ⏳ acero visible en la vista 3D del add-in: implementado (`MostrarArmadura` /
  `OcultarArmadura`), sin probar. "Sin obstrucciones" se quitó (decisión 10).
- ⏳ aviso de permisos y `Commit` mirado: implementado (`AvisosDeRevit`), sin probar.
- ⏳ aviso propio para "fuera de su host": implementado (`AvisosDeRevit`), sin probar.

Pendiente:

- Probar todo en Revit 2026 (lista de "Cómo probarlo").
- Pedirle a Santiago el aviso del traslapo completo, con el texto entre paréntesis (mejor una
  captura).
- "Permisos": solo se avisa (decisión 11). Falta saber qué 5 elementos eran en el modelo de
  Santiago: con la versión nueva, el aviso del add-in lo dice.
- Vigas sigue con el espacio corto (decisión 1): hablarlo con Santiago.
- La maqueta `Tablero/web/demo.js` no calcula estribos, así que no hace falta cambiarla.
