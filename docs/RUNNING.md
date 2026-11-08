# راهنمای اجرای Avand

این راهنما برای کسی است که می‌خواهد Avand را روی سیستم خودش بسازد و اجرا کند و درستیِ رفتار آن را ببیند.

## Avand چیست و چطور اجرا می‌شود

Avand بک‌اندِ خرید و فروش طلا با .NET 10 است و دو سرویس دارد:

| سرویس | پروژه | نقش |
| --- | --- | --- |
| Application API | `src/Avand.Application.Api` | رابط مشتری: شارژ کیف پول، قیمت‌دهی، ثبت سفارش خرید و فروش، پیگیری و بازیابی |
| Ledger API | `src/Avand.Ledger.Api` | تصمیم و ثبت مالی: دفتر روزنامه، موجودی‌ها، نقدینگی و پشتوانهٔ طلا |

هر سرویس پایگاه‌دادهٔ PostgreSQL جداگانه و نقش (role) پایگاه‌دادهٔ جداگانه دارد. دو سرویس فقط از راه HTTP با هم حرف می‌زنند.

**نکتهٔ مهم:** در این نسخه، سرویس‌ها به‌تنهایی و با `dotnet run` اجرا نمی‌شوند. نسخهٔ فعلی این موارد را ندارد:

- اجرای خودکار migration هنگام شروع
- ساخت نقش‌های پایگاه‌داده
- مشتری نمونه
- درگاه پرداخت واقعی (فقط درگاه شبیه‌سازی‌شده وجود دارد)
- فایل تنظیمات آماده

همهٔ این‌ها را مجموعهٔ تست یکپارچه (integration tests) آماده می‌کند. هر کلاس تست این مراحل را انجام می‌دهد:

1. یک PostgreSQL 17.6 تازه بالا می‌آورد.
2. دو پایگاه‌داده و دو نقش جدا می‌سازد و migrationها را اجرا می‌کند.
3. هر دو سرویس واقعی را روی پورت‌های محلی اجرا می‌کند.
4. سناریوهای مشتری را از طریق HTTP اجرا می‌کند.

پس روشِ اجرای برنامه در این مرحله، اجرای همین تست‌هاست.

## پیش‌نیازها

| مورد | نسخه | بررسی |
| --- | --- | --- |
| .NET SDK | 10.0 (مثلاً 10.0.403) | `dotnet --version` |
| Docker Desktop (یا هر موتور سازگار با Docker) | Engine 27.x | `docker version --format "{{.Server.Version}}"` |
| Git | هر نسخهٔ جدید | `git --version` |

- Docker باید در حال اجرا باشد. تست‌ها تصویر `postgres:17.6` را خودکار دریافت می‌کنند، پس دفعهٔ اول اینترنت لازم است.
- روی ویندوز، Docker Desktop باید روی WSL 2 و حالت Linux containers باشد.
- اگر Docker ندارید، می‌توانید از یک PostgreSQL 17 یک‌بارمصرف استفاده کنید. بخش «بدون Docker» را ببینید.

## مراحل اجرا

### ۱. دریافت کد

```bash
git clone <آدرس مخزن> AvandTask
```

```bash
cd AvandTask
```

### ۲. ساخت (build)

```bash
dotnet build Avand.slnx --ignore-failed-sources
```

نتیجهٔ درست: `Build succeeded` با `0 Error(s)`.

### ۳. اجرای همهٔ سناریوها (حدود ۸ تا ۱۰ دقیقه)

```bash
dotnet test tests/Avand.V01.IntegrationTests/Avand.V01.IntegrationTests.csproj --no-build
```

- نتیجهٔ درست: `Passed!` و `Failed: 0`. در آخرین اجرا ۱۱۲ تست بود.
- کلاس‌های تست عمداً یکی‌یکی اجرا می‌شوند و هر کدام کانتینر PostgreSQL خودش را دارد. برای همین اجرا طول می‌کشد.

### ۴. اجرای یک بخش مشخص (سریع‌تر)

| می‌خواهید ببینید | دستور |
| --- | --- |
| فقط فروش طلا (V03) | `dotnet test tests/Avand.V01.IntegrationTests/Avand.V01.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~V03"` |
| فقط خرید طلا (V02) | `dotnet test tests/Avand.V01.IntegrationTests/Avand.V01.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~V02"` |
| سفر کامل مشتری (شارژ ← خرید ← فروش ← خرید دوباره با پول فروش) | `dotnet test tests/Avand.V01.IntegrationTests/Avand.V01.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~V03SellJourneyTests"` |
| فقط محاسبات قیمت و کارمزد (بدون Docker، چند ثانیه) | `dotnet test tests/Avand.V01.IntegrationTests/Avand.V01.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~QuoteMathTests"` |

### ۵. دیدن جزئیات و لاگ سرویس‌ها

برای ذخیرهٔ نتیجه همراه با لاگ هر دو سرویس:

```bash
dotnet test tests/Avand.V01.IntegrationTests/Avand.V01.IntegrationTests.csproj --no-build --logger "trx;LogFileName=run.trx"
```

فایل خروجی در `tests/Avand.V01.IntegrationTests/TestResults/run.trx` ذخیره می‌شود و در Visual Studio یا Rider باز می‌شود.

## بدون Docker

یک PostgreSQL 17 خالی و یک‌بارمصرف آماده کنید. تست‌ها روی آن پایگاه‌داده و نقش می‌سازند و پاک می‌کنند، پس هرگز از سرور واقعی استفاده نکنید. بعد رشتهٔ اتصال کاربر superuser را در متغیر محیطی بگذارید:

```powershell
$env:AVAND_V01_TEST_POSTGRES = "Host=localhost;Port=5432;Username=postgres;Password=<رمز>;Database=postgres"
```

سپس مرحلهٔ ۳ را اجرا کنید.

## رفع اشکال

| نشانه | علت و راه‌حل |
| --- | --- |
| `DockerUnavailableException` یا `Docker API responded with status code='InternalServerError'` | موتور Docker آماده نیست. Docker Desktop را باز یا Restart کنید (`docker desktop restart`). صبر کنید تا `docker version` نسخهٔ Server را نشان دهد، بعد تست را تکرار کنید. |
| همهٔ تست‌ها در چند ثانیهٔ اول شکست می‌خورند | تقریباً همیشه مشکل Docker است، نه کد. ردیف بالا را ببینید. |
| `error` در build دربارهٔ قفل بودن فایل | یک build یا تست دیگر هم‌زمان در حال اجراست. صبر کنید و دوباره build کنید. |
| اجرای اول خیلی کند است | دریافت تصویر `postgres:17.6` و بستههای NuGet. اجراهای بعدی سریع‌ترند. |

## برای مطالعهٔ بیشتر

- اصول و قواعد پروژه: `AGENTS.md` و `.specify/memory/constitution.md`
- مشخصات هر فیچر: `specs/001-fund-irr-wallet`، `specs/002-buy-gold-wallet`، `specs/003-sell-gold-wallet` (فایل‌های `spec.md`، `quickstart.md` و `contracts/`)
- قراردادهای مالی: `docs/FINANCIAL_OPERATIONS.md`
- شواهد آخرین اجراها: `harness/evidence/`

همهٔ داده‌های تست ساختگی‌اند: مشتری، قیمت، درگاه و موجودی. هیچ اطلاعات یا اعتبارنامهٔ واقعی لازم نیست و نباید استفاده شود.
