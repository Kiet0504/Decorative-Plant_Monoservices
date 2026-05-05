import time
import machine
import network
import ujson
import config
import automation
import urequests
import sensors
import gc
from umqtt.simple import MQTTClient

# Lay thong tin tu config
SECRET = config.env.get("DEVICE_SECRET", "d816a3c6")
WIFI_SSID = config.env.get("WIFI_SSID", "")
WIFI_PASS = config.env.get("WIFI_PASSWORD", "")

# MQTT Config
MQTT_BROKER = config.env.get("MQTT_BROKER", "broker.hivemq.com")
MQTT_PORT = int(config.env.get("MQTT_PORT", 1883))
MQTT_USER = config.env.get("MQTT_USERNAME", "")
MQTT_PASS = config.env.get("MQTT_PASSWORD", "")
# Topics
TOPIC_RULES = "decorativeplant/device/{}/rules".format(SECRET)
TOPIC_COMMANDS = "decorativeplant/device/{}/commands".format(SECRET)

# API Endpoint
API_URL_INGEST = config.env.get("API_URL", "")

active_rules = []
mqtt_client = None

# --- Sensor Failure Tracking ---
# Track consecutive failure count per sensor key
# If a sensor returns None for SENSOR_FAIL_THRESHOLD times in a row, send an alert
SENSOR_FAIL_THRESHOLD = 2
sensor_fail_counts = {}   # { "temp_sensor": 0, "humidity_sensor": 0, ... }
sensor_alerted = {}       # { "temp_sensor": False, ... } - avoid alert spam

def connect_wifi():
    wlan = network.WLAN(network.STA_IF)
    wlan.active(True)
    if not wlan.isconnected():
        print("[WiFi] Dang ket noi toi {}...".format(WIFI_SSID))
        wlan.connect(WIFI_SSID, WIFI_PASS)
        for _ in range(15):
            if wlan.isconnected(): break
            time.sleep(1)
    if wlan.isconnected():
        print("[WiFi] Da ket noi! IP:", wlan.ifconfig()[0])
        return True
    return False

def mqtt_callback(topic, msg):
    global active_rules
    print("\n[MQTT] ======== CO LENH MOI ========")
    try:
        raw_msg = msg.decode('utf-8')
        data = ujson.loads(raw_msg)
        t = topic.decode()
        if t == TOPIC_RULES:
            active_rules = data
            print("[MQTT] Da cap nhat {} rules.".format(len(active_rules)))
            automation.HardwareActions.all_off()
        elif t == TOPIC_COMMANDS:
            cmd = data.get("command") or data.get("action_type")
            val = data.get("data") or data.get("value")
            automation.HardwareActions.execute(cmd, val)
    except Exception as e:
        print("[MQTT] Loi callback: ", e)

def send_sensor_data(sensor_data):
    """Gui du lieu cam bien len server. Bo qua cam bien bi loi (None)."""
    gc.collect()
    headers = {
        "Content-Type": "application/json",
        "x-device-secret": config.env.get("DEVICE_SECRET", "")
    }

    print("\n[Sensor] Dang gui du lieu len Server...")
    try:
        sent = 0
        for key, value in sensor_data.items():
            if value is None:
                continue  # Bo qua cam bien bi loi, khong gui gia tri sai
            payload = {
                "ComponentKey": key,
                "Value": str(value),
                "Timestamp": time.time()
            }
            response = urequests.post(API_URL_INGEST, json=payload, headers=headers)
            response.close()
            gc.collect()
            sent += 1
        print("  OK! Da gui {} thong so.".format(sent))
    except Exception as e:
        print("[Sensor] Loi khi gui du lieu: ", e)

