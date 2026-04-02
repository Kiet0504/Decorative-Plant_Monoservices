-- ============================================================================
-- SEED DATA: Nhóm Ưa bóng / Chịu bóng (Shade-loving plants)
-- Chạy trong pgAdmin: Query Tool → Paste → F5 (Execute)
-- Yêu cầu: Company & Branch từ pg_dummy_data.sql đã được insert trước
-- ============================================================================

-- ============================================================================
-- 1. PLANT CATEGORY: Nhóm Ưa bóng / Chịu bóng
-- ============================================================================
INSERT INTO "PlantCategories" ("Id", "Name", "Slug", "ParentId", "IconUrl")
VALUES 
('ca000001-0001-0001-0001-000000000001', 'Cây ưa bóng / chịu bóng', 'cay-ua-bong-chiu-bong', NULL, NULL)
ON CONFLICT ("Id") DO NOTHING;

-- ============================================================================
-- 2. PLANT TAXONOMY: 4 loài cây
-- ============================================================================

-- 2.1 Aglaonema commutatum (Vạn lộc / Ngọc ngân)
INSERT INTO plant_taxonomy ("Id", "ScientificName", "CommonNames", "TaxonomyInfo", "CareInfo", "GrowthInfo", "ImageUrl", "CategoryId", "CreatedAt", "UpdatedAt")
VALUES (
  'aa000001-0001-0001-0001-000000000001',
  'Aglaonema commutatum',
  '{"vi": ["Vạn lộc", "Ngọc ngân"], "en": ["Chinese Evergreen"]}',
  '{"family": "Araceae", "genus": "Aglaonema", "species": "commutatum"}',
  '{"care_level": "easy", "light": "low", "water": "weekly", "humidity": "medium", "temp_min": 18, "temp_max": 30}',
  '{"growth_rate": "slow", "max_height": 90, "is_toxic": true}',
  'https://upload.wikimedia.org/wikipedia/commons/thumb/5/5e/Aglaonema_commutatum.jpg/800px-Aglaonema_commutatum.jpg',
  'ca000001-0001-0001-0001-000000000001',
  NOW(), NULL
) ON CONFLICT ("Id") DO NOTHING;

-- 2.2 Dieffenbachia seguine (Vạn niên thanh)
INSERT INTO plant_taxonomy ("Id", "ScientificName", "CommonNames", "TaxonomyInfo", "CareInfo", "GrowthInfo", "ImageUrl", "CategoryId", "CreatedAt", "UpdatedAt")
VALUES (
  'aa000002-0002-0002-0002-000000000002',
  'Dieffenbachia seguine',
  '{"vi": ["Vạn niên thanh"], "en": ["Dumb Cane", "Dieffenbachia"]}',
  '{"family": "Araceae", "genus": "Dieffenbachia", "species": "seguine"}',
  '{"care_level": "easy", "light": "low", "water": "weekly", "humidity": "medium", "temp_min": 16, "temp_max": 30}',
  '{"growth_rate": "medium", "max_height": 150, "is_toxic": true}',
  'https://upload.wikimedia.org/wikipedia/commons/thumb/a/a7/Dieffenbachia_seguine.jpg/800px-Dieffenbachia_seguine.jpg',
  'ca000001-0001-0001-0001-000000000001',
  NOW(), NULL
) ON CONFLICT ("Id") DO NOTHING;

-- 2.3 Spathiphyllum wallisii (Lan ý)
INSERT INTO plant_taxonomy ("Id", "ScientificName", "CommonNames", "TaxonomyInfo", "CareInfo", "GrowthInfo", "ImageUrl", "CategoryId", "CreatedAt", "UpdatedAt")
VALUES (
  'aa000003-0003-0003-0003-000000000003',
  'Spathiphyllum wallisii',
  '{"vi": ["Lan ý", "Huệ hòa bình"], "en": ["Peace Lily"]}',
  '{"family": "Araceae", "genus": "Spathiphyllum", "species": "wallisii"}',
  '{"care_level": "easy", "light": "low", "water": "biweekly", "humidity": "high", "temp_min": 18, "temp_max": 30}',
  '{"growth_rate": "medium", "max_height": 60, "is_toxic": true}',
  'https://upload.wikimedia.org/wikipedia/commons/thumb/b/bd/Spathiphyllum_wallisii.jpg/800px-Spathiphyllum_wallisii.jpg',
  'ca000001-0001-0001-0001-000000000001',
  NOW(), NULL
) ON CONFLICT ("Id") DO NOTHING;

