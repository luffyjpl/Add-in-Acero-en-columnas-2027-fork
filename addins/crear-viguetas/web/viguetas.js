// «Crear viguetas» (#947). La página dibuja la planta del piso y pide; los paneles y el reparto (fórmula del Excel de
// Carlos) se calculan en C# (Calculo/Viguetas). Mensajes: ProtocoloViguetas.cs. Medidas en mm; en pantalla, en m.
(() => {
  const $ = (id) => document.getElementById(id);
  const CLAVE = 'cqeing-viguetas-3';
  const CHK = '<svg viewBox="0 0 12 12" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-label="Listo"><path d="M2.6 6.3l2.2 2.2 4.6-4.9"/></svg>';
  const ICO_BORRAR = '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"><path d="M3 4.5h10M6.5 4.5V3h3v1.5M4.5 4.5l.6 8.5h5.8l.6-8.5M7 7v4M9 7v4"/></svg>';
  // Cómo se eligen las zonas (#954, decisión 1): por puntos (columnas, extremos, cruces y vértices del suelo), por las
  // vigas que forman el perímetro (decisión 24 de #947) o por paneles (uno; Ctrl + clic suma o quita).
  const linea = () => (S.dir === 'Y' ? 'columna' : 'fila');
  const MODOS = [
    ['puntos', 'Puntos', 'Clic en los puntos del contorno: columnas, extremos, cruces o vértices del suelo'],
    ['perimetro', 'Por vigas', 'Clic en las vigas, viguetas de borde o voladizos que forman el perímetro'],
    ['uno', 'Paneles', 'Un clic elige un panel; Ctrl + clic suma o quita'],
  ];

  // Los puntos del modo Puntos (decisión 9): tipo, color de la leyenda y nombre, en orden de prioridad cuando se enciman.
  const TIPOS_PUNTO = [
    ['columna', 'Columna', 'Centro de columna'],
    ['cruce', 'Cruce de vigas', 'Cruce de ejes de dos vigas'],
    ['viga', 'Punta de viga', 'Extremo de una viga'],
    ['vigueta', 'Vigueta', 'Extremo de una vigueta o cruce con una'],
    ['suelo', 'Suelo', 'Vértice del suelo'],
  ];
  const RADIO_PUNTO = 3.5, SEPARACION_PUNTOS = 9; // px de pantalla

  const S = {
    doc: '', pisos: [], tipos: [], piso: null, comoBorde: false, modo: 'puntos', paneles: [], bordes: [], perimetro: [],
    // Modo Puntos: las zonas que armó el usuario (contorno y vacíos, por ejes), las que devolvió C# y el trazo en curso.
    zonas: [], zonasArm: [], trazo: [], conVacio: false,
    // Vacíos marcados en Por vigas o Paneles (decisión 29).
    vaciosPanel: [],
    // Paneles vueltos vacíos con un clic (decisión 30): { id, eje }.
    panelesVacios: [],
    dir: 'X', sep: '0,80', anc: '0,10', tipo: '', continuas: true, seguir: true, borradas: [], quitar: [], editar: false, propuesta: null, vista: null,
  };
  try {
    // El modo no se recuerda de la versión anterior (2): ahora el que viene marcado es Puntos.
    const m = JSON.parse(localStorage.getItem(CLAVE) || localStorage.getItem('cqeing-viguetas-2') || 'null');
    // Decisión 26: «Seguir las que ya están» no se recuerda; siempre arranca marcado.
    if (m) Object.assign(S, { sep: m.sep || S.sep, anc: m.anc || S.anc, tipo: m.tipo || '', dir: m.dir || S.dir });
    if (m && MODOS.some(([v]) => v === m.modo) && localStorage.getItem(CLAVE)) S.modo = m.modo;
  } catch (e) { /* sin almacenamiento se usan los valores por defecto */ }
  const guardar = () => {
    try { localStorage.setItem(CLAVE, JSON.stringify({ sep: S.sep, anc: S.anc, tipo: S.tipo, dir: S.dir, modo: S.modo })); } catch (e) { /* nada */ }
  };

  const fmt = (mm) => (mm / 1000).toFixed(2).replace('.', ',');
  const num = (s) => { const t = String(s).trim(); const v = Number(t.replace(',', '.')); return t !== '' && isFinite(v) ? v : NaN; };
  const esc = (t) => String(t ?? '').replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
  let enviar = () => {};

  function valores() {
    const s = num(S.sep), b = num(S.anc), e = {};
    if (!(s > 0)) e.sep = 'Escribe un número mayor que 0';
    if (!(b > 0)) e.anc = 'Escribe un número mayor que 0';
    if (!e.sep && !e.anc && b >= s) e.anc = 'Debe ser menor que la separación';
    return { s: Math.round(s * 1000), b: Math.round(b * 1000), e, ok: !e.sep && !e.anc };
  }
  const caja = (pts) => {
    const xs = pts.map((p) => p[0]), ys = pts.map((p) => p[1]);
    return { x0: Math.min(...xs), x1: Math.max(...xs), y0: Math.min(...ys), y1: Math.max(...ys) };
  };
  const elegidos = () => (S.piso ? S.paneles.map((id) => [...S.piso.paneles, ...S.perimetro, ...S.zonasArm].find((p) => p.id === id)).filter(Boolean) : []);
  const perimetral = () => S.modo === 'perimetro';
  const porPuntos = () => S.modo === 'puntos';
  function armar() { enviar({ tipo: 'perimetro', ids: S.bordes }); }
  /** Manda todas las zonas por puntos (decisión 6): el libre de los lados libres depende del ancho de vigueta. */
  function armarZonas() {
    const V = valores();
    if (V.ok) enviar({ tipo: 'zonas', ancho: V.b, zonas: S.zonas });
  }
  const mismoPunto = (a, b) => Math.hypot(a[0] - b[0], a[1] - b[1]) < 1;
  /** Decisión 27: dos puntos van en línea si el lado es casi horizontal o casi vertical (5°, como la decisión 20). */
  const enLineaRecta = (a, b) => {
    const dx = Math.abs(b[0] - a[0]), dy = Math.abs(b[1] - a[1]);
    return dy <= 0.0875 * dx || dx <= 0.0875 * dy;
  };
  /** Si <q> cae dentro del polígono (regla par-impar). */
  function dentroDe(p, q) {
    let d = false;
    for (let i = 0, j = p.length - 1; i < p.length; j = i++) {
      if ((p[i][1] > q[1]) !== (p[j][1] > q[1]) && q[0] < (p[j][0] - p[i][0]) * (q[1] - p[i][1]) / (p[j][1] - p[i][1]) + p[i][0]) d = !d;
    }
    return d;
  }
  /** Cierra el trazo: una zona nueva o, con «＋ Vacío», un vacío de la zona que lo contiene (decisión 5). */
  function cerrarTrazo() {
    const t = S.trazo;
    if (t.length < 3) return;
    if (!enLineaRecta(t[t.length - 1], t[0])) {
      aviso('El último punto no queda en línea con el primero: para cerrar en cuadrado, marca la esquina que falta.');
      return;
    }
    S.trazo = [];
    if (S.conVacio) {
      const centro = [t.reduce((s, p) => s + p[0], 0) / t.length, t.reduce((s, p) => s + p[1], 0) / t.length];
      if (!porPuntos()) {
        // Decisión 29: en Por vigas y Paneles, el vacío va en lo elegido que contiene su centro.
        S.conVacio = false;
        if (elegidos().some((x) => dentroDe(x.eje, centro))) S.vaciosPanel = [...S.vaciosPanel, t];
        else aviso('El vacío tiene que quedar dentro de lo elegido.');
        lateral(); pintar(); pedir();
        return;
      }
      const z = S.zonas.find((x) => dentroDe(x.contorno, centro));
      S.conVacio = false;
      if (!z) { aviso('El vacío tiene que quedar dentro de una zona.'); lateral(); pintar(); return; }
      z.vacios.push(t);
    } else {
      const n = S.zonas.reduce((m, x) => Math.max(m, Number(x.id.slice(1)) || 0), 0) + 1;
      S.zonas.push({ id: `Z${n}`, contorno: t, vacios: [] });
    }
    lateral(); pintar(); armarZonas();
  }
  /** Todos los vacíos de Por vigas y Paneles: los marcados con puntos (decisión 29) y los paneles vaciados (decisión 30). */
  const vaciosFueraDePuntos = () => (porPuntos() ? [] : [...S.vaciosPanel, ...S.panelesVacios.map((x) => x.eje)]);
  /** Decisión 30: con «＋ Vacío», un clic en un panel lo vuelve vacío; C# corta en Revit las viguetas que lo cruzan y relee el piso. */
  function vaciarPanel(id) {
    S.conVacio = false;
    if (S.panelesVacios.some((x) => x.id === id)) { aviso(`${id} ya está marcado como vacío.`); lateral(); pintar(); return; }
    S.panelesVacios = [...S.panelesVacios, { id, eje: S.piso.paneles.find((p) => p.id === id).eje }];
    enviar({ tipo: 'vaciar', id });
    aviso(`Volviendo vacío ${id}…`);
    // Un panel vacío ya no se reparte: si estaba elegido, deja de estarlo.
    if (S.paneles.includes(id)) return elegir(S.paneles.filter((x) => x !== id));
    lateral(); pintar();
  }
  function quitarZona(id, vacio) {
    if (vacio === undefined) S.zonas = S.zonas.filter((z) => z.id !== id);
    else S.zonas.find((z) => z.id === id)?.vacios.splice(vacio, 1);
    if (!S.zonas.length) { S.zonasArm = []; S.conVacio = false; return elegir([]); }
    armarZonas();
  }
  /** El primer panel elegido: de él salen las cotas y el ancho libre que se muestra. */
  const principal = () => elegidos()[0] || null;
  const vivas = () => (S.propuesta ? S.propuesta.viguetas.filter((v) => !v.borrada) : []);
  const creadas = () => elegidos().reduce((n, p) => n + p.creadas, 0);
  const lista = (ids) => (ids.length <= 4 ? ids.join(', ') : `${ids.slice(0, 3).join(', ')} y ${ids.length - 3} más`);

  /** Los paneles alineados con <id> en la dirección de las viguetas: la columna en ↕ Y, la fila en ↔ X. */
  function enLinea(id) {
    const base = S.piso.paneles.find((p) => p.id === id), enX = S.dir === 'Y';
    const rango = (p) => { const c = caja(p.libre); return enX ? [c.x0, c.x1] : [c.y0, c.y1]; };
    const orden = (p) => { const c = caja(p.libre); return enX ? -c.y1 : c.x0; };
    const [a0, a1] = rango(base);
    return S.piso.paneles.filter((p) => {
      const [b0, b1] = rango(p), cruce = Math.min(a1, b1) - Math.max(a0, b0);
      return cruce > 0.5 * Math.min(a1 - a0, b1 - b0);
    }).sort((p, q) => orden(p) - orden(q)).map((p) => p.id);
  }

  /* ---------- mensajes ---------- */
  let tPedir;
  function pedir() {
    clearTimeout(tPedir);
    if (!S.paneles.length || !valores().ok) { S.propuesta = null; pintar(); return; }
    tPedir = setTimeout(() => enviar({ tipo: 'propuesta', ...pedido() }), 120);
  }
  function pedido() {
    const V = valores();
    return { paneles: S.paneles, direccion: S.dir, ejeAEje: V.s, ancho: V.b, borradas: S.borradas, continuas: S.continuas, seguir: S.seguir, quitarModelo: S.quitar,
      // Decisión 28: en Paneles las viguetas siguen por su línea, salvo con «Solo P…». Decisión 29: vacíos fuera de Puntos.
      prolongar: S.modo === 'uno' && S.paneles.length === 1 && S.sinAviso !== S.paneles[0], vacios: vaciosFueraDePuntos() };
  }
  function abrirPiso(id) { enviar({ tipo: 'piso', id, viguetasComoBorde: S.comoBorde }); }

  function recibir(m) {
    if (m.tipo === 'pisos') {
      S.doc = m.documento; S.pisos = m.pisos; S.tipos = m.tipos;
      if (!S.tipos.some((t) => t.id === S.tipo)) S.tipo = (S.tipos.find((t) => t.sugerido) || S.tipos[0] || {}).id || '';
      modal();
      todo();
    } else if (m.tipo === 'piso') {
      const mismo = S.piso && S.piso.id === m.id;
      S.piso = m; S.comoBorde = m.viguetasComoBorde;
      if (!mismo) { S.vista = null; S.paneles = []; S.bordes = []; S.perimetro = []; S.zonas = []; S.zonasArm = []; S.trazo = []; S.conVacio = false; S.seguir = true; S.vaciosPanel = []; S.panelesVacios = []; }
      // Al crear o quitar, C# vuelve a leer el piso y olvida las zonas: se mandan de nuevo, como el perímetro.
      if (porPuntos()) {
        $('modal').hidden = true;
        todo();
        if (S.zonas.length) armarZonas();
        return;
      }
      if (perimetral()) {
        S.bordes = S.bordes.filter((id) => m.armazon.some((a) => a.id === id));
        $('modal').hidden = true;
        todo();
        if (S.bordes.length) { S.rearmar = true; armar(); }
        return;
      }
      const quedan = S.paneles.filter((id) => m.paneles.some((p) => p.id === id));
      if (quedan.length !== S.paneles.length) { S.paneles = quedan; S.borradas = []; S.quitar = []; }
      // Las del modelo que se quitaron al crear ya no están en el piso.
      S.quitar = S.quitar.filter((id) => m.armazon.some((a) => a.id === id.split('#')[0]));
      if (!S.paneles.length) { S.propuesta = null; S.editar = false; }
      $('modal').hidden = true;
      todo();
      if (S.paneles.length) pedir();
    } else if (m.tipo === 'perimetro') {
      if (m.ids.join() !== S.bordes.join()) return;
      S.perimetro = m.paneles;
      // Otro perímetro también se llama Z1: las marcas solo siguen si es el mismo, vuelto a armar al releer el piso.
      const mismo = S.rearmar; S.rearmar = false;
      (mismo ? volvieron : elegir)(m.paneles.map((p) => p.id));
    } else if (m.tipo === 'zonas') {
      if (!porPuntos()) return;
      S.zonasArm = m.paneles;
      const perdidas = S.zonas.filter((z) => !m.paneles.some((p) => p.id === z.id)).map((z) => z.id);
      if (perdidas.length) aviso(`${lista(perdidas)}: con esos puntos no queda losa entre las vigas.`);
      volvieron(m.paneles.map((p) => p.id));
    } else if (m.tipo === 'propuesta') {
      if (m.paneles.join() === S.paneles.join()) { S.propuesta = m.propuesta; pintar(); }
    } else if (m.tipo === 'aviso') {
      aviso(m.texto, m.bien);
    }
  }

  /* ---------- primera entrada ---------- */
  /** Decisión 36: si en ese piso ya hay viguetas creadas con el add-in. */
  const estadoPiso = (p) => (p.creadas ? `<span class="hecho">Construido · ${p.creadas} viguetas</span>` : 'Sin construir');
  function modal() {
    $('m-pisos').innerHTML = S.pisos.length
      ? S.pisos.map((p) => `<button class="piso" data-piso="${esc(p.id)}"><span class="lv">${esc(p.nombre)}</span><span>${fmt(p.cota)} m</span><span class="pc">${estadoPiso(p)}</span></button>`).join('')
      : '<p>Este modelo no tiene vigas con nivel de referencia.</p>';
    $('modal').hidden = !!S.piso;
  }
  $('m-pisos').addEventListener('click', (e) => { const b = e.target.closest('[data-piso]'); if (b) abrirPiso(b.dataset.piso); });

  /* ---------- en qué pisos se crea (decisión 34) ---------- */
  const ejesVaciados = () => (porPuntos() ? [] : S.panelesVacios.map((x) => x.eje));
  /** Sin propuesta, pero con paneles vueltos vacíos que se pueden llevar a otros pisos. */
  const soloVacios = () => !S.propuesta && ejesVaciados().length > 0 && S.pisos.length > 1;
  function crearEn(pisos) {
    enviar({ tipo: 'crear', ...pedido(), tipoVigueta: S.tipo, pisos, vaciados: ejesVaciados() });
    aviso(pisos.length > 1 ? `Creando en ${pisos.length} pisos…` : 'Creando las viguetas en Revit…');
  }
  function marcasCrear() {
    const n = S.pisosCrear.length;
    $('mc-pisos').querySelectorAll('input').forEach((c) => { c.checked = S.pisosCrear.includes(c.dataset.p); });
    $('mc-todos').checked = n === S.pisos.length;
    $('mc-crear').disabled = !n;
    $('mc-crear').textContent = n > 1 ? `Crear en ${n} pisos` : n ? 'Crear en 1 piso' : 'Elige un piso';
  }
  function abrirCrear() {
    S.pisosCrear = [S.piso.id];
    $('mc-pisos').innerHTML = S.pisos.map((p) => `<label class="piso marca"><input type="checkbox" data-p="${esc(p.id)}"><span class="lv">${esc(p.nombre)}</span><span>${fmt(p.cota)} m</span><span class="pc">${p.id === S.piso.id ? 'piso abierto · ' : ''}${estadoPiso(p)}</span></label>`).join('');
    marcasCrear();
    $('m-crear').hidden = false;
  }
  $('mc-pisos').addEventListener('change', (e) => {
    const id = e.target.dataset.p;
    S.pisosCrear = S.pisos.map((p) => p.id).filter((x) => (x === id ? e.target.checked : S.pisosCrear.includes(x)));
    marcasCrear();
  });
  $('mc-todos').addEventListener('change', (e) => { S.pisosCrear = e.target.checked ? S.pisos.map((p) => p.id) : []; marcasCrear(); });
  $('mc-cancelar').addEventListener('click', () => { $('m-crear').hidden = true; });
  $('mc-crear').addEventListener('click', () => { $('m-crear').hidden = true; crearEn(S.pisosCrear); });

  /* ---------- barra lateral ---------- */
  function lateral() {
    const sel = elegidos(), pan = principal(), V = valores();
    const n = (k, ok, extra = '') => `<span class="n ${ok ? 'done' : ''}" ${extra}>${ok ? CHK : k}</span>`;
    const campo = (id, k, lab) => `<label class="fld ${V.e[k] ? 'bad' : ''}" id="w-${k}" for="${id}"><span class="fl">${lab}</span><span class="inp"><input id="${id}" inputmode="decimal" autocomplete="off" value="${esc(S[k])}"><em>m</em></span><span class="err" id="e-${k}">${V.e[k] || ''}</span></label>`;
    let infoPanel = `<span class="info">${porPuntos() ? 'Haz clic en los puntos del contorno y cierra en el primero.' : perimetral() ? (S.bordes.length ? `<b>${S.bordes.length}</b> elementos elegidos · todavía no cierran un perímetro.` : 'Haz clic en las vigas que forman el perímetro.') : 'Haz clic en un panel de la planta.'}</span>`;
    if (porPuntos()) {
      // Cada zona y cada vacío se quitan con su ×; las demás siguen (decisión 6).
      const fila = (txt, id, v, t) => `<li><span>${txt}</span><button class="x" data-act="sin-zona" data-z="${esc(id)}" ${v === undefined ? '' : `data-vacio="${v}"`} title="${t}" aria-label="${t}">×</button></li>`;
      const zonas = S.zonas.map((z) => {
        const arm = S.zonasArm.find((x) => x.id === z.id);
        return fila(`<b>${esc(z.id)}</b> · ${z.contorno.length} puntos${arm && arm.bordes.length ? ` · ${arm.bordes.length} de borde` : ''}${arm && arm.creadas ? ` · ${arm.creadas} creadas` : ''}`, z.id, undefined, `Quitar ${z.id}`)
          + z.vacios.map((v, i) => fila(`<span class="sub">vacío ${i + 1} de ${esc(z.id)} · ${v.length} puntos</span>`, z.id, i, `Quitar el vacío ${i + 1} de ${z.id}`)).join('');
      }).join('');
      const usados = TIPOS_PUNTO.filter(([v]) => (S.piso?.puntos || []).some((q) => q.tipo === v));
      infoPanel = `${zonas ? `<ul class="zonas">${zonas}</ul>` : infoPanel}
        <div class="leyenda-pt" aria-label="Tipos de punto">${usados.map(([v, l, t]) => `<span title="${t}"><i class="pt-sw t-${v}"></i>${l}</span>`).join('')}</div>
        <button class="ib ${S.conVacio ? 'vac' : ''}" data-act="vacio" ${S.zonas.length ? '' : 'disabled'} aria-pressed="${S.conVacio}" title="Marca los puntos de un vacío dentro de una zona: las viguetas paran en él">＋ Vacío${S.conVacio ? '<span class="k">marcando</span>' : ''}</button>`;
    } else if (pan) {
      const c = caja(pan.libre);
      infoPanel = (sel.length > 1
        ? `<span class="info"><b>${sel.length} paneles</b> · ${esc(lista(S.paneles))}</span>`
        : `<span class="info"><b>${esc(pan.id)}</b> · libre <b>${fmt(c.x1 - c.x0)} × ${fmt(c.y1 - c.y0)}</b> m</span>`)
        + (creadas() ? `<span class="info">Ya tiene${sel.length > 1 ? 'n' : ''} <b>${creadas()}</b> viguetas creadas por CQEING.</span>` : '');
    }
    if (!porPuntos() && S.piso) {
      // Decisiones 29 y 30: ＋ Vacío también en Por vigas y Paneles, con puntos o con un clic en un panel.
      const marcados = S.panelesVacios.map((x, i) => `<li><span><b>${esc(x.id)}</b> · vacío</span><button class="x" data-act="sin-panel-vacio" data-i="${i}" title="Quitar la marca de ${esc(x.id)}. Lo cortado se recupera con Ctrl+Z en Revit" aria-label="Quitar la marca de ${esc(x.id)}">×</button></li>`).join('');
      const filas = marcados + S.vaciosPanel.map((v, i) => `<li><span>vacío ${i + 1} · ${v.length} puntos</span><button class="x" data-act="sin-vacio" data-i="${i}" title="Quitar el vacío ${i + 1}" aria-label="Quitar el vacío ${i + 1}">×</button></li>`).join('');
      infoPanel += `${filas ? `<ul class="zonas">${filas}</ul>` : ''}<button class="ib ${S.conVacio ? 'vac' : ''}" data-act="vacio" aria-pressed="${S.conVacio}" title="Haz clic en un panel para volverlo vacío o marca los puntos de un vacío dentro de lo elegido: las viguetas paran en él">＋ Vacío${S.conVacio ? '<span class="k">marcando</span>' : ''}</button>`;
    }
    $('side').innerHTML = `
      <div class="sec"><div class="sh">${n(1, !!S.piso)}Piso</div>
        <select class="sel" id="f-piso" aria-label="Piso">${S.piso ? '' : '<option value="">Elige un piso</option>'}${S.pisos.map((p) => `<option value="${esc(p.id)}" ${S.piso && S.piso.id === p.id ? 'selected' : ''}>${esc(p.nombre)}${p.creadas ? ' · construido' : ''}</option>`).join('')}</select>
        <label class="chk" title="Las viguetas que ya están en el modelo también cierran paneles (viguetas de borde)"><input type="checkbox" id="f-borde" ${S.comoBorde ? 'checked' : ''} ${S.piso ? '' : 'disabled'}>Usar las viguetas del modelo como borde</label></div>
      <div class="sec ${S.piso ? '' : 'off'}"><div class="sh">${n(2, !!pan)}${porPuntos() ? 'Zonas' : 'Paneles'}</div>
        <div class="modos" role="group" aria-label="Cómo elegir las zonas">${MODOS.map(([v, l, t]) => `<button data-act="modo" data-v="${v}" aria-pressed="${S.modo === v}" title="${t}">${l}</button>`).join('')}</div>
        ${S.modo === 'uno' ? '<span class="info tip">Con <b>Ctrl + clic</b> sumas o quitas paneles.</span>' : ''}
        ${infoPanel}
        <button class="ib ${S.editar ? 'on' : ''}" id="b-editar" data-act="editar" ${S.propuesta ? '' : 'disabled'} aria-pressed="${S.editar}" title="Clic sobre una vigueta para quitarla; si cruza una viga, cada lado se quita por separado">${ICO_BORRAR}Eliminar viguetas${S.borradas.length + S.quitar.length ? `<span class="k">${S.borradas.length + S.quitar.length}</span>` : ''}</button></div>
      <div class="sec ${pan ? '' : 'off'}"><div class="sh">${n(3, !!pan)}Dirección</div>
        <div class="dirs">${[['X', '↔', 'Viguetas paralelas a X'], ['Y', '↕', 'Viguetas paralelas a Y']].map(([v, a, t]) => `<button class="dirbtn" data-act="dir" data-v="${v}" aria-pressed="${S.dir === v}" title="${t}"><span class="arr">${a}</span><b>${v}</b></button>`).join('')}</div></div>
      <div class="sec ${pan ? '' : 'off'}"><div class="sh">${n(4, !!pan && V.ok, 'id="n4"')}Separación</div>
        <div class="campos">${campo('f-sep', 'sep', 'Eje a eje')}${campo('f-anc', 'anc', 'Ancho de vigueta')}</div>
        <label class="fld" for="f-tipo"><span class="fl">Tipo de vigueta en Revit</span>
          <select class="sel" id="f-tipo">${S.tipos.map((t) => `<option value="${esc(t.id)}" ${t.id === S.tipo ? 'selected' : ''}>${esc(t.nombre)}</option>`).join('')}</select></label>
        <div id="ubic"></div></div>
      <div class="sum" id="sum"></div>`;
  }

  function ubicacion() {
    const p = S.propuesta;
    $('ubic').innerHTML = p && p.hayGuias
      ? `<div class="fld"><span class="fl">Dónde van las viguetas</span><div class="modos dos" role="group">${[[true, 'Seguir las que ya están', 'En las mismas líneas que las viguetas de la misma ' + linea()], [false, 'Fórmula del Excel', 'Repartidas con la fórmula sobre el ancho del panel']].map(([v, l, t]) => `<button data-act="seguir" data-v="${v}" aria-pressed="${S.seguir === v}" title="${t}">${l}</button>`).join('')}</div></div>`
      : '';
  }

  function resumen() {
    const p = S.propuesta;
    const f = (k, v) => `<dt>${k}</dt><dd>${v}</dd>`;
    $('sum').innerHTML = `<div class="sum-h">Resumen</div><dl>${f('Piso', S.piso ? esc(S.piso.nombre) : '—')}${f('Elegidos', S.paneles.length ? esc(lista(S.paneles)) : '—')}${f('Viguetas a crear', p ? p.lineas : '—')}${f('Tipos', p ? p.tipos.length : '—')}${f('Ya creadas', S.paneles.length ? creadas() : '—')}</dl>`;
  }

  /**
   * Volvieron las zonas o el perímetro armados (decisión 8): al crear, al quitar o al sumar o quitar una zona. Las
   * eliminadas de las zonas que siguen y los tramos del modelo marcados se conservan; solo se van las de una zona quitada.
   */
  function volvieron(ids) {
    const siguen = (id) => ids.includes(id.split(':')[0]);
    if (ids.join() !== S.paneles.join()) S.propuesta = null;
    S.paneles = ids; S.borradas = S.borradas.filter(siguen);
    if (!ids.length) { S.quitar = []; S.editar = false; }
    lateral(); pintar(); pedir();
  }

  /** Cambia los paneles elegidos: la propuesta y las eliminadas se reinician. */
  function elegir(ids) {
    S.paneles = ids; S.borradas = []; S.quitar = []; S.propuesta = null; S.editar = false;
    // Decisión 29: los vacíos que ya no caen en lo elegido se van.
    const dentro = (v) => { const c = [v.reduce((s, p) => s + p[0], 0) / v.length, v.reduce((s, p) => s + p[1], 0) / v.length]; return elegidos().some((x) => dentroDe(x.eje, c)); };
    S.vaciosPanel = S.vaciosPanel.filter(dentro);
    lateral(); pintar(); pedir();
  }

  $('side').addEventListener('click', (e) => {
    const b = e.target.closest('[data-act]'); if (!b || b.disabled) return;
    const v = b.dataset.v;
    if (b.dataset.act === 'editar') { S.editar = !S.editar; lateral(); pintar(); return; }
    if (b.dataset.act === 'vacio') { S.conVacio = !S.conVacio; S.trazo = []; lateral(); pintar(); return; }
    if (b.dataset.act === 'sin-vacio') { S.vaciosPanel = S.vaciosPanel.filter((_, i) => i !== Number(b.dataset.i)); S.borradas = []; lateral(); pintar(); pedir(); return; }
    if (b.dataset.act === 'sin-panel-vacio') { S.panelesVacios = S.panelesVacios.filter((_, i) => i !== Number(b.dataset.i)); S.borradas = []; lateral(); pintar(); pedir(); return; }
    if (b.dataset.act === 'sin-zona') return quitarZona(b.dataset.z, b.dataset.vacio === undefined ? undefined : Number(b.dataset.vacio));
    if (b.dataset.act === 'seguir') { S.seguir = v === 'true'; S.borradas = []; guardar(); ubicacion(); pedir(); return; }
    if (b.dataset.act === 'modo' && S.modo !== v) {
      // Cambiar de modo empieza de nuevo: cada uno elige de otra manera (y en C# las zonas y los perímetros comparten ids).
      S.modo = v; guardar();
      S.bordes = []; S.perimetro = []; S.zonas = []; S.zonasArm = []; S.trazo = []; S.conVacio = false; S.vaciosPanel = [];
      return elegir([]);
    }
    if (b.dataset.act === 'dir' && S.dir !== v) {
      S.dir = v; guardar();
      lateral();
      return elegir(S.paneles);
    }
  });
  $('side').addEventListener('change', (e) => {
    if (e.target.id === 'f-piso' && e.target.value) abrirPiso(e.target.value);
    if (e.target.id === 'f-borde') { S.comoBorde = e.target.checked; abrirPiso(S.piso.id); }
    if (e.target.id === 'f-tipo') { S.tipo = e.target.value; guardar(); pie(); }
  });
  let tZonas;
  $('side').addEventListener('input', (e) => {
    const k = { 'f-sep': 'sep', 'f-anc': 'anc' }[e.target.id]; if (!k) return;
    S[k] = e.target.value; S.borradas = [];
    const V = valores();
    ['sep', 'anc'].forEach((x) => { $('w-' + x).classList.toggle('bad', !!V.e[x]); $('e-' + x).textContent = V.e[x] || ''; });
    $('n4').classList.toggle('done', V.ok); $('n4').innerHTML = V.ok ? CHK : '4';
    if (V.ok) guardar();
    // El libre de los lados libres depende del ancho de vigueta: las zonas se vuelven a armar (decisión 6).
    if (k === 'anc' && porPuntos() && S.zonas.length) { clearTimeout(tZonas); tZonas = setTimeout(armarZonas, 200); return; }
    pedir();
  });

  /* ---------- dibujo de la planta ---------- */
  const pv = $('pv');
  function encuadre(c, margen) {
    const w0 = (c.x1 - c.x0) * (1 + margen * 2) || 1000, h0 = (c.y1 - c.y0) * (1 + margen * 2) || 1000;
    const r = (pv.clientWidth || 1) / (pv.clientHeight || 1);
    const w = Math.max(w0, h0 * r), h = w / r;
    return { x: (c.x0 + c.x1) / 2 - w / 2, y: -(c.y0 + c.y1) / 2 - h / 2, w, h };
  }
  function cajaPiso() {
    const pts = [];
    S.piso.armazon.forEach((a) => pts.push(a.desde, a.hasta));
    S.piso.columnas.forEach((c) => pts.push(c.min, c.max));
    (S.piso.suelos || []).forEach((s) => s.contornos.forEach((c) => pts.push(...c)));
    return pts.length ? caja(pts) : { x0: 0, x1: 1000, y0: 0, y1: 1000 };
  }
  const pt = (p) => `${p[0].toFixed(1)},${(-p[1]).toFixed(1)}`;
  /** Un camino SVG con varios anillos (un contorno y sus huecos), para pintarlo con la regla par-impar. */
  const anillos = (rs) => rs.filter((r) => r.length >= 3).map((r) => `M${r.map(pt).join('L')}Z`).join('');
  function barra(a, b, ancho, attrs, titulo) {
    const dx = b[0] - a[0], dy = b[1] - a[1], l = Math.hypot(dx, dy) || 1, nx = -dy / l * ancho / 2, ny = dx / l * ancho / 2;
    const pts = `${pt([a[0] + nx, a[1] + ny])} ${pt([b[0] + nx, b[1] + ny])} ${pt([b[0] - nx, b[1] - ny])} ${pt([a[0] - nx, a[1] - ny])}`;
    return `<polygon points="${pts}" ${attrs}>${titulo ? `<title>${esc(titulo)}</title>` : ''}</polygon>`;
  }

  function dibujo() {
    $('vacio').hidden = !!S.piso;
    if (!S.piso) { pv.querySelector('svg')?.remove(); return; }
    if (!S.vista) S.vista = encuadre(cajaPiso(), 0.1);
    const v = S.vista, fs = v.w / 80, pan = principal(), p = S.propuesta;
    const ns = 'vector-effect="non-scaling-stroke"';
    let h = `<svg viewBox="${v.x} ${v.y} ${v.w} ${v.h}" class="${S.editar ? 'editando' : ''} ${porPuntos() ? 'puntos' : ''} ${!porPuntos() && S.conVacio && !S.editar ? 'vaciando' : ''}" role="img" aria-label="Planta del piso">`;

    S.piso.paneles.forEach((x) => {
      const sel = S.paneles.includes(x.id), mudo = porPuntos();
      h += `<polygon class="pn ${sel ? 'sel' : ''} ${x.creadas ? 'con' : ''} ${mudo ? 'mudo' : ''}" ${mudo ? '' : `data-panel="${esc(x.id)}"`} points="${x.libre.map(pt).join(' ')}" ${ns}><title>${esc(x.id)}${x.creadas ? ` · ${x.creadas} viguetas creadas` : ''}</title></polygon>`;
    });
    S.perimetro.forEach((x) => {
      h += `<polygon class="pn sel" points="${x.libre.map(pt).join(' ')}" ${ns}><title>${esc(x.id)}</title></polygon>`;
    });
    // Las zonas por puntos: el libre sin sus vacíos y, encima, el contorno y los vacíos por ejes, como se marcaron.
    S.zonasArm.forEach((x) => {
      h += `<path class="pn sel zona" d="${anillos([x.libre, ...x.huecosLibre])}" ${ns}><title>${esc(x.id)}</title></path>`;
    });
    vaciosFueraDePuntos().forEach((r) => { h += `<polygon class="hueco" points="${r.map(pt).join(' ')}" ${ns}/>`; });
    S.zonas.forEach((z) => {
      h += `<polygon class="zeje" points="${z.contorno.map(pt).join(' ')}" ${ns}/>`;
      z.vacios.forEach((r) => { h += `<polygon class="hueco" points="${r.map(pt).join(' ')}" ${ns}/>`; });
    });
    // El suelo, en un color muy claro (decisión 3): encima de los paneles (que son opacos) y debajo de todo lo demás.
    (S.piso.suelos || []).forEach((s) => { h += `<path class="suelo" d="${anillos(s.contornos)}" ${ns}/>`; });
    const clase = (a) => (a.creada ? 'creada' : a.esVigueta ? 'existente' : 'viga');
    // En el modo perímetro, todo el armazón (menos lo creado) se puede elegir: vigas, viguetas de borde y voladizos.
    S.piso.armazon.forEach((a) => {
      const elegible = perimetral() && !S.editar && !a.creada;
      const titulo = elegible ? `${a.tipo || ''} · clic para ${S.bordes.includes(a.id) ? 'sacarlo del' : 'sumarlo al'} perímetro` : a.tipo || '';
      h += barra(a.desde, a.hasta, a.ancho, `class="${clase(a)} ${elegible ? 'elegible' : ''} ${S.bordes.includes(a.id) ? 'borde' : ''}" ${elegible ? `data-b="${esc(a.id)}"` : ''}`, titulo);
    });
    // Decisión 22: las viguetas del modelo dentro de los paneles elegidos van por piezas, partidas donde las cruza una
    // viga; cada una se quita por separado. Van encima de las vigas, de eje a eje, para que se vean pasar (decisión 29):
    // al pasar el mouse se ve hasta dónde llega cada pieza.
    if (p && p.modelo && p.modelo.length) {
      p.modelo.forEach((m) => {
        const a = S.piso.armazon.find((x) => x.id === m.elemento), ancho = a ? a.ancho : 100;
        if (m.quitada && !S.editar) return;
        h += m.quitada
          ? barra(m.desde, m.hasta, ancho * 1.6, `class="ghost corte" data-a="${esc(m.id)}" ${ns}`, 'Este tramo se quita al crear · clic para dejarlo')
          : barra(m.desde, m.hasta, ancho, `class="existente quitable" data-a="${esc(m.id)}"`, 'Vigueta del modelo · con Eliminar viguetas, clic para quitar este tramo');
      });
    }
    S.piso.columnas.forEach((c) => { h += `<rect class="colm" x="${c.min[0]}" y="${-c.max[1]}" width="${c.max[0] - c.min[0]}" height="${c.max[1] - c.min[1]}"/>`; });

    if (p) {
      const enX = p.direccion === 'X';
      // Decisión 12: lo eliminado solo se ve (punteado, para restaurarlo) en «Eliminar viguetas».
      p.viguetas.forEach((g) => {
        if (g.borrada && !S.editar) return;
        h += g.borrada
          ? barra(g.ejeDesde, g.ejeHasta, p.ancho, `class="ghost" data-v="${esc(g.id)}" ${ns}`, 'Eliminada · clic para restaurar')
          : barra(g.ejeDesde, g.ejeHasta, p.ancho, `class="vg" data-v="${esc(g.id)}"`, `Vigueta ${g.id} · Tipo ${g.tipo} · ${fmt(g.largo)} m libres`);
        if (!g.borrada && fs < 260) {
          h += `<text class="vt halo" x="${g.desde[0] + (enX ? fs * 0.4 : p.ancho)}" y="${-g.desde[1] - (enX ? p.ancho : fs * 0.4)}" font-size="${fs * 0.62}" stroke-width="${fs * 0.25}">T${g.tipo}</text>`;
        }
      });
      // Las viguetas de borde de los lados libres (decisión 7): se quitan y se restauran como las demás.
      (p.bordes || []).forEach((g) => {
        if (g.borrada && !S.editar) return;
        h += g.borrada
          ? barra(g.desde, g.hasta, p.ancho, `class="ghost" data-v="${esc(g.id)}" ${ns}`, 'Vigueta de borde eliminada · clic para restaurar')
          : barra(g.desde, g.hasta, p.ancho, `class="vg vb" data-v="${esc(g.id)}"`, `Vigueta de borde ${g.id} · ${fmt(g.largo)} m entre ejes`);
      });
      // Decisión 18: una zona repartida por regiones trae un reparto por región; cada una lleva sus cotas en su lugar.
      // Las regiones con el mismo reparto (mismo ancho y mismas viguetas) llevan sus cotas una sola vez, en la de más arriba
      // o más a la izquierda, para que no se repitan en cada panel.
      const deEste = pan ? p.paneles.filter((x) => x.id === pan.id) : [];
      const vistas = new Set();
      (deEste.some((x) => x.region) ? deEste.filter((x) => x.region).sort((a, b) => (enX ? a.region.wDesde - b.region.wDesde : b.region.wHasta - a.region.wHasta)) : deEste.slice(0, 1))
        .forEach((r) => {
          const clave = r.region ? [r.region.desde, r.region.hasta, r.cantidad, r.libreExtremos].map(Math.round).join('|') : '';
          if (vistas.has(clave)) return;
          vistas.add(clave);
          h += cotas(pan, r, p, fs);
        });
    }
    if (!porPuntos() && S.conVacio && !S.editar) h += capaPuntos(fs, ns);
    if (porPuntos()) {
      S.zonasArm.forEach((x) => {
        if (p) return;
        const c = caja(x.libre);
        h += `<text class="lbl halo" x="${(c.x0 + c.x1) / 2}" y="${-(c.y0 + c.y1) / 2}" text-anchor="middle" font-size="${fs * 0.95}" stroke-width="${fs * 0.3}">${esc(x.id)}</text>`;
      });
      if (!S.editar) h += capaPuntos(fs, ns);
    }
    S.piso.paneles.forEach((x) => {
      if (porPuntos()) return;
      if (p && S.paneles.includes(x.id)) return;
      const c = caja(x.libre), cx = (c.x0 + c.x1) / 2, cy = -(c.y0 + c.y1) / 2;
      const chico = Math.min(c.x1 - c.x0, c.y1 - c.y0) < fs * 3;
      h += `<text class="lbl halo" x="${cx}" y="${cy}" text-anchor="middle" font-size="${fs * (chico ? 0.6 : 0.95)}" stroke-width="${fs * 0.3}">${esc(x.id)}</text>`;
      if (x.creadas && !chico) h += `<text class="lbl2 halo" x="${cx}" y="${cy + fs * 1.1}" text-anchor="middle" font-size="${fs * 0.6}" stroke-width="${fs * 0.25}">${x.creadas} creadas</text>`;
    });
    h += '</svg>';
    pv.querySelector('svg')?.remove();
    pv.insertAdjacentHTML('beforeend', h);
  }

  /**
   * Modo Puntos (decisión 2): el trazo en curso (lados y, con tres puntos o más, el cierre punteado al primero) y los
   * puntos que se pueden elegir. Cada punto es su propio círculo: solo se elige el que recibe el clic.
   */
  function capaPuntos(fs, ns) {
    const t = S.trazo, px = S.vista.w / (pv.clientWidth || 1), r = RADIO_PUNTO * px;
    let h = '';
    if (t.length > 1) h += `<polyline class="trazo ${S.conVacio ? 'vac' : ''}" points="${t.map(pt).join(' ')}" ${ns}/>`;
    if (t.length > 2 && enLineaRecta(t[t.length - 1], t[0])) h += `<line class="trazo cierre ${S.conVacio ? 'vac' : ''}" x1="${t[t.length - 1][0]}" y1="${-t[t.length - 1][1]}" x2="${t[0][0]}" y2="${-t[0][1]}" ${ns}/>`;
    // Decisión 9: sin acercar no se enciman; a menos de SEPARACION_PUNTOS px se dibuja el de más prioridad (los del trazo
    // siempre). Al acercar aparecen los demás.
    const lejos = SEPARACION_PUNTOS * px, dibujados = [];
    const orden = (S.piso.puntos || []).map((q, i) => ({ q, i, k: t.findIndex((x) => mismoPunto(x, q.xy)) }))
      .sort((a, b) => (b.k >= 0) - (a.k >= 0) || TIPOS_PUNTO.findIndex(([v]) => v === a.q.tipo) - TIPOS_PUNTO.findIndex(([v]) => v === b.q.tipo));
    orden.forEach(({ q, i, k }) => {
      if (k < 0 && dibujados.some((d) => Math.hypot(d[0] - q.xy[0], d[1] - q.xy[1]) < lejos)) return;
      dibujados.push(q.xy);
      const primero = k === 0 && t.length > 2 && enLineaRecta(t[t.length - 1], t[0]), nombre = (TIPOS_PUNTO.find(([v]) => v === q.tipo) || [])[2] || 'Punto';
      const titulo = primero ? 'Clic para cerrar el contorno' : k >= 0 ? `Punto ${k + 1} del trazo` : nombre;
      h += `<circle class="pto t-${q.tipo} ${k >= 0 ? 'tr' : ''} ${primero ? 'primero' : ''}" data-pt="${i}" cx="${q.xy[0]}" cy="${-q.xy[1]}" r="${primero ? r * 1.6 : r}" ${ns}><title>${titulo}</title></circle>`;
    });
    return h;
  }

  /** Un clic en un punto: lo suma al trazo, o cierra si es el primero (con tres o más). Uno ya marcado no se repite. */
  function clicPunto(i) {
    const q = S.piso.puntos[i];
    if (!q) return;
    const k = S.trazo.findIndex((x) => mismoPunto(x, q.xy));
    if (k === 0 && S.trazo.length >= 3) return cerrarTrazo();
    if (k >= 0) return;
    // Decisión 27: sin lados en diagonal. Un punto que no queda en línea con el anterior no se agrega.
    if (S.trazo.length && !enLineaRecta(S.trazo[S.trazo.length - 1], q.xy)) {
      aviso('Ese punto no queda en línea con el anterior: las viguetas no van en triángulo. Marca primero la esquina.');
      return;
    }
    S.trazo = [...S.trazo, q.xy];
    pintar();
  }

  /**
   * Cotas del panel principal: libre en los extremos, eje a eje y el total, como en la maqueta. Con <r.region> (decisión
   * 18), las de esa región: su ancho, sus viguetas y al costado de donde empieza a lo largo. Cada cota dice lo que mide.
   */
  function cotas(pan, r, p, fs) {
    const c = caja(pan.libre), b = p.ancho, enY = p.direccion === 'X', g0 = r.region;
    const W = (q) => (enY ? q[0] : q[1]);
    const enRegion = (g) => !g0 || (g.cota >= g0.desde && g.cota <= g0.hasta
      && Math.min(Math.max(W(g.desde), W(g.hasta)), g0.wHasta) - Math.max(Math.min(W(g.desde), W(g.hasta)), g0.wDesde) > 1);
    const ejes = [...new Set(p.viguetas.filter((g) => g.panel === pan.id && !g.borrada && enRegion(g)).map((g) => g.cota))].sort((x, y) => x - y);
    if (!ejes.length) return '';
    const desde = g0 ? g0.desde : enY ? c.y0 : c.x0, hasta = g0 ? g0.hasta : enY ? c.y1 : c.x1;
    const tramos = [[desde, ejes[0] - b / 2, ejes[0] - b / 2 - desde, 'lib']];
    for (let i = 0; i + 1 < ejes.length; i++) tramos.push([ejes[i], ejes[i + 1], ejes[i + 1] - ejes[i], 'eje']);
    tramos.push([ejes[ejes.length - 1] + b / 2, hasta, hasta - (ejes[ejes.length - 1] + b / 2), 'lib']);
    // Al costado de la región, a lo largo: a la izquierda (↔ X) o arriba (↕ Y).
    c.x0 = enY && g0 ? g0.wDesde : c.x0;
    c.y1 = !enY && g0 ? g0.wHasta : c.y1;
    const t = fs * 0.3, d1 = fs * 2, d2 = fs * 4;
    const ln = (x1, y1, x2, y2) => `<line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" vector-effect="non-scaling-stroke"/>`;
    const txt = (x, y, s, rot) => `<text x="${x}" y="${y}" text-anchor="middle" font-size="${fs * 0.72}" class="halo" stroke-width="${fs * 0.25}" ${rot ? `transform="rotate(-90 ${x} ${y})"` : ''}>${s}</text>`;
    let h = '';
    const cota = (a, z, lab, cls, off) => {
      if (enY) {
        const x = c.x0 - off, y1 = -z, y2 = -a;
        h += `<g class="dim ${cls}">${ln(x, y1, x, y2)}${ln(x - t, y1 + t, x + t, y1 - t)}${ln(x - t, y2 + t, x + t, y2 - t)}${txt(x - fs * 0.45, (y1 + y2) / 2, lab, true)}</g>`;
      } else {
        const y = -c.y1 - off;
        h += `<g class="dim ${cls}">${ln(a, y, z, y)}${ln(a - t, y + t, a + t, y - t)}${ln(z - t, y + t, z + t, y - t)}${txt((a + z) / 2, y - fs * 0.45, lab, false)}</g>`;
      }
    };
    tramos.forEach(([a, z, val, cls]) => cota(a, z, fmt(val), cls, d1));
    cota(desde, hasta, `${fmt(hasta - desde)} m`, 'tot', d2);
    return h;
  }

  /* ---------- acercar, mover y elegir ---------- */
  pv.addEventListener('wheel', (e) => {
    if (!S.vista) return;
    e.preventDefault();
    const r = pv.getBoundingClientRect(), f = e.deltaY > 0 ? 1.15 : 1 / 1.15, v = S.vista;
    const px = v.x + (e.clientX - r.left) / r.width * v.w, py = v.y + (e.clientY - r.top) / r.height * v.h;
    S.vista = { x: px - (px - v.x) * f, y: py - (py - v.y) * f, w: v.w * f, h: v.h * f };
    dibujo();
  }, { passive: false });

  let arrastre = null;
  pv.addEventListener('mousedown', (e) => { if (S.vista && e.button === 0) arrastre = { x: e.clientX, y: e.clientY, v: S.vista, movio: false }; });
  window.addEventListener('mousemove', (e) => {
    if (!arrastre) return;
    const dx = e.clientX - arrastre.x, dy = e.clientY - arrastre.y;
    if (!arrastre.movio && Math.hypot(dx, dy) < 4) return;
    arrastre.movio = true; pv.classList.add('arrastre');
    const r = pv.getBoundingClientRect(), v = arrastre.v;
    S.vista = { ...v, x: v.x - dx / r.width * v.w, y: v.y - dy / r.height * v.h };
    dibujo();
  });
  window.addEventListener('mouseup', (e) => {
    const a = arrastre; arrastre = null; pv.classList.remove('arrastre');
    if (!a || a.movio || !pv.contains(e.target)) return;
    const vg = e.target.closest('[data-v]'), pn = e.target.closest('[data-panel]'), ma = e.target.closest('[data-a]');
    if (S.editar && ma && !vg) {
      const id = ma.dataset.a;
      S.quitar = S.quitar.includes(id) ? S.quitar.filter((x) => x !== id) : [...S.quitar, id];
      lateral(); pedir();
    } else if (S.editar && vg) {
      const id = vg.dataset.v;
      S.borradas = S.borradas.includes(id) ? S.borradas.filter((x) => x !== id) : [...S.borradas, id];
      lateral(); pedir();
    } else if (!S.editar && (porPuntos() || S.conVacio)) {
      const q = e.target.closest('[data-pt]');
      if (q) clicPunto(Number(q.dataset.pt));
      else if (S.conVacio && !S.trazo.length && pn) vaciarPanel(pn.dataset.panel);
    } else if (!S.editar && perimetral()) {
      const b = e.target.closest('[data-b]');
      if (!b) return;
      const id = b.dataset.b;
      S.bordes = S.bordes.includes(id) ? S.bordes.filter((x) => x !== id) : [...S.bordes, id];
      armar(); lateral(); pintar();
    } else if (!S.editar && pn) {
      const id = pn.dataset.panel;
      if (e.ctrlKey || e.shiftKey || e.metaKey) elegir(S.paneles.includes(id) ? S.paneles.filter((x) => x !== id) : [...S.paneles, id]);
      else if (S.paneles.join() !== id) elegir([id]);
    }
  });
  // Decisión 14: el trazo también se cierra con Enter, clic derecho o doble clic, sin volver al primer punto.
  const puedeCerrar = () => (porPuntos() || S.conVacio) && !S.editar && S.trazo.length >= 3;
  pv.addEventListener('contextmenu', (e) => { if (puedeCerrar()) { e.preventDefault(); cerrarTrazo(); } });
  pv.addEventListener('dblclick', () => { if (puedeCerrar()) cerrarTrazo(); });
  window.addEventListener('keydown', (e) => {
    if (!$('m-crear').hidden) { if (e.key === 'Escape') $('m-crear').hidden = true; return; }
    if (e.key === 'Enter' && puedeCerrar()) { e.preventDefault(); cerrarTrazo(); return; }
    if (e.key !== 'Escape') return;
    if (S.editar) S.editar = false;
    else if (S.trazo.length || S.conVacio) { S.trazo = []; S.conVacio = false; }
    else return;
    lateral(); pintar();
  });
  window.addEventListener('resize', () => { if (S.piso) { S.vista = null; dibujo(); } });
  $('b-todo').addEventListener('click', () => { if (S.piso) { S.vista = encuadre(cajaPiso(), 0.1); dibujo(); } });
  $('b-panel').addEventListener('click', () => {
    const sel = elegidos(); if (!sel.length) return;
    S.vista = encuadre(caja(sel.flatMap((p) => p.libre)), 0.35); dibujo();
  });

  /* ---------- franjas, pie y avisos ---------- */
  function franjas() {
    const sel = elegidos(), p = S.propuesta, V = valores();
    $('chips').innerHTML = (S.piso ? `<span class="cchip mono">${esc(S.piso.nombre)}</span><span class="cchip">${S.piso.paneles.length} paneles</span>` : '')
      + (sel.length ? `<span class="cchip ok"><i></i>${esc(lista(S.paneles))}</span><span class="cchip mono">${S.dir === 'X' ? '↔' : '↕'} ${S.dir}</span>` : (S.piso ? '<span class="cchip warn"><i></i>Sin panel elegido</span>' : ''));
    $('b-panel').disabled = !sel.length;
    $('b-panel').textContent = sel.length > 1 ? 'Acercar a los paneles' : 'Acercar al panel';
    const partidas = p && p.viguetas.some((g) => /[a-z]$/.test(g.id));
    const vecinos = sel.length === 1 && S.modo === 'uno' && S.sinAviso !== sel[0].id ? enLinea(sel[0].id) : [];
    const n = S.trazo.length, que = S.conVacio ? 'del vacío' : 'del contorno';
    // Decisión 27: se cierra solo en cuadrado (el último en línea con el primero).
    const cierra = n >= 3 && enLineaRecta(S.trazo[n - 1], S.trazo[0]);
    const avisoPuntos = (porPuntos() || S.conVacio) && !S.editar && S.piso
      ? `<div class="banner hint ${S.conVacio ? 'vac' : ''}"><span>${n
        ? `<b>${n}</b> punto${n > 1 ? 's' : ''} ${que}. ${cierra ? 'Para cerrar: <b>Enter</b>, clic derecho, doble clic, clic en el primero o <b>Cerrar contorno</b>.' : n >= 3 ? 'Para cerrar en cuadrado, marca un punto en línea con el primero.' : 'Sigue marcando puntos en línea (sin diagonales).'}`
        : S.conVacio ? (porPuntos() ? 'Marca los puntos del <b>vacío</b>, dentro de una zona: las viguetas paran en él.' : 'Haz clic en un <b>panel</b> o marca sus puntos.')
          : S.zonas.length ? `<b>${esc(lista(S.zonas.map((z) => z.id)))}</b> armada${S.zonas.length > 1 ? 's' : ''}. Marca otra zona, separada o no, o un <b>＋ Vacío</b>.`
            : 'Haz clic en los <b>puntos</b> del contorno: columnas, extremos, cruces de ejes o vértices del suelo.'}</span>${n ? `<button class="bb" data-act="atras">Quitar el último</button><button class="bb" data-act="cancelar">Cancelar</button><button class="bb azul" data-act="cerrar" ${cierra ? '' : 'disabled'}>Cerrar contorno</button>` : ''}</div>`
      : '';
    const avisoPerimetro = perimetral() && !S.editar && S.piso
      ? `<div class="banner hint"><span>${sel.length ? `<b>${esc(lista(S.paneles))}</b>: perímetro cerrado con ${S.bordes.length} elementos. Clic en otro elemento para cambiarlo.` : `Haz clic en las <b>vigas, viguetas de borde o voladizos</b> que forman el perímetro${S.bordes.length ? ` · llevas ${S.bordes.length}, todavía no cierran` : ''}.`}</span>${S.bordes.length ? '<button class="bb" data-act="limpiar">Empezar de nuevo</button>' : ''}</div>`
      : '';
    const enColumna = vecinos.length > 1
      ? `<div class="banner hint"><span><b>${esc(sel[0].id)}</b> está en una ${linea()} con ${esc(lista(vecinos.filter((x) => x !== sel[0].id)))}. Sus viguetas siguen por ella hasta donde lleguen.</span><button class="bb" data-act="solo">Solo ${esc(sel[0].id)}</button><button class="bb azul" data-act="toda">Toda la ${linea()} (${vecinos.length} paneles)</button></div>`
      : '';
    $('msg').innerHTML = S.editar
      ? `<div class="banner"><span><b>Eliminar viguetas:</b> clic sobre una vigueta para quitarla o restaurarla. Las del modelo (azul oscuro) también se quitan por tramos, de cada lado de la viga que las cruza${partidas ? '. Las que cruzan una viga están partidas: quita el lado que no va' : ''}.</span><button class="bb" data-act="deshacer" ${S.borradas.length + S.quitar.length ? '' : 'disabled'}>Deshacer</button><button class="bb solid" data-act="listo">Listo</button></div>`
      : avisoPuntos || avisoPerimetro || (S.piso && !sel.length ? '<div class="banner hint"><span>Haz clic en un panel de la planta para ver sus viguetas. Con <b>Ctrl + clic</b> sumas o quitas paneles.</span></div>'
        : (enColumna || (partidas ? '<div class="banner hint"><span>Hay viguetas que cruzan una viga. Con <b>Eliminar viguetas</b> puedes quitar solo uno de los lados.</span></div>' : '')));
    const m = (l, v) => `<div class="m"><span class="m-l">${l}</span><span class="m-v">${v}</span></div>`;
    let ext = '—';
    if (p && p.paneles.length) {
      const vals = p.paneles.flatMap((r) => [r.libreExtremos, r.libreFinal]), lo = Math.min(...vals), hi = Math.max(...vals);
      ext = Math.round(lo) === Math.round(hi) ? `${fmt(lo)} m` : `${fmt(lo)}–${fmt(hi)} m`;
    }
    let pie = '';
    if (!S.piso) pie = '<span class="msg-warn">Elige un piso.</span>';
    else if (!sel.length) pie = '<span class="msg-warn">Elige un panel.</span>';
    else if (!V.ok) pie = '<span class="msg-warn">Revisa la separación y el ancho.</span>';
    const deBorde = p ? (p.bordes || []).filter((g) => !g.borrada).length : 0;
    if (p && S.piso && sel.length && V.ok) pie = `<span class="tipos">${p.tipos.map((t) => `<span class="tc" title="Viguetas iguales van en el mismo tipo">T${t.numero} · ${fmt(t.largo)} m × ${t.cantidad}</span>`).join('')}${deBorde ? `<span class="tc" title="En los lados sin viga, con la cara al ras del lado, hacia adentro de la losa">De borde × ${deBorde}</span>` : ''}${S.borradas.length ? `<span class="tc del">Eliminadas × ${S.borradas.length}</span>` : ''}${S.quitar.length ? `<span class="tc del">Del modelo × ${S.quitar.length}</span>` : ''}</span>`;
    // Decisión 20: un libre de extremo mayor que el máximo se avisa.
    const grandes = p ? p.paneles.filter((r) => Math.max(r.libreExtremos, r.libreFinal) > r.libreMaximo + 1) : [];
    if (grandes.length && V.ok) pie = `<span class="msg-warn">${esc(lista(grandes.map((r) => r.id)))}: queda un libre de ${fmt(Math.max(grandes[0].libreExtremos, grandes[0].libreFinal))} m, más que el máximo de ${fmt(grandes[0].libreMaximo)} m.</span>${pie}`;
    $('mt').innerHTML = `<div class="mt-row">${m('Viguetas a crear', p ? p.lineas : '—')}${m('Eje a eje', p && p.paneles.length ? ejeAEje(p) : '—')}${m('Libre en extremos', ext)}</div>
      <div class="mt-foot">${pie}<span class="tag" title="Cada panel: cantidad = REDONDEAR.MENOS(libre / eje a eje); extremos = (libre − ((cantidad − 1) · eje a eje + ancho)) / 2; si pasan del libre máximo se agrega una vigueta.">${origen(p)}</span></div>`;
  }
  /** El eje a eje de la propuesta: uno solo o, si cada panel o región tiene el suyo, del menor al mayor. */
  function ejeAEje(p) {
    const vals = p.paneles.filter((r) => r.cantidad > 1).map((r) => r.ejeAEje);
    if (!vals.length) return `${fmt(p.paneles[0].ejeAEje)} m`;
    const lo = Math.min(...vals), hi = Math.max(...vals);
    return Math.round(lo) === Math.round(hi) ? `${fmt(lo)} m` : `${fmt(lo)}–${fmt(hi)} m`;
  }
  /** De dónde salen las líneas: la fórmula, las del primer panel de la línea o las viguetas que ya están. */
  function origen(p) {
    if (!p || !p.paneles.length) return 'Fórmula del Excel de Carlos';
    const siguen = p.paneles.filter((r) => r.origen === 'Existentes').map((r) => r.id);
    const alineadas = p.paneles.filter((r) => r.origen === 'Alineado');
    if (siguen.length) return `${esc(lista(siguen))}: siguen las viguetas que ya están`;
    // Decisión 25: en una zona, los paneles siguen las líneas del de al lado (a través de la viga) que manda.
    if (alineadas.some((r) => r.region)) return 'Fórmula del Excel · los paneles siguen las líneas del vecino que manda (vacío o viga corta)';
    if (alineadas.length) return `Fórmula del Excel en ${esc(alineadas[0].guia)} · ${esc(lista(alineadas.map((r) => r.id)))} alineadas con ${esc(alineadas[0].guia)}`;
    return `Fórmula del Excel de Carlos · libre máx. ${fmt(p.paneles[0].libreMaximo)} m`;
  }

  $('msg').addEventListener('click', (e) => {
    const b = e.target.closest('[data-act]'); if (!b || b.disabled) return;
    // Deshacer quita la última marca, sea de la propuesta o del modelo.
    if (b.dataset.act === 'deshacer') { if (S.quitar.length) S.quitar = S.quitar.slice(0, -1); else S.borradas = S.borradas.slice(0, -1); pedir(); }
    if (b.dataset.act === 'listo') S.editar = false;
    if (b.dataset.act === 'toda') return elegir(enLinea(S.paneles[0]));
    if (b.dataset.act === 'solo') { S.sinAviso = S.paneles[0]; S.borradas = []; pintar(); pedir(); return; }
    if (b.dataset.act === 'limpiar') { S.bordes = []; S.perimetro = []; return elegir([]); }
    if (b.dataset.act === 'cerrar') return cerrarTrazo();
    if (b.dataset.act === 'atras') S.trazo = S.trazo.slice(0, -1);
    if (b.dataset.act === 'cancelar') { S.trazo = []; S.conVacio = false; }
    lateral(); pintar();
  });

  function pie() {
    const n = creadas();
    $('doc').textContent = S.doc;
    // La propuesta llega después de elegir el panel: sin esto el botón quedaba desactivado.
    if ($('b-editar')) $('b-editar').disabled = !S.propuesta;
    $('b-quitar').disabled = !n;
    $('b-quitar').textContent = n ? `Quitar creadas (${n})` : 'Quitar creadas';
    $('b-crear').disabled = !((S.propuesta && (vivas().length || S.quitar.length) || soloVacios()) && S.tipo);
    $('b-crear').textContent = soloVacios() ? 'Llevar vacíos a otros pisos' : n ? 'Reemplazar viguetas' : 'Crear viguetas';
  }
  $('b-crear').addEventListener('click', () => { if (S.pisos.length > 1) abrirCrear(); else crearEn([S.piso.id]); });
  $('b-quitar').addEventListener('click', () => { enviar({ tipo: 'quitar', paneles: S.paneles }); aviso('Quitando las viguetas creadas…'); });
  $('b-cerrar').addEventListener('click', () => enviar({ tipo: 'cerrar' }));

  let tAviso;
  function aviso(texto, bien) {
    const t = $('toast'); t.textContent = texto; t.classList.toggle('bien', !!bien); t.classList.add('show');
    clearTimeout(tAviso); tAviso = setTimeout(() => t.classList.remove('show'), Math.max(3200, texto.length * 65));
  }

  function pintar() { dibujo(); franjas(); pie(); if ($('sum')) resumen(); if ($('ubic')) ubicacion(); }
  function todo() { lateral(); pintar(); }

  /* ---------- arranque ---------- */
  const webview = window.chrome && window.chrome.webview;
  todo();
  if (webview) {
    enviar = (m) => webview.postMessage(m);
    webview.addEventListener('message', (e) => recibir(e.data));
    enviar({ tipo: 'listo' });
  } else {
    // Fuera de Revit: un piso de ejemplo para ver la página (viguetas-demo.js).
    const s = document.createElement('script');
    s.src = 'viguetas-demo.js';
    s.onload = () => { const demo = window.DEMO_VIGUETAS(); enviar = (m) => demo(m).forEach((r) => setTimeout(() => recibir(r), 0)); enviar({ tipo: 'listo' }); };
    document.head.appendChild(s);
  }
})();
