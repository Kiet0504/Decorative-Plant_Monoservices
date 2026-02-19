# JSONB Schema Reference — Smart Ornamental Plant Support System

> **Purpose:** Use this doc when coding. JSONB has no schema validation—the DB won't enforce structure.  
> Always reference this file to know what to put in each JSONB field.

---

## 1. user_account.addresses

**Type:** Array of address objects

```json
[
  {
    "id": "uuid",
    "label": "Home",
    "recipient_name": "...",
    "phone": "...",
    "address_line_1": "...",
    "city": "...",
    "is_default": true
  }
]
```

---

## 2. notification.data

**Type:** Object — metadata for notification click action

```json
{
  "order_id": "uuid",
  "plant_id": "uuid",
  "type": "order|care_reminder|system|promotion|alert"
}
```

---

## 3. company.info

**Type:** Object — logo, website, description, address

```json
{
  "logo_url": "...",
  "website": "...",
  "description": "...",
  "address": "..."
}
```

---

## 4. branch.contact_info

**Type:** Object

```json
{
  "phone": "...",
  "email": "...",
  "full_address": "...",
  "city": "...",
  "lat": 10.0,
  "long": 106.0
}
```

---

## 5. branch.operating_hours

**Type:** Object — day keys to time ranges

```json
{
  "mon": "8:00-18:00",
  "tue": "8:00-18:00",
  "wed": "8:00-18:00",
  "thu": "8:00-18:00",
  "fri": "8:00-18:00",
  "sat": "8:00-12:00",
  "sun": "closed"
}
```

---

## 6. branch.settings

**Type:** Object

```json
{
  "supports_online_order": true,
  "supports_pickup": true,
  "supports_shipping": true
}
```

---

## 7. staff_assignment.permissions

**Type:** Object

```json
{
  "can_manage_inventory": true,
  "can_manage_orders": true,
  "can_view_other_branches": false
}
```

---

## 8. plant_taxonomy.common_names

**Type:** Object — locale to name

```json
{
  "en": "Monstera",
  "vi": "Trầu bà Nam Mỹ"
}
```

---

## 9. plant_taxonomy.taxonomy_info

**Type:** Object — family, genus, species, cultivar

```json
{
  "family": "Araceae",
  "genus": "Monstera",
  "species": "deliciosa",
  "cultivar": "Thai Constellation"
}
```

---

## 10. plant_taxonomy.care_info

**Type:** Object

```json
{
  "care_level": "easy|medium|hard",
  "light": "low|medium|bright_indirect|direct",
  "water": "weekly|biweekly|when_dry",
  "humidity": "low|medium|high",
  "temp_min": 18,
  "temp_max": 30
}
```

---

## 11. plant_taxonomy.growth_info

**Type:** Object

```json
{
  "growth_rate": "slow|medium|fast",
  "max_height": 300,
  "is_toxic": false
}
```

---

## 12. inventory_location.details

**Type:** Object

```json
{
  "capacity": 100,
  "environment_type": "indoor|outdoor|shaded|controlled",
  "description": "..."
}
```

---

## 13. supplier.contact_info

**Type:** Object

```json
{
  "contact_name": "...",
  "address": "...",
  "website": "..."
}
```

---

## 14. plant_batch.source_info

**Type:** Object

```json
{
  "type": "seed|cutting|tissue_culture|purchase|import",
  "acquisition_date": "2024-01-15",
  "sowing_date": "2024-01-20"
}
```

---

## 15. plant_batch.specs

**Type:** Object

```json
{
  "unit": "unit|kg|bunch",
  "pot_size": "3inch|5inch|1gallon|bare_root",
  "maturity_stage": "seedling|juvenile|mature|flowering"
}
```

---

## 16. batch_stock.quantities

**Type:** Object

```json
{
  "quantity": 50,
  "reserved_quantity": 5,
  "available_quantity": 45
}
```

---

## 17. batch_stock.last_count_info

**Type:** Object

