/* MoveGo · Máy tính giá + Popup đặt xe (AJAX tới MoveGo.API) */
(function () {
  'use strict';
  var M = window.MOVEGO || {};
  var D = M.data || { vehicles: [] };
  var API = M.apiBase || '';
  var $ = function (s, r) { return (r || document).querySelector(s); };
  var $$ = function (s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };
  var reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  var byId = {};
  D.vehicles.forEach(function (v) { byId[v.id] = v; });
  var rate = D.commissionRate || 0.1;
  var holdMinutes = D.depositHoldMinutes || 30;
  var DAY_NAMES = ['Chủ nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy'];

  /* ---------- Hàm tiện ích ---------- */
  function vnd(n) { return String(Math.round(n)).replace(/\B(?=(\d{3})+(?!\d))/g, '.') + 'đ'; }
  function pad(n) { return (n < 10 ? '0' : '') + n; }
  function iso(d) { return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()) + 'T' + pad(d.getHours()) + ':' + pad(d.getMinutes()) + ':00'; }
  function ymd(d) { return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate()); }
  function parseYmd(s) { var p = s.split('-'); return new Date(+p[0], +p[1] - 1, +p[2]); }
  function withTime(date, hhmm) { var p = hhmm.split(':'); var d = new Date(date.getTime()); d.setHours(+p[0], +p[1], 0, 0); return d; }
  function longDate(d) { return DAY_NAMES[d.getDay()] + ', ' + pad(d.getDate()) + '/' + pad(d.getMonth() + 1) + ' · ' + pad(d.getHours()) + ':' + pad(d.getMinutes()); }
  function el(html) { var t = document.createElement('div'); t.innerHTML = html.trim(); return t.firstChild; }
  function safeGet(k) { try { return localStorage.getItem(k); } catch (e) { return null; } }
  function safeSet(k, v) { try { localStorage.setItem(k, v); } catch (e) { /* trình duyệt chặn lưu trữ: bỏ qua */ } }

  /* Công thức giá PHẢN CHIẾU PricingService bên API (server luôn tính lại khi tạo đơn) */
  function calcDays(start, end) { return Math.max(1, Math.ceil((end - start) / 36e5 / 24)); }
  function quote(vehicle, days) {
    var total = vehicle.pricePerDay * days;
    var commission = Math.round(total * rate / 1000) * 1000;
    return {
      days: days, total: total, commission: commission, owner: total - commission,
      lines: [{ label: 'Thuê xe ' + days + ' ngày × ' + vnd(vehicle.pricePerDay), amount: total }]
    };
  }

  /* Số chạy mượt khi giá thay đổi */
  function tween(node, to) {
    if (!node) return;
    var from = parseFloat(node.getAttribute('data-v')) || 0;
    node.setAttribute('data-v', to);
    if (reduce || from === to) { node.textContent = vnd(to); return; }
    cancelAnimationFrame(node._raf);
    var t0 = null, dur = 600;
    (function step(t) {
      if (t0 === null) t0 = t;
      var p = Math.min(1, (t - t0) / dur), e = 1 - Math.pow(1 - p, 3);
      node.textContent = vnd(from + (to - from) * e);
      if (p < 1) node._raf = requestAnimationFrame(step);
    })(performance.now());
  }
  function renderLines(host, q, dark) {
    host.innerHTML = '';
    q.lines.forEach(function (l, i) {
      var row = el('<div class="anim-pop flex items-start justify-between gap-3" style="--d:' + (i * 0.05) + 's"><span class="' + (dark ? '' : 'text-slate-500') + '"></span><b class="whitespace-nowrap ' + (dark ? '' : 'text-ink-900') + '"></b></div>');
      row.children[0].textContent = l.label; row.children[1].textContent = vnd(l.amount);
      host.appendChild(row);
    });
  }
  function setArt(wrap, useNode, v) { wrap.style.color = v.tone; useNode.setAttribute('href', '#' + v.art); }

  /* =====================================================================
   *  MÁY TÍNH GIÁ
   * ===================================================================== */
  var calc = { id: D.vehicles.length ? D.vehicles[0].id : null, days: 3 };
  var cSel = $('#calc-vehicle'), cDays = $('#calc-days'), cDaysOut = $('#days-out');

  function renderCalc() {
    var v = byId[calc.id]; if (!v) return;
    var q = quote(v, calc.days);
    $('#calc-name').textContent = v.name;
    $('#calc-loc').textContent = v.brand + ' · ' + v.seats + ' chỗ';
    setArt($('#calc-art'), $('#calc-art-use'), v);
    renderLines($('#calc-lines'), q, false);
    tween($('#calc-total'), q.total); tween($('#calc-deposit'), q.commission); tween($('#calc-remaining'), q.owner);
    cDaysOut.textContent = calc.days;
    var max = +cDays.max, min = +cDays.min;
    cDays.style.setProperty('--p', ((calc.days - min) / (max - min) * 100) + '%');
  }
  if (cSel && D.vehicles.length) {
    cSel.addEventListener('change', function () { calc.id = +cSel.value; renderCalc(); });
    cDays.addEventListener('input', function () { calc.days = +cDays.value; renderCalc(); });
    $('#days-minus').addEventListener('click', function () { calc.days = Math.max(+cDays.min, calc.days - 1); cDays.value = calc.days; renderCalc(); });
    $('#days-plus').addEventListener('click', function () { calc.days = Math.min(+cDays.max, calc.days + 1); cDays.value = calc.days; renderCalc(); });
    $('#calc-book').addEventListener('click', function () { openBooking(calc.id, { days: calc.days }); });
    cSel.value = calc.id; renderCalc();
  }

  /* =====================================================================
   *  POPUP ĐẶT XE
   * ===================================================================== */
  var modal = $('#booking-modal');
  if (!modal) return;
  var f = {
    form: $('#bm-form'), success: $('#bm-success'), err: $('#bm-error'), submit: $('#bm-submit'),
    name: $('#bm-name'), phone: $('#bm-phone'), address: $('#bm-address'), note: $('#bm-note'),
    fromTime: $('#bm-from-time'), toTime: $('#bm-to-time'),
    rangeText: $('#bm-range-text'), busyNote: $('#bm-busy-note')
  };
  var st = { vehicle: null, fp: null, busy: [], start: null, end: null, busy_loading: false, lastFocus: null, submitting: false };
  var busyCache = {};

  // Giờ nhận/trả: 06:00 → 22:00, mỗi 30 phút
  $$('[data-time-select]', modal).forEach(function (sel) {
    for (var m = 360; m <= 1320; m += 30) { var t = pad(Math.floor(m / 60)) + ':' + pad(m % 60); var o = document.createElement('option'); o.value = t; o.textContent = t; sel.appendChild(o); }
    sel.value = '08:00';
  });

  function showError(msg) { f.err.textContent = msg; f.err.classList.toggle('hidden', !msg); if (msg) f.err.scrollIntoView({ block: 'nearest', behavior: reduce ? 'auto' : 'smooth' }); }

  /* --- lịch bận --- */
  function loadBusy(id) {
    if (busyCache[id]) return Promise.resolve(busyCache[id]);
    return fetch(API + '/api/vehicles/' + id + '/busy', { headers: { Accept: 'application/json' } })
      .then(function (r) { if (!r.ok) throw new Error('busy ' + r.status); return r.json(); })
      .then(function (rows) {
        var parsed = rows.map(function (r) { return { from: new Date(r.from), to: new Date(r.to), kind: r.kind }; });
        busyCache[id] = parsed; return parsed;
      });
  }
  function dayBusy(date) {
    var s = new Date(date.getFullYear(), date.getMonth(), date.getDate()), e = new Date(s.getTime() + 864e5);
    return st.busy.some(function (b) { return b.from < e && b.to > s; });
  }
  function conflicts(start, end) { return st.busy.some(function (b) { return b.from < end && b.to > start; }); }

  /* --- tóm tắt giá --- */
  function currentRange() {
    var sel = st.fp ? st.fp.selectedDates : [];
    if (sel.length < 1) return null;
    var d1 = sel[0], d2 = sel.length > 1 ? sel[1] : sel[0];
    return { start: withTime(d1, f.fromTime.value), end: withTime(d2, f.toTime.value), complete: sel.length > 1 };
  }
  function updateSummary() {
    var v = st.vehicle; if (!v) return;
    var r = currentRange();
    var linesHost = $('#bm-lines');
    if (!r || !r.complete) {
      f.rangeText.textContent = r ? 'Tiếp tục chạm vào ngày trả xe.' : 'Chạm vào lịch để chọn ngày nhận, rồi ngày trả.';
      f.rangeText.className = 'mt-2 text-sm font-semibold text-slate-500';
      linesHost.innerHTML = '<div class="text-emerald-100/70">Chọn ngày để xem giá chi tiết.</div>';
      tween($('#bm-total'), 0); tween($('#bm-deposit'), 0); tween($('#bm-remaining'), 0);
      st.start = st.end = null; return;
    }
    st.start = r.start; st.end = r.end;
    if (r.end <= r.start) {
      f.rangeText.textContent = 'Giờ trả xe phải sau giờ nhận xe. Hãy chọn giờ khác.';
      f.rangeText.className = 'mt-2 text-sm font-semibold text-red-500'; st.start = st.end = null; return;
    }
    if (conflicts(r.start, r.end)) {
      f.rangeText.textContent = 'Khoảng thời gian này trùng lịch đã có của xe. Vui lòng chọn lại.';
      f.rangeText.className = 'mt-2 text-sm font-semibold text-red-500'; st.start = st.end = null; return;
    }
    var days = calcDays(r.start, r.end), q = quote(v, days);
    f.rangeText.innerHTML = '<span class="text-emerald-600">Nhận:</span> ' + longDate(r.start) + '<br><span class="text-emerald-600">Trả:</span> ' + longDate(r.end) + ' <span class="ml-1 rounded-full bg-emerald-100 px-2 py-0.5 text-xs font-bold text-emerald-700">' + days + ' ngày</span>';
    f.rangeText.className = 'mt-2 text-sm font-semibold text-ink-900';
    renderLines(linesHost, q, true);
    tween($('#bm-total'), q.total); tween($('#bm-deposit'), q.commission); tween($('#bm-remaining'), q.owner);
  }

  /* --- khởi tạo lịch --- */
  function initCalendar(preset) {
    if (st.fp) { st.fp.destroy(); st.fp = null; }
    var host = $('#bm-calendar-host');
    var input = $('#bm-calendar');
    window.flatpickr.localize(window.flatpickr.l10ns.vn);
    st.fp = window.flatpickr(input, {
      inline: true, mode: 'range', minDate: 'today', dateFormat: 'Y-m-d', showMonths: 1, locale: window.flatpickr.l10ns.vn,
      disable: [function (date) { return dayBusy(date); }],
      onDayCreate: function (dObj, dStr, fp, dayElem) {
        if (dayBusy(dayElem.dateObj)) { dayElem.classList.add('is-busy'); dayElem.title = 'Đã có lịch'; }
      },
      onChange: function (sel) {
        showError('');
        if (sel.length === 2) {
          // Có ngày bận nằm giữa khoảng đã chọn?
          var a = withTime(sel[0], f.fromTime.value), b = withTime(sel[1], f.toTime.value);
          if (b > a && conflicts(a, b)) { window.toast('Khoảng này có ngày đã kín lịch, vui lòng chọn lại.', 'error'); st.fp.clear(); }
        }
        updateSummary();
      }
    });
    if (preset && preset.dates && preset.dates.length === 2) st.fp.setDate(preset.dates, true);
    else updateSummary();
  }

  /* --- mở / đóng popup --- */
  function openBooking(id, opts) {
    var v = byId[id]; if (!v) return;
    opts = opts || {};
    st.vehicle = v; st.lastFocus = document.activeElement; st.submitting = false;
    f.form.classList.remove('hidden'); f.success.classList.add('hidden'); showError('');
    f.submit.disabled = false; f.submit.querySelector('[data-label]').innerHTML = '<i class="fa-solid fa-paper-plane"></i> Gửi yêu cầu cho chủ xe';

    $('#bm-title').textContent = v.name;
    $('#bm-meta').textContent = v.categoryLabel + ' · ' + vnd(v.pricePerDay) + '/ngày';
    setArt($('#bm-art'), $('#bm-art-use'), v);
    $$('[data-hold-minutes]', modal).forEach(function (n) { n.textContent = holdMinutes; });

    // thông tin đã nhập lần trước (chỉ tiện lợi, không bắt buộc)
    f.name.value = f.name.value || safeGet('mg_name') || '';
    f.phone.value = f.phone.value || safeGet('mg_phone') || '';
    f.address.value = f.address.value || safeGet('mg_address') || '';

    var s = D.search || {};
    f.fromTime.value = s.fromTime || '08:00'; f.toTime.value = s.toTime || '08:00';

    modal.classList.add('is-open'); modal.setAttribute('aria-hidden', 'false');
    document.documentElement.style.overflow = 'hidden';
    $('.modal-panel', modal).scrollTop = 0;

    // ngày xem trước: từ form tìm xe hoặc từ máy tính giá (số ngày)
    var preset = null;
    if (s.from && s.to) preset = { dates: [parseYmd(s.from), parseYmd(s.to)] };
    else if (opts.days) { var d1 = new Date(); d1.setDate(d1.getDate() + 1); d1.setHours(0, 0, 0, 0); var d2 = new Date(d1.getTime() + (opts.days) * 864e5); preset = { dates: [d1, d2] }; }

    f.busyNote.classList.remove('hidden'); st.busy = [];
    initCalendar(null);   // vẽ lịch trước, ngày bận sẽ gạch đỏ ngay khi tải xong
    loadBusy(id).then(function (rows) { st.busy = rows; })
      .catch(function () { window.toast('Không tải được lịch của xe. Hệ thống vẫn kiểm tra lại khi bạn gửi.', 'info'); })
      .then(function () { f.busyNote.classList.add('hidden'); initCalendar(preset); if (opts.focusCalendar) setTimeout(function () { $('#bm-calendar-host').scrollIntoView({ block: 'center', behavior: reduce ? 'auto' : 'smooth' }); }, 150); });

    setTimeout(function () { var c = $('[data-close]', modal.querySelector('.modal-panel')); if (c) c.focus({ preventScroll: true }); }, 60);
  }
  function closeModal() {
    modal.classList.remove('is-open'); modal.setAttribute('aria-hidden', 'true');
    document.documentElement.style.overflow = '';
    if (st.lastFocus && st.lastFocus.focus) st.lastFocus.focus({ preventScroll: true });
  }
  window.openBooking = openBooking;

  document.addEventListener('click', function (e) {
    var open = e.target.closest('[data-open-booking]');
    if (open) { e.preventDefault(); openBooking(+open.getAttribute('data-open-booking'), { focusCalendar: open.getAttribute('data-focus') === 'calendar' }); return; }
    if (e.target.closest('[data-close]')) closeModal();
  });
  document.addEventListener('keydown', function (e) {
    if (!modal.classList.contains('is-open')) return;
    if (e.key === 'Escape') { closeModal(); return; }
    if (e.key === 'Tab') {   // giữ focus trong popup
      var f2 = $$('button, [href], input:not(.sr-only), select, textarea, [tabindex]:not([tabindex="-1"])', modal.querySelector('.modal-panel')).filter(function (n) { return n.offsetParent !== null && !n.disabled; });
      if (!f2.length) return;
      var first = f2[0], last = f2[f2.length - 1];
      if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
      else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
    }
  });
  [f.fromTime, f.toTime].forEach(function (s) { s.addEventListener('change', function () { showError(''); updateSummary(); }); });

  /* --- gửi yêu cầu --- */
  function setLoading(on) {
    st.submitting = on; f.submit.disabled = on;
    f.submit.querySelector('[data-label]').innerHTML = on ? '<span class="spinner"></span> Đang gửi…' : '<i class="fa-solid fa-paper-plane"></i> Gửi yêu cầu cho chủ xe';
  }
  f.form.addEventListener('submit', function (e) {
    e.preventDefault();
    if (st.submitting) return;
    showError('');
    var name = f.name.value.trim(), phone = f.phone.value.replace(/[\s.\-]/g, '');
    if (!st.start || !st.end) { showError('Vui lòng chọn ngày nhận và ngày trả xe trên lịch (và kiểm tra giờ nhận/trả).'); return; }
    if (name.length < 2) { showError('Vui lòng nhập họ và tên.'); f.name.focus(); return; }
    if (!/^(\+?84|0)[35789]\d{8}$/.test(phone)) { showError('Số điện thoại chưa đúng (ví dụ: 0912345678).'); f.phone.focus(); return; }

    var address = f.address.value.trim();
    if (address.length < 5) { showError('Vui lòng nhập địa chỉ bạn muốn nhận xe (ví dụ: 12 Trần Hưng Đạo, Đà Lạt).'); f.address.focus(); return; }

    setLoading(true);
    var body = {
      vehicleId: st.vehicle.id, customerName: name, customerPhone: phone, pickupAddress: address, customerNote: f.note.value.trim() || null,
      startDate: iso(st.start), endDate: iso(st.end)
    };
    fetch(API + '/api/booking/create', { method: 'POST', headers: { 'Content-Type': 'application/json', Accept: 'application/json' }, body: JSON.stringify(body) })
      .then(function (r) { return r.json().catch(function () { return {}; }).then(function (j) { return { ok: r.ok, status: r.status, json: j }; }); })
      .then(function (res) {
        if (!res.ok) {
          var msg = res.status === 429 ? 'Bạn thao tác hơi nhanh, vui lòng thử lại sau ít phút.' : (res.json.title || 'Có lỗi xảy ra, vui lòng thử lại.');
          if (res.status === 409) { delete busyCache[st.vehicle.id]; }
          showError(msg); setLoading(false); return;
        }
        safeSet('mg_name', name); safeSet('mg_phone', phone); safeSet('mg_address', address);
        $('#bm-code').textContent = res.json.code;
        $('#bm-track').setAttribute('href', '/booking/' + encodeURIComponent(res.json.code));
        f.form.classList.add('hidden'); f.success.classList.remove('hidden');
        $('.modal-panel', modal).scrollTop = 0;
        setLoading(false);
      })
      .catch(function () { showError('Không kết nối được máy chủ. Vui lòng kiểm tra mạng và thử lại.'); setLoading(false); });
  });

  $('#bm-copy').addEventListener('click', function () {
    var code = $('#bm-code').textContent;
    (navigator.clipboard ? navigator.clipboard.writeText(code) : Promise.reject()).then(function () { window.toast('Đã sao chép mã đơn ' + code); }, function () { window.toast('Mã đơn: ' + code, 'info'); });
  });
})();
