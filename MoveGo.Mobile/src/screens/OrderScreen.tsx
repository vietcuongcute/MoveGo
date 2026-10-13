import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Alert, Linking, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { api, ApiError, Booking, BookingStatus, rememberOrder } from '../api';
import { dmyHm, mmss, vnd } from '../format';
import { Nav } from '../nav';
import { C } from '../theme';
import * as Clipboard from 'expo-clipboard';
import QRCode from 'react-native-qrcode-svg';
import { Btn, Card, ErrorBox, Header, Loading, Row, StatusBadge, VehicleThumb } from '../ui';

const POLL_WAIT_OWNER_MS = 8000;
const POLL_WAIT_DEPOSIT_MS = 3000;   // chờ tiền về: hỏi nhanh để thấy xác nhận gần như tức thì
const WAITING: BookingStatus[] = ['PendingOwner', 'AwaitingDeposit'];

const STEPS = ['Gửi yêu cầu', 'Chủ xe xác nhận', 'Đặt cọc 10%', 'Nhận xe'];

/** Đơn đang ở bước nào (0–3) để vẽ thanh tiến trình */
function stepOf(s: BookingStatus): number {
  switch (s) {
    case 'PendingOwner': return 1;
    case 'AwaitingDeposit': return 2;
    case 'Confirmed': case 'Completed': return 3;
    default: return 0;
  }
}

