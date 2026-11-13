# Plan: add-in «Área útil del lote» (versión 1)

> **Para quien lo ejecute (persona o agente):** se implementa tarea por tarea, en orden, con
> `superpowers:executing-plans` o `superpowers:subagent-driven-development`. Los pasos usan casillas (`- [ ]`). Regla del
> repo: si algo cambia, primero se anota aquí (Decisiones o Hallazgos, con fecha) y después se programa.

**Objetivo:** un add-in de la pestaña CQEING donde el arquitecto ingresa el lote y los datos de la ficha de la
curaduría, ve el área útil en una vista previa con áreas y avisos, y la genera en Revit como líneas de modelo.

**Arquitectura:** el patrón de `acero-zapatas`. El cálculo va sin Revit en `calculo/` (net8.0, con pruebas xUnit); la
ventana es WebView2 sin XAML, con una página de tres columnas que habla con C# por mensajes JSON; la parte de Revit crea
los estilos de línea CQ y las líneas de modelo marcadas con Extensible Storage.

**Tecnología:** C# (.NET 8 para Revit 2026, .NET 10 para 2027), Revit API por NuGet (`addins/Directory.Build.props`),
WebView2, HTML/JS/SVG sin librerías, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-10-area-util-lote-design.md`.

## Restricciones generales

- Carpeta `addins/area-util/`, id `area-util`, ensamblado y namespace `CQEING.AreaUtil`. Años `["2026", "2027"]`.
- `OnStartup` empieza con `Licencia.Vigente` y `BloqueoPorVersion.AlDia` (`docs/guia-addins-cqeing.md`).
- Botón «Área útil del lote» en el panel «Lote» de la pestaña CQEING, con `CintaCqeing`.
- El cálculo trabaja en **metros**; solo `Revit/` convierte a pies con `UnitUtils` (decisión 9 del spec).
- Estilos de línea: «CQ Lote», «CQ Retiro frontal», «CQ Retiro lateral», «CQ Retiro posterior», «CQ Área útil»
  (decisiones 1 y 2 del spec).
- Nada de API interna de Revit; lo que no esté en `RevitAPI.xml` no se usa.
- Antes de desplegar: `Get-Process -Name Revit -ErrorAction SilentlyContinue` vacío.
- Sin firma de Claude en commits ni PR. Commit, push y PR solo cuando el usuario los pida.
- Textos al usuario en español neutro, sin voseo.

## Puntos de revisión

Entradas que el spec no menciona y que más pueden fallar con un usuario real. Cada una tiene su prueba en la tarea que
la cubre:

1. **Puntos pegados desde Excel en español**: coma decimal y columnas separadas por tabulador → `LectorPuntos`
   (tarea 6).
2. **El topógrafo repite el primer punto al final** para cerrar el polígono → `Poligono` lo quita (tarea 2).
3. **Puntos en sentido horario o antihorario** → el área útil sale igual (tarea 4).
4. **Coordenadas topográficas grandes** (E 1 200 000, N 900 000) → el área no pierde precisión (tarea 2), y el
   generador avisa si el lote queda a más de 30 km del origen interno de Revit (tarea 9).
5. **Lote angosto con retiros que se comen un lado** → aviso que bloquea y no se genera (tarea 4).

---

## Contexto

- **Qué se pide:** el add-in de la reunión del 09-nov-2026 (Carlos Quintero, la arquitecta María Belén y Joseph).
  Transcripción completa con minutos: `C:\Users\JOSETP\Documents\WORK\typescritp\Reunion 2026-11-09 - transcripcion
  completa (por minuto).md`. Spec aprobado el 2026-11-10.
- **Fuera de alcance:** dibujo libre, línea de propiedad de 2027, 2.º piso y voladizos, recordar los datos en el modelo
  (sección «Fuera de la versión 1» del spec).
- **Entorno:** Revit 2026.5.10 (API 26.5.10.38), .NET SDK 8.0.424 (sin SDK 10: 2027 se compila en el CI), modelo
  `Prueba 080720sfsd26.0028`, rama `area-util/addin-lote` desde `main` en `687414d`.

## Criterios de aceptación

- [ ] El botón aparece en la pestaña CQEING y abre la ventana de tres columnas.
- [ ] Un lote de 7 × 14 con retiros 1.5 / 2 / 1 / 1 da 98 m² de lote y 52.5 m² de área útil, en la página y en las
      pruebas.
- [ ] Los puntos pegados desde Excel (coma decimal, tabuladores) arman el lote.
- [ ] Clic en un lado lo vuelve frente y reclasifica los demás; un lado se puede cambiar a mano.
- [ ] Los avisos de la ficha (frente, lote mínimo, IMO, IMC) y los bloqueantes (lote inválido, retiros que se comen un
      lado) salen en la columna derecha.
- [ ] «Generar en Revit» crea el contorno, los tramos de retiro y el área útil con sus estilos en el nivel elegido; al
      volver a generar se reemplazan sin duplicar, y Ctrl+Z deshace todo de una vez.
- [ ] `dotnet test CQEING-Addins.sln -c Release` sin fallos y compilación sin advertencias nuevas.

## Hallazgos del código y de Revit real

1. (2026-11-10) `PropertyLine` no tiene miembros públicos en la API 2026 (`RevitAPI.xml` 2026.0.4); la 2027 trae
   `Create`, `SetBoundary`, `GetBoundary` y `Area` (`RevitAPI.xml` 2027.0.0). Ver decisión 1 del spec.
2. (2026-11-10) Patrón que se copia: `addins/acero-zapatas/` (`AppZapatas.cs`, `Tablero/ComandoTablero.cs`,
   `Tablero/VentanaTablero.cs`, `Tablero/AccionesRevit.cs`, `Tablero/PuenteTablero.cs`, `Revit/MarcaZapata.cs`,
   `calculo/Directory.Build.props`).
3. (2026-11-10) `TodosLosAddinsExigenLicenciaTests` (en `gestor/CQEING.Gestor.Core.Tests/`) recorre cada
   `IExternalApplication` de `addins/`.
4. Por medir en la tarea 7: estilos de línea, `NewModelCurve` sobre el plano de un nivel, transformación de
   coordenadas compartidas y si Revit avisa cuando dos líneas de modelo se superponen (retiro 0).
5. (2026-11-10, tarea 4) `Poligono.Contiene` (regla par-impar) no cuenta los puntos sobre el borde: con un retiro en 0,
   las esquinas del área útil caen en el lado del lote y el desfase fallaba. Se agregó `ContieneOBorde` (tolerancia de
   1 mm) con su prueba, y el desfase la usa.
6. (2026-11-10, tarea 5) Un lote con lados cruzados en forma de moño tiene área neta 0: el cruce se revisa antes que el
   área para que el aviso diga «se cruzan» y no «necesita al menos 3 puntos».
7. (2026-11-10) Todos los miembros de la API que usan `Revit/` y el puente son públicos en `RevitAPI.xml` 2026.0.4.
   `GetTotalTransform` es de `Instance` (de la que hereda `ProjectLocation`). La tarea 7 se mide después de escribir
   las tareas 8 y 9, porque Revit quedó cerrado y era el momento de desplegar; se mide antes de dar la tarea 9 por
   probada.
8. (2026-11-11 00:40, tarea 7, MCP sobre `Prueba 080720sfsd26.0028`, Revit 2026.5.10; todo dentro de una
   `SubTransaction` deshecha):
   - Estilo de línea: `NewSubcategory` + `LineColor` + `SetLineWeight(5)` funcionan (color 208,2,27, peso 5). Con la
     misma referencia de la categoría, `SubCategories.Contains` da `False` justo después de crear; pedida de nuevo da
     `True`. Crear un nombre que ya existe lanza `ArgumentException`. `EstilosRevit.Asegurar` pide la categoría al
     empezar, así que entre generaciones encuentra los estilos.
   - `SketchPlane.Create(doc, nivel.Id)`: la Z del plano es la cota del nivel (N-0.65 → −0,65 m; N+0.00 → 0).
   - Dos líneas de modelo encimadas con estilos distintos **no** generan avisos (7 antes, 7 después): la decisión 10
     queda como está.
   - El modelo de prueba tiene coordenadas compartidas iguales a las internas (emplazamiento por defecto, sin giro).
9. (2026-11-11, tarea 7) **`GetTotalTransform` lleva de compartidas a internas**, al revés de lo supuesto en la
   decisión 8 del spec. Con el punto interno (7, 0) en compartidas (1007, 500): `t.OfPoint` da (7, 0) y `t.Inverse`
   daba (2007, 1000); con el norte girado 30°, `t.OfPoint` da (7, 0) y la inversa (1619, 1439). `GeneradorLote` usa
   `GetTotalTransform()` sin invertir.
10. (2026-11-11, prueba del usuario del 10-nov a las 23:06 y 23:07) **Revit se cerraba al cerrar la ventana del
    add-in**, no al generar: la transacción «Área útil del lote» terminó bien. Los volcados `journal.0003.0001.dmp` y
    `journal.0004.0001.dmp` (leídos con `dotnet-dump analyze … -c pe`) dan la misma excepción:
    `Autodesk.Revit.Exceptions.InvalidOperationException: Invalid call to Revit API! Revit is currently not within an
    API context.` en `UIApplication.remove_ViewActivated` ← `PuenteTablero.Soltar()` ← `Window.Closed`. Quitar un evento
    de Revit fuera del contexto de la API lanza, y dentro de un evento de WPF nadie lo atrapa. Zapatas lo envuelve en
    `try/catch`. Corrección (decisión 15): el cierre se cancela en `Closing` y se repite desde la cola de Revit, donde
    se quitan los eventos; si Revit destruye la ventana al cerrarse, `Soltar` lo intenta dentro de un `try`.
11. (2026-11-12, prueba del usuario: «en la planta no se ve, en 3D sí») El journal `journal.0016.txt` muestra que 6 de
    las 7 generaciones dejaron activa «Floor Plan: N-0.65»: las líneas se generaron en la **cimentación**. Con el
    add-in abierto desde la vista 3D, el nivel por defecto era el más bajo del modelo (`GenLevel` de una vista 3D es
    null). En la planta «N+0.00 Piso 1» no se ven porque quedan 0,65 m por debajo de su rango de vista. Ver decisión 23.
12. (2026-11-12, medición de la decisión 22, `diagnostico-dibujo.txt`) En dos corridas (N+0.00 y N-0.65): los clics
    con `PickPoint` y las líneas temporales funcionan; el clic sobre el primer punto cierra; un doble clic se ignora;
    el `RollBack` no deja rastro (84 → 84 y 90 → 90 líneas de modelo). Los botones de la barra («Deshacer punto»,
    «Cancelar») y la tecla Esc quedaron sin registro en el diagnóstico; se probaron después con la versión final (pasos
    53 a 55 de la lista, usuario, 2026-11-12): funcionan. No quedó anotado cuál de los dos envíos de Esc de la barra
    (`PostMessage` o teclado) corta el clic; la barra prueba los dos. El comando de diagnóstico se quitó al cerrar S8.

## Decisiones

Siguen las del spec (1 a 9).

10. **Tramos de retiro sin superponerse** (2026-11-10, Claude): cada línea de retiro va del lado del lote vecino a la
    esquina del área útil, como las líneas naranjas del dibujo de Carlos (min 7–10). El tramo que coincide con el área
    útil se dibuja solo una vez, con el estilo «CQ Área útil». Así no hay dos líneas encima de otra y el área útil queda
    como un contorno cerrado de un solo estilo.
11. **Los laterales comparten estilo** (2026-11-10, Claude): izquierdo y derecho son «CQ Retiro lateral». Cada uno
    tiene su propio valor de retiro.
12. **Volver a generar reemplaza todo lo del add-in en el modelo** (2026-11-10, Claude): un proyecto tiene un lote.
    Se borran todas las líneas con la marca, en cualquier nivel, y se crean las nuevas.
13. **Medición con el MCP, sin comando de diagnóstico** (2026-11-10, Claude): ya está conectado y probado en este
    equipo (`send_code_to_revit`).
14. **Columnas al pegar puntos** (2026-11-10, Claude): X, Y y, opcionalmente, Z. Si una fila trae 4 valores o más y
    el primero no es un número (un rótulo como «P1»), se descarta. Si la primera fila no tiene números, se toma como
    encabezado.
15. **Cerrar la ventana solo dentro del contexto de la API** (2026-11-11, Claude, por el hallazgo 10): `AlPedirCierre`
    cancela el cierre y lo encola en `AccionesRevit`; ahí `QuitarEventos` quita `ViewActivated` y `DocumentClosing` y
    recién entonces se cierra la ventana. `ExternalEvent` se libera con `Dispatcher.BeginInvoke`, fuera de su propia
    ejecución.
16. **Mostrar lo generado** (2026-11-11, usuario: «cuando lo genere se minimice el add-in y me muestre dónde se
    generó; ya otros add-ins la tienen»). Como acero-vigas y zapatas: al terminar se aparta la ventana (`Apartar`,
    vuelve con el botón de la cinta). Si la vista activa no es una planta del nivel elegido, se abre la primera planta
    de ese nivel (de piso, estructural o de techo, sin plantillas) con `uidoc.ActiveView`. Después se seleccionan las
    líneas creadas y se acerca a ellas con `ShowElements`. Si el nivel no tiene planta, se muestran en la vista activa.
17. **Errores humanos que no deben romper el cálculo** (2026-11-11, usuario pidió pruebas de error humano). Revisando
    los caminos de entrada:
    - «Pisos» con decimales (2.5) hacía fallar el JSON entero (`int?`) y la página dejaba de recibir resultados. Se
      lee como número y se toma la parte entera.
    - Frente o fondo en 0 o negativos daban un lote espejado o el aviso genérico. Ahora el aviso dice qué falta.
    - Datos de la ficha en 0 o negativos se ignoran y se avisa, en vez de comparar contra áreas sin sentido.
    - Coordenadas pegadas con separador de miles («1.200.000,50» o «1,200,000.50»), que es como Excel copia una celda
      con formato, se leen bien: el último separador es el decimal.

18. **Elegir dónde se coloca el lote** (2026-11-11, usuario: opción A; sub-issue S7, #338). Solo frente × fondo:
    X e Y (m, coordenadas internas de Revit, las mismas que da un clic) de la esquina izquierda del frente y el giro
    del frente (°, antihorario desde el eje X). El rectángulo se mueve y gira en el protocolo, antes de calcular, así la
    vista previa, las áreas y lo generado coinciden; el modo puntos no cambia. «Elegir en Revit»: se aparta la ventana,
    se abre la planta del nivel si la vista activa no es una planta (`PickPoint` exige un plano de trabajo) y se piden
    dos clics con `Selection.PickPoint(ObjectSnapTypes, mensaje)`, público en `RevitAPIUI.xml` 2026. Esc lanza
    `OperationCanceledException`: se atrapa en el mismo trabajo para que la ventana vuelva. La cuenta va en
    `calculo/Ubicacion.cs` con pruebas.

19. **Cada lado se clasifica por hacia dónde mira** (2026-11-11, usuario: opción A; reemplaza la decisión 7 del spec).
    En un lote en L, el escalón y el fondo real van igual de opuestos al frente, y la regla vieja (un solo posterior,
    el más opuesto) se quedaba con el escalón: el fondo real salía lateral con 1 m en vez de 2. Ahora, con la normal
    hacia afuera de cada lado: si mira más de 45° hacia el fondo (coseno con la dirección hacia adentro del frente
    mayor que cos 45°) es **posterior**; si mira más de 45° hacia la calle, también es **frente** (un lote con
    entrante en la fachada); si no, es **lateral derecho o izquierdo** según hacia qué costado mira. Puede haber más de
    un posterior o ninguno (un triángulo con el frente en la base tiene dos laterales). Cualquier lado se sigue
    cambiando a mano.

20. **Lienzo libre: dibujar en Revit con clics** (2026-11-11, usuario: opción A; será la sub-issue S8). Como lo
    describió Carlos (min 30–32): se aparta la ventana, una barra arriba («Deshacer punto», «Terminar», «Cancelar») y
    clics en la planta con enganches; al terminar, los puntos pasan a la tabla de «Puntos». Antes de programarlo se
    mide en Revit cómo convive el ciclo de `PickPoint` con la barra (regla del repo), y se acuerdan cómo se termina el
    dibujo y si se ve el polígono mientras se dibuja.

21. **Cómo se dibuja el lote a mano** (2026-11-11, usuario: 1-A y 2-A; S8, #339). Termina con **Esc** o con un clic
    sobre el primer punto (con 3 puntos o más); la barra trae «Deshacer punto» y «Cancelar». Mientras se dibuja, cada
    tramo es una línea de modelo temporal dentro de un `TransactionGroup` que se deshace al final (`RollBack`): no
    queda nada en el modelo ni en Ctrl+Z. Los puntos pasan a la tabla en coordenadas del proyecto.
22. **Medición con un comando de diagnóstico, no con el MCP** (2026-11-11, Claude): el MCP ejecuta su código dentro de
    una transacción propia y no recibe clics en una barra. `ComandoDiagnosticoDibujo` (solo en Debug) usa el mismo
    `DibujoLibre` y la misma barra que la versión final y anota en `%AppData%\CQEING\area-util\diagnostico-dibujo.txt`
    cada clic, cada Esc y cada botón de la barra, con la hora. Lo que se mide: (1) si los botones de la barra reciben
    el clic mientras `PickPoint` espera; (2) si el botón logra cortar ese `PickPoint` mandando Esc a Revit, con
    `PostMessage` o con `SetForegroundWindow` + `keybd_event`; (3) si las líneas temporales se ven y si el
    `RollBack` no deja rastro (líneas de modelo antes y después, y Ctrl+Z).

23. **El nivel por defecto es el primer piso, y se ve** (2026-11-12, Claude, por el hallazgo 11): el de la planta
    activa; si no hay planta activa, el primero a cota 0 o más arriba; si todos están bajo cero, el más alto
    (`NivelesLote.PorDefecto`, con pruebas). El botón dice «Generar en <nivel>». Después de generar, si las líneas no se
    ven en la planta que se abrió (plantilla que oculta Líneas o rango de vista), se avisa qué revisar.

24. **Paquete de uso diario** (2026-11-12, usuario: opción A del repaso de uso). Seis arreglos para que el add-in sea
    práctico en el trabajo de una arquitecta:
    1. **Recordar los datos por modelo**: la página guarda lo escrito (lote, ubicación, puntos, retiros, ficha, nivel,
       colores) en su almacenamiento local con el nombre del modelo, como zapatas, y lo trae al volver a abrir.
    2. **Retiros vacíos de entrada**, sin valores de ejemplo. Un retiro vacío se toma como 0 con un aviso amarillo que
       dice cuáles faltan (`EntradaLote.RetirosVacios`, con pruebas); escribir 0 no avisa.
    3. **Barra «Volver al add-in»** arriba del dibujo cuando la ventana se minimiza al generar: un clic la trae de
       vuelta, sin buscar el botón de la cinta. Es solo interfaz, no llama a la API de Revit.
    4. **Ventana más chica** (1180 × 760 como máximo) para ver el modelo detrás; se puede agrandar.
    5. **«Vía pública»** rotulada junto al frente en la vista previa, y la explicación de izquierda y derecha
       (mirando el lote desde la calle) bajo «Lados del lote».
    6. **Botón «Leer puntos»** junto al cuadro de pegar, para cuando se escribe a mano en él.


## Plan paso a paso

Issue madre [#328](https://github.com/utilidadcqeing-prog/Add-in-Acero-en-columnas-2027/issues/328); sub-issues #329 a #334. **S1** = tarea 1; **S2** = tareas 2 a 6; **S3** = tarea 7; **S4** = tarea 8; **S5** = tarea 9;
**S6** = tarea 10. Cada sub-issue anota su hora de inicio y de fin en **Estado**.

Comandos que se repiten:

```powershell
# Pruebas del cálculo
dotnet test addins/area-util/calculo/CQEING.AreaUtil.Calculo.Tests -c Release
# Compilar el add-in para Revit 2026 (Release, para que no choque con la DLL de Debug que Revit tenga cargada)
dotnet build addins/area-util/CQEING.AreaUtil.csproj -c Release -p:RevitVersion=2026
```

---

### Tarea 1 (S1): crear el add-in vacío

**Archivos:**
- Crear: `addins/area-util/addin.json`, `CQEING.AreaUtil.csproj`, `CQEING.AreaUtil.addin`, `AppAreaUtil.cs`,
  `Resources/iconoCQ16.png`, `Resources/iconoCQ32.png` (copiados de `addins/acero-zapatas/Resources/`)
- Crear: `addins/area-util/Tablero/ComandoTablero.cs`, `VentanaTablero.cs`, `AccionesRevit.cs`, `PuenteTablero.cs`,
  `web/index.html`
- Crear: `addins/area-util/calculo/Directory.Build.props`, `calculo/CQEING.AreaUtil.Calculo/CQEING.AreaUtil.Calculo.csproj`,
  `calculo/CQEING.AreaUtil.Calculo.Tests/CQEING.AreaUtil.Calculo.Tests.csproj`
- Modificar: `CQEING-Addins.sln`, `README.md`

**Interfaces:**
- Produce: `CQEING.AreaUtil.Tablero.VentanaTablero` con `event Action<string>? MensajeRecibido`,
  `void Enviar(string json)`, `void Volver()`; `AccionesRevit.Encolar(Action<UIApplication>)` y su evento `Fallo`.

- [ ] **Paso 1: proyectos del cálculo**

`addins/area-util/calculo/Directory.Build.props`:

```xml
<Project>
  <!--
    Corta la herencia de addins/Directory.Build.props: el cálculo del área útil no usa Revit, WPF ni addins/compartido.
    Así compila en net8.0 puro y lo pueden usar los add-ins de 2026 y 2027.
  -->
