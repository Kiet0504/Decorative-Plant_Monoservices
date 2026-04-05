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

def sync_time():
    """Dong bo thoi gian thuc tu Internet (NTP)"""
    import ntptime
    ntptime.host = "pool.ntp.org"
    max_retries = 5
    for i in range(max_retries):
        try:
            print("[NTP] Dang thu dong bo gio (Lan {}/{})...".format(i+1, max_retries))
            ntptime.settime()
            print("[NTP] Dong bo gio Internet thanh cong (UTC)")
            return True
        except Exception as e:
            print("[NTP] Loi dong bo:", e)
            time.sleep(2)
    return False

def connect_wifi():
    wlan = network.WLAN(network.STA_IF)
    wlan.active(True)
    
    if wlan.isconnected():
        print("[WiFi] Da ket noi san! IP:", wlan.ifconfig()[0])
        sync_time() # Luon luon thu sync time ke ca khi reconnect
        return True

    print("[WiFi] Dang khoi tao radio...")
    wlan.active(False)
    time.sleep(0.5)
    wlan.active(True)

    print("[WiFi] Dang ket noi toi '" + WIFI_SSID + "'...")
    try:
        wlan.connect(WIFI_SSID, WIFI_PASSWORD)
    except OSError as e:
        print("[WiFi] Loi he thong:", e)
        return False

    timeout = 15
    while not wlan.isconnected() and timeout > 0:
        status = wlan.status()
        print("  ... Dang cho (Status: {})".format(status))
        time.sleep(1)
        timeout -= 1

    if wlan.isconnected():
        print("[WiFi] Ket noi thanh cong! IP: " + wlan.ifconfig()[0])
        sync_time()
        return True
    else:
        print("[WiFi] Ket noi that bai. Kiem tra lai SSID/Password.")
        return False

# Tu dong ket noi khi ESP32 khoi dong
connect_wifi()
