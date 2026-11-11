# Diseño: add-in «Área útil del lote»

- **Fecha:** 2026-11-10
- **Origen:** reunión del 09-nov-2026 con Carlos Quintero, la arquitecta María Belén y Joseph. La transcripción
  completa, con el minuto de cada frase, está en
  `C:\Users\JOSETP\Documents\WORK\typescritp\Reunion 2026-11-09 - transcripcion completa (por minuto).md`.
- **Estado:** diseño aprobado por el usuario el 2026-11-10.

## Problema

Para saber dónde se puede construir, el arquitecto toma los índices y retiros de la ficha reglamentaria de la curaduría,
calcula el área útil en Excel y después dibuja a mano en AutoCAD o en Revit las líneas del lote y de los retiros. Carlos
quiere que todo eso pase dentro de Revit: «la meta de esto es no trabajar para nada con AutoCAD, solamente con Revit»
(min 50).

## Objetivo

Un add-in de la pestaña CQEING donde el arquitecto ingresa el lote y los datos de la ficha, ve el resultado en una vista
previa con las áreas y los avisos, y genera en Revit el contorno del lote, las líneas de retiro y el contorno cerrado del
**área útil del 1.er piso**.

**Éxito:** un lote de 7 × 14 m con retiros frontal 1.5, posterior 2 y laterales 1 genera en Revit el contorno de
98 m² y un área útil de 5 × 10.5 = **52.5 m²**. La columna derecha muestra esas áreas y los avisos de la ficha.

## Alcance

### Versión 1 (este diseño)

- Lote por **frente × fondo** o por **puntos X/Y/Z**: se pegan desde Excel o AutoCAD, o se escriben fila por fila.
- Retiros frontal, posterior, lateral izquierdo y lateral derecho. En lotes de más de 4 lados, cada lado toma el retiro
  de su tipo.
- Datos de la ficha: IMO, IMC, número de pisos, lote mínimo y frente mínimo.
- Vista previa con el lote, los retiros y el área útil; clic en un lado para marcarlo como frente.
- Resumen en vivo: área del lote, de retiros y útil; comparación con IMO × lote e IMC × lote; avisos.
- Generar en Revit en el nivel elegido; al volver a generar, se reemplaza lo que generó el add-in y no se toca lo demás.
- Estilos de línea propios, con color y grosor configurables.

### Fuera de la versión 1

- Dibujo libre del lote (barra de selección en Revit y clics en el lienzo). Carlos lo pidió (min 30–32); va en la
  versión 2.
- Línea de propiedad nativa en Revit 2027 (ver decisión 1).
- 2.º piso, voladizos y balcones (min 7–8).
- Recordar en el modelo los datos ingresados.
- Mapa de sectores precargado (Carlos lo descartó por el trabajo que implica, min 46–47).
- El add-in de muros y espacios dentro del área útil (min 55–59).

## Decisiones

1. **Contorno del lote como línea de modelo en 2026** (2026-11-10, usuario y Claude). La opción elegida fue la línea de
   propiedad nativa para el lote, pero la API de Revit 2026 no tiene ningún miembro público en `PropertyLine`: ni
   `Create` ni `GetBoundary` (revisado en `RevitAPI.xml` 2026.0.4). La API 2027 sí trae `PropertyLine.Create`,
   `SetBoundary` y `Area` (revisado en `RevitAPI.xml` 2027.0.0). En la versión 1, el lote va como línea de modelo con el
   estilo «CQ Lote» en los dos años; la línea de propiedad de 2027 queda para la versión 2.
2. **Retiros y área útil como líneas de modelo con estilos CQ** (2026-11-10, usuario): «CQ Retiro frontal»,
   «CQ Retiro lateral», «CQ Retiro posterior» y «CQ Área útil», subcategorías de la categoría Líneas. Así cambian de
   color desde el add-in (pedido de Carlos, min 50 y 54) y el add-in futuro de muros encuentra el área útil por su
   estilo.
3. **Por fases** (2026-11-10, usuario): la versión 1 trae frente × fondo y puntos; el dibujo libre va en la versión 2.
4. **Tres columnas** (2026-11-10, usuario): datos a la izquierda, vista previa al centro, resumen y recomendaciones a la
   derecha. Es la distribución de `cotas-planta` y del tablero de zapatas.
5. **Mismo patrón que zapatas** (2026-11-10, Claude): cálculo sin Revit con pruebas en `calculo/`, ventana WebView2 sin
   XAML, `PuenteTablero` y `AccionesRevit` en `Tablero/`, y marca con Extensible Storage en `Revit/`.
6. **Desfase por lado** (2026-11-10, Claude): cada lado se corre en paralelo hacia adentro su retiro, y cada esquina del
   área útil es el corte de los dos lados vecinos ya corridos. Así cada lado tiene su propio retiro, que es lo que pide
   la norma. Si el resultado se cruza, se invierte o desaparece, se avisa y no se genera (no se intenta repararlo en la
   versión 1).
