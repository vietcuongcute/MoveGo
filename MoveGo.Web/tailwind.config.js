/** Tailwind CSS 3 - cấu hình cho MoveGo.Web.  Build: npm run css  (hoặc npm run css:watch khi đang sửa giao diện) */
module.exports = {
  content: ['./Views/**/*.cshtml', './wwwroot/js/**/*.js'],
  theme: {
    extend: {
      fontFamily: {
        sans: ['"Be Vietnam Pro"', 'ui-sans-serif', 'system-ui', '-apple-system', '"Segoe UI"', 'Roboto', 'sans-serif']
      },
      colors: {
        // Green-Tech: Emerald #10B981 + Cyan #06B6D4 dùng sẵn của Tailwind (emerald-500, cyan-500)
        ink: { 950: '#021a17', 900: '#04231f', 800: '#073029', 700: '#0b4037', 600: '#12594c' }
      },
      boxShadow: {
        glow: '0 14px 40px -10px rgb(16 185 129 / .6)',
        'glow-cyan': '0 14px 40px -10px rgb(6 182 212 / .55)',
        soft: '0 18px 50px -18px rgb(4 35 31 / .28)',
        lift: '0 30px 60px -20px rgb(4 35 31 / .35)'
      },
      borderRadius: { '4xl': '2rem' }
    }
  },
  plugins: []
};
