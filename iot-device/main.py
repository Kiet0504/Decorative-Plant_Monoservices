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

# =============================================
#  CAU HINH - dang doc cac gia tri tu file .env
# =============================================

# URL API Backend
API_URL       = config.env.get("API_URL", "")

# Secret Key cua thiet bi
DEVICE_SECRET = config.env.get("DEVICE_SECRET", "")

# Thoi gian giua 2 lan doc cam bien (giay)
SEND_INTERVAL = 30

# Thoi gian giua 2 lan tai automation rules tu Server (giay)
RULE_FETCH_INTERVAL = 120
last_rule_fetch = 0
active_rules = []

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
    """Doc cuong do anh sang tu BH1750, tra ve lux (float) hoac None neu loi."""
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
    """Doc cam bien do am dat.
    ADC tra ve 0-4095:  0 = uot sung, 4095 = kho can
    Ham nay chuyen doi thanh phan tram % (0% = kho, 100% = uot)
    """
    try:
        raw = soil_adc.read()
        percent = round((1 - raw / 4095.0) * 100, 1)
        return percent
    except Exception as e:
        print("[Soil] Loi doc cam bien:", e)
        return None

def send_sensor_data(component_key, value):
    """Gui du lieu cam bien len Backend API."""
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

while True:
    current_time = time.time()
    
    # Kiem tra neu can fetch rule: LAN DAU hoac SAU KHI het thoi gian thiet lap
    if last_rule_fetch == 0 or (current_time - last_rule_fetch) > RULE_FETCH_INTERVAL:
        loaded_rules = automation.fetch_rules()
        if loaded_rules is not None:
            active_rules = loaded_rules
        last_rule_fetch = current_time

    print("\n[" + str(time.ticks_ms() // 1000) + "s] Dang doc cam bien...")
    
    # Dictionary de luu tam du lieu cung cap cho engine Automation
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
        automation.evaluate_and_run(sensor_data, active_rules)

    print("  -> Cho " + str(SEND_INTERVAL) + "s roi doc lai...")
    time.sleep(SEND_INTERVAL)
