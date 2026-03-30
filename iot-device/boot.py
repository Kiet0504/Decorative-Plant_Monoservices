import network
import time

import config

# =============================================
#  CAU HINH WIFI - DONG CUNG VA XOA .ENV CU
# =============================================
try:
    import os
    os.remove(".env")
    print("[dev] Da xoa file .env kẹt tren mach be mat!")
except:
    pass

WIFI_SSID     = "DoAnIoT"
WIFI_PASSWORD = "12345678"

def connect_wifi():
    wlan = network.WLAN(network.STA_IF)
    
    if wlan.isconnected():
        print("[WiFi] Da ket noi:", wlan.ifconfig())
        return True

    print("[WiFi] Dang khoi tao radio...")
    wlan.active(False)
    time.sleep(0.5)
    wlan.active(True)

    # --- CHUAN DOAN ---
    print("[WiFi] Dang quet cac mang xung quanh...")
    try:
        nets = wlan.scan()
        found = False
        for n in nets:
            ssid = n[0].decode('utf-8')
            if ssid == WIFI_SSID:
                found = True
                print("  => Tim thay mang: '{}' (RSSI: {} dBm)".format(ssid, n[3]))
        if not found:
            print("  !! KHONG TIM THAY mang '{}' trong tam phu song!".format(WIFI_SSID))
    except Exception as e:
        print("  !! Loi khi quet mang:", e)
    # ------------------

    print("[WiFi] Dang ket noi toi '" + WIFI_SSID + "'...")
    try:
        wlan.connect(WIFI_SSID, WIFI_PASSWORD)
    except OSError as e:
        print("[WiFi] Loi he thong:", e)
        return False

    timeout = 15
    while not wlan.isconnected() and timeout > 0:
        # In ra status code de biet ly do (VD: 202, 203...)
        status = wlan.status()
        print("  ... Dang cho (Status: {})".format(status))
        time.sleep(1)
        timeout -= 1

    if wlan.isconnected():
        ip, mask, gateway, dns = wlan.ifconfig()
        print("[WiFi] Ket noi thanh cong! IP: " + ip)
        
        # Đong bo thoi gian thuc tu Internet (NTP)
        try:
            import ntptime
            ntptime.host = "pool.ntp.org"
            ntptime.settime()  # settime() cai dat gio UTC
            print("[NTP] Dong bo gio Internet thanh cong (UTC)")
        except Exception as e:
            print("[NTP] Loi dong bo gio:", e)
            
        return True
    else:
        print("[WiFi] Ket noi that bai. Kiem tra lai SSID/Password.")
        return False

# Tu dong ket noi khi ESP32 khoi dong
connect_wifi()
