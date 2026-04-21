import urequests
import ujson
from machine import Pin
import config
import time

API_URL_RULES = config.env.get("API_URL", "").replace("/sensors/ingest", "/sensors/rules")
API_URL_LOGS = config.env.get("API_URL", "").replace("/sensors/ingest", "/sensors/logs")
DEVICE_SECRET = config.env.get("DEVICE_SECRET", "")

# MUC TIN HIEU CHUAN
LEVEL_ON  = 1
LEVEL_OFF = 0

# --- 2. KHOI TAO PHAN CUNG ---
RELAY_PUMP   = Pin(18, Pin.OUT, value=0)
BUZZER_WATER = Pin(19, Pin.OUT, value=0)
FAN          = Pin(23, Pin.OUT, value=0)
BUZZER_FAN   = Pin(27, Pin.OUT, value=0) # Chuyển sang chân 27 tránh nhiễu

# Dam bao mac dinh la TAT
RELAY_PUMP.value(0)
BUZZER_WATER.value(0)
FAN.value(0)
BUZZER_FAN.value(0)

# Flag de chong chay chong (Concurrency Lock)
_is_watering = False
_timed_actions = {} # Luu tru target_stop_time cho tung thiet bi

class HardwareActions:
    @staticmethod
    def all_off():
        """Cuong buc TAT tat ca thiet bi"""
        global _is_watering, _timed_actions
        RELAY_PUMP.value(LEVEL_OFF)
        BUZZER_WATER.value(LEVEL_OFF)
        FAN.value(LEVEL_OFF)
        BUZZER_FAN.value(LEVEL_OFF)
        _is_watering = False
        _timed_actions = {}
        print("[Action] All outputs forced OFF.")

    @staticmethod
    def process_timed_actions():
        """Kiem tra cac thiet bi dang chay theo thoi gian de tu dong ngat (Non-blocking)"""
        global _is_watering, _timed_actions
        now = time.time()
        to_delete = []

        for comp, stop_time in _timed_actions.items():
            if now >= stop_time:
                print("[Action] {} duration reached. Stopping...".format(comp))
                if comp in ["water_pump", "turn_on_pump", "water_now"]:
                    RELAY_PUMP.value(LEVEL_OFF)
                    _is_watering = False
                elif comp in ["fan", "motor", "cooling_fan"]:
                    FAN.value(LEVEL_OFF)
                elif comp in ["buzzer", "alarm", "beep"]:
                    BUZZER_WATER.value(LEVEL_OFF)
                
                to_delete.append(comp)
        
        for comp in to_delete:
            del _timed_actions[comp]

    @staticmethod
    def execute(action_name, action_value, params=None, mqtt_client=None):
        global _is_watering
        """
        Supports:
        1. Flat call: execute("turn_on_pump", 1)
        2. Nested DTO: execute("water_pump", "turn_on", {"duration": 5})
        """
        if action_name in ["water_pump", "turn_on_pump", "water_now"]:
            if _is_watering:
                print("[Action] Device is currently watering. Command skipped.")
                return False, "Already watering"

        success = False
        msg = ""
        try:
            # Normalize action_value
            if isinstance(action_value, str):
                try:
                    action_value = float(action_value)
                except:
                    pass

            is_on = (action_value == "turn_on" or action_value == "ON" or action_value == "1" or action_value == 1 or action_name == "water_now")
            is_off = (action_value == "turn_off" or action_value == "OFF" or action_value == "0" or action_value == 0)

            if isinstance(action_value, (int, float)) and action_value > 0:
                is_on = True

            duration = 0
            if params and "duration" in params:
                try:
                    duration = float(params["duration"])
                except:
                    pass
            
            # Neu nhan qua "value" ma lon hon 1 thi mac dinh do la duration (giay)
            if isinstance(action_value, (int, float)) and action_value > 1:
                duration = action_value
            
            if duration > 0:
                is_on = True

            if is_on and action_name in ["turn_on_pump", "water_pump", "water_now"]:
                if duration <= 0:
                    duration = 5 
                
                # --- Tieng tit canh bao truoc khi tuoi (1.5s) ---
                # Luu y: Do canh bao rat ngan nen tam thoi giu sleep (1.5s khong dang ke)
                BUZZER_WATER.value(LEVEL_ON)
                time.sleep(1.5)
                BUZZER_WATER.value(LEVEL_OFF)
                time.sleep(0.5)

                print("[Action] Pump STARTING for {}s...".format(duration))
                _is_watering = True
                RELAY_PUMP.value(LEVEL_ON) 
                
                # Dat lich tat (Non-blocking)
                _timed_actions[action_name] = time.time() + duration
                
                success = True
                msg = "Pump turned ON (Scheduled for {}s)".format(duration)
                
            elif is_on and action_name in ["fan", "motor", "cooling_fan"]:
                # --- Tieng tit canh bao (1.5s) ---
                BUZZER_FAN.value(1)
                time.sleep(1.5)
                BUZZER_FAN.value(0)
                time.sleep(0.5)

                print("[Action] Fan STARTING...")
                FAN.value(LEVEL_ON)
                success = True
                msg = "Fan turned ON"
                
                if duration > 0:
                    _timed_actions[action_name] = time.time() + duration
                    msg += " (Scheduled for {}s)".format(duration)

            elif is_on and action_name in ["buzzer", "alarm", "beep"]:
                print("[Action] Buzzer STARTING")
                BUZZER_WATER.value(LEVEL_ON)
                success = True
                msg = "Buzzer turned ON"
                
                if duration > 0:
                    _timed_actions[action_name] = time.time() + duration
                    msg += " (Scheduled for {}s)".format(duration)

            elif is_off:
                # --- Sửa lỗi "Vạ lây": Chỉ tắt thiết bị được yêu cầu ---
                target_pin = None
                if action_name in ["water_pump", "turn_on_pump", "water_now"]:
                    target_pin = RELAY_PUMP
                    _is_watering = False
                elif action_name in ["fan", "motor", "cooling_fan"]:
                    target_pin = FAN
                elif action_name in ["buzzer", "alarm", "beep"]:
                    target_pin = BUZZER_WATER
                
                if target_pin:
                    target_pin.value(LEVEL_OFF)
                    success = True
                    msg = "Device {} turned OFF".format(action_name)
                else:
                    # Neu la lenh tat chung (khong ro thiet bi) thi moi tat het
                    HardwareActions.all_off()
                    success = True
                    msg = "All devices turned OFF"
            else:
                msg = "Action unknown: {}/{}".format(action_name, action_value)
        except Exception as e:
            msg = "Hardware error: {}".format(str(e))
            
        print("[Action] " + msg)
        return success, msg

