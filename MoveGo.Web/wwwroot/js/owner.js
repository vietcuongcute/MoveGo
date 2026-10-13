/* MoveGo · trang chủ xe: form từ chối đơn, chọn khoảng ngày để khoá lịch */
(function () {
  'use strict';
  var $ = function (s, r) { return (r || document).querySelector(s); };
  var $$ = function (s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };

  /* ---------- Trang xác nhận đơn ---------- */
  var form = $('#respond-form');
  if (form) {
    var decision = $('#decision'), box = $('#reject-box'), toggle = $('#reject-toggle'), confirmBtn = $('#reject-confirm');
    toggle.addEventListener('click', function () {
      var open = box.classList.toggle('hidden') === false;
      confirmBtn.classList.toggle('hidden', !open);
      toggle.innerHTML = open ? '<i class="fa-solid fa-rotate-left"></i> Quay lại' : '<i class="fa-solid fa-xmark"></i> Từ chối';
      if (open) $('#reason').focus();
    });
    $$('button[data-decision]', form).forEach(function (btn) {
      btn.addEventListener('click', function () { decision.value = btn.getAttribute('data-decision'); });
    });
    // Chống bấm 2 lần
    form.addEventListener('submit', function () {
      $$('button', form).forEach(function (b) { setTimeout(function () { b.disabled = true; }, 0); });
    });
  }

  /* ---------- Trang quản lý: chọn ngày khoá ---------- */
  var ranges = $$('.block-range');
  if (ranges.length && window.flatpickr) {
    window.flatpickr.localize(window.flatpickr.l10ns.vn);
    ranges.forEach(function (input) {
      var f = input.closest('form');
      var from = $('input[name=from]', f), to = $('input[name=to]', f);
      window.flatpickr(input, {
        mode: 'range', minDate: 'today', dateFormat: 'd/m/Y', disableMobile: true, locale: window.flatpickr.l10ns.vn,
        onChange: function (sel) {
          if (sel.length === 2) { from.value = window.flatpickr.formatDate(sel[0], 'Y-m-d'); to.value = window.flatpickr.formatDate(sel[1], 'Y-m-d'); }
          else if (sel.length === 1) { from.value = to.value = window.flatpickr.formatDate(sel[0], 'Y-m-d'); }
          else { from.value = to.value = ''; }
        }
      });
      f.addEventListener('submit', function (e) {
        if (!from.value || !to.value) { e.preventDefault(); window.toast('Vui lòng chọn ngày cần khoá.', 'error'); input._flatpickr.open(); }
      });
    });
  }

  /* ---------- Ô lịch hiện lần lượt ---------- */
  var cells = $$('.cal-cell');
  if (cells.length && 'IntersectionObserver' in window && !window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
    cells.forEach(function (c) { c.style.opacity = '0'; c.style.transform = 'scale(.6)'; });
    var seen = new Set();
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (en) {
        if (!en.isIntersecting) return;
        var grid = en.target; io.unobserve(grid);
        $$('.cal-cell', grid).forEach(function (c, i) {
          setTimeout(function () { c.style.transition = 'opacity .35s, transform .45s cubic-bezier(.3,1.4,.4,1)'; c.style.opacity = ''; c.style.transform = ''; setTimeout(function () { c.style.transition = ''; }, 500); }, i * 14);
        });
      });
    }, { threshold: .15 });
    $$('.cal-cell').map(function (c) { return c.parentElement; }).forEach(function (g) { if (!seen.has(g)) { seen.add(g); io.observe(g); } });
  }
})();
