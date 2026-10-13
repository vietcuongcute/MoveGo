import React, { useCallback, useEffect, useState } from 'react';
import { Keyboard, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import {
  api, BookingSummary, getRecentOrders, looksLikePhone, normalizeCode, RecentOrder,
} from '../api';
import { dmy, vnd } from '../format';
import { Nav } from '../nav';
import { C } from '../theme';
import { Btn, Card, ErrorBox, Field, Header, StatusBadge } from '../ui';

export default function LookupScreen({ nav }: { nav: Nav }) {
  const [text, setText] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [results, setResults] = useState<BookingSummary[] | null>(null);
  const [recent, setRecent] = useState<RecentOrder[]>([]);

  useEffect(() => { getRecentOrders().then(setRecent); }, []);

  const submit = useCallback(async () => {
    Keyboard.dismiss();
    setError(null);
    setResults(null);
    const input = text.trim();
    if (!input) { setError('Nhập mã đơn (vd MG-K7P2QX) hoặc số điện thoại đã đặt.'); return; }

    if (looksLikePhone(input)) {
      setBusy(true);
      try {
        const list = await api.lookupPhone(input);
        setResults(list);
        if (list.length === 0) setError('Không tìm thấy đơn nào với số điện thoại này.');
      } catch (e) {
        setError((e as Error).message);
      } finally {
        setBusy(false);
      }
      return;
    }

    const code = normalizeCode(input);
    if (!code) { setError('Mã đơn gồm 6 ký tự sau "MG-" (không có O, 0, I, 1). Hoặc nhập số điện thoại.'); return; }
    nav.push({ name: 'order', code });
  }, [text, nav]);

  return (
    <View style={{ flex: 1 }}>
      <Header title="Tra cứu đơn" subtitle="Bằng mã đơn hoặc số điện thoại" />
      <ScrollView contentContainerStyle={{ padding: 16, paddingBottom: 40 }} keyboardShouldPersistTaps="handled">
        <Card>
          <Field
            label="Mã đơn hoặc số điện thoại"
            value={text}
            onChangeText={setText}
            placeholder="MG-K7P2QX  /  0901234567"
            autoCapitalize="characters"
            autoCorrect={false}
            returnKeyType="search"
            onSubmitEditing={submit}
          />
          {error ? <ErrorBox text={error} /> : null}
          <Btn title="Tra cứu" onPress={submit} loading={busy} style={{ marginTop: 14 }} />
        </Card>

        {results && results.length > 0 ? (
          <View style={{ marginTop: 18 }}>
            <Text style={s.section}>Tìm thấy {results.length} đơn</Text>
            {results.map((b) => (
              <Pressable key={b.code} onPress={() => nav.push({ name: 'order', code: b.code })} style={{ marginTop: 10 }}>
                <Card>
                  <View style={s.rowBetween}>
                    <Text style={s.code}>{b.code}</Text>
                    <StatusBadge status={b.status} />
                  </View>
                  <Text style={s.vehicle}>{b.vehicleName}</Text>
                  <Text style={s.meta}>{dmy(b.startDate)} → {dmy(b.endDate)}  ·  {vnd(b.totalAmount)}</Text>
                </Card>
              </Pressable>
            ))}
          </View>
        ) : null}

        {recent.length > 0 ? (
          <View style={{ marginTop: 22 }}>
            <Text style={s.section}>Đơn đặt gần đây trên máy này</Text>
            {recent.map((o) => (
              <Pressable key={o.code} onPress={() => nav.push({ name: 'order', code: o.code })} style={{ marginTop: 10 }}>
                <Card style={{ paddingVertical: 12 }}>
                  <View style={s.rowBetween}>
                    <View style={{ flex: 1 }}>
                      <Text style={s.code}>{o.code}</Text>
                      <Text style={s.meta} numberOfLines={1}>{o.vehicle}</Text>
                    </View>
                    <Text style={{ color: C.green, fontSize: 22 }}>›</Text>
                  </View>
                </Card>
              </Pressable>
            ))}
          </View>
        ) : null}
      </ScrollView>
    </View>
  );
}

const s = StyleSheet.create({
  section: { color: C.inkSoft, fontWeight: '800', fontSize: 14 },
  rowBetween: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', gap: 10 },
  code: { color: C.ink, fontWeight: '800', fontSize: 16, letterSpacing: 0.5 },
  vehicle: { color: C.inkSoft, fontSize: 15, fontWeight: '600', marginTop: 8 },
  meta: { color: C.muted, fontSize: 13, marginTop: 3 },
});