```json
{
  "last_counted_at": "2024-02-01T10:00:00Z",
  "last_counted_by": "uuid"
}
```

---

## 18. stock_adjustment.meta_info

**Type:** Object

```json
{
  "adjusted_by": "uuid",
  "approved_by": "uuid",
  "reference_doc": "...",
  "quantities_before_after": { "before": 50, "after": 45 }
}
```

---

## 19. stock_transfer.logistics_info

**Type:** Object

```json
{
  "reason": "...",
  "requested_by": "uuid",
  "approved_by": "uuid",
  "shipped_at": "...",
  "received_at": "..."
}
```

---

## 20. cultivation_log.details

**Type:** Object

```json
{
  "quantity_affected": 20,
  "products_used": { "fertilizer": "NPK 20-20-20", "amount": "500ml" },
  "environmental_conditions": { "temp": 25, "humidity": 70 }
}
```

---

## 21. health_incident.details

**Type:** Object

```json
{
  "disease_name": "...",
  "pest_name": "...",
  "symptoms": "...",
  "environmental_factors": "..."
}
```

---

## 22. health_incident.treatment_info

**Type:** Object

```json
{
  "method": "...",
  "products": "...",
  "cost": "...",
  "applied_text": "..."
}
```

---

## 23. health_incident.status_info

**Type:** Object

```json
{
  "is_resolved": false,
  "resolution_notes": "...",
  "detected_at": "...",
  "resolved_at": "..."
}
```

---

## 24. health_incident.images

**Type:** Array of objects

```json
[
  {
    "url": "...",
    "caption": "...",
    "taken_at": "...",
    "ai_analysis": { "disease": "...", "confidence": 0.9 }
  }
]
```

---

## 25. iot_device.device_info

**Type:** Object

```json
{
  "code": "DEV-HCM-001",
  "name": "Greenhouse A Sensor Hub",
  "type": "sensor_hub|controller|camera",
  "mac": "...",
  "ip": "...",
  "firmware": "...",
  "manufacturer": "..."
}
```

---

## 26. iot_device.activity_log

**Type:** Object

```json
{
  "last_heartbeat_at": "...",
  "last_data_at": "..."
}
```

---

## 27. iot_device.components

**Type:** Array — sensor/actuator definitions (key used in sensor_reading.component_key)

```json
[
  {
    "key": "temp_sensor",
    "type": "sensor",
    "data_type": "temperature",
    "unit": "celsius"
  }
]
```

---

## 28. automation_rule.schedule

**Type:** Object

```json
{
  "type": "always|time_based|condition_based",
  "time_schedule": { "start": "06:00", "end": "18:00" }
}
```

---

## 29. automation_rule.conditions

**Type:** Array of condition objects

```json
[
  {
    "operator": ">|<|>=|<=|=",
    "threshold": 30,
    "component_key": "temp_sensor"
  }
]
```

---

## 30. automation_rule.actions

**Type:** Array of action objects

```json
[
  {
    "target_component_key": "pump_1",
    "action_type": "turn_on|turn_off|set_value",
    "value": "100"
  }
]
```

---

## 31. automation_execution_log.execution_info

**Type:** Object

```json
{
  "triggered_by": "...",
  "trigger_values": { "temp_sensor": 35 },
  "actions_executed": [...],
  "result": "success|failed",
  "error": "..."
}
```

---

## 32. iot_alert.alert_info

**Type:** Object

```json
{
  "type": "threshold_exceeded|device_offline|sensor_error",
  "severity": "info|warning|critical",
  "title": "...",
  "message": "...",
  "values": { "current": 35, "threshold": 30 }
}
```

---

## 33. iot_alert.resolution_info

**Type:** Object

```json
{
  "is_resolved": false,
  "resolved_at": "...",
  "resolved_by": "uuid"
}
```

---

## 34. product_listing.product_info

**Type:** Object

```json
{
  "title": "...",
  "slug": "...",
  "description": "...",
  "price": "100000",
  "min_order": 1,
  "max_order": 10
}
```

