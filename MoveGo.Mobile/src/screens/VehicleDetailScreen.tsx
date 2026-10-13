import React, { useEffect, useMemo, useState } from 'react';
import { Image, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { absoluteUrl, api, BusyRange, getBaseUrl, Quote, rememberOrder, Vehicle } from '../api';
import { dayAt8, dayName, dmy, isoLocal, vnd } from '../format';
import { Nav } from '../nav';
import { C, R } from '../theme';
import { Btn, Card, ErrorBox, Field, Header, Loading, Row, VehicleThumb } from '../ui';

const MAX_DAYS = 30;

function Stepper(props: { label: string; value: string; onMinus: () => void; onPlus: () => void; minusOff?: boolean; plusOff?: boolean }) {
  return (
    <View style={{ flex: 1 }}>
      <Text style={s.stepLabel}>{props.label}</Text>
      <View style={s.stepper}>
        <Pressable onPress={props.onMinus} disabled={props.minusOff} style={[s.stepBtn, props.minusOff && { opacity: 0.35 }]}>
          <Text style={s.stepBtnText}>−</Text>
        </Pressable>
        <Text style={s.stepValue}>{props.value}</Text>
        <Pressable onPress={props.onPlus} disabled={props.plusOff} style={[s.stepBtn, props.plusOff && { opacity: 0.35 }]}>
          <Text style={s.stepBtnText}>+</Text>
        </Pressable>
      </View>
    </View>
  );
}

export default function VehicleDetailScreen({ id, nav }: { id: number; nav: Nav }) {
  const [vehicle, setVehicle] = useState<Vehicle | null>(null);
  const [busy, setBusy] = useState<BusyRange[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [base, setBase] = useState('');

  const [offset, setOffset] = useState(1);   // nhận xe sau mấy ngày (1 = ngày mai)
  const [days, setDays] = useState(2);
  const [quote, setQuote] = useState<Quote | null>(null);
  const [quoteError, setQuoteError] = useState<string | null>(null);

  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [address, setAddress] = useState('');
  const [note, setNote] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  useEffect(() => {
    getBaseUrl().then(setBase);
    (async () => {
      try {
        const [v, b] = await Promise.all([api.vehicle(id), api.busy(id).catch(() => [] as BusyRange[])]);
        setVehicle(v);
        setBusy(b);
      } catch (e) {
        setLoadError((e as Error).message);
      }
    })();
  }, [id]);

  const start = useMemo(() => dayAt8(offset), [offset]);
  const end = useMemo(() => dayAt8(offset + days), [offset, days]);

  // Báo giá mỗi khi đổi ngày
  useEffect(() => {
    setQuote(null);
    setQuoteError(null);
    const t = setTimeout(async () => {
      try {
        setQuote(await api.quote(id, isoLocal(start), isoLocal(end)));
      } catch (e) {
        setQuoteError((e as Error).message);
      }
    }, 250);
    return () => clearTimeout(t);
  }, [id, start, end]);

  const overlap = useMemo(
    () => busy.some((b) => new Date(b.from) < end && new Date(b.to) > start),
    [busy, start, end],
  );

  const submit = async () => {
    if (!vehicle) return;
    setSubmitting(true);
    setSubmitError(null);
    try {
      const b = await api.createBooking({
        vehicleId: vehicle.id,
        startDate: isoLocal(start),
        endDate: isoLocal(end),
        customerName: name.trim(),
        customerPhone: phone.trim(),
        pickupAddress: address.trim(),
        customerNote: note.trim() || undefined,
      });
      await rememberOrder(b.code, vehicle.name);
      nav.pop();
      nav.push({ name: 'order', code: b.code });
    } catch (e) {
      setSubmitError((e as Error).message);
    } finally {
      setSubmitting(false);
    }
  };

  if (loadError) {
    return (
      <View style={{ flex: 1 }}>
        <Header title="Chi tiết xe" onBack={nav.pop} />
        <View style={{ padding: 16 }}><ErrorBox text={loadError} /></View>
      </View>
    );
  }
  if (!vehicle) {
    return (
      <View style={{ flex: 1 }}>
        <Header title="Chi tiết xe" onBack={nav.pop} />
        <Loading />
      </View>
    );
  }

  const gallery = (vehicle.images && vehicle.images.length > 0 ? vehicle.images : vehicle.imageUrl ? [vehicle.imageUrl] : [])
    .map((p) => absoluteUrl(base, p))
    .filter((u): u is string => !!u);

  return (
    <View style={{ flex: 1 }}>
      <Header title={vehicle.name} subtitle={`${vehicle.categoryLabel} · ${vehicle.seats} chỗ`} onBack={nav.pop} />
      <ScrollView
        contentContainerStyle={{ padding: 16, paddingBottom: 60 }}
        keyboardShouldPersistTaps="handled"
        automaticallyAdjustKeyboardInsets
      >
        {gallery.length > 0 ? (
          <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: 10 }}>
            {gallery.map((u) => (
              <Image key={u} source={{ uri: u }} style={s.photo} resizeMode="cover" />
            ))}
          </ScrollView>
        ) : (
          <VehicleThumb category={vehicle.category} imageUrl={null} height={190} />
        )}

        <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-end', marginTop: 16 }}>
          <View style={{ flex: 1 }}>
            <Text style={s.title}>{vehicle.name}</Text>
            <Text style={{ color: C.muted, marginTop: 2 }}>
              {vehicle.brand}{vehicle.rangeKm ? ` · ${vehicle.rangeKm} km/lần sạc` : ''}
            </Text>
          </View>
          <View style={{ alignItems: 'flex-end' }}>
            <Text style={s.price}>{vnd(vehicle.pricePerDay)}</Text>
            <Text style={{ color: C.muted, fontSize: 12 }}>/ ngày</Text>
          </View>
        </View>

        <Text style={s.desc}>{vehicle.description}</Text>
        <View style={s.tags}>
          {vehicle.highlights.map((h) => (
            <View key={h} style={s.tag}><Text style={s.tagText}>{h}</Text></View>
          ))}
        </View>

        {busy.length > 0 ? (
          <Card style={{ marginTop: 18 }}>
            <Text style={s.h2}>Lịch xe đang bận</Text>
            {busy.slice(0, 5).map((b, i) => (
              <Text key={i} style={{ color: C.inkSoft, marginTop: 6 }}>
                • {dmy(b.from)} → {dmy(b.to)} {b.kind === 'blocked' ? '(chủ xe khoá)' : '(đã có khách)'}
              </Text>
            ))}
          </Card>
        ) : null}

        {/* ------------------------------ Đặt xe ------------------------------ */}
        <Card style={{ marginTop: 18 }}>
          <Text style={s.h2}>Đặt xe</Text>
          <View style={{ flexDirection: 'row', gap: 12, marginTop: 12 }}>
            <Stepper
              label="Ngày nhận (08:00)"
              value={`${dayName(start)} ${dmy(start).slice(0, 5)}`}
              onMinus={() => setOffset((o) => Math.max(1, o - 1))}
              onPlus={() => setOffset((o) => Math.min(120, o + 1))}
              minusOff={offset <= 1}
            />
            <Stepper
              label="Số ngày thuê"
              value={`${days} ngày`}
              onMinus={() => setDays((d) => Math.max(1, d - 1))}
              onPlus={() => setDays((d) => Math.min(MAX_DAYS, d + 1))}
              minusOff={days <= 1}
              plusOff={days >= MAX_DAYS}
            />
          </View>
          <Text style={{ color: C.muted, marginTop: 10, fontSize: 13 }}>
            Trả xe: {dayName(end)} {dmy(end)} lúc 08:00
          </Text>

          {overlap ? <ErrorBox text="Xe đã có lịch trong khoảng ngày này, hãy chọn ngày khác." /> : null}

          <View style={s.quoteBox}>
            {quote ? (
              <>
                <Row label="Tổng tiền thuê" value={vnd(quote.totalAmount)} bold />
                <Row label="Cọc online 10% (hoa hồng MoveGo)" value={vnd(quote.commissionAmount)} />
                <Row label="Trả trực tiếp cho chủ xe" value={vnd(quote.ownerAmount)} />
              </>
            ) : quoteError ? (
              <Text style={{ color: C.red, fontWeight: '700' }}>{quoteError}</Text>
            ) : (
              <Text style={{ color: C.muted }}>Đang tính giá…</Text>
            )}
          </View>

          <Field label="Họ tên" value={name} onChangeText={setName} placeholder="Nguyễn Văn A" autoCapitalize="words" />
          <Field label="Số điện thoại" value={phone} onChangeText={setPhone} placeholder="0912 345 678" keyboardType="phone-pad" />
          <Field label="Địa chỉ nhận xe" value={address} onChangeText={setAddress} placeholder="12 Trần Hưng Đạo, Đà Lạt" />
          <Field label="Ghi chú (không bắt buộc)" value={note} onChangeText={setNote} placeholder="Cần thêm mũ bảo hiểm…" />

          {submitError ? <ErrorBox text={submitError} /> : null}

          <Btn
            title="Gửi yêu cầu thuê xe"
            onPress={submit}
            loading={submitting}
            disabled={overlap || !quote}
            style={{ marginTop: 18 }}
          />
          <Text style={{ color: C.muted, fontSize: 12, marginTop: 10, textAlign: 'center', lineHeight: 18 }}>
            Chủ xe sẽ nhận yêu cầu và xác nhận. Chỉ khi chủ xe đồng ý bạn mới đặt cọc 10%.
          </Text>
        </Card>
      </ScrollView>
    </View>
  );
}

