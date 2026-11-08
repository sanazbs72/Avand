# Specification Quality Checklist — فروش طلا و دریافت ریال قابل‌مصرف

**Purpose** — بررسی آمادگی spec پیش از Plan  
**Created** — 2026-11-08  
**Feature** — [V03](../spec.md)

## Content Quality

- [x] فاقد جزئیات پیاده‌سازی
- [x] متمرکز بر ارزش مشتری و قواعد کسب‌وکار
- [x] قابل‌خواندن برای ذی‌نفعان کسب‌وکار
- [x] همهٔ بخش‌های الزامی تکمیل شده‌اند

## Requirement Completeness

- [x] پرسش باز مؤثر بر Plan باقی نمانده است
- [x] همهٔ قواعد آزمون‌پذیر و بدون ابهام‌اند
- [x] معیارهای موفقیت قابل اندازه‌گیری‌اند
- [x] معیارهای موفقیت مستقل از فناوری‌اند
- [x] همهٔ سناریوهای پذیرش انتظار عددی یا تصمیم نهایی دارند
- [x] حالت‌های مرزی مشخص‌اند
- [x] محدوده مشخص است
- [x] وابستگی‌ها و مفروضات مشخص‌اند

## Feature Readiness

- [x] همهٔ الزامات معیار پذیرش دارند
- [x] سناریوها مسیرهای اصلی کاربر را پوشش می‌دهند
- [x] معیارهای پذیرش به نتایج قابل مشاهده پیوند دارند
- [x] انتخاب‌های پیاده‌سازی به Plan سپرده شده‌اند

## Notes

- Clarify در 2026-11-08 تکمیل شد. C-01 مبنای ورود و حد، C-02 گرد کردن و C-03 نقدینگی آزاد را قطعی می‌کنند. AC-01b، AC-04، AC-08 و FR-02، FR-03 و FR-05 بر این اساس به‌روزرسانی شدند. spec آمادهٔ Plan است.

## Analyze — 2026-11-08

| # | Artifact / requirement / task | Finding | Resolution |
| --- | --- | --- | --- |
| A1 | spec AC-08 / C-03 | AC-08 said a withdrawal reservation lowers free liquidity, contradicting section 7 of the chart of accounts (reclassification only). | AC-08 corrected. H/R increases are added to T018. |
| A2 | tasks T002 → T005/T006 | T002 used `OpeningCash` before the schema that defines it existed (ordering). | Cash funding moved to T006. |
| A3 | plan / T003 | Sell fee policy has to be registered in both `TradingPolicy` classes. The plan called `TradingFinancialState.cs` unchanged. | Plan structure corrected. T003 names both files. |
| A4 | data-model / T007, Constitution IX | Adding the liquidity guard to F02 could accidentally gate top-ups. | Capture never rejects on liquidity. T007 adds a test. |
| A5 | spec edge cases / T018, T022 | Tampered snapshot, unresolved status and unavailable balance had no test. | Added to T018 and T022. |
| A6 | AC-07, AC-08 / T018 | Reserved/restricted gold and suspense credit can't be created by any flow yet. | Recorded as NotRun with reason. Structural guarantee: only the available bucket is read. |

Rerun: every AC-01 to AC-13 has a task and verification path. Dependency order is valid. No conflict with Constitution I–XIII or service ownership remains. Ready for Implement.