def fetch_rules():
    print("[Automation] Dang tai rules tu Server...")
    headers = {
        "Content-Type": "application/json",
        "x-device-secret": DEVICE_SECRET
    }
    try:
        response = urequests.get(API_URL_RULES, headers=headers)
        if response.status_code == 200:
            rules_data = response.json()
            response.close()
            print("[Automation] Da tai {} rules.".format(len(rules_data)))
            return rules_data
        else:
            print("[Automation] Loi tai rules: {}".format(response.status_code))
            response.close()
            return []
    except Exception as e:
        print("[Automation] Khong the lay rules:", str(e))
        return []

def send_execution_log(rule_id, action_taken, success, message):
    print("[Automation] Dang gui log chay rule ve Server...")
    headers = {
        "Content-Type": "application/json",
        "x-device-secret": DEVICE_SECRET
    }
    
    payload = {
        "RuleId": rule_id,
        "ExecutionInfo": {
            "actionTaken": action_taken,
            "success": success,
            "message": message
        }
    }
    
    try:
        req = urequests.post(API_URL_LOGS, json=payload, headers=headers)
        if req.status_code == 200:
            print("[Automation] Da gui log thanh cong!")
        else:
            print("[Automation] Gui log that bai: {}".format(req.status_code))
        req.close()
    except Exception as e:
        print("[Automation] Loi khi gui log:", str(e))

