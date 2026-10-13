/* MoveGo · trang theo dõi đơn: tự cập nhật trạng thái, đếm ngược giữ xe, vẽ mã QR cọc */
(function () {
  'use strict';
  var root = document.getElementById('track');
  if (!root) return;

  var $ = function (s, r) { return (r || document).querySelector(s); };
  var API = (window.MOVEGO && window.MOVEGO.apiBase) || '';
  var code = root.getAttribute('data-code');
  var status = root.getAttribute('data-status');
  var liveNote = $('#live-note');

  function setNote(text) { if (liveNote) liveNote.textContent = text; }

  /* ---------- Sao chép mã đơn ---------- */
  var copyBtn = $('#copy-code');
  if (copyBtn) copyBtn.addEventListener('click', function () {
    var done = function () { window.toast('Đã sao chép mã đơn ' + code); };
    if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(code).then(done, done);
    else done();
  });

  /* ---------- Mã QR (nội dung VietQR do PayOS trả về) ---------- */
  var qrFrame = $('#qr-frame');
  if (qrFrame) {
    var data = qrFrame.getAttribute('data-qr');
    var target = $('#qr-target');
    if (data && window.qrcode) {
      try {
        var qr = window.qrcode(0, 'M');
        qr.addData(data); qr.make();
        target.outerHTML = qr.createSvgTag({ scalable: true, margin: 0, alt: 'Mã QR đặt cọc ' + code });
      } catch (e) {
        target.innerHTML = '<span class="px-3 text-center text-xs text-slate-400">Không tạo được mã QR.<br>Hãy dùng nút Mở trang thanh toán.</span>';
      }
    } else if (target) {
      target.innerHTML = '<span class="px-3 text-center text-xs text-slate-400">Chưa có mã QR.<br>Vui lòng tải lại trang.</span>';
    }
  }

  /* ---------- Đếm ngược thời gian giữ xe ---------- */
  var clock = $('#hold-clock'), box = $('#hold-box');
  var hold = box ? parseInt(box.getAttribute('data-seconds'), 10) || 0 : 0;
  var holdTimer = null;
  function pad(n) { return n < 10 ? '0' + n : '' + n; }
  function tickHold() {
    if (!clock) return;
    if (hold <= 0) {
      clock.textContent = '00:00';
      box.classList.add('!bg-red-500');
      clearInterval(holdTimer);
      setNote('Đã hết thời gian giữ xe');
      setTimeout(function () { location.reload(); }, 1500);
      return;
    }
    clock.textContent = pad(Math.floor(hold / 60)) + ':' + pad(hold % 60);
    if (hold <= 300) box.classList.add('!bg-red-500');
    hold--;
  }
  if (clock) { tickHold(); holdTimer = setInterval(tickHold, 1000); }

  /* ---------- Hỏi API xem trạng thái có đổi không ---------- */
  var busy = false, fails = 0, timer = null;
  function check(manual) {
    if (busy) return Promise.resolve();
    busy = true;
    return fetch(API + '/api/booking/' + encodeURIComponent(code), { headers: { Accept: 'application/json' }, cache: 'no-store' })
      .then(function (r) { if (!r.ok) throw new Error('http ' + r.status); return r.json(); })
      .then(function (j) {
        fails = 0;
        setNote('Cập nhật lúc ' + new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit', second: '2-digit' }));
        if (j.status !== status) {
          setNote('Có cập nhật mới — đang tải lại…');
          setTimeout(function () { location.reload(); }, 400);
        } else if (manual) {
          window.toast('Chưa nhận được tiền cọc. Nếu đã chuyển khoản, vui lòng chờ vài giây.', 'info');
        }
      })
      .catch(function () {
        fails++;
        setNote('Mất kết nối, đang thử lại…');
        if (manual) window.toast('Không kết nối được hệ thống, thử lại sau nhé.', 'error');
      })
      .then(function () { busy = false; });
  }

  // Đơn đã kết thúc thì không cần hỏi liên tục.
  var finished = status === 'Completed' || status === 'Rejected' || status === 'Cancelled' || status === 'Expired';
  if (finished) setNote('Đơn đã kết thúc');
  else {
    var schedule = function () {
      var delay = status === 'AwaitingDeposit' ? 4000 : 8000;
      timer = setTimeout(function () { if (document.hidden) return schedule(); check().then(schedule); }, delay * Math.min(1 + fails, 5));
    };
    schedule();
    document.addEventListener('visibilitychange', function () { if (!document.hidden) check(); });
  }

  var refresh = $('#refresh-now');
  if (refresh) refresh.addEventListener('click', function () {
    refresh.disabled = true;
    check(true).then(function () { setTimeout(function () { refresh.disabled = false; }, 1500); });
  });

  /* ---------- Giả lập thanh toán (chỉ khi API chạy chế độ mock) ---------- */
  var mock = $('#mock-pay');
  if (mock) mock.addEventListener('click', function () {
    mock.disabled = true;
    mock.innerHTML = '<span class="spinner"></span> Đang xử lý…';
    fetch(API + '/api/payments/mock/' + encodeURIComponent(code) + '/complete', { method: 'POST' })
      .then(function (r) {
        if (!r.ok) throw new Error('http ' + r.status);
        window.toast('Đã ghi nhận thanh toán!');
        setTimeout(function () { location.reload(); }, 500);
      })
      .catch(function () {
        window.toast('Không giả lập được (API phải chạy ở môi trường Development).', 'error');
        mock.disabled = false; mock.innerHTML = '<i class="fa-solid fa-wand-magic-sparkles"></i> Giả lập đã thanh toán';
      });
  });
})();
