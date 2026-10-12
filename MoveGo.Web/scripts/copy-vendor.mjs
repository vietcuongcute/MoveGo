// Sao chép font / icon / thư viện từ node_modules vào wwwroot/lib để website chạy KHÔNG cần CDN.
// Chạy:  npm run vendor      (kết quả đã được commit sẵn nên bạn không bắt buộc phải chạy)
import { cpSync, mkdirSync, readFileSync, writeFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';

const nm = 'node_modules';
const out = 'wwwroot/lib';
mkdirSync(out, { recursive: true });

// ---- Font Be Vietnam Pro (có đủ dấu tiếng Việt) ----
const fontDir = join(out, 'fonts');
mkdirSync(fontDir, { recursive: true });
let fontCss = '/* Be Vietnam Pro - tự host (SIL OFL). Sinh bởi scripts/copy-vendor.mjs */\n';
for (const w of [400, 500, 600, 700, 800]) {
  const css = readFileSync(join(nm, '@fontsource/be-vietnam-pro', `${w}.css`), 'utf8');
  for (const block of css.match(/@font-face\s*{[^}]+}/g) ?? []) {
    const m = block.match(/url\(\.\/files\/([^)]+?\.woff2)\)/);
    if (!m) continue;
    const file = m[1];
    cpSync(join(nm, '@fontsource/be-vietnam-pro/files', file), join(fontDir, file));
    fontCss += block
      .replace(/src:[^;]+;/, `src: url(../lib/fonts/${file}) format("woff2");`)
      .replace(/font-display:\s*swap;?/, 'font-display: swap;') + '\n';
  }
}
mkdirSync('wwwroot/css', { recursive: true });
writeFileSync('wwwroot/css/fonts.css', fontCss);

// ---- Font Awesome (chỉ solid + brands) ----
const fa = join(out, 'fontawesome');
mkdirSync(join(fa, 'css'), { recursive: true });
mkdirSync(join(fa, 'webfonts'), { recursive: true });
const faSrc = join(nm, '@fortawesome/fontawesome-free');
for (const f of ['fontawesome.min.css', 'solid.min.css', 'brands.min.css']) cpSync(join(faSrc, 'css', f), join(fa, 'css', f));
for (const f of ['fa-solid-900.woff2', 'fa-brands-400.woff2']) cpSync(join(faSrc, 'webfonts', f), join(fa, 'webfonts', f));

// ---- Flatpickr (lịch chọn ngày) ----
const fp = join(out, 'flatpickr');
mkdirSync(fp, { recursive: true });
cpSync(join(nm, 'flatpickr/dist/flatpickr.min.js'), join(fp, 'flatpickr.min.js'));
cpSync(join(nm, 'flatpickr/dist/flatpickr.min.css'), join(fp, 'flatpickr.min.css'));
cpSync(join(nm, 'flatpickr/dist/l10n/vn.js'), join(fp, 'vn.js'));

// ---- QR code ----
const qr = join(out, 'qrcode');
mkdirSync(qr, { recursive: true });
cpSync(join(nm, 'qrcode-generator/qrcode.js'), join(qr, 'qrcode.js'));

console.log('Đã sao chép thư viện vào wwwroot/lib');