last_run_schedule = {}
last_run_execution = {}
COOLDOWN_FILE = "/last_run.json"

def _load_cooldown_data():
    global last_run_execution
    try:
        import os
        try:
            os.stat(COOLDOWN_FILE)
        except:
            return 

        with open(COOLDOWN_FILE, "r") as f:
            data = ujson.loads(f.read())
            if isinstance(data, dict):
                last_run_execution = data
                print("[Cooldown] Da tai thong tin lan vung cuoi tu flash ({} rules).".format(len(last_run_execution)))
    except Exception as e:
        print("[Cooldown] Loi khi doc file flash:", e)

def _save_cooldown_data(rule_id, timestamp):
    global last_run_execution
    try:
        last_run_execution[rule_id] = timestamp
        with open(COOLDOWN_FILE, "w") as f:
            f.write(ujson.dumps(last_run_execution))
        # Micropython requires flush usually or just close (which with open does)
    except Exception as e:
        print("[Cooldown] Loi khi ghi file flash:", e)

_load_cooldown_data()
DEFAULT_COOLDOWN = 100 

def is_time_synced():
    """Kiem tra xem gio da duoc dong bo (NTP) chua.
    MicroPython mac dinh bat dau tu nam 2000.
    """
    t = time.localtime()
    return t[0] >= 2024

def check_schedule(rule_id, schedule_dict):
    if not schedule_dict:
        return True, False

    if schedule_dict.get("type") == "always":
        return True, False

    # Neu lich trinh can gio dung, ma chua sync NTP thi khong chay
    if not is_time_synced():
        return False, True

    target_time = schedule_dict.get("time")
    if not target_time:
        ts = schedule_dict.get("time_schedule")
        if isinstance(ts, dict):
            target_time = ts.get("start")

    if not target_time:
        return True, False

    t = time.localtime(time.time() + 25200) # GMT+7
    curr_hour, curr_min = t[3], t[4]
    curr_date_str = "{}-{}-{}".format(t[0], t[1], t[2]) 

    parts = target_time.split(":")
    if len(parts) == 2:
        t_hour, t_min = int(parts[0]), int(parts[1])
        if curr_hour == t_hour and curr_min == t_min:
            key = "{}_{}".format(rule_id, curr_date_str)
            if last_run_schedule.get(key) is True:
                return False, True 
            else:
                last_run_schedule[key] = True
                return True, True  
                
    return False, True 

def _check_single_condition(r, sensor_data):
    comp = r.get("component") or r.get("component_key")
    if not comp:
        return False
        
    # 1. Tim cac giá trị cảm biến khớp (chính xác hoặc theo nhóm)
    matching_values = []
    if comp in sensor_data:
        matching_values.append(float(sensor_data[comp]))
    else:
        # Tim theo prefix (ví dụ: humidity_sensor_1, humidity_sensor_2)
        prefix = comp.lower()
        for k, v in sensor_data.items():
            k_low = k.lower()
            if k_low.startswith(prefix + "_") or k_low.startswith(prefix + " #") or k_low == prefix:
                try:
                    matching_values.append(float(v))
                except:
                    pass
    
    if not matching_values:
        return False

    # 2. Tính trung bình cộng (Aggregation)
    c_val = sum(matching_values) / len(matching_values)
    
    op = r.get("operator")
    val = r.get("value")
    if val is None:
        val = r.get("threshold")
    
    if not op and "logic" in r:
        logic_dict = r["logic"]
        if isinstance(logic_dict, dict):
            op = list(logic_dict.keys())[0]
            val = logic_dict[op]

    res = False
    if val is not None:
        try:
            # Handle range-based operators
            if isinstance(val, str) and "-" in val:
                min_max = val.split("-")
                if len(min_max) == 2:
                    t_min = float(min_max[0])
                    t_max = float(min_max[1])
                    
                    if op == "between":
                        res = t_min <= c_val <= t_max
                    elif op == "outside":
                        res = c_val < t_min or c_val > t_max
            else:
                t_val = float(val)
                if op == ">": res = c_val > t_val
                elif op == "<": res = c_val < t_val
                elif op == "==" or op == "=": res = c_val == t_val
                elif op == ">=": res = c_val >= t_val
                elif op == "<=": res = c_val <= t_val
        except:
            pass

    return res