---

## 35. product_listing.status_info

**Type:** Object

```json
{
  "status": "draft|active|inactive|out_of_stock",
  "visibility": "public|private|branch_only",
  "featured": false,
  "view_count": 0,
  "sold_count": 0,
  "tags": ["indoor", "easy-care"]
}
```

---

## 36. product_listing.images

**Type:** Array

```json
[
  { "url": "...", "alt": "...", "is_primary": true, "sort_order": 0 }
]
```

---

## 37. shipping_zone.locations

**Type:** Object — cities, districts

```json
{
  "cities": ["HCM", "HN"],
  "districts": ["Q1", "Q2"]
}
```

---

## 38. shipping_zone.fee_config

**Type:** Object

```json
{
  "base_fee": "30000",
  "fee_per_km": "5000",
  "free_threshold": "500000"
}
```

---

## 39. shipping_zone.delivery_time_config

**Type:** Object

```json
{
  "min_days": 1,
  "max_days": 3
}
```

---

## 40. voucher.info

**Type:** Object

```json
{
  "name": "...",
  "description": "...",
  "valid_from": "...",
  "valid_to": "..."
}
```

---

## 41. voucher.rules

**Type:** Object

```json
{
  "type": "percentage|fixed_amount|free_shipping",
  "discount_type": "...",
  "value": "10",
  "min_order": "100000",
  "usage_limits": 100,
  "applicable_products": ["uuid", "uuid"]
}
```

---

## 42. promotion.config

**Type:** Object

```json
{
  "discount_type": "percentage|fixed_amount",
  "value": "10",
  "dates": { "start": "...", "end": "..." },
  "apply_to_all": false,
  "target_categories": ["uuid"],
  "min_order": "100000"
}
```

---

## 43. shopping_cart.items

**Type:** Array

```json
[
  {
    "listing_id": "uuid",
    "quantity": 1,
    "added_at": "2024-02-01T10:00:00Z"
  }
]
```

---

## 44. order_header.type_info

**Type:** Object

```json
{
  "order_type": "online|offline",
  "fulfillment_method": "delivery|pickup"
}
```

---

## 45. order_header.financials

**Type:** Object

```json
{
  "subtotal": "100000",
  "shipping": "30000",
  "discount": "10000",
  "tax": "0",
  "total": "120000"
}
```

---

## 46. order_header.notes

**Type:** Object

```json
{
  "customer_note": "...",
  "internal_note": "...",
  "rejection_reason": "...",
  "cancellation_reason": "..."
}
```

---

## 47. order_header.delivery_address

**Type:** Object — snapshot if delivery

```json
{
  "recipient_name": "...",
  "phone": "...",
  "address_line_1": "...",
  "city": "..."
}
```

---

## 48. order_header.pickup_info

**Type:** Object — snapshot if pickup

```json
{
  "branch_name": "...",
  "full_address": "...",
  "contact_name": "...",
  "contact_phone": "..."
}
```

---

## 49. order_item.pricing

**Type:** Object

```json
{
  "unit_price": "100000",
  "subtotal": "200000"
}
```

---

## 50. order_item.snapshots

**Type:** Object

```json
{
  "title_snapshot": "...",
  "image_snapshot": "...",
  "taxonomy_snapshot": { ... }
}
```

---

## 51. payment_transaction.details

**Type:** Object

```json
{
  "provider": "momo|vnpay|zalopay|cash|bank_transfer",
  "method": "e_wallet|credit_card|bank_transfer|cod",
  "type": "payment|refund",
  "amount": "120000",
  "status": "pending|success|failed",
  "external_id": "...",
  "metadata": { ... }
}
```

---

## 52. shipping.carrier_info

**Type:** Object

```json
{
  "carrier": "ghn|ghtk|viettel_post",
  "method": "standard|express",
  "fee": "30000"
}
```

---

## 53. shipping.delivery_details

**Type:** Object

```json
{
  "dates": { "estimated": "...", "actual": "..." },
  "person_received": "...",
  "photo": "..."
}
```

