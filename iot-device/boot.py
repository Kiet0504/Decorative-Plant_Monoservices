import network
import time

import config

# =============================================
#  CAU HINH WIFI - dang doc tu file .env
# =============================================
WIFI_SSID     = config.env.get("WIFI_SSID", "")
WIFI_PASSWORD = config.env.get("WIFI_PASSWORD", "")

def connect_wifi():
    wlan = network.WLAN(network.STA_IF)
    wlan.active(True)

    if wlan.isconnected():
        print("[WiFi] Da ket noi:", wlan.ifconfig())
        return True

    print("[WiFi] Dang ket noi toi '" + WIFI_SSID + "'...")
    wlan.connect(WIFI_SSID, WIFI_PASSWORD)

    timeout = 15  # cho toi da 15 giay
    while not wlan.isconnected() and timeout > 0:
        time.sleep(1)
        timeout -= 1
        print("  ...")

    if wlan.isconnected():
        ip, mask, gateway, dns = wlan.ifconfig()
        print("[WiFi] Ket noi thanh cong!")
        print("       IP: " + ip + " | Gateway: " + gateway)
        return True
    else:
        print("[WiFi] Ket noi that bai. Kiem tra lai SSID/Password.")
        return False

# Tu dong ket noi khi ESP32 khoi dong
connect_wifi()
