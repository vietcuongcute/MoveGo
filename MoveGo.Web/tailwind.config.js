/** Tailwind CSS 3 - cấu hình cho MoveGo.Web.  Build: npm run css  (hoặc npm run css:watch khi đang sửa giao diện) */
module.exports = {
  content: ['./Views/**/*.cshtml', './wwwroot/js/**/*.js'],
  theme: {
    extend: {
      fontFamily: {
        sans: ['"Be Vietnam Pro"', 'ui-sans-serif', 'system-ui', '-apple-system', '"Segoe UI"', 'Roboto', 'sans-serif']
      },
      colors: {
        // Đà Lạt Sunrise: trời xanh (sky) + xanh lá (emerald) + cam nắng cho nút chính; ink = xanh navy cho chữ và nền tối
        ink: { 950: '#0b2a4a', 900: '#102f52', 800: '#173c66', 700: '#22507f', 600: '#2f679f' }
      },
      boxShadow: {
        glow: '0 14px 40px -10px rgb(249 115 22 / .55)',
        'glow-cyan': '0 14px 40px -10px rgb(6 182 212 / .55)',
        soft: '0 18px 50px -18px rgb(16 47 82 / .24)',
        lift: '0 30px 60px -20px rgb(16 47 82 / .3)'
      },
      borderRadius: { '4xl': '2rem' }
    }
  },
  plugins: []
};