---

## 54. shipping.events

**Type:** Array — history of status changes

```json
[
  {
    "status": "in_transit",
    "location": "...",
    "description": "...",
    "event_time": "..."
  }
]
```

---

## 55. return_request.info

**Type:** Object

```json
{
  "reason": "damaged|wrong_item",
  "description": "...",
  "refund_amount": "100000"
}
```

---

## 56. return_request.images

**Type:** Array of image URLs

```json
["https://...", "https://..."]
```

---

## 57. garden_plant.details

**Type:** Object

```json
{
  "nickname": "...",
  "location": "Living room",
  "source": "purchased|gift|propagation|manual_add",
  "adopted_date": "2024-01-15",
  "health": "healthy|needs_attention|struggling",
  "size": "small|medium|large",
  "milestones": [
    {
      "id": "uuid",
      "type": "first_leaf|new_growth|flowering|repotted|other",
      "occurred_at": "2024-02-10T14:00:00Z",
      "notes": "...",
      "image_url": "https://..."
    }
  ]
}
```

---

## 58. care_schedule.task_info

**Type:** Object

```json
{
  "type": "water|fertilize|prune|repot|inspect",
  "frequency": "weekly|biweekly|monthly",
  "time_of_day": "morning|afternoon|evening",
  "next_due": "2024-02-10T09:00:00Z"
}
```

---

## 59. care_log.log_info

**Type:** Object

```json
{
  "action_type": "watered|fertilized|pruned|repotted|inspected",
  "description": "...",
  "products": { "fertilizer": "NPK 10-10-10", "amount": "1 tbsp" },
  "observations": "...",
  "mood": "thriving|okay|concerning"
}
```

---

## 60. care_log.images

**Type:** Array

```json
[
  { "url": "...", "caption": "...", "ai_tags": ["new_leaf", "healthy"] }
]
```

---

## 61. plant_diagnosis.user_input

**Type:** Object

```json
{
  "description": "...",
  "image_urls": ["https://..."]
}
```

---

## 62. plant_diagnosis.ai_result

**Type:** Object

```json
{
  "disease": "Powdery Mildew",
  "confidence": 0.87,
  "symptoms": ["white_powder", "yellowing"],
  "recommendations": ["Improve air circulation", "Apply fungicide"]
}
```

---

## 63. plant_diagnosis.feedback

**Type:** Object

```json
{
  "user_feedback": "helpful|not_helpful|wrong",
  "expert_notes": "..."
}
```

---

## 64. product_review.content

**Type:** Object

```json
{
  "rating": 5,
  "title": "...",
  "comment": "..."
}
```

---

## 65. product_review.status_info

**Type:** Object

```json
{
  "is_verified": true,
  "helpful_count": 5,
  "status": "published|pending|hidden"
}
```

---

## 66. product_review.images

**Type:** Array

```json
[
  { "url": "...", "alt": "...", "sort": 0 }
]
```

---

## 67. ai_training_feedback.source_info

**Type:** Object

```json
{
  "type": "diagnosis|recommendation",
  "id": "uuid"
}
```

---

## 68. ai_training_feedback.feedback_content

**Type:** Object

```json
{
  "rating": 5,
  "is_correct": true,
  "text": "...",
  "correction": "..."
}
```

---

## 69. system_config.value

**Type:** Any — flexible key-value store for system settings

---

## Quick Tips for Coding with JSONB

1. **Define C# DTOs** for each JSONB structure and serialize/deserialize with `System.Text.Json`.
2. **Add XML/remarks** on entity properties pointing to this doc, e.g. `/// <see cref="JSONB_SCHEMA_REFERENCE.md#1-user_accountaddresses"/>`
3. **Validate on write** — add FluentValidation or custom validation for JSONB payloads before saving.
4. **Use owned types** in EF Core if you want type safety: `OwnsOne(x => x.AddressesJson, a => a.ToJson())` (EF Core 7+).
