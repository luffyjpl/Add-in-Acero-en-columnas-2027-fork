# Plan: despiece de vigas, observaciones de la prueba del 06-11-2026 (#303)

## Contexto

- Issue padre #303 (parte de #217), con tres sub-issues:
  - #300: la vista previa se cae con losa (cota de alto sin referencia).
  - #301: la barra de piel va entre la viga y los bastones.
  - #302: los textos de los estribos se alejaron de la viga.
- Lo pidió Luciano el 06-11-2026, después de probar el despiece en Revit 2027 con un modelo con losa.
- Fuera de alcance: el despiece de columnas y el add-in de vigas (`acero-vigas`).
- Entorno: Revit 2027, .NET 10. Rama `despiece/vigas-303` en el worktree `C:\Proyecto\addins-303`, creada
  desde `origin/main` en `a826977` (sin upstream: al pushear, `git push -u origin despiece/vigas-303`).

## Criterios de aceptación

- [ ] #300: con la viga de 0.35 x 0.50 y la losa de 0.25 encima, la previa se genera y la cota de alto de la
      sección marca 0.50.
- [ ] #300: si una cota de la sección no se puede crear, se registra en la traza y la previa sigue; no se deshace.
- [ ] #300: sin error al salir con un modelo con losa (a confirmar con la traza de la otra PC).
- [ ] #301: la piel se dibuja pegada a la viga, antes de los bastones, del lado que dice su altura.
- [ ] #302: las etiquetas de los estribos quedan pegadas a la viga, como antes de #262.
- [ ] `dotnet test CQEING-Addins.sln` sin fallos y compilación sin advertencias.

## Hallazgos del código y de Revit real

### #300 (la previa se cae con losa)

- Traza de Luciano (`%AppData%\CQEING\despiece\traza.log` de la PC donde probó, 06-nov): la previa se cae al armar la
  vista de sección, porque `NewDimension` lanza `Invalid number of references` en la cota de alto. En la traza de esta
  PC no aparece: la prueba con losa se hizo en otra máquina.
- `addins/despiece/Revit/GeneradorPrevia.cs`, método `Acotar` (líneas ~1009-1057): `Tapas()` toma las caras
  horizontales del sólido de la viga y usa la más alta (`tapas[^1]`). Con la losa, esa cara está en Z = 4310 (arriba de
  la losa, o sea los 0.50 completos), pero su `Reference` no sirve para una cota.
- `Caras` (línea ~1062) devuelve todas las caras planas, también las que no traen `Reference`.
- `Acotar` no está protegida: la excepción sube y se deshace la previa entera. `CotarColumnas` (línea ~515) ya resuelve
  el mismo problema con una `SubTransaction` y `try/catch`, y acota contra planos de referencia propios de la vista
  (`doc.Create.NewReferencePlane`, en blanco), un método ya probado en Revit (31-oct).
- Error al salir: en la traza de esta PC, cada vez que se cierra la ventana aparece
  `ventana: no se pudo quitar la suscripción: Invalid call to Revit API!`. Pasa con y sin losa y ya se registraba
  antes, así que no explica el error. Para ver qué pasa hace falta la traza de la PC donde se probó.

### #301 (piel)

- El add-in de vigas marca la piel como pieza `P1`, `P2`… (`MarcaVigas.Serie` devuelve `P`).
- En `Longitudinal`, `EstaArriba` manda la serie `P` arriba o abajo según la altura de su tramo recto (bien).
- `Lineas` (línea ~368) acomoda primero los bastones, después la fila `S`/`I` y al final "las demás". La piel cae en
  "las demás", o sea en la fila más alejada de la viga (captura del 06-nov: la barra azul quedó debajo de todo).

### #302 (etiquetas de estribos)

- `EtiquetarEstribos` pone cada etiqueta en `techo + AltoTexto`. Desde el commit `ad07127` (#262, columnas de los nudos
  visibles), `Longitudinal` le pasa `max.Z + asomo` como techo, con `Asomo = 0.20 m`. Por eso las etiquetas subieron
  0.20 m y se separaron de la viga.

## Decisiones

1. **La piel va del lado que dice su altura** (2026-11-06, Luciano): si su tramo recto está en la mitad de arriba de la
   viga se dibuja arriba; si no, abajo. Es la regla de hoy; lo que cambia es que pasa a ser la primera fila, entre la
   viga y los bastones.
2. **La cota de alto marca la viga completa, 0.50 con la losa** (2026-11-06, Luciano): del fondo de la viga a la cara
   de arriba de la losa, el peralte del plano.
