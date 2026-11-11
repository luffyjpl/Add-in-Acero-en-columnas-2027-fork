# Plan: nombre del add-in de columnas en el aviso de Revit («practicacq»)

## Contexto

- Pedido de Luciano (11-nov): al aceptar el add-in en Revit (el aviso de
  seguridad de "cargar siempre / cargar una vez") aparece «practicacq».
  Quiere un nombre claro, tipo «Acero-Columnas».
- Fuera de alcance: renombrar la DLL (`practicacq.dll`), el proyecto
  (`practicacq.csproj`), el espacio de nombres `practicacq` o el archivo
  `practicacq.addin`. Están atados a los «heredados» del gestor
  (`addin.json`, `LimpiezaVersionesViejas.Heredados`) y a sus pruebas;
  cambiarlos es otro trabajo, con más riesgo.
- Entorno: Revit 2026 y 2027. Rama `columnas/nombre-addin` desde `main`
  (74820d5), worktree `C:\Proyecto\addins-nombre`.

## Criterios de aceptación

- [ ] En el aviso de Revit el add-in de columnas sale como
      «CQEING - Acero en columnas», no como «practicacq».
- [ ] El add-in sigue cargando y su botón funciona (2026 y 2027).
- [ ] El gestor lo sigue instalando y actualizando igual (misma DLL, mismo
      `ClientId`, mismo archivo `.addin`).
- [ ] `dotnet test CQEING-Addins.sln` sin fallos y compilación sin advertencias.

## Hallazgos del código y de Revit real

1. El texto sale del `<Name>` del manifiesto
   `addins/armado-columnas/practicacq.addin:4`: `<Name>practicacq</Name>`.
2. Los demás add-ins usan «CQEING - <nombre>»: `CQEING.Vigas.addin` →
   «CQEING - Acero en vigas», `CQEING.Despiece.addin` → «CQEING - Despiece», etc.
   El `addin.json` de columnas ya dice `"nombre": "Acero en columnas"` (lo que
   muestra el gestor).
3. Ni el publicador ni el gestor tocan `<Name>`: el publicador solo exige un
   `.addin` en el ZIP (`tools/CQEING.Publicador/Empaquetador.cs:18`); el gestor
   y `tools/desplegar-local.ps1` reescriben solo `<Assembly>`
   (`gestor/CQEING.Gestor.Core/Instalador.cs:188`, `desplegar-local.ps1:30`).
4. Pendiente de ver en Revit real: si cambiar el `<Name>` hace que Revit
   vuelva a preguntar «cargar siempre» a quien ya lo había aceptado (se espera
   que sí pregunte una vez; se anota lo que pase).

## Decisiones

1. **Nombre: «CQEING - Acero en columnas»** (2026-11-11, propuesta de Claude
   sobre el pedido de Luciano, «Acero-Columnas o algo así»): igual formato
   que los demás add-ins y mismo nombre que el gestor.
2. **Solo se cambia `<Name>`** (2026-11-11): DLL, `ClientId`, clase y archivo
   quedan igual (ver Contexto).

## Plan paso a paso

1. **Cambiar el nombre** — `addins/armado-columnas/practicacq.addin`, línea
   `<Name>` — cómo se prueba: build Debug 2027 sin advertencias y
   `dotnet test CQEING-Addins.sln`.
2. **Desplegar y probar en Revit** (2027 y 2026) — anotar en Hallazgos qué
   muestra el aviso y si volvió a preguntar.
3. **PR y versión** — solo con pedido de Luciano.

## Cómo probarlo

```
dotnet build addins/armado-columnas/practicacq.csproj -c Debug -p:RevitVersion=2027
dotnet test CQEING-Addins.sln
Get-Process -Name Revit -ErrorAction SilentlyContinue   # tiene que no devolver nada
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin armado-columnas -Revit 2027
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin armado-columnas -Revit 2026
```

1. Abrir Revit: si sale el aviso, tiene que decir «CQEING - Acero en columnas».
2. Abrir la ventana de columnas desde la cinta CQEING: funciona como antes.

## Estado

- [x] Plan revisado con el usuario (aceptó el nombre propuesto).
- [x] Paso 1 (11-nov): `<Name>CQEING - Acero en columnas</Name>`. Build Debug
      2027 con 0 advertencias; `dotnet test CQEING-Addins.sln` todo correcto.
- [x] Desplegado en 2027 y 2026 desde `C:\Proyecto\addins-nombre` (Revit
      cerrado, comprobado con `Get-Process`); el `.addin` instalado ya dice
      el nombre nuevo.
- [x] Probado en Revit por Luciano el 11-nov: "funciona". No dijo si Revit
      volvió a preguntar «cargar siempre» (hallazgo 4 sin dato).
- Criterios: ✅ los cuatro (el del gestor, porque no cambió nada de lo que el
  gestor lee: DLL, `ClientId` y archivo `.addin` son los mismos).
