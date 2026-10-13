export const vnd = (n: number): string => `${Math.round(n).toLocaleString('vi-VN')}đ`;

const pad = (n: number) => String(n).padStart(2, '0');

/** Chuỗi ngày giờ KHÔNG kèm múi giờ (API đang dùng giờ Việt Nam): 2026-10-23T08:00:00 */
export const isoLocal = (d: Date): string =>
  `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}:00`;

export const dmy = (d: Date | string): string => {
  const x = typeof d === 'string' ? new Date(d) : d;
  return `${pad(x.getDate())}/${pad(x.getMonth() + 1)}/${x.getFullYear()}`;
};

export const dmyHm = (d: Date | string): string => {
  const x = typeof d === 'string' ? new Date(d) : d;
  return `${pad(x.getHours())}:${pad(x.getMinutes())} ${dmy(x)}`;
};

const DAY = ['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'];
export const dayName = (d: Date): string => DAY[d.getDay()];

/** Ngày hôm nay + n ngày, lúc 08:00 */
export const dayAt8 = (offsetDays: number): Date => {
  const d = new Date();
  d.setDate(d.getDate() + offsetDays);
  d.setHours(8, 0, 0, 0);
  return d;
};

export const mmss = (sec: number): string => `${pad(Math.floor(sec / 60))}:${pad(sec % 60)}`;
