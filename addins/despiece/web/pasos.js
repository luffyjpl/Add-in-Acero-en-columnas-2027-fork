// Los tres pasos del despiece (#220 y #221). Las imágenes son vistas reales de Revit, que manda PuenteDespiece.
(function () {
  const { html, render, Component } = window.htmPreact;
  const enviar = (m) => window.chrome?.webview?.postMessage(m);

  const VERDE = '#1E8E3E', AZUL = '#1F5FAF';
  // Los límites del zoom: 1 es la imagen entera en su recuadro.
  const ZOOM_MIN = 0.35, ZOOM_MAX = 16;
  // Lo mínimo del dibujo que queda a la vista al moverlo, en píxeles de pantalla.
  const MARGEN_VISIBLE = 80;
  // El tamaño de letra de las vistas, en mm (#261): los mismos límites que TamanoLetra.cs. Se recuerda entre aperturas.
  // La clave cambió con #272: lo guardado antes (3.5 por defecto) no tapa los 2.5 mm de norma.
  const LETRA = { min: 2, max: 5, paso: 0.5, defecto: 2.5, clave: 'despiece.letra.2' };
  const limitarLetra = (mm) => Number.isFinite(mm) ? Math.min(LETRA.max, Math.max(LETRA.min, Math.round(mm / LETRA.paso) * LETRA.paso)) : LETRA.defecto;
  // La escala de las vistas (#272): el texto mide lo mismo impreso y lo que cambia es la escala. Los mismos valores que
  // EscalaVista.cs; con "Otra" se escribe cualquiera.
  const ESCALA = { min: 1, max: 24000, defecto: 50, lista: [1, 2, 5, 10, 15, 20, 25, 50, 75, 100, 200, 500], clave: 'despiece.escala' };
  const limitarEscala = (e) => Number.isFinite(e) ? Math.min(ESCALA.max, Math.max(ESCALA.min, Math.round(e))) : ESCALA.defecto;
  const guardado = (clave, defecto, limitar) => {
    try {
      const v = window.localStorage.getItem(clave);
      return v === null ? defecto : limitar(parseFloat(v));
    } catch {
      return defecto;
    }
  };
  const recordar = (clave, valor) => {
    try {
      window.localStorage.setItem(clave, String(valor));
    } catch {
      // Recordarlo es una ayuda: sin almacenamiento, la próxima vez vuelve al de por defecto.
    }
  };
  const letraGuardada = () => guardado(LETRA.clave, LETRA.defecto, limitarLetra);
  // Una escala para la vista larga o el alzado y otra para la sección (decisión 20 del plan de #287).
  // Alzado 1:50 y sección 1:20 por defecto (decisión 27). La clave de la sección cambió para que lo guardado en las
  // pruebas (1:15) no tape el 1:20.
  const CLAVE_ESCALA_SECCION = 'despiece.escala.seccion.2', ESCALA_SECCION = 20; // = EscalaVista.PorDefectoSeccion
  const escalaGuardada = () => guardado(ESCALA.clave, ESCALA.defecto, limitarEscala);
  const escalaSeccionGuardada = () => guardado(CLAVE_ESCALA_SECCION, ESCALA_SECCION, limitarEscala);
  const TIPOS = [
    { n: 'Vigas', k: 'viga', icon: 'M3 14h30v8H3zM8 14v8M28 14v8' },
    { n: 'Columnas', k: 'columna', icon: 'M13 3h10v30H13zM13 12h10M13 24h10' },
    { n: 'Viguetas', k: 'vigueta', icon: 'M3 12h30M3 18h30M3 24h30' },
    { n: 'Zapatas · pronto', off: true, icon: 'M6 22h24v9H6zM14 22V6h8v16' }
  ];
  const TEXTOS = {
    1: ['¿Qué vas a despiezar?', 'Muévete entre plantas y haz clic en una viga. Las que llevan el mismo acero se eligen solas, también en otras plantas.'],
    2: ['Revisa el despiece', 'Arrastra las barras y los textos para acomodarlos. Todavía no se creó nada en el proyecto.'],
    3: ['Vista previa', 'Las vistas quedan en el navegador de Revit, con etiquetas y colores. Tú las arrastras a tu plano.'],
    // Solo con columnas: la sección superior va en su propio paso (decisión 17 del plan de columnas).
    seccion: ['Revisa la sección', 'Al comienzo de la columna: una con los colores de traslapo y otra con los de cada varilla. Todavía no se creó nada en el proyecto.']
  };
  /** Los pasos de cada tipo: la columna suma uno para la sección superior (decisión 17 del plan de columnas). */
  const PASOS = { viga: ['Elegir', 'Revisar', 'Crear vistas'], columna: ['Elegir', 'Alzado', 'Sección', 'Crear vistas'] };

  /**
   * Una imagen de Revit que se acerca con la rueda y se mueve arrastrando. El zoom y el desplazamiento viven aquí y se
   * aplican directo al dibujo, sin volver a pintar la página: así siguen el dedo sin retraso (Juan, 30-oct: "que sea muy
   * fluido"). <c>movio</c> dice si lo último fue un arrastre, para no tomarlo por un clic en lo que hay dentro.
   */
  class Visor extends Component {
    constructor(props) {
      super(props);
      this.vista = { k: 1, tx: 0, ty: 0 };
      this.capa = null;
      this.lienzo = null;
      this.arrastrando = null;
      this.movio = false;
      this.alMoverPuntero = (e) => this.moverPuntero(e);
      this.alSoltarPuntero = () => this.soltarPuntero();
    }

    componentWillUnmount() {
      this.soltarPuntero();
    }

    aplicar() {
      const { k, tx, ty } = this.vista;
      this.capa?.setAttribute('transform', `translate(${tx} ${ty}) scale(${k})`);
    }

    reiniciar() {
      this.vista = { k: 1, tx: 0, ty: 0 };
      this.aplicar();
    }

    /** De un punto de la pantalla a coordenadas del dibujo (píxeles de la imagen). */
    aDibujo(x, y) {
      const m = this.lienzo?.getScreenCTM();
      if (!m) return null;
      const p = this.lienzo.createSVGPoint();
      p.x = x;
      p.y = y;
      return p.matrixTransform(m.inverse());
    }

    /**
     * La imagen se mueve libre en cualquier sentido y a cualquier zoom (Juan, 30-oct: "no me deja arrastrar hacia
     * arriba"); solo no se deja perder del todo: siempre queda a la vista. El límite se mide sobre lo que se ve en la
     * ventana, no sobre la imagen (hallazgo 28 del plan de columnas), y basta con una franja fija a la vista
     * (hallazgo 34): con una imagen alta y angosta, cualquier exigencia mayor frena el zoom y lo lleva al medio.
     */
    limitar(v) {
      const d = this.dibujo();
      const k = Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, v.k));
      let x0 = d.x0, x1 = d.x1, y0 = d.y0, y1 = d.y1;
      const caja = this.lienzo?.getBoundingClientRect();
      const a = caja?.width ? this.aDibujo(caja.left, caja.top) : null, b = caja?.width ? this.aDibujo(caja.right, caja.bottom) : null;
      if (a && b) [x0, x1, y0, y1] = [a.x, b.x, a.y, b.y];
      // Queda a la vista una franja fija de dibujo, MARGEN_VISIBLE píxeles de pantalla (o el dibujo entero si es más chico).
      // Medido sobre un cuarto de la ventana frenaba casi siempre con una imagen alta y angosta (hallazgo 34).
      const porPixelX = caja?.width ? (x1 - x0) / caja.width : 1, porPixelY = caja?.height ? (y1 - y0) / caja.height : 1;
      // El dibujo va de t + k·desde a t + k·hasta: entre los dos bordes de la ventana tiene que quedar la franja.
      const rango = (t, desde, hasta, v0, v1, porPixel) => {
        const m = Math.min(MARGEN_VISIBLE * porPixel, k * (hasta - desde));
        return Math.min(Math.max(t, v0 + m - k * hasta), v1 - m - k * desde);
      };
      return { k, tx: rango(v.tx, d.x0, d.x1, x0, x1, porPixelX), ty: rango(v.ty, d.y0, d.y1, y0, y1, porPixelY) };
    }

    acercar(factor, x, y) {
      const c = this.aDibujo(x, y);
      if (!c) return;
      const v = this.vista;
      const k = Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, v.k * factor));
      const r = k / v.k;
      this.vista = this.limitar({ k, tx: c.x - (c.x - v.tx) * r, ty: c.y - (c.y - v.ty) * r });
      this.aplicar();
    }

    /** Los botones acercan hacia el centro de lo que se ve. */
    acercarAlCentro(factor) {
      const caja = this.lienzo?.getBoundingClientRect();
      if (caja) this.acercar(factor, caja.left + caja.width / 2, caja.top + caja.height / 2);
    }

    alRueda(e) {
      e.preventDefault();
      // Con un touchpad, pellizcar manda la rueda con Ctrl; arrastrar con dos dedos, también la rueda.
      this.acercar(Math.exp(-e.deltaY * (e.ctrlKey ? 0.01 : 0.0015)), e.clientX, e.clientY);
    }

    alPulsar(e) {
      if (e.button !== 0) return;
      this.arrastrando = { x: e.clientX, y: e.clientY };
      this.movio = false;
      window.addEventListener('pointermove', this.alMoverPuntero);
      window.addEventListener('pointerup', this.alSoltarPuntero);
    }

    moverPuntero(e) {
      const a = this.arrastrando;
      if (!a) return;
      const dx = e.clientX - a.x, dy = e.clientY - a.y;
      if (!this.movio && Math.hypot(dx, dy) < 4) return;
      this.movio = true;
      const m = this.lienzo.getScreenCTM();
      this.vista = this.limitar({ ...this.vista, tx: this.vista.tx + dx / m.a, ty: this.vista.ty + dy / m.d });
      this.arrastrando = { x: e.clientX, y: e.clientY };
      this.aplicar();
    }

    soltarPuntero() {
      this.arrastrando = null;
      window.removeEventListener('pointermove', this.alMoverPuntero);
      window.removeEventListener('pointerup', this.alSoltarPuntero);
    }

    /**
     * Lo que ocupa el dibujo en coordenadas de la imagen: la imagen y, si se pasa <c>limites</c>, lo que se dibuja encima
     * (las barras sacadas y sus textos quedan fuera de la imagen exportada; hallazgo 34 del plan de columnas).
     */
    dibujo() {
      const { ancho, alto, limites } = this.props;
      const l = limites ?? { x0: 0, y0: 0, x1: ancho, y1: alto };
      return { x0: Math.min(0, l.x0), y0: Math.min(0, l.y0), x1: Math.max(ancho, l.x1), y1: Math.max(alto, l.y1) };
    }

    render({ etiqueta, children }) {
      const d = this.dibujo();
      const estilo = 'width: 36px; height: 36px; border: 1px solid #C9CFD6; border-radius: 6px; background: #FFFFFF; color: #16191D; font: inherit; font-size: 18px; cursor: pointer';
      return html`
        <div style="position: relative; width: 100%; height: 100%">
          <svg ref=${(el) => (this.lienzo = el)} viewBox="${d.x0} ${d.y0} ${d.x1 - d.x0} ${d.y1 - d.y0}" style="width: 100%; height: 100%; display: block; touch-action: none; cursor: grab; user-select: none" role="img" aria-label=${etiqueta}
            onWheel=${(e) => this.alRueda(e)} onPointerDown=${(e) => this.alPulsar(e)} onDblClick=${() => this.reiniciar()}>
            <g ref=${(el) => { this.capa = el; this.aplicar(); }}>${children}</g>
          </svg>
          <div style="position: absolute; right: 12px; top: 12px; display: flex; flex-direction: column; gap: 6px">
            <button type="button" aria-label="Acercar" title="Acercar" onClick=${() => this.acercarAlCentro(1.5)} style=${estilo}>+</button>
            <button type="button" aria-label="Alejar" title="Alejar" onClick=${() => this.acercarAlCentro(1 / 1.5)} style=${estilo}>−</button>
            <button type="button" aria-label="Ajustar a la ventana" title="Ajustar a la ventana" onClick=${() => this.reiniciar()} style=${estilo + '; font-size: 12px'}>Todo</button>
          </div>
        </div>`;
    }
  }

  /**
   * Lo que ocupa la capa movible, en píxeles de la imagen: los puntos de las barras y una caja aproximada de cada texto
   * (Arial: unos 0.6 del alto de letra por carácter). Lo que el usuario arrastre después no cuenta.
   */
  function limitesCapa(capa) {
    let x0 = Infinity, y0 = Infinity, x1 = -Infinity, y1 = -Infinity;
    const sumar = (x, y) => {
      x0 = Math.min(x0, x); x1 = Math.max(x1, x); y0 = Math.min(y0, y); y1 = Math.max(y1, y);
    };
    const texto = (t) => {
      const largo = t.texto.length * capa.altoLetra * 0.6 / 2, alto = capa.altoLetra / 2;
      const [mx, my] = t.girado ? [alto, largo] : [largo, alto];
      sumar(t.x - mx, t.y - my);
      sumar(t.x + mx, t.y + my);
    };
    capa.piezas.forEach((p) => {
      p.puntos.forEach(([x, y]) => sumar(x, y));
      texto(p.etiqueta);
      p.anexos.forEach(texto);
    });
    return x0 === Infinity ? null : { x0, y0, x1, y1 };
  }

  /** Un tubo por cada eje de barra, todos en una sola geometría: un objeto por barra giraría peor con cientos de estribos. */
  function tubos(barras) {
    const T = window.THREE, LADOS = 6;
    const posiciones = [], normales = [], indices = [];
    barras.forEach(({ r, p }) => {
      const puntos = [];
      for (let i = 0; i + 2 < p.length; i += 3) puntos.push(new T.Vector3(p[i], p[i + 1], p[i + 2]));
      if (puntos.length < 2) return;
      const inicio = posiciones.length / 3;
      let n = null;
      puntos.forEach((q, i) => {
        const t = puntos[Math.min(i + 1, puntos.length - 1)].clone().sub(puntos[Math.max(i - 1, 0)]).normalize();
        // La normal se arrastra de un punto al siguiente, para que el tubo no se retuerza en los dobleces.
        n = n ? n.sub(t.clone().multiplyScalar(n.dot(t))) : new T.Vector3(Math.abs(t.z) < 0.9 ? 0 : 1, 0, Math.abs(t.z) < 0.9 ? 1 : 0).cross(t);
        n.normalize();
        const b = t.clone().cross(n);
        for (let l = 0; l < LADOS; l++) {
          const a = (l / LADOS) * Math.PI * 2;
          const d = n.clone().multiplyScalar(Math.cos(a)).add(b.clone().multiplyScalar(Math.sin(a)));
          posiciones.push(q.x + d.x * r, q.y + d.y * r, q.z + d.z * r);
          normales.push(d.x, d.y, d.z);
        }
      });
      for (let i = 0; i < puntos.length - 1; i++) {
        for (let l = 0; l < LADOS; l++) {
          const a = inicio + i * LADOS + l, c = inicio + i * LADOS + (l + 1) % LADOS;
          indices.push(a, c, a + LADOS, c, c + LADOS, a + LADOS);
        }
      }
    });
    const g = new T.BufferGeometry();
    g.setAttribute('position', new T.Float32BufferAttribute(posiciones, 3));
    g.setAttribute('normal', new T.Float32BufferAttribute(normales, 3));
    g.setIndex(indices);
    return g;
  }

  /** El acero real de la viga, para girarlo dentro del add-in. Solo se dibuja cuando la cámara se mueve: en reposo no gasta nada. */
  class Vista3D extends Component {
    componentDidMount() {
      const T = window.THREE, { datos } = this.props, caja = this.caja;
      try {
        this.pintor = new T.WebGLRenderer({ antialias: true });
      } catch (e) {
        this.setState({ sinWebgl: true });
        return;
      }
      this.pintor.setPixelRatio(Math.min(window.devicePixelRatio, 2));
      this.pintor.setClearColor(0xffffff);
      caja.appendChild(this.pintor.domElement);

      const escena = new T.Scene();
      const grupo = new T.Group();
      const porColor = {};
      datos.barras.forEach((b) => (porColor[b.c] ??= []).push(b));
      Object.entries(porColor).forEach(([color, barras]) => grupo.add(new T.Mesh(tubos(barras), new T.MeshLambertMaterial({ color }))));
      const viga = new T.BufferGeometry();
      viga.setAttribute('position', new T.Float32BufferAttribute(datos.viga, 3));
      viga.computeVertexNormals();
      grupo.add(new T.Mesh(viga, new T.MeshLambertMaterial({ color: 0x9aa3ad, transparent: true, opacity: 0.22, depthWrite: false, side: T.DoubleSide })));
      escena.add(grupo);

      const camara = new T.PerspectiveCamera(35, 1, 0.05, 2000);
      camara.up.set(0, 0, 1);
      camara.add(new T.DirectionalLight(0xffffff, 0.7));
      escena.add(camara, new T.AmbientLight(0xffffff, 0.65));

      const limites = new T.Box3().setFromObject(grupo);
      const centro = limites.getCenter(new T.Vector3()), radio = limites.getSize(new T.Vector3()).length() / 2;
      camara.position.copy(centro).add(new T.Vector3(-0.55, -1, 0.45).normalize().multiplyScalar(radio * 2.3));
      camara.far = radio * 40;

      this.camara = camara;
      this.pintar = () => this.pintor.render(escena, camara);
      this.controles = new T.OrbitControls(camara, this.pintor.domElement);
      this.controles.target.copy(centro);
      this.controles.addEventListener('change', this.pintar);
      this.controles.update();

      const ajustar = () => {
        const w = caja.clientWidth, h = caja.clientHeight;
        if (!w || !h) return;
        this.pintor.setSize(w, h);
        camara.aspect = w / h;
        camara.updateProjectionMatrix();
        this.pintar();
      };
      this.observador = new ResizeObserver(ajustar);
      this.observador.observe(caja);
      ajustar();
    }

    componentWillUnmount() {
      this.observador?.disconnect();
      this.controles?.dispose();
      this.pintor?.dispose();
    }

    render(_, { sinWebgl }) {
      return html`
        <div ref=${(el) => (this.caja = el)} style="position: absolute; inset: 0; overflow: hidden">
          ${sinWebgl ? html`<div style="display: grid; place-items: center; height: 100%; color: #5E6670">Este equipo no puede dibujar el 3D.</div>` : null}
        </div>`;
    }
  }

  class Pasos extends Component {
    constructor(props) {
      super(props);
      this.state = { paso: 1, modelo: null, plantaId: null, plantas: {}, grupo: null, previas: {}, negros: true, letra: letraGuardada(), escala: escalaGuardada(), escalaSeccion: escalaSeccionGuardada(), tipo: 'viga', movimientos: {}, modo3d: false, modelos3d: {}, hechos: {}, error: null, hecho: false, ...(props.inicial ?? {}) };
      this.alCambiarTamano = () => this.forceUpdate();
      this.alRecibir = (e) => this.recibir(e.data);
    }

    /** Un clic en una viga solo cuenta si no fue el final de un arrastre. */
    elegirViga(g) {
      if (this.visor?.movio) {
        this.visor.movio = false;
        return;
      }
      if (g) this.setState({ grupo: g.indice, hecho: false });
    }

    componentDidMount() {
      window.addEventListener('resize', this.alCambiarTamano);
      window.chrome?.webview?.addEventListener('message', this.alRecibir);
      enviar({ tipo: 'iniciar' });
    }

    componentWillUnmount() {
      window.removeEventListener('resize', this.alCambiarTamano);
      window.chrome?.webview?.removeEventListener('message', this.alRecibir);
      clearTimeout(this.esperaLetra);
    }

    recibir(m) {
      if (m.tipo === 'modelo' && m.refresco && this.state.modelo) {
        this.refrescar(m);
      } else if (m.tipo === 'modelo') {
        this.setState({ modelo: m, error: null });
        if (m.plantas.length > 0) this.verPlanta(m.plantas[0].id);
      } else if (m.tipo === 'planta') {
        this.setState((s) => ({ plantas: { ...s.plantas, [m.id]: m }, error: null }));
      } else if (m.tipo === 'previa') {
        this.setState((s) => ({ previas: { ...s.previas, [m.clave]: m }, error: null }));
      } else if (m.tipo === 'modelo3d') {
        this.setState((s) => ({ modelos3d: { ...s.modelos3d, [m.grupo]: m.modelo }, error: null }));
      } else if (m.tipo === 'vistas') {
        // En verde solo cuando Revit ya las tiene.
        this.setState((s) => ({ creando: false, hecho: m.yaExistian ? 'Esas vistas ya existen: no se duplicaron.' : `Vistas creadas: ${m.nombres ? m.nombres.join(', ') : `${m.longitudinal} y ${m.seccion}`}. Arrástralas a tu plano.`, hechos: { ...s.hechos, [m.grupo]: true }, error: null }));
      } else if (m.tipo === 'error') {
        this.setState({ error: m.texto, creando: false });
      }
    }

    /**
     * El modelo cambió en Revit: se sigue en el mismo paso, planta, zoom y viga, y se piden primero las imágenes que se están
     * viendo. Los grupos pueden cambiar de número y de nombre, así que se buscan por sus vigas; si la elegida ya no existe,
     * vuelve al paso 1.
     */
    refrescar(m) {
      const { modelo, grupo, hechos, plantaId, paso } = this.state;
      const elegida = modelo.grupos[grupo]?.vigas[0];
      const nuevo = m.grupos.find((g) => g.vigas.includes(elegida)) ?? null;
      const hechosNuevos = {};
      m.grupos.forEach((g) => {
        const antes = modelo.grupos.find((a) => a.vigas.some((v) => g.vigas.includes(v)));
        if (antes && hechos[antes.indice]) hechosNuevos[g.indice] = true;
      });
      const sigue = m.plantas.some((p) => p.id === plantaId);
      const planta = sigue ? plantaId : m.plantas[0]?.id ?? null;
      const pasoNuevo = nuevo?.tieneAcero ? paso : 1;
      if (!sigue) this.visor?.reiniciar();
      this.setState({ modelo: m, grupo: nuevo?.indice ?? null, hechos: hechosNuevos, previas: {}, modelos3d: {}, plantaId: planta, paso: pasoNuevo, error: null });
      if (planta) enviar({ tipo: 'verPlanta', id: planta });
      if (pasoNuevo > 1) enviar(this.mensajePrevia(nuevo, this.state.negros, pasoNuevo));
      if (pasoNuevo > 1 && this.state.modo3d && !nuevo.esColumna) enviar({ tipo: 'ver3d', grupo: nuevo.indice });
    }

    cambiar3d(g) {
      const modo3d = !this.state.modo3d;
      this.setState({ modo3d });
      if (modo3d && !this.state.modelos3d[g.indice]) enviar({ tipo: 'ver3d', grupo: g.indice });
    }

    vistaGrande(g) {
      const { modo3d, modelos3d, paso, vistaFinal } = this.state;
      const datos = modelos3d[g.indice];
      // En "Crear vistas" de una columna se ve en grande la vista elegida en la lista (decisión 19 del plan de columnas).
      if (g.esColumna && paso === this.ultimo() && vistaFinal) {
        const pv = this.previa(g);
        const i = pv?.[vistaFinal === 1 ? 'seccion' : 'seccionVarillas'];
        if (!pv) return html`<div style="display: grid; place-items: center; height: 100%; color: #5E6670">Generando la vista en Revit…</div>`;
        if (!i) return html`<div style="display: grid; place-items: center; height: 100%; padding: 16px; text-align: center; color: #5E6670">No se pudo saber qué varillas traslapan juntas: esta sección no se crea.</div>`;
        return html`
          <${Visor} key=${'seccion' + vistaFinal} ancho=${i.ancho} alto=${i.alto} etiqueta="Sección de Revit">
            <image href=${i.url} width=${i.ancho} height=${i.alto} draggable="false"></image>
          <//>`;
      }
      return html`
        ${!modo3d || g.esColumna ? this.imagenes(g) : datos ? html`<${Vista3D} key=${g.indice} datos=${datos} />` : html`<div style="display: grid; place-items: center; height: 100%; color: #5E6670">Leyendo el acero en Revit…</div>`}
        ${g.esColumna ? null : html`<button type="button" onClick=${() => this.cambiar3d(g)} style="position: absolute; left: 12px; top: 12px; height: 36px; padding: 0 14px; border: 1px solid #C9CFD6; border-radius: 6px; background: #FFFFFF; color: #16191D; font: inherit; cursor: pointer">${modo3d ? 'Plano' : '3D'}</button>`}`;
    }

    verPlanta(id) {
      this.visor?.reiniciar();
      this.setState({ plantaId: id, error: null });
      if (!this.state.plantas[id]) enviar({ tipo: 'verPlanta', id });
    }

    /** Cuántos pasos tiene el tipo elegido: el último es el de crear vistas. */
    ultimo() {
      return (PASOS[this.state.tipo] ?? PASOS.viga).length;
    }

    irA(paso) {
      const g = this.grupoElegido();
      if (paso > 1 && !g?.tieneAcero) return;
      // Al entrar a un paso se ve el alzado (decisión 19 del plan de columnas).
      this.setState({ paso, hecho: false, vistaFinal: 0 });
      if (paso > 1) this.pedirPrevia(g, this.state.negros, paso);
    }

    /** Lo movido en una viga, en metros: <c>{ id: { dz, tx, tz } }</c>. Va por viga: el número del grupo cambia con el modelo. */
    movimientos(g) {
      return this.state.movimientos[g.vigas[0]] ?? {};
    }

    /**
     * Los pasos de revisar traen lo movible aparte y no dependen de lo movido; el último es la imagen real con todo
     * aplicado. La sección de la columna usa la misma previa que el alzado: no se vuelve a generar.
     */
    mensajePrevia(g, negros, paso, letra = this.state.letra, escala = this.state.escala, escalaSeccion = this.state.escalaSeccion) {
      const separar = paso > 1 && paso < this.ultimo();
      const movimientos = separar ? {} : this.movimientos(g);
      return { tipo: 'previa', grupo: g.indice, negros, letra, escala, escalaSeccion, separar, movimientos, clave: `${g.indice}|${negros}|${letra}|${escala}|${escalaSeccion}|${separar}|${JSON.stringify(movimientos)}` };
    }

    /**
     * Mientras Revit genera la del otro color, queda a la vista la que ya estaba (#274): los textos que se arrastran los
     * dibuja la página con el color elegido, así que cambian al instante; solo los que van en la imagen esperan.
     */
    previa(g) {
      const { previas, negros, paso } = this.state;
      return previas[this.mensajePrevia(g, negros, paso).clave] ?? previas[this.mensajePrevia(g, !negros, paso).clave];
    }

    pedirPrevia(g, negros, paso, letra = this.state.letra, escala = this.state.escala, escalaSeccion = this.state.escalaSeccion) {
      const m = this.mensajePrevia(g, negros, paso, letra, escala, escalaSeccion);
      if (!this.state.previas[m.clave]) enviar(m);
    }

    /** Cada modo se genera una vez en Revit; volver a uno ya visto es inmediato. */
    elegirTextos(g, negros) {
      this.setState({ negros });
      this.pedirPrevia(g, negros, this.state.paso);
    }

    /**
     * Sube o baja la letra medio milímetro. La vista previa se pide a Revit medio segundo después del último clic: cada
     * una tarda unos segundos y no tiene sentido generar las intermedias.
     */
    cambiarLetra(g, delta) {
      const letra = limitarLetra(this.state.letra + delta);
      if (letra === this.state.letra) return;
      this.setState({ letra });
      recordar(LETRA.clave, letra);
      clearTimeout(this.esperaLetra);
      this.esperaLetra = setTimeout(() => this.pedirPrevia(g, this.state.negros, this.state.paso, letra), 500);
    }

    /** Como la letra: la vista previa se pide medio segundo después del último cambio (al escribir una escala, por ejemplo). */
    /** <campo> es 'escala' (vista larga o alzado) o 'escalaSeccion'. */
    cambiarEscala(g, valor, campo = 'escala') {
      const escala = limitarEscala(valor);
      if (escala === this.state[campo]) return;
      this.setState({ [campo]: escala });
      recordar(campo === 'escala' ? ESCALA.clave : CLAVE_ESCALA_SECCION, escala);
      clearTimeout(this.esperaLetra);
      this.esperaLetra = setTimeout(() => this.pedirPrevia(g, this.state.negros, this.state.paso), 500);
    }

    selectorEscala(g, campo = 'escala', titulo = 'Escala') {
      const escala = this.state[campo];
      const claveOtra = campo + 'Otra';
      const otra = this.state[claveOtra] || !ESCALA.lista.includes(escala);
      const estilo = 'height: 24px; border: 1px solid #C9CED6; border-radius: 4px; background: #FFFFFF; color: #1F2329; font: inherit; font-size: 12px';
      return html`
        <label style="display: flex; align-items: center; gap: 6px">
          ${titulo}
          <select value=${otra ? 'otra' : String(escala)} style="${estilo}; padding: 0 4px"
            onChange=${(e) => e.target.value === 'otra' ? this.setState({ [claveOtra]: true }) : (this.setState({ [claveOtra]: false }), this.cambiarEscala(g, Number(e.target.value), campo))}>
            ${ESCALA.lista.map((n) => html`<option value=${String(n)}>1:${n}</option>`)}
            <option value="otra">Otra…</option>
          </select>
          ${otra ? html`<span style="display: flex; align-items: center; gap: 2px">1:<input type="number" min=${ESCALA.min} max=${ESCALA.max} step="1" value=${escala} aria-label=${titulo + ', el número después de 1:'}
            onInput=${(e) => { const n = parseFloat(e.target.value); if (Number.isFinite(n)) this.cambiarEscala(g, n, campo); }} style="${estilo}; width: 64px; padding: 0 4px" /></span>` : null}
        </label>`;
    }

    selectorLetra(g) {
      const boton = (texto, delta, apagado, titulo) => html`
        <button type="button" title=${titulo} aria-label=${titulo} disabled=${apagado} onClick=${() => this.cambiarLetra(g, delta)}
          style="width: 22px; height: 22px; padding: 0; border: 1px solid #C9CED6; border-radius: 4px; background: #FFFFFF; color: ${apagado ? '#B0B6BF' : AZUL}; font: inherit; font-size: 14px; line-height: 1; cursor: ${apagado ? 'default' : 'pointer'}">${texto}</button>`;
      const letra = this.state.letra;
      return html`
        <span style="display: flex; align-items: center; gap: 6px">
          Letra
          ${boton('−', -LETRA.paso, letra <= LETRA.min, 'Letra más chica')}
          <span style="min-width: 44px; text-align: center; font-variant-numeric: tabular-nums; color: #1F2329">${letra.toFixed(1)} mm</span>
          ${boton('+', LETRA.paso, letra >= LETRA.max, 'Letra más grande')}
        </span>`;
    }

    selectorTextos(g) {
      const movido = this.state.paso === 2 && Object.keys(this.movimientos(g)).length > 0;
      // Con las dos escalas ya no entra todo en una línea: los controles pasan a la siguiente en vez de aplastarse
      // (el botón de volver a la posición automática quedaba en una sola letra, Luciano 06-nov).
      return html`
        <div style="display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; column-gap: 16px; row-gap: 6px; flex-shrink: 0; font-size: 12px; color: #5E6670; white-space: nowrap">
          <label style="display: flex; align-items: center; gap: 6px; cursor: pointer">
            <input type="checkbox" checked=${this.state.negros} onChange=${(e) => this.elegirTextos(g, e.target.checked)} style="margin: 0; accent-color: ${AZUL}" />Textos en negro
          </label>
          ${this.selectorEscala(g, 'escala', 'Escala alzado')}
          ${this.selectorEscala(g, 'escalaSeccion', 'Escala sección')}
          ${this.selectorLetra(g)}
          ${movido ? html`<button type="button" onClick=${() => this.setState((s) => ({ movimientos: { ...s.movimientos, [g.vigas[0]]: {} } }))} style="flex-shrink: 0; padding: 0; border: 0; background: transparent; color: ${AZUL}; font: inherit; cursor: pointer">Volver a la posición automática</button>` : null}
        </div>`;
    }

    /**
     * Arrastra una barra (con su texto y su gancho: arriba y abajo en la viga, a los lados en la columna, donde
     * <c>horizontal</c>) o un texto (libre). El zoom del visor ya viene en la matriz.
     */
    arrastrar(e, g, id, esBarra, escala, horizontal) {
      if (e.button !== 0) return;
      e.stopPropagation();
      const k = e.currentTarget.getScreenCTM().a;
      const clave = g.vigas[0];
      let x = e.clientX, y = e.clientY;
      const mover = (ev) => {
        const dx = (ev.clientX - x) / k / escala, dz = -(ev.clientY - y) / k / escala;
        x = ev.clientX;
        y = ev.clientY;
        this.setState((s) => {
          const deViga = s.movimientos[clave] ?? {};
          const m = deViga[id] ?? { dz: 0, tx: 0, tz: 0 };
          const nuevo = !esBarra ? { ...m, tx: m.tx + dx, tz: m.tz + dz } : horizontal ? { ...m, dx: (m.dx ?? 0) + dx } : { ...m, dz: m.dz + dz };
          return { movimientos: { ...s.movimientos, [clave]: { ...deViga, [id]: nuevo } } };
        });
      };
      const soltar = () => {
        window.removeEventListener('pointermove', mover);
        window.removeEventListener('pointerup', soltar);
      };
      window.addEventListener('pointermove', mover);
      window.addEventListener('pointerup', soltar);
    }

    /** Lo movible de la vista larga, encima de la imagen: mismas medidas que da Revit, en píxeles de la imagen. */
    capaMovible(g, capa) {
      const { escala, altoLetra, piezas, horizontal } = capa;
      const cursorBarra = horizontal ? 'ew-resize' : 'ns-resize';
      const movs = this.movimientos(g), negros = this.state.negros;
      const texto = (t, color, extra) => html`
        <text x=${t.x} y=${t.y} font-size=${altoLetra} font-family="Arial, sans-serif" text-anchor="middle" dominant-baseline="central" fill=${negros ? '#000000' : color} transform=${t.girado ? `rotate(-90 ${t.x} ${t.y})` : null} ...${extra}>${t.texto}</text>`;
      return piezas.map((p) => {
        const m = movs[p.id] ?? { dz: 0, tx: 0, tz: 0 };
        const puntos = p.puntos.map((q) => q.join(',')).join(' ');
        const alBarra = (e) => this.arrastrar(e, g, p.id, true, escala, horizontal);
        return html`
          <g key=${p.id} transform="translate(${(m.dx ?? 0) * escala} ${-m.dz * escala})">
            ${puntos ? html`
              <polyline points=${puntos} fill="none" stroke=${p.color} stroke-width="6"></polyline>
              <polyline points=${puntos} fill="none" stroke="transparent" stroke-width="18" pointer-events="stroke" style="cursor: ${cursorBarra}" onPointerDown=${alBarra}></polyline>` : null}
            ${p.anexos.map((t) => texto(t, p.color, { style: 'cursor: ' + cursorBarra, onPointerDown: alBarra }))}
            <g transform="translate(${m.tx * escala} ${-m.tz * escala})">
              ${texto(p.etiqueta, p.color, { style: 'cursor: move', onPointerDown: (e) => this.arrastrar(e, g, p.id, false, escala) })}
            </g>
          </g>`;
      });
    }

    grupoElegido() {
      return this.state.modelo?.grupos[this.state.grupo] ?? null;
    }

    grupoDeViga(id) {
      return this.state.modelo.grupos.find((g) => g.vigas.includes(id));
    }

    /** Si el grupo es de lo que se está despiezando: cada botón deja elegir solo lo suyo. */
    delTipo(g) {
      return !!g && (g.esColumna ? 'columna' : g.esVigueta ? 'vigueta' : 'viga') === this.state.tipo;
    }

    /** Cambiar de tipo limpia la selección: lo elegido ya no se puede tocar en la planta. */
    elegirTipo(tipo) {
      if (tipo !== this.state.tipo) this.setState({ tipo, grupo: null, hecho: false });
    }

    planta() {
      const { modelo, plantaId, plantas, grupo, hechos } = this.state;
      const p = plantas[plantaId];
      if (!modelo) return html`<span style="color: #5E6670">Leyendo el modelo…</span>`;
      if (modelo.plantas.length === 0) return html`<span style="color: #5E6670">El proyecto no tiene vigas con nivel de referencia.</span>`;
      if (!p) return html`<span style="color: #5E6670">Leyendo la planta…</span>`;
      const elegidas = new Set(this.grupoElegido()?.vigas ?? []);
      // La planta la dibuja la página con lo que se leyó del modelo, como el tablero de vigas (decisión 7 del plan
      // docs/planes/2026-11-05-reunion-despiece-287.md): ejes, vigas y columnas, y nada más (ni CAD ni anotaciones).
      const R = 20;
      const puntos = (e) => e.puntos.map((q) => q.join(',')).join(' ');
      const forma = (e) => {
        const g = this.grupoDeViga(e.viga);
        const base = e.columna
          ? html`<polygon points=${puntos(e)} fill="#9AA2AB" stroke="#3F454C" stroke-width="1.5" vector-effect="non-scaling-stroke"></polygon>`
          : html`<polygon points=${puntos(e)} fill="#ECEFF2" stroke="#5E6670" stroke-width="1.2" vector-effect="non-scaling-stroke"></polygon>`;
        if (!this.delTipo(g)) return base;
        const color = g && hechos[g.indice] ? VERDE : elegidas.has(e.viga) ? AZUL : 'transparent';
        return html`${base}<polygon points=${puntos(e)} fill=${color} fill-opacity="0.55" pointer-events="all" style="cursor: pointer" onClick=${() => this.elegirViga(g)}><title>${g?.nombre ?? ''}</title></polygon>`;
      };
      return html`
        <div style="position: absolute; inset: 0">
          <${Visor} ref=${(v) => (this.visor = v)} ancho=${p.ancho} alto=${p.alto} etiqueta="Planta con las vigas y columnas para elegir">
            <rect x="0" y="0" width=${p.ancho} height=${p.alto} fill="#FFFFFF"></rect>
            ${p.ejes.map((e) => {
              // El nombre en un círculo al comienzo del eje, dentro del margen del dibujo: ahí no hay elementos que tapar.
              const largo = Math.hypot(e.x2 - e.x1, e.y2 - e.y1) || 1;
              const cx = e.x1 + ((e.x2 - e.x1) / largo) * R, cy = e.y1 + ((e.y2 - e.y1) / largo) * R;
              return html`
                <line x1=${e.x1} y1=${e.y1} x2=${e.x2} y2=${e.y2} stroke="#8A929B" stroke-width="1" stroke-dasharray="12 4 2 4" vector-effect="non-scaling-stroke"></line>
                <circle cx=${cx} cy=${cy} r=${R} fill="#FFFFFF" stroke="#5E6670" stroke-width="1" vector-effect="non-scaling-stroke"></circle>
                <text x=${cx} y=${cy} font-size="18" font-family="Arial, sans-serif" text-anchor="middle" dominant-baseline="central" fill="#16191D">${e.nombre}</text>`;
            })}
            ${p.elementos.filter((e) => !e.columna).map(forma)}
            ${p.elementos.filter((e) => e.columna).map(forma)}
          <//>
        </div>`;
    }

    imagenes(g) {
      const pv = this.previa(g);
      if (!pv) return html`<div style="display: grid; place-items: center; height: 100%; color: #5E6670">Generando la vista en Revit…</div>`;
      const i = pv.longitudinal;
      return html`
        <${Visor} ancho=${i.ancho} alto=${i.alto} limites=${pv.capa ? limitesCapa(pv.capa) : null} etiqueta="Vista longitudinal de Revit">
          <image href=${i.url} width=${i.ancho} height=${i.alto} draggable="false"></image>
          ${pv.capa ? this.capaMovible(g, pv.capa) : null}
        <//>`;
    }

    /** La sección de la viga, a la derecha de la vista larga. */
    seccion(g) {
      const i = this.previa(g)?.seccion;
      if (!i) return null;
      return html`
        <div style="width: 100%; height: 360px; flex-shrink: 0; box-sizing: border-box; background: #FFFFFF; border: 1px solid #D9DDE2; border-radius: 6px; overflow: hidden">
          <${Visor} ancho=${i.ancho} alto=${i.alto} etiqueta="Sección de Revit">
            <image href=${i.url} width=${i.ancho} height=${i.alto} draggable="false"></image>
          <//>
        </div>`;
    }

    /** Las dos secciones superiores de la columna, lado a lado y grandes, en su propio paso (decisión 17 del plan de columnas). */
    secciones(g) {
      const pv = this.previa(g);
      if (!pv) return html`<div style="flex: 1; display: grid; place-items: center; color: #5E6670">Generando la sección en Revit…</div>`;
      const caja = (i, titulo) => html`
        <div style="display: flex; flex-direction: column; gap: 6px; min-width: 0; min-height: 0">
          <span style="font-weight: 600">${titulo}</span>
          <div style="flex: 1; min-height: 0; position: relative; background: #FFFFFF; border: 1px solid #D9DDE2; border-radius: 6px; overflow: hidden">
            ${i ? html`
              <${Visor} ancho=${i.ancho} alto=${i.alto} etiqueta=${'Sección de Revit: ' + titulo}>
                <image href=${i.url} width=${i.ancho} height=${i.alto} draggable="false"></image>
              <//>` : html`<div style="display: grid; place-items: center; height: 100%; padding: 16px; text-align: center; color: #5E6670">No se pudo saber qué varillas traslapan juntas: va solo la sección con los colores de cada varilla.</div>`}
          </div>
        </div>`;
      return html`
        <div style="flex: 1; min-height: 0; display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px">
          ${caja(pv.seccion, 'Traslapos')}
          ${caja(pv.seccionVarillas, 'Varillas')}
        </div>`;
    }

    render() {
      const { paso, modelo, plantaId, plantas, hechos, error, hecho, creando, tipo } = this.state;
      const g = this.grupoElegido();
      const ancho = window.innerWidth, alto = window.innerHeight;
      const set = (o) => this.setState(o);
      const ultimo = this.ultimo();
      const enSeccion = tipo === 'columna' && paso === 3;
      const pasos = (PASOS[tipo] ?? PASOS.viga).map((n, i) => {
        const k = i + 1, on = k === paso, ya = k < paso;
        return { n, num: ya ? '✓' : String(k), color: on ? '#16191D' : '#5E6670', fw: on ? 600 : 400, fondo: on || ya ? AZUL : '#DDE1E6', numC: on || ya ? '#FFFFFF' : '#3F4A57', pick: () => (k === 1 ? set({ paso: 1 }) : this.irA(k)) };
      });
      const tipos = TIPOS.map((t) => ({ ...t, off: !!t.off, borde: t.k === tipo ? AZUL : '#C9CFD6', fondo: t.k === tipo ? '#E4ECF6' : '#FFFFFF', color: t.k === tipo ? '#174A8A' : t.off ? '#6B737C' : '#16191D', cursor: t.off ? 'not-allowed' : 'pointer' }));
      const elemento = tipo === 'vigueta' ? 'vigueta' : tipo === 'columna' ? 'columna' : 'viga';
      const texto = enSeccion ? TEXTOS.seccion : TEXTOS[paso === ultimo ? 3 : paso];
      const titulo = texto[0], ayuda = paso === 1 && tipo !== 'viga' ? TEXTOS[1][1].replace('una viga', 'una ' + elemento) : texto[1];
      const enEstaPlanta = g ? g.vigas.filter((v) => plantas[plantaId]?.elementos.some((e) => e.viga === v)).length : 0;
      const hechas = modelo ? modelo.grupos.filter((x) => hechos[x.indice] && this.delTipo(x)).length : 0;
      const puedeSeguir = !creando && (paso === ultimo || !!g?.tieneAcero);
      const back = () => {
        if (paso > 1) set({ paso: paso - 1, hecho: false });
        else enviar({ tipo: 'cerrar' });
      };
      const next = () => {
        if (paso < ultimo) return this.irA(paso + 1);
        if (hecho) return set({ paso: 1, hecho: false, grupo: null });
        set({ creando: true, error: null });
        enviar({ tipo: 'crearVistas', grupo: g.indice, negros: this.state.negros, letra: this.state.letra, escala: this.state.escala, escalaSeccion: this.state.escalaSeccion, movimientos: this.movimientos(g) });
      };

      return html`
        <div style="width: ${ancho}px; height: ${alto}px; display: flex; flex-direction: column; background: #F4F6F8; font-family: 'IBM Plex Sans', 'Segoe UI', sans-serif; font-size: 14px; color: #16191D; overflow: hidden">

          <div style="height: 64px; flex-shrink: 0; display: flex; align-items: center; gap: 24px; padding: 0 24px; background: #FFFFFF; border-bottom: 1px solid #D9DDE2">
            <span style="font-size: 16px; font-weight: 600">Despiece</span>
            <div style="flex: 1; display: flex; justify-content: center; align-items: center; gap: 12px">
              ${pasos.map((p) => html`
                <button type="button" onClick=${p.pick} style="display: flex; align-items: center; gap: 8px; height: 40px; padding: 0 12px; border: 0; background: transparent; font: inherit; color: ${p.color}; font-weight: ${p.fw}; cursor: pointer">
                  <span style="width: 26px; height: 26px; display: grid; place-items: center; border-radius: 13px; background: ${p.fondo}; color: ${p.numC}; font-size: 13px; font-weight: 600">${p.num}</span>${p.n}
                </button>`)}
            </div>
            <div style="width: 70px"></div>
          </div>

          <div style="flex: 1; min-height: 0; padding: 20px 24px; display: flex; flex-direction: column; gap: 14px">
            ${paso > 1 && g ? html`
              <div style="display: flex; flex-direction: column; gap: 2px; min-width: 0">
                <span style="font-size: 24px; font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap" title=${g.nombre}>${g.nombre}</span>
                <span style="color: #5E6670"><span style="font-weight: 600; color: #16191D">${titulo}</span> · ${ayuda}</span>
              </div>` : html`
              <div style="display: flex; flex-direction: column; gap: 2px">
                <span style="font-size: 22px; font-weight: 600">${titulo}</span>
                <span style="color: #5E6670">${ayuda}</span>
              </div>`}

            ${paso === 1 ? html`
              <div style="flex: 1; min-height: 0; display: grid; grid-template-columns: 360px minmax(0, 1fr); gap: 16px">
                <div style="display: flex; flex-direction: column; gap: 12px; min-height: 0; overflow: auto">
                  <div style="display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px">
                    ${tipos.map((t) => html`
                      <button type="button" disabled=${t.off} onClick=${() => t.k && this.elegirTipo(t.k)} style="display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 6px; height: 92px; border: 1px solid ${t.borde}; border-radius: 6px; background: ${t.fondo}; color: ${t.color}; font: inherit; font-weight: 500; cursor: ${t.cursor}">
                        <svg width="36" height="36" viewBox="0 0 36 36" aria-hidden="true"><path d=${t.icon} stroke="currentColor" stroke-width="2" fill="none" stroke-linejoin="round"></path></svg>${t.n}
                      </button>`)}
                  </div>
                  <span style="font-weight: 600">Plantas</span>
                  <div style="display: flex; flex-wrap: wrap; gap: 6px; max-height: 232px; overflow-y: auto; padding: 1px">
                    ${(modelo?.plantas ?? []).map((p) => html`
                      <button type="button" onClick=${() => this.verPlanta(p.id)} style="height: 36px; padding: 0 12px; border: 1px solid ${p.id === plantaId ? AZUL : '#C9CFD6'}; border-radius: 6px; background: ${p.id === plantaId ? '#E4ECF6' : '#FFFFFF'}; color: ${p.id === plantaId ? '#174A8A' : '#16191D'}; font: inherit; cursor: pointer">${p.nombre}</button>`)}
                  </div>
                  ${g ? html`
                    <div style="display: flex; flex-direction: column; gap: 4px; padding: 10px 12px; background: #FFFFFF; border: 1px solid ${AZUL}; border-radius: 6px">
                      <span style="font-weight: 600">${g.nombre}</span>
                      ${g.tieneAcero ? html`
                        <span style="font-size: 12px; color: #5E6670">${g.cantidad === 1 ? 'Acero único' : `${g.cantidad} ${elemento}s con el mismo acero`}</span>
                        ${g.esColumna ? null : html`<span style="font-size: 12px; color: #5E6670">${enEstaPlanta} tramos en esta planta, ${g.vigas.length - enEstaPlanta} en otras</span>`}` : html`
                        <span style="font-size: 12px; color: #B3261E">Esta ${elemento} no tiene acero: no se puede despiezar.</span>`}
                    </div>` : html`<span style="font-size: 12px; color: #5E6670">Ninguna ${elemento} elegida.</span>`}
                  <div style="display: flex; gap: 14px; font-size: 12px; color: #5E6670">
                    <span><span style="display: inline-block; width: 10px; height: 10px; background: ${AZUL}; margin-right: 4px"></span>Elegida</span>
                    <span><span style="display: inline-block; width: 10px; height: 10px; background: ${VERDE}; margin-right: 4px"></span>Ya tiene despiece (${hechas})</span>
                  </div>
                  ${g && (g.vistaCreada || hechos[g.indice]) ? html`
                    <div style="flex-shrink: 0; margin-top: auto; display: flex; flex-direction: column; gap: 6px">
                      <span style="font-weight: 600">Ver en Revit</span>
                      <div style="display: grid; grid-template-columns: repeat(${g.esColumna ? 3 : 2}, minmax(0, 1fr)); gap: 8px">
                        ${(g.esColumna
                          ? ['Alzado', 'Traslapos', 'Varillas'].map((n, cual) => [n, { cual }])
                          : [['Vista larga', { seccion: false }], ['Sección', { seccion: true }]]).map(([n, cual]) => html`
                          <button type="button" onClick=${() => enviar({ tipo: 'irAVista', grupo: g.indice, ...cual })} style="height: 36px; border: 1px solid #C9CFD6; border-radius: 6px; background: #FFFFFF; color: #16191D; font: inherit; cursor: pointer">${n}</button>`)}
                      </div>
                    </div>` : null}
                </div>
                <div style="position: relative; min-width: 0; min-height: 0; display: grid; place-items: center; background: #FFFFFF; border: 1px solid #D9DDE2; border-radius: 6px; overflow: hidden">
                  ${this.planta()}
                </div>
              </div>` : null}

            ${enSeccion && g ? html`
              <div style="flex: 1; min-height: 0; display: flex; flex-direction: column; gap: 8px">
                ${this.selectorTextos(g)}
                ${this.secciones(g)}
              </div>` : null}

            ${paso > 1 && !enSeccion && g ? html`
              <div style="flex: 1; min-height: 0; display: grid; grid-template-columns: minmax(0, 1fr) 460px; gap: 16px">
                <div style="position: relative; min-width: 0; min-height: 0; background: #FFFFFF; border: 1px solid #D9DDE2; border-radius: 6px; overflow: hidden">
                  ${this.vistaGrande(g)}
                </div>
                <div style="display: flex; flex-direction: column; gap: 8px; min-width: 0; min-height: 0; overflow: hidden auto">
                  ${g.esColumna ? null : this.seccion(g)}
                  ${this.selectorTextos(g)}
                  ${paso === 2 ? html`
                    <span style="font-weight: 600">Barras de ${g.nombre}</span>
                    ${g.filas.map((f) => html`
                      <div style="display: grid; grid-template-columns: 14px minmax(0, 1fr); gap: 10px; align-items: center; padding: 10px; background: #FFFFFF; border: 1px solid #D9DDE2; border-radius: 6px">
                        <span style="width: 14px; height: 14px; border-radius: 3px; background: ${f.c}"></span>
                        <span style="display: flex; flex-direction: column; gap: 2px; min-width: 0"><span style="font-family: 'IBM Plex Mono', monospace; font-size: 13px">${f.et}</span><span style="font-size: 12px; color: #5E6670">${f.d}</span></span>
                      </div>`)}` : html`
                    <span style="font-weight: 600">Vistas que se crearán</span>
                    ${g.esColumna
                      ? [g.nombre, 'Sección ' + g.nombre + ' - traslapos', 'Sección ' + g.nombre + ' - varillas'].map((n, i) => {
                          const elegida = (this.state.vistaFinal ?? 0) === i;
                          return html`<button type="button" aria-pressed=${elegida} onClick=${() => this.setState({ vistaFinal: i })} style="padding: 10px; text-align: left; background: ${elegida ? '#E4ECF6' : '#FFFFFF'}; border: 1px solid ${elegida ? AZUL : '#D9DDE2'}; border-radius: 6px; color: ${elegida ? '#174A8A' : '#16191D'}; font: inherit; cursor: pointer">${n}</button>`;
                        })
                      : [g.nombre, 'Sección ' + g.nombre].map((n) => html`<div style="padding: 10px; background: #FFFFFF; border: 1px solid #D9DDE2; border-radius: 6px">${n}</div>`)}`}
                </div>
              </div>` : null}
          </div>

          <div style="height: 68px; flex-shrink: 0; display: flex; align-items: center; justify-content: space-between; gap: 16px; padding: 0 24px; background: #FFFFFF; border-top: 1px solid #D9DDE2">
            <button type="button" onClick=${back} style="height: 44px; padding: 0 20px; border: 1px solid #C9CFD6; border-radius: 6px; background: #FFFFFF; font: inherit; color: #16191D; cursor: pointer">${paso === 1 ? 'Cerrar' : 'Atrás'}</button>
            ${error ? html`<span style="color: #B3261E; font-weight: 500">${error}</span>` : null}
            ${hecho && !error ? html`<span style="color: #1E7A3C; font-weight: 500">${hecho}</span>` : null}
            <button type="button" disabled=${!puedeSeguir} onClick=${next} style="height: 44px; padding: 0 24px; border: 0; border-radius: 6px; background: ${puedeSeguir ? AZUL : '#AEB5BD'}; color: #FFFFFF; font: inherit; font-weight: 600; cursor: pointer">${paso === ultimo ? (hecho ? 'Despiezar otro' : 'Crear vistas') : 'Siguiente'}</button>
          </div>
        </div>`;
    }
  }

  window.Pasos = Pasos;
  const destino = document.getElementById('app');
  if (destino) render(html`<${Pasos} />`, destino);
})();