-- 2.4 Zamioculcas zamiifolia (Kim tiền)
INSERT INTO plant_taxonomy ("Id", "ScientificName", "CommonNames", "TaxonomyInfo", "CareInfo", "GrowthInfo", "ImageUrl", "CategoryId", "CreatedAt", "UpdatedAt")
VALUES (
  'aa000004-0004-0004-0004-000000000004',
  'Zamioculcas zamiifolia',
  '{"vi": ["Kim tiền", "Cây kim phát tài"], "en": ["ZZ Plant", "Zanzibar Gem"]}',
  '{"family": "Araceae", "genus": "Zamioculcas", "species": "zamiifolia"}',
  '{"care_level": "easy", "light": "low", "water": "when_dry", "humidity": "low", "temp_min": 15, "temp_max": 30}',
  '{"growth_rate": "slow", "max_height": 100, "is_toxic": true}',
  'https://upload.wikimedia.org/wikipedia/commons/thumb/f/f0/Zamioculcas_zamiifolia_2.jpg/800px-Zamioculcas_zamiifolia_2.jpg',
  'ca000001-0001-0001-0001-000000000001',
  NOW(), NULL
) ON CONFLICT ("Id") DO NOTHING;

-- ============================================================================
-- 3. PLANT BATCHES (Lô hàng)
-- BranchId = '22222222-...' (Chi nhánh HCM) từ pg_dummy_data.sql
-- ============================================================================

INSERT INTO plant_batch ("Id", "BranchId", "TaxonomyId", "SupplierId", "ParentBatchId", "BatchCode", "SourceInfo", "Specs", "InitialQuantity", "CurrentTotalQuantity", "CreatedAt")
VALUES
  ('ba000001-0001-0001-0001-000000000001', '22222222-2222-2222-2222-222222222222', 'aa000001-0001-0001-0001-000000000001', NULL, NULL, 'BATCH-AGLA-001', '{"type": "purchase", "acquisition_date": "2025-12-01"}', '{"unit": "unit", "pot_size": "5inch", "maturity_stage": "mature"}', 50, 50, NOW()),
  ('ba000002-0002-0002-0002-000000000002', '22222222-2222-2222-2222-222222222222', 'aa000002-0002-0002-0002-000000000002', NULL, NULL, 'BATCH-DIEF-001', '{"type": "purchase", "acquisition_date": "2025-12-01"}', '{"unit": "unit", "pot_size": "5inch", "maturity_stage": "mature"}', 40, 40, NOW()),
  ('ba000003-0003-0003-0003-000000000003', '22222222-2222-2222-2222-222222222222', 'aa000003-0003-0003-0003-000000000003', NULL, NULL, 'BATCH-SPATH-001', '{"type": "purchase", "acquisition_date": "2025-12-01"}', '{"unit": "unit", "pot_size": "5inch", "maturity_stage": "flowering"}', 60, 60, NOW()),
  ('ba000004-0004-0004-0004-000000000004', '22222222-2222-2222-2222-222222222222', 'aa000004-0004-0004-0004-000000000004', NULL, NULL, 'BATCH-ZAMI-001', '{"type": "purchase", "acquisition_date": "2025-12-01"}', '{"unit": "unit", "pot_size": "5inch", "maturity_stage": "mature"}', 80, 80, NOW())
ON CONFLICT ("Id") DO NOTHING;

-- ============================================================================
-- 4. BATCH STOCK (Tồn kho)
-- LocationId = '33333333-...' (Khu trưng bày) từ pg_dummy_data.sql
-- ============================================================================

INSERT INTO batch_stock ("Id", "BatchId", "LocationId", "Quantities", "HealthStatus", "LastCountInfo", "UpdatedAt")
VALUES
  ('b5000001-0001-0001-0001-000000000001', 'ba000001-0001-0001-0001-000000000001', '33333333-3333-3333-3333-333333333333', '{"quantity": 50, "reserved_quantity": 0, "available_quantity": 50}', 'Healthy', '{"last_counted_at": "2025-12-15T10:00:00Z"}', NOW()),
  ('b5000002-0002-0002-0002-000000000002', 'ba000002-0002-0002-0002-000000000002', '33333333-3333-3333-3333-333333333333', '{"quantity": 40, "reserved_quantity": 0, "available_quantity": 40}', 'Healthy', '{"last_counted_at": "2025-12-15T10:00:00Z"}', NOW()),
  ('b5000003-0003-0003-0003-000000000003', 'ba000003-0003-0003-0003-000000000003', '33333333-3333-3333-3333-333333333333', '{"quantity": 60, "reserved_quantity": 0, "available_quantity": 60}', 'Healthy', '{"last_counted_at": "2025-12-15T10:00:00Z"}', NOW()),
  ('b5000004-0004-0004-0004-000000000004', 'ba000004-0004-0004-0004-000000000004', '33333333-3333-3333-3333-333333333333', '{"quantity": 80, "reserved_quantity": 0, "available_quantity": 80}', 'Healthy', '{"last_counted_at": "2025-12-15T10:00:00Z"}', NOW())
