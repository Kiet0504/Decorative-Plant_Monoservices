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
        # print("[MQTT] Raw Payload: " + raw_msg) # Giam bot log de tranh lag
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
    """Gui du lieu cam bien len server"""
    gc.collect() # Giai phong RAM truoc khi gui thong tin cam bien
    headers = {
        "Content-Type": "application/json",
        "x-device-secret": config.env.get("DEVICE_SECRET", "")
    }
    
    print("\n[Sensor] Dang gui du lieu len Server...")
    try:
        # Gui tung thong so va don dep RAM ngay sau moi lan gui (tranh hụt RAM cho MQTT SSL)
        for key, value in sensor_data.items():
            payload = {
                "ComponentKey": key,
                "Value": str(value),
                "Timestamp": time.time()
            }
            response = urequests.post(API_URL_INGEST, json=payload, headers=headers)
            response.close()
            gc.collect()
        print("  OK! Da gui {} thong so.".format(len(sensor_data)))
    except Exception as e:
        print("[Sensor] Loi khi gui du lieu: ", e)

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