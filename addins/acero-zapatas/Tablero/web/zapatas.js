// Tablero de zapatas (#252). Todo el cálculo lo hace C# (MotorParrillas); aquí solo se dibuja y se manda lo elegido.
'use strict';

const $ = id => document.getElementById(id);
const NS = 'http://www.w3.org/2000/svg';
const esc = t => String(t ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const m = (mm, dec = 2) => (mm / 1000).toFixed(dec);

const estado = {
  modelo: '', zapatas: [], grupos: [], catalogo: { barras: [], formas: [], ganchos: [] }, porDefecto: 75,
  elegidas: [], activa: null, res: null, modo: 'transparente', cam: { yaw: -0.75, pitch: 0.62, zoom: 1 },
  cfg: null, verChoques: true,
};

// Por dirección: la parrilla inferior y, en «sup», la superior, cada una con su forma y su gancho (decisión 30 del plan de
// 2026-11-05). largoPata: «Cuadro» o «Recubrimiento» (decisión 29).
const extremos = () => ({ terminacion: 'Pata90', forma: null, gancho: null });
const direccion = () => ({ numero: 5, separacion: 150, ...extremos(), sup: extremos() });
const cfgInicial = () => ({ x: direccion(), y: direccion(), inferior: 75, lateral: 75, superior: 75, incluirSuperior: true, largoPata: 'Cuadro' });
estado.cfg = cfgInicial();

/** Lo guardado con una versión anterior de la página no trae la superior ni el largo de la pata: se completa. */
function completar(guardada) {
  const base = cfgInicial();
  const dir = (b, g) => ({ ...b, ...g, sup: { ...b.sup, ...(g?.sup ?? {}) } });
  return { ...base, ...guardada, x: dir(base.x, guardada.x), y: dir(base.y, guardada.y) };
}

function enviar(mensaje) {
  if (window.chrome?.webview) window.chrome.webview.postMessage(mensaje);
}

const leerGuardado = clave => {
  try {
    return JSON.parse(localStorage.getItem(clave));
  } catch {
    return null;
  }
};
const claveProyecto = () => `zapatas:${estado.modelo}`;
function guardar() {
  try {
    localStorage.setItem(claveProyecto(), JSON.stringify(estado.cfg));
  } catch { /* Sin almacenamiento solo se pierde recordar la última elección. */ }
}

const zapata = id => estado.zapatas.find(z => z.id === id);
const activa = () => zapata(estado.activa);
const grupoDe = id => estado.grupos.find(g => g.includes(id)) ?? [id];

window.chrome?.webview?.addEventListener('message', e => {
  const d = e.data;
  if (d.tipo === 'datos') recibirDatos(d);
  else if (d.tipo === 'resultado') recibirResultado(d);
  else if (d.tipo === 'seleccion') elegir(d.ids);
  else if (d.tipo === 'aviso') aviso(d.texto, d.bien ? 'bien' : d.texto.startsWith('No ') ? 'mal' : '');
  else if (d.tipo === 'choques') preguntarChoques(d.cantidad);
});

function recibirDatos(d) {
  // Otro modelo (decisión 33): lo elegido y el resultado eran del anterior.
  if (d.modelo !== estado.modelo) Object.assign(estado, { elegidas: [], activa: null, res: null });
  const anteriores = estado.elegidas;
  estado.modelo = d.modelo;
  estado.zapatas = d.zapatas;
  estado.grupos = d.grupos;
  estado.catalogo = d.catalogo;
  estado.porDefecto = d.recubrimientoPorDefecto;
  recibirReglas(d.reglas);
  const guardada = leerGuardado(claveProyecto());
  if (guardada?.x && guardada?.y && anteriores.length === 0) estado.cfg = completar(guardada);
  llenarSelects();
  const siguen = anteriores.filter(zapata);
  if (siguen.length) elegir(siguen);
  else if (d.zapatas.length) {
    const z = d.zapatas[0];
    if (!guardada) {
      estado.cfg.inferior = z.recubrimientoInferior ?? estado.porDefecto;
      estado.cfg.lateral = z.recubrimientoLateral ?? estado.porDefecto;
      estado.cfg.superior = z.recubrimientoSuperior ?? estado.porDefecto;
    }
    elegir([z.id]);
  } else {
    dibujarTodo();
  }
}

function llenarSelects() {
  $('sel').innerHTML = estado.zapatas.map(z => `<option value="${esc(z.id)}">${esc(z.nombre)}${z.eje ? ` · Eje ${esc(z.eje)}` : ''}</option>`).join('');
  const opciones = estado.catalogo.barras.map(b => `<option value="${b.numero}">#${b.numero} · ${esc(b.nombre)}</option>`).join('');
  $('numX').innerHTML = opciones;
  $('numY').innerHTML = opciones;
}

function elegir(ids) {
  const validas = ids.filter(zapata);
  if (!validas.length) return;
  estado.elegidas = validas;
  estado.activa = validas.includes(estado.activa) ? estado.activa : validas[0];
  calcular();
  dibujarTodo();
}

// La configuración y lo elegido en el catálogo, lo mismo para calcular y para aplicar. La forma y el gancho de la superior
// van como '' cuando son los automáticos: null querría decir «los de la inferior» (TiposElegidos en C#).
function configMensaje() {
  const c = estado.cfg;
  const dir = d => ({ numero: d.numero, separacion: d.separacion, terminacion: d.terminacion, superior: { terminacion: d.sup.terminacion } });
  return {
    x: dir(c.x), y: dir(c.y), inferior: c.inferior, lateral: c.lateral, superior: c.superior, incluirSuperior: c.incluirSuperior, largoPata: c.largoPata,
    formaX: c.x.forma, formaY: c.y.forma, ganchoX: c.x.gancho, ganchoY: c.y.gancho,
    formaSuperiorX: c.x.sup.forma ?? '', formaSuperiorY: c.y.sup.forma ?? '', ganchoSuperiorX: c.x.sup.gancho ?? '', ganchoSuperiorY: c.y.sup.gancho ?? '',
  };
}

function calcular() {
  if (estado.elegidas.length) enviar({ tipo: 'calcular', ids: estado.elegidas, ...configMensaje() });
}

function recibirResultado(d) {
  if (d.ids.join() !== estado.elegidas.join()) return;
  estado.res = d;
  dibujarRefuerzo();
  dibujar3d();
  dibujar2d();
}

function dibujarTodo() {
  const z = activa();
  if (z) $('sel').value = z.id;
  dibujarPlanta();
  dibujarDetalles();
  dibujarRefuerzo();
  dibujar3d();
  dibujar2d();
  const n = estado.elegidas.length;
  $('txtEstado').textContent = `Conectado a Revit · Modelo: ${estado.modelo} · ${estado.zapatas.length} zapatas · ${n} ${n === 1 ? 'zapata seleccionada' : 'zapatas seleccionadas'}`;
  const iguales = z ? grupoDe(z.id).length : 0;
  $('bIguales').textContent = `Elegir iguales${iguales ? ` (${iguales})` : ''}`;
  $('bIguales').disabled = iguales < 2;
}

// ---- Planta

function dibujarPlanta() {
  const puntos = estado.zapatas.flatMap(z => z.enModelo);
  if (!puntos.length) {
    $('planta').innerHTML = '';
    return;
  }
  const xs = puntos.map(p => p.x), ys = puntos.map(p => p.y);
  const x0 = Math.min(...xs), x1 = Math.max(...xs), y0 = Math.min(...ys), y1 = Math.max(...ys);
  const borde = Math.max(x1 - x0, y1 - y0, 1000) * 0.06;
  const vista = `${x0 - borde} ${-y1 - borde} ${x1 - x0 + 2 * borde} ${y1 - y0 + 2 * borde}`;
  const texto = Math.max(x1 - x0, y1 - y0, 1000) * 0.028;
  const figuras = estado.zapatas.map(z => {
    const on = estado.elegidas.includes(z.id), act = z.id === estado.activa;
    const pts = z.enModelo.map(p => `${p.x},${-p.y}`).join(' ');
    const cx = z.enModelo.reduce((s, p) => s + p.x, 0) / z.enModelo.length, cy = z.enModelo.reduce((s, p) => s + p.y, 0) / z.enModelo.length;
    // Con el eje, como en la lista de arriba: «Z-12 · Eje 9-A» (Juan, 05-nov).
    const nombre = `${z.nombre}${z.eje ? ` · Eje ${z.eje}` : ''}`;
    return `<g data-id="${esc(z.id)}" style="cursor:pointer"><polygon points="${pts}" fill="${on ? '#D3E2F4' : '#E2E5E9'}" stroke="${on ? '#1F5FAD' : '#5B6672'}" stroke-width="${act ? 3 : 1.4}" vector-effect="non-scaling-stroke"><title>${esc(nombre)}</title></polygon>
      <text x="${cx}" y="${-cy}" font-size="${texto}" text-anchor="middle" dominant-baseline="middle" fill="${on ? '#174A87' : '#3E4751'}" style="pointer-events:none">${esc(nombre)}</text></g>`;
  }).join('');
  $('planta').innerHTML = `<svg viewBox="${vista}" preserveAspectRatio="xMidYMid meet" role="img" aria-label="Planta de cimentación">${figuras}</svg>`;
  $('planta').querySelectorAll('g[data-id]').forEach(g => g.addEventListener('click', e => {
    const id = g.dataset.id;
    if (e.ctrlKey) {
      const lista = estado.elegidas.includes(id) ? estado.elegidas.filter(x => x !== id) : [...estado.elegidas, id];
      if (lista.length) {
        estado.activa = id;
        elegir(lista);
      }
    } else {
      estado.activa = id;
      elegir([id]);
    }
  }));
  const z = activa();
  $('cota').textContent = z ? `${(z.zBase / 1000).toFixed(2)} m` : '';
}

function dibujarDetalles() {
  const z = activa();
  if (!z) {
    $('detalles').innerHTML = '';
    return;
  }
  const tipo = `${z.tipo === 'Combinada' ? 'Combinada' : 'Individual'} ${{ Cuadrada: 'cuadrada', Rectangular: 'rectangular', Atipica: 'atípica' }[z.forma]}`;
  const columnas = z.columnas.length ? z.columnas.map(c => `${m(c.ancho)} × ${m(c.alto)}`).join(', ') : 'sin columna';
  const filas = [['Identificador', z.nombre], ['Tipo', tipo], ['Dimensiones', `${m(z.ancho)} × ${m(z.largo)} m`], ['Altura (h)', `${m(z.altura)} m`], ['Columna', columnas],
    ['Nivel de desplante', `${m(z.zBase)} m`], ['Eje / Nivel Revit', `${z.eje || '–'} · ${z.nivel || '–'}`], ['Acero existente', z.barrasTotales ? `${z.barrasTotales} (${z.barrasPropias} del add-in)` : 'ninguno']];
  $('detalles').innerHTML = filas.map(([a, b]) => `<dt>${esc(a)}</dt><dd>${esc(b)}</dd>`).join('');
}

// ---- Refuerzo

function nombreForma(d) {
  return d.forma ? estado.catalogo.formas.find(f => f.id === d.forma)?.nombre ?? 'Automática' : 'Automática';
}

function nombreGancho(d) {
  if (d.terminacion === 'Recta') return 'Sin gancho';
  if (d.terminacion === 'Pata90') return '90° · pata';
  return `${d.terminacion === 'Gancho135' ? '135°' : '180°'} · ${estado.catalogo.ganchos.find(g => g.id === d.gancho)?.nombre ?? ''}`;
}

function dibujarRefuerzo() {
  const c = estado.cfg, r = estado.res;
  $('numX').value = c.x.numero;
  $('numY').value = c.y.numero;
  $('sepX').value = m(c.x.separacion, 3);
  $('sepY').value = m(c.y.separacion, 3);
  $('recG').value = c.inferior === c.lateral && c.lateral === c.superior ? m(c.inferior, 3) : '';
  $('recG').placeholder = 'por cara';
  $('recI').value = m(c.inferior, 3);
  $('recL').value = m(c.lateral, 3);
  $('recS').value = m(c.superior, 3);
  $('sup').checked = c.incluirSuperior;
  $('forX').textContent = nombreForma(c.x);
  $('forY').textContent = nombreForma(c.y);
  $('ganX').textContent = nombreGancho(c.x);
  $('ganY').textContent = nombreGancho(c.y);
  $('forSX').textContent = nombreForma(c.x.sup);
  $('forSY').textContent = nombreForma(c.y.sup);
  $('ganSX').textContent = nombreGancho(c.x.sup);
  $('ganSY').textContent = nombreGancho(c.y.sup);
  $('tablaSup').hidden = !c.incluirSuperior;
  $('largoPata').querySelectorAll('button').forEach(b => b.setAttribute('aria-pressed', b.dataset.largo === c.largoPata));
  for (const eje of ['X', 'Y']) {
    const inferiores = (r?.conjuntos ?? []).filter(b => b.parrilla === 'Inferior' && b.direccion === eje);
    $(`can${eje}`).textContent = inferiores.length ? inferiores.reduce((s, b) => s + b.posiciones.length, 0) : '–';
    $(`pat${eje}`).textContent = inferiores.length && inferiores[0].pata ? m(inferiores[0].pata) : '–';
  }
  const hay = r && !r.errores.length;
  const largos = hay ? [...new Set(r.conjuntos.map(b => b.longitudCorte))].sort((a, b) => a - b) : [];
  const n = Math.max(estado.elegidas.length, 1);
  $('rLon').textContent = !hay ? '–' : largos.length === 1 ? `${m(largos[0])} m` : `${m(largos[0])} a ${m(largos[largos.length - 1])} m`;
  $('rBar').textContent = hay ? (n > 1 ? `${r.resumen.totalBarras * n} (${n} × ${r.resumen.totalBarras})` : r.resumen.totalBarras) : '–';
  $('rTot').textContent = hay ? `${(r.resumen.longitudTotal * n / 1000).toFixed(2)} m` : '–';
  $('rKg').textContent = hay ? `${(r.resumen.kilos * n).toFixed(1)} kg` : '–';
  const avisos = [...(r?.errores ?? []).map(t => [t, 'mal']), ...(r?.avisos ?? []).map(t => [t, ''])];
  const manuales = estado.elegidas.map(zapata).filter(z => z && z.barrasTotales > z.barrasPropias);
  if (manuales.length) avisos.push([`${manuales.map(z => z.nombre).join(', ')}: ya ${manuales.length === 1 ? 'tiene' : 'tienen'} barras hechas a mano, que no se tocan. Revise que no queden duplicadas.`, '']);
  $('avisos').innerHTML = avisos.map(([t, c]) => `<div class="${c}">${esc(t)}</div>`).join('');
  $('bAplicar').disabled = !hay;
  $('bAplicar').textContent = n > 1 ? `Aplicar refuerzo a ${n} zapatas` : 'Aplicar refuerzo en Revit';
}

function cambio() {
  guardar();
  dibujarRefuerzo();
  calcular();
}

const numeroMm = (id, actual) => {
  const v = parseFloat($(id).value);
  return Number.isFinite(v) && v >= 0 ? Math.round(v * 10000) / 10 : actual;
};

$('numX').addEventListener('change', () => { estado.cfg.x.numero = +$('numX').value; cambio(); });
$('numY').addEventListener('change', () => { estado.cfg.y.numero = +$('numY').value; cambio(); });
$('sepX').addEventListener('change', () => { estado.cfg.x.separacion = numeroMm('sepX', estado.cfg.x.separacion); cambio(); });
$('sepY').addEventListener('change', () => { estado.cfg.y.separacion = numeroMm('sepY', estado.cfg.y.separacion); cambio(); });
$('sup').addEventListener('change', () => { estado.cfg.incluirSuperior = $('sup').checked; cambio(); });
$('recG').addEventListener('change', () => {
  const v = numeroMm('recG', null);
  if (v !== null) Object.assign(estado.cfg, { inferior: v, lateral: v, superior: v });
  cambio();
});
$('recI').addEventListener('change', () => { estado.cfg.inferior = numeroMm('recI', estado.cfg.inferior); cambio(); });
$('recL').addEventListener('change', () => { estado.cfg.lateral = numeroMm('recL', estado.cfg.lateral); cambio(); });
$('recS').addEventListener('change', () => { estado.cfg.superior = numeroMm('recS', estado.cfg.superior); cambio(); });
$('largoPata').addEventListener('click', e => {
  const largo = e.target.dataset?.largo;
  if (!largo) return;
  estado.cfg.largoPata = largo;
  cambio();
});
$('bOpciones').addEventListener('click', () => { $('mOpciones').hidden = false; });
document.querySelectorAll('[data-cierra]').forEach(b => b.addEventListener('click', () => { b.closest('.modal').hidden = true; }));

// ---- Catálogo (#250)

let catalogoAbierto = null;

function dibujoForma(f) {
  const pts = f.curvas.flat();
  if (!pts.length) return '';
  const xs = pts.map(p => p[0]), ys = pts.map(p => p[1]);
  const x0 = Math.min(...xs), x1 = Math.max(...xs), y0 = Math.min(...ys), y1 = Math.max(...ys);
  const borde = Math.max(x1 - x0, y1 - y0, 1) * 0.08;
  const trazos = f.curvas.map(c => `<polyline points="${c.map(p => `${p[0]},${-p[1]}`).join(' ')}" fill="none" stroke="#1F5FAD" stroke-width="2.4" stroke-linejoin="round" stroke-linecap="round" vector-effect="non-scaling-stroke"/>`).join('');
  return `<svg viewBox="${x0 - borde} ${-y1 - borde} ${x1 - x0 + 2 * borde} ${y1 - y0 + 2 * borde}" preserveAspectRatio="xMidYMid meet" aria-hidden="true">${trazos}</svg>`;
}

function dibujoGancho(grados) {
  const puntas = { 0: '10,30 80,30', 90: '10,44 66,44 Q 76,44 76,34 L 76,8', 135: '10,44 66,44 Q 78,44 72,32 L 50,10', 180: '10,44 66,44 Q 82,44 82,30 Q 82,16 66,16 L 44,16' };
  const trazo = grados === 0 ? `<polyline points="${puntas[0]}" fill="none" stroke="#1F5FAD" stroke-width="3"/>` : `<path d="M ${puntas[grados]}" fill="none" stroke="#1F5FAD" stroke-width="3" stroke-linecap="round"/>`;
  return `<svg viewBox="0 0 90 54" aria-hidden="true">${trazo}</svg>`;
}

// Las formas que se ofrecen (decisión 37 del plan de 2026-11-05): con la pata de 90°, recta, L o U, y la forma decide las
// patas; con 135°, 180° o sin gancho, solo las rectas, porque el gancho va en las dos puntas.
const CLASES = { Recta: 'recta, sin patas', L: 'L, pata al inicio', U: 'U, dos patas' };
const formasPara = d => estado.catalogo.formas.filter(f => d.terminacion === 'Pata90' || f.clase === 'Recta');

function abrirCatalogo(tipo, eje, parrilla) {
  catalogoAbierto = { tipo, eje, parrilla };
  dibujarCatalogo();
  $('mCatalogo').hidden = false;
}

function dibujarCatalogo() {
  const { tipo, eje, parrilla } = catalogoAbierto;
  const d = parrilla === 'sup' ? estado.cfg[eje].sup : estado.cfg[eje];
  const numero = estado.cfg[eje].numero;
  const barra = estado.catalogo.barras.find(b => b.numero === numero);
  const donde = `Parrilla ${parrilla === 'sup' ? 'superior' : 'inferior'} · Dirección ${eje.toUpperCase()}`;
  let fichas;
  if (tipo === 'forma') {
    $('tCat').textContent = `Forma de barra · ${donde}`;
    $('notaCat').textContent = d.terminacion === 'Pata90'
      ? 'Con la pata de 90°, la forma decide dónde va la pata: recta sin patas, L con una pata al inicio o U con dos.'
      : 'Con ganchos de 135° o 180°, o sin gancho, la barra es recta: solo se muestran las formas rectas.';
    fichas = [`<button class="ficha" data-valor="" aria-pressed="${!d.forma}">${dibujoGancho(90)}<span>Automática</span><small>la que Revit reconozca</small></button>`,
      ...formasPara(d).map(f => `<button class="ficha" data-valor="${esc(f.id)}" aria-pressed="${d.forma === f.id}">${dibujoForma(f)}<span>${esc(f.nombre)}</span><small>${CLASES[f.clase]}</small></button>`)];
  } else {
    $('tCat').textContent = `Gancho · ${donde} · barra #${numero}`;
    $('notaCat').textContent = '';
    const del = g => (!barra ? '' : `Cuadro: ${m({ 90: barra.gancho90, 135: barra.gancho135, 180: barra.gancho180 }[g], 3)} m`);
    fichas = [`<button class="ficha" data-valor="pata" aria-pressed="${d.terminacion === 'Pata90'}">${dibujoGancho(90)}<span>90° · pata</span><small>largo: botón «Largo de la pata»</small><small>${del(90)}</small></button>`,
      `<button class="ficha" data-valor="recta" aria-pressed="${d.terminacion === 'Recta'}">${dibujoGancho(0)}<span>Sin gancho</span></button>`,
      ...estado.catalogo.ganchos.filter(g => g.grados === 135 || g.grados === 180).map(g => `<button class="ficha" data-valor="${esc(g.id)}" aria-pressed="${d.gancho === g.id}">${dibujoGancho(g.grados)}<span>${esc(g.nombre)}</span><small>${g.grados}° · ${g.extension.toFixed(1)} d</small><small>${del(g.grados)}</small></button>`)];
  }
  $('rejilla').innerHTML = fichas.join('');
  $('rejilla').querySelectorAll('.ficha').forEach(b => b.addEventListener('click', () => {
    const v = b.dataset.valor;
    if (tipo === 'forma') {
      d.forma = v || null;
    } else if (v === 'pata') {
      Object.assign(d, { terminacion: 'Pata90', gancho: null });
    } else if (v === 'recta') {
      Object.assign(d, { terminacion: 'Recta', gancho: null });
    } else {
      const g = estado.catalogo.ganchos.find(x => x.id === v);
      Object.assign(d, { terminacion: g.grados === 135 ? 'Gancho135' : 'Gancho180', gancho: v });
    }
    // Una L o una U no sirve sin la pata de 90°: vuelve a la automática.
    if (d.forma && !formasPara(d).some(f => f.id === d.forma)) d.forma = null;
    $('mCatalogo').hidden = true;
    cambio();
  }));
}

for (const [boton, tipo, eje, parrilla] of [['forX', 'forma', 'x', 'inf'], ['forY', 'forma', 'y', 'inf'], ['ganX', 'gancho', 'x', 'inf'], ['ganY', 'gancho', 'y', 'inf'],
  ['forSX', 'forma', 'x', 'sup'], ['forSY', 'forma', 'y', 'sup'], ['ganSX', 'gancho', 'x', 'sup'], ['ganSY', 'gancho', 'y', 'sup']])
  $(boton).addEventListener('click', () => abrirCatalogo(tipo, eje, parrilla));

// ---- Vista 3D

// Las barras y los choques vienen hechos de C# (V1 a V4 del plan de 2026-11-05): cada trazo trae sus puntos [x, y, z] en
// los ejes de la zapata y el color de su diámetro (#154). Aquí solo se proyectan.
const resultadoBueno = () => (estado.res && !estado.res.errores.length ? estado.res : null);
const conjuntoDe = t => estado.res.conjuntos[+t.pieza.split('.')[0]];
const colores = () => new Map((resultadoBueno()?.trazos ?? []).map(t => [t.numero, t.color]));

function dibujarLeyenda() {
  $('diametros').innerHTML = [...colores()].sort((a, b) => a[0] - b[0]).map(([n, c]) => `<span><i style="background:${esc(c)}"></i>#${n}</span>`).join('');
}

function dibujar3d() {
  const svg = $('svg3d'), z = activa();
  svg.innerHTML = '';
  $('titulo3d').textContent = z ? `Vista 3D · ${z.nombre}` : 'Vista 3D';
  if (!z) return;
  const { width: w, height: h } = svg.getBoundingClientRect();
  if (!w || !h) return;
  svg.setAttribute('viewBox', `0 0 ${w} ${h}`);
  const xs = z.contorno.map(p => p.x), ys = z.contorno.map(p => p.y);
  const cx = (Math.min(...xs) + Math.max(...xs)) / 2, cy = (Math.min(...ys) + Math.max(...ys)) / 2;
  const alturaCol = Math.max(z.altura, 500);
  const radio = Math.hypot(Math.max(...xs) - Math.min(...xs), Math.max(...ys) - Math.min(...ys), z.altura + alturaCol) / 2 || 1;
  const escala = Math.min(w, h) / (2.3 * radio) * estado.cam.zoom;
  const { yaw, pitch } = estado.cam;
  const [sy, cyw, sp, cp] = [Math.sin(yaw), Math.cos(yaw), Math.sin(pitch), Math.cos(pitch)];
  const P = (x, y, a) => {
    const dx = x - cx, dy = y - cy, dz = a - z.altura / 2;
    const rx = dx * cyw - dy * sy, ry = dx * sy + dy * cyw;
    return { x: w / 2 + rx * escala, y: h / 2 + (ry * sp - dz * cp) * escala, d: ry * cp + dz * sp };
  };
  const tr = pts => pts.map(p => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' ');
  const opacidad = { solido: 1, transparente: 0.32, alambre: 0 }[estado.modo];
  let html = '';
  const cara = (pts, color, profundidad) => ({ d: profundidad, s: `<polygon points="${tr(pts)}" fill="${color}" fill-opacity="${opacidad}" stroke="#3E4751" stroke-width="1.2" stroke-linejoin="round"/>` });
  const caras = [];
  const n = z.contorno.length;
  for (let i = 0; i < n; i++) {
    const a = z.contorno[i], b = z.contorno[(i + 1) % n];
    const pts = [P(a.x, a.y, 0), P(b.x, b.y, 0), P(b.x, b.y, z.altura), P(a.x, a.y, z.altura)];
    caras.push(cara(pts, i % 2 ? '#AEB5BD' : '#C1C7CE', pts.reduce((s, p) => s + p.d, 0) / 4));
  }
  caras.sort((u, v) => u.d - v.d);
  const fondo = z.contorno.map(p => P(p.x, p.y, 0)), tapa = z.contorno.map(p => P(p.x, p.y, z.altura));
  html += cara(fondo, '#C1C7CE', 0).s;
  // Las caras traseras van antes del acero y las delanteras después, para que el modo sólido tape el acero.
  const mitad = Math.ceil(caras.length / 2);
  html += caras.slice(0, mitad).map(c => c.s).join('');
  const r = resultadoBueno();
  for (const t of r?.trazos ?? [])
    html += `<polyline points="${tr(t.puntos.map(p => P(p[0], p[1], p[2])))}" fill="none" stroke="${esc(t.color)}" stroke-width="1.8" stroke-linejoin="round"/>`;
  html += caras.slice(mitad).map(c => c.s).join('');
  html += `<polygon points="${tr(tapa)}" fill="#D5D9DE" fill-opacity="${opacidad}" stroke="#3E4751" stroke-width="1.2" stroke-linejoin="round"/>`;
  for (const c of z.columnas) {
    const x0 = c.centro.x - c.ancho / 2, x1 = c.centro.x + c.ancho / 2, y0 = c.centro.y - c.alto / 2, y1 = c.centro.y + c.alto / 2;
    const base = [[x0, y0], [x1, y0], [x1, y1], [x0, y1]];
    const op = Math.max(opacidad, 0.25);
    base.forEach((a, i) => {
      const b = base[(i + 1) % 4];
      html += `<polygon points="${tr([P(a[0], a[1], z.altura), P(b[0], b[1], z.altura), P(b[0], b[1], z.altura + alturaCol), P(a[0], a[1], z.altura + alturaCol)])}" fill="#9AA3AD" fill-opacity="${op}" stroke="#3E4751"/>`;
    });
    html += `<polygon points="${tr(base.map(a => P(a[0], a[1], z.altura + alturaCol)))}" fill="#E2E5E9" fill-opacity="${op}" stroke="#3E4751"/>`;
  }
  // Los choques encima de todo, como los puntos rojos del alzado de vigas; al pasar el mouse, su texto abajo (V2).
  if (estado.verChoques)
    (r?.choques ?? []).forEach((c, i) => {
      const p = P(c.punto[0], c.punto[1], c.punto[2]);
      html += `<circle cx="${p.x.toFixed(1)}" cy="${p.y.toFixed(1)}" r="5" fill="#E53935" stroke="#7A0E0E" stroke-width="1.2" data-choque="${i}" style="cursor:help"><title>${esc(c.texto)}</title></circle>`;
    });
  svg.innerHTML = html;
  dibujarLeyenda();
}

$('svg3d').addEventListener('pointerover', e => {
  const i = e.target.dataset?.choque;
  const c = i === undefined ? null : resultadoBueno()?.choques?.[+i];
  $('textoChoque').hidden = !c;
  $('textoChoque').textContent = c ? c.texto : '';
});
$('capaChoques').addEventListener('change', () => {
  estado.verChoques = $('capaChoques').checked;
  $('textoChoque').hidden = true;
  dibujar3d();
});

let arrastre = null;
const vistaEl = $('vista');
vistaEl.addEventListener('pointerdown', e => {
  if (e.target.closest('#emergente')) return;
  arrastre = { x: e.clientX, y: e.clientY, yaw: estado.cam.yaw, pitch: estado.cam.pitch };
  vistaEl.setPointerCapture(e.pointerId);
});
vistaEl.addEventListener('pointermove', e => {
  if (!arrastre) return;
  estado.cam.yaw = arrastre.yaw + (e.clientX - arrastre.x) * 0.008;
  estado.cam.pitch = Math.max(0.02, Math.min(Math.PI / 2, arrastre.pitch + (e.clientY - arrastre.y) * 0.008));
  dibujar3d();
});
vistaEl.addEventListener('pointerup', () => { arrastre = null; });
vistaEl.addEventListener('pointercancel', () => { arrastre = null; });
vistaEl.addEventListener('wheel', e => {
  if (e.target.closest('#emergente')) return;
  e.preventDefault();
  estado.cam.zoom = Math.max(0.3, Math.min(6, estado.cam.zoom * (e.deltaY < 0 ? 1.12 : 0.89)));
  dibujar3d();
}, { passive: false });
new ResizeObserver(() => dibujar3d()).observe(vistaEl);
$('modos').addEventListener('click', e => {
  const modo = e.target.dataset?.modo;
  if (!modo) return;
  estado.modo = modo;
  $('modos').querySelectorAll('button').forEach(b => b.setAttribute('aria-pressed', b.dataset.modo === modo));
  dibujar3d();
});
$('bArriba').addEventListener('click', () => { Object.assign(estado.cam, { yaw: 0, pitch: Math.PI / 2, zoom: 1 }); dibujar3d(); });
$('bIso').addEventListener('click', () => { Object.assign(estado.cam, { yaw: -0.75, pitch: 0.62, zoom: 1 }); dibujar3d(); });

// ---- Sección 2D

function dibujar2d() {
  const z = activa(), r = estado.res;
  if (!z) {
    $('sec2d').innerHTML = '';
    return;
  }
  // Corte en X por el centro: las barras en X se ven enteras y las de Y, como puntos.
  const xs = z.contorno.map(p => p.x), ys = z.contorno.map(p => p.y);
  const x0 = Math.min(...xs), x1 = Math.max(...xs), yc = (Math.min(...ys) + Math.max(...ys)) / 2;
  const W = 320, H = 190, margen = 34;
  const esc2 = Math.min((W - 2 * margen) / (x1 - x0), (H - 2 * margen - 18) / z.altura);
  const X = x => margen + (x - x0) * esc2 + (W - 2 * margen - (x1 - x0) * esc2) / 2;
  const base = H - margen;
  const Y = a => base - a * esc2;
  let s = `<rect x="${X(x0)}" y="${Y(z.altura)}" width="${(x1 - x0) * esc2}" height="${z.altura * esc2}" fill="#E2E5E9" stroke="#3E4751" stroke-width="1.3"/>`;
  const bueno = resultadoBueno(), conjuntos = bueno ? bueno.conjuntos : [], color = colores();
  let etiqueta = '';
  // Las barras en X, con el trazo de C# de la barra más cercana al corte (su forma y su gancho reales).
  conjuntos.forEach((b, k) => {
    if (b.direccion !== 'X') return;
    const i = b.posiciones.reduce((u, v, j) => (Math.abs(v - yc) < Math.abs(b.posiciones[u] - yc) ? j : u), 0);
    if (Math.abs(b.posiciones[i] - yc) > (b.separacion || 1e9)) return;
    const t = bueno.trazos.find(x => x.pieza === `${k}.${i}`);
    if (!t) return;
    s += `<polyline points="${t.puntos.map(p => `${X(p[0]).toFixed(1)},${Y(p[2]).toFixed(1)}`).join(' ')}" fill="none" stroke="${esc(t.color)}" stroke-width="2" stroke-linejoin="round"/>`;
    if (b.parrilla === 'Inferior' && !etiqueta) etiqueta = `#${b.numero} @ ${m(b.separacion)}`;
  });
  // Las barras en Y se ven cortadas, como puntos.
  for (const b of conjuntos.filter(c => c.direccion === 'Y'))
    for (const t of b.posiciones)
      s += `<circle cx="${X(t).toFixed(1)}" cy="${Y(b.z).toFixed(1)}" r="1.9" fill="${esc(color.get(b.numero) ?? '#5E6670')}"/>`;
  for (const c of z.columnas) {
    if (Math.abs(c.centro.y - yc) > c.alto / 2) continue;
    const a = c.centro.x - c.ancho / 2;
    s += `<rect x="${X(a)}" y="${Y(z.altura) - 20}" width="${c.ancho * esc2}" height="20" fill="#CDD2D8" stroke="#3E4751"/>`;
  }
  s += `<g stroke="#3E4751" stroke-width=".9"><line x1="${X(x0)}" y1="${base + 14}" x2="${X(x1)}" y2="${base + 14}"/><line x1="${X(x0)}" y1="${base + 9}" x2="${X(x0)}" y2="${base + 19}"/><line x1="${X(x1)}" y1="${base + 9}" x2="${X(x1)}" y2="${base + 19}"/>
    <line x1="${X(x1) + 12}" y1="${Y(0)}" x2="${X(x1) + 12}" y2="${Y(z.altura)}"/><line x1="${X(x1) + 7}" y1="${Y(0)}" x2="${X(x1) + 17}" y2="${Y(0)}"/><line x1="${X(x1) + 7}" y1="${Y(z.altura)}" x2="${X(x1) + 17}" y2="${Y(z.altura)}"/></g>
    <text x="${(X(x0) + X(x1)) / 2}" y="${base + 28}" text-anchor="middle" font-size="10" font-family="Consolas,monospace">${m(x1 - x0)} m</text>
    <text x="${X(x1) + 22}" y="${(Y(0) + Y(z.altura)) / 2 + 3}" font-size="10" font-family="Consolas,monospace">${m(z.altura)}</text>`;
  if (etiqueta) s += `<text x="${X(x0) + 4}" y="${Y(z.altura) - 6}" font-size="10" fill="#3E4751" font-family="Consolas,monospace">${esc(etiqueta)}</text>`;
  $('sec2d').innerHTML = `<svg viewBox="0 0 ${W} ${H}" role="img" aria-label="Sección transversal de la zapata">${s}</svg>`;
  $('titulo2d').textContent = `Sección A-A · ${z.nombre} (2D)`;
  $('esc2d').textContent = `Escala 1:${Math.max(1, Math.round(1 / (esc2 * 0.2646)))}`;
  const pata = conjuntos.find(b => b.direccion === 'X' && b.pata);
  $('gan2d').textContent = pata ? `Gancho 90° · pata ${m(pata.pata)} m` : '';
}

$('c2d').addEventListener('click', () => { $('emergente').hidden = true; $('b2d').setAttribute('aria-pressed', false); });
$('b2d').addEventListener('click', () => {
  const ver = $('emergente').hidden;
  $('emergente').hidden = !ver;
  $('b2d').setAttribute('aria-pressed', ver);
});

// ---- Acciones

let temporizador = null;
function aviso(texto, clase = '') {
  const t = $('toast');
  t.textContent = texto;
  t.className = clase;
  t.hidden = false;
  clearTimeout(temporizador);
  temporizador = setTimeout(() => { t.hidden = true; }, clase === 'mal' || texto.length > 90 ? 9000 : 3500);
}

$('sel').addEventListener('change', () => { estado.activa = $('sel').value; elegir([$('sel').value]); });
$('bSeleccionar').addEventListener('click', () => enviar({ tipo: 'seleccionar' }));
$('bMostrar').addEventListener('click', () => { if (estado.elegidas.length) enviar({ tipo: 'mostrar', ids: estado.elegidas }); });
$('bRecalcular').addEventListener('click', () => { calcular(); aviso('Recalculado.'); });
$('bIguales').addEventListener('click', () => { if (estado.activa) elegir(grupoDe(estado.activa)); });
$('bCerrar').addEventListener('click', () => enviar({ tipo: 'cerrar' }));

function aplicarAhora(forzar = false) {
  enviar({ tipo: 'aplicar', ids: estado.elegidas, ...configMensaje(), forzar });
  aviso('Generando en Revit…');
}

// Revit no generó porque hay choques (decisión 35, como vigas): «Revisar» cierra y deja ver los puntos rojos;
// «Aplicar de todos modos» vuelve a mandar con forzar.
function preguntarChoques(cantidad) {
  $('toast').hidden = true;
  $('txtChoques').innerHTML = `<p style="margin:0 0 8px">${cantidad === 1 ? 'Hay 1 lugar' : `Hay ${cantidad} lugares`} donde una barra se mete en otra. Se marcan con puntos rojos en la vista 3D; al pasar el mouse dice cuáles son.</p>`;
  estado.verChoques = true;
  $('capaChoques').checked = true;
  dibujar3d();
  $('mChoques').hidden = false;
}
$('bForzar').addEventListener('click', () => {
  $('mChoques').hidden = true;
  aplicarAhora(true);
});

$('bAplicar').addEventListener('click', () => {
  const lista = estado.elegidas.map(zapata).filter(Boolean);
  const propias = lista.filter(z => z.barrasPropias > 0), manuales = lista.filter(z => z.barrasTotales > z.barrasPropias);
  const lineas = [];
  if (propias.length) lineas.push(`${propias.map(z => z.nombre).join(', ')} ya ${propias.length === 1 ? 'tiene' : 'tienen'} acero del add-in: se reemplaza.`);
  if (manuales.length) lineas.push(`${manuales.map(z => z.nombre).join(', ')} ${manuales.length === 1 ? 'tiene' : 'tienen'} barras hechas a mano: no se tocan y pueden quedar duplicadas.`);
  if (!lineas.length) {
    aplicarAhora();
    return;
  }
  $('txtConfirmar').innerHTML = lineas.map(l => `<p style="margin:0 0 8px">${esc(l)}</p>`).join('');
  $('mConfirmar').hidden = false;
});
$('bConfirmar').addEventListener('click', () => {
  $('mConfirmar').hidden = true;
  aplicarAhora();
});

// ---- Reglas generales (decisiones 38 y 38a del plan de 2026-11-05): las de vigas, en su mismo archivo. Como la
// pantalla de vigas: abre bloqueada, se desbloquea para editar, «Guardar y bloquear» manda guardarReglas, y si se sale
// con cambios avisa. Los textos van como los manda C# (PantallaReglas): m, cm y kg/m.

const reglasUi = { guardadas: null, borrador: null, bloqueadas: true, esperando: false };
const copiaReglas = r => JSON.parse(JSON.stringify(r));
const hayCambios = () => reglasUi.borrador && JSON.stringify(reglasUi.borrador) !== JSON.stringify(reglasUi.guardadas);

function recibirReglas(r) {
  if (!r) return;
  reglasUi.guardadas = copiaReglas(r);
  // Al llegar las recién guardadas (o al abrir), el borrador las toma y se bloquea.
  if (reglasUi.esperando || reglasUi.bloqueadas || !reglasUi.borrador) {
    reglasUi.borrador = copiaReglas(r);
    reglasUi.bloqueadas = true;
    reglasUi.esperando = false;
  }
  if (!$('mReglas').hidden) dibujarReglas();
}

function dibujarReglas() {
  const b = reglasUi.borrador, off = reglasUi.bloqueadas;
  if (!b) return;
  $('candado').classList.toggle('abierto', !off);
  $('txtCandado').textContent = off ? 'Bloqueadas. Valen para vigas y zapatas en este PC.' : 'Desbloqueadas: los cambios se guardan con «Guardar y bloquear».';
  $('bCandado').textContent = off ? 'Desbloquear' : 'Bloquear sin guardar';
  $('arcoCandado').setAttribute('d', off ? 'M4.5 6V4.5a2.5 2.5 0 0 1 5 0V6' : 'M4.5 6V4.5a2.5 2.5 0 0 1 5 0');
  $('problemaReglas').hidden = !b.problema;
  $('problemaReglas').textContent = b.problema ?? '';
  $('rMult').value = b.multiplo;
  $('rGap').value = b.distanciaLibreCm;
  for (const id of ['rMult', 'rGap']) $(id).disabled = off;
  $('rVarilla').querySelectorAll('button').forEach(x => { x.disabled = off; x.setAttribute('aria-pressed', (x.dataset.v === '12') === b.varilla12); });
  $('rRedondeo').querySelectorAll('button').forEach(x => { x.disabled = off; x.setAttribute('aria-pressed', x.dataset.v === (b.redondeoGanchoCm ?? '0')); });
  const campos = [['t', 'Traslapo'], ['g', 'Gancho 90°'], ['g180', 'Gancho 180°'], ['e', 'Estribo 135°'], ['k', 'kg/m']];
  $('rTabla').innerHTML = `<span></span>${campos.map(([, n]) => `<span class="cab">${n}</span>`).join('')}`
    + b.porDiametro.map((f, i) => `<span>${esc(f.d)}</span>${campos.map(([c, n]) =>
      `<input type="text" data-fila="${i}" data-campo="${c}" value="${esc(f[c])}" placeholder="—" aria-label="${esc(`${n} del ${f.d}`)}" ${off ? 'disabled' : ''}>`).join('')}`).join('');
  $('rGuardar').disabled = off;
}

function abrirReglas() {
  $('avisoReglas').hidden = true;
  dibujarReglas();
  $('mReglas').hidden = false;
}

function salirDeReglas() {
  if (!reglasUi.bloqueadas && hayCambios()) {
    $('avisoReglas').hidden = false;
    return;
  }
  reglasUi.bloqueadas = true;
  reglasUi.borrador = copiaReglas(reglasUi.guardadas);
  $('mReglas').hidden = true;
}

function guardarReglas() {
  reglasUi.esperando = true;
  enviar({ tipo: 'guardarReglas', reglas: { ...reglasUi.borrador, problema: null } });
  $('avisoReglas').hidden = true;
  aviso('Guardando las reglas…');
}

function descartarReglas() {
  reglasUi.borrador = copiaReglas(reglasUi.guardadas);
  reglasUi.bloqueadas = true;
  $('avisoReglas').hidden = true;
  $('mReglas').hidden = true;
}

$('bReglas').addEventListener('click', abrirReglas);
$('cReglas').addEventListener('click', salirDeReglas);
$('rCerrar').addEventListener('click', salirDeReglas);
$('rGuardar').addEventListener('click', guardarReglas);
$('rAvisoGuardar').addEventListener('click', guardarReglas);
$('rAvisoDescartar').addEventListener('click', descartarReglas);
$('rAvisoSeguir').addEventListener('click', () => { $('avisoReglas').hidden = true; });
$('bCandado').addEventListener('click', () => {
  // Desbloquear para editar, o volver a bloquear dejando las guardadas (sin cerrar la pantalla).
  if (!reglasUi.bloqueadas) reglasUi.borrador = copiaReglas(reglasUi.guardadas);
  reglasUi.bloqueadas = !reglasUi.bloqueadas;
  $('avisoReglas').hidden = true;
  dibujarReglas();
});
$('rMult').addEventListener('input', () => { reglasUi.borrador.multiplo = $('rMult').value; });
$('rGap').addEventListener('input', () => { reglasUi.borrador.distanciaLibreCm = $('rGap').value; });
$('rVarilla').addEventListener('click', e => {
  if (!e.target.dataset?.v || reglasUi.bloqueadas) return;
  reglasUi.borrador.varilla12 = e.target.dataset.v === '12';
  dibujarReglas();
});
$('rRedondeo').addEventListener('click', e => {
  if (!e.target.dataset?.v || reglasUi.bloqueadas) return;
  reglasUi.borrador.redondeoGanchoCm = e.target.dataset.v;
  dibujarReglas();
});
$('rTabla').addEventListener('input', e => {
  const { fila, campo } = e.target.dataset ?? {};
  if (fila === undefined) return;
  reglasUi.borrador.porDiametro[+fila][campo] = e.target.value;
});

dibujarTodo();
enviar({ tipo: 'listo' });