export default function OrderScreen({ code, nav }: { code: string; nav: Nav }) {
  const [b, setB] = useState<Booking | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [payBusy, setPayBusy] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [left, setLeft] = useState<number | null>(null);
  const alive = useRef(true);

  const load = useCallback(async (silent: boolean) => {
    try {
      const data = await api.booking(code);
      if (!alive.current) return;
      setB(data);
      setError(null);
      setLeft(data.holdSecondsLeft);
      rememberOrder(data.code, data.vehicleName);
    } catch (e) {
      if (!alive.current) return;
      // lỗi mạng thoáng qua khi đang tự làm mới: giữ nguyên dữ liệu cũ, chỉ báo khi chưa có gì
      if (!silent || !b) setError((e as Error).message);
    }
  }, [code, b]);

  useEffect(() => {
    alive.current = true;
    load(false);
    return () => { alive.current = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [code]);

  // Tự làm mới trong lúc chờ chủ xe / chờ cọc
  useEffect(() => {
    if (!b || !WAITING.includes(b.status)) return;
    const t = setInterval(() => load(true), b.status === 'AwaitingDeposit' ? POLL_WAIT_DEPOSIT_MS : POLL_WAIT_OWNER_MS);
    return () => clearInterval(t);
  }, [b?.status, load]); // eslint-disable-line react-hooks/exhaustive-deps

  // Đếm ngược thời gian giữ xe
  useEffect(() => {
    if (left === null || left <= 0) return;
    const t = setInterval(() => setLeft((x) => (x === null ? x : Math.max(0, x - 1))), 1000);
    return () => clearInterval(t);
  }, [left === null, b?.status]); // eslint-disable-line react-hooks/exhaustive-deps

  const onRefresh = async () => { setRefreshing(true); await load(false); setRefreshing(false); };

  const pay = async () => {
    if (!b) return;
    setPayBusy(true);
    try {
      if (b.isMockPayment) {
        const done = await api.mockPay(b.code);
        setB(done);
        setLeft(done.holdSecondsLeft);
      } else if (b.checkoutUrl) {
        await Linking.openURL(b.checkoutUrl);
      }
    } catch (e) {
      Alert.alert('Không thanh toán được', e instanceof ApiError ? e.message : 'Có lỗi xảy ra, thử lại.');
    } finally {
      setPayBusy(false);
    }
  };

  const open = (url: string) => Linking.openURL(url).catch(() => Alert.alert('Không mở được liên kết', url));

  if (!b) {
    return (
      <View style={{ flex: 1 }}>
        <Header title={code} subtitle="Chi tiết đơn" onBack={nav.pop} />
        {error ? <View style={{ padding: 16 }}><ErrorBox text={error} /><Btn title="Thử lại" onPress={() => load(false)} style={{ marginTop: 14 }} /></View> : <Loading text="Đang tải đơn..." />}
      </View>
    );
  }

  const step = stepOf(b.status);
  const dead = b.status === 'Rejected' || b.status === 'Expired' || b.status === 'Cancelled';
  const holdActive = b.status === 'AwaitingDeposit' && left !== null && left > 0;

  return (
    <View style={{ flex: 1 }}>
      <Header title={b.code} subtitle={b.vehicleName} onBack={nav.pop} />
      <ScrollView
        contentContainerStyle={{ padding: 16, paddingBottom: 48 }}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={C.green} />}
      >
        <Card>
          <VehicleThumb category={b.category} imageUrl={b.vehicleImageUrl} height={140} />
          <View style={[s.between, { marginTop: 12 }]}>
            <StatusBadge status={b.status} />
            <Text style={s.small}>Tạo lúc {dmyHm(b.createdAt)}</Text>
          </View>
          <Text style={s.statusText}>{b.statusText}</Text>

          {!dead ? (
            <View style={s.steps}>
              {STEPS.map((label, i) => {
                const done = i <= step;
                return (
                  <View key={label} style={{ flex: 1, alignItems: 'center' }}>
                    <View style={[s.dot, done && { backgroundColor: C.green }]}>
                      <Text style={{ color: done ? C.white : C.muted, fontWeight: '800', fontSize: 12 }}>{done ? '✓' : i + 1}</Text>
                    </View>
                    <Text style={[s.stepLabel, done && { color: C.ink, fontWeight: '700' }]}>{label}</Text>
                  </View>
                );
              })}
            </View>
          ) : null}
        </Card>

        {b.status === 'Rejected' && b.rejectReason ? <ErrorBox text={`Lý do từ chối: ${b.rejectReason}`} /> : null}

        {b.status === 'PendingOwner' ? (
          <Card style={{ ...s.note, backgroundColor: C.amberSoft }}>
            <Text style={{ color: C.amber, fontWeight: '700', lineHeight: 20 }}>
              ⏳ Đã gửi yêu cầu tới chủ xe qua Zalo. Màn hình tự cập nhật mỗi vài giây — kéo xuống để làm mới ngay.
            </Text>
          </Card>
        ) : null}

        {b.status === 'AwaitingDeposit' ? (
          <Card style={{ ...s.note, backgroundColor: C.cyanSoft }}>
            <Text style={{ color: C.cyan, fontWeight: '800', fontSize: 16 }}>💳 Chủ xe đã đồng ý!</Text>
            <Text style={s.noteText}>
              Đặt cọc {vnd(b.depositAmount)} (10%) để giữ xe và nhận số điện thoại chủ xe.
            </Text>
            {holdActive ? (
              <Text style={s.timer}>Giữ xe còn {mmss(left as number)}</Text>
            ) : (
              <Text style={[s.noteText, { color: C.red }]}>Hết thời gian giữ xe — kéo xuống để cập nhật.</Text>
            )}
            {b.isMockPayment ? (
              <>
                <Btn title={`Thanh toán cọc ${vnd(b.depositAmount)} (giả lập)`} onPress={pay} loading={payBusy} disabled={!holdActive} style={{ marginTop: 12 }} />
                <Text style={[s.small, { textAlign: 'center', marginTop: 8 }]}>Bản thử nghiệm: bấm là coi như đã chuyển khoản thành công.</Text>
              </>
            ) : (
              <>
                {b.qrCode && holdActive ? (
                  <View style={s.qrBox}>
                    <QRCode value={b.qrCode} size={200} />
                    <Text style={s.qrHint}>Quét mã bằng app ngân hàng (máy khác) — số tiền và nội dung đã điền sẵn</Text>
                  </View>
                ) : null}
                {b.bank ? (
                  <View style={s.bankBox}>
                    <Row label="Ngân hàng" value={b.bank.bankName} />
                    <Row label="Số tài khoản" value={b.bank.accountNumber} bold />
                    <Row label="Chủ tài khoản" value={b.bank.accountName} />
                    <Row label="Số tiền" value={vnd(b.bank.amount)} bold />
                    <Row label="Nội dung (giữ nguyên)" value={b.bank.content} bold />
                  </View>
                ) : null}
                {b.bank ? (
                  <Btn title="Sao chép số tài khoản" kind="outline" onPress={async () => { await Clipboard.setStringAsync(b.bank!.accountNumber); Alert.alert('Đã sao chép', b.bank!.accountNumber); }} style={{ marginTop: 10 }} />
                ) : null}
                <Btn
                  title={b.bank ? 'Mở ảnh QR để lưu / quét' : 'Mở trang thanh toán / app ngân hàng'}
                  kind={b.bank ? 'outline' : 'primary'}
                  onPress={pay} loading={payBusy} disabled={!holdActive || !b.checkoutUrl} style={{ marginTop: 10 }}
                />
                <Text style={[s.small, { textAlign: 'center', marginTop: 8 }]}>
                  🔄 Đang chờ nhận tiền — chuyển khoản xong đơn tự xác nhận trong vài giây, không cần bấm gì thêm.
                </Text>
              </>
            )}
          </Card>
        ) : null}

        {(b.status === 'Confirmed' || b.status === 'Completed') && b.owner ? (
          <Card style={{ ...s.note, backgroundColor: C.greenSoft }}>
            <Text style={{ color: C.greenDark, fontWeight: '800', fontSize: 16 }}>✅ Đã giữ xe — liên hệ chủ xe</Text>
            <Text style={s.owner}>{b.owner.name}</Text>
            <Text style={s.phone}>{b.owner.phone}</Text>
            <View style={{ flexDirection: 'row', gap: 10, marginTop: 12 }}>
              <Btn title="📞 Gọi" onPress={() => open(`tel:${b.owner!.phone.replace(/\s/g, '')}`)} style={{ flex: 1 }} />
              {b.owner.zaloLink ? (
                <Btn title="Nhắn Zalo" kind="outline" onPress={() => open(b.owner!.zaloLink)} style={{ flex: 1 }} />
              ) : null}
            </View>
            <Text style={[s.small, { marginTop: 10 }]}>
              Còn lại {vnd(b.ownerAmount)} (90%) trả trực tiếp cho chủ xe khi nhận xe.
            </Text>
          </Card>
        ) : null}

        <Card style={{ marginTop: 14 }}>
          <Text style={s.cardTitle}>Chuyến đi</Text>
          <Row label="Xe" value={b.vehicleName} />
          <Row label="Nhận xe" value={dmyHm(b.startDate)} />
          <Row label="Trả xe" value={dmyHm(b.endDate)} />
          <Row label="Số ngày" value={`${b.rentalDays} ngày`} />
          <Row label="Địa chỉ nhận xe" value={b.pickupAddress} />
          <Row label="Người đặt" value={b.customerName} />
        </Card>

        <Card style={{ marginTop: 14 }}>
          <Text style={s.cardTitle}>Thanh toán</Text>
          <Row label="Tổng tiền thuê" value={vnd(b.totalAmount)} bold />
          <Row label="Đặt cọc qua MoveGo (10%)" value={vnd(b.depositAmount)} />
          <Row label="Trả chủ xe khi nhận (90%)" value={vnd(b.ownerAmount)} />
        </Card>

        <Pressable onPress={() => nav.goTab('vehicles')} style={{ alignItems: 'center', marginTop: 20 }}>
          <Text style={{ color: C.green, fontWeight: '800' }}>← Xem thêm xe khác</Text>
        </Pressable>
      </ScrollView>
    </View>
  );
}

const s = StyleSheet.create({
  between: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  small: { color: C.muted, fontSize: 12 },
  statusText: { color: C.inkSoft, fontSize: 14, marginTop: 10, lineHeight: 20 },
  steps: { flexDirection: 'row', marginTop: 16 },
  dot: { width: 26, height: 26, borderRadius: 13, backgroundColor: C.line, alignItems: 'center', justifyContent: 'center' },
  stepLabel: { color: C.muted, fontSize: 11, marginTop: 6, textAlign: 'center' },
  note: { marginTop: 14, borderWidth: 0 },
  noteText: { color: C.inkSoft, fontSize: 14, lineHeight: 20, marginTop: 6 },
  qrBox: { alignItems: 'center', backgroundColor: C.white, borderRadius: 16, padding: 16, marginTop: 12 },
  bankBox: { backgroundColor: C.white, borderRadius: 14, paddingHorizontal: 14, paddingVertical: 6, marginTop: 12 },
  qrHint: { color: C.muted, fontSize: 12, marginTop: 10, textAlign: 'center' },
  timer: { color: C.ink, fontSize: 22, fontWeight: '800', marginTop: 8 },
  owner: { color: C.ink, fontSize: 18, fontWeight: '800', marginTop: 8 },
  phone: { color: C.greenDark, fontSize: 22, fontWeight: '800', marginTop: 2, letterSpacing: 0.5 },
  cardTitle: { color: C.ink, fontWeight: '800', fontSize: 16, marginBottom: 6 },
});
