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
    $('avisos').insertAdjacentHTML('afterbegin', `<div class="aviso bien">Listo: ${d.lineas} líneas en Revit, seleccionadas en la planta del nivel. Ctrl+Z las deshace. Esta ventana vuelve con el botón «Área útil del lote».</div>`);
  }
});

$('modoRect').addEventListener('click', () => cambiarModo('rectangulo'));
$('modoPuntos').addEventListener('click', () => cambiarModo('puntos'));
document.querySelectorAll('#datosRect input, .contenido > .fila input, #nivel, #coordenadas').forEach(el => el.addEventListener('input', calcular));
$('generar').addEventListener('click', () => enviar({ tipo: 'generar', ...entrada() }));
$('cerrar').addEventListener('click', () => enviar({ tipo: 'cerrar' }));

pintarPuntos();
enviar({ tipo: 'listo' });