ON CONFLICT ("Id") DO NOTHING;

-- ============================================================================
-- 5. PRODUCT LISTINGS (Sản phẩm bán online)
-- ============================================================================

-- 5.1 Vạn lộc / Ngọc ngân - 180,000 VND
INSERT INTO product_listing ("Id", "BranchId", "BatchId", "ProductInfo", "StatusInfo", "SeoInfo", "Images", "CreatedAt")
VALUES (
  'a1000001-0001-0001-0001-000000000001',
  '22222222-2222-2222-2222-222222222222',
  'ba000001-0001-0001-0001-000000000001',
  '{"title": "Cây Vạn Lộc (Ngọc Ngân) - Aglaonema", "slug": "cay-van-loc-ngoc-ngan-aglaonema", "description": "Nhà vô địch của các góc tối, rất bền bỉ dưới ánh sáng nhân tạo. Lá có vân rực rỡ, decor bàn làm việc, góc khuất văn phòng, kệ sách hoặc tủ trang trí tại phòng khách. Nên dùng chậu sứ trắng để tôn vân lá rực rỡ.", "price": "180000", "min_order": 1, "max_order": 10}',
  '{"status": "active", "visibility": "public", "featured": true, "view_count": 0, "sold_count": 0, "tags": ["indoor", "shade-loving", "easy-care", "office", "air-purifier"]}',
  '{"meta_title": "Cây Vạn Lộc Aglaonema - Cây chịu bóng tốt nhất", "meta_description": "Mua cây Vạn Lộc (Ngọc Ngân) - Aglaonema commutatum. Cây cảnh ưa bóng, dễ chăm, phù hợp văn phòng và phòng khách."}',
  '[{"url": "https://images.unsplash.com/photo-1637967886160-fd78dc3ce3f5?w=800", "alt": "Cây Vạn Lộc Aglaonema", "is_primary": true, "sort_order": 0}]',
  NOW()
) ON CONFLICT ("Id") DO NOTHING;

-- 5.2 Vạn niên thanh - 150,000 VND
INSERT INTO product_listing ("Id", "BranchId", "BatchId", "ProductInfo", "StatusInfo", "SeoInfo", "Images", "CreatedAt")
VALUES (
  'a1000002-0002-0002-0002-000000000002',
  '22222222-2222-2222-2222-222222222222',
  'ba000002-0002-0002-0002-000000000002',
  '{"title": "Cây Vạn Niên Thanh - Dieffenbachia", "slug": "cay-van-nien-thanh-dieffenbachia", "description": "Ưa bóng râm, lá to đẹp. Phù hợp đặt cạnh kệ tivi phòng khách, hành lang hoặc góc phòng làm việc. Lưu ý đặt trên kệ cao để tránh xa tầm tay trẻ em và thú cưng do nhựa cây có độc.", "price": "150000", "min_order": 1, "max_order": 10}',
  '{"status": "active", "visibility": "public", "featured": false, "view_count": 0, "sold_count": 0, "tags": ["indoor", "shade-loving", "easy-care", "decorative-leaves"]}',
  '{"meta_title": "Cây Vạn Niên Thanh Dieffenbachia - Cây phong thủy", "meta_description": "Mua cây Vạn Niên Thanh - Dieffenbachia seguine. Cây ưa bóng, lá to đẹp, phù hợp trang trí phòng khách và hành lang."}',
  '[{"url": "https://images.unsplash.com/photo-1616690710400-a16d146927c5?w=800", "alt": "Cây Vạn Niên Thanh", "is_primary": true, "sort_order": 0}]',
  NOW()
) ON CONFLICT ("Id") DO NOTHING;

