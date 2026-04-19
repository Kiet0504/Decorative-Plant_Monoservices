"""
main.py - ESP32 IoT Sensor Code (MicroPython)
Phan cung: DHT22 (D15) + Cam bien do am dat Analog (D34) + BH1750 anh sang (I2C D21/D22)
Backend: Decorative Plant API - POST /api/iot/sensors/ingest
"""
import machine
import time
import urequests
import ujson
import dht

import config
import automation
from umqtt.simple import MQTTClient

# =============================================
#  CAU HINH - dang doc cac gia tri tu file .env
# =============================================

# URL# API Endpoint de gui du lieu
API_URL = "http://192.168.137.1:8080/api/iot/sensors/ingest"
DEVICE_SECRET = config.env.get("DEVICE_SECRET", "d816a3c6-9cc3-4a00-b6fa-4ed3a96860db")

# Thoi gian giua 2 lan doc cam bien (giay)
SEND_INTERVAL = 30

active_rules = []

# =============================================
#  CAU HINH MQTT (Real-time Automation)
# =============================================
MQTT_BROKER = config.env.get("MQTT_BROKER") or "broker.hivemq.com"
MQTT_PORT = int(config.env.get("MQTT_PORT") or 1883)
MQTT_USERNAME = config.env.get("MQTT_USERNAME") or ""
MQTT_PASSWORD = config.env.get("MQTT_PASSWORD") or ""

MQTT_CLIENT_ID = "esp32_" + DEVICE_SECRET[:8]
MQTT_TOPIC_RULES = "decorativeplant/device/{}/rules".format(DEVICE_SECRET).encode('utf-8')
MQTT_TOPIC_COMMANDS = "decorativeplant/device/{}/commands".format(DEVICE_SECRET).encode('utf-8')

mqtt_client = None

def mqtt_callback(topic, msg):
    global active_rules
    print("\n[MQTT] ======== CO LENH MOI TU SERVER ========")
    try:
        data = ujson.loads(msg.decode('utf-8'))
        
        # 1. Update Rules
        if topic == MQTT_TOPIC_RULES:
            active_rules = data
            print("[MQTT] Da cap nhat {} rules thanh cong!".format(len(active_rules)))
            
        # 2. Direct Command Execution
        elif topic == MQTT_TOPIC_COMMANDS:
            # Backend bọc lệnh vào 'command' và 'data'
            action = data.get("command") or data.get("action")
            payload = data.get("data")
            
            # Nếu payload là object (dict), bóc tách tiếp
            if isinstance(payload, dict):
                value = payload.get("value")
                params = payload.get("params") or {}
            else:
                value = data.get("value") or action
                params = data.get("params") or {}
            
            is_on = 1 if (str(value).lower() in ["turn_on", "on", "1", "water_now"] or value == 1) else 0
            
            # --- Report status BEFORE execution (so UI shows activity immediately) ---
            if action == "fan":
                send_sensor_data("fan_status", is_on)
            elif action in ["water_pump", "water_now"]:
                send_sensor_data("pump_status", is_on)
            
            print("[MQTT] Dang thuc thi lenh truc tiep: {}={}".format(action, value))
            automation.HardwareActions.execute(action, value, params)
            
            # --- Report status AFTER execution (especially for timed actions like pump) ---
            if action in ["water_pump", "water_now"] and is_on:
                # After the duration-based execution finishes, report the pump as OFF
                send_sensor_data("pump_status", 0)
            
    except Exception as e:
        print("[MQTT] Loi xu ly tin nhan:", e)

def connect_mqtt():
    global mqtt_client
    try:
        use_ssl = (MQTT_PORT == 8883)
        client = MQTTClient(
            client_id=MQTT_CLIENT_ID, 
            server=MQTT_BROKER, 
            port=MQTT_PORT,
            user=MQTT_USERNAME if len(MQTT_USERNAME) > 0 else None,
            password=MQTT_PASSWORD if len(MQTT_PASSWORD) > 0 else None,
            keepalive=60,
            ssl=use_ssl,
            ssl_params={"server_hostname": MQTT_BROKER} if use_ssl else {}
        )
        client.set_callback(mqtt_callback)
        client.connect()
        client.subscribe(MQTT_TOPIC_RULES)
        client.subscribe(MQTT_TOPIC_COMMANDS)
        print("[MQTT] Ket noi thanh cong! Subscribed to Rules & Commands.")
        mqtt_client = client
    except Exception as e:
        print("[MQTT] Loi ket noi:", e)
        mqtt_client = None

# =============================================
#  CHAN KET NOI (theo HARDWARE_CONNECTIONS.md)
# =============================================
DHT_PIN   = 15   # DHT22 - Data
SOIL_PIN  = 34   # Cam bien do am dat - Analog A0
I2C_SDA   = 21   # BH1750 - SDA
I2C_SCL   = 22   # BH1750 - SCL

