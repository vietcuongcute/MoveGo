import React, { useEffect, useState } from 'react';
import {
  ActivityIndicator, Image, Pressable, StyleSheet, Text, TextInput, TextInputProps, View, ViewStyle,
} from 'react-native';
import { absoluteUrl, BookingStatus, Category, getBaseUrl } from './api';
import { C, R } from './theme';

// ------------------------------------------------------------------ Nút
export function Btn(props: {
  title: string;
  onPress: () => void;
  kind?: 'primary' | 'outline' | 'dark' | 'danger';
  disabled?: boolean;
  loading?: boolean;
  style?: ViewStyle;
}) {
  const kind = props.kind ?? 'primary';
  const off = props.disabled || props.loading;
  return (
    <Pressable
      onPress={off ? undefined : props.onPress}
      style={({ pressed }) => [
        s.btn,
        kind === 'primary' && { backgroundColor: C.green },
        kind === 'dark' && { backgroundColor: C.greenDark },
        kind === 'danger' && { backgroundColor: C.red },
        kind === 'outline' && { backgroundColor: C.white, borderWidth: 1.5, borderColor: C.green },
        off && { opacity: 0.5 },
        pressed && { opacity: 0.8 },
        props.style,
      ]}
    >
      {props.loading ? (
        <ActivityIndicator color={kind === 'outline' ? C.green : C.white} />
      ) : (
        <Text style={[s.btnText, kind === 'outline' && { color: C.green }]}>{props.title}</Text>
      )}
    </Pressable>
  );
}

// ------------------------------------------------------------------ Ô nhập
export function Field(props: TextInputProps & { label: string }) {
  const { label, style, ...rest } = props;
  return (
    <View style={{ marginTop: 12 }}>
      <Text style={s.label}>{label}</Text>
      <TextInput placeholderTextColor={C.muted} style={[s.input, style]} {...rest} />
    </View>
  );
}

// ------------------------------------------------------------------ Thẻ / thông báo
export function Card(props: { children: React.ReactNode; style?: ViewStyle }) {
  return <View style={[s.card, props.style]}>{props.children}</View>;
}

export function ErrorBox({ text }: { text: string }) {
  return (
    <View style={s.errorBox}>
      <Text style={s.errorText}>⚠️  {text}</Text>
    </View>
  );
}

export function Loading({ text }: { text?: string }) {
  return (
    <View style={{ padding: 40, alignItems: 'center' }}>
      <ActivityIndicator color={C.green} size="large" />
      {text ? <Text style={{ color: C.muted, marginTop: 12 }}>{text}</Text> : null}
    </View>
  );
}

export function Row({ label, value, bold }: { label: string; value: string; bold?: boolean }) {
  return (
    <View style={s.row}>
      <Text style={s.rowLabel}>{label}</Text>
      <Text style={[s.rowValue, bold && { fontWeight: '800', color: C.ink }]}>{value}</Text>
    </View>
  );
}

// ------------------------------------------------------------------ Trạng thái đơn
export const STATUS_STYLE: Record<BookingStatus, { bg: string; fg: string; label: string; icon: string }> = {
  PendingOwner: { bg: C.amberSoft, fg: C.amber, label: 'Chờ chủ xe', icon: '⏳' },
  AwaitingDeposit: { bg: C.cyanSoft, fg: C.cyan, label: 'Chờ đặt cọc', icon: '💳' },
  Confirmed: { bg: C.greenSoft, fg: C.green, label: 'Đã xác nhận', icon: '✅' },
  Completed: { bg: C.greenSoft, fg: C.green, label: 'Hoàn tất', icon: '🏁' },
  Rejected: { bg: C.redSoft, fg: C.red, label: 'Chủ xe từ chối', icon: '✖️' },
  Expired: { bg: '#eef1ef', fg: C.muted, label: 'Hết hạn', icon: '⌛' },
  Cancelled: { bg: '#eef1ef', fg: C.muted, label: 'Đã huỷ', icon: '🚫' },
};

export function StatusBadge({ status }: { status: BookingStatus }) {
  const st = STATUS_STYLE[status] ?? STATUS_STYLE.Expired;
  return (
    <View style={[s.badge, { backgroundColor: st.bg }]}>
      <Text style={{ color: st.fg, fontWeight: '800', fontSize: 12 }}>
        {st.icon} {st.label}
      </Text>
    </View>
  );
}

