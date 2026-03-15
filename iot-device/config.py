def load_env(filename=".env"):
    env = {}
    try:
        with open(filename, "r") as f:
            for line in f:
                line = line.strip()
                if not line or line.startswith("#"):
                    continue
                if "=" in line:
                    key, value = line.split("=", 1)
                    env[key.strip()] = value.strip()
    except Exception as e:
        print("[Env] Khong that file " + filename + " hoac bi loi:", e)
    return env

# Tu dong load khi import
env = load_env()
