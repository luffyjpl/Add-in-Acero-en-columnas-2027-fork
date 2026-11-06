# Observaciones de la reunión del 05-11-2026 -- despiece y columnas

## Contexto

Reunión diaria del equipo de programadores (05-nov-2026, 82 min: Carlos
Quintero, Santiago Bula, Juan Felipe, Edgeryz Goez, Luciano). Transcripción
de Tactiq: `Reunión Eqpo Programadores (3).txt`.

Aquí solo se anota **lo que le toca a Luciano**: el add-in de **despiece**
(vigas y columnas) y el de **armado de columnas**. Quedan fuera: cotas de
planta y escalas de cuantía (Juan Felipe), viguetas por puntos (Edgeryz) y
zapatas (Juan Felipe / Santiago).

Luciano mostró el despiece de columnas (#226-#228) y los ajustes de vigas
(#260-#264). Las observaciones son de 07:36 a 36:13.

Cada punto está pensado para convertirse en un issue (sub-issue de #217 para
el despiece). Los marcados **a confirmar** tienen una duda que hay que
aclarar con Carlos o Santiago antes de programar.

## Regla general (vale para todo: vigas, columnas y los demás add-ins)

### 1. La escala manda; el texto no cambia de tamaño (28:23-35:49)

Carlos: *"a nivel normativo internacionalmente uno debe manejar una altura de
fuente estática y lo que se modifica es la escala, nunca lo contrario"*.

- Hoy el control "letra menor / mayor" cambia la altura del texto. Al
  probarlo en vivo, el dibujo quedó igual y el texto se achicó: **es al
  revés** de lo que tiene que pasar.
- Lo correcto: el texto mide siempre lo mismo impreso (ej. **2,5 mm**) y lo
  que cambia es la **escala de la vista** (1:15, 1:25, 1:50...). Santiago lo
  mostró en Revit poniendo la vista en una hoja y cambiando la escala.
- **No quitar** el control de fuente: dejarlo como opción por si alguien
  quiere, por ejemplo, títulos de 3,5 mm (35:35). Pero el control principal
  pasa a ser la escala.
- Aplica al despiece de vigas **y** de columnas (*"tanto aquí como en vigas,
  columnas, todo es igual"*).

### 2. Los textos van en negro (25:00, 49:25)

*"Los textos son en negro, todos"*. Las cotas tampoco van en color (*"las
cotas nunca se ponen con colores, eso es una ley"*). Las **líneas** de las
barras sí pueden ir en color, pero cuidando que se lean sobre blanco (nada
de amarillo claro ni verde biche). Hoy existe la opción "textos en negro".

**Decisión (05-11-2026, Luciano):** "textos en negro" pasa a ser el **valor
por defecto**; la opción de textos en color se mantiene por si acaso.

### 3. Cambiar el color del texto tarda mucho (25:28-25:44)

Carlos se quejó de lo que demora. Luciano explicó que es porque se vuelve a
generar la vista entera. Revisar si se puede cambiar solo el estilo del
texto sin regenerar.

## Despiece de columnas

### 4. Elegir qué niveles se muestran en las plantas (19:16-19:26)

*"Se selecciona cuáles son los que se van a mostrar en esta parte de plantas:
de las ocho, quiero mostrar cuatro"*. Hoy se muestran todos. Agregar una
selección de niveles (con "todos" disponible).

### 5. Texto pegado a la barra, a distancia constante (20:14-20:55)

Cada etiqueta (ej. "2 #6 L=...") hoy queda a una distancia distinta de su
barra. Tiene que quedar **automáticamente pegada a la barra**, con un espacio
pequeño y **siempre igual**. Mover a mano está bien que siga existiendo, pero
no tiene que hacer falta.

### 6. Ordenar las barras por hacia dónde mira el gancho, y compactar (21:38-26:12)

- Los ganchos de las barras sacadas se miran **uno al otro, en espejo**. Las
  verdes de abajo estaban bien hechas y son el ejemplo.
- Ordenar: primero, **juntas una al lado de la otra, todas las que tienen el
  gancho a la derecha** (mismo punto de traslapo); después un espacio chico;
  después todas las del gancho a la izquierda. Así quien corta ve de un
  vistazo cuántas barras son de cada largo.
- La meta: que el espacio a la derecha del acero sea **lo más angosto
  posible**.

### 7. Siempre se dibuja el gancho, también en las barras que miran hacia la vista (16:21-17:48)

Las barras cuyo gancho apunta hacia el que mira (o hacia atrás) hoy salen
**sin gancho** y parecen barras rectas. Santiago: *"siempre se muestran los
ganchos, porque si no son barras longitudinales sin gancho y así las
construye"*. Mostrarlas con el gancho girado al costado (*"el gancho hacia
la derecha"*).

### 8. Ganchos de las primeras barras: uno para adentro y otro para afuera (15:33-16:06) -- a confirmar

Santiago vio en el despiece que las primeras barras tienen los ganchos en
direcciones distintas, *"lo cual no pasa: los ganchos siempre van en la
misma dirección"*. Preguntó si el acero lo había generado el add-in de
columnas (sí). **Hay que averiguar** si el error está en el acero generado
(add-in de columnas) o solo en cómo lo dibuja el despiece, midiendo en el
modelo.

### 9. Sección vista desde abajo (10:24-11:03, 18:04)

Hoy la sección muestra la cara de arriba, donde ya están los traslapos, y por
eso la barra de la esquina aparece corrida. Santiago: *"creería que desde
abajo"*. Cambiar la sección a vista desde abajo.

### 10. Sección más simple: colores y leyendas (30:11-30:56)

*"La idea no es empachar los planos, sino ser muy simples al mostrar las
cosas"*. El esquema de la sección con tantas diferencias no entra en esa
filosofía: diferenciar con **colores y una leyenda**, más práctico de leer.

### 11. Orden de las cotas: primero las parciales, después la total (18:18-18:58)

*"Las primeras que van siempre son las pequeñas y después va la total"*
(ej. 0,04 / 0,22 / 0,04 y después el total).

### 12. Separaciones de estribos mal (pendiente que ya tenía Luciano) (08:54, 14:45)

Algunas separaciones no coinciden: donde va 0,14 termina poniendo 0,30.
Revisar junto con #256 (estribos a 0,13 donde van a 0,14) por si es la misma
causa.

### 13. Largos de barra medidos por afuera (pendiente que ya tenía Luciano) (08:54)

Algunos largos salen distintos a Revit (ej. 3,05 donde Revit da 3,00): se
está tomando la medida por afuera de la barra y no la de la barra. Revisar
junto con #245.

## Despiece general (vigas y columnas)

### 14. Siguen apareciendo CAD y secciones en las plantas (26:56-27:58)

Pese a #263 (cerrado), en el modelo de Santiago **siguen apareciendo** el CAD
y también las **líneas de sección**. Reabrir #263 o abrir un bug nuevo.

### 15. Mostrar solo vigas, viguetas, columnas y losa (16:06, 55:08)

Si el modelo tiene muros, ventanas, etc., la imagen se ensucia. Que las
vistas muestren **solo** vigas, viguetas, columnas y losa (y zapatas donde
corresponda) y nada más.

## Estado

- [x] Lista revisada con Luciano (05-11): van todos como issue.
- [x] Punto 2 decidido: negro por defecto, color como opción.
- [ ] Punto 8 sigue **a confirmar** (¿armado de columnas o despiece?).
- [x] Issues creados: padre **#287** (sub-issue de #217). El punto N es el
  issue #271+N (punto 1 = #272 ... punto 15 = #286).