def evaluate_and_run(sensor_data, active_rules, mqtt_client=None):
    if not active_rules:
        return
        
    # 1. Sap xep theo Priority (P1 uu tien cao nhat)
    sorted_rules = sorted(active_rules, key=lambda x: x.get('priority', 50))
    
    print("[Engine] Checking {} rules (sorted by priority)...".format(len(sorted_rules)))
    
    # 2. Danh sach cac actuator da duoc dieu khien trong loop nay
    executed_actuators = set()
    
    for rule in sorted_rules:
        rule_name = rule.get('name', 'Unknown')
        conditions = rule.get("conditions", {}) or {}
        actions = rule.get("actions", {}) or {}
        schedule = rule.get("schedule", {}) or {}
        rule_id = rule.get("id", "0000")
        
        should_run_time, is_scheduled = check_schedule(rule_id, schedule)
        if not should_run_time:
            continue
            
        # Kiem tra logic AND/OR
        logic = "and"
        if isinstance(conditions, dict):
            logic = conditions.get("logic", "and").lower()
            
        rules_list = []
        if isinstance(conditions, list):
            rules_list = conditions
        elif isinstance(conditions, dict) and "rules" in conditions:
            rules_list = conditions["rules"]

        match = (logic == "and")
        if not rules_list:
            match = True
        else:
            if logic == "or":
                match = False
                for r in rules_list:
                    if _check_single_condition(r, sensor_data):
                        match = True
                        break
            else: 
                match = True
                for r in rules_list:
                    if not _check_single_condition(r, sensor_data):
                        match = False
                        break
                
        # Xac dinh tinh hop le
        is_valid = False
        if is_scheduled and match:
            is_valid = True
        elif not is_scheduled and conditions and match:
            is_valid = True
        else:
            is_valid = False
            
        # Trich xuat danh sach hanh dong
        action_list = []
        if isinstance(actions, list):
            action_list = actions
        elif isinstance(actions, dict) and "actions" in actions:
            action_list = actions["actions"]
        elif isinstance(actions, dict):
            action_list = [{"component": k, "value": v} for k, v in actions.items()]

        # 3. Thuc thi neu Hop le
        if is_valid and action_list:
            print("[Automation] Rule [{}] triggered!".format(rule_name))
            for a in action_list:
                comp = a.get("component") or a.get("target_component_key")
                cmd = a.get("command") or a.get("action_type")
                params = a.get("parameters") or {}
                val = a.get("value") or cmd
                
                # KIEM TRA KHOA THIET BI (LOCKING)
                if comp in executed_actuators:
                    print("  -> Skip action [{}] cua Rule [{}] vi da bi khoa boi Rule uu tien cao hon".format(comp, rule_name))
                    continue
                
                _save_cooldown_data(rule_id, time.time())
                succ, msg = HardwareActions.execute(comp, val, params, mqtt_client)
                executed_actuators.add(comp)
                send_execution_log(rule_id, comp, succ, msg)
            
            time.sleep(0.5)
        
        # 4. Xu ly khi KHONG hop le (Dinh chi thiet bi)
        elif not is_valid and action_list:
            is_pump_action = any("pump" in str(a.get("component", "")).lower() or "water" in str(a.get("component", "")).lower() for a in action_list)
            if not is_scheduled and is_pump_action:
                for a in action_list:
                    comp = a.get("component") or a.get("target_component_key")
                    if comp and comp not in executed_actuators and "pump" in comp.lower():
                        HardwareActions.execute(comp, "turn_off", None, mqtt_client)
                        executed_actuators.add(comp)
