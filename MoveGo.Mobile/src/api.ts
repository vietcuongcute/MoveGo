import AsyncStorage from '@react-native-async-storage/async-storage';
import { DEFAULT_API_URL } from './config';

// ---------------------------------------------------------------- Kiểu dữ liệu (khớp JSON của MoveGo.API)
export type Category = 'Car4Seats' | 'Car7Seats' | 'ElectricBike';

export type Vehicle = {
  id: number;
  name: string;
  brand: string;
  category: Category;
  categoryLabel: string;
  seats: number;
  pricePerDay: number;
  imageUrl: string | null;
  rangeKm: number | null;
  description: string;
  highlights: string[];
  isAvailable: boolean;
  images: string[] | null;
};

export type BusyRange = { from: string; to: string; kind: 'booked' | 'blocked' | string };

export type Quote = {
  days: number;
  pricePerDay: number;
  totalAmount: number;
  commissionAmount: number;
  ownerAmount: number;
};

export type BookingStatus =
  | 'PendingOwner' | 'AwaitingDeposit' | 'Confirmed' | 'Completed' | 'Rejected' | 'Expired' | 'Cancelled';

export type BankTransfer = { bankName: string; accountNumber: string; accountName: string; amount: number; content: string };

export type OwnerContact = { name: string; phone: string; zaloLink: string };

export type Booking = {
  code: string;
  status: BookingStatus;
  statusText: string;
  vehicleName: string;
  category: Category;
  categoryLabel: string;
  vehicleImageUrl: string | null;
  pickupAddress: string;
  startDate: string;
  endDate: string;
  rentalDays: number;
  totalAmount: number;
  depositAmount: number;
  ownerAmount: number;
  customerName: string;
  createdAt: string;
  holdSecondsLeft: number | null;
  checkoutUrl: string | null;
  qrCode: string | null;
  isMockPayment: boolean;
  owner: OwnerContact | null;
  rejectReason: string | null;
  bank?: BankTransfer | null;
};

export type BookingSummary = {
  code: string;
  status: BookingStatus;
  statusText: string;
  vehicleName: string;
  vehicleImageUrl: string | null;
  startDate: string;
  endDate: string;
  totalAmount: number;
  createdAt: string;
};

export type CreateBookingInput = {
  vehicleId: number;
  startDate: string;
  endDate: string;
  customerName: string;
  customerPhone: string;
  pickupAddress: string;
  customerNote?: string;
};

// ---------------------------------------------------------------- Địa chỉ API (lưu trên máy)
const KEY_URL = 'mg_api_url';
let cachedBase: string | null = null;

