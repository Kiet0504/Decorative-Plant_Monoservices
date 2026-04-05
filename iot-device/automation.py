import urequests
import ujson
from machine import Pin
import config
import time

API_URL_RULES = config.env.get("API_URL", "").replace("/sensors/ingest", "/sensors/rules")
API_URL_LOGS = config.env.get("API_URL", "").replace("/sensors/ingest", "/sensors/logs")
DEVICE_SECRET = config.env.get("DEVICE_SECRET", "")

# Cau hinh chan GPIO
RELAY_PUMP = Pin(18, Pin.OUT)

# CAU HINH MUC TIN HIEU (High Level Trigger: 1 = ON, 0 = OFF)
# Thay doi neu dung Relay Low Level Trigger: ON=0, OFF=1
LEVEL_ON  = 1
LEVEL_OFF = 0

# Dam bao mac dinh la TAT luc moi khoi dong
RELAY_PUMP.value(LEVEL_OFF)

# Flag de chong chay chong (Concurrency Lock)
_is_watering = False

class HardwareActions:
    @staticmethod
    def all_off():
        """Cuong buc TAT tat ca thiet bi dau ra (Relay)"""
        RELAY_PUMP.value(LEVEL_OFF)
        print("[Action] All outputs forced OFF.")

    @staticmethod
    def execute(action_name, action_value, params=None):
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
            # 1. Determine the effective duration and ON/OFF status
            # Try to force action_value to numeric if it's a string representation of a number
            if isinstance(action_value, str):
                try:
                    action_value = float(action_value)
                except:
                    pass

            is_on = (action_value == "turn_on" or action_value == "ON" or action_value == "1" or action_value == 1 or action_name == "water_now")
            is_off = (action_value == "turn_off" or action_value == "OFF" or action_value == "0" or action_value == 0)

            # If action_value is a number > 0, it implies ON for pump/actuator components
            if isinstance(action_value, (int, float)) and action_value > 0:
                is_on = True

            # Extract duration from params or action_value
            duration = 0
            if params and "duration" in params:
                try:
                    duration = float(params["duration"])
                except:
                    pass
            
            # If the action_name is a watering action and action_value is a number, treat it as duration
            if action_name in ["water_pump", "turn_on_pump", "water_now"]:
                if isinstance(action_value, (int, float)) and action_value > 1:
                    # Logic: if > 20, assume it might be '50' for 5s ( legacy support), else use as is
                    duration = action_value / 10 if action_value > 20 else action_value
            
            # If we calculated a duration, it implies 'is_on'
            if duration > 0:
                is_on = True

            if is_on and action_name in ["turn_on_pump", "water_pump", "water_now"]:
                # Safety Fix: Default 5s duration if not specified for a water command
                if duration <= 0:
                    duration = 5 
                    print("[Safety] No duration specified for water command. Using default 5s.")

                print("[Action] Pump STARTING ({})...".format(LEVEL_ON))
                _is_watering = True
                RELAY_PUMP.value(LEVEL_ON) 
                success = True
                msg = "Pump turned ON"
                
                # If a duration was specified or detected, wait and then turn OFF
                if duration > 0:
                    msg += " for {}s".format(duration)
                    print("[Action] Waiting {}s...".format(duration))
                    time.sleep(duration)
                    RELAY_PUMP.value(LEVEL_OFF) 
                    _is_watering = False
                    msg += " and then AUTO-OFF"
                    print("[Action] Pump AUTO-OFF.")
                
            elif action_name == "turn_off_pump" or (action_name == "water_pump" and is_off):
                RELAY_PUMP.value(LEVEL_OFF) 
                _is_watering = False
                success = True
                msg = "Pump turned OFF"
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
    
    # Payload gui len backend (CreateExecutionLogCommand structure)
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

# Tu dien theo doi de tranh chay lai Rule thi hanh qua gan nhau (Cooldown)
last_run_schedule = {}
last_run_execution = {}
COOLDOWN_FILE = "/last_run.json"