</Project>
```

`calculo/CQEING.AreaUtil.Calculo/CQEING.AreaUtil.Calculo.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>CQEING.AreaUtil.Calculo</RootNamespace>
  </PropertyGroup>

</Project>
```

`calculo/CQEING.AreaUtil.Calculo.Tests/CQEING.AreaUtil.Calculo.Tests.csproj`: igual al de zapatas, con la referencia
al proyecto nuevo:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\CQEING.AreaUtil.Calculo\CQEING.AreaUtil.Calculo.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Paso 2: proyecto del add-in**

`addins/area-util/addin.json`:

```json
{
  "id": "area-util",
  "nombre": "Área útil del lote",
  "descripcion": "Dibuja el lote, los retiros de la curaduría y el área útil del primer piso, con vista previa y avisos de la ficha.",
  "proyecto": "CQEING.AreaUtil.csproj",
  "revit": ["2026", "2027"],
  "heredados": []
}
```

`CQEING.AreaUtil.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <!-- Año de Revit, framework, API por NuGet y salida bin/<config>/<año>/: addins/Directory.Build.props. -->
  <PropertyGroup>
    <AssemblyName>CQEING.AreaUtil</AssemblyName>
    <RootNamespace>CQEING.AreaUtil</RootNamespace>
    <Company>CQEING</Company>
    <Product>CQEING - Área útil del lote</Product>
    <Copyright>© CQEING</Copyright>
    <!-- El cálculo vive en calculo/ como proyecto aparte (sin Revit): no se compila dos veces. -->
    <DefaultItemExcludes>$(DefaultItemExcludes);calculo\**</DefaultItemExcludes>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="calculo\CQEING.AreaUtil.Calculo\CQEING.AreaUtil.Calculo.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- Solo para compilar: Revit ya trae WebView2 (Core, Wpf y WebView2Loader); no se copian a la salida. -->
    <PackageReference Include="Microsoft.Web.WebView2" Version="$(WebView2Version)" ExcludeAssets="runtime;native;build;buildTransitive" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <Content Include="Resources\*.png" CopyToOutputDirectory="PreserveNewest" />
    <None Include="CQEING.AreaUtil.addin" CopyToOutputDirectory="PreserveNewest" />
    <!-- La página del tablero: se sirve desde bin\...\web con un host virtual de WebView2. -->
    <Content Include="Tablero\web\**" CopyToOutputDirectory="PreserveNewest" Link="web\%(RecursiveDir)%(Filename)%(Extension)" />
  </ItemGroup>

</Project>
```

`CQEING.AreaUtil.addin` (ClientId propio, generado el 2026-11-10):

```xml
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>CQEING - Área útil del lote</Name>
    <!-- El gestor reescribe esta ruta como absoluta a la carpeta de la versión instalada. -->
    <Assembly>CQEING.AreaUtil.dll</Assembly>
    <FullClassName>CQEING.AreaUtil.AppAreaUtil</FullClassName>
    <ClientId>e51dd9c3-c817-4e2b-bd1e-ac940fa5171e</ClientId>
    <VendorId>CQEING</VendorId>
    <VendorDescription>CQEING</VendorDescription>
  </AddIn>
</RevitAddIns>
```

`AppAreaUtil.cs`:

```csharp
using Autodesk.Revit.UI;
using CQEING.Compartido;

namespace CQEING.AreaUtil;

