/* MoveGo · trang tra cứu đơn: tự viết hoa mã, gợi ý lại các đơn đã đặt trên thiết bị này */
(function () {
  'use strict';
  var input = document.getElementById('ma');
  if (input) {
    input.addEventListener('input', function () {
      var pos = input.selectionStart;
      input.value = input.value.toUpperCase().replace(/[^A-Z0-9+ .-]/g, '');
      try { input.setSelectionRange(pos, pos); } catch (e) { /* bỏ qua */ }
    });
  }

  var box = document.getElementById('recent'), list = document.getElementById('recent-list');
  if (!box || !list) return;
  var orders = [];
  try { orders = JSON.parse(localStorage.getItem('mg_orders') || '[]'); } catch (e) { orders = []; }
  if (!Array.isArray(orders) || !orders.length) return;

  orders.slice(0, 5).forEach(function (o) {
    if (!o || !/^MG-[A-Z0-9]{6}$/.test(o.code)) return;   // chỉ nhận đúng định dạng mã, tránh chèn nội dung lạ
    var li = document.createElement('li');
    var a = document.createElement('a');
    a.href = '/booking/' + encodeURIComponent(o.code);
    a.className = 'group flex items-center justify-between gap-3 rounded-2xl border border-slate-200 bg-white px-4 py-3 transition hover:border-emerald-400 hover:shadow-soft';
    var left = document.createElement('span'); left.className = 'min-w-0';
    var code = document.createElement('b'); code.className = 'block tracking-wider text-ink-900'; code.textContent = o.code;
    var name = document.createElement('span'); name.className = 'block truncate text-xs text-slate-400';
    var d = o.at ? new Date(o.at) : null;
    name.textContent = (o.vehicle || 'Đơn thuê xe') + (d && !isNaN(d) ? ' · ' + d.toLocaleDateString('vi-VN') : '');
    left.appendChild(code); left.appendChild(name);
    var arrow = document.createElement('i'); arrow.className = 'fa-solid fa-arrow-right text-emerald-500 transition group-hover:translate-x-1';
    a.appendChild(left); a.appendChild(arrow); li.appendChild(a); list.appendChild(li);
  });
  if (list.children.length) box.classList.remove('hidden');
})();
