/* Move-Go · hiệu ứng & tương tác chung (Vanilla JS, không phụ thuộc thư viện ngoài trừ Flatpickr) */
(function () {
  'use strict';
  var reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  var fine = window.matchMedia('(hover: hover) and (pointer: fine)').matches;
  var $ = function (s, r) { return (r || document).querySelector(s); };
  var $$ = function (s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };

  /* ---------- Toast ---------- */
  window.toast = function (message, type) {
    var host = $('#toasts'); if (!host) return;
    var el = document.createElement('div');
    var color = type === 'error' ? 'bg-red-500' : type === 'info' ? 'bg-ink-800' : 'bg-emerald-500';
    var icon = type === 'error' ? 'fa-circle-exclamation' : type === 'info' ? 'fa-circle-info' : 'fa-circle-check';
    el.className = 'toast pointer-events-auto flex max-w-sm items-center gap-3 rounded-2xl px-5 py-3.5 text-sm font-semibold text-white shadow-lift ' + color;
    el.innerHTML = '<i class="fa-solid ' + icon + '"></i><span></span>';
    el.lastChild.textContent = message;
    host.appendChild(el);
    setTimeout(function () { el.classList.add('is-out'); setTimeout(function () { el.remove(); }, 380); }, 3800);
  };

  /* ---------- Header: kính mờ khi cuộn, menu mobile ---------- */
  var header = $('#site-header');
  var overlay = header && !header.classList.contains('is-solid');
  var progress = $('#scroll-progress');
  var toTop = $('#to-top');
  function onScroll() {
    var y = window.scrollY || document.documentElement.scrollTop;
    if (header && overlay) header.classList.toggle('is-solid', y > 40 || $('#mobile-menu:not(.hidden)') !== null);
    if (progress) {
      var h = document.documentElement.scrollHeight - window.innerHeight;
      progress.style.transform = 'scaleX(' + (h > 0 ? Math.min(1, y / h) : 0) + ')';
    }
    if (toTop) {
      var show = y > 700;
      toTop.classList.toggle('opacity-0', !show); toTop.classList.toggle('pointer-events-none', !show);
      toTop.classList.toggle('translate-y-4', !show);
    }
  }
  window.addEventListener('scroll', onScroll, { passive: true }); onScroll();
  if (toTop) toTop.addEventListener('click', function () { window.scrollTo({ top: 0, behavior: reduce ? 'auto' : 'smooth' }); });

  var burger = $('#burger'), menu = $('#mobile-menu');
  if (burger && menu) {
    burger.addEventListener('click', function () {
      var open = menu.classList.toggle('hidden') === false;
      burger.setAttribute('aria-expanded', open); burger.classList.toggle('is-open', open);
      if (header && overlay) header.classList.toggle('is-solid', open || window.scrollY > 40);
    });
    $$('a', menu).forEach(function (a) { a.addEventListener('click', function () { menu.classList.add('hidden'); burger.classList.remove('is-open'); burger.setAttribute('aria-expanded', 'false'); onScroll(); }); });
  }

  /* ---------- Xuất hiện khi cuộn (Intersection Observer) ---------- */
  var io = 'IntersectionObserver' in window ? new IntersectionObserver(function (entries) {
    entries.forEach(function (e) {
      if (!e.isIntersecting) return;
      e.target.classList.add('is-visible', 'in-view');
      io.unobserve(e.target);
    });
  }, { threshold: 0.14, rootMargin: '0px 0px -6% 0px' }) : null;
  window.observeReveal = function (root) {
    $$('.reveal, [data-inview]', root).forEach(function (el) { if (io) io.observe(el); else el.classList.add('is-visible', 'in-view'); });
  };
  window.observeReveal();

  /* ---------- Số đếm tăng dần ---------- */
  function animateCount(el) {
    var target = parseFloat(el.getAttribute('data-count')) || 0;
    if (reduce || target === 0) { el.textContent = target.toLocaleString('vi-VN'); return; }
    var dur = 1700, t0 = null;
    function step(t) {
      if (t0 === null) t0 = t;
      var p = Math.min(1, (t - t0) / dur), eased = p === 1 ? 1 : 1 - Math.pow(2, -10 * p);
      el.textContent = Math.round(target * eased).toLocaleString('vi-VN');
      if (p < 1) requestAnimationFrame(step);
    }
    requestAnimationFrame(step);
  }
  var cio = 'IntersectionObserver' in window ? new IntersectionObserver(function (entries) {
    entries.forEach(function (e) { if (e.isIntersecting) { animateCount(e.target); cio.unobserve(e.target); } });
  }, { threshold: 0.6 }) : null;
  $$('[data-count]').forEach(function (el) { if (cio) cio.observe(el); else animateCount(el); });

  /* ---------- Menu: tô sáng mục đang xem ---------- */
  var links = {};
  $$('[data-nav]').forEach(function (a) { links[a.getAttribute('data-nav')] = a; });
  if ('IntersectionObserver' in window && Object.keys(links).length) {
    var nio = new IntersectionObserver(function (entries) {
      entries.forEach(function (e) {
        if (!e.isIntersecting) return;
        Object.keys(links).forEach(function (k) { links[k].classList.toggle('is-active', k === e.target.id); });
      });
    }, { rootMargin: '-45% 0px -50% 0px' });
    Object.keys(links).forEach(function (id) { var s = document.getElementById(id); if (s) nio.observe(s); });
  }

  /* ---------- Hero: parallax theo chuột + xe chạy chậm lại sau khi vào cảnh ---------- */
  var scene = $('#hero-scene');
  var car = $('#hero-car');
  if (car) setTimeout(function () { car.classList.add('is-idle'); }, 1800);
  if (scene && !reduce && fine) {
    var layers = $$('[data-depth]', scene), mx = 0, my = 0, cx = 0, cy = 0, raf = 0;
    var hero = $('#top');
    hero.addEventListener('pointermove', function (e) {
      var r = hero.getBoundingClientRect();
      mx = (e.clientX - r.left) / r.width - 0.5; my = (e.clientY - r.top) / r.height - 0.5;
      if (!raf) raf = requestAnimationFrame(tick);
    });
    function tick() {
      cx += (mx - cx) * 0.08; cy += (my - cy) * 0.08;
      layers.forEach(function (l) {
        var d = parseFloat(l.getAttribute('data-depth'));
        l.style.transform = 'translate3d(' + (-cx * d * 2.2).toFixed(2) + 'px,' + (-cy * d * 1.1).toFixed(2) + 'px,0)';
      });
      raf = (Math.abs(mx - cx) > 0.001 || Math.abs(my - cy) > 0.001) ? requestAnimationFrame(tick) : 0;
    }
  }

  /* ---------- Thẻ tính năng: ánh sáng theo con trỏ ---------- */
  $$('[data-glow]').forEach(function (c) {
    c.addEventListener('pointermove', function (e) {
      var r = c.getBoundingClientRect();
      c.style.setProperty('--mx', (e.clientX - r.left) + 'px'); c.style.setProperty('--my', (e.clientY - r.top) + 'px');
    });
  });

  /* ---------- Thẻ xe: nghiêng 3D nhẹ ---------- */
  function bindTilt(card) {
    if (reduce || !fine) return;
    card.addEventListener('pointermove', function (e) {
      var r = card.getBoundingClientRect();
      var px = (e.clientX - r.left) / r.width - 0.5, py = (e.clientY - r.top) / r.height - 0.5;
      card.style.transform = 'perspective(1000px) rotateX(' + (-py * 5).toFixed(2) + 'deg) rotateY(' + (px * 6).toFixed(2) + 'deg) translateY(-8px)';
    });
    card.addEventListener('pointerleave', function () { card.style.transform = ''; });
  }
  $$('[data-vehicle-card]').forEach(bindTilt);

  /* ---------- Tab lọc loại xe ---------- */
  var tabs = $('#filter-tabs'), pill = $('#filter-pill');
  if (tabs) {
    var buttons = $$('.filter-tab', tabs), grid = $('#fleet-grid'), empty = $('#fleet-empty');
    function movePill(btn, instant) {
      if (!pill || !btn) return;
      if (instant) pill.style.transition = 'none';
      pill.style.width = btn.offsetWidth + 'px';
      pill.style.transform = 'translateX(' + btn.offsetLeft + 'px)';
      if (instant) { void pill.offsetWidth; pill.style.transition = ''; }
    }
    function applyFilter(key, animate) {
      var shown = 0;
      $$('[data-vehicle-card]', grid || document).forEach(function (card) {
        var match = key === 'all' || card.getAttribute('data-category') === key;
        card.classList.toggle('hidden', !match);
        if (match) {
          card.style.setProperty('--d', (shown % 3 * 0.07) + 's');
          if (animate) { card.classList.remove('anim-pop'); void card.offsetWidth; card.classList.add('anim-pop'); }
          card.classList.add('is-visible');
          shown++;
        }
      });
      if (empty) empty.classList.toggle('hidden', shown > 0);
    }
    buttons.forEach(function (b) {
      b.addEventListener('click', function () {
        buttons.forEach(function (x) { x.classList.toggle('is-active', x === b); x.setAttribute('aria-selected', x === b); });
        movePill(b); applyFilter(b.getAttribute('data-filter'), true);
      });
    });
    var current = buttons.filter(function (b) { return b.classList.contains('is-active'); })[0] || buttons[0];
    if (current) { current.classList.add('is-active'); movePill(current, true); applyFilter(current.getAttribute('data-filter'), false); }
    window.addEventListener('resize', function () { movePill($('.filter-tab.is-active', tabs), true); });
    if (document.fonts && document.fonts.ready) document.fonts.ready.then(function () { movePill($('.filter-tab.is-active', tabs), true); });
  }

  /* ---------- FAQ ---------- */
  $$('.faq-item').forEach(function (item) {
    var btn = $('button', item);
    btn.addEventListener('click', function () {
      var open = !item.classList.contains('is-open');
      $$('.faq-item.is-open').forEach(function (o) { if (o !== item) { o.classList.remove('is-open'); $('button', o).setAttribute('aria-expanded', 'false'); } });
      item.classList.toggle('is-open', open); btn.setAttribute('aria-expanded', open);
    });
  });

  /* ---------- Form tìm xe: lịch chọn khoảng ngày ---------- */
  var range = $('#s-range');
  if (range && window.flatpickr) {
    var from = $('#s-from'), to = $('#s-to');
    var preset = (from.value && to.value) ? [from.value, to.value] : null;
    window.flatpickr.localize(window.flatpickr.l10ns.vn);
    window.flatpickr(range, {
      mode: 'range', minDate: 'today', dateFormat: 'Y-m-d', altInput: true, altFormat: 'd/m/Y', altInputClass: 'field cursor-pointer',
      defaultDate: preset, showMonths: window.innerWidth > 900 ? 2 : 1, disableMobile: true,
      onChange: function (sel) {
        if (sel.length === 2) { from.value = window.flatpickr.formatDate(sel[0], 'Y-m-d'); to.value = window.flatpickr.formatDate(sel[1], 'Y-m-d'); }
        else { from.value = ''; to.value = ''; }
      }
    });
    var form = $('#search-form');
    form.addEventListener('submit', function (e) {
      if (!from.value && !to.value && range._flatpickr && range._flatpickr.selectedDates.length === 1) {
        e.preventDefault(); window.toast('Vui lòng chọn cả ngày nhận và ngày trả xe.', 'error'); range._flatpickr.open();
      }
    });
  }
})();