7. ~~**Clasificación de los lados** (2026-11-10, Claude): el usuario marca un lado como frente. El posterior es el lado
   cuya dirección es la más opuesta a la del frente, y el resto son laterales: izquierdo o derecho según el lado del
   frente en que quedan. Cualquier lado se puede cambiar a mano. En frente × fondo, el frente es el lado sobre el eje X.~~
   **Reemplazada** el 2026-11-11 por la decisión 19 del plan: cada lado se clasifica por hacia dónde mira.
8. **Coordenadas** (2026-11-10, Claude; **por medir en Revit**): frente × fondo se dibuja desde el origen del proyecto.
   Los puntos se interpretan en coordenadas compartidas (las del punto de reconocimiento, que son las de la topografía)
   y se pasan a coordenadas internas con `ActiveProjectLocation.GetTotalTransform()` (medido el 2026-11-11: lleva de compartidas a internas; ver hallazgo 9 del plan). La Z se guarda y se muestra, pero
   las líneas se dibujan en el plano del nivel elegido.
9. **Unidades** (2026-11-10, Claude): el usuario trabaja en metros. El cálculo trabaja en metros y solo la parte de
   Revit convierte a pies internos con `UnitUtils`.

## Datos de la ficha y avisos

| Dato | Ejemplo (ficha de Montería, min 47–49) | Uso |
|---|---|---|
| IMO (índice máximo de ocupación) | coeficiente | IMO × área del lote = ocupación máxima en 1.er piso |
| IMC (índice máximo de construcción) | coeficiente | IMC × área del lote = área construida máxima |
| Pisos | 2 | área útil × pisos se compara con IMC × lote |
| Lote mínimo | 96 m² | aviso si el lote es menor |
| Frente mínimo | 7 m | aviso si el frente es menor («si hay un frente de 6.5 no entra», min 48) |
| Retiros | frontal 1.5, posterior 2, lateral ~1 | líneas de retiro y área útil |

Avisos de la versión 1:

- El lote tiene menos de 3 puntos, sus lados se cruzan o tiene área cero.
- El frente o el lote están por debajo del mínimo de la ficha.
- Los retiros dejan el área útil cruzada, invertida o vacía.
- El área útil supera IMO × lote: la ocupación la limita el índice, no los retiros.
- El área construible en todos los pisos (área útil × pisos) supera IMC × lote.

Los datos de la ficha que no se llenen no generan avisos.

## Componentes

### `calculo/CQEING.AreaUtil.Calculo` (net8.0, sin Revit)

- `Poligono`: puntos en metros, área con signo (fórmula del área de Gauss), orientación, detección de lados que se
  cruzan.
- `LadosLote`: tipo de cada lado (frente, posterior, lateral izquierdo o derecho) a partir del frente elegido
  (decisión 7).
- `DesfaseRetiros`: área útil a partir del polígono, los tipos de los lados y los retiros (decisión 6).
- `Normativa`: resumen de áreas y avisos (tabla de arriba).
- `ProtocoloAreaUtil`: los mensajes JSON entre la página y C#: datos de entrada, resultado con los trazos y estilos de
  línea.

Proyecto de pruebas `CQEING.AreaUtil.Calculo.Tests`: rectángulo 7 × 14 (52.5 m²), lote en L, trapecio, polígono girado,
lado marcado como frente en cada posición, retiros que vacían el área útil, avisos de la ficha.

### `Revit/`

- `EstilosLinea`: crea o actualiza las subcategorías «CQ …» de `OST_Lines` con su color y grosor.
- `GeneradorLote`: en una transacción, borra las líneas marcadas del nivel y crea las nuevas: contorno, retiros y área
  útil, con su estilo y su marca.
- `MarcaAreaUtil`: Extensible Storage con esquema propio, como `MarcaZapata`.
- `Coordenadas`: paso de coordenadas compartidas a internas (decisión 8).
- La medición de la decisión 8 y de la creación de estilos y líneas se hace con el MCP de Revit (`send_code_to_revit`), sin comando de diagnóstico.

### `Tablero/`

- `ComandoTablero`, `VentanaTablero` (WebView2 sin XAML), `PuenteTablero` y `AccionesRevit`: el patrón de zapatas.
- `web/index.html` y `web/area-util.js`: las tres columnas. La vista previa se dibuja en SVG.

## Flujo

1. El usuario abre la ventana: el puente manda los niveles del modelo y los estilos actuales.
2. Cada cambio en la página manda un mensaje `calcular` a C# (con una espera corta para no mandar uno por tecla). C#
   calcula con `calculo/`, sin tocar Revit, y devuelve el resultado que dibuja la vista previa. La cuenta existe una sola
   vez y tiene pruebas. Es como lo hace `PuenteTablero.Calcular` en zapatas.
3. «Generar en Revit» manda los datos; C# recalcula, crea estilos y líneas en una transacción y devuelve lo que creó.
4. Si algo falla, la transacción se deshace entera, el error se muestra en la página y queda en
   `%AppData%\CQEING\area-util\errores.log`.

## Pruebas

- `dotnet test` del cálculo, con los casos de arriba.
- Medición en Revit 2026 con el MCP (`send_code_to_revit`) sobre una copia del modelo de prueba, antes de escribir
  `Revit/`: creación de estilos, líneas de modelo en un nivel, transformación de coordenadas compartidas.
- Lista de prueba manual en Revit 2026: el plan la detalla.
