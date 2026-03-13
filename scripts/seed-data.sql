-- DATA MẪU CHUẨN CHO FE TEST MAPPING (Cập nhật khớp với Database Schema thực tế)
-- File: Decorative-Plant_Monoservices/scripts/seed-data.sql

-- 1. Xóa dữ liệu cũ (Sử dụng CASCADE để xóa các bảng liên quan)
TRUNCATE TABLE "user_account", "branch", "company" RESTART IDENTITY CASCADE;

-- 2. TẠO CÔNG TY MẪU (Bắt buộc vì Branch cần CompanyId)
INSERT INTO "company" ("Id", "Name", "TaxCode", "Email", "Phone", "CreatedAt") VALUES
('11111111-1111-1111-1111-111111111111', 'EcoSystem Corp', '123456789', 'contact@ecosystem.com', '0123456789', now());

-- 3. TÀI KHOẢN NGƯỜI DÙNG (Cột phải viết hoa chính xác và đặt trong dấu nháy kép "")
-- password_hash mẫu cho 'Password123'
INSERT INTO "user_account" ("Id", "Email", "PasswordHash", "Role", "DisplayName", "IsActive", "EmailVerified", "Bio", "CreatedAt") VALUES
(
    'a1b2c3d4-e5f6-7a8b-9c0d-e1f2a3b4c5d6', 
    'admin@decorativeplant.com', 
    'AQAAAAEAACcQAAAAEHr8Z...', 
    'Admin', 
    'Alex Rivera', 
    true, 
    true,
    'Alex is a senior product designer with over 8 years of experience.', 
    '2023-01-12'
),
(
    'b2c3d4e5-f6a7-8b9c-0d1e-f2a3b4c5d6e7', 
    'jane@organicfarm.com', 
    'AQAAAAEAACcQAAAAEHr8Z...', 
    'Moderator', 
    'Jane Cooper', 
    true, 
    true,
    'Specializes in content moderation and community management.', 
    '2023-02-05'
),
(
    'c3d4e5f6-a7b8-9c0d-1e2f-a3b4c5d6e7f8', 
    'cody@freshgreens.io', 
    'AQAAAAEAACcQAAAAEHr8Z...', 
    'User', 
    'Cody Fisher', 
    true, 
    false,
    'Lover of succulents and indoor plants.', 
    '2023-03-04'
),
(
    'd4e5f6a7-b8c9-0d1e-2f3a-b4c5d6e7f8a9', 
    'jenny@naturefirst.org', 
    'AQAAAAEAACcQAAAAEHr8Z...', 
    'User', 
    'Jenny Wilson', 
    false, 
    true,
    'Botanical researcher and plant care expert.', 
    '2023-08-02'
);

-- 4. CHI NHÁNH MẪU (Seller test)
-- Lưu ý: Phải có CompanyId và các cột đúng định dạng PascalCase
INSERT INTO "branch" ("Id", "CompanyId", "Code", "Name", "Slug", "IsActive", "ContactInfo", "CreatedAt") VALUES
(
    '23c4d5e6-f7a8-9b0c-1d2e-3f4a5b6c7d8e', 
    '11111111-1111-1111-1111-111111111111', 
    'HQ-001', 
    'EcoFresh Market HQ', 
    'ecofresh-market-hq', 
    true, 
    '{"phone": "0987654321", "full_address": "District 1, HCM City"}', 
    now()
),
(
    '34d5e6f7-a8b9-0c1d-2e3f-4a5b6c7d8e9f', 
    '11111111-1111-1111-1111-111111111111', 
    'ST-002', 
    'Urban Jungle Store', 
    'urban-jungle-store', 
    true, 
    '{"phone": "0123456789", "full_address": "District 7, HCM City"}', 
    now()
);