// ------------------------------------------------------------------ Ảnh xe (hoặc biểu tượng nếu chưa có ảnh)
const ICON: Record<Category, string> = { Car4Seats: '🚗', Car7Seats: '🚐', ElectricBike: '🛵' };
const TONE: Record<Category, string> = { Car4Seats: '#d9ecfb', Car7Seats: '#e6e0fa', ElectricBike: '#dcf5e8' };

export function VehicleThumb(props: { category: Category; imageUrl: string | null; height: number; radius?: number }) {
  const [base, setBase] = useState<string>('');
  const [failed, setFailed] = useState(false);
  useEffect(() => { getBaseUrl().then(setBase); }, []);
  const uri = base ? absoluteUrl(base, props.imageUrl) : null;
  const radius = props.radius ?? R.card;
  if (uri && !failed) {
    return (
      <Image
        source={{ uri }}
        onError={() => setFailed(true)}
        style={{ width: '100%', height: props.height, borderRadius: radius, backgroundColor: C.line }}
        resizeMode="cover"
      />
    );
  }
  return (
    <View style={{ height: props.height, borderRadius: radius, backgroundColor: TONE[props.category], alignItems: 'center', justifyContent: 'center' }}>
      <Text style={{ fontSize: props.height * 0.45 }}>{ICON[props.category]}</Text>
    </View>
  );
}

// ------------------------------------------------------------------ Thanh tiêu đề
export function Header(props: { title: string; subtitle?: string; onBack?: () => void }) {
  return (
    <View style={s.header}>
      {props.onBack ? (
        <Pressable onPress={props.onBack} hitSlop={12} style={s.back}>
          <Text style={{ color: C.white, fontSize: 22, fontWeight: '700' }}>‹</Text>
        </Pressable>
      ) : null}
      <View style={{ flex: 1 }}>
        <Text style={s.headerTitle} numberOfLines={1}>{props.title}</Text>
        {props.subtitle ? <Text style={s.headerSub} numberOfLines={1}>{props.subtitle}</Text> : null}
      </View>
    </View>
  );
}

const s = StyleSheet.create({
  btn: { minHeight: 50, borderRadius: R.pill, alignItems: 'center', justifyContent: 'center', paddingHorizontal: 20 },
  btnText: { color: C.white, fontSize: 16, fontWeight: '800' },
  label: { color: C.inkSoft, fontSize: 13, fontWeight: '700', marginBottom: 6 },
  input: {
    backgroundColor: C.white, borderWidth: 1.5, borderColor: C.line, borderRadius: R.input,
    paddingHorizontal: 14, paddingVertical: 12, fontSize: 16, color: C.ink,
  },
  card: {
    backgroundColor: C.card, borderRadius: R.card, padding: 16, borderWidth: 1, borderColor: C.line,
    shadowColor: '#0e3326', shadowOpacity: 0.06, shadowRadius: 12, shadowOffset: { width: 0, height: 4 }, elevation: 2,
  },
  errorBox: { backgroundColor: C.redSoft, borderRadius: 14, padding: 12, marginTop: 12 },
  errorText: { color: C.red, fontWeight: '700', fontSize: 14, lineHeight: 20 },
  row: { flexDirection: 'row', justifyContent: 'space-between', paddingVertical: 7, gap: 12 },
  rowLabel: { color: C.muted, fontSize: 14, flexShrink: 0 },
  rowValue: { color: C.inkSoft, fontSize: 14, fontWeight: '600', flex: 1, textAlign: 'right' },
  badge: { alignSelf: 'flex-start', borderRadius: R.pill, paddingHorizontal: 10, paddingVertical: 4 },
  header: { backgroundColor: C.greenDark, paddingHorizontal: 18, paddingBottom: 16, paddingTop: 10, flexDirection: 'row', alignItems: 'center', gap: 10 },
  back: { width: 36, height: 36, borderRadius: 18, backgroundColor: 'rgba(255,255,255,0.16)', alignItems: 'center', justifyContent: 'center' },
  headerTitle: { color: C.white, fontSize: 22, fontWeight: '800' },
  headerSub: { color: '#a9d9c5', fontSize: 13, marginTop: 2 },
});
