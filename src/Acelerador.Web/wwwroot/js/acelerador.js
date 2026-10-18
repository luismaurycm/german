// Mejoras progresivas: todo funciona sin JavaScript; esto solo agrega el filtro en vivo,
// Mostrar/Ocultar la credencial, el resumen de módulos y el estado de carga de los botones.
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
})();