/// <summary>Registra el panel «Lote» en la pestaña CQEING. Reunión del 09-nov-2026; spec 2026-11-10-area-util-lote.</summary>
public class AppAreaUtil : IExternalApplication
{
    public Result OnStartup(UIControlledApplication app)
    {
        // Sin licencia vigente no se crea el botón; el aviso lo da el componente del gestor (issue #189).
        if (!Licencia.Vigente(out _)) return Result.Succeeded;
        // Con una versión nueva publicada, tampoco: hay que actualizar desde el gestor (actualización obligatoria).
        if (!BloqueoPorVersion.AlDia(app)) return Result.Succeeded;

        RibbonPanel panel = CintaCqeing.ObtenerPanel(app, "Lote");
        panel.AddItem(CintaCqeing.CrearBoton(
            "ComandoAreaUtil",
            "Área útil del lote",
            "CQEING.AreaUtil.Tablero.ComandoTablero",
            "Dibuja el lote, los retiros de la curaduría y el área útil del primer piso.",
            "iconoCQ"));

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;
}
```

- [ ] **Paso 3: ventana, comando, cola y puente**

`Tablero/AccionesRevit.cs`: copia de `addins/acero-zapatas/Tablero/AccionesRevit.cs` con estos cambios:
- `namespace CQEING.AreaUtil.Tablero;`
- `RutaErrores`: `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CQEING", "area-util", "errores.log")`
- `GetName()` devuelve `"CQEING Área útil del lote"`.

`Tablero/VentanaTablero.cs`: copia de `addins/acero-zapatas/Tablero/VentanaTablero.cs` con estos cambios:
- `namespace CQEING.AreaUtil.Tablero;`
- `private const string Host = "area-util.cqeing";`
- `Title = "Área útil del lote";`
- `CarpetaDatos`: `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CQEING", "area-util", "WebView2")`
- El texto del `MessageBox` de error: `"No se pudo abrir el tablero del área útil."`.
- Se quitan `Apartar()` y `estadoAntes`, porque la versión 1 no los usa; `Volver()` queda solo con `Activate()` y la
  restauración del minimizado.

`Tablero/ComandoTablero.cs`: copia de `addins/acero-zapatas/Tablero/ComandoTablero.cs` con estos cambios:
- `namespace CQEING.AreaUtil.Tablero;`, `Titulo = "Área útil del lote"`
- El aviso sin modelo: `"Abra un modelo de proyecto para dibujar el lote."`
- El resumen de la clase: `/// <summary>Abre el tablero del área útil, una sola ventana sin modo; si ya está abierta, la trae al frente.</summary>`

`Tablero/PuenteTablero.cs`, versión mínima para esta tarea (la tarea 8 la completa):

```csharp
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace CQEING.AreaUtil.Tablero;

/// <summary>Une la página del tablero con Revit. La tarea 1 del plan solo cierra la ventana.</summary>
internal sealed class PuenteTablero
{
    private readonly VentanaTablero ventana;
    private readonly AccionesRevit acciones = new();

    public PuenteTablero(VentanaTablero ventana, UIApplication uiapp)
    {
        this.ventana = ventana;
        ventana.MensajeRecibido += json =>
        {
            if (json.Contains("\"cerrar\"", StringComparison.Ordinal))
                ventana.Close();
        };
        ventana.Closed += (_, _) => acciones.Dispose();
    }
}
```

`Tablero/web/index.html`, la maqueta vacía de tres columnas (la tarea 8 la reemplaza):

```html
<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<title>Área útil del lote</title>
<style>
body{margin:0;height:100vh;display:grid;grid-template-columns:320px 1fr 340px;gap:12px;padding:12px;box-sizing:border-box;background:#DDE1E6;font:13px 'Segoe UI',system-ui,sans-serif}
section{background:#fff;border:1px solid #D0D5DB;border-radius:6px;padding:12px}
</style>
</head>
<body>
<section><h2>Datos del lote</h2></section>
<section><h2>Vista previa</h2></section>
<section><h2>Resumen</h2></section>
</body>
</html>
```

- [ ] **Paso 4: solución y README**

```powershell
dotnet sln CQEING-Addins.sln add --solution-folder area-util addins/area-util/CQEING.AreaUtil.csproj addins/area-util/calculo/CQEING.AreaUtil.Calculo/CQEING.AreaUtil.Calculo.csproj addins/area-util/calculo/CQEING.AreaUtil.Calculo.Tests/CQEING.AreaUtil.Calculo.Tests.csproj
```

En la tabla de `README.md`, debajo de `addins/despiece/`:

```markdown
| `addins/area-util/` | Área útil del lote: lote, retiros de la curaduría y área útil del primer piso, con vista previa. |
```

- [ ] **Paso 5: compilar y probar**

```powershell
dotnet build addins/area-util/CQEING.AreaUtil.csproj -c Release -p:RevitVersion=2026
dotnet test CQEING-Addins.sln -c Release
```

Esperado: compila sin advertencias nuevas; las pruebas pasan, incluida `TodosLosAddinsExigenLicenciaTests` con el
add-in nuevo.

- [ ] **Paso 6: probar en Revit** (el usuario mira la pantalla)

```powershell
Get-Process -Name Revit -ErrorAction SilentlyContinue   # vacío
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin area-util -Revit 2026
```

Abrir Revit 2026, pestaña CQEING, panel «Lote», botón «Área útil del lote»: abre la ventana con las tres columnas. Si
se pulsa otra vez, trae la misma ventana al frente.

- [ ] **Paso 7: commit** (solo si el usuario lo pide)

```bash
git add addins/area-util CQEING-Addins.sln README.md docs/planes/2026-11-10-addin-area-util.md docs/superpowers/specs/2026-11-10-area-util-lote-design.md
git commit -m "feat(area-util): crea el add-in del área útil del lote con su ventana vacía (S1)"
```

---

### Tarea 2 (S2): `Punto` y `Poligono`

**Archivos:**
- Crear: `addins/area-util/calculo/CQEING.AreaUtil.Calculo/Geometria.cs`
- Prueba: `addins/area-util/calculo/CQEING.AreaUtil.Calculo.Tests/PoligonoTests.cs`

**Interfaces:**
- Produce: `readonly record struct Punto(double X, double Y)` con `+`, `-`, `* double`, `Cruz`, `Escalar`, `Largo`,
  `Unitario()`; `sealed class Poligono` con `Vertices`, `Lados`, `Lado(int)`, `LargoLado(int)`, `AreaConSigno`,
  `Area`, `EsAntihorario`, `NormalAdentro(int)`, `SeCruza()`, `Contiene(Punto)`, `static Rectangulo(double, double)`,
  `const double Tolerancia`; `static class Rectas` con `Interseccion(Punto, Punto, Punto, Punto, out Punto)`.

- [ ] **Paso 1: escribir las pruebas que fallan**

```csharp
namespace CQEING.AreaUtil.Calculo.Tests;

public class PoligonoTests
{
    [Fact]
    public void El_rectangulo_de_7_por_14_mide_98_m2() =>
        Assert.Equal(98, Poligono.Rectangulo(7, 14).Area, 6);

    [Fact]
    public void Con_el_primer_punto_repetido_al_final_tiene_cuatro_lados()
    {
        var p = new Poligono([new(0, 0), new(7, 0), new(7, 14), new(0, 14), new(0, 0)]);
        Assert.Equal(4, p.Lados);
    }

    [Fact]
    public void Quita_puntos_repetidos_y_alineados()
    {
        var p = new Poligono([new(0, 0), new(3, 0), new(3, 0), new(7, 0), new(7, 14), new(0, 14)]);
        Assert.Equal(4, p.Lados);
    }

    [Fact]
    public void Con_coordenadas_topograficas_grandes_el_area_no_pierde_precision()
    {
        var p = new Poligono([new(1_200_000, 900_000), new(1_200_007, 900_000), new(1_200_007, 900_014), new(1_200_000, 900_014)]);
        Assert.Equal(98, p.Area, 6);
    }

    [Fact]
    public void Horario_y_antihorario_miden_lo_mismo()
    {
        var anti = Poligono.Rectangulo(7, 14);
        var horario = new Poligono(anti.Vertices.Reverse());
        Assert.True(anti.EsAntihorario);
        Assert.False(horario.EsAntihorario);
        Assert.Equal(anti.Area, horario.Area, 6);
    }

    [Fact]
    public void La_normal_hacia_adentro_del_frente_apunta_al_lote_en_los_dos_sentidos()
    {
        var anti = Poligono.Rectangulo(7, 14);
        Assert.Equal(new Punto(0, 1), anti.NormalAdentro(0));
        var horario = new Poligono([new(0, 0), new(0, 14), new(7, 14), new(7, 0)]);
        Assert.Equal(new Punto(1, 0), horario.NormalAdentro(0));
    }

    [Fact]
    public void Un_mono_de_lados_cruzados_se_detecta()
    {
        var p = new Poligono([new(0, 0), new(7, 14), new(7, 0), new(0, 14)]);
        Assert.True(p.SeCruza());
        Assert.False(Poligono.Rectangulo(7, 14).SeCruza());
    }

    [Fact]
    public void Contiene_distingue_adentro_y_afuera()
    {
        var p = Poligono.Rectangulo(7, 14);
        Assert.True(p.Contiene(new(3, 3)));
        Assert.False(p.Contiene(new(8, 3)));
    }

    [Fact]
    public void La_interseccion_de_dos_rectas()
    {
        Assert.True(Rectas.Interseccion(new(0, 1.5), new(1, 0), new(1, 0), new(0, 1), out Punto p));
        Assert.Equal(new Punto(1, 1.5), p);
        Assert.False(Rectas.Interseccion(new(0, 0), new(1, 0), new(0, 1), new(1, 0), out _));
    }
}
```

- [ ] **Paso 2: correrlas y ver que fallan**

Correr: `dotnet test addins/area-util/calculo/CQEING.AreaUtil.Calculo.Tests -c Release`
Esperado: no compila (`Poligono` no existe).

- [ ] **Paso 3: implementar `Geometria.cs`**

```csharp
namespace CQEING.AreaUtil.Calculo;

/// <summary>Un punto o un vector en planta, en metros.</summary>
public readonly record struct Punto(double X, double Y)
{
    public static Punto operator +(Punto a, Punto b) => new(a.X + b.X, a.Y + b.Y);
    public static Punto operator -(Punto a, Punto b) => new(a.X - b.X, a.Y - b.Y);
    public static Punto operator -(Punto a) => new(-a.X, -a.Y);
    public static Punto operator *(Punto a, double k) => new(a.X * k, a.Y * k);

    public double Cruz(Punto b) => X * b.Y - Y * b.X;
    public double Escalar(Punto b) => X * b.X + Y * b.Y;
    public double Largo => Math.Sqrt(X * X + Y * Y);
    // Se divide (no se multiplica por 1/largo) para que (7, 0) dé exactamente (1, 0).
    public Punto Unitario() => new(X / Largo, Y / Largo);
}

/// <summary>Rectas infinitas dadas por un punto y una dirección.</summary>
public static class Rectas
{
    /// <summary>Corte de <c>p + t·u</c> con <c>q + s·v</c>. False si son paralelas.</summary>
    public static bool Interseccion(Punto p, Punto u, Punto q, Punto v, out Punto corte)
    {
        double denominador = u.Cruz(v);
        if (Math.Abs(denominador) < 1e-12)
        {
            corte = default;
            return false;
        }
        corte = p + u * ((q - p).Cruz(v) / denominador);
        return true;
    }
}

/// <summary>
/// El contorno de un lote o del área útil, en metros. Quita los puntos repetidos (también el último igual al primero,
/// como lo cierran los topógrafos) y los que quedan alineados entre sus vecinos.
/// </summary>
public sealed class Poligono
{
    /// <summary>Un milímetro: por debajo, dos puntos son el mismo.</summary>
    public const double Tolerancia = 1e-3;

    public Poligono(IEnumerable<Punto> puntos)
    {
        var limpios = new List<Punto>();
        foreach (Punto p in puntos)
            if (limpios.Count == 0 || (p - limpios[^1]).Largo > Tolerancia)
                limpios.Add(p);
        if (limpios.Count > 1 && (limpios[0] - limpios[^1]).Largo <= Tolerancia)
            limpios.RemoveAt(limpios.Count - 1);
        bool quito = true;
        while (quito && limpios.Count > 3)
        {
            quito = false;
            for (int i = 0; i < limpios.Count; i++)
            {
                Punto a = limpios[(i - 1 + limpios.Count) % limpios.Count], b = limpios[i], c = limpios[(i + 1) % limpios.Count];
                Punto ab = b - a, bc = c - b;
                // Alineado y siguiendo el mismo sentido: el punto del medio no cambia la forma.
                if (Math.Abs(ab.Unitario().Cruz(bc.Unitario())) < 1e-9 && ab.Escalar(bc) > 0)
                {
                    limpios.RemoveAt(i);
                    quito = true;
                    break;
                }
            }
        }
        Vertices = limpios;
    }

    public IReadOnlyList<Punto> Vertices { get; }

    public int Lados => Vertices.Count;

    public static Poligono Rectangulo(double frente, double fondo) => new([new(0, 0), new(frente, 0), new(frente, fondo), new(0, fondo)]);

    public (Punto A, Punto B) Lado(int i) => (Vertices[i], Vertices[(i + 1) % Lados]);

    public double LargoLado(int i) => (Lado(i).B - Lado(i).A).Largo;

    /// <summary>Positiva en sentido antihorario. Se mide desde el primer punto para no perder precisión con coordenadas grandes.</summary>
    public double AreaConSigno
    {
        get
        {
            if (Lados < 3)
                return 0;
            Punto o = Vertices[0];
            double doble = 0;
            for (int i = 1; i < Lados - 1; i++)
                doble += (Vertices[i] - o).Cruz(Vertices[i + 1] - o);
            return doble / 2;
        }
    }

    public double Area => Math.Abs(AreaConSigno);

    public bool EsAntihorario => AreaConSigno > 0;

    /// <summary>La normal unitaria del lado <paramref name="i"/> que apunta hacia adentro del polígono.</summary>
    public Punto NormalAdentro(int i)
    {
        (Punto a, Punto b) = Lado(i);
        Punto d = (b - a).Unitario();
        var izquierda = new Punto(-d.Y, d.X);
        return EsAntihorario ? izquierda : -izquierda;
    }

    /// <summary>True si dos lados que no son vecinos se tocan.</summary>
    public bool SeCruza()
    {
        for (int i = 0; i < Lados; i++)
            for (int j = i + 1; j < Lados; j++)
            {
                bool vecinos = j == i + 1 || (i == 0 && j == Lados - 1);
                if (!vecinos && SeTocan(Lado(i), Lado(j)))
                    return true;
            }
        return false;
    }

    /// <summary>Regla par-impar: un rayo hacia la derecha cruza un número impar de lados si el punto está adentro.</summary>
    public bool Contiene(Punto p)
    {
        bool adentro = false;
        for (int i = 0, j = Lados - 1; i < Lados; j = i++)
        {
            Punto a = Vertices[i], b = Vertices[j];
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
                adentro = !adentro;
        }
        return adentro;
    }

    private static bool SeTocan((Punto A, Punto B) s, (Punto A, Punto B) t)
    {
        double o1 = (s.B - s.A).Cruz(t.A - s.A), o2 = (s.B - s.A).Cruz(t.B - s.A);
        double o3 = (t.B - t.A).Cruz(s.A - t.A), o4 = (t.B - t.A).Cruz(s.B - t.A);
        if (((o1 > 0 && o2 < 0) || (o1 < 0 && o2 > 0)) && ((o3 > 0 && o4 < 0) || (o3 < 0 && o4 > 0)))
            return true;
        return (Math.Abs(o1) < 1e-12 && EnSegmento(s, t.A)) || (Math.Abs(o2) < 1e-12 && EnSegmento(s, t.B))
            || (Math.Abs(o3) < 1e-12 && EnSegmento(t, s.A)) || (Math.Abs(o4) < 1e-12 && EnSegmento(t, s.B));
    }

    private static bool EnSegmento((Punto A, Punto B) s, Punto p) =>
        p.X >= Math.Min(s.A.X, s.B.X) - 1e-12 && p.X <= Math.Max(s.A.X, s.B.X) + 1e-12
        && p.Y >= Math.Min(s.A.Y, s.B.Y) - 1e-12 && p.Y <= Math.Max(s.A.Y, s.B.Y) + 1e-12;
}
```

- [ ] **Paso 4: correr las pruebas y ver que pasan**

Correr: `dotnet test addins/area-util/calculo/CQEING.AreaUtil.Calculo.Tests -c Release`
Esperado: 9 pruebas correctas.

- [ ] **Paso 5: commit** (solo si el usuario lo pide): `feat(area-util): polígono del lote con área, sentido y cruces (S2)`

---

### Tarea 3 (S2): tipos de lado

**Archivos:**
- Crear: `calculo/CQEING.AreaUtil.Calculo/LadosLote.cs`
- Prueba: `calculo/CQEING.AreaUtil.Calculo.Tests/LadosLoteTests.cs`

**Interfaces:**
- Consume: `Poligono`, `Punto` (tarea 2).
- Produce: `enum TipoLado { Frente, Posterior, LateralIzquierdo, LateralDerecho }`;
  `LadosLote.Clasificar(Poligono lote, int frente) → TipoLado[]`;
  `sealed record Retiros(double Frontal, double Posterior, double LateralIzquierdo, double LateralDerecho)` con
  `double Para(TipoLado)`.

- [ ] **Paso 1: escribir las pruebas que fallan**

```csharp
namespace CQEING.AreaUtil.Calculo.Tests;

public class LadosLoteTests
{
    [Fact]
    public void En_el_rectangulo_el_frente_abajo_deja_el_posterior_arriba_y_los_laterales_a_los_lados()
    {
        TipoLado[] tipos = LadosLote.Clasificar(Poligono.Rectangulo(7, 14), 0);
        Assert.Equal([TipoLado.Frente, TipoLado.LateralDerecho, TipoLado.Posterior, TipoLado.LateralIzquierdo], tipos);
    }

    [Fact]
    public void En_sentido_horario_derecha_e_izquierda_siguen_siendo_las_de_quien_mira_desde_la_calle()
    {
        var horario = new Poligono([new(0, 0), new(0, 14), new(7, 14), new(7, 0)]);
        // El frente es el lado de abajo (7,0)-(0,0), que en este orden es el lado 3.
        TipoLado[] tipos = LadosLote.Clasificar(horario, 3);
        Assert.Equal([TipoLado.LateralIzquierdo, TipoLado.Posterior, TipoLado.LateralDerecho, TipoLado.Frente], tipos);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Cualquier_lado_puede_ser_el_frente_y_el_opuesto_queda_posterior(int frente)
    {
        TipoLado[] tipos = LadosLote.Clasificar(Poligono.Rectangulo(7, 14), frente);
        Assert.Equal(TipoLado.Frente, tipos[frente]);
        Assert.Equal(TipoLado.Posterior, tipos[(frente + 2) % 4]);
    }

    [Fact]
    public void En_un_lote_en_L_hay_un_solo_frente_y_un_solo_posterior()
    {
        var l = new Poligono([new(0, 0), new(10, 0), new(10, 6), new(5, 6), new(5, 12), new(0, 12)]);
        TipoLado[] tipos = LadosLote.Clasificar(l, 0);
        Assert.Single(tipos, t => t == TipoLado.Frente);
        Assert.Single(tipos, t => t == TipoLado.Posterior);
    }

    [Fact]
    public void Un_indice_fuera_de_rango_se_toma_dando_la_vuelta() =>
        Assert.Equal(TipoLado.Frente, LadosLote.Clasificar(Poligono.Rectangulo(7, 14), 5)[1]);

    [Fact]
    public void Cada_tipo_toma_su_retiro()
    {
        var r = new Retiros(1.5, 2, 1, 0.5);
        Assert.Equal(1.5, r.Para(TipoLado.Frente));
        Assert.Equal(2, r.Para(TipoLado.Posterior));
        Assert.Equal(1, r.Para(TipoLado.LateralIzquierdo));
        Assert.Equal(0.5, r.Para(TipoLado.LateralDerecho));
    }
}
```

- [ ] **Paso 2: correrlas y ver que fallan** (no compila: `LadosLote` no existe).

- [ ] **Paso 3: implementar `LadosLote.cs`**

```csharp
namespace CQEING.AreaUtil.Calculo;

/// <summary>El papel de cada lado del lote frente a la norma (min 3–5 y 33 de la reunión del 09-nov).</summary>
public enum TipoLado
{
    Frente,
    Posterior,
    LateralIzquierdo,
    LateralDerecho,
}

/// <summary>Los retiros de la ficha de la curaduría, en metros.</summary>
public sealed record Retiros(double Frontal, double Posterior, double LateralIzquierdo, double LateralDerecho)
{
    public double Para(TipoLado tipo) => tipo switch
    {
        TipoLado.Frente => Frontal,
        TipoLado.Posterior => Posterior,
        TipoLado.LateralIzquierdo => LateralIzquierdo,
        _ => LateralDerecho,
    };
}

/// <summary>Decisión 7 del spec: el usuario marca el frente y el resto se deduce; luego se puede cambiar a mano.</summary>
public static class LadosLote
{
    public static TipoLado[] Clasificar(Poligono lote, int frente)
    {
        int n = lote.Lados;
        frente = ((frente % n) + n) % n;
        (Punto fa, Punto fb) = lote.Lado(frente);
        Punto direccion = (fb - fa).Unitario();
        Punto adentro = lote.NormalAdentro(frente);
        // Quien está en la calle mirando hacia el lote tiene la derecha girando 90° en sentido horario.
        var derecha = new Punto(adentro.Y, -adentro.X);
        Punto medioFrente = (fa + fb) * 0.5;

        int posterior = -1;
        double masOpuesto = double.MaxValue;
        for (int i = 0; i < n; i++)
        {
            if (i == frente)
                continue;
            (Punto a, Punto b) = lote.Lado(i);
            double coseno = direccion.Escalar((b - a).Unitario());
            if (coseno < masOpuesto)
            {
                masOpuesto = coseno;
                posterior = i;
            }
        }

        var tipos = new TipoLado[n];
        for (int i = 0; i < n; i++)
        {
            (Punto a, Punto b) = lote.Lado(i);
            tipos[i] = i == frente ? TipoLado.Frente
                : i == posterior ? TipoLado.Posterior
                : ((a + b) * 0.5 - medioFrente).Escalar(derecha) > 0 ? TipoLado.LateralDerecho
                : TipoLado.LateralIzquierdo;
        }
        return tipos;
    }
}
```

- [ ] **Paso 4: correr las pruebas y ver que pasan** (las de la tarea 2 siguen pasando).

- [ ] **Paso 5: commit** (solo si el usuario lo pide): `feat(area-util): frente, posterior y laterales del lote (S2)`

---

### Tarea 4 (S2): desfase de los retiros y trazos

**Archivos:**
- Crear: `calculo/CQEING.AreaUtil.Calculo/DesfaseRetiros.cs`
- Prueba: `calculo/CQEING.AreaUtil.Calculo.Tests/DesfaseRetirosTests.cs`

**Interfaces:**
- Consume: `Poligono`, `Punto`, `Rectas`, `TipoLado`, `Retiros`.
- Produce: `static class Estilos` (claves `Lote`, `RetiroFrontal`, `RetiroLateral`, `RetiroPosterior`, `AreaUtil` y
  `DeRetiro(TipoLado)`); `sealed record Trazo(Punto A, Punto B, string Estilo)`;
  `sealed record Desfase(Poligono? Util, IReadOnlyList<Trazo> Trazos, string? Problema)`;
  `DesfaseRetiros.Calcular(Poligono lote, IReadOnlyList<TipoLado> tipos, Retiros retiros) → Desfase`.

- [ ] **Paso 1: escribir las pruebas que fallan**

```csharp
namespace CQEING.AreaUtil.Calculo.Tests;

public class DesfaseRetirosTests
{
    private static readonly Retiros Ficha = new(1.5, 2, 1, 1);

    private static Desfase Rectangulo(Poligono lote, int frente = 0) =>
        DesfaseRetiros.Calcular(lote, LadosLote.Clasificar(lote, frente), Ficha);

    [Fact]
    public void El_lote_de_7_por_14_deja_52_5_m2_utiles()
    {
        Desfase d = Rectangulo(Poligono.Rectangulo(7, 14));
        Assert.Null(d.Problema);
        Assert.Equal(52.5, d.Util!.Area, 6);
        Assert.Contains(new Punto(1, 1.5), d.Util.Vertices);
        Assert.Contains(new Punto(6, 12), d.Util.Vertices);
    }

    [Fact]
    public void En_sentido_horario_el_area_util_es_la_misma()
    {
        var horario = new Poligono([new(0, 0), new(0, 14), new(7, 14), new(7, 0)]);
        Desfase d = Rectangulo(horario, 3);
        Assert.Equal(52.5, d.Util!.Area, 6);
    }

    [Fact]
    public void Con_el_frente_en_otro_lado_cambian_los_retiros()
    {
        // Frente a la derecha (lado 1, x = 7): ese lado se corre 1.5 y el de la izquierda (posterior) 2.
        Desfase d = Rectangulo(Poligono.Rectangulo(7, 14), 1);
        Assert.Equal((7 - 1.5 - 2) * (14 - 1 - 1), d.Util!.Area, 6);
    }

    [Fact]
    public void Retiros_en_cero_dejan_el_mismo_lote() =>
        Assert.Equal(98, DesfaseRetiros.Calcular(Poligono.Rectangulo(7, 14), LadosLote.Clasificar(Poligono.Rectangulo(7, 14), 0), new Retiros(0, 0, 0, 0)).Util!.Area, 6);

    [Fact]
    public void Un_trapecio_tambien_se_desfasa()
    {
        var trapecio = new Poligono([new(0, 0), new(10, 0), new(8, 12), new(2, 12)]);
        Desfase d = Rectangulo(trapecio);
        Assert.Null(d.Problema);
        Assert.True(d.Util!.Area > 0 && d.Util.Area < trapecio.Area);
        Assert.All(d.Util.Vertices, v => Assert.True(trapecio.Contiene(v)));
    }

    [Fact]
    public void Un_lote_angosto_con_laterales_que_se_comen_el_frente_da_problema()
    {
        var lote = Poligono.Rectangulo(7, 14);
        Desfase d = DesfaseRetiros.Calcular(lote, LadosLote.Clasificar(lote, 0), new Retiros(1.5, 2, 4, 4));
        Assert.Null(d.Util);
        Assert.Contains("se comen", d.Problema);
    }

    [Fact]
    public void Un_retiro_negativo_da_problema()
    {
        var lote = Poligono.Rectangulo(7, 14);
        Assert.NotNull(DesfaseRetiros.Calcular(lote, LadosLote.Clasificar(lote, 0), new Retiros(-1, 2, 1, 1)).Problema);
    }

    [Fact]
    public void Los_trazos_no_se_superponen_y_el_area_util_queda_cerrada()
    {
        Desfase d = Rectangulo(Poligono.Rectangulo(7, 14));
        Assert.Equal(4, d.Trazos.Count(t => t.Estilo == Estilos.Lote));
        Assert.Equal(4, d.Trazos.Count(t => t.Estilo == Estilos.AreaUtil));
        // Cada línea de retiro va del lado vecino a la esquina del área útil: dos tramos por retiro.
        Assert.Equal(2, d.Trazos.Count(t => t.Estilo == Estilos.RetiroFrontal));
        Assert.Contains(d.Trazos, t => t.Estilo == Estilos.RetiroFrontal && t.A == new Punto(0, 1.5) && t.B == new Punto(1, 1.5));
        Assert.Equal(4, d.Trazos.Count(t => t.Estilo == Estilos.RetiroLateral));
        Assert.Equal(2, d.Trazos.Count(t => t.Estilo == Estilos.RetiroPosterior));
    }

    [Fact]
    public void Un_retiro_en_cero_no_dibuja_tramos_de_ese_lado()
    {
        var lote = Poligono.Rectangulo(7, 14);
        Desfase d = DesfaseRetiros.Calcular(lote, LadosLote.Clasificar(lote, 0), new Retiros(1.5, 2, 0, 1));
        Assert.Equal(2, d.Trazos.Count(t => t.Estilo == Estilos.RetiroLateral));
    }
}
```

- [ ] **Paso 2: correrlas y ver que fallan** (no compila: `DesfaseRetiros` no existe).

- [ ] **Paso 3: implementar `DesfaseRetiros.cs`**

```csharp
namespace CQEING.AreaUtil.Calculo;

/// <summary>Las claves de los estilos de línea (decisiones 1, 2 y 11 del plan). Revit les pone el nombre «CQ …».</summary>
public static class Estilos
{
    public const string Lote = "lote";
    public const string RetiroFrontal = "retiro-frontal";
    public const string RetiroLateral = "retiro-lateral";
    public const string RetiroPosterior = "retiro-posterior";
    public const string AreaUtil = "area-util";

    public static string DeRetiro(TipoLado tipo) => tipo switch
    {
        TipoLado.Frente => RetiroFrontal,
        TipoLado.Posterior => RetiroPosterior,
        _ => RetiroLateral,
    };
}

/// <summary>Un tramo recto que se dibuja en Revit con el estilo <paramref name="Estilo"/>.</summary>
public sealed record Trazo(Punto A, Punto B, string Estilo);

/// <summary>El área útil y lo que se dibuja; si los retiros no dejan área válida, <see cref="Problema"/> dice por qué.</summary>
public sealed record Desfase(Poligono? Util, IReadOnlyList<Trazo> Trazos, string? Problema);

/// <summary>
/// Decisión 6 del spec: cada lado se corre hacia adentro su retiro y cada esquina del área útil es el corte de dos lados
/// vecinos ya corridos. Decisión 10 del plan: los tramos de retiro van del lado vecino del lote a la esquina del área útil.
/// </summary>
public static class DesfaseRetiros
{
    public static Desfase Calcular(Poligono lote, IReadOnlyList<TipoLado> tipos, Retiros retiros)
    {
        int n = lote.Lados;
        var origen = new Punto[n];
        var direccion = new Punto[n];
        var distancia = new double[n];
        for (int i = 0; i < n; i++)
        {
            distancia[i] = retiros.Para(tipos[i]);
            if (distancia[i] < 0)
                return Fallo("Los retiros no pueden ser negativos.");
            (Punto a, Punto b) = lote.Lado(i);
            direccion[i] = (b - a).Unitario();
            origen[i] = a + lote.NormalAdentro(i) * distancia[i];
        }

        var esquinas = new Punto[n];
        for (int j = 0; j < n; j++)
        {
            int previo = (j - 1 + n) % n;
            if (!Rectas.Interseccion(origen[previo], direccion[previo], origen[j], direccion[j], out esquinas[j]))
                return Fallo($"Los lados {previo + 1} y {j + 1} son paralelos y seguidos: revise los puntos del lote.");
        }

        // Si un lado del área útil quedó al revés, los retiros de sus vecinos se lo comieron.
        for (int i = 0; i < n; i++)
            if ((esquinas[(i + 1) % n] - esquinas[i]).Escalar(direccion[i]) <= Poligono.Tolerancia)
                return Fallo($"Los retiros se comen el lado {i + 1} ({Nombre(tipos[i])}): no queda área útil.");

        var util = new Poligono(esquinas);
        if (util.Lados < 3 || util.Area <= 0.01 || util.EsAntihorario != lote.EsAntihorario || util.SeCruza())
            return Fallo("Los retiros no dejan un área útil válida.");
        if (esquinas.Any(e => !lote.Contiene(e)))
            return Fallo("El área útil se sale del lote: revise los retiros.");

        var trazos = new List<Trazo>();
        for (int i = 0; i < n; i++)
            trazos.Add(new Trazo(lote.Lado(i).A, lote.Lado(i).B, Estilos.Lote));
        for (int i = 0; i < n; i++)
            trazos.Add(new Trazo(esquinas[i], esquinas[(i + 1) % n], Estilos.AreaUtil));
        for (int i = 0; i < n; i++)
        {
            if (distancia[i] <= Poligono.Tolerancia)
                continue;
            int previo = (i - 1 + n) % n, siguiente = (i + 1) % n;
            string estilo = Estilos.DeRetiro(tipos[i]);
            double T(Punto p) => (p - origen[i]).Escalar(direccion[i]);
            if (Rectas.Interseccion(origen[i], direccion[i], lote.Lado(previo).A, direccion[previo], out Punto inicio)
                && T(esquinas[i]) - T(inicio) > Poligono.Tolerancia)
                trazos.Add(new Trazo(inicio, esquinas[i], estilo));
            if (Rectas.Interseccion(origen[i], direccion[i], lote.Lado(siguiente).A, direccion[siguiente], out Punto fin)
                && T(fin) - T(esquinas[siguiente]) > Poligono.Tolerancia)
                trazos.Add(new Trazo(esquinas[siguiente], fin, estilo));
        }
        return new Desfase(util, trazos, null);
    }

    public static string Nombre(TipoLado tipo) => tipo switch
    {
        TipoLado.Frente => "frente",
        TipoLado.Posterior => "posterior",
        TipoLado.LateralIzquierdo => "lateral izquierdo",
        _ => "lateral derecho",
    };

    private static Desfase Fallo(string problema) => new(null, [], problema);
}
```

- [ ] **Paso 4: correr las pruebas y ver que pasan.** Las esquinas del rectángulo salen exactas porque `Unitario()`
  divide por el largo; si alguna comparación exacta falla por redondeo, se cambia a comparar con tolerancia y se anota
  en Hallazgos.

- [ ] **Paso 5: commit** (solo si el usuario lo pide): `feat(area-util): desfase de retiros y trazos del lote (S2)`

---

### Tarea 5 (S2): ficha de la curaduría y cálculo completo

**Archivos:**
- Crear: `calculo/CQEING.AreaUtil.Calculo/CalculoLote.cs`
- Prueba: `calculo/CQEING.AreaUtil.Calculo.Tests/CalculoLoteTests.cs`

**Interfaces:**
- Consume: todo lo anterior.
- Produce: `sealed record Ficha(double? Imo = null, double? Imc = null, int? Pisos = null, double? LoteMinimo = null, double? FrenteMinimo = null)`;
  `sealed record Aviso(string Texto, bool Bloquea)`;
  `sealed record Resumen(double AreaLote, double AreaUtil, double AreaRetiros, double Frente, double? OcupacionMaxima, double? ConstruccionMaxima, double? ConstruccionPosible)`;
  `sealed record EntradaLote(IReadOnlyList<Punto> Puntos, int Frente, IReadOnlyList<TipoLado>? Tipos, Retiros Retiros, Ficha Ficha)`;
  `sealed record ResultadoLote(Poligono? Lote, IReadOnlyList<TipoLado> Tipos, Poligono? Util, IReadOnlyList<Trazo> Trazos, Resumen? Resumen, IReadOnlyList<Aviso> Avisos)`
  con `bool SePuedeGenerar`; `CalculoLote.Calcular(EntradaLote) → ResultadoLote`.

- [ ] **Paso 1: escribir las pruebas que fallan**

```csharp
namespace CQEING.AreaUtil.Calculo.Tests;

public class CalculoLoteTests
{
    private static EntradaLote Lote7x14(Ficha? ficha = null, Retiros? retiros = null) =>
        new(Poligono.Rectangulo(7, 14).Vertices, 0, null, retiros ?? new Retiros(1.5, 2, 1, 1), ficha ?? new Ficha());

    [Fact]
    public void El_resumen_del_7_por_14()
    {
        ResultadoLote r = CalculoLote.Calcular(Lote7x14());
        Assert.True(r.SePuedeGenerar);
        Assert.Equal(98, r.Resumen!.AreaLote, 6);
        Assert.Equal(52.5, r.Resumen.AreaUtil, 6);
        Assert.Equal(45.5, r.Resumen.AreaRetiros, 6);
        Assert.Equal(7, r.Resumen.Frente, 6);
        Assert.Empty(r.Avisos);
    }

    [Fact]
    public void Un_frente_menor_al_minimo_avisa_sin_bloquear()
    {
        ResultadoLote r = CalculoLote.Calcular(new EntradaLote(Poligono.Rectangulo(6.5, 14).Vertices, 0, null, new Retiros(1.5, 2, 1, 1), new Ficha(FrenteMinimo: 7)));
        Assert.True(r.SePuedeGenerar);
        Assert.Contains(r.Avisos, a => a.Texto.Contains("frente") && !a.Bloquea);
    }

    [Fact]
    public void Un_lote_menor_al_minimo_avisa() =>
        Assert.Contains(CalculoLote.Calcular(Lote7x14(new Ficha(LoteMinimo: 120))).Avisos, a => a.Texto.Contains("lote") && !a.Bloquea);

    [Fact]
    public void Si_el_area_util_supera_el_imo_manda_el_indice()
    {
        ResultadoLote r = CalculoLote.Calcular(Lote7x14(new Ficha(Imo: 0.5)));
        Assert.Equal(49, r.Resumen!.OcupacionMaxima!.Value, 6);
        Assert.Contains(r.Avisos, a => a.Texto.Contains("IMO"));
    }

    [Fact]
    public void Con_los_pisos_se_compara_con_el_imc()
    {
        ResultadoLote r = CalculoLote.Calcular(Lote7x14(new Ficha(Imc: 1, Pisos: 2)));
        Assert.Equal(105, r.Resumen!.ConstruccionPosible!.Value, 6);
        Assert.Contains(r.Avisos, a => a.Texto.Contains("IMC"));
    }

    [Fact]
    public void Sin_ficha_no_hay_avisos_de_ficha() => Assert.Empty(CalculoLote.Calcular(Lote7x14()).Avisos);

    [Fact]
    public void Dos_puntos_no_son_un_lote()
    {
        ResultadoLote r = CalculoLote.Calcular(new EntradaLote([new(0, 0), new(7, 0)], 0, null, new Retiros(1, 1, 1, 1), new Ficha()));
        Assert.False(r.SePuedeGenerar);
        Assert.Contains(r.Avisos, a => a.Bloquea);
    }

    [Fact]
    public void Un_lote_con_lados_cruzados_bloquea()
    {
        ResultadoLote r = CalculoLote.Calcular(new EntradaLote([new(0, 0), new(7, 14), new(7, 0), new(0, 14)], 0, null, new Retiros(1, 1, 1, 1), new Ficha()));
        Assert.False(r.SePuedeGenerar);
        Assert.Contains(r.Avisos, a => a.Bloquea && a.Texto.Contains("cruzan"));
    }

    [Fact]
    public void Los_retiros_que_se_comen_un_lado_bloquean() =>
        Assert.False(CalculoLote.Calcular(Lote7x14(retiros: new Retiros(1.5, 2, 4, 4))).SePuedeGenerar);

    [Fact]
    public void Los_tipos_cambiados_a_mano_mandan_sobre_los_calculados()
    {
        TipoLado[] amano = [TipoLado.Frente, TipoLado.Frente, TipoLado.Posterior, TipoLado.LateralIzquierdo];
        ResultadoLote r = CalculoLote.Calcular(new EntradaLote(Poligono.Rectangulo(7, 14).Vertices, 0, amano, new Retiros(1.5, 2, 1, 1), new Ficha()));
        Assert.Equal(amano, r.Tipos);
        Assert.Equal((7 - 1 - 1.5) * (14 - 1.5 - 2), r.Resumen!.AreaUtil, 6);
    }

    [Fact]
    public void Tipos_de_otro_largo_se_ignoran() =>
        Assert.Equal(TipoLado.Frente, CalculoLote.Calcular(new EntradaLote(Poligono.Rectangulo(7, 14).Vertices, 0, [TipoLado.Posterior], new Retiros(1, 1, 1, 1), new Ficha())).Tipos[0]);
}
```

- [ ] **Paso 2: correrlas y ver que fallan.**

- [ ] **Paso 3: implementar `CalculoLote.cs`**

```csharp
using System.Globalization;

namespace CQEING.AreaUtil.Calculo;

/// <summary>Los datos de la ficha reglamentaria de la curaduría (min 47–49). Lo que no se llena no genera avisos.</summary>
public sealed record Ficha(double? Imo = null, double? Imc = null, int? Pisos = null, double? LoteMinimo = null, double? FrenteMinimo = null);

/// <summary>Un aviso de la columna derecha. Si <paramref name="Bloquea"/>, no se puede generar en Revit.</summary>
public sealed record Aviso(string Texto, bool Bloquea);

/// <summary>Las áreas en m², el largo del frente en m y lo que permite la ficha (null si falta el dato).</summary>
public sealed record Resumen(double AreaLote, double AreaUtil, double AreaRetiros, double Frente,
    double? OcupacionMaxima, double? ConstruccionMaxima, double? ConstruccionPosible);

/// <summary>Lo que llena el usuario. <paramref name="Tipos"/>: los tipos cambiados a mano, o null para deducirlos del frente.</summary>
public sealed record EntradaLote(IReadOnlyList<Punto> Puntos, int Frente, IReadOnlyList<TipoLado>? Tipos, Retiros Retiros, Ficha Ficha);

public sealed record ResultadoLote(Poligono? Lote, IReadOnlyList<TipoLado> Tipos, Poligono? Util, IReadOnlyList<Trazo> Trazos,
    Resumen? Resumen, IReadOnlyList<Aviso> Avisos)
{
    public bool SePuedeGenerar => Lote is not null && Util is not null && !Avisos.Any(a => a.Bloquea);
}

public static class CalculoLote
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-CO");

    public static ResultadoLote Calcular(EntradaLote e)
    {
        var lote = new Poligono(e.Puntos);
        if (lote.Lados < 3 || lote.Area <= 0.01)
            return Bloqueado(null, "El lote necesita al menos 3 puntos que encierren un área.");
        if (lote.SeCruza())
            return Bloqueado(lote, "Los lados del lote se cruzan: revise el orden de los puntos.");

        int frente = ((e.Frente % lote.Lados) + lote.Lados) % lote.Lados;
        IReadOnlyList<TipoLado> tipos = e.Tipos is { } amano && amano.Count == lote.Lados ? amano : LadosLote.Clasificar(lote, frente);
        Desfase d = DesfaseRetiros.Calcular(lote, tipos, e.Retiros);

        double largoFrente = Enumerable.Range(0, lote.Lados).Where(i => tipos[i] == TipoLado.Frente).Sum(lote.LargoLado);
        double areaUtil = d.Util?.Area ?? 0;
        Ficha f = e.Ficha;
        double? ocupacion = f.Imo * lote.Area;
        double? construccion = f.Imc * lote.Area;
        double? posible = f.Pisos is int pisos && d.Util is not null ? areaUtil * pisos : null;
        var resumen = new Resumen(lote.Area, areaUtil, lote.Area - areaUtil, largoFrente, ocupacion, construccion, posible);

        var avisos = new List<Aviso>();
        if (d.Problema is { } problema)
            avisos.Add(new Aviso(problema, true));
        if (f.FrenteMinimo is double fm && largoFrente < fm - Poligono.Tolerancia)
            avisos.Add(new Aviso(Texto($"El frente mide {largoFrente:0.##} m y la ficha pide al menos {fm:0.##} m."), false));
        if (f.LoteMinimo is double lm && lote.Area < lm - 0.01)
            avisos.Add(new Aviso(Texto($"El lote mide {lote.Area:0.##} m² y la ficha pide al menos {lm:0.##} m²."), false));
        if (ocupacion is double om && d.Util is not null && areaUtil > om + 0.01)
            avisos.Add(new Aviso(Texto($"El área útil ({areaUtil:0.##} m²) supera la ocupación que permite el IMO ({om:0.##} m²): manda el índice, no los retiros."), false));
        if (construccion is double cm && posible is double pm && pm > cm + 0.01)
            avisos.Add(new Aviso(Texto($"Con {f.Pisos} pisos se podrían construir {pm:0.##} m², más que los {cm:0.##} m² que permite el IMC."), false));

        return new ResultadoLote(lote, tipos, d.Util, d.Trazos, resumen, avisos);
    }

    private static string Texto(FormattableString texto) => texto.ToString(Es);

    private static ResultadoLote Bloqueado(Poligono? lote, string texto) =>
        new(lote, [], null, [], null, [new Aviso(texto, true)]);
}
```

- [ ] **Paso 4: correr las pruebas y ver que pasan** (todas las del proyecto).

- [ ] **Paso 5: commit** (solo si el usuario lo pide): `feat(area-util): ficha de la curaduría, resumen y avisos (S2)`

---

### Tarea 6 (S2): puntos pegados y protocolo JSON

**Archivos:**
- Crear: `calculo/CQEING.AreaUtil.Calculo/LectorPuntos.cs`, `calculo/CQEING.AreaUtil.Calculo/ProtocoloAreaUtil.cs`
- Prueba: `calculo/CQEING.AreaUtil.Calculo.Tests/LectorPuntosTests.cs`, `ProtocoloAreaUtilTests.cs`

**Interfaces:**
- Consume: `EntradaLote`, `ResultadoLote`, `Retiros`, `Ficha`, `TipoLado`, `Estilos`.
- Produce: `sealed record PuntoLote(double X, double Y, double Z)`; `LectorPuntos.Leer(string) → (List<PuntoLote> Puntos, List<string> Errores)`;
  `sealed record EstiloLinea(string Clave, string Nombre, string Color, int Grosor)` y `EstilosLinea.PorDefecto`;
  `sealed record NivelLeido(string Id, string Nombre, double Cota)`;
  mensajes `ListoAreaUtil`, `CalcularAreaUtil(EntradaLote)`, `LeerPuntosAreaUtil(string Texto)`,
  `GenerarAreaUtil(EntradaLote Entrada, string NivelId, bool Compartidas, IReadOnlyList<EstiloLinea> Estilos)`,
  `CerrarAreaUtil`, `DesconocidoAreaUtil`; `ProtocoloAreaUtil.Leer(string) → MensajeAreaUtil`,
  `Datos(string modelo, IReadOnlyList<NivelLeido> niveles, string? nivelActivo)`, `Resultado(ResultadoLote)`,
  `Puntos(List<PuntoLote>, List<string>)`, `Generado(int)`, `Aviso(string, bool bien = false)`.

- [ ] **Paso 1: escribir las pruebas que fallan**

`LectorPuntosTests.cs`:

```csharp
namespace CQEING.AreaUtil.Calculo.Tests;

public class LectorPuntosTests
{
    [Fact]
    public void Columnas_de_excel_en_espanol_con_tabulador_y_coma_decimal()
    {
        var (puntos, errores) = LectorPuntos.Leer("1200000,50\t900000,25\t10,1\n1200007\t900000\t10");
        Assert.Empty(errores);
        Assert.Equal(new PuntoLote(1200000.50, 900000.25, 10.1), puntos[0]);
        Assert.Equal(new PuntoLote(1200007, 900000, 10), puntos[1]);
    }

    [Fact]
    public void Separado_por_punto_y_coma_o_por_espacios()
    {
        Assert.Equal(new PuntoLote(1.5, 2, 0), LectorPuntos.Leer("1,5;2").Puntos[0]);
        Assert.Equal(new PuntoLote(1.5, 2, 3), LectorPuntos.Leer("1.5   2   3").Puntos[0]);
    }

    [Fact]
    public void Csv_con_punto_decimal_y_coma_como_separador() =>
        Assert.Equal(new PuntoLote(1.5, 2.25, 0), LectorPuntos.Leer("1.5,2.25").Puntos[0]);

    [Fact]
    public void El_encabezado_y_el_rotulo_del_punto_se_ignoran()
    {
        var (puntos, errores) = LectorPuntos.Leer("Punto\tX\tY\tZ\nP1\t10\t20\t3\nP2\t17\t20\t3");
        Assert.Empty(errores);
        Assert.Equal(2, puntos.Count);
        Assert.Equal(new PuntoLote(10, 20, 3), puntos[0]);
    }

    [Fact]
    public void Una_fila_que_no_se_entiende_se_informa_con_su_numero()
    {
        var (puntos, errores) = LectorPuntos.Leer("10\t20\nhola\n17\t20");
        Assert.Equal(2, puntos.Count);
        Assert.Single(errores);
        Assert.Contains("Fila 2", errores[0]);
    }
}
```

`ProtocoloAreaUtilTests.cs`:

```csharp
namespace CQEING.AreaUtil.Calculo.Tests;

public class ProtocoloAreaUtilTests
{
    [Fact]
    public void Calcular_un_rectangulo()
    {
        var m = ProtocoloAreaUtil.Leer("""{"tipo":"calcular","modo":"rectangulo","frente":7,"fondo":14,"ladoFrente":0,"retiros":{"frontal":1.5,"posterior":2,"lateralIzquierdo":1,"lateralDerecho":1},"ficha":{"frenteMinimo":7}}""");
        var c = Assert.IsType<CalcularAreaUtil>(m);
        Assert.Equal(4, c.Entrada.Puntos.Count);
        Assert.Equal(7, c.Entrada.Ficha.FrenteMinimo);
        Assert.Equal(52.5, CalculoLote.Calcular(c.Entrada).Resumen!.AreaUtil, 6);
    }

    [Fact]
    public void Calcular_por_puntos_con_tipos_a_mano()
    {
        var m = ProtocoloAreaUtil.Leer("""{"tipo":"calcular","modo":"puntos","puntos":[{"x":0,"y":0,"z":1},{"x":7,"y":0},{"x":7,"y":14},{"x":0,"y":14}],"ladoFrente":2,"tipos":["Posterior","LateralDerecho","Frente","LateralIzquierdo"],"retiros":{"frontal":1,"posterior":1,"lateralIzquierdo":1,"lateralDerecho":1},"ficha":{}}""");
        var c = Assert.IsType<CalcularAreaUtil>(m);
        Assert.Equal(TipoLado.Frente, c.Entrada.Tipos![2]);
        Assert.Equal(2, c.Entrada.Frente);
    }

    [Fact]
    public void Generar_trae_el_nivel_las_coordenadas_y_los_estilos()
    {
        var m = ProtocoloAreaUtil.Leer("""{"tipo":"generar","modo":"puntos","puntos":[{"x":0,"y":0},{"x":7,"y":0},{"x":7,"y":14}],"retiros":{"frontal":1,"posterior":1,"lateralIzquierdo":1,"lateralDerecho":1},"ficha":{},"nivelId":"311","coordenadas":"compartidas","estilos":[{"clave":"lote","nombre":"CQ Lote","color":"#000000","grosor":5}]}""");
        var g = Assert.IsType<GenerarAreaUtil>(m);
        Assert.Equal("311", g.NivelId);
        Assert.True(g.Compartidas);
        Assert.Equal("CQ Lote", g.Estilos[0].Nombre);
    }

    [Fact]
    public void Frente_por_fondo_siempre_va_en_coordenadas_del_proyecto() =>
        Assert.False(Assert.IsType<GenerarAreaUtil>(ProtocoloAreaUtil.Leer("""{"tipo":"generar","modo":"rectangulo","frente":7,"fondo":14,"retiros":{"frontal":1,"posterior":1,"lateralIzquierdo":1,"lateralDerecho":1},"nivelId":"1","coordenadas":"compartidas"}""")).Compartidas);

    [Fact]
    public void Sin_estilos_usa_los_de_siempre() =>
        Assert.Equal(EstilosLinea.PorDefecto, Assert.IsType<GenerarAreaUtil>(ProtocoloAreaUtil.Leer("""{"tipo":"generar","modo":"rectangulo","frente":7,"fondo":14,"retiros":{"frontal":1,"posterior":1,"lateralIzquierdo":1,"lateralDerecho":1},"nivelId":"1"}""")).Estilos);

    [Fact]
    public void Un_json_roto_es_desconocido() => Assert.IsType<DesconocidoAreaUtil>(ProtocoloAreaUtil.Leer("{no es json"));

    [Fact]
    public void El_resultado_lleva_areas_trazos_y_avisos()
    {
        string json = ProtocoloAreaUtil.Resultado(CalculoLote.Calcular(new EntradaLote(Poligono.Rectangulo(7, 14).Vertices, 0, null, new Retiros(1.5, 2, 1, 1), new Ficha())));
        Assert.Contains("\"tipo\":\"resultado\"", json);
        Assert.Contains("\"areaUtil\":52.5", json);
        Assert.Contains("\"estilo\":\"area-util\"", json);
        Assert.Contains("\"sePuedeGenerar\":true", json);
    }
}
```

- [ ] **Paso 2: correrlas y ver que fallan.**

- [ ] **Paso 3: implementar `LectorPuntos.cs`**

```csharp
using System.Globalization;

namespace CQEING.AreaUtil.Calculo;

/// <summary>Un punto de la topografía, en metros. La Z se guarda y se muestra; las líneas se dibujan en el nivel.</summary>
public sealed record PuntoLote(double X, double Y, double Z);

/// <summary>
/// Lee los puntos pegados desde Excel o AutoCAD: columnas X, Y y Z opcional (decisión 14 del plan). Acepta tabulador,
/// punto y coma, coma o espacios entre columnas, y coma o punto decimal.
/// </summary>
public static class LectorPuntos
{
    public static (List<PuntoLote> Puntos, List<string> Errores) Leer(string texto)
    {
        var puntos = new List<PuntoLote>();
        var errores = new List<string>();
        string[] filas = texto.Replace("\r", "").Split('\n');
        for (int i = 0; i < filas.Length; i++)
        {
            string fila = filas[i].Trim();
            if (fila.Length == 0)
                continue;
            List<string> campos = Campos(fila);
            // El rótulo del punto («P1») va adelante cuando hay 4 columnas o más.
            if (campos.Count >= 4 && !EsNumero(campos[0]))
                campos.RemoveAt(0);
            List<double> numeros = campos.Where(EsNumero).Select(Numero).ToList();
            if (numeros.Count == campos.Count && numeros.Count is 2 or 3)
                puntos.Add(new PuntoLote(numeros[0], numeros[1], numeros.Count == 3 ? numeros[2] : 0));
            else if (!(puntos.Count == 0 && errores.Count == 0 && numeros.Count == 0))
                errores.Add($"Fila {i + 1}: no se entiende «{fila}». Se esperan X, Y y Z opcional.");
        }
        return (puntos, errores);
    }

    private static List<string> Campos(string fila)
    {
        string[] partes = fila.Contains('\t') ? fila.Split('\t')
            : fila.Contains(';') ? fila.Split(';')
            : fila.Contains(',') && fila.Contains('.') ? fila.Split(',')
            : fila.Contains(',') && !fila.Contains(' ') ? fila.Split(',')
            : fila.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return partes.Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
    }

    private static bool EsNumero(string campo) => double.TryParse(campo.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    private static double Numero(string campo) => double.Parse(campo.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);
}
```

Con «1,5;2», la fila tiene `;` y se separa por ahí, así que «1,5» queda como 1.5. La prueba
`Separado_por_punto_y_coma_o_por_espacios` lo comprueba.

- [ ] **Paso 4: implementar `ProtocoloAreaUtil.cs`**

```csharp
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CQEING.AreaUtil.Calculo;

/// <summary>Un estilo de línea de Revit: «CQ …», con color hexadecimal y grosor de 1 a 16.</summary>
public sealed record EstiloLinea(string Clave, string Nombre, string Color, int Grosor);

public static class EstilosLinea
{
    /// <summary>Los colores del dibujo de Carlos (min 7–10): lote negro, retiros naranjas, área útil roja.</summary>
    public static IReadOnlyList<EstiloLinea> PorDefecto { get; } =
    [
        new(Estilos.Lote, "CQ Lote", "#1F2933", 5),
        new(Estilos.RetiroFrontal, "CQ Retiro frontal", "#E67E22", 3),
        new(Estilos.RetiroLateral, "CQ Retiro lateral", "#F2A900", 3),
        new(Estilos.RetiroPosterior, "CQ Retiro posterior", "#B9770E", 3),
        new(Estilos.AreaUtil, "CQ Área útil", "#D0021B", 5),
    ];
}

/// <summary>Un nivel del modelo para la lista de la izquierda; la cota va en metros.</summary>
public sealed record NivelLeido(string Id, string Nombre, double Cota);

public abstract record MensajeAreaUtil;

public sealed record ListoAreaUtil : MensajeAreaUtil;

public sealed record CalcularAreaUtil(EntradaLote Entrada) : MensajeAreaUtil;

public sealed record LeerPuntosAreaUtil(string Texto) : MensajeAreaUtil;

/// <summary><paramref name="Compartidas"/>: los puntos vienen en coordenadas compartidas (decisión 8 del spec).</summary>
public sealed record GenerarAreaUtil(EntradaLote Entrada, string NivelId, bool Compartidas, IReadOnlyList<EstiloLinea> Estilos) : MensajeAreaUtil;

public sealed record CerrarAreaUtil : MensajeAreaUtil;

public sealed record DesconocidoAreaUtil : MensajeAreaUtil;

/// <summary>Los mensajes entre la página del tablero y C#, como en zapatas.</summary>
public static class ProtocoloAreaUtil
{
    public static JsonSerializerOptions Json { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Nunca lanza: un JSON roto da <see cref="DesconocidoAreaUtil"/>.</summary>
    public static MensajeAreaUtil Leer(string json)
    {
        Entrada? e;
        try
        {
            e = JsonSerializer.Deserialize<Entrada>(json, Json);
        }
        catch (JsonException)
        {
            return new DesconocidoAreaUtil();
        }
        if (e is null)
            return new DesconocidoAreaUtil();
        return e.Tipo switch
        {
            "listo" => new ListoAreaUtil(),
            "calcular" => new CalcularAreaUtil(Lote(e)),
            "leerPuntos" => new LeerPuntosAreaUtil(e.Texto ?? ""),
            "generar" when !string.IsNullOrEmpty(e.NivelId) => new GenerarAreaUtil(Lote(e), e.NivelId,
                e.Modo == "puntos" && e.Coordenadas == "compartidas", e.Estilos is { Count: > 0 } s ? s : EstilosLinea.PorDefecto),
            "cerrar" => new CerrarAreaUtil(),
            _ => new DesconocidoAreaUtil(),
        };
    }

    private static EntradaLote Lote(Entrada e)
    {
        IReadOnlyList<Punto> puntos = e.Modo == "puntos"
            ? (e.Puntos ?? []).Select(p => new Punto(p.X, p.Y)).ToList()
            : Poligono.Rectangulo(e.Frente ?? 0, e.Fondo ?? 0).Vertices;
        List<TipoLado>? tipos = e.Tipos?.Select(t => Enum.TryParse(t, out TipoLado tipo) ? tipo : (TipoLado?)null).ToList() is { } leidos
            && leidos.All(t => t is not null) ? leidos.Select(t => t!.Value).ToList() : null;
        RetirosJson r = e.Retiros ?? new RetirosJson();
        FichaJson f = e.Ficha ?? new FichaJson();
        return new EntradaLote(puntos, e.LadoFrente ?? 0, tipos, new Retiros(r.Frontal, r.Posterior, r.LateralIzquierdo, r.LateralDerecho),
            new Ficha(f.Imo, f.Imc, f.Pisos, f.LoteMinimo, f.FrenteMinimo));
    }

    public static string Datos(string modelo, IReadOnlyList<NivelLeido> niveles, string? nivelActivo) =>
        Escribir(new { tipo = "datos", modelo, niveles, nivelActivo, estilos = EstilosLinea.PorDefecto });

    public static string Resultado(ResultadoLote r) => Escribir(new
    {
        tipo = "resultado",
        lote = r.Lote?.Vertices.Select(p => new { x = p.X, y = p.Y }),
        util = r.Util?.Vertices.Select(p => new { x = p.X, y = p.Y }),
        tipos = r.Tipos,
        trazos = r.Trazos.Select(t => new { ax = t.A.X, ay = t.A.Y, bx = t.B.X, by = t.B.Y, estilo = t.Estilo }),
        resumen = r.Resumen,
        avisos = r.Avisos,
        sePuedeGenerar = r.SePuedeGenerar,
    });

    public static string Puntos(List<PuntoLote> puntos, List<string> errores) => Escribir(new { tipo = "puntos", puntos, errores });

    public static string Generado(int lineas) => Escribir(new { tipo = "generado", lineas });

    public static string Aviso(string texto, bool bien = false) => Escribir(new { tipo = "aviso", texto, bien });

    private static string Escribir<T>(T valor) => JsonSerializer.Serialize(valor, Json);

    private sealed class Entrada
    {
        public string? Tipo { get; set; }
        public string? Modo { get; set; }
        public double? Frente { get; set; }
        public double? Fondo { get; set; }
        public List<PuntoJson>? Puntos { get; set; }
        public int? LadoFrente { get; set; }
        public List<string>? Tipos { get; set; }
        public RetirosJson? Retiros { get; set; }
        public FichaJson? Ficha { get; set; }
        public string? NivelId { get; set; }
        public string? Coordenadas { get; set; }
        public List<EstiloLinea>? Estilos { get; set; }
        public string? Texto { get; set; }
    }

    private sealed class PuntoJson
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    private sealed class RetirosJson
    {
        public double Frontal { get; set; }
        public double Posterior { get; set; }
        public double LateralIzquierdo { get; set; }
        public double LateralDerecho { get; set; }
    }

    private sealed class FichaJson
    {
        public double? Imo { get; set; }
        public double? Imc { get; set; }
        public int? Pisos { get; set; }
        public double? LoteMinimo { get; set; }
        public double? FrenteMinimo { get; set; }
    }
}
```

`Sin_estilos_usa_los_de_siempre` compara listas con `Assert.Equal`: como `PorDefecto` es la misma instancia, pasa.

- [ ] **Paso 5: correr todas las pruebas del cálculo y ver que pasan.**

- [ ] **Paso 6: commit** (solo si el usuario lo pide): `feat(area-util): puntos pegados y protocolo con la página (S2)`

---

### Tarea 7 (S3): medir en Revit antes de escribir `Revit/`

**Archivos:**
- Modificar: este plan (Hallazgos 5 en adelante, con fecha y valores).

Se corre con el MCP (`send_code_to_revit`) sobre una **copia** de `Prueba 080720sfsd26.0028`, nunca sobre el original.
La plantilla del MCP abre y confirma una transacción: cada prueba trabaja dentro de una `SubTransaction` que se deshace
con `RollBack()`, así el modelo queda como estaba.

- [ ] **Paso 1: estilos de línea**

```csharp
var sub = new SubTransaction(document); sub.Start();
Category lineas = document.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
Category nueva = document.Settings.Categories.NewSubcategory(lineas, "CQ Prueba");
nueva.LineColor = new Color(208, 2, 27);
nueva.SetLineWeight(5, GraphicsStyleType.Projection);
GraphicsStyle gs = nueva.GetGraphicsStyle(GraphicsStyleType.Projection);
string r = $"existe={lineas.SubCategories.Contains("CQ Prueba")} gs={gs?.Id} peso={nueva.GetLineWeight(GraphicsStyleType.Projection)}";
sub.RollBack();
return r;
```

Esperado: `existe=True`, un id de `GraphicsStyle` y `peso=5`. Se anota.

- [ ] **Paso 2: línea de modelo en el plano de un nivel**

```csharp
var sub = new SubTransaction(document); sub.Start();
Level nivel = new FilteredElementCollector(document).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).First();
SketchPlane plano = SketchPlane.Create(document, nivel.Id);
double z = plano.GetPlane().Origin.Z;
var linea = Line.CreateBound(new XYZ(0, 0, z), new XYZ(UnitUtils.ConvertToInternalUnits(7, UnitTypeId.Meters), 0, z));
ModelCurve mc = document.Create.NewModelCurve(linea, plano);
var lineas = document.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
var sub2 = document.Settings.Categories.NewSubcategory(lineas, "CQ Prueba");
mc.LineStyle = sub2.GetGraphicsStyle(GraphicsStyleType.Projection);
string r = $"nivel={nivel.Name} elev={nivel.Elevation} projElev={nivel.ProjectElevation} z={z} estilo={mc.LineStyle.Name}";
// Dos líneas encimadas con estilos distintos: ¿Revit avisa? (decisión 10 y retiro 0)
ModelCurve encima = document.Create.NewModelCurve(linea, plano);
document.Regenerate();
r += $" avisos={document.GetWarnings().Count}";
sub.RollBack();
return r;
```

Esperado: la Z del plano coincide con la cota del nivel. Se anota si `GetWarnings` aumenta con la línea encimada; si
aumenta, se cambia la decisión 10 para no dibujar el lado del área útil cuando su retiro es 0.

- [ ] **Paso 3: coordenadas compartidas**

```csharp
Transform t = document.ActiveProjectLocation.GetTotalTransform();
XYZ origenInternoEnCompartidas = t.OfPoint(XYZ.Zero);
XYZ compartidoDeVuelta = t.Inverse.OfPoint(origenInternoEnCompartidas);
return $"origen interno en compartidas (m): {UnitUtils.ConvertFromInternalUnits(origenInternoEnCompartidas.X, UnitTypeId.Meters)}, {UnitUtils.ConvertFromInternalUnits(origenInternoEnCompartidas.Y, UnitTypeId.Meters)} | vuelta={compartidoDeVuelta}";
```

Esperado: `GetTotalTransform` lleva de internas a compartidas (como dice su documentación) y `Inverse` vuelve a
`(0,0,0)`. Se anota el valor del modelo de prueba y se confirma la decisión 8.

- [ ] **Paso 4: anotar los Hallazgos 5, 6 y 7 en este plan**, con los valores medidos. Si algo no coincide con lo
  esperado, primero se anota la decisión nueva y después se adapta la tarea 9.

---

### Tarea 8 (S4): tablero de tres columnas

**Archivos:**
- Modificar: `addins/area-util/Tablero/PuenteTablero.cs` (versión completa, menos «generar», que llega en la tarea 9)
- Modificar: `addins/area-util/Tablero/web/index.html`
- Crear: `addins/area-util/Tablero/web/area-util.js`

**Interfaces:**
- Consume: `ProtocoloAreaUtil`, `CalculoLote`, `LectorPuntos`, `NivelLeido` (tarea 6); `VentanaTablero`, `AccionesRevit` (tarea 1).
- Produce: la página manda `listo`, `calcular`, `leerPuntos`, `generar` y `cerrar`, y recibe `datos`, `resultado`,
  `puntos`, `aviso` y `generado`.

- [ ] **Paso 1: `PuenteTablero.cs` completo (sin generar)**

```csharp
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using CQEING.AreaUtil.Calculo;

namespace CQEING.AreaUtil.Tablero;

/// <summary>
/// Une la página con Revit. Calcular y leer puntos no usan la API y se contestan enseguida; leer niveles y generar van al
/// hilo de Revit por <see cref="AccionesRevit"/>. Sigue al modelo activo como zapatas.
/// </summary>
internal sealed class PuenteTablero
{
    private readonly VentanaTablero ventana;
    private readonly UIApplication uiapp;
    private readonly AccionesRevit acciones = new();
    private Document? doc;

    public PuenteTablero(VentanaTablero ventana, UIApplication uiapp)
    {
        this.ventana = ventana;
        this.uiapp = uiapp;
        doc = uiapp.ActiveUIDocument?.Document;
        ventana.MensajeRecibido += Recibir;
        acciones.Fallo += texto => ventana.Enviar(ProtocoloAreaUtil.Aviso($"No se pudo hacer en Revit: {texto}"));
        uiapp.ViewActivated += AlActivarVista;
        uiapp.Application.DocumentClosing += AlCerrarDocumento;
        ventana.Closed += (_, _) => Soltar();
    }

    private void Recibir(string json)
    {
        MensajeAreaUtil mensaje = ProtocoloAreaUtil.Leer(json);
        switch (mensaje)
        {
            case CalcularAreaUtil c:
                ventana.Enviar(ProtocoloAreaUtil.Resultado(CalculoLote.Calcular(c.Entrada)));
                break;
            case LeerPuntosAreaUtil l:
                var (puntos, errores) = LectorPuntos.Leer(l.Texto);
                ventana.Enviar(ProtocoloAreaUtil.Puntos(puntos, errores));
                break;
            case CerrarAreaUtil:
                ventana.Close();
                break;
            case DesconocidoAreaUtil:
                break;
            default:
                acciones.Encolar(app => Atender(app, mensaje));
                break;
        }
    }

    private void Atender(UIApplication app, MensajeAreaUtil mensaje)
    {
        if (mensaje is ListoAreaUtil)
        {
            Recargar(app);
            return;
        }
        // Se genera solo sobre el modelo leído y si sigue siendo el activo.
        if (doc is null || !doc.IsValidObject || app.ActiveUIDocument?.Document is not Document activo || !activo.Equals(doc))
        {
            Recargar(app);
            return;
        }
        if (mensaje is GenerarAreaUtil g)
            Generar(doc, g);
    }

    private void Recargar(UIApplication app)
    {
        doc = app.ActiveUIDocument?.Document;
        if (doc is null || doc.IsFamilyDocument)
        {
            doc = null;
            ventana.Enviar(ProtocoloAreaUtil.Datos("", [], null));
            ventana.Enviar(ProtocoloAreaUtil.Aviso("No hay un modelo de proyecto abierto. Abra uno para dibujar el lote."));
            return;
        }
        List<Level> niveles = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();
        string? activo = (doc.ActiveView?.GenLevel ?? niveles.FirstOrDefault())?.Id.ToString();
        ventana.Enviar(ProtocoloAreaUtil.Datos(doc.Title,
            niveles.Select(l => new NivelLeido(l.Id.ToString(), l.Name, UnitUtils.ConvertFromInternalUnits(l.Elevation, UnitTypeId.Meters))).ToList(),
            activo));
    }

    /// <summary>La tarea 9 lo completa.</summary>
    private void Generar(Document doc, GenerarAreaUtil g) =>
        ventana.Enviar(ProtocoloAreaUtil.Aviso("Generar en Revit llega en la tarea 9 del plan."));

    private void AlActivarVista(object? sender, ViewActivatedEventArgs e)
    {
        try
        {
            if (!e.Document.Equals(doc))
                acciones.Encolar(Recargar);
        }
        catch
        {
            // Revit llama este evento desde su lado nativo: un error aquí podría tumbarlo.
        }
    }

    private void AlCerrarDocumento(object? sender, DocumentClosingEventArgs e)
    {
        try
        {
            if (e.Document.Equals(doc))
            {
                doc = null;
                acciones.Encolar(Recargar);
            }
        }
        catch
        {
            // Igual que arriba.
        }
    }

    private void Soltar()
    {
        uiapp.ViewActivated -= AlActivarVista;
        uiapp.Application.DocumentClosing -= AlCerrarDocumento;
        acciones.Dispose();
    }
}
```

- [ ] **Paso 2: `web/index.html`**

```html
<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<title>Área útil del lote</title>
<!-- Tablero de tres columnas (decisión 4 del spec 2026-11-10-area-util-lote): datos, vista previa, resumen. -->
<style>
:root{--ink:#1F2933;--ink3:#5B6672;--line:#D0D5DB;--line2:#E4E7EB;--campo:#B9C0C8;--azul:#1F5FAD;--bien:#1E8E4E;--mal:#B42318;--amarillo:#8A5A00;--fondoAm:#FFF6DD}
*{box-sizing:border-box}
html,body{height:100%;margin:0}
body{background:#DDE1E6;font:13px/1.35 'Segoe UI',system-ui,sans-serif;color:var(--ink);display:flex;flex-direction:column;overflow:hidden}
button,select,input,textarea{font:inherit;color:inherit}
button{cursor:pointer}
button:disabled{cursor:not-allowed;opacity:.55}
h2{margin:0;font-size:13px;font-weight:600}
h3{margin:12px 0 6px;font-size:11px;font-weight:700;letter-spacing:.06em;text-transform:uppercase;color:var(--ink3)}
.barra{display:flex;align-items:center;gap:8px;padding:10px 12px;background:#fff;border-bottom:1px solid var(--line)}
.barra .modelo{color:var(--ink3);margin-right:auto}
.btn{height:32px;padding:0 12px;border:1px solid var(--campo);border-radius:6px;background:#fff;font-weight:500}
.btn.azul{background:var(--azul);border-color:var(--azul);color:#fff;font-weight:600}
#cuerpo{flex:1;min-height:0;display:grid;grid-template-columns:320px minmax(420px,1fr) 340px;gap:12px;padding:12px}
.panel{background:#fff;border:1px solid var(--line);border-radius:6px;display:flex;flex-direction:column;min-height:0}
.titulo{padding:9px 12px;border-bottom:1px solid var(--line2)}
.contenido{padding:4px 12px 12px;overflow:auto;flex:1}
.fila{display:grid;grid-template-columns:1fr 110px;align-items:center;gap:8px;margin:4px 0}
.fila label{color:var(--ink3)}
input[type=number],select{height:30px;padding:0 6px;border:1px solid var(--campo);border-radius:6px;width:100%}
.pestanas{display:flex;gap:4px;margin:8px 0}
.pestanas button{flex:1;height:30px;border:1px solid var(--campo);background:#fff;border-radius:6px}
.pestanas button.activa{background:var(--azul);border-color:var(--azul);color:#fff}
table{width:100%;border-collapse:collapse}
td,th{padding:2px 3px;text-align:left}
td input{width:100%;height:26px;border:1px solid var(--campo);border-radius:4px;padding:0 4px}
textarea{width:100%;height:70px;border:1px solid var(--campo);border-radius:6px;padding:6px;resize:vertical}
#vista{flex:1;min-height:0;background:#FAFBFC}
#vista svg{width:100%;height:100%;display:block}
.lado{cursor:pointer}
.lado:hover{stroke:#1F5FAD}
.leyenda{display:flex;flex-wrap:wrap;gap:10px;padding:8px 12px;border-top:1px solid var(--line2);color:var(--ink3)}
.leyenda span::before{content:"";display:inline-block;width:14px;height:3px;margin-right:4px;vertical-align:middle;background:var(--c)}
.dato{display:flex;justify-content:space-between;padding:4px 0;border-bottom:1px solid var(--line2)}
.dato b{font-variant-numeric:tabular-nums}
.aviso{margin:6px 0;padding:8px;border-radius:6px;background:var(--fondoAm);color:var(--amarillo)}
.aviso.bloquea{background:#FDECEA;color:var(--mal)}
.aviso.bien{background:#E7F5EC;color:var(--bien)}
.colores .fila{grid-template-columns:1fr 46px 56px}
</style>
</head>
<body>
<div class="barra">
  <h2>Área útil del lote</h2><span class="modelo" id="modelo"></span>
  <button class="btn" id="cerrar">Cerrar</button>
</div>
<div id="cuerpo">
  <section class="panel">
    <div class="titulo"><h2>Datos</h2></div>
    <div class="contenido">
      <h3>Lote</h3>
      <div class="pestanas"><button id="modoRect" class="activa">Frente × fondo</button><button id="modoPuntos">Puntos</button></div>
      <div id="datosRect">
        <div class="fila"><label for="frente">Frente (m)</label><input id="frente" type="number" step="0.01" value="7"></div>
        <div class="fila"><label for="fondo">Fondo (m)</label><input id="fondo" type="number" step="0.01" value="14"></div>
      </div>
      <div id="datosPuntos" hidden>
        <table><thead><tr><th>#</th><th>X (m)</th><th>Y (m)</th><th>Z (m)</th><th></th></tr></thead><tbody id="tablaPuntos"></tbody></table>
        <button class="btn" id="agregarPunto">+ Punto</button>
        <h3>Pegar desde Excel o AutoCAD</h3>
        <textarea id="pegar" placeholder="X   Y   Z (una fila por punto)"></textarea>
        <div class="fila"><label for="coordenadas">Coordenadas</label>
          <select id="coordenadas"><option value="compartidas">Compartidas (topografía)</option><option value="proyecto">Del proyecto</option></select></div>
      </div>
      <h3>Retiros (m)</h3>
      <div class="fila"><label for="rFrontal">Frontal</label><input id="rFrontal" type="number" step="0.01" value="1.5"></div>
      <div class="fila"><label for="rPosterior">Posterior</label><input id="rPosterior" type="number" step="0.01" value="2"></div>
      <div class="fila"><label for="rIzquierdo">Lateral izquierdo</label><input id="rIzquierdo" type="number" step="0.01" value="1"></div>
      <div class="fila"><label for="rDerecho">Lateral derecho</label><input id="rDerecho" type="number" step="0.01" value="1"></div>
      <h3>Ficha de la curaduría</h3>
      <div class="fila"><label for="imo">IMO</label><input id="imo" type="number" step="0.01"></div>
      <div class="fila"><label for="imc">IMC</label><input id="imc" type="number" step="0.01"></div>
      <div class="fila"><label for="pisos">Pisos</label><input id="pisos" type="number" step="1"></div>
      <div class="fila"><label for="loteMinimo">Lote mínimo (m²)</label><input id="loteMinimo" type="number" step="0.01"></div>
      <div class="fila"><label for="frenteMinimo">Frente mínimo (m)</label><input id="frenteMinimo" type="number" step="0.01"></div>
      <h3>Lados del lote</h3>
      <div id="lados"></div>
      <h3>Nivel</h3>
      <select id="nivel"></select>
    </div>
  </section>
  <section class="panel">
    <div class="titulo"><h2>Vista previa</h2></div>
    <div id="vista"></div>
    <div class="leyenda" id="leyenda"></div>
  </section>
  <section class="panel">
    <div class="titulo"><h2>Resumen y recomendaciones</h2></div>
    <div class="contenido">
      <div id="resumen"></div>
      <div id="avisos"></div>
      <h3>Colores en Revit</h3>
      <div class="colores" id="colores"></div>
      <button class="btn azul" id="generar" style="width:100%;margin-top:12px" disabled>Generar en Revit</button>
    </div>
  </section>
</div>
<script src="area-util.js"></script>
</body>
</html>
```

- [ ] **Paso 3: `web/area-util.js`**

```js
'use strict';
// Tablero del área útil: la página solo junta datos y dibuja; la cuenta la hace C# (calculo/), con pruebas.

const $ = id => document.getElementById(id);
const NOMBRES = { Frente: 'Frente', Posterior: 'Posterior', LateralIzquierdo: 'Lateral izquierdo', LateralDerecho: 'Lateral derecho' };
const estado = {
  modo: 'rectangulo',
  puntos: [{ x: 0, y: 0, z: 0 }, { x: 7, y: 0, z: 0 }, { x: 7, y: 14, z: 0 }, { x: 0, y: 14, z: 0 }],
  ladoFrente: 0,
  tipos: null,
  estilos: [],
  resultado: null,
};

function enviar(mensaje) {
  if (window.chrome?.webview) window.chrome.webview.postMessage(mensaje);
}

/** Acepta coma o punto decimal; vacío es null. */
function numero(id) {
  const t = $(id).value.trim().replace(',', '.');
  return t === '' ? null : Number(t);
}

function entrada() {
  return {
    modo: estado.modo,
    frente: numero('frente') ?? 0,
    fondo: numero('fondo') ?? 0,
    puntos: estado.puntos,
    ladoFrente: estado.ladoFrente,
    tipos: estado.tipos,
    retiros: {
      frontal: numero('rFrontal') ?? 0,
      posterior: numero('rPosterior') ?? 0,
      lateralIzquierdo: numero('rIzquierdo') ?? 0,
      lateralDerecho: numero('rDerecho') ?? 0,
    },
    ficha: {
      imo: numero('imo'), imc: numero('imc'), pisos: numero('pisos'),
      loteMinimo: numero('loteMinimo'), frenteMinimo: numero('frenteMinimo'),
    },
    nivelId: $('nivel').value,
    coordenadas: $('coordenadas').value,
    estilos: estado.estilos,
  };
}

let espera;
function calcular() {
  clearTimeout(espera);
  espera = setTimeout(() => enviar({ tipo: 'calcular', ...entrada() }), 150);
}

// ---------- datos de la izquierda

function cambiarModo(modo) {
  estado.modo = modo;
  estado.ladoFrente = 0;
  estado.tipos = null;
  $('modoRect').classList.toggle('activa', modo === 'rectangulo');
  $('modoPuntos').classList.toggle('activa', modo === 'puntos');
  $('datosRect').hidden = modo !== 'rectangulo';
  $('datosPuntos').hidden = modo !== 'puntos';
  calcular();
}

function pintarPuntos() {
  $('tablaPuntos').innerHTML = estado.puntos.map((p, i) => `<tr>
    <td>${i + 1}</td>
    <td><input data-i="${i}" data-c="x" value="${p.x}"></td>
    <td><input data-i="${i}" data-c="y" value="${p.y}"></td>
    <td><input data-i="${i}" data-c="z" value="${p.z}"></td>
    <td><button class="btn" data-quitar="${i}" title="Quitar el punto">−</button></td></tr>`).join('');
}

$('tablaPuntos').addEventListener('input', e => {
  const { i, c } = e.target.dataset;
  if (i === undefined) return;
  estado.puntos[+i][c] = Number(e.target.value.replace(',', '.')) || 0;
  estado.tipos = null;
  calcular();
});
$('tablaPuntos').addEventListener('click', e => {
  const i = e.target.dataset.quitar;
  if (i === undefined) return;
  estado.puntos.splice(+i, 1);
  estado.tipos = null;
  pintarPuntos();
  calcular();
});
$('agregarPunto').addEventListener('click', () => {
  const ultimo = estado.puntos.at(-1) ?? { x: 0, y: 0, z: 0 };
  estado.puntos.push({ ...ultimo });
  pintarPuntos();
});
$('pegar').addEventListener('paste', e => {
  e.preventDefault();
  enviar({ tipo: 'leerPuntos', texto: e.clipboardData.getData('text') });
});

function pintarLados() {
  const r = estado.resultado;
  if (!r?.tipos?.length) {
    $('lados').innerHTML = '<div class="aviso">Clic en un lado de la vista previa para marcar el frente.</div>';
    return;
  }
  $('lados').innerHTML = r.tipos.map((t, i) => `<div class="fila"><label>Lado ${i + 1} (${largo(r.lote, i).toFixed(2)} m)</label>
    <select data-lado="${i}">${Object.entries(NOMBRES).map(([k, v]) => `<option value="${k}" ${k === t ? 'selected' : ''}>${v}</option>`).join('')}</select></div>`).join('');
}

$('lados').addEventListener('change', e => {
  const i = e.target.dataset.lado;
  if (i === undefined) return;
  estado.tipos = [...estado.resultado.tipos];
  estado.tipos[+i] = e.target.value;
  calcular();
});

const largo = (lote, i) => {
  const a = lote[i], b = lote[(i + 1) % lote.length];
  return Math.hypot(b.x - a.x, b.y - a.y);
};

// ---------- vista previa

const color = clave => estado.estilos.find(s => s.clave === clave)?.color ?? '#888';

function dibujar() {
  const r = estado.resultado;
  if (!r?.lote?.length) {
    $('vista').innerHTML = '';
    return;
  }
  const xs = r.lote.map(p => p.x), ys = r.lote.map(p => p.y);
  const minX = Math.min(...xs), maxX = Math.max(...xs), minY = Math.min(...ys), maxY = Math.max(...ys);
  const margen = Math.max(maxX - minX, maxY - minY) * 0.12 + 1;
  const ancho = maxX - minX + 2 * margen, alto = maxY - minY + 2 * margen;
  const X = x => x - minX + margen, Y = y => maxY - y + margen; // Y hacia arriba, como en planta
  const grueso = Math.max(ancho, alto) / 250;
  let svg = `<svg viewBox="0 0 ${ancho} ${alto}" preserveAspectRatio="xMidYMid meet">`;
  if (r.util) svg += `<polygon points="${r.util.map(p => `${X(p.x)},${Y(p.y)}`).join(' ')}" fill="${color('area-util')}" fill-opacity=".14"/>`;
  for (const t of r.trazos)
    svg += `<line x1="${X(t.ax)}" y1="${Y(t.ay)}" x2="${X(t.bx)}" y2="${Y(t.by)}" stroke="${color(t.estilo)}" stroke-width="${grueso * (t.estilo === 'lote' || t.estilo === 'area-util' ? 2 : 1.4)}"/>`;
  r.lote.forEach((a, i) => {
    const b = r.lote[(i + 1) % r.lote.length];
    const frente = r.tipos?.[i] === 'Frente';
    svg += `<line class="lado" data-lado="${i}" x1="${X(a.x)}" y1="${Y(a.y)}" x2="${X(b.x)}" y2="${Y(b.y)}" stroke="${frente ? '#1F5FAD' : 'transparent'}" stroke-width="${grueso * 6}" stroke-opacity="${frente ? .35 : 1}"><title>Lado ${i + 1}: clic para marcarlo como frente</title></line>`;
    svg += `<text x="${(X(a.x) + X(b.x)) / 2}" y="${(Y(a.y) + Y(b.y)) / 2}" font-size="${grueso * 7}" fill="#5B6672" text-anchor="middle">${largo(r.lote, i).toFixed(2)}</text>`;
  });
  svg += '</svg>';
  $('vista').innerHTML = svg;
}

$('vista').addEventListener('click', e => {
  const i = e.target.dataset?.lado;
  if (i === undefined) return;
  estado.ladoFrente = +i;
  estado.tipos = null;
  calcular();
});

// ---------- resumen y colores

const m2 = v => v == null ? '—' : `${v.toLocaleString('es-CO', { maximumFractionDigits: 2 })} m²`;

function pintarResumen() {
  const r = estado.resultado;
  const s = r?.resumen;
  $('resumen').innerHTML = s ? `
    <div class="dato"><span>Área del lote</span><b>${m2(s.areaLote)}</b></div>
    <div class="dato"><span>Área de retiros</span><b>${m2(s.areaRetiros)}</b></div>
    <div class="dato"><span>Área útil (1.er piso)</span><b>${m2(s.areaUtil)}</b></div>
    <div class="dato"><span>Frente</span><b>${s.frente.toLocaleString('es-CO', { maximumFractionDigits: 2 })} m</b></div>
    <div class="dato"><span>Ocupación máxima (IMO)</span><b>${m2(s.ocupacionMaxima)}</b></div>
    <div class="dato"><span>Construcción máxima (IMC)</span><b>${m2(s.construccionMaxima)}</b></div>
    <div class="dato"><span>Construcción con los pisos</span><b>${m2(s.construccionPosible)}</b></div>` : '';
  $('avisos').innerHTML = (r?.avisos ?? []).map(a => `<div class="aviso ${a.bloquea ? 'bloquea' : ''}">${a.texto}</div>`).join('')
    || (r ? '<div class="aviso bien">Cumple con los datos de la ficha que se llenaron.</div>' : '');
  $('generar').disabled = !r?.sePuedeGenerar || !$('nivel').value;
}

function pintarColores() {
  $('colores').innerHTML = estado.estilos.map((s, i) => `<div class="fila"><label>${s.nombre}</label>
    <input type="color" data-i="${i}" value="${s.color}"><input type="number" min="1" max="16" data-g="${i}" value="${s.grosor}"></div>`).join('');
  $('leyenda').innerHTML = estado.estilos.map(s => `<span style="--c:${s.color}">${s.nombre}</span>`).join('');
}

$('colores').addEventListener('input', e => {
  const { i, g } = e.target.dataset;
  if (i !== undefined) estado.estilos[+i].color = e.target.value;
  if (g !== undefined) estado.estilos[+g].grosor = Math.min(16, Math.max(1, +e.target.value || 1));
  pintarColores();
  dibujar();
});

// ---------- mensajes de C#

window.chrome?.webview?.addEventListener('message', e => {
  const d = e.data;
  if (d.tipo === 'datos') {
    $('modelo').textContent = d.modelo;
    $('nivel').innerHTML = d.niveles.map(n => `<option value="${n.id}">${n.nombre} (${n.cota.toFixed(2)} m)</option>`).join('');
    if (d.nivelActivo) $('nivel').value = d.nivelActivo;
    if (!estado.estilos.length) estado.estilos = d.estilos.map(s => ({ ...s }));
    pintarColores();
    calcular();
  } else if (d.tipo === 'resultado') {
    estado.resultado = d;
    dibujar();
    pintarLados();
    pintarResumen();
  } else if (d.tipo === 'puntos') {
    if (d.puntos.length) {
      estado.puntos = d.puntos;
      estado.ladoFrente = 0;
      estado.tipos = null;
      pintarPuntos();
      calcular();
    }
    $('avisos').insertAdjacentHTML('afterbegin', d.errores.map(t => `<div class="aviso">${t}</div>`).join(''));
  } else if (d.tipo === 'aviso') {
    $('avisos').insertAdjacentHTML('afterbegin', `<div class="aviso ${d.bien ? 'bien' : ''}">${d.texto}</div>`);
  } else if (d.tipo === 'generado') {
    $('avisos').insertAdjacentHTML('afterbegin', `<div class="aviso bien">Listo: ${d.lineas} líneas en Revit. Ctrl+Z las deshace.</div>`);
  }
});

$('modoRect').addEventListener('click', () => cambiarModo('rectangulo'));
$('modoPuntos').addEventListener('click', () => cambiarModo('puntos'));
document.querySelectorAll('#datosRect input, .contenido > .fila input, #nivel, #coordenadas').forEach(el => el.addEventListener('input', calcular));
$('generar').addEventListener('click', () => enviar({ tipo: 'generar', ...entrada() }));
$('cerrar').addEventListener('click', () => enviar({ tipo: 'cerrar' }));

pintarPuntos();
enviar({ tipo: 'listo' });
```

- [ ] **Paso 4: probar la página sin Revit.** Revisar la sintaxis con `node --check addins/area-util/Tablero/web/area-util.js`
  (Node 24 está instalado). La prueba de pantalla es la del paso 6.

- [ ] **Paso 5: compilar y correr las pruebas** (los dos comandos del inicio de la sección).

- [ ] **Paso 6: probar en Revit** (el usuario mira la pantalla). Desplegar con Revit cerrado, abrir el modelo de prueba
  y comprobar:
  1. Con 7 × 14 y los retiros por defecto, la derecha dice 98 m², 45,5 m² y 52,5 m², y la vista previa muestra el
     área útil roja.
  2. Clic en el lado derecho: pasa a ser el frente y el área útil cambia a 3,5 × 12 = 42 m².
  3. «Puntos», pegar desde Excel `0⇥0⇥0`, `10⇥0⇥0`, `8⇥12⇥0`, `2⇥12⇥0`: aparece un trapecio.
  4. Frente mínimo 8: aparece el aviso del frente y «Generar» sigue activo.
  5. Laterales 4 y 4 con frente 7: aviso rojo y «Generar» se desactiva.

- [ ] **Paso 7: commit** (solo si el usuario lo pide): `feat(area-util): tablero de tres columnas con vista previa y resumen (S4)`

---

### Tarea 9 (S5): generar en Revit

**Archivos:**
- Crear: `addins/area-util/Revit/EstilosRevit.cs`, `Revit/MarcaAreaUtil.cs`, `Revit/GeneradorLote.cs`
- Modificar: `addins/area-util/Tablero/PuenteTablero.cs` (método `Generar`)

**Interfaces:**
- Consume: `ResultadoLote`, `Trazo`, `EstiloLinea`, `GenerarAreaUtil`; los hallazgos de la tarea 7.
- Produce: `GeneradorLote.Generar(Document, ResultadoLote, Level, bool compartidas, IReadOnlyList<EstiloLinea>) → int`
  (líneas creadas). Lanza `InvalidOperationException` con un texto para el usuario si el lote queda lejos del origen.

- [ ] **Paso 1: `Revit/MarcaAreaUtil.cs`** (mismo mecanismo que `MarcaZapata`, con su propio esquema)

```csharp
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;

namespace CQEING.AreaUtil.Revit;

/// <summary>La marca invisible de cada línea que crea el add-in, para volver a generar sin duplicar (decisión 12).</summary>
internal static class MarcaAreaUtil
{
    private static readonly Guid IdEsquema = new("83b87101-0818-4eab-985d-f1f3bf45aca0");
    private const string NombreEsquema = "CQEINGAreaUtilLote";
    private const string CampoPieza = "Pieza";

    /// <summary>Debe llamarse dentro de una transacción abierta la primera vez (crea el esquema).</summary>
    private static Schema Esquema()
    {
        if (Schema.Lookup(IdEsquema) is { } existente)
            return existente;
        var constructor = new SchemaBuilder(IdEsquema);
        constructor.SetSchemaName(NombreEsquema);
        constructor.SetReadAccessLevel(AccessLevel.Public);
        constructor.SetWriteAccessLevel(AccessLevel.Vendor);
        constructor.SetVendorId("CQEING");
        constructor.AddSimpleField(CampoPieza, typeof(string)).SetDocumentation("Estilo de la línea: lote, retiro-… o area-util.");
        return constructor.Finish();
    }

    public static void Marcar(Element e, string pieza)
    {
        var entidad = new Entity(Esquema());
        entidad.Set(CampoPieza, pieza);
        e.SetEntity(entidad);
    }

    private static bool EsPropia(Element e) => Schema.Lookup(IdEsquema) is { } esquema && e.GetEntity(esquema).IsValid();

    /// <summary>Borra todas las líneas del add-in en el modelo; nunca las que no tienen la marca. Devuelve cuántas borró.</summary>
    public static int BorrarTodas(Document doc)
    {
        if (Schema.Lookup(IdEsquema) is null)
            return 0;
        List<ElementId> propias = new FilteredElementCollector(doc).OfClass(typeof(CurveElement)).Where(EsPropia).Select(e => e.Id).ToList();
        if (propias.Count > 0)
            doc.Delete(propias);
        return propias.Count;
    }
}
```

- [ ] **Paso 2: `Revit/EstilosRevit.cs`**

```csharp
using Autodesk.Revit.DB;
using CQEING.AreaUtil.Calculo;

namespace CQEING.AreaUtil.Revit;

/// <summary>Los estilos «CQ …» como subcategorías de Líneas (decisión 2 del spec). Medido en la tarea 7, paso 1.</summary>
internal static class EstilosRevit
{
    /// <summary>Crea o actualiza cada estilo con su color y grosor. Dentro de una transacción abierta.</summary>
    public static Dictionary<string, GraphicsStyle> Asegurar(Document doc, IReadOnlyList<EstiloLinea> estilos)
    {
        Category lineas = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
        var porClave = new Dictionary<string, GraphicsStyle>();
        foreach (EstiloLinea e in estilos)
        {
            Category sub = lineas.SubCategories.Contains(e.Nombre)
                ? lineas.SubCategories.get_Item(e.Nombre)
                : doc.Settings.Categories.NewSubcategory(lineas, e.Nombre);
            sub.LineColor = DeHex(e.Color);
            sub.SetLineWeight(Math.Clamp(e.Grosor, 1, 16), GraphicsStyleType.Projection);
            porClave[e.Clave] = sub.GetGraphicsStyle(GraphicsStyleType.Projection);
        }
        return porClave;
    }

    /// <summary>«#RRGGBB»; si no se entiende, gris.</summary>
    private static Color DeHex(string hex)
    {
        string h = hex.TrimStart('#');
        return h.Length == 6 && int.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out int v)
            ? new Color((byte)(v >> 16), (byte)(v >> 8 & 0xFF), (byte)(v & 0xFF))
            : new Color(128, 128, 128);
    }
}
```

- [ ] **Paso 3: `Revit/GeneradorLote.cs`**

```csharp
using Autodesk.Revit.DB;
using CQEING.AreaUtil.Calculo;

namespace CQEING.AreaUtil.Revit;

/// <summary>
/// Dibuja en Revit lo calculado, en una sola transacción («Área útil del lote»), así Ctrl+Z lo deshace de una vez.
/// Antes borra lo que generó el add-in (decisión 12). Coordenadas: decisión 8 del spec, medida en la tarea 7.
/// </summary>
internal static class GeneradorLote
{
    /// <summary>Revit pierde precisión lejos del origen interno; más allá de esto se avisa y no se genera.</summary>
    private const double DistanciaMaximaM = 30_000;

    public static int Generar(Document doc, ResultadoLote r, Level nivel, bool compartidas, IReadOnlyList<EstiloLinea> estilos)
    {
        // Medido en Revit 2026.5 (hallazgo 9 del plan): GetTotalTransform lleva de compartidas a internas.
        Transform aInterno = compartidas ? doc.ActiveProjectLocation.GetTotalTransform() : Transform.Identity;
        XYZ Interno(Punto p) => aInterno.OfPoint(new XYZ(Pies(p.X), Pies(p.Y), 0));

        if (r.Trazos.SelectMany(x => new[] { x.A, x.B }).Select(Interno).Any(q => Metros(Math.Sqrt(q.X * q.X + q.Y * q.Y)) > DistanciaMaximaM))
            throw new InvalidOperationException(
                "El lote queda a más de 30 km del origen del proyecto: revise el punto de reconocimiento o use «Coordenadas del proyecto».");

        using var tx = new Transaction(doc, "Área útil del lote");
        tx.Start();
        Dictionary<string, GraphicsStyle> estilo = EstilosRevit.Asegurar(doc, estilos);
        MarcaAreaUtil.BorrarTodas(doc);
        SketchPlane plano = SketchPlane.Create(doc, nivel.Id);
        double z = plano.GetPlane().Origin.Z;
        double corta = doc.Application.ShortCurveTolerance;
        int creadas = 0;
        foreach (Trazo trazo in r.Trazos)
        {
            XYZ a = Interno(trazo.A), b = Interno(trazo.B);
            var inicio = new XYZ(a.X, a.Y, z);
            var fin = new XYZ(b.X, b.Y, z);
            if (inicio.DistanceTo(fin) < corta)
                continue;
            ModelCurve linea = doc.Create.NewModelCurve(Line.CreateBound(inicio, fin), plano);
            linea.LineStyle = estilo[trazo.Estilo];
            MarcaAreaUtil.Marcar(linea, trazo.Estilo);
            creadas++;
        }
        tx.Commit();
        return creadas;
    }

    private static double Pies(double metros) => UnitUtils.ConvertToInternalUnits(metros, UnitTypeId.Meters);

    private static double Metros(double pies) => UnitUtils.ConvertFromInternalUnits(pies, UnitTypeId.Meters);
}
```

- [ ] **Paso 4: completar `Generar` en `PuenteTablero.cs`**

Reemplazar el método de la tarea 8 por:

```csharp
    private void Generar(Document doc, GenerarAreaUtil g)
    {
        ResultadoLote r = CalculoLote.Calcular(g.Entrada);
        if (!r.SePuedeGenerar)
        {
            ventana.Enviar(ProtocoloAreaUtil.Aviso("Hay avisos en rojo: corríjalos antes de generar."));
            return;
        }
        if (doc.GetElement(new ElementId(long.Parse(g.NivelId))) is not Level nivel)
        {
            ventana.Enviar(ProtocoloAreaUtil.Aviso("El nivel elegido ya no existe. Elija otro."));
            Recargar(uiapp);
            return;
        }
        try
        {
            ventana.Enviar(ProtocoloAreaUtil.Generado(GeneradorLote.Generar(doc, r, nivel, g.Compartidas, g.Estilos)));
        }
        catch (InvalidOperationException e)
        {
            ventana.Enviar(ProtocoloAreaUtil.Aviso(e.Message));
        }
    }
```

Y arriba del archivo, `using CQEING.AreaUtil.Revit;`. `long.Parse` sirve en 2026 y 2027 porque `ElementId` usa
`long` desde 2024. Si el id no es un número, falla `FormatException`, que `AccionesRevit` muestra en la página.

- [ ] **Paso 5: compilar y correr las pruebas.**

- [ ] **Paso 6: probar en Revit** (el usuario mira la pantalla; Revit cerrado para desplegar):
  1. 7 × 14 por defecto, nivel más bajo, «Generar en Revit»: en planta aparecen el lote negro, los tramos de retiro
     naranjas y el área útil roja de 5 × 10,5. En Gestionar → Estilos de objeto → Líneas están los cinco «CQ …».
  2. Cambiar el frente y generar otra vez: las líneas viejas desaparecen y quedan solo las nuevas.
  3. Ctrl+Z: desaparece la última generación completa.
  4. Cambiar el color del área útil y generar: el estilo «CQ Área útil» toma el color nuevo.
  5. Una línea de modelo dibujada a mano no se borra al generar.
  6. Puntos en coordenadas compartidas cerca del punto de reconocimiento del modelo de prueba (valor de la tarea 7):
     el lote cae donde corresponde.

- [ ] **Paso 7: commit** (solo si el usuario lo pide): `feat(area-util): genera el lote, los retiros y el área útil en Revit (S5)`

---

### Tarea 10 (S6): depuración y cierre

**Archivos:**
- Modificar: este plan (Estado y Hallazgos).

- [ ] **Paso 1: todo el repo**

```powershell
dotnet test CQEING-Addins.sln -c Release
dotnet build addins/area-util/CQEING.AreaUtil.csproj -c Release -p:RevitVersion=2026
```

Esperado: sin fallos y sin advertencias nuevas. 2027 no se compila en este equipo (no tiene SDK 10): lo hace el CI del
PR (`.github/workflows/compilar.yml`).

- [ ] **Paso 2: recorrer los criterios de aceptación** uno por uno en Revit 2026 con el usuario, y anotar ✅ / ⚠️ / ⏳
  con su evidencia en **Estado**.

- [ ] **Paso 3: anotar en Estado** la hora de inicio y de fin de cada sub-issue y lo que quedó pendiente para la
  versión 2.

- [ ] **Paso 4: PR y versión** (solo si el usuario los pide): PR de `luffyjpl/…-fork:area-util/addin-lote` a
  `utilidadcqeing-prog/…:main`. Después del merge, la etiqueta `area-util-v0.1.0` (anotada) publica la primera versión
  en el gestor.

### Tarea 11 (S7, #338): elegir dónde se coloca el lote

**Archivos:** crear `calculo/CQEING.AreaUtil.Calculo/Ubicacion.cs` y `calculo/CQEING.AreaUtil.Calculo.Tests/UbicacionTests.cs`;
modificar `ProtocoloAreaUtil.cs` (campo `ubicacion`, mensaje `elegirUbicacion`, respuesta `ubicacion`),
`Tablero/PuenteTablero.cs` (`ElegirUbicacion`), `Tablero/web/index.html` y `area-util.js` (grupo «Ubicación en Revit»).

**Interfaces:** `sealed record Ubicacion(double X, double Y, double GiroGrados)` con `Punto Aplicar(Punto)`,
`static Ubicacion Origen` y `static Ubicacion DesdeDosPuntos(Punto esquina, Punto haciaElFrente)`;
`sealed record ElegirUbicacionAreaUtil(string NivelId) : MensajeAreaUtil`; `ProtocoloAreaUtil.UbicacionElegida(Ubicacion)`.

- [ ] Pruebas que fallan: mover sin giro, girar 90°, dos clics a 90° y a 30°, dos clics en el mismo punto (giro 0),
  el rectángulo ubicado y girado mide lo mismo (52.5 m² útiles), el modo puntos ignora la ubicación.
- [ ] Implementar y ver las pruebas en verde; suite completa en verde.
- [ ] Página y puente; `node --check`; compilar 2026 sin advertencias.
- [ ] Probar en Revit (usuario): dos clics, Esc, X/Y/giro a mano.

### Tarea 12 (S8, #339): dibujo libre del lote

**Archivos:** crear `Revit/DibujoLibre.cs` (el ciclo de clics, las líneas temporales y la cuenta del cierre),
`Tablero/BarraDibujo.cs` (la barra), `Revit/ComandoDiagnosticoDibujo.cs` (solo Debug); modificar `AppAreaUtil.cs`
(botón de diagnóstico en Debug). Después: `calculo/CierreDibujo.cs` con pruebas, el mensaje `dibujarLote` en el
protocolo, el puente y el botón «Dibujar en Revit» de la página.

- [ ] Medición (decisión 22): desplegar Debug, correr «Diagnóstico de dibujo» en una planta, anotar el resultado en
  Hallazgos.
- [ ] Pruebas del cierre y de los puntos repetidos, en rojo y luego en verde.
- [ ] Barra, puente y página; compilar 2026 sin advertencias; suite en verde.
- [ ] Prueba del usuario en Revit (lista de la sub-issue #339).

## Cómo probarlo

```powershell
dotnet test addins/area-util/calculo/CQEING.AreaUtil.Calculo.Tests -c Release
dotnet build addins/area-util/CQEING.AreaUtil.csproj -c Release -p:RevitVersion=2026
dotnet test CQEING-Addins.sln -c Release

Get-Process -Name Revit -ErrorAction SilentlyContinue   # no debe devolver nada
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin area-util -Revit 2026
```

Lista en Revit: el paso 6 de las tareas 1, 8 y 9.

### Lista de prueba visual completa (2026-11-11)

Revit 2026 con una copia de `Prueba 080720sfsd26.0028`. Retiros por defecto: frontal 1.5, posterior 2, laterales 1.
Al terminar, no guardar el modelo.

| # | Qué | Cómo | Esperado |
|---|---|---|---|
| 1 | Abrir | CQEING → Lote → «Área útil del lote» | Ventana de tres columnas, el nombre del modelo arriba, niveles en la lista |
| 2 | Cerrar con la X | X de la ventana | Se cierra y Revit sigue abierto |
| 3 | Cerrar con «Cerrar» | Botón de la barra | Igual |
| 4 | Botón dos veces | Pulsar el botón con la ventana abierta | Trae la misma ventana, no abre otra |
| 5 | Rectángulo | 7 × 14 por defecto | 98 / 45,5 / 52,5 m², frente 7 m, área roja en la vista previa |
| 6 | Cambiar el frente | Clic en el lado derecho de la vista previa | Ese lado queda azul; útil 42 m² |
| 7 | Lado a mano | «Lados del lote»: lado 2 → Frente | Cambia el área útil |
| 8 | Frente mínimo | Frente mínimo 8 | Aviso amarillo; «Generar» sigue activo |
| 9 | Lote mínimo | Lote mínimo 120 | Aviso amarillo |
| 10 | IMO | IMO 0,5 | Ocupación máx. 49 m² y aviso «manda el índice» |
| 11 | IMC y pisos | IMC 1, pisos 2 | Construcción con los pisos 105 m² y aviso del IMC |
| 12 | Pisos con decimales | Pisos 2,5 | Sigue calculando (toma 2) |
| 13 | Ficha negativa | IMO -0,5 | Aviso «no se tuvo en cuenta» |
| 14 | Retiros que se comen el lote | Laterales 4 y 4 | Aviso rojo y «Generar» desactivado |
| 15 | Frente en 0 | Frente 0 | Aviso rojo «escriba frente y fondo mayores que cero» |
| 16 | Ubicación con clics | «Elegir en Revit», dos clics | Vuelve la ventana con X, Y y giro; vista previa girada |
| 17 | Ubicación: Esc | «Elegir en Revit» y Esc | Vuelve con «Se canceló» y nada cambia |
| 18 | Ubicación a mano | X 10, Y 5, giro 30 | La vista previa gira; las áreas no cambian |
| 19 | Puntos: rectángulo | Pegar `0 0 / 7 0 / 7 14 / 0 14` | Igual que el caso 5 |
| 20 | Puntos: trapecio | `0 0 / 10 0 / 8 12 / 2 12` | 96 / 44,53 / 51,47 m² |
| 21 | Puntos: lote en L | `0 0 / 12 0 / 12 8 / 6 8 / 6 16 / 0 16` | 144 / 67 / 77 m²; lados F, D, P, D, P, I |
| 22 | Puntos: irregular | `0 0 / 9 -1 / 11 7 / 6 13 / -1 10` | 124 / 55,31 / 68,69 m²; los dos lados de arriba posteriores |
| 23 | Excel con encabezado | Tres columnas con encabezado, copiadas | Ignora el encabezado |
| 24 | Rótulo del punto | `P1 0 0 0` … | Ignora «P1» |
| 25 | Coma decimal | `0;0 / 7,5;0 / 7,5;14 / 0;14` | 105 m² de lote |
| 26 | Miles | `1.200.000,50⇥900.000,25` | Lo lee como un punto |
| 27 | Fila basura | Una fila «hola» en medio | Aviso «Fila N: no se entiende» y el resto se lee |
| 28 | Cierre del topógrafo | El primer punto repetido al final | Mismo resultado, sin lado de más |
| 29 | Agregar y quitar | «+ Punto», editar una celda, «−» | La vista previa se actualiza |
| 30 | Dos puntos | Quitar hasta dejar 2 | Aviso rojo y «Generar» desactivado |
| 31 | Desorden | `0 0 / 7 14 / 7 0 / 0 14` | Aviso rojo «los lados del lote se cruzan» |
| 32 | Coordenadas grandes | `1200000 900000 / 1200007 900000 / 1200007 900014 / 1200000 900014` y «Generar» | Vista previa bien; al generar, aviso «más de 30 km del origen» |
| 33 | Generar | Caso 5, nivel N+0.00, desde la vista 3D | Se minimiza, abre la planta del nivel, líneas seleccionadas con zoom |
| 34 | Estilos | Gestionar → Estilos de objeto → Líneas | Los cinco «CQ …» con sus colores |
| 35 | Volver | Botón «Área útil del lote» | La ventana vuelve como estaba |
| 36 | Regenerar | Otro frente y «Generar» | Reemplaza; no quedan líneas viejas |
| 37 | Doble clic | «Generar» dos veces seguidas | Las líneas quedan una sola vez |
| 38 | Ctrl+Z | Después de generar | Se deshace toda la generación de una vez |
| 39 | Líneas a mano | Dibujar una línea de modelo y generar | La línea a mano no se borra |
| 40 | Colores | Cambiar el color y el grosor del área útil y generar | «CQ Área útil» toma el color nuevo |
| 41 | Nivel sin planta | Nivel N-0.65 (si no tiene planta) y generar | Se muestran en la vista activa, sin error |
| 42 | Cerrar tras generar | Generar y cerrar enseguida | Revit sigue abierto |
| 43 | Otro modelo | Abrir otro modelo con la ventana abierta y volver | Recarga modelo y niveles |
| 44 | Cerrar el modelo | Cerrar el modelo con la ventana abierta | Aviso «No hay un modelo de proyecto abierto», sin error |
| 45 | Nivel por defecto | Abrir el add-in desde la vista 3D | El botón dice «Generar en N+0.00 Piso 1 (0,00 m)», no la cimentación |
| 46 | Líneas en la planta | Generar y abrir la planta N+0.00 Piso 1 | Las líneas se ven en la planta |
| 47 | Retiros vacíos | Abrir el add-in en un modelo nuevo | Retiros vacíos y aviso amarillo «Faltan los retiros … se toman como 0» |
| 48 | Recordar los datos | Llenar, cerrar el add-in y volver a abrirlo | Vuelven el lote, los retiros, la ficha, el nivel y los colores |
| 49 | Volver al add-in | Generar | Aparece arriba «Lote generado en … · Volver al add-in»; un clic trae la ventana y la barra se va |
| 50 | Ventana más chica | Abrir el add-in | Se ve Revit alrededor; la ventana se puede agrandar |
| 51 | Vía pública | Vista previa | «Vía pública» del lado de afuera del frente; se mueve al cambiar el frente |
| 52 | Leer puntos | Escribir `0 0` / `7 0` / `7 14` / `0 14` en el cuadro y pulsar «Leer puntos» | Los puntos pasan a la tabla y el cuadro queda vacío |
| 53 | Dibujo: Deshacer | «Dibujar en Revit», 3 clics y «Deshacer punto» en la barra | Se borra el último tramo y se sigue dibujando |
| 54 | Dibujo: Cancelar | «Dibujar en Revit», 2 clics y «Cancelar» | Aviso «Se canceló», la tabla no cambia |
| 55 | Dibujo: Esc | «Dibujar en Revit», 4 clics y Esc | Los 4 puntos pasan a la tabla |

## Estado

- [x] Spec aprobado por el usuario (2026-11-10).
- [x] Plan revisado con el usuario (2026-11-10: «sí lo apruebo», ejecución en la sesión).
- [x] `dotnet test CQEING-Addins.sln -c Release` (2026-11-10 21:05): 1856 correctas, 0 fallos; compila 2026 Release con 0 advertencias.
- [x] Desplegado en Revit 2026 (Debug) el 2026-11-10 a las 21:06.

| Sub-issue | Tareas | Inicio | Fin | Duración | Estado |
|---|---|---|---|---|---|
| S1 Crear el add-in | 1 | 2026-11-10 20:57 | 20:59 (código) | 2 min | Código listo; falta verlo en Revit |
| S2 Cálculo con pruebas | 2–6 | 2026-11-10 20:59 | 21:03 | 4 min | ✅ 51 pruebas |
| S3 Medición en Revit | 7 | 2026-11-11 00:40 | 2026-11-11 00:42 | 2 min | ✅ Hallazgos 8 y 9; se corrigió el sentido de las coordenadas |
| S4 Tablero de tres columnas | 8 | 2026-11-10 21:04 | 21:05 (código) | 1 min | Código listo; falta verlo en Revit |
| S5 Generar en Revit | 9 | 2026-11-10 21:04 | 21:05 (código) + 2026-11-11 00:42 (coordenadas) | 1 min + corrección | Código listo y medido; falta probarlo en Revit |
| S6 Depuración y cierre | 10 | 2026-11-11 | | | ⏳ Lista de prueba de 55 pasos pasada (usuario, 11 y 12-nov); falta pasar el PR #340 a listo para revisión |
| S8 Dibujo libre del lote | 12 | 2026-11-11 10:13 | 2026-11-12 19:53 | código 2026-11-12 18:40–19:20; probado por el usuario | ✅ Clics, cierre, Deshacer, Cancelar, Esc; sin rastro |
| S7 Elegir dónde se coloca el lote | 11 | 2026-11-11 08:47 | 2026-11-11 09:51 | código 08:47–08:55; probado por el usuario | ✅ Dos clics, Esc, X/Y/giro a mano |

Versión 2 (fuera de este plan): dibujo libre del lote, línea de propiedad en 2027, recordar los datos en el modelo,
2.º piso y voladizos.
