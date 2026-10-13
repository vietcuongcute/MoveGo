import React, { useCallback, useEffect, useState } from 'react';
import { FlatList, Pressable, RefreshControl, StyleSheet, Text, View } from 'react-native';
import { api, Category, Vehicle } from '../api';
import { vnd } from '../format';
import { Nav } from '../nav';
import { C, R } from '../theme';
import { Btn, ErrorBox, Header, Loading, VehicleThumb } from '../ui';

const FILTERS: { key: Category | undefined; label: string }[] = [
  { key: undefined, label: 'Tất cả' },
  { key: 'Car4Seats', label: 'Ô tô 4 chỗ' },
  { key: 'Car7Seats', label: 'Ô tô 7 chỗ' },
  { key: 'ElectricBike', label: 'Xe máy điện' },
];

export default function VehiclesScreen({ nav }: { nav: Nav }) {
  const [filter, setFilter] = useState<Category | undefined>(undefined);
  const [items, setItems] = useState<Vehicle[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async (cat: Category | undefined, soft = false) => {
    if (!soft) setItems(null);
    setError(null);
    try {
      setItems(await api.vehicles(cat));
    } catch (e) {
      setError((e as Error).message);
      setItems([]);
    }
  }, []);

  useEffect(() => { load(filter); }, [filter, load]);

  const onRefresh = async () => {
    setRefreshing(true);
    await load(filter, true);
    setRefreshing(false);
  };

  return (
    <View style={{ flex: 1 }}>
      <Header title="MoveGo" subtitle="Thuê ô tô & xe máy điện tại Đà Lạt" />
      <View style={s.chips}>
        {FILTERS.map((f) => {
          const on = f.key === filter;
          return (
            <Pressable key={f.label} onPress={() => setFilter(f.key)} style={[s.chip, on && s.chipOn]}>
              <Text style={[s.chipText, on && { color: C.white }]}>{f.label}</Text>
            </Pressable>
          );
        })}
      </View>

      {items === null ? (
        <Loading text="Đang tải danh sách xe…" />
      ) : (
        <FlatList
          data={items}
          keyExtractor={(v) => String(v.id)}
          contentContainerStyle={{ padding: 16, paddingBottom: 32, gap: 14 }}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={C.green} />}
          ListHeaderComponent={error ? (
            <View>
              <ErrorBox text={error} />
              <View style={{ flexDirection: 'row', gap: 10, marginTop: 12 }}>
                <Btn title="Thử lại" onPress={() => load(filter)} style={{ flex: 1 }} />
                <Btn title="Cài đặt API" kind="outline" onPress={() => nav.goTab('settings')} style={{ flex: 1 }} />
              </View>
            </View>
          ) : null}
          ListEmptyComponent={!error ? <Text style={s.empty}>Chưa có xe nào trong mục này.</Text> : null}
          renderItem={({ item }) => <VehicleCard v={item} onPress={() => nav.push({ name: 'vehicle', id: item.id })} />}
        />
      )}
    </View>
  );
}

function VehicleCard({ v, onPress }: { v: Vehicle; onPress: () => void }) {
  return (
    <Pressable onPress={onPress} style={({ pressed }) => [s.card, pressed && { opacity: 0.85 }]}>
      <VehicleThumb category={v.category} imageUrl={v.imageUrl} height={150} radius={R.card - 4} />
      <View style={{ paddingTop: 12 }}>
        <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-start', gap: 8 }}>
          <Text style={s.name} numberOfLines={2}>{v.name}</Text>
          <View style={{ alignItems: 'flex-end' }}>
            <Text style={s.price}>{vnd(v.pricePerDay)}</Text>
            <Text style={s.perDay}>/ ngày</Text>
          </View>
        </View>
        <Text style={s.meta}>
          {v.categoryLabel} · {v.seats} chỗ{v.rangeKm ? ` · ${v.rangeKm} km/lần sạc` : ''}
        </Text>
        <View style={s.tags}>
          {v.highlights.slice(0, 3).map((h) => (
            <View key={h} style={s.tag}><Text style={s.tagText}>{h}</Text></View>
          ))}
        </View>
      </View>
    </Pressable>
  );
}

const s = StyleSheet.create({
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: 8, paddingHorizontal: 16, paddingTop: 14, paddingBottom: 2 },
  chip: { paddingHorizontal: 14, paddingVertical: 8, borderRadius: R.pill, backgroundColor: C.white, borderWidth: 1.5, borderColor: C.line },
  chipOn: { backgroundColor: C.green, borderColor: C.green },
  chipText: { color: C.inkSoft, fontWeight: '700', fontSize: 14 },
  empty: { textAlign: 'center', color: C.muted, marginTop: 40 },
  card: {
    backgroundColor: C.card, borderRadius: R.card, padding: 12, borderWidth: 1, borderColor: C.line,
    shadowColor: '#0e3326', shadowOpacity: 0.07, shadowRadius: 14, shadowOffset: { width: 0, height: 5 }, elevation: 2,
  },
  name: { flex: 1, fontSize: 18, fontWeight: '800', color: C.ink },
  price: { fontSize: 17, fontWeight: '800', color: C.green },
  perDay: { fontSize: 12, color: C.muted },
  meta: { color: C.muted, marginTop: 4, fontSize: 13 },
  tags: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginTop: 10 },
  tag: { backgroundColor: C.greenSoft, borderRadius: R.pill, paddingHorizontal: 10, paddingVertical: 4 },
  tagText: { color: C.greenDark, fontSize: 12, fontWeight: '600' },
});
