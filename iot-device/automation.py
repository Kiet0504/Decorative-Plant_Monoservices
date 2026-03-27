import urequests
import ujson
from machine import Pin
import config
import time

API_URL_RULES = config.env.get("API_URL", "").replace("/sensors/ingest", "/sensors/rules")
API_URL_LOGS = config.env.get("API_URL", "").replace("/sensors/ingest", "/sensors/logs")
DEVICE_SECRET = config.env.get("DEVICE_SECRET", "")

# Cau hinh chan GPIO
RELAY_PUMP = Pin(4, Pin.OUT)

# Dam bao mac dinh la tat
RELAY_PUMP.value(0)

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
            # Handle duration if provided
            duration = 0
            if params and "duration" in params:
                duration = params["duration"]

            # Mapping logic
            if action_name == "turn_on_pump" or (action_name == "water_pump" and action_value == "turn_on"):
                RELAY_PUMP.value(1)
                success = True
                msg = "Pump turned ON"
                if duration > 0:
                    msg += " for {}s".format(duration)
                    # Note: Non-blocking duration would be better, but for now we block or just set state
                    # We will just log it for now
            elif action_name == "turn_off_pump" or (action_name == "water_pump" and action_value == "turn_off"):
                RELAY_PUMP.value(0)
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

# Tu dien theo doi de tranh chay lai Rule lich trinh nhieu lan trong cung 1 phut/ngay
last_run_schedule = {}

def check_schedule(rule_id, schedule_dict):
    """
    Kiem tra neu luat co chua 'schedule' (vd: {"time": "06:00"}).
    Tra ve (co_can_chay_khong, is_scheduled_rule)
    """
    if not schedule_dict:
        return True, False # Khong co lich hen, coi nhu pass ve mat thoi gian

    target_time = schedule_dict.get("time", "")
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

def evaluate_and_run(sensor_data, active_rules):
    # sensor_data: dict, e.g., {"temp_sensor": 30.5}
    for rule in active_rules:
        conditions = rule.get("conditions", {}) or {}
        actions = rule.get("actions", {}) or {}
        schedule = rule.get("schedule", {}) or {}
        
        rule_id = rule.get("id", "0000")
        
        # 1. Kiem tra gio giac (Neu co set)
        should_run_time, is_scheduled = check_schedule(rule_id, schedule)
        if not should_run_time:
            continue # Chua den gio hoac da chay roi
            
        # 2. Kiem tra dieu kien cam bien
        match = True
        
        # Handle different condition structures
        rules_list = []
        if isinstance(conditions, list):
            rules_list = conditions
        elif isinstance(conditions, dict) and "rules" in conditions:
            rules_list = conditions["rules"]
        elif isinstance(conditions, dict):
            # Legacy flat format: {"temp_sensor": {">": 30}}
            for k, v in conditions.items():
                rules_list.append({"component": k, "logic": v})

        for r in rules_list:
            comp = r.get("component")
            current_val = sensor_data.get(comp, None)
            if current_val is None:
                match = False
                break
            
            # Nested logic: {"operator": "<", "value": 40}
            op = r.get("operator")
            val = r.get("value")
            
            # Fallback for old format
            if not op and "logic" in r:
                logic_dict = r["logic"]
                if isinstance(logic_dict, dict):
                    op = list(logic_dict.keys())[0]
                    val = logic_dict[op]

            if op == ">" and not (current_val > val): match = False
            elif op == "<" and not (current_val < val): match = False
            elif (op == "==" or op == "=") and not (current_val == val): match = False
            elif op == ">=" and not (current_val >= val): match = False
            elif op == "<=" and not (current_val <= val): match = False
            
            if not match:
                break
                
        # Rule hop le khi nao? 
        # - Neu la luat Hen Gio (is_scheduled = True) thi dieu kien Match = True moi chay (hoac ko co dieu kien)
        # - Neu la luat Cam Bien ko thoi thi dieu kien Match phai = True va phai co it nhat 1 condition
        is_valid = False
        if is_scheduled and match:
            is_valid = True
        elif not is_scheduled and conditions and match:
            is_valid = True
            
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
                    send_execution_log(rule_id, comp, succ, msg)
            else:
                # Legacy flat format: {"turn_on_pump": True}
                for action_key, action_val in actions.items():
                    succ, msg = HardwareActions.execute(action_key, action_val)
                    send_execution_log(rule_id, action_key, succ, msg)
                
            # Ngi giua cac action de dam bao on dinh phan cung
            time.sleep(1)