# =============================================
#  KHOI TAO PHAN CUNG
# =============================================
dht_sensor  = dht.DHT22(machine.Pin(DHT_PIN))
soil_adc    = machine.ADC(machine.Pin(SOIL_PIN))
soil_adc.atten(machine.ADC.ATTN_11DB)  # Dat dai do 0-3.6V

# Khoi tao I2C cho BH1750
i2c = machine.SoftI2C(scl=machine.Pin(I2C_SCL), sda=machine.Pin(I2C_SDA))
BH1750_ADDR = 0x23  # Dia chi khi chan ADDR noi GND hoac de ho

def read_bh1750():
    try:
        i2c.writeto(BH1750_ADDR, bytes([0x10]))
        time.sleep_ms(180)
        data = i2c.readfrom(BH1750_ADDR, 2)
        lux = (data[0] << 8 | data[1]) / 1.2
        return round(lux, 1)
    except Exception as e:
        print("[BH1750] Loi doc cam bien:", e)
        return None

def read_soil_moisture():
    try:
        raw = soil_adc.read()
        percent = round((1 - raw / 4095.0) * 100, 1)
        return percent
    except Exception as e:
        print("[Soil] Loi doc cam bien:", e)
        return None

def send_sensor_data(component_key, value):
    payload = {"componentKey": component_key, "value": value}
    headers = {
        "Content-Type": "application/json",
        "x-device-secret": DEVICE_SECRET
    }
    try:
        response = urequests.post(
            API_URL,
            data=ujson.dumps(payload),
            headers=headers
        )
        if response.status_code == 200:
            print("  OK [" + component_key + "] Gui thanh cong: " + str(value))
        else:
            print("  ERR [" + component_key + "] Loi " + str(response.status_code) + ": " + response.text)
        response.close()
    except Exception as e:
        print("  ERR [" + component_key + "] Khong ket noi duoc server:", e)

# =============================================
#  VONG LAP CHINH
# =============================================
print("========================================")
print("  Decorative Plant IoT Sensor")
print("========================================")

connect_mqtt()

# Khi vua khoi dong, load rules lan dau (fallback)
fallback_rules = automation.fetch_rules()
if fallback_rules:
    active_rules = fallback_rules

while True:
    current_time = time.time()
    
    print("\n[" + str(time.ticks_ms() // 1000) + "s] Dang doc cam bien...")
    
    sensor_data = {}

    # 1. Doc DHT22 (Nhiet do & Do am Khong khi)
    try:
        dht_sensor.measure()
        temp     = dht_sensor.temperature()   # C
        humidity = dht_sensor.humidity()      # %
        print("  DHT22  -> Nhiet do: " + str(temp) + "C | Do am KK: " + str(humidity) + "%")
        send_sensor_data("temp_sensor", temp)
        time.sleep_ms(500)
        send_sensor_data("humidity_sensor", humidity)
        
        sensor_data["temp_sensor"] = temp
        sensor_data["humidity_sensor"] = humidity
    except Exception as e:
        print("  [DHT22] Loi doc:", e)

    time.sleep_ms(500)

    # 2. Doc Do am Dat
    soil = read_soil_moisture()
    if soil is not None:
        print("  Soil   -> Do am dat: " + str(soil) + "%")
        send_sensor_data("soil_moisture", soil)
        sensor_data["soil_moisture"] = soil

    time.sleep_ms(500)

    # 3. Doc BH1750 (Anh sang)
    lux = read_bh1750()
    if lux is not None:
        print("  BH1750 -> Anh sang: " + str(lux) + " lux")
        send_sensor_data("light_sensor", lux)
        sensor_data["light_sensor"] = lux

    # 4. Kiem tra cac tin hieu tu Automation
    if active_rules and sensor_data:
        automation.evaluate_and_run(sensor_data, active_rules, mqtt_client)

    print("  -> Dang cho vuot {}s (MQTT san sang nhan lenh realtime)...".format(SEND_INTERVAL))
    
    for _ in range(SEND_INTERVAL * 5):
        # --- Kiem tra va tu dong ngat thiet bi (Non-blocking) ---
        automation.HardwareActions.process_timed_actions()

        if mqtt_client:
            try:
                if _ % 50 == 0:
                    print("[MQTT] Dang cho lenh ({}s)...".format(time.ticks_ms() // 1000))
                mqtt_client.check_msg()
            except OSError as e:
                print("[MQTT] Mat ket noi ({}). Dang thu lai...".format(e))
                connect_mqtt()
            except Exception as e:
                pass
        time.sleep(0.2)