def _load_cooldown_data():
    global last_run_execution
    try:
        import os
        # Check if file exists (MicroPython os.stat or similar)
        try:
            os.stat(COOLDOWN_FILE)
        except:
            return # File not found

        with open(COOLDOWN_FILE, "r") as f:
            data = ujson.loads(f.read())
            if isinstance(data, dict):
                # MicroPython: dictionary update
                for k, v in data.items():
                    last_run_execution[k] = v
                print("[Cooldown] Da tai thong tin lan vung cuoi tu flash.")
    except Exception as e:
        print("[Cooldown] Loi khi doc file flash:", e)

def _save_cooldown_data(rule_id, timestamp):
    global last_run_execution
    try:
        last_run_execution[rule_id] = timestamp
        with open(COOLDOWN_FILE, "w") as f:
            f.write(ujson.dumps(last_run_execution))
    except Exception as e:
        print("[Cooldown] Loi khi ghi file flash:", e)

# Initial load
_load_cooldown_data()

# Thoi gian nghi mac dinh cho moi Rule (giay) - 10 phut (đa ngam nuoc)
DEFAULT_COOLDOWN = 600 

def check_schedule(rule_id, schedule_dict):
    """
    Kiem tra neu luat co chua 'schedule' (vd: {"time": "06:00"}).
    Tra ve (co_can_chay_khong, is_scheduled_rule)
    """
    if not schedule_dict:
        return True, False # Khong co lich hen, coi nhu pass ve mat thoi gian

    # Neu loai hinh la 'always', bo qua cac thong so thoi gian
    if schedule_dict.get("type") == "always":
        return True, False

    # Try both 'time' (manual) and 'time_schedule.start' (official)
    target_time = schedule_dict.get("time")
    if not target_time:
        ts = schedule_dict.get("time_schedule")
        if isinstance(ts, dict):
            target_time = ts.get("start")

    if not target_time:
        return True, False

    # Lay thoi gian thuc cua Viet Nam (UTC + 7)
    # time.time() tra ve giay. + 25200 de ra mui gio VN
    t = time.localtime(time.time() + 25200) 
    curr_hour, curr_min = t[3], t[4]
    curr_date_str = "{}-{}-{}".format(t[0], t[1], t[2]) # Nam-Thang-Ngay

    parts = target_time.split(":")
    if len(parts) == 2:
        t_hour, t_min = int(parts[0]), int(parts[1])
        
        # Kiem tra neu dung gio va phut
        if curr_hour == t_hour and curr_min == t_min:
            # Kiem tra hom nay da chay chua
            key = "{}_{}".format(rule_id, curr_date_str)
            if last_run_schedule.get(key) is True:
                return False, True # Hom nay tai gio nay da chay roi
            else:
                last_run_schedule[key] = True
                return True, True  # Den h chay va chua chay hom nay
                
    return False, True # Co lich nhung khong phai bay gio

def _check_single_condition(r, sensor_data):
    comp = r.get("component") or r.get("component_key")
    current_val = sensor_data.get(comp, None)
    
    # Use 'operator' and either 'value' (manual) or 'threshold' (official)
    op = r.get("operator")
    val = r.get("value")
    if val is None:
        val = r.get("threshold")
    
    # Fallback for old format
    if not op and "logic" in r:
        logic_dict = r["logic"]
        if isinstance(logic_dict, dict):
            op = list(logic_dict.keys())[0]
            val = logic_dict[op]

    res = False
    if current_val is not None and val is not None:
        try:
            c_val = float(current_val)
            t_val = float(val)
            if op == ">": res = c_val > t_val
            elif op == "<": res = c_val < t_val
            elif op == "==" or op == "=": res = c_val == t_val
            elif op == ">=": res = c_val >= t_val
            elif op == "<=": res = c_val <= t_val
        except:
            pass

    print("    [Check] {} (v={}) {} (t={}) -> {}".format(comp, current_val, op, val, res))
    return res

