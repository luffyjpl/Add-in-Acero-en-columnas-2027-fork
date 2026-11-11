# Área útil del lote

Add-in de Revit 2026 y 2027 (pestaña **CQEING → Lote**). Con los datos de la ficha de la curaduría dibuja en Revit el
contorno del lote, las líneas de retiro y el **área útil del primer piso**: lo que hoy se hace en Excel y después a mano
en AutoCAD o Revit.

> Versión 1. Dibujo libre del lote (clics en Revit), 2.º piso con voladizos y recordar los datos en el modelo llegan en
> versiones siguientes.

## Antes de empezar

- El equipo tiene que tener la **licencia CQEING** aprobada y el add-in instalado desde el **Gestor de add-ins**.
- Abra un **modelo de proyecto** (no una familia). Conviene que el nivel donde va el lote tenga una **planta**.

## La ventana

Pestaña **CQEING → panel Lote → Área útil del lote**. La ventana tiene tres columnas:

| Columna | Qué tiene |
|---|---|
| **Datos** (izquierda) | El lote, los retiros, la ficha de la curaduría, los lados y el nivel |
| **Vista previa** (centro) | El lote (negro), los tramos de retiro (naranjas) y el área útil (roja), con el largo de cada lado |
| **Resumen y recomendaciones** (derecha) | Las áreas, lo que permite la ficha, los avisos, los colores y el botón **Generar en Revit** |

Todo se recalcula al escribir: no hay que pulsar nada para ver el resultado.

## Paso a paso

### 1. El lote

**Lote rectangular** (pestaña **Frente × fondo**):

1. Escriba el **frente** y el **fondo** en metros (por ejemplo 7 × 14).
2. **Ubicación en Revit**: por defecto la esquina izquierda del frente va en el origen del proyecto, con el frente sobre
   el eje X. Para ponerlo en otro lugar:
   - **Elegir en Revit**: la ventana se minimiza y Revit abre la planta del nivel. Dé **dos clics**: primero la esquina
     izquierda del frente y después un punto hacia donde va el frente. X, Y y el giro se llenan solos. **Esc** cancela.
   - O escriba **X**, **Y** (m) y el **giro del frente** (°, antihorario desde el eje X).

**Lote por puntos** (pestaña **Puntos**), para lotes irregulares o con levantamiento topográfico:

1. Copie las coordenadas desde Excel o AutoCAD (columnas X, Y y Z opcional) y péguelas con **Ctrl+V** en el cuadro
   **Pegar desde Excel o AutoCAD**. Los puntos pasan a la tabla.
   - Se aceptan columnas separadas por tabulador, punto y coma o espacios; coma o punto decimal; números con separador
     de miles; una fila de encabezado y un rótulo por punto (P1, P2…).
   - Si una fila no se entiende, se avisa con su número y el resto se lee.
   - Si el primer punto se repite al final (como lo cierran los topógrafos), no pasa nada.
2. También se puede escribir en la tabla: **+ Punto** agrega una fila y **−** la quita.
3. **Coordenadas**: «Compartidas (topografía)» si los puntos vienen del topógrafo; «Del proyecto» si son medidas de
   Revit.

> El cuadro de pegar solo reacciona al pegar (Ctrl+V). Para escribir a mano, use la tabla.

### 2. El frente y los lados

- El **frente** se marca en **azul** en la vista previa. Para cambiarlo, **haga clic en otro lado**.
- Los demás lados se clasifican solos **según hacia dónde miran**: los que miran hacia el fondo son **posteriores**
  (en un lote en L pueden ser dos), los que miran a un costado son **laterales izquierdo o derecho**, y los que miran
  a la calle también son **frente**.
- En **Lados del lote** se puede cambiar cualquier lado a mano.

### 3. Los retiros y la ficha

1. **Retiros** (m): frontal, posterior, lateral izquierdo y lateral derecho, como los da la ficha.
2. **Ficha de la curaduría** (opcional): IMO, IMC, pisos, lote mínimo y frente mínimo. Lo que se deje vacío no genera
   avisos.

### 4. Revisar el resumen

| Dato | Qué es |
|---|---|
| Área del lote | El área del contorno |
| Área de retiros | Lo que queda fuera del área útil |
| Área útil (1.er piso) | Donde se puede construir |
| Ocupación máxima (IMO) | IMO × área del lote |
| Construcción máxima (IMC) | IMC × área del lote |
| Construcción con los pisos | Área útil × pisos |

**Avisos**:

- **Amarillos** (se puede generar): frente o lote menores que el mínimo; área útil mayor que la que permite el IMO;
  construcción con los pisos mayor que la del IMC; datos de la ficha en cero o negativos (se ignoran).
- **Rojos** (no se puede generar): falta el lote, los lados se cruzan (puntos en desorden), o los retiros se comen el
  lote.

### 5. Generar en Revit

1. Elija el **nivel** donde va el lote.
2. Si quiere, cambie el **color** y el **grosor** de cada estilo en **Colores en Revit**.
3. Pulse **Generar en Revit**. La ventana se minimiza, Revit abre la planta del nivel y deja **seleccionadas** las
   líneas creadas.
4. Para volver a la ventana, pulse otra vez el botón **Área útil del lote** de la cinta.

Lo que queda en el modelo son **líneas de modelo** con estos estilos (Gestionar → Estilos de objeto → Líneas):

| Estilo | Qué es |
|---|---|
| CQ Lote | El contorno del lote |
| CQ Retiro frontal / lateral / posterior | Los tramos de retiro, del lado del lote a la esquina del área útil |
| CQ Área útil | El contorno cerrado del área útil |

- **Volver a generar** reemplaza lo que generó el add-in; las líneas dibujadas a mano no se tocan.
- **Ctrl+Z** deshace toda la generación de una vez.

## Preguntas frecuentes

- **El área útil sale en rojo y no deja generar.** Los retiros son más grandes que el lote en algún lado, o los puntos
  están en desorden. El aviso dice cuál.
- **El lote quedó lejos del modelo.** Si eligió «Compartidas», revise que el punto de reconocimiento del modelo esté
  bien ubicado; si el lote queda a más de 30 km del origen, el add-in avisa y no genera.
- **No veo las líneas.** Revise que la planta del nivel elegido no oculte la categoría Líneas o los estilos «CQ …».
- **El nivel no tiene planta.** Las líneas se generan igual y se muestran en la vista activa.

## Para quien mantiene el add-in

- Código: `addins/area-util/` (cálculo sin Revit en `calculo/`, con sus pruebas; ventana y página en `Tablero/`;
  escritura en Revit en `Revit/`).
- Diseño: `docs/superpowers/specs/2026-11-10-area-util-lote-design.md`. Plan, decisiones, hallazgos y lista de prueba
  completa: `docs/planes/2026-11-10-addin-area-util.md`.
- Compilar y probar:

  ```powershell
  dotnet test addins/area-util/calculo/CQEING.AreaUtil.Calculo.Tests -c Release
  dotnet build addins/area-util/CQEING.AreaUtil.csproj -c Release -p:RevitVersion=2026
  powershell -ExecutionPolicy Bypass -File tools/desplegar-local.ps1 -Addin area-util -Revit 2026   # con Revit cerrado
  ```