const s = StyleSheet.create({
  photo: { width: 300, height: 190, borderRadius: R.card, backgroundColor: C.line },
  title: { fontSize: 24, fontWeight: '800', color: C.ink },
  price: { fontSize: 20, fontWeight: '800', color: C.green },
  desc: { color: C.inkSoft, marginTop: 12, fontSize: 15, lineHeight: 22 },
  tags: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: 12 },
  tag: { backgroundColor: C.greenSoft, borderRadius: R.pill, paddingHorizontal: 10, paddingVertical: 5 },
  tagText: { color: C.greenDark, fontSize: 12, fontWeight: '700' },
  h2: { fontSize: 17, fontWeight: '800', color: C.ink },
  stepLabel: { color: C.inkSoft, fontSize: 13, fontWeight: '700', marginBottom: 6 },
  stepper: {
    flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', backgroundColor: C.white,
    borderWidth: 1.5, borderColor: C.line, borderRadius: R.input, paddingHorizontal: 6, paddingVertical: 6,
  },
  stepBtn: { width: 36, height: 36, borderRadius: 18, backgroundColor: C.greenSoft, alignItems: 'center', justifyContent: 'center' },
  stepBtnText: { fontSize: 20, fontWeight: '800', color: C.greenDark, marginTop: -2 },
  stepValue: { fontSize: 15, fontWeight: '800', color: C.ink },
  quoteBox: { backgroundColor: C.bg, borderRadius: 14, padding: 12, marginTop: 14, borderWidth: 1, borderColor: C.line },
});
