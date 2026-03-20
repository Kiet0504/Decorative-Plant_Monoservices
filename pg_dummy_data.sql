-- Dữ liệu mẫu (Dummy Data) để Insert trực tiếp vào PostgreSQL (pgAdmin)
-- Lưu ý: Bạn có thể chọn tất cả đoạn mã và ấn F5 (hoặc nút Play/Execute) trong mục Query Tool của pgAdmin để thêm nhanh dữ liệu.

-- 1. COMPANIES (Công ty)
INSERT INTO company ("Id", "Name", "TaxCode", "Email", "Phone", "Info", "CreatedAt", "UpdatedAt")
VALUES 
('11111111-1111-1111-1111-111111111111', 'Công ty Cổ phần Decorative Plant', '0123456789', 'contact@decorativeplant.vn', '0901234567', '{"website": "https://decorativeplant.vn"}', NOW(), NULL)
ON CONFLICT ("Id") DO NOTHING;

-- 2. BRANCHES (Chi nhánh nhà vườn)
-- Lưu ý: "CompanyId" phải khớp với ID Công ty ở trên
INSERT INTO branch ("Id", "CompanyId", "Code", "Name", "Slug", "BranchType", "ContactInfo", "OperatingHours", "Settings", "IsActive", "CreatedAt", "UpdatedAt")
VALUES 
('22222222-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111111', 'BR-HCM-01', 'Chi nhánh Trung tâm TP.HCM', 'chi-nhanh-hcm', 'Nursery', '{"phone": "0901234567"}', '{"open": "08:00", "close": "18:00"}', '{}', true, NOW(), NULL)
ON CONFLICT ("Id") DO NOTHING;

-- 3. INVENTORY LOCATIONS (Khu vực / Trạm / Kệ)
INSERT INTO inventory_location ("Id", "BranchId", "ParentLocationId", "Code", "Name", "Type", "Details")
VALUES 
('33333333-3333-3333-3333-333333333333', '22222222-2222-2222-2222-222222222222', NULL, 'ZONE-A1', 'Khu trưng bày Lan Hồ Điệp', 'Zone', '{"description": "Khu vực ưu tiên cho Lan", "capacity": 500}')
ON CONFLICT ("Id") DO NOTHING;

-- 4. IOT DEVICES (Thiết bị IOT)
-- Lưu ý: Không có CreatedAt / UpdatedAt vì IotDevice không kế thừa từ BaseEntity
INSERT INTO iot_device ("Id", "BranchId", "LocationId", "Status", "SecretKey", "DeviceInfo", "Components")
VALUES 
('44444444-4444-4444-4444-444444444444', '22222222-2222-2222-2222-222222222222', '33333333-3333-3333-3333-333333333333', 'Active', 'test-secret-key-12345', '{"model": "ESP32", "firmware": "1.0"}', '{"temp_sensor": "DHT22", "soil_moisture": "Analog"}')
ON CONFLICT ("Id") DO NOTHING;

-- 5. SENSOR READINGS (Dữ liệu Đọc Cảm biến - Ví dụ 1 dòng độ C)
-- Bảng này thường được POST từ API của phần cứng, nhưng bạn có thể thêm tay để test Query
INSERT INTO sensor_reading ("Id", "DeviceId", "ComponentKey", "Value", "RecordedAt")
VALUES 
('55555555-5555-5555-5555-555555555555', '44444444-4444-4444-4444-444444444444', 'temp_sensor', 26.5, NOW())
ON CONFLICT ("Id") DO NOTHING;
