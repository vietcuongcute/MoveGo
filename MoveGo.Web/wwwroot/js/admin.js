/* MoveGo · trang quản trị: sao chép link, xác nhận thao tác, form thêm/sửa xe */
(function () {
  'use strict';
  var $ = function (s, r) { return (r || document).querySelector(s); };
  var $$ = function (s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };

  /* Sao chép link quản lý của chủ xe */
  $$('[data-copy]').forEach(function (btn) {
    btn.addEventListener('click', function () {
      var text = btn.getAttribute('data-copy');
      var done = function () {
        var old = btn.innerHTML;
        btn.innerHTML = '<i class="fa-solid fa-check"></i> Đã chép';
        setTimeout(function () { btn.innerHTML = old; }, 1600);
      };
      if (navigator.clipboard && window.isSecureContext) {
        navigator.clipboard.writeText(text).then(done, function () { window.prompt('Sao chép link:', text); });
      } else { window.prompt('Sao chép link:', text); }
    });
  });

  /* Hỏi lại trước thao tác không hoàn tác được */
  $$('form[data-confirm]').forEach(function (f) {
    f.addEventListener('submit', function (e) {
      if (!window.confirm(f.getAttribute('data-confirm'))) e.preventDefault();
    });
  });

  /* Chống bấm 2 lần */
  $$('form').forEach(function (f) {
    f.addEventListener('submit', function (e) {
      if (e.defaultPrevented) return;
      $$('button[type=submit]', f).forEach(function (b) { setTimeout(function () { b.disabled = true; }, 0); });
    });
  });

  /* Form xe */
  var form = $('.admin-vehicle-form');
  if (!form) return;

  var category = $('#Category', form), seats = $('#Seats', form), rangeBox = $('#range-box');
  var defaultSeats = { Car4Seats: 4, Car7Seats: 7, ElectricBike: 2 };
  category.addEventListener('change', function () {
    rangeBox.classList.toggle('hidden', category.value !== 'ElectricBike');
    seats.value = defaultSeats[category.value] || seats.value;
  });

  var price = $('#PricePerDay', form), hint = $('#price-hint', form);
  var vnd = function (n) { return n.toLocaleString('vi-VN') + 'đ'; };
  var updateHint = function () {
    var p = parseInt(price.value, 10);
    if (!p || p < 0) { hint.textContent = ''; return; }
    var deposit = Math.round(p * 0.10 / 1000) * 1000;
    hint.textContent = 'Thuê 1 ngày: khách cọc ' + vnd(deposit) + ' (10%), chủ xe nhận ' + vnd(p - deposit) + '.';
  };
  price.addEventListener('input', updateHint);
  updateHint();

  /* Bộ ảnh xe: ảnh đã có (đặt làm bìa / bỏ) + ảnh mới chọn từ máy (xem trước, bỏ từng ảnh) */
  var grid = $('#gallery-grid', form), picker = $('#imageFiles', form), addTile = $('#gal-add', form), countEl = $('#gal-count', form);
  if (!grid || !picker) return;
  var MAX = parseInt(grid.getAttribute('data-max'), 10) || 8, MAX_BYTES = 5 * 1024 * 1024;
  var pending = [];   // các File mới chọn, theo thứ tự

  var existingTiles = function () { return $$('.gal-item[data-existing]', grid); };
  var total = function () { return existingTiles().length + pending.length; };

  function syncInput() {
    try {
      var dt = new DataTransfer();
      pending.forEach(function (f) { dt.items.add(f); });
      picker.files = dt.files;
    } catch (e) { /* trình duyệt cũ: bỏ qua, giữ nguyên danh sách file */ }
  }

  function render() {
    $$('.gal-item[data-new]', grid).forEach(function (n) { URL.revokeObjectURL(n.querySelector('img').src); n.remove(); });
    pending.forEach(function (f, i) {
      var tile = document.createElement('div');
      tile.className = 'gal-item'; tile.setAttribute('data-new', '');
      var im = document.createElement('img'); im.src = URL.createObjectURL(f); im.alt = '';
      var tag = document.createElement('span'); tag.className = 'gal-tag gal-tag-new'; tag.textContent = 'Mới';
      var actions = document.createElement('div'); actions.className = 'gal-actions';
      var rm = document.createElement('button'); rm.type = 'button'; rm.className = 'gal-btn gal-btn-danger'; rm.title = 'Bỏ ảnh này';
      rm.innerHTML = '<i class="fa-solid fa-xmark"></i>';
      rm.addEventListener('click', function () { pending.splice(i, 1); syncInput(); render(); });
      actions.appendChild(rm); tile.appendChild(im); tile.appendChild(tag); tile.appendChild(actions);
      grid.insertBefore(tile, addTile);
    });
    // nhãn "Ảnh bìa" cho ảnh đầu tiên (ưu tiên ảnh đã có)
    var first = existingTiles()[0];
    $$('.gal-tag:not(.gal-tag-new)', grid).forEach(function (tg) { tg.classList.add('hidden'); });
    if (first) first.querySelector('.gal-tag').classList.remove('hidden');
    else { var n0 = $('.gal-item[data-new]', grid); if (n0) { var s = document.createElement('span'); s.className = 'gal-tag'; s.textContent = 'Ảnh bìa'; s.style.left = 'auto'; s.style.right = '.5rem'; n0.appendChild(s); } }
    $$('[data-gal=cover]', grid).forEach(function (b) { b.classList.toggle('hidden', b.closest('.gal-item') === first); });
    addTile.classList.toggle('hidden', total() >= MAX);
    countEl.textContent = total() + '/' + MAX + ' ảnh';
  }

  picker.addEventListener('change', function () {
    var added = Array.prototype.slice.call(picker.files || []), msg = '';
    added.forEach(function (f) {
      if (pending.some(function (p) { return p.name === f.name && p.size === f.size && p.lastModified === f.lastModified; })) return;
      if (!/^image\/(jpeg|png|webp)$/.test(f.type)) { msg = 'Chỉ nhận ảnh JPG, PNG hoặc WebP.'; return; }
      if (f.size > MAX_BYTES) { msg = f.name + ' quá lớn, mỗi ảnh tối đa 5 MB.'; return; }
      if (total() >= MAX) { msg = 'Mỗi xe tối đa ' + MAX + ' ảnh.'; return; }
      pending.push(f);
    });
    syncInput(); render();
    if (msg) alert(msg);
  });

  grid.addEventListener('click', function (e) {
    var btn = e.target.closest('[data-gal]');
    if (!btn) return;
    var tile = btn.closest('.gal-item');
    if (btn.getAttribute('data-gal') === 'remove') { tile.remove(); render(); }
    else if (btn.getAttribute('data-gal') === 'cover') { grid.insertBefore(tile, grid.firstChild); render(); }
  });

  render();
})();
