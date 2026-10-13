import React, { useCallback, useMemo, useState } from 'react';
import { BackHandler, Pressable, StyleSheet, Text, View } from 'react-native';
import { SafeAreaProvider, useSafeAreaInsets } from 'react-native-safe-area-context';
import { StatusBar } from 'expo-status-bar';
import { Nav, Route, Tab } from './src/nav';
import { C } from './src/theme';
import LookupScreen from './src/screens/LookupScreen';
import OrderScreen from './src/screens/OrderScreen';
import SettingsScreen from './src/screens/SettingsScreen';
import VehicleDetailScreen from './src/screens/VehicleDetailScreen';
import VehiclesScreen from './src/screens/VehiclesScreen';

const TABS: { key: Tab; icon: string; label: string }[] = [
  { key: 'vehicles', icon: '🚗', label: 'Xe' },
  { key: 'lookup', icon: '🔎', label: 'Tra cứu' },
  { key: 'settings', icon: '⚙️', label: 'Cài đặt' },
];

function Shell() {
  const insets = useSafeAreaInsets();
  const [tab, setTab] = useState<Tab>('vehicles');
  const [stack, setStack] = useState<Route[]>([]);

  const nav = useMemo<Nav>(() => ({
    push: (r) => setStack((s) => [...s, r]),
    pop: () => setStack((s) => s.slice(0, -1)),
    goTab: (t) => { setStack([]); setTab(t); },
  }), []);

  // Nút Back (Android) — iOS không cần
  const onBack = useCallback(() => {
    if (stack.length > 0) { nav.pop(); return true; }
    return false;
  }, [stack.length, nav]);
  React.useEffect(() => {
    const sub = BackHandler.addEventListener('hardwareBackPress', onBack);
    return () => sub.remove();
  }, [onBack]);

  const top = stack[stack.length - 1];
  // Các tab được giữ nguyên (không huỷ) để quay lại không phải tải lại; chỉ ẩn khi đang xem chi tiết
  const showTabs = !top;

  return (
    <View style={[s.root, { paddingTop: insets.top }]}>
      <StatusBar style="light" />
      <View style={{ flex: 1, display: showTabs ? 'flex' : 'none' }}>
        <View style={{ flex: 1, display: tab === 'vehicles' ? 'flex' : 'none' }}><VehiclesScreen nav={nav} /></View>
        <View style={{ flex: 1, display: tab === 'lookup' ? 'flex' : 'none' }}><LookupScreen nav={nav} /></View>
        <View style={{ flex: 1, display: tab === 'settings' ? 'flex' : 'none' }}><SettingsScreen /></View>
      </View>

      {top ? (
        <View style={{ flex: 1 }} key={stack.length + JSON.stringify(top)}>
          {top.name === 'vehicle' ? <VehicleDetailScreen id={top.id} nav={nav} /> : <OrderScreen code={top.code} nav={nav} />}
        </View>
      ) : null}

      {showTabs ? (
        <View style={[s.tabBar, { paddingBottom: Math.max(insets.bottom, 8) }]}>
          {TABS.map((t) => (
            <Pressable key={t.key} style={s.tab} onPress={() => setTab(t.key)}>
              <Text style={{ fontSize: 22, opacity: tab === t.key ? 1 : 0.45 }}>{t.icon}</Text>
              <Text style={[s.tabLabel, tab === t.key && { color: C.green }]}>{t.label}</Text>
            </Pressable>
          ))}
        </View>
      ) : null}
    </View>
  );
}

export default function App() {
  return (
    <SafeAreaProvider>
      <Shell />
    </SafeAreaProvider>
  );
}

const s = StyleSheet.create({
  root: { flex: 1, backgroundColor: C.greenDark },
  tabBar: { flexDirection: 'row', backgroundColor: C.white, borderTopWidth: 1, borderTopColor: C.line, paddingTop: 8 },
  tab: { flex: 1, alignItems: 'center', gap: 2 },
  tabLabel: { fontSize: 11, fontWeight: '700', color: C.muted },
});
