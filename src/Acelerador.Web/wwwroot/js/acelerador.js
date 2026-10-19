// Mejoras progresivas: todo funciona sin JavaScript; esto solo agrega el filtro en vivo,
// Mostrar/Ocultar la credencial, el resumen de módulos, el estado de carga de los botones y de las
// pantallas que leen del sistema del cliente, las pestañas del inventario y las versiones de presupuesto.
(function () {
  'use strict';

  // Estado de carga: al enviar, el botón muestra el ícono girando y el texto de data-ocupado.
  document.addEventListener('submit', function (e) {
    var form = e.target;
    var etiqueta = form.getAttribute('data-ocupado');
    var boton = e.submitter || form.querySelector('button[type="submit"]');
    if (!etiqueta || !boton || form.dataset.enviando) return;
    form.dataset.enviando = '1';
    form.setAttribute('aria-busy', 'true');
    var tarjeta = form.closest('section');
    if (tarjeta) tarjeta.setAttribute('aria-busy', 'true');
    // El valor del botón (accion=continuar) se conserva porque se deshabilita después del envío.
    setTimeout(function () {
      boton.disabled = true;
      boton.classList.add('ocupado');
      boton.innerHTML = '<span class="spin" aria-hidden="true"></span>' + etiqueta;
    }, 0);
  });

  // HU-04 CA1: la credencial va enmascarada y solo se revela con Mostrar.
  document.querySelectorAll('[data-mostrar]').forEach(function (b) {
    var campo = document.getElementById(b.getAttribute('data-mostrar'));
    if (!campo) return;
    b.hidden = false;
    b.addEventListener('click', function () {
      var ver = campo.type === 'password';
      campo.type = ver ? 'text' : 'password';
      b.textContent = ver ? 'Ocultar' : 'Mostrar';
      b.setAttribute('aria-pressed', String(ver));
    });
  });

  // HU-02 CA5: filtro por nombre mientras se escribe (la búsqueda del servidor sigue funcionando sin JS).
  document.querySelectorAll('[data-filtro]').forEach(function (input) {
    var tabla = document.querySelector(input.getAttribute('data-filtro'));
    var vacio = document.getElementById('sin-resultados');
    if (!tabla) return;
    var normal = function (s) { return s.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase().trim(); };
    input.addEventListener('input', function () {
      var q = normal(input.value);
      var visibles = 0;
      tabla.querySelectorAll('tbody tr').forEach(function (tr) {
        var ok = normal(tr.getAttribute('data-nombre') || '').indexOf(q) >= 0;
        tr.hidden = !ok;
        if (ok) visibles++;
      });
      tabla.closest('.tblw').hidden = visibles === 0;
      if (vacio) {
        vacio.hidden = visibles !== 0;
        vacio.textContent = 'Ningún cliente coincide con «' + input.value.trim() + '».';
      }
    });
  });

  // HU-08 CA2 y CA3: el resumen se actualiza al marcar y Continuar se habilita con al menos un módulo.
  document.querySelectorAll('form[data-modulos]').forEach(function (form) {
    var resumen = form.querySelector('[data-resumen]');
    var aviso = form.querySelector('[data-sin-modulos]');
    var continuar = form.querySelector('[data-continuar]');
    var actualizar = function () {
      var marcados = form.querySelectorAll('input[name="modulos"]:checked');
      var reportes = 0;
      marcados.forEach(function (c) { reportes += parseInt(c.getAttribute('data-reportes'), 10) || 0; });
      var n = marcados.length;
      resumen.textContent = n + (n === 1 ? ' módulo' : ' módulos') + ' · ' + reportes + (reportes === 1 ? ' reporte' : ' reportes');
      aviso.hidden = n > 0;
      continuar.disabled = n === 0;
    };
    form.addEventListener('change', actualizar);
  });

  // Estado de carga de pantalla (UX-03, UX-05, UX-06, UX-09): mientras el servidor lee del sistema del cliente,
  // el contenido se reemplaza por un esqueleto o una barra de progreso con el texto de data-carga.
  var main = document.getElementById('contenido');
  function mostrarCarga(texto, tipo, detalle) {
    // Razor deja data-carga="" cuando el paso no lee del cliente: sin texto no hay estado de carga.
    if (!texto || !main || main.getAttribute('aria-busy') === 'true') return;
    var ph = main.querySelector('.ph');
    Array.prototype.forEach.call(main.children, function (c) {
      if (c !== ph) { c.setAttribute('data-oculto-carga', ''); c.hidden = true; }
    });
    var tarjeta = document.createElement('div');
    tarjeta.className = 'card carga';
    tarjeta.setAttribute('role', 'status');
    var titulo = document.createElement(tipo === 'barra' ? 'b' : 'p');
    titulo.className = tipo === 'barra' ? '' : 'row sin-margen';
    if (tipo !== 'barra') titulo.innerHTML = '<span class="spin" aria-hidden="true"></span>';
    titulo.appendChild(document.createTextNode(texto));
    tarjeta.appendChild(titulo);
    if (tipo === 'barra') {
      tarjeta.insertAdjacentHTML('beforeend', '<div class="barra" aria-hidden="true"><span></span></div>');
      if (detalle) {
        var d = document.createElement('span');
        d.className = 'muted';
        d.textContent = detalle;
        tarjeta.appendChild(d);
      }
    } else {
      tarjeta.insertAdjacentHTML('beforeend', '<div class="sk l1"></div><div class="sk l2"></div><div class="sk l2"></div><div class="sk l2"></div>');
    }
    main.setAttribute('aria-busy', 'true');
    main.appendChild(tarjeta);
  }
  function quitarCarga() {
    if (!main) return;
    main.removeAttribute('aria-busy');
    main.querySelectorAll('.carga').forEach(function (c) { c.remove(); });
    main.querySelectorAll('[data-oculto-carga]').forEach(function (c) { c.hidden = false; c.removeAttribute('data-oculto-carga'); });
  }
  // Al volver con el botón Atrás, el navegador puede restaurar la página con la carga puesta.
  window.addEventListener('pageshow', function (e) { if (e.persisted) quitarCarga(); });
  document.addEventListener('click', function (e) {
    var a = e.target.closest && e.target.closest('a[data-carga]');
    if (!a || e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
    mostrarCarga(a.getAttribute('data-carga'), a.getAttribute('data-carga-tipo'), a.getAttribute('data-carga-detalle'));
  });
  document.addEventListener('submit', function (e) {
    var origen = (e.submitter && e.submitter.hasAttribute('data-carga')) ? e.submitter : e.target;
    if (e.defaultPrevented || !origen.hasAttribute('data-carga')) return;
    var texto = origen.getAttribute('data-carga');
    var tipo = origen.getAttribute('data-carga-tipo');
    var detalle = origen.getAttribute('data-carga-detalle');
    setTimeout(function () { mostrarCarga(texto, tipo, detalle); }, 0);
  });

  // Los filtros de UX-06 y del registro se aplican al cambiar (el botón sigue funcionando sin JavaScript).
  document.querySelectorAll('form[data-auto]').forEach(function (form) {
    form.addEventListener('change', function () {
      if (form.requestSubmit) form.requestSubmit(); else form.submit();
    });
  });

  // HU-07 CA1: pestañas del inventario (patrón de pestañas de WAI-ARIA, con flechas, Inicio y Fin).
  document.querySelectorAll('[data-pestanas]').forEach(function (lista) {
    var tabs = Array.prototype.slice.call(lista.querySelectorAll('[role="tab"]'));
    var contenedor = lista.parentElement;
    function elegir(tab, enfocar) {
      tabs.forEach(function (t) {
        var si = t === tab;
        t.setAttribute('aria-selected', String(si));
        t.tabIndex = si ? 0 : -1;
        var panel = document.getElementById(t.getAttribute('aria-controls'));
        if (panel) { panel.hidden = !si; panel.setAttribute('aria-labelledby', t.id); }
      });
      if (enfocar) tab.focus();
      if (history.replaceState) {
        var url = new URL(location.href);
        url.searchParams.set('pestana', tab.id.replace('tab-', ''));
        history.replaceState(null, '', url);
      }
    }
    lista.hidden = false;
    contenedor.classList.add('con-pestanas');
    elegir(tabs.filter(function (t) { return t.getAttribute('aria-selected') === 'true'; })[0] || tabs[0], false);
    tabs.forEach(function (t, i) {
      t.addEventListener('click', function () { elegir(t, false); });
      t.addEventListener('keydown', function (e) {
        var j = e.key === 'ArrowRight' ? i + 1 : e.key === 'ArrowLeft' ? i - 1 : e.key === 'Home' ? 0 : e.key === 'End' ? tabs.length - 1 : null;
        if (j === null) return;
        e.preventDefault();
        elegir(tabs[(j + tabs.length) % tabs.length], true);
      });
    });
  });

  // HU-12 CA1: la sección de versiones aparece solo con Presupuesto marcado.
  document.querySelectorAll('[data-versiones]').forEach(function (seccion) {
    var form = seccion.closest('form');
    var presupuesto = form && form.querySelector('input[name="modulos"][value="Presupuesto"]');
    if (!presupuesto) return;
    presupuesto.addEventListener('change', function () { seccion.hidden = !presupuesto.checked; });
  });

  // HU-14: mientras la ejecución avanza, UX-09 consulta el estado y se recarga cuando cambia un paso.
  if (main && main.getAttribute('data-refrescar')) {
    var url = main.getAttribute('data-refrescar');
    var anterior = null;
    var consultar = function () {
      fetch(url, { credentials: 'same-origin', headers: { 'Accept': 'application/json' } })
        .then(function (r) { return r.json(); })
        .then(function (d) {
          var actual = JSON.stringify(d);
          if (anterior !== null && actual !== anterior) { location.reload(); return; }
          anterior = actual;
          if (d.estado === 'EnCurso') setTimeout(consultar, 1500); else location.reload();
        })
        .catch(function () { setTimeout(consultar, 3000); });
    };
    setTimeout(consultar, 1000);
  }

  // HU-25: subir el archivo no SAP con barra de progreso y arrastrar y soltar (sin JavaScript, el formulario normal funciona).
  document.querySelectorAll('form[data-subida]').forEach(function (form) {
    var zona = form.querySelector('.zona');
    var campo = form.querySelector('input[type="file"]');
    var progreso = form.querySelector('.progreso');
    var barra = progreso && progreso.querySelector('progress');
    var pct = progreso && progreso.querySelector('[data-pct]');
    if (zona && campo) {
      ['dragenter', 'dragover'].forEach(function (t) { zona.addEventListener(t, function (e) { e.preventDefault(); zona.classList.add('encima'); }); });
      ['dragleave', 'drop'].forEach(function (t) { zona.addEventListener(t, function () { zona.classList.remove('encima'); }); });
      zona.addEventListener('drop', function (e) {
        e.preventDefault();
        if (e.dataTransfer && e.dataTransfer.files.length) { campo.files = e.dataTransfer.files; }
      });
    }
    if (!window.XMLHttpRequest || !window.FormData || !progreso) return;
    form.addEventListener('submit', function (e) {
      if (!campo.files || !campo.files.length) return;
      e.preventDefault();
      var xhr = new XMLHttpRequest();
      xhr.open('POST', form.action);
      progreso.hidden = false;
      xhr.upload.addEventListener('progress', function (ev) {
        if (!ev.lengthComputable) return;
        var v = Math.round(ev.loaded * 100 / ev.total);
        barra.value = v;
        barra.textContent = v + ' %';
        pct.textContent = v + ' %';
      });
      xhr.addEventListener('load', function () { location.href = xhr.responseURL || location.href; });
      xhr.addEventListener('error', function () { form.submit(); });
      xhr.send(new FormData(form));
    });
  });
})();