export const normalizeUrl = (raw: string): string => {
  let s = raw.trim().replace(/\/+$/, '');
  if (s && !/^https?:\/\//i.test(s)) s = `http://${s}`;
  return s;
};

export async function getBaseUrl(): Promise<string> {
  if (cachedBase) return cachedBase;
  try {
    const saved = await AsyncStorage.getItem(KEY_URL);
    cachedBase = saved ? normalizeUrl(saved) : normalizeUrl(DEFAULT_API_URL);
  } catch {
    cachedBase = normalizeUrl(DEFAULT_API_URL);
  }
  return cachedBase;
}

export async function setBaseUrl(url: string): Promise<string> {
  const v = normalizeUrl(url);
  cachedBase = v;
  try { await AsyncStorage.setItem(KEY_URL, v); } catch { /* bỏ qua */ }
  return v;
}

/** Ảnh xe: API trả đường dẫn tương đối (/uploads/...) hoặc link https đầy đủ. */
export const absoluteUrl = (base: string, path: string | null | undefined): string | null => {
  if (!path) return null;
  return /^https?:\/\//i.test(path) ? path : `${base}${path.startsWith('/') ? '' : '/'}${path}`;
};

// ---------------------------------------------------------------- Gọi API
export class ApiError extends Error {
  status: number;
  constructor(message: string, status: number) {
    super(message);
    this.status = status;
  }
}

async function request<T>(method: string, path: string, body?: unknown, timeoutMs = 15000): Promise<T> {
  const base = await getBaseUrl();
  const ctrl = new AbortController();
  const timer = setTimeout(() => ctrl.abort(), timeoutMs);
  try {
    const res = await fetch(`${base}${path}`, {
      method,
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
      signal: ctrl.signal,
    });
    const text = await res.text();
    let json: unknown = null;
    try { json = text ? JSON.parse(text) : null; } catch { /* không phải JSON */ }
    if (!res.ok) {
      // API trả lỗi dạng ProblemDetails: { title: "Thông báo tiếng Việt" }
      const title = (json as { title?: string } | null)?.title;
      throw new ApiError(title || `Lỗi ${res.status}`, res.status);
    }
    return json as T;
  } catch (e) {
    if (e instanceof ApiError) throw e;
    if ((e as Error).name === 'AbortError')
      throw new ApiError('Máy chủ phản hồi quá lâu. Kiểm tra địa chỉ API trong tab Cài đặt.', 0);
    throw new ApiError('Không kết nối được máy chủ. Kiểm tra mạng và địa chỉ API trong tab Cài đặt.', 0);
  } finally {
    clearTimeout(timer);
  }
}

export const api = {
  vehicles: (category?: Category) =>
    request<Vehicle[]>('GET', `/api/vehicles${category ? `?category=${category}` : ''}`),
  vehicle: (id: number) => request<Vehicle>('GET', `/api/vehicles/${id}`),
  busy: (id: number) => request<BusyRange[]>('GET', `/api/vehicles/${id}/busy`),
  quote: (vehicleId: number, startDate: string, endDate: string) =>
    request<Quote>('POST', '/api/booking/quote', { vehicleId, startDate, endDate }),
  createBooking: (input: CreateBookingInput) => request<Booking>('POST', '/api/booking/create', input),
  booking: (code: string) => request<Booking>('GET', `/api/booking/${encodeURIComponent(code)}`),
  lookupPhone: (phone: string) => request<BookingSummary[]>('POST', '/api/booking/lookup', { phone }),
  mockPay: (code: string) => request<Booking>('POST', `/api/payments/mock/${encodeURIComponent(code)}/complete`),
  ping: () => request<unknown>('GET', '/api/meta', undefined, 8000),
};

// ---------------------------------------------------------------- Mã đơn / SĐT
const CODE_ALPHABET = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';

/** "mg k7p2qx" / "K7P2QX" → "MG-K7P2QX"; sai định dạng → null (giống trang web) */
export function normalizeCode(input: string): string | null {
  const s = input.replace(/[^a-zA-Z0-9]/g, '').toUpperCase();
  const body = s.length === 8 && s.startsWith('MG') ? s.slice(2) : s.length === 6 ? s : null;
  return body && [...body].every((c) => CODE_ALPHABET.includes(c)) ? `MG-${body}` : null;
}

export const looksLikePhone = (input: string): boolean =>
  /^[\d+\s.\-()]+$/.test(input.trim()) && (input.match(/\d/g) ?? []).length >= 9;

// ---------------------------------------------------------------- Đơn gần đây (lưu trên máy)
export type RecentOrder = { code: string; vehicle: string; at: number };
const KEY_ORDERS = 'mg_orders';

export async function getRecentOrders(): Promise<RecentOrder[]> {
  try {
    const raw = await AsyncStorage.getItem(KEY_ORDERS);
    const arr = raw ? (JSON.parse(raw) as RecentOrder[]) : [];
    return Array.isArray(arr) ? arr.filter((o) => /^MG-[A-Z0-9]{6}$/.test(o.code)).slice(0, 5) : [];
  } catch {
    return [];
  }
}

export async function rememberOrder(code: string, vehicle: string): Promise<void> {
  const list = (await getRecentOrders()).filter((o) => o.code !== code);
  list.unshift({ code, vehicle, at: Date.now() });
  try { await AsyncStorage.setItem(KEY_ORDERS, JSON.stringify(list.slice(0, 5))); } catch { /* bỏ qua */ }
}
