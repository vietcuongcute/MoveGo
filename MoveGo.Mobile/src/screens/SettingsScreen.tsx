import React, { useEffect, useState } from 'react';
import { ScrollView, Text, View } from 'react-native';
import { api, getBaseUrl, setBaseUrl } from '../api';
import { C } from '../theme';
import { Btn, Card, ErrorBox, Field, Header } from '../ui';

export default function SettingsScreen() {
  const [url, setUrl] = useState('');
  const [msg, setMsg] = useState<{ ok: boolean; text: string } | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => { getBaseUrl().then(setUrl); }, []);

  const saveAndTest = async () => {
    setBusy(true);
    setMsg(null);
    const saved = await setBaseUrl(url);
    setUrl(saved);
    try {
      await api.ping();
      setMsg({ ok: true, text: `Kết nối thành công tới ${saved}` });
    } catch (e) {
      setMsg({ ok: false, text: (e as Error).message });
    } finally {
      setBusy(false);
    }
  };

  return (
    <View style={{ flex: 1 }}>
      <Header title="Cài đặt" subtitle="Địa chỉ máy chủ MoveGo API" />
      <ScrollView contentContainerStyle={{ padding: 16, paddingBottom: 40 }} keyboardShouldPersistTaps="handled">
        <Card>
          <Text style={{ color: C.ink, fontWeight: '800', fontSize: 16 }}>Địa chỉ API</Text>
          <Text style={{ color: C.muted, marginTop: 6, lineHeight: 20 }}>
            Ví dụ: http://192.168.1.23:5100 (API chạy trên máy Mac cùng Wi-Fi) hoặc địa chỉ website đã up lên host,
            ví dụ http://ten-site.runasp.net
          </Text>
          <Field
            label="URL"
            value={url}
            onChangeText={setUrl}
            autoCapitalize="none"
            autoCorrect={false}
            keyboardType="url"
            placeholder="http://192.168.1.23:5100"
          />
          <Btn title="Lưu và kiểm tra kết nối" onPress={saveAndTest} loading={busy} style={{ marginTop: 16 }} />
          {msg ? (
            msg.ok ? (
              <Text style={{ color: C.green, fontWeight: '800', marginTop: 12 }}>✅  {msg.text}</Text>
            ) : (
              <ErrorBox text={msg.text} />
            )
          ) : null}
        </Card>

        <Card style={{ marginTop: 16 }}>
          <Text style={{ color: C.ink, fontWeight: '800', fontSize: 16 }}>Về app demo</Text>
          <Text style={{ color: C.muted, marginTop: 6, lineHeight: 20 }}>
            MoveGo là nền tảng trung gian: khách đặt xe, chủ xe xác nhận, khách cọc 10% (hoa hồng MoveGo), 90% còn lại trả
            trực tiếp cho chủ xe. Thanh toán và tin nhắn Zalo đang ở chế độ giả lập.
          </Text>
        </Card>
      </ScrollView>
    </View>
  );
}
