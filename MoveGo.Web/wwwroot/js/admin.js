/* Move-Go · trang quản trị: sao chép link, xác nhận thao tác, form thêm/sửa xe */
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

  /* Xem trước ảnh */
  var file = $('#imageFile', form), url = $('#ImageUrl', form), img = $('#image-preview', form), empty = $('#image-empty', form);
  var show = function (src) {
    if (src) { img.src = src; img.classList.remove('hidden'); empty.classList.add('hidden'); }
    else { img.removeAttribute('src'); img.classList.add('hidden'); empty.classList.remove('hidden'); }
  };
  file.addEventListener('change', function () {
    var f = file.files && file.files[0];
    if (!f) return;
    if (f.size > 5 * 1024 * 1024) { alert('Ảnh quá lớn, tối đa 5 MB.'); file.value = ''; return; }
    show(URL.createObjectURL(f));
  });
  url.addEventListener('change', function () { if (!file.value) show(url.value.trim()); });
})();
