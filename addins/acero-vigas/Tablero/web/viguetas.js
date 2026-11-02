// «Crear viguetas» (#947). La página dibuja la planta del piso y pide; los paneles y el reparto (fórmula del Excel de
// Carlos) se calculan en C# (Calculo/Viguetas). Mensajes: ProtocoloViguetas.cs. Medidas en mm; en pantalla, en m.
(() => {
  const $ = (id) => document.getElementById(id);
  const CLAVE = 'cqeing-viguetas';
  const CHK = '<svg viewBox="0 0 12 12" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-label="Listo"><path d="M2.6 6.3l2.2 2.2 4.6-4.9"/></svg>';
  const ICO_BORRAR = '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"><path d="M3 4.5h10M6.5 4.5V3h3v1.5M4.5 4.5l.6 8.5h5.8l.6-8.5M7 7v4M9 7v4"/></svg>';
  // Cómo se eligen los paneles (decisiones 12 y 18 del plan): uno, la columna o fila entera, o varios con clic.
  const linea = () => (S.dir === 'Y' ? 'columna' : 'fila');
  const MODOS = () => [
    ['uno', 'Un panel', 'Un clic elige un panel'],
    ['linea', `${linea()[0].toUpperCase()}${linea().slice(1)} entera`, `Un clic elige todos los paneles de la ${linea()}`],
    ['varios', 'Varios', 'Cada clic suma o quita un panel'],
  ];

  const S = {
    doc: '', pisos: [], tipos: [], piso: null, comoBorde: false, modo: 'uno', paneles: [],
    dir: 'X', sep: '0,80', anc: '0,10', tipo: '', continuas: true, seguir: true, borradas: [], quitar: [], editar: false, propuesta: null, vista: null,
  };
  try {
    const m = JSON.parse(localStorage.getItem(CLAVE) || 'null');
    if (m) Object.assign(S, { sep: m.sep || S.sep, anc: m.anc || S.anc, tipo: m.tipo || '', dir: m.dir || S.dir, modo: m.modo || S.modo, continuas: m.continuas !== false, seguir: m.seguir !== false });
  } catch (e) { /* sin almacenamiento se usan los valores por defecto */ }
  const guardar = () => {
    try { localStorage.setItem(CLAVE, JSON.stringify({ sep: S.sep, anc: S.anc, tipo: S.tipo, dir: S.dir, modo: S.modo, continuas: S.continuas, seguir: S.seguir })); } catch (e) { /* nada */ }
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
  const elegidos = () => (S.piso ? S.paneles.map((id) => S.piso.paneles.find((p) => p.id === id)).filter(Boolean) : []);
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
    return { paneles: S.paneles, direccion: S.dir, ejeAEje: V.s, ancho: V.b, borradas: S.borradas, continuas: S.continuas, seguir: S.seguir, quitarModelo: S.quitar };
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
      if (!mismo) { S.vista = null; S.paneles = []; }
      const quedan = S.paneles.filter((id) => m.paneles.some((p) => p.id === id));
      if (quedan.length !== S.paneles.length) { S.paneles = quedan; S.borradas = []; S.quitar = []; }
      // Las del modelo que se quitaron al crear ya no están en el piso.
      S.quitar = S.quitar.filter((id) => m.armazon.some((a) => a.id === id.split('#')[0]));
      if (!S.paneles.length) { S.propuesta = null; S.editar = false; }
      $('modal').hidden = true;
      todo();
      if (S.paneles.length) pedir();
    } else if (m.tipo === 'propuesta') {
      if (m.paneles.join() === S.paneles.join()) { S.propuesta = m.propuesta; pintar(); }
    } else if (m.tipo === 'aviso') {
      aviso(m.texto, m.bien);
    }
  }

  /* ---------- primera entrada ---------- */
  function modal() {
    $('m-pisos').innerHTML = S.pisos.length
      ? S.pisos.map((p) => `<button class="piso" data-piso="${esc(p.id)}"><span class="lv">${esc(p.nombre)}</span><span>${fmt(p.cota)} m</span><span class="pc">${p.armazon} vigas y viguetas</span></button>`).join('')
      : '<p>Este modelo no tiene vigas con nivel de referencia.</p>';
    $('modal').hidden = !!S.piso;
  }
  $('m-pisos').addEventListener('click', (e) => { const b = e.target.closest('[data-piso]'); if (b) abrirPiso(b.dataset.piso); });

  /* ---------- barra lateral ---------- */
  function lateral() {
    const sel = elegidos(), pan = principal(), V = valores();
    const n = (k, ok, extra = '') => `<span class="n ${ok ? 'done' : ''}" ${extra}>${ok ? CHK : k}</span>`;
    const campo = (id, k, lab) => `<label class="fld ${V.e[k] ? 'bad' : ''}" id="w-${k}" for="${id}"><span class="fl">${lab}</span><span class="inp"><input id="${id}" inputmode="decimal" autocomplete="off" value="${esc(S[k])}"><em>m</em></span><span class="err" id="e-${k}">${V.e[k] || ''}</span></label>`;
    let infoPanel = `<span class="info">${S.modo === 'varios' ? 'Haz clic en los paneles que quieras.' : 'Haz clic en un panel de la planta.'}</span>`;
    if (pan) {
      const c = caja(pan.libre);
      infoPanel = (sel.length > 1
        ? `<span class="info"><b>${sel.length} paneles</b> · ${esc(lista(S.paneles))}</span>`
        : `<span class="info"><b>${esc(pan.id)}</b> · libre <b>${fmt(c.x1 - c.x0)} × ${fmt(c.y1 - c.y0)}</b> m</span>`)
        + (creadas() ? `<span class="info">Ya tiene${sel.length > 1 ? 'n' : ''} <b>${creadas()}</b> viguetas creadas por CQEING.</span>` : '');
    }
    $('side').innerHTML = `
      <div class="sec"><div class="sh">${n(1, !!S.piso)}Piso</div>
        <select class="sel" id="f-piso" aria-label="Piso">${S.piso ? '' : '<option value="">Elige un piso</option>'}${S.pisos.map((p) => `<option value="${esc(p.id)}" ${S.piso && S.piso.id === p.id ? 'selected' : ''}>${esc(p.nombre)}</option>`).join('')}</select>
        <label class="chk" title="Las viguetas que ya están en el modelo también cierran paneles (viguetas de borde)"><input type="checkbox" id="f-borde" ${S.comoBorde ? 'checked' : ''} ${S.piso ? '' : 'disabled'}>Usar las viguetas del modelo como borde</label></div>
      <div class="sec ${S.piso ? '' : 'off'}"><div class="sh">${n(2, !!pan)}Paneles</div>
        <div class="modos" role="group" aria-label="Cómo elegir paneles">${MODOS().map(([v, l, t]) => `<button data-act="modo" data-v="${v}" aria-pressed="${S.modo === v}" title="${t}">${l}</button>`).join('')}</div>
        <span class="info tip">Con <b>Ctrl + clic</b> sumas o quitas paneles.</span>
        ${infoPanel}
        <button class="ib ${S.editar ? 'on' : ''}" id="b-editar" data-act="editar" ${S.propuesta ? '' : 'disabled'} aria-pressed="${S.editar}" title="Clic sobre una vigueta para quitarla; si cruza una viga, cada lado se quita por separado">${ICO_BORRAR}Eliminar viguetas${S.borradas.length + S.quitar.length ? `<span class="k">${S.borradas.length + S.quitar.length}</span>` : ''}</button></div>
      <div class="sec ${pan ? '' : 'off'}"><div class="sh">${n(3, !!pan)}Dirección</div>
        <div class="dirs">${[['X', '↔', 'Viguetas paralelas a X'], ['Y', '↕', 'Viguetas paralelas a Y']].map(([v, a, t]) => `<button class="dirbtn" data-act="dir" data-v="${v}" aria-pressed="${S.dir === v}" title="${t}"><span class="arr">${a}</span><b>${v}</b></button>`).join('')}</div></div>
      <div class="sec ${pan ? '' : 'off'}"><div class="sh">${n(4, !!pan && V.ok, 'id="n4"')}Separación</div>
        <div class="campos">${campo('f-sep', 'sep', 'Eje a eje')}${campo('f-anc', 'anc', 'Ancho de vigueta')}</div>
        <label class="fld" for="f-tipo"><span class="fl">Tipo de vigueta en Revit</span>
          <select class="sel" id="f-tipo">${S.tipos.map((t) => `<option value="${esc(t.id)}" ${t.id === S.tipo ? 'selected' : ''}>${esc(t.nombre)}</option>`).join('')}</select></label>
        <div id="ubic"></div>
        <label class="chk" title="Las viguetas en la misma línea que se tocan en una viga se crean como una sola, de punta a punta"><input type="checkbox" id="f-continuas" ${S.continuas ? 'checked' : ''}>Viguetas continuas a través de las vigas</label></div>
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

  /** Cambia los paneles elegidos: la propuesta y las eliminadas se reinician. */
  function elegir(ids) {
    S.paneles = ids; S.borradas = []; S.quitar = []; S.propuesta = null; S.editar = false;
    lateral(); pintar(); pedir();
  }

  $('side').addEventListener('click', (e) => {
    const b = e.target.closest('[data-act]'); if (!b || b.disabled) return;
    const v = b.dataset.v;
    if (b.dataset.act === 'editar') { S.editar = !S.editar; lateral(); pintar(); return; }
    if (b.dataset.act === 'seguir') { S.seguir = v === 'true'; S.borradas = []; guardar(); ubicacion(); pedir(); return; }
    if (b.dataset.act === 'modo' && S.modo !== v) {
      S.modo = v; guardar();
      const pan = principal();
      return elegir(!pan ? [] : v === 'linea' ? enLinea(pan.id) : v === 'uno' ? [pan.id] : S.paneles);
    }
    if (b.dataset.act === 'dir' && S.dir !== v) {
      S.dir = v; guardar();
      const pan = principal();
      lateral();
      return elegir(pan && S.modo === 'linea' ? enLinea(pan.id) : S.paneles);
    }
  });
  $('side').addEventListener('change', (e) => {
    if (e.target.id === 'f-piso' && e.target.value) abrirPiso(e.target.value);
    if (e.target.id === 'f-borde') { S.comoBorde = e.target.checked; abrirPiso(S.piso.id); }
    if (e.target.id === 'f-tipo') { S.tipo = e.target.value; guardar(); pie(); }
    if (e.target.id === 'f-continuas') { S.continuas = e.target.checked; guardar(); pedir(); }
  });
  $('side').addEventListener('input', (e) => {
    const k = { 'f-sep': 'sep', 'f-anc': 'anc' }[e.target.id]; if (!k) return;
    S[k] = e.target.value; S.borradas = [];
    const V = valores();
    ['sep', 'anc'].forEach((x) => { $('w-' + x).classList.toggle('bad', !!V.e[x]); $('e-' + x).textContent = V.e[x] || ''; });
    $('n4').classList.toggle('done', V.ok); $('n4').innerHTML = V.ok ? CHK : '4';
    if (V.ok) guardar();
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
    return pts.length ? caja(pts) : { x0: 0, x1: 1000, y0: 0, y1: 1000 };
  }
  const pt = (p) => `${p[0].toFixed(1)},${(-p[1]).toFixed(1)}`;
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
    let h = `<svg viewBox="${v.x} ${v.y} ${v.w} ${v.h}" class="${S.editar ? 'editando' : ''}" role="img" aria-label="Planta del piso">`;

    S.piso.paneles.forEach((x) => {
      const sel = S.paneles.includes(x.id);
      h += `<polygon class="pn ${sel ? 'sel' : ''} ${x.creadas ? 'con' : ''}" data-panel="${esc(x.id)}" points="${x.libre.map(pt).join(' ')}" ${ns}><title>${esc(x.id)}${x.creadas ? ` · ${x.creadas} viguetas creadas` : ''}</title></polygon>`;
    });
    const clase = (a) => (a.creada ? 'creada' : a.esVigueta ? 'existente' : 'viga');
    S.piso.armazon.forEach((a) => { h += barra(a.desde, a.hasta, a.ancho, `class="${clase(a)}"`, a.tipo || ''); });
    // Decisión 22: las viguetas del modelo dentro de los paneles elegidos van por piezas, partidas donde las cruza una
    // viga; cada una se quita por separado. Las vigas se dibujan otra vez encima, para que se vea dónde cortan.
    if (p && p.modelo && p.modelo.length) {
      p.modelo.forEach((m) => {
        const a = S.piso.armazon.find((x) => x.id === m.elemento), ancho = a ? a.ancho : 100;
        h += m.quitada
          ? barra(m.desde, m.hasta, ancho * 1.6, `class="ghost corte" data-a="${esc(m.id)}" ${ns}`, 'Este tramo se quita al crear · clic para dejarlo')
          : barra(m.desde, m.hasta, ancho, `class="existente quitable" data-a="${esc(m.id)}"`, 'Vigueta del modelo · con Eliminar viguetas, clic para quitar este tramo');
      });
      S.piso.armazon.filter((a) => !a.esVigueta && !a.creada).forEach((a) => { h += barra(a.desde, a.hasta, a.ancho, 'class="viga"'); });
    }
    S.piso.columnas.forEach((c) => { h += `<rect class="colm" x="${c.min[0]}" y="${-c.max[1]}" width="${c.max[0] - c.min[0]}" height="${c.max[1] - c.min[1]}"/>`; });

    if (p) {
      const enX = p.direccion === 'X';
      p.viguetas.forEach((g) => {
        h += g.borrada
          ? barra(g.desde, g.hasta, p.ancho, `class="ghost" data-v="${esc(g.id)}" ${ns}`, 'Eliminada · clic para restaurar')
          : barra(g.desde, g.hasta, p.ancho, `class="vg" data-v="${esc(g.id)}"`, `Vigueta ${g.id} · Tipo ${g.tipo} · ${fmt(g.largo)} m`);
        if (!g.borrada && fs < 260) {
          h += `<text class="vt halo" x="${g.desde[0] + (enX ? fs * 0.4 : p.ancho)}" y="${-g.desde[1] - (enX ? p.ancho : fs * 0.4)}" font-size="${fs * 0.62}" stroke-width="${fs * 0.25}">T${g.tipo}</text>`;
        }
      });
      const r = pan && p.paneles.find((x) => x.id === pan.id);
      if (r) h += cotas(pan, r, p, fs);
    }
    S.piso.paneles.forEach((x) => {
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

  /** Cotas del panel principal: libre en los extremos, eje a eje y el total, como en la maqueta. */
  function cotas(pan, r, p, fs) {
    const c = caja(pan.libre), b = p.ancho, enY = p.direccion === 'X';
    const ejes = [...new Set(p.viguetas.filter((g) => g.panel === pan.id).map((g) => g.cota))].sort((x, y) => x - y);
    if (!ejes.length) return '';
    const desde = enY ? c.y0 : c.x0, hasta = enY ? c.y1 : c.x1;
    const tramos = [[desde, ejes[0] - b / 2, r.libreExtremos, 'lib']];
    for (let i = 0; i + 1 < ejes.length; i++) tramos.push([ejes[i], ejes[i + 1], r.ejeAEje, 'eje']);
    tramos.push([ejes[ejes.length - 1] + b / 2, hasta, r.libreExtremos, 'lib']);
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
    } else if (!S.editar && pn) {
      const id = pn.dataset.panel;
      if (S.modo === 'varios' || e.ctrlKey || e.shiftKey || e.metaKey) elegir(S.paneles.includes(id) ? S.paneles.filter((x) => x !== id) : [...S.paneles, id]);
      else if (S.modo === 'linea') elegir(enLinea(id));
      else if (S.paneles.join() !== id) elegir([id]);
    }
  });
  window.addEventListener('keydown', (e) => { if (e.key === 'Escape' && S.editar) { S.editar = false; lateral(); pintar(); } });
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
    const vecinos = sel.length === 1 && S.modo !== 'varios' && S.sinAviso !== sel[0].id ? enLinea(sel[0].id) : [];
    const enColumna = vecinos.length > 1
      ? `<div class="banner hint"><span><b>${esc(sel[0].id)}</b> está en una ${linea()} con ${esc(lista(vecinos.filter((x) => x !== sel[0].id)))}. ¿Las viguetas van seguidas?</span><button class="bb" data-act="solo">Solo ${esc(sel[0].id)}</button><button class="bb azul" data-act="toda">Toda la ${linea()} (${vecinos.length} paneles)</button></div>`
      : '';
    $('msg').innerHTML = S.editar
      ? `<div class="banner"><span><b>Eliminar viguetas:</b> clic sobre una vigueta para quitarla o restaurarla. Las del modelo (azul oscuro) también se quitan por tramos, de cada lado de la viga que las cruza${partidas ? '. Las que cruzan una viga están partidas: quita el lado que no va' : ''}.</span><button class="bb" data-act="deshacer" ${S.borradas.length + S.quitar.length ? '' : 'disabled'}>Deshacer</button><button class="bb solid" data-act="listo">Listo</button></div>`
      : (S.piso && !sel.length ? `<div class="banner hint"><span>${S.modo === 'linea' ? 'Haz clic en un panel: se elige toda su ' + (S.dir === 'Y' ? 'columna' : 'fila') + '.' : S.modo === 'varios' ? 'Haz clic en los paneles que quieras.' : 'Haz clic en un panel de la planta para ver sus viguetas.'}</span></div>`
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
    else if (p) pie = `<span class="tipos">${p.tipos.map((t) => `<span class="tc" title="Viguetas iguales van en el mismo tipo">T${t.numero} · ${fmt(t.largo)} m × ${t.cantidad}</span>`).join('')}${S.borradas.length ? `<span class="tc del">Eliminadas × ${S.borradas.length}</span>` : ''}${S.quitar.length ? `<span class="tc del">Del modelo × ${S.quitar.length}</span>` : ''}</span>`;
    // Decisión 20: un libre de extremo mayor que el máximo se avisa.
    const grandes = p ? p.paneles.filter((r) => Math.max(r.libreExtremos, r.libreFinal) > r.libreMaximo + 1) : [];
    if (grandes.length && V.ok) pie = `<span class="msg-warn">${esc(lista(grandes.map((r) => r.id)))}: queda un libre de ${fmt(Math.max(grandes[0].libreExtremos, grandes[0].libreFinal))} m, más que el máximo de ${fmt(grandes[0].libreMaximo)} m.</span>${pie}`;
    $('mt').innerHTML = `<div class="mt-row">${m('Viguetas a crear', p ? p.lineas : '—')}${m('Eje a eje', p && p.paneles.length ? fmt(p.paneles[0].ejeAEje) + ' m' : '—')}${m('Libre en extremos', ext)}</div>
      <div class="mt-foot">${pie}<span class="tag" title="Cada panel: cantidad = REDONDEAR.MENOS(libre / eje a eje); extremos = (libre − ((cantidad − 1) · eje a eje + ancho)) / 2; si pasan del libre máximo se agrega una vigueta.">${origen(p)}</span></div>`;
  }
  /** De dónde salen las líneas: la fórmula, las del primer panel de la línea o las viguetas que ya están. */
  function origen(p) {
    if (!p || !p.paneles.length) return 'Fórmula del Excel de Carlos';
    const siguen = p.paneles.filter((r) => r.origen === 'Existentes').map((r) => r.id);
    const alineadas = p.paneles.filter((r) => r.origen === 'Alineado');
    if (siguen.length) return `${esc(lista(siguen))}: siguen las viguetas que ya están`;
    if (alineadas.length) return `Fórmula del Excel en ${esc(alineadas[0].guia)} · ${esc(lista(alineadas.map((r) => r.id)))} alineadas con ${esc(alineadas[0].guia)}`;
    return `Fórmula del Excel de Carlos · libre máx. ${fmt(p.paneles[0].libreMaximo)} m`;
  }

  $('msg').addEventListener('click', (e) => {
    const b = e.target.closest('[data-act]'); if (!b || b.disabled) return;
    // Deshacer quita la última marca, sea de la propuesta o del modelo.
    if (b.dataset.act === 'deshacer') { if (S.quitar.length) S.quitar = S.quitar.slice(0, -1); else S.borradas = S.borradas.slice(0, -1); pedir(); }
    if (b.dataset.act === 'listo') S.editar = false;
    if (b.dataset.act === 'toda') return elegir(enLinea(S.paneles[0]));
    if (b.dataset.act === 'solo') { S.sinAviso = S.paneles[0]; pintar(); return; }
    lateral(); pintar();
  });

  function pie() {
    const n = creadas();
    $('doc').textContent = S.doc;
    // La propuesta llega después de elegir el panel: sin esto el botón quedaba desactivado.
    if ($('b-editar')) $('b-editar').disabled = !S.propuesta;
    $('b-quitar').disabled = !n;
    $('b-quitar').textContent = n ? `Quitar creadas (${n})` : 'Quitar creadas';
    $('b-crear').disabled = !(S.propuesta && (vivas().length || S.quitar.length) && S.tipo);
    $('b-crear').textContent = n ? 'Reemplazar viguetas' : 'Crear viguetas';
  }
  $('b-crear').addEventListener('click', () => { enviar({ tipo: 'crear', ...pedido(), tipoVigueta: S.tipo }); aviso('Creando las viguetas en Revit…'); });
  $('b-quitar').addEventListener('click', () => { enviar({ tipo: 'quitar', paneles: S.paneles }); aviso('Quitando las viguetas creadas…'); });
  $('b-cerrar').addEventListener('click', () => enviar({ tipo: 'cerrar' }));

  let tAviso;
  function aviso(texto, bien) {
    const t = $('toast'); t.textContent = texto; t.classList.toggle('bien', !!bien); t.classList.add('show');
    clearTimeout(tAviso); tAviso = setTimeout(() => t.classList.remove('show'), 3200);
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
