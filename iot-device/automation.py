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
    def execute(action_name, action_value):
        success = False
        msg = ""
        try:
            val = 1 if action_value else 0
            if action_name == "turn_on_pump":
                RELAY_PUMP.value(val)
                success = True
                msg = "Pump set to {}".format(val)
            else:
                msg = "Action unknown: {}".format(action_name)
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
            
        # 2. Kiem tra dieu kien cam bien (Neu co set)
        match = True
        for key, expected_val in conditions.items():
            current_val = sensor_data.get(key, None)
            if current_val is None:
                match = False
                break
            
            # Kiem tra logic JSON (vd: ">", "<", "==")
            if isinstance(expected_val, dict):
                if ">" in expected_val and not (current_val > expected_val[">"]):
                    match = False
                if "<" in expected_val and not (current_val < expected_val["<"]):
                    match = False
                if "==" in expected_val and not (current_val == expected_val["=="]):
                    match = False
            else:
                if current_val != expected_val:
                    match = False
                    
            
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
            print("[Automation] Phat hien Rule [{}] khop dieu kien hoac dung gio!".format(rule.get('name', 'Unknown')))
            for action_key, action_val in actions.items():
                succ, msg = HardwareActions.execute(action_key, action_val)
                send_execution_log(rule_id, action_key, succ, msg)
                
            # Ngi giua cac action de dam bao on dinh phan cung
            time.sleep(1)
