// Solo para ver viguetas.html fuera de Revit (en un navegador): un piso de ejemplo de 2 × 2 paneles rectangulares, con
// una viga corta que entra en P3 y viguetas que ya están en P1, y una copia simple del reparto del Excel. En Revit no se
// carga: el cálculo de verdad es el de C# (Calculo/Viguetas).
window.DEMO_VIGUETAS = () => {
  const xs = [0, 3870, 9070], ys = [0, 2300, 6280], anchoViga = 300;
  const armazon = [];
  ys.forEach((y, i) => armazon.push({ id: `h${i}`, desde: [xs[0], y], hasta: [xs[2], y], ancho: anchoViga, tipo: 'V30X40', esVigueta: false, creada: false }));
  xs.forEach((x, i) => armazon.push({ id: `v${i}`, desde: [x, ys[0]], hasta: [x, ys[2]], ancho: anchoViga, tipo: 'V30X40', esVigueta: false, creada: false }));
  armazon.push({ id: 'corta', desde: [2000, 0], hasta: [2000, 1200], ancho: 200, tipo: 'V20X30', esVigueta: false, creada: false });
  // Viguetas del modelo en P1, en ↕ Y: P3, debajo, puede seguirlas.
  [700, 1500, 2300, 3100].forEach((x, i) => armazon.push({ id: `vt${i}`, desde: [x, ys[1]], hasta: [x, ys[2]], ancho: 100, tipo: 'VT_10X35', esVigueta: true, creada: false }));
  // Una viga en arco sobre el borde de arriba, en tramos como la lee Revit (decisión 38).
  const verticesArco = Array.from({ length: 11 }, (_, i) => { const t = i / 10; return [xs[2] * t, ys[2] + 1300 * Math.sin(Math.PI * t)]; });
  verticesArco.slice(0, -1).forEach((p, i) => armazon.push({ id: `arco~${i}`, desde: p, hasta: verticesArco[i + 1], ancho: 300, tipo: 'V30X45', esVigueta: false, creada: false }));
  const columnas = [];
  xs.forEach((x) => ys.forEach((y) => columnas.push({ id: `c${x}-${y}`, min: [x - 200, y - 200], max: [x + 200, y + 200] })));
  const paneles = [];
  [[0, 1], [1, 1], [0, 0], [1, 0]].forEach(([i, j], k) => {
    const [x0, x1, y0, y1] = [xs[i], xs[i + 1], ys[j], ys[j + 1]], m = anchoViga / 2;
    paneles.push({ id: `P${k + 1}`, eje: [[x0, y0], [x1, y0], [x1, y1], [x0, y1]], libre: [[x0 + m, y0 + m], [x1 - m, y0 + m], [x1 - m, y1 - m], [x0 + m, y1 - m]], creadas: 0 });
  });
  let perimetros = [];
  // Un suelo que sale de las vigas arriba a la izquierda (#954): sus vértices de afuera son puntos atípicos.
  const suelos = [{ id: 's1', contornos: [[[0, 0], [9070, 0], [9070, 6280], [5500, 6280], [5500, 7600], [0, 7600]]] }];
  // Los puntos: columnas, cruces de la grilla, puntas de la viga corta y vértices del suelo (a menos de 30 mm, uno solo).
  const puntos = [];
  const sumar = (xy, tipo) => { if (puntos.every((p) => Math.hypot(p.xy[0] - xy[0], p.xy[1] - xy[1]) > 30)) puntos.push({ xy, tipo }); };
  columnas.forEach((c) => sumar([(c.min[0] + c.max[0]) / 2, (c.min[1] + c.max[1]) / 2], 'columna'));
  [[2000, 0], [2000, 1200]].forEach((p, i) => sumar(p, i ? 'viga' : 'cruce'));
  armazon.filter((a) => a.esVigueta).forEach((a) => { sumar(a.desde, 'vigueta'); sumar(a.hasta, 'vigueta'); });
  // Dos puntas a 4 cm: sin acercar se ve una sola.
  sumar([9070 - 40, 6280 + 40], 'viga');
  suelos[0].contornos[0].forEach((p) => sumar(p, 'suelo'));
  const piso = { tipo: 'piso', id: '1', nombre: 'N+3.70', cota: 3700, viguetasComoBorde: false, armazon, columnas, paneles, suelos, puntos };
  const pisos = [{ id: '1', nombre: 'N+3.70', cota: 3700 }, { id: '2', nombre: 'N+6.70', cota: 6700, creadas: 12 }, { id: '3', nombre: 'N+9.70', cota: 9700 }];
  let abierto = piso;

  /**
   * Copia simple de PisoViguetas.Zonas para el ejemplo: un lado que va sobre una viga se corre 150 mm y uno libre, un
   * ancho de vigueta con su vigueta de borde. Solo para lados rectos en X o en Y.
   */
  function zonas(m) {
    const b = m.ancho;
    const sobreViga = (a, c) => armazon.some((x) => {
      const vertical = Math.abs(a[0] - c[0]) < 1, k = vertical ? 1 : 0;
      if (x.esVigueta || (Math.abs(x.desde[0] - x.hasta[0]) < 1) !== vertical || Math.abs(x.desde[1 - k] - a[1 - k]) >= 30) return false;
      const comun = Math.min(Math.max(a[k], c[k]), Math.max(x.desde[k], x.hasta[k])) - Math.max(Math.min(a[k], c[k]), Math.min(x.desde[k], x.hasta[k]));
      return comun > Math.abs(c[k] - a[k]) / 2;
    });
    const area = (r) => r.reduce((s, p, i) => s + p[0] * r[(i + 1) % r.length][1] - r[(i + 1) % r.length][0] * p[1], 0) / 2;
    const correr = (r, voladizos = []) => {
      const mismo = (a, c) => Math.hypot(a[0] - c[0], a[1] - c[1]) < 1;
      const bordes = [], d = r.map((p, i) => { const c = r[(i + 1) % r.length]; if (sobreViga(p, c)) return anchoViga / 2; if (voladizos.some(([u, v]) => (mismo(u, p) && mismo(v, c)) || (mismo(u, c) && mismo(v, p)))) return 0; bordes.push([p, c]); return b; });
      // Cada lado se corre a su izquierda; los vértices son el cruce de los lados corridos (todos en X o en Y).
      const lados = r.map((p, i) => { const c = r[(i + 1) % r.length], l = Math.hypot(c[0] - p[0], c[1] - p[1]); return [-(c[1] - p[1]) / l * d[i], (c[0] - p[0]) / l * d[i]]; });
      const libre = r.map((p, i) => { const a = lados[(i - 1 + r.length) % r.length], c = lados[i]; return [p[0] + a[0] + c[0], p[1] + a[1] + c[1]]; });
      const ejes = bordes.map(([p, c]) => { const l = Math.hypot(c[0] - p[0], c[1] - p[1]), nx = -(c[1] - p[1]) / l * b / 2, ny = (c[0] - p[0]) / l * b / 2; return { desde: [p[0] + nx, p[1] + ny], hasta: [c[0] + nx, c[1] + ny] }; });
      return { libre, bordes: ejes };
    };
    const armadas = m.zonas.filter((z) => z.contorno.length >= 3).map((z) => {
      const eje = area(z.contorno) > 0 ? z.contorno : [...z.contorno].reverse();
      const fuera = correr(eje, z.voladizos), huecos = z.vacios.map((v) => correr(area(v) < 0 ? v : [...v].reverse()));
      return { id: z.id, eje, libre: fuera.libre, creadas: 0, huecosEje: z.vacios, huecosLibre: huecos.map((x) => x.libre), bordes: [...fuera.bordes, ...huecos.flatMap((x) => x.bordes)] };
    });
    perimetros = armadas;
    return { tipo: 'zonas', paneles: armadas };
  }
  const dentro = (pan, p) => p[0] > pan.eje[0][0] && p[0] < pan.eje[1][0] && p[1] > pan.eje[0][1] && p[1] < pan.eje[2][1];

  function propuesta(m) {
    const enY = m.direccion === 'X', d = m.ejeAEje, b = m.ancho, repartos = [], viguetas = [];
    const elegidos = m.paneles.map((id) => [...paneles, ...perimetros].find((p) => p.id === id));
    const guias = armazon.filter((a) => a.esVigueta && (enY ? a.desde[1] === a.hasta[1] : a.desde[0] === a.hasta[0])
      && !elegidos.some((p) => dentro(p, [(a.desde[0] + a.hasta[0]) / 2, (a.desde[1] + a.hasta[1]) / 2]))).map((a) => (enY ? a.desde[1] : a.desde[0]));
    let hayGuias = false;
    elegidos.forEach((pan) => {
      const us = pan.libre.map((p) => (enY ? p[1] : p[0])), ws = pan.libre.map((p) => (enY ? p[0] : p[1]));
      const u0 = Math.min(...us), u1 = Math.max(...us), L = u1 - u0, w0 = Math.min(...ws), w1 = Math.max(...ws);
      const propias = guias.filter((c) => c - b / 2 >= u0 && c + b / 2 <= u1).sort((x, y) => x - y);
      if (propias.length) hayGuias = true;
      let cotas, r;
      if (propias.length && m.seguir) {
        cotas = propias;
        r = { id: pan.id, libre: L, cantidad: cotas.length, ejeAEje: cotas[1] - cotas[0] || d, libreExtremos: cotas[0] - b / 2 - u0, libreFinal: u1 - cotas[cotas.length - 1] - b / 2, libreMaximo: d - b, origen: 'Existentes', guia: null };
      } else {
        const ext = (n) => (L - ((n - 1) * d + b)) / 2;
        let n = Math.max(1, Math.floor(L / d));
        if (ext(n) > d - b) n++;
        const e = ext(n);
        cotas = Array.from({ length: n }, (_, i) => u0 + e + b / 2 + i * d);
        r = { id: pan.id, libre: L, cantidad: n, ejeAEje: d, libreExtremos: e, libreFinal: e, libreMaximo: d - b, origen: 'Excel', guia: null };
      }
      repartos.push(r);
      const punto = (c, w) => (enY ? [w, c] : [c, w]);
      cotas.forEach((c, i) => {
        // Solo la viga corta del ejemplo, que es vertical: parte las viguetas en ↔ X que la cruzan.
        const cruza = enY && w0 < 2000 && 2000 < w1 && c < 1200;
        const partes = cruza ? [[w0, 1900, 'a'], [2100, w1, 'b']] : [[w0, w1, '']];
        partes.forEach(([a, z, s]) => {
          const vid = `${pan.id}:${i + 1}${s}`;
          // De eje a eje: hasta la mitad de la viga del borde (150 mm) o de la viga corta (100 mm).
          const ejeDesde = punto(c, a === w0 ? w0 - 150 : a - 100), ejeHasta = punto(c, z === w1 ? w1 + 150 : z + 100);
          viguetas.push({ id: vid, panel: pan.id, cota: c, desde: punto(c, a), hasta: punto(c, z), ejeDesde, ejeHasta, largo: z - a, tipo: 1, borrada: m.borradas.includes(vid) });
        });
      });
    });
    const vivas = viguetas.filter((v) => !v.borrada);
    const largos = [...new Set(vivas.map((v) => Math.round(v.largo / 10) * 10))].sort((x, y) => y - x);
    vivas.forEach((v) => { v.tipo = largos.indexOf(Math.round(v.largo / 10) * 10) + 1; });
    const tipos = largos.map((l, i) => ({ numero: i + 1, largo: l, cantidad: vivas.filter((v) => v.tipo === i + 1).length }));
    const lineas = new Set(vivas.map((v) => Math.round(v.cota))).size;
    // Las viguetas del modelo dentro de los paneles elegidos, en una pieza cada una (en el ejemplo no las cruza ninguna viga).
    const modelo = armazon.filter((a) => a.esVigueta && elegidos.some((p) => dentro(p, [(a.desde[0] + a.hasta[0]) / 2, (a.desde[1] + a.hasta[1]) / 2])))
      .map((a) => ({ id: `${a.id}#1`, elemento: a.id, desde: a.desde, hasta: a.hasta, quitada: (m.quitarModelo || []).includes(`${a.id}#1`) }));
    const bordes = elegidos.flatMap((p) => (p.bordes || []).map((g, i) => {
      const id = `${p.id}:B${i + 1}`;
      return { id, panel: p.id, desde: g.desde, hasta: g.hasta, largo: Math.hypot(g.hasta[0] - g.desde[0], g.hasta[1] - g.desde[1]), borrada: m.borradas.includes(id) };
    }));
    const deBorde = bordes.filter((g) => !g.borrada).length;
    return { tipo: 'propuesta', paneles: m.paneles, propuesta: { direccion: m.direccion, ancho: b, lineas: (m.continuas ? lineas : vivas.length) + deBorde, hayGuias, modelo, paneles: repartos, viguetas, tipos, bordes } };
  }

  return (m) => {
    if (m.tipo === 'listo') return [{ tipo: 'pisos', documento: 'Ejemplo (fuera de Revit)', pisos: pisos.map((p) => ({ ...p, armazon: armazon.length })), tipos: [{ id: '10', nombre: 'Vigueta : VT_10X35', sugerido: true }] }];
    if (m.tipo === 'piso') { abierto = { ...piso, ...pisos.find((p) => p.id === m.id) }; return [abierto]; }
    if (m.tipo === 'propuesta') return [propuesta(m)];
    if (m.tipo === 'zonas') return [zonas(m)];
    if (m.tipo === 'perimetro') {
      // Ejemplo simple: con dos vigas horizontales y dos verticales elegidas, el perímetro es el rectángulo entre ellas.
      const elegidas = armazon.filter((a) => m.ids.includes(a.id));
      const xs2 = elegidas.filter((a) => a.desde[0] === a.hasta[0]).map((a) => a.desde[0]);
      const ys2 = elegidas.filter((a) => a.desde[1] === a.hasta[1]).map((a) => a.desde[1]);
      if (xs2.length < 2 || ys2.length < 2) return [{ tipo: 'perimetro', ids: m.ids, paneles: [] }];
      const [x0, x1, y0, y1] = [Math.min(...xs2), Math.max(...xs2), Math.min(...ys2), Math.max(...ys2)], k = anchoViga / 2;
      const z = { id: 'Z1', eje: [[x0, y0], [x1, y0], [x1, y1], [x0, y1]], libre: [[x0 + k, y0 + k], [x1 - k, y0 + k], [x1 - k, y1 - k], [x0 + k, y1 - k]], creadas: 0 };
      perimetros = [z];
      return [{ tipo: 'perimetro', ids: m.ids, paneles: [z] }];
    }
    if (m.tipo === 'vaciar') {
      // En el ejemplo, las viguetas del modelo van dentro de un solo panel: cortarlas es quitarlas enteras.
      const pan = paneles.find((p) => p.id === m.id);
      const cortadas = armazon.filter((a) => a.esVigueta && dentro(pan, [(a.desde[0] + a.hasta[0]) / 2, (a.desde[1] + a.hasta[1]) / 2]));
      cortadas.forEach((a) => armazon.splice(armazon.indexOf(a), 1));
      return [abierto, { tipo: 'aviso', texto: cortadas.length ? `${m.id} queda como vacío: se cortaron ${cortadas.length} viguetas.` : `${m.id} queda como vacío: no tenía viguetas.`, bien: true }];
    }
    // Como en Revit, al crear se vuelve a mandar el piso (y la página vuelve a armar sus zonas).
    if (m.tipo === 'crear') return [abierto, { tipo: 'aviso', texto: `Ejemplo: en Revit aquí se repetiría en ${m.pisos.length} piso(s), con ${m.vaciados.length} panel(es) vacío(s).`, bien: true }];
    return [];
  };
};
