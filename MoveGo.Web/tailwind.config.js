/** Tailwind CSS 3 - cấu hình cho MoveGo.Web.  Build: npm run css  (hoặc npm run css:watch khi đang sửa giao diện) */
module.exports = {
  content: ['./Views/**/*.cshtml', './wwwroot/js/**/*.js'],
  theme: {
    extend: {
      fontFamily: {
        sans: ['"Be Vietnam Pro"', 'ui-sans-serif', 'system-ui', '-apple-system', '"Segoe UI"', 'Roboto', 'sans-serif']
      },
      colors: {
        // "Thông & Nắng": xanh thông (emerald) + xanh bạc hà (cyan/sky) + xanh thông đậm cho nút chính; ink = xanh rừng đậm cho chữ và nền tối.
        // Ghi đè luôn thang emerald/cyan/sky/orange của Tailwind để toàn bộ giao diện đổi màu đồng bộ.
        emerald: { 50: '#f0faf5', 100: '#d9f4e6', 200: '#b3e8cf', 300: '#82d6b2', 400: '#4cbd92', 500: '#25a074', 600: '#1b8760', 700: '#166c4e', 800: '#13573f', 900: '#104633', 950: '#082a1f' },
        cyan: { 50: '#effbf8', 100: '#d3f5ec', 200: '#a8eadb', 300: '#74d9c4', 400: '#43c2aa', 500: '#1ea78f', 600: '#168a76', 700: '#146f61', 800: '#135850', 900: '#134943', 950: '#082b27' },
        sky: { 50: '#effbf8', 100: '#d3f5ec', 200: '#a8eadb', 300: '#74d9c4', 400: '#43c2aa', 500: '#1ea78f', 600: '#168a76', 700: '#146f61', 800: '#135850', 900: '#134943', 950: '#082b27' },
        // bỏ cam: mọi class orange-* đổi sang xanh thông để không còn màu chói
        orange: { 50: '#f0faf5', 100: '#d9f4e6', 200: '#b3e8cf', 300: '#82d6b2', 400: '#4cbd92', 500: '#25a074', 600: '#1b8760', 700: '#166c4e', 800: '#13573f', 900: '#104633', 950: '#082a1f' },
        ink: { 950: '#08241b', 900: '#0e3326', 800: '#154434', 700: '#1e5a46', 600: '#2b7660' }
      },
      boxShadow: {
        glow: '0 14px 40px -10px rgb(37 160 116 / .5)',
        'glow-cyan': '0 14px 40px -10px rgb(30 167 143 / .5)',
        soft: '0 18px 50px -18px rgb(14 51 38 / .22)',
        lift: '0 30px 60px -20px rgb(14 51 38 / .28)'
      },
      borderRadius: { '4xl': '2rem' }
    }
  },
  plugins: []
};
