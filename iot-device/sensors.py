import machine
import dht
import time

# DHT22 on GPIO 15
dht_pin = machine.Pin(15)
d = dht.DHT22(dht_pin)

# Soil Moisture on GPIO 34 (ADC1)
# 4095 = Dry, 0 = Wet (Approx)
soil_adc = machine.ADC(machine.Pin(34))
soil_adc.atten(machine.ADC.ATTN_11DB) # 0 - 3.6V range

# BH1750 (Light) on I2C (SDA=21, SCL=22)
i2c = machine.SoftI2C(scl=machine.Pin(22), sda=machine.Pin(21))
BH1750_ADDR = 0x23

def read_all():
    """Doc tat ca cam bien va tra ve dictionary.
    Tra ve None cho cam bien bi loi (khac voi gia tri 0 binh thuong).
    """
    data = {}

    # 1. Read DHT22
    try:
        d.measure()
        data["temp_sensor"] = d.temperature()
        data["humidity_sensor"] = d.humidity()
    except Exception as e:
        print("[Sensors] Loi DHT22: ", e)
        data["temp_sensor"] = None      # None = loi phần cứng, khac voi gia tri 0
        data["humidity_sensor"] = None

    # 2. Read Soil Moisture
    try:
        raw_val = soil_adc.read()
        moisture = (4095 - raw_val) * 100 / (4095 - 1500)
        if moisture < 0: moisture = 0
        if moisture > 100: moisture = 100
        data["soil_moisture"] = round(moisture, 1)
    except Exception as e:
        print("[Sensors] Loi Soil: ", e)
        data["soil_moisture"] = None

    # 3. Read Light (BH1750)
    try:
        i2c.writeto(BH1750_ADDR, b'\x10')
        time.sleep_ms(180)
        raw_light = i2c.readfrom(BH1750_ADDR, 2)
        lux = (raw_light[0] << 8 | raw_light[1]) / 1.2
        data["light_sensor"] = round(lux, 1)
    except Exception as e:
        print("[Sensors] Loi BH1750: ", e)
        data["light_sensor"] = None

    return data
