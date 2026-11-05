# Plan: Crear el add-in de acero en zapatas en el monorepo (#247, épica #246)

## Contexto

- **Qué se pide:** [#247](https://github.com/utilidadcqeing-prog/Add-in-Acero-en-columnas-2027/issues/247), la primera
  sub-issue de la épica [#246](https://github.com/utilidadcqeing-prog/Add-in-Acero-en-columnas-2027/issues/246). Crear
  `addins/acero-zapatas/` con la misma estructura que `addins/acero-vigas`, y que en Revit aparezca un botón que abre una
  ventana vacía. El add-in coloca acero en zapatas que ya existen; no crea zapatas (Carlos y Juan Felipe, reunión del
  04-nov-2026).
- **Fuera de alcance:** leer zapatas (#248), reglas de ganchos (#249), catálogo (#250), motor (#251), tablero (#252),
  zapatas iguales (#253), generar acero (#254) y publicar (#255).
- **Entorno:** Revit 2026.5.1, .NET SDK 8 y 10, Windows 11. Rama `zapatas/247-crear-addin`, en el fork
  `luffyjpl/Add-in-Acero-en-columnas-2027-fork`, con PR hacia `main` de upstream.

## Criterios de aceptación

- [ ] Existe `addins/acero-zapatas/` con `Revit/`, `Tablero/` y `calculo/`, y el cálculo tiene su proyecto de pruebas.
- [ ] `addin.json`, `.addin` y `.csproj` siguen a vigas: mismo `Directory.Build.props` y años 2026 y 2027.
- [ ] El botón se crea con `CintaCqeing`, y `OnStartup` exige licencia y versión al día con `compartido/`, sin copiar ese
      código.
- [ ] Compila y carga en Revit 2026 con `desplegar-local.ps1 -Revit 2026`, mostrando una ventana vacía.
- [ ] **Listo cuando:** el botón aparece en la cinta y abre la ventana del add-in.

## Hallazgos del código y de Revit real

Todo esto salió de leer el código de `main` en `6de33cb`. Todavía no se ha probado nada del add-in en Revit.

- **Calcular sin Revit:** `addins/acero-vigas/calculo/Directory.Build.props` y el de viguetas cortan la herencia de
  `addins/Directory.Build.props`. Sin ese archivo, el cálculo heredaría la API de Revit y WPF.
- **Ventana sin XAML:** `addins/crear-viguetas/Interfaz/VentanaViguetas.cs` se arma en código porque en Revit 2027 el
  lector de XAML no resuelve el WebView2 que trae Revit.
- **Licencia:** `TodosLosAddinsExigenLicenciaTests` revisa cada `IExternalApplication` de `addins/`, así que un add-in
  sin las dos líneas no pasa las pruebas.
- **Sin atajo para desarrollo:** `desplegar-local.ps1` carga el add-in, pero `Licencia.Vigente` también manda en este
  caso (decisión 9 de `docs/planes/2026-10-28-licencias-hoja-google.md`). Sin licencia el botón no aparece.
- **Este equipo (04-nov):** tiene el gestor 0.3.1 instalado y ya recibió `licencia.json`.
- **.NET en 2026.5:** `RevitAPI.dll` de este Revit apunta a .NET 10, y el repo compila 2026 en `net8.0-windows`.
  `docs/planes/2026-10-19-gestor-cuadro-cantidades-105.md` indica que eso solo deja el aviso «Assembly version conflict»
  en el journal. Hay que confirmarlo aquí.
- **Precedente de reutilización:** `crear-viguetas` y `despiece` enlazan archivos sueltos del cálculo de vigas con
  `<Compile Include … Link=…>`; no referencian el proyecto. Sirve para decidir en #249.

## Decisiones

### De este issue

1. **Nombres** (2026-11-04, Claude): id `acero-zapatas`, ensamblado y namespace `CQEING.Zapatas`, panel «Zapatas». El id
   es el de la etiqueta `acero-zapatas-v0.1.0` de #255 y no se cambia nunca.
2. **Ventana con WebView2 y sin XAML** (2026-11-04, Claude): la maqueta aprobada es una página web, así que #252 la puede
   montar sin rehacer la ventana. Sin XAML, por el hallazgo de Revit 2027.
3. **Tamaño fijo por ahora** (2026-11-04, Claude): 1100 × 700. El encaje al monitor y el zoom de viguetas se dejan para
   #252, cuando haya una página que encajar.
4. **`Revit/` con `.gitkeep`** (2026-11-04, Claude): git no guarda carpetas vacías y #248 pondrá ahí el primer código.
5. **Icono `iconoCQ`** (2026-11-04, Claude): como viguetas y cotas de planta, hasta que exista uno propio.
6. **Un valor real en el cálculo** (2026-11-04, Claude): `ValoresPorDefecto.RecubrimientoMm = 75`, para que el proyecto y
   su prueba tengan contenido. Lo usará #251.

### Para las siguientes sub-issues

Se anotan aquí para no perderlas. No se programan en este issue.

7. **Interfaz** (2026-11-04, Juan Felipe, con el usuario): planta solo de cimentación, eligiendo una o varias zapatas; se
   conserva el movimiento de la vista; sin tipo de acero ni f'c (cambia #246, #252 y la maqueta).
8. **Recubrimiento** (2026-11-04, Juan Felipe, con el usuario): un valor global y un botón **Opciones** para editar
   abajo, lados y arriba, cambiable desde el add-in. **Por defecto 7.5 cm:** el usuario descartó los 8 cm propuestos.
9. **Barras** (2026-11-04, Juan Felipe): no se pueden atravesar entre sí.
10. **Orden de capas** (2026-11-04, Juan Felipe): en zapata rectangular, la barra paralela al lado largo va más cerca del
    suelo; en cuadrada, X abajo salvo que el plano diga otra cosa; arriba se invierte, con la principal hacia afuera; en
    combinada, la principal es la dirección larga que une las columnas.
11. **Choque de ganchos** (2026-11-04, Juan Felipe): la parrilla superior se corre media separación.
12. **Largo de la pata** (2026-11-04, Juan Felipe): altura − recubrimiento inferior − recubrimiento superior. La barra se
    crea ajustada y solo se avisa si la pata queda por debajo de 12 diámetros.
13. **Redondeo** (2026-11-04, Juan Felipe): la longitud total de corte sube a múltiplos de 5 cm, y el peso sale de esa
    longitud.
14. **Gancho** (2026-11-04, Juan Felipe): manda el plano o el modelador; se crea con eso y se avisa si no coincide con la
    tabla.
15. **Arranques de columna** (2026-11-04, Juan Felipe): los coloca el add-in de columnas, no cuentan como choque y no
    bloquean. Como mucho se cuida que una barra no pase justo por un arranque.

### Por resolver en su sub-issue

- **#249/#250 (resuelto el 2026-11-04, usuario):** con la pata ajustada casi nunca coincidirá el largo del cuadro
  (h = 0.50 y 7.5 cm dan 0.35 m; el #5 dice 0.278). El aviso de tabla compara el tipo y el ángulo del gancho elegido, y la
  pata conserva solo su aviso de 12 diámetros.
- **#251:** `Reglas.LargoGanchoRedondeado` redondea el gancho y la decisión 13 pide redondear la longitud total, así que
  no se puede usar tal cual. Correr media separación deja normalmente una barra menos por dirección arriba, y el resumen
  debe reflejarlo.
- **#254:** falta medir en Revit el choque entre la barra de borde y los ganchos de la otra dirección.

## Plan paso a paso

1. **Cálculo y pruebas** — archivos: `addins/acero-zapatas/calculo/Directory.Build.props`,
   `CQEING.Zapatas.Calculo/` y `CQEING.Zapatas.Calculo.Tests/` — cómo se prueba: `dotnet test` del proyecto de pruebas.
2. **Proyecto del add-in** — archivos: `addin.json`, `CQEING.Zapatas.csproj`, `CQEING.Zapatas.addin` con `ClientId`
   propio, `AppZapatas.cs`, `Resources/iconoCQ16.png`, `Resources/iconoCQ32.png` y `Revit/.gitkeep` — cómo se prueba: se
   compila en el paso 4.
3. **Ventana y comando** — archivos: `Tablero/ComandoTablero.cs`, `Tablero/VentanaTablero.cs` y
   `Tablero/web/index.html` — cómo se prueba: se compila en el paso 4 y se abre en el paso 6.
4. **Solución y README** — archivos: `CQEING-Addins.sln` y `README.md` — cómo se prueba: compilar para 2026 y 2027, sin
   advertencias.
5. **Pruebas de todo el repo** — cómo se prueba: `dotnet test CQEING-Addins.sln -c Release`, sin fallos.
6. **Probar en Revit** — requiere licencia aprobada y Revit cerrado — cómo se prueba: la lista de abajo.
7. **Cerrar el plan** — actualizar **Estado**. El commit, el push y el PR se hacen solo cuando el usuario los pida.

## Cómo probarlo

```powershell
dotnet build addins/acero-zapatas/CQEING.Zapatas.csproj -c Debug -p:RevitVersion=2026
dotnet build addins/acero-zapatas/CQEING.Zapatas.csproj -c Debug -p:RevitVersion=2027
dotnet test CQEING-Addins.sln -c Release

Get-Process -Name Revit -ErrorAction SilentlyContinue   # no debe devolver nada
Test-Path "$env:LOCALAPPDATA\CQEING\licencia.json"      # debe dar True
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin acero-zapatas -Revit 2026
```

1. Abrir Revit 2026 con un proyecto cualquiera.
2. En la pestaña **CQEING**, comprobar que aparece el panel **Zapatas** con el botón **Acero en zapatas**.
3. Pulsar el botón: se abre la ventana «Acero en zapatas», vacía.
4. Pulsar de nuevo: no se abre otra ventana; la misma pasa al frente.
5. Cerrarla y abrirla otra vez: abre sin errores.

## Estado

- [x] Plan aceptado por el usuario para avanzar (2026-11-04: «proseguir con todo el flujo y su markdown»).
- [x] Pasos 1 a 5 implementados (2026-11-04), sobre `main` en `6de33cb`.
- [x] Paso 6, probado en Revit 2026.5.1 por el usuario (2026-11-04): el add-in carga, el botón aparece y abre la ventana
      vacía. Volver a pulsar el botón y reabrir la ventana no se confirmaron por separado.
- [x] Git (2026-11-04, usuario): la épica va en una sola rama, `zapatas/246-acero-zapatas`, con commit local por issue.
      Push y PR, solo cuando el usuario los pida.

Comprobado (2026-11-04):

- `dotnet build` de `CQEING.Zapatas.csproj` en Debug para 2026 y 2027: 0 advertencias, 0 errores.
- `dotnet test CQEING-Addins.sln -c Release`: 1495 pruebas correctas en 7 proyectos, 0 fallos. Incluye
  `TodosLosAddinsExigenLicenciaTests`, que ya recorre este add-in, y la prueba nueva `ValoresPorDefectoTests`.

Criterios de aceptación:

- ✅ Carpeta con `Revit/`, `Tablero/` y `calculo/`, con proyecto de pruebas.
- ✅ `addin.json`, `.addin` y `.csproj` como en vigas, para 2026 y 2027.
- ✅ Botón con `CintaCqeing`, licencia y bloqueo por versión de `compartido/`, sin copiar código.
- ✅ Compila y carga en Revit 2026 con `desplegar-local.ps1`, mostrando una ventana vacía.
- ✅ El botón aparece en la cinta y abre la ventana del add-in.