def send_sensor_alert(sensor_key):
    """Gui canh bao len server khi cam bien bi loi 2 lan lien tiep."""
    gc.collect()
    headers = {
        "Content-Type": "application/json",
        "x-device-secret": config.env.get("DEVICE_SECRET", "")
    }
    payload = {
        "ComponentKey": sensor_key,
        "Value": "ERROR",
        "Timestamp": time.time()
    }
    try:
        print("[Alert] Cam bien {} loi {} lan - Gui canh bao!".format(sensor_key, SENSOR_FAIL_THRESHOLD))
        response = urequests.post(API_URL_INGEST, json=payload, headers=headers)
        response.close()
        gc.collect()
    except Exception as e:
        print("[Alert] Loi khi gui canh bao cam bien: ", e)

def check_sensor_failures(sensor_data):
    """
    Kiem tra cac cam bien bi loi lien tiep.
    Neu mot cam bien tra ve None 2 lan lien tiep -> gui canh bao.
    Neu cam bien phuc hoi -> reset bo dem va trang thai canh bao.
    """
    global sensor_fail_counts, sensor_alerted

    for key, value in sensor_data.items():
        if value is None:
            # Tang bo dem loi
            sensor_fail_counts[key] = sensor_fail_counts.get(key, 0) + 1
            print("[Sensor] {} loi lan thu {}.".format(key, sensor_fail_counts[key]))

            # Neu dat nguong va chua gui canh bao -> gui alert
            if sensor_fail_counts[key] >= SENSOR_FAIL_THRESHOLD and not sensor_alerted.get(key, False):
                send_sensor_alert(key)
                sensor_alerted[key] = True  # Danh dau da gui, tranh spam
        else:
            # Cam bien phuc hoi -> reset
            if sensor_fail_counts.get(key, 0) > 0:
                print("[Sensor] {} da phuc hoi sau {} lan loi.".format(key, sensor_fail_counts[key]))
            sensor_fail_counts[key] = 0
            sensor_alerted[key] = False  # Cho phep gui lai canh bao neu loi tiep

def connect_mqtt():
    global mqtt_client
    client_id = "esp32_" + SECRET[:8]
    use_ssl = (MQTT_PORT == 8883)
    try:
        mqtt_client = MQTTClient(client_id, MQTT_BROKER, port=MQTT_PORT, user=MQTT_USER, password=MQTT_PASS, keepalive=60, ssl=use_ssl, ssl_params={'server_hostname': MQTT_BROKER} if use_ssl else {})
        mqtt_client.set_callback(mqtt_callback)
        mqtt_client.connect()
        mqtt_client.subscribe(TOPIC_RULES)
        mqtt_client.subscribe(TOPIC_COMMANDS)
        print("[MQTT] Ket noi thanh cong!")
        return True
    except Exception as e:
        print("[MQTT] Ket noi that bai: ", e)
        return False

def main():
    if not connect_wifi():
        time.sleep(5); machine.reset()

    if not connect_mqtt():
        time.sleep(5); machine.reset()

    last_trigger = 0
    last_ingest = 0
    last_ping = time.time()

    while True:
        try:
            # 1. Kiem tra tin nhan MQTT
            mqtt_client.check_msg()

            # 2. Gui ping de giữ ket noi (Keep-alive) moi 20s
            if time.time() - last_ping > 20:
                mqtt_client.ping()
                last_ping = time.time()

            # 3. Xu ly timer thiet bi
            automation.HardwareActions.process_timed_actions()

            # 4. Doc va Gui du lieu cam bien moi 15 giay
            if time.time() - last_ingest > 15:
                # Doc du lieu CAM BIEN THAT
                sensor_data = sensors.read_all()

                # Kiem tra va canh bao neu cam bien loi lien tiep
                check_sensor_failures(sensor_data)

                # Chi gui cac cam bien co gia tri hop le (khong gui None)
                send_sensor_data(sensor_data)

                # Sau khi co data moi thi check Rule luon
                automation.evaluate_and_run(sensor_data, active_rules, mqtt_client)

                last_ingest = time.time()
                last_trigger = time.time()

            time.sleep(0.5)

        except Exception as e:
            print("[Main] Mat ket noi ({}). Dang thu lai...".format(e))
            time.sleep(5)
            # Thu ket noi lai neu loi
            if connect_wifi():
                connect_mqtt()

if __name__ == "__main__":
    main()