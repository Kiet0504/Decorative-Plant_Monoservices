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

# Dam bao mac dinh la TAT (Low-level trigger: 1 = OFF)
RELAY_PUMP.value(1)

class HardwareActions:
    @staticmethod
    def execute(action_name, action_value, params=None):
        """
        Supports:
        1. Flat call: execute("turn_on_pump", 1)
        2. Nested DTO: execute("water_pump", "turn_on", {"duration": 5})
        """
        success = False
        msg = ""
        try:
            # 1. Determine the effective duration
            # Default to 0 (which means stay on indefinitely if not specified)
            # But let's set a safe limit for water_pump to prevent crash loops
            duration = 0
            if params and "duration" in params:
                duration = float(params["duration"])
            
            # If the action_value is a number and it's a water_pump action, treat it as duration
            if action_name == "water_pump" or action_name == "turn_on_pump":
                try:
                    val_num = float(action_value)
                    if val_num > 1: # If it's a significant number (e.g., 50 or 5)
                        duration = val_num / 10 if val_num > 20 else val_num # Handle '50' as 5s if user implies it
                except:
                    pass

            # Mapping logic
            is_on = (action_value == "turn_on" or action_value == "ON" or action_value == "1" or action_value == 1)
            is_off = (action_value == "turn_off" or action_value == "OFF" or action_value == "0" or action_value == 0)
            
            # If we calculated a duration, it implies 'is_on'
            if duration > 0:
                is_on = True

            # 2. Execution Logic (Low-Level Trigger: 0=ON, 1=OFF)
            if action_name == "turn_on_pump" or (action_name == "water_pump" and is_on):
                print("[Action] Pump STARTING (LOW)...")
                RELAY_PUMP.value(0) # 0 = ON
                success = True
                msg = "Pump turned ON"
                
                # If a duration was specified or detected, wait and then turn OFF
                if duration > 0:
                    msg += " for {}s".format(duration)
                    print("[Action] Waiting {}s...".format(duration))
                    time.sleep(duration)
                    RELAY_PUMP.value(1) # 1 = OFF
                    msg += " and then AUTO-OFF"
                    print("[Action] Pump AUTO-OFF.")
                
            elif action_name == "turn_off_pump" or (action_name == "water_pump" and is_off):
                RELAY_PUMP.value(1) # 1 = OFF
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

# Thoi gian nghi mac dinh cho moi Rule (giay) - mac dinh 10 phut
DEFAULT_COOLDOWN = 150 

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
            now = time.time()
            if rule_id in last_run_execution:
                elapsed = now - last_run_execution[rule_id]
                if elapsed < DEFAULT_COOLDOWN:
                    print("  -> Rule [{}] dang trong thoi gian nghi (con {}s)...".format(rule_name, int(DEFAULT_COOLDOWN - elapsed)))
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
            if not is_scheduled and "water_pump" in ujson.dumps(actions):
                RELAY_PUMP.value(1) # 1 = OFF
            
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
                    
                    succ, msg = HardwareActions.execute(comp, val, params)
                    if succ:
                        # Cap nhat thoi gian chay cuoi cùng de cooldown
                        last_run_execution[rule_id] = time.time()
                    send_execution_log(rule_id, comp, succ, msg)
            else:
                # Legacy flat format: {"turn_on_pump": True}
                for action_key, action_val in actions.items():
                    succ, msg = HardwareActions.execute(action_key, action_val)
                    if succ:
                        last_run_execution[rule_id] = time.time()
                    send_execution_log(rule_id, action_key, succ, msg)
                
            # Ngi giua cac action de dam bao on dinh phan cung
            time.sleep(1)