3. **Cotas de la sección contra planos de referencia si la cara no sirve** (2026-11-06): se intenta primero con las
   caras de la viga, como hoy. Si Revit no acepta la cota, se deshace solo ese intento y se repite con planos de
   referencia en blanco, a la Z (o a la posición) de esas mismas caras, como en `CotarColumnas`. Si eso también falla,
   se registra y la sección queda sin esa cota.
4. **Las etiquetas de estribos vuelven a la viga** (2026-11-06): se calculan desde `max.Z`, sin `asomo`. Las filas de
   barras siguen empezando después del asomo de las columnas. Hay que mirar en Revit que una etiqueta de la zona junto a
   una columna no choque con la columna que asoma.

## Plan paso a paso

1. **#302: etiquetas desde la viga.** Archivo: `GeneradorPrevia.cs`, en `Longitudinal`, la llamada a
   `EtiquetarEstribos` recibe `max.Z` en lugar de `max.Z + asomo`. Para comprobarlo: compila. En Revit, las etiquetas
   quedan a la misma distancia de la viga que antes de #262.
2. **#301: la piel primero.** Archivo: `GeneradorPrevia.cs`, en `Lineas`: se acomoda primero la serie `P` (piel),
   después los bastones, después la fila `S`/`I` y al final el resto (que ya no incluye la `P`). Se actualiza el
   comentario. Para comprobarlo: compila. En Revit, la barra azul queda pegada a la viga, del lado de su altura.
3. **#300: cotas protegidas y con respaldo.** Archivo: `GeneradorPrevia.cs`, método `Acotar`:
   - `Caras` no cambia: la cara de arriba de la losa puede no traer referencia, pero es la que da la Z de los 0.50.
     La usan también otras partes del despiece.
   - Cada `Cota(...)` se hace dentro de una `SubTransaction`. Si falla, se deshace y se reintenta contra dos planos de
     referencia nuevos de la vista, en blanco, puestos en las posiciones de las caras. Si eso también falla, se deja
     en la traza y se sigue.
   - Las posiciones (Z de abajo y de arriba, caras de los lados) se siguen leyendo del sólido de la viga, sin importar
     si la cara trae referencia: así la cota de alto marca 0.50 (decisión 2).
   - En la traza quedan las caras horizontales leídas (Z y si traen referencia) y qué camino se usó para cada cota.
   Para comprobarlo: compila. En Revit, la previa de la viga con losa sale y la cota de alto dice 0.50.
4. **Pruebas y build.** `dotnet build` del despiece sin advertencias y `dotnet test CQEING-Addins.sln` sin fallos.
5. **Deploy y prueba en Revit 2027** (la hace Luciano). Ver "Cómo probarlo".

## Cómo probarlo

```
cd C:\Proyecto\addins-303
dotnet build addins/despiece/CQEING.Despiece.csproj -c Debug -p:RevitVersion=2027
dotnet test CQEING-Addins.sln
Get-Process -Name Revit -ErrorAction SilentlyContinue   # tiene que no devolver nada
powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin despiece -Revit 2027
```

1. Modelo con losa: vista previa de la viga 0.35 x 0.50 con la losa de 0.25. Sale la previa y la cota de alto dice 0.50.
   En la traza, buscar `sección: cota`.
2. La misma viga u otra con piel: la barra de piel queda pegada a la viga, antes de los bastones.
3. Una viga con columnas en los nudos: las etiquetas de los estribos quedan pegadas a la viga y no chocan con la
   columna que asoma.
4. Cerrar la ventana y salir de Revit con el modelo con losa abierto. Si hay error, pasar la traza.

## Estado

- [x] Plan revisado con el usuario (decisiones 1 y 2, 06-nov).
- [x] Pasos 1 a 3 implementados (06-nov, sin commit todavía):
  - #302: `EtiquetarEstribos` recibe `max.Z`.
  - #301: `MarcaVigas.EsPiel` (serie `P`, igual que el `Id` de la piel en `ArmadorVistaTests` del add-in de vigas)
    y `Lineas` acomoda la piel primero.
  - #300: `Acotar` intenta cada cota con las caras y, si falla, con planos de referencia, cada intento en su
    `SubTransaction`. En la traza quedan `sección: caras horizontales …` y `sección: cota del alto …`.
- [x] Paso 4: build del despiece con 0 advertencias y 0 errores. `dotnet test CQEING-Addins.sln` sin fallos
  (por ejemplo, despiece 138/138 y vigas 1135/1135).
- [x] Desplegado en Revit 2027 desde `C:\Proyecto\addins-303` (06-nov).
- [x] Probado en Revit 2027 por Luciano (06-nov): las 4 pruebas de "Cómo probarlo" pasan ("todo funciona"). Previa
  con losa correcta, piel pegada a la viga, etiquetas de estribos pegadas a la viga y sin error al salir. Con esto se
  cumplen todos los criterios de aceptación.