-- 5.3 Lan ý - 120,000 VND
INSERT INTO product_listing ("Id", "BranchId", "BatchId", "ProductInfo", "StatusInfo", "SeoInfo", "Images", "CreatedAt")
VALUES (
  'a1000003-0003-0003-0003-000000000003',
  '22222222-2222-2222-2222-222222222222',
  'ba000003-0003-0003-0003-000000000003',
  '{"title": "Cây Lan Ý - Peace Lily", "slug": "cay-lan-y-peace-lily", "description": "Chịu bóng tốt, có thể ra hoa ngay cả khi không có nắng trực tiếp. Đẹp khi đặt trên đầu giường phòng ngủ, bàn làm việc hoặc bàn ăn. Có thể trồng thủy sinh trong bình thủy tinh tạo vẻ thanh tao và trang nhã.", "price": "120000", "min_order": 1, "max_order": 10}',
  '{"status": "active", "visibility": "public", "featured": true, "view_count": 0, "sold_count": 0, "tags": ["indoor", "shade-loving", "easy-care", "flowering", "hydroponic", "bedroom"]}',
  '{"meta_title": "Cây Lan Ý Peace Lily - Hoa đẹp chịu bóng", "meta_description": "Mua cây Lan Ý - Spathiphyllum wallisii. Cây cảnh chịu bóng, ra hoa đẹp, phù hợp phòng ngủ và bàn làm việc."}',
  '[{"url": "https://images.unsplash.com/photo-1593691509543-c55fb32d8de5?w=800", "alt": "Cây Lan Ý Peace Lily", "is_primary": true, "sort_order": 0}]',
  NOW()
) ON CONFLICT ("Id") DO NOTHING;

-- 5.4 Kim tiền - 250,000 VND
INSERT INTO product_listing ("Id", "BranchId", "BatchId", "ProductInfo", "StatusInfo", "SeoInfo", "Images", "CreatedAt")
VALUES (
  'a1000004-0004-0004-0004-000000000004',
  '22222222-2222-2222-2222-222222222222',
  'ba000004-0004-0004-0004-000000000004',
  '{"title": "Cây Kim Tiền - ZZ Plant", "slug": "cay-kim-tien-zz-plant", "description": "Sống khỏe trong bóng râm và là lựa chọn hàng đầu cho văn phòng. Phù hợp đặt tại góc sofa phòng khách, quầy thu ngân, sảnh văn phòng hoặc lối đi hành lang. Chậu đá mài to đặt sàn tạo vẻ hiện đại.", "price": "250000", "min_order": 1, "max_order": 10}',
  '{"status": "active", "visibility": "public", "featured": true, "view_count": 0, "sold_count": 0, "tags": ["indoor", "shade-loving", "easy-care", "office", "feng-shui", "modern"]}',
  '{"meta_title": "Cây Kim Tiền ZZ Plant - Cây phong thủy văn phòng", "meta_description": "Mua cây Kim Tiền - Zamioculcas zamiifolia. Cây cảnh dễ sống, phong thủy tốt, phù hợp văn phòng và phòng khách."}',
  '[{"url": "https://images.unsplash.com/photo-1632207691143-643e2a9a9361?w=800", "alt": "Cây Kim Tiền ZZ Plant", "is_primary": true, "sort_order": 0}]',
  NOW()
) ON CONFLICT ("Id") DO NOTHING;

-- ============================================================================
-- DONE! Xác nhận kết quả:
-- ============================================================================
SELECT 'Categories' AS "Table", COUNT(*) AS "Count" FROM "PlantCategories" WHERE "Id" = 'ca000001-0001-0001-0001-000000000001'
UNION ALL
SELECT 'Taxonomies', COUNT(*) FROM plant_taxonomy WHERE "CategoryId" = 'ca000001-0001-0001-0001-000000000001'
UNION ALL
SELECT 'Batches', COUNT(*) FROM plant_batch WHERE "Id" IN ('ba000001-0001-0001-0001-000000000001', 'ba000002-0002-0002-0002-000000000002', 'ba000003-0003-0003-0003-000000000003', 'ba000004-0004-0004-0004-000000000004')
UNION ALL
SELECT 'Stocks', COUNT(*) FROM batch_stock WHERE "Id" IN ('b5000001-0001-0001-0001-000000000001', 'b5000002-0002-0002-0002-000000000002', 'b5000003-0003-0003-0003-000000000003', 'b5000004-0004-0004-0004-000000000004')
UNION ALL
SELECT 'Listings', COUNT(*) FROM product_listing WHERE "Id" IN ('a1000001-0001-0001-0001-000000000001', 'a1000002-0002-0002-0002-000000000002', 'a1000003-0003-0003-0003-000000000003', 'a1000004-0004-0004-0004-000000000004');