def evaluate_and_run(sensor_data, active_rules):
    # sensor_data: dict, e.g., {"temp_sensor": 30.5}
    print("[Engine] Bat dau kiem tra {} rules...".format(len(active_rules)))
    for rule in active_rules:
        print("[Rule Debug] Content: {}".format(ujson.dumps(rule)))
        rule_name = rule.get('name', 'Unknown')
        conditions = rule.get("conditions", {}) or {}
        actions = rule.get("actions", {}) or {}
        schedule = rule.get("schedule", {}) or {}
        rule_id = rule.get("id", "0000")
        
        # 1. Kiem tra gio giac
        should_run_time, is_scheduled = check_schedule(rule_id, schedule)
        if not should_run_time:
            continue
            
        # 2. Kiem tra Cooldown (Neu khong phai rule hen gio)
        # De tranh viet tuoi di tuoi lai nhieu lan khi nuoc chua kip ngam
        if not is_scheduled:
            # Re-load from flash just in case it was updated by another process or after reboot
            # (Though in single-thread MicroPython it's mostly for startup)
            now = time.time()
            if rule_id in last_run_execution:
                elapsed = now - last_run_execution[rule_id]
                if elapsed < DEFAULT_COOLDOWN:
                    print("  -> Rule [{}] dang trong thoi gian nghi (con {}s)...".format(rule_name, int(DEFAULT_COOLDOWN - elapsed)))
                    # Fix cho loi may bom van chay: 
                    # Khi đang nghỉ, ta vẫn cưỡng bức Tắt bơm cho chắc chắn
                    if "water" in ujson.dumps(actions) or "pump" in ujson.dumps(actions):
                        HardwareActions.all_off()
                    continue
            
        # 2. Kiem tra dieu kien cam bien
        logic = "and"
        if isinstance(conditions, dict):
            logic = conditions.get("logic", "and").lower()
            
        rules_list = []
        if isinstance(conditions, list):
            rules_list = conditions
        elif isinstance(conditions, dict) and "rules" in conditions:
            rules_list = conditions["rules"]

        print("  -> Dang xet Rule: '{}' (Logic={})".format(rule_name, logic))
        
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
            else: # logic == "and"
                match = True
                for r in rules_list:
                    if not _check_single_condition(r, sensor_data):
                        match = False
                        break
                
        print("  => Result: match={}".format(match))

        is_valid = False
        if is_scheduled and match:
            is_valid = True
        elif not is_scheduled and conditions and match:
            is_valid = True
        else:
            is_valid = False
            # Fix cho loi may bom khong chiu dung:
            # Neu day la rule tuoi cay nhung khong thoa man dieu kien, ta cuong buc TẮT bơm cho an toan
            if not is_scheduled and ("water_pump" in ujson.dumps(actions) or "water_now" in ujson.dumps(actions)):
                HardwareActions.all_off() 
            
        # Kiem tra action co dien ra qua nhanh ko de chong spam (tu tuy chinh)
            
        # Kiem tra action co dien ra qua nhanh ko de chong spam (tu tuy chinh)
        if is_valid and actions:
            print("[Automation] Rule [{}] triggered!".format(rule.get('name', 'Unknown')))
            
            # Handle nested action structure: {"actions": [...]}
            action_list = []
            if isinstance(actions, list):
                action_list = actions
            elif isinstance(actions, dict) and "actions" in actions:
                action_list = actions["actions"]
            
            if action_list:
                for a in action_list:
                    comp = a.get("component") or a.get("target_component_key")
                    cmd = a.get("command") or a.get("action_type")
                    params = a.get("parameters") or {}
                    val = a.get("value") or cmd
                    
                    # Cap nhat thoi gian chay cuoi cùng de cooldown (Persist to Flash) 
                    # QUAN TRONG: Phai luu TRUOC khi thuc thi hanh dong delay (5s) de tranh trung lap khi reboot
                    _save_cooldown_data(rule_id, time.time())
                    
                    succ, msg = HardwareActions.execute(comp, val, params)
                    send_execution_log(rule_id, comp, succ, msg)
            else:
                # Legacy flat format: {"turn_on_pump": True}
                for action_key, action_val in actions.items():
                    _save_cooldown_data(rule_id, time.time())
                    succ, msg = HardwareActions.execute(action_key, action_val)
                    send_execution_log(rule_id, action_key, succ, msg)
                
            # Ngi giua cac action de dam bao on dinh phan cung
            time.sleep(1)
