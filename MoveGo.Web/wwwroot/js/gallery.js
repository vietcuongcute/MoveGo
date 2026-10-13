/* MoveGo · cửa sổ xem bộ ảnh xe: phóng to, mũi tên / vuốt / phím ← → để chuyển ảnh, Esc để đóng */
(function () {
  'use strict';
  var root = document.getElementById('gallery');
  if (!root) return;
  var $ = function (id) { return document.getElementById(id); };
  var img = $('gallery-img'), thumbs = $('gallery-thumbs'), prev = $('gallery-prev'), next = $('gallery-next');
  var title = $('gallery-title'), count = $('gallery-count'), stage = $('gallery-stage');
  var s = { list: [], i: 0, opener: null };

  function show(i) {
    if (!s.list.length) return;
    s.i = Math.max(0, Math.min(s.list.length - 1, i));
    img.style.animation = 'none'; void img.offsetWidth; img.style.animation = '';   // chạy lại hiệu ứng xuất hiện
    img.src = s.list[s.i];
    img.alt = title.textContent + ' - ảnh ' + (s.i + 1);
    count.textContent = 'Ảnh ' + (s.i + 1) + '/' + s.list.length;
    prev.disabled = s.i === 0; next.disabled = s.i === s.list.length - 1;
    var multi = s.list.length > 1;
    prev.style.display = next.style.display = thumbs.style.display = multi ? '' : 'none';
    Array.prototype.forEach.call(thumbs.children, function (t, k) {
      t.classList.toggle('is-current', k === s.i);
      if (k === s.i) t.scrollIntoView({ block: 'nearest', inline: 'center', behavior: 'smooth' });
    });
    [s.i - 1, s.i + 1].forEach(function (k) { if (s.list[k]) { var p = new Image(); p.src = s.list[k]; } });   // tải trước ảnh kế bên
  }

  function open(list, start, name, opener) {
    if (!list || !list.length) return;
    s.list = list; s.opener = opener || document.activeElement;
    title.textContent = name || '';
    thumbs.innerHTML = '';
    list.forEach(function (u, k) {
      var b = document.createElement('button'); b.type = 'button'; b.className = 'gallery-thumb'; b.setAttribute('aria-label', 'Xem ảnh ' + (k + 1));
      var t = document.createElement('img'); t.src = u; t.alt = ''; t.loading = 'lazy'; b.appendChild(t);
      b.addEventListener('click', function () { show(k); });
      thumbs.appendChild(b);
    });
    root.classList.add('is-open'); root.setAttribute('aria-hidden', 'false');
    document.documentElement.style.overflow = 'hidden';
    show(start || 0);
    $('gallery-close').focus();
  }

  function close() {
    root.classList.remove('is-open'); root.setAttribute('aria-hidden', 'true');
    document.documentElement.style.overflow = '';
    img.removeAttribute('src');
    if (s.opener && s.opener.focus) s.opener.focus();
  }

  document.addEventListener('click', function (e) {
    var t = e.target.closest('[data-gallery]');
    if (!t || t.classList.contains('hidden')) return;
    var list; try { list = JSON.parse(t.getAttribute('data-gallery')); } catch (err) { return; }
    e.preventDefault(); e.stopPropagation();
    open(list, parseInt(t.getAttribute('data-gallery-start') || '0', 10), t.getAttribute('data-gallery-title') || '', t);
  }, true);

  $('gallery-close').addEventListener('click', close);
  prev.addEventListener('click', function () { show(s.i - 1); });
  next.addEventListener('click', function () { show(s.i + 1); });
  // bấm vào vùng tối quanh ảnh thì đóng
  stage.addEventListener('click', function (e) { if (e.target === stage) close(); });

  document.addEventListener('keydown', function (e) {
    if (!root.classList.contains('is-open')) return;
    if (e.key === 'Escape') { e.stopPropagation(); close(); }
    else if (e.key === 'ArrowLeft') show(s.i - 1);
    else if (e.key === 'ArrowRight') show(s.i + 1);
  }, true);

  // vuốt trái/phải trên điện thoại
  var x0 = null;
  stage.addEventListener('touchstart', function (e) { x0 = e.touches[0].clientX; }, { passive: true });
  stage.addEventListener('touchend', function (e) {
    if (x0 === null) return;
    var dx = e.changedTouches[0].clientX - x0; x0 = null;
    if (Math.abs(dx) > 50) show(s.i + (dx < 0 ? 1 : -1));
  }, { passive: true });

  window.MOVEGO = window.MOVEGO || {};
  window.MOVEGO.openGallery = open;
})();
