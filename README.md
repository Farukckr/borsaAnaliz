# Borsa Analiz

Borsa Analiz; BIST ve ABD hisselerini takip etmek, teknik göstergeleri incelemek, Gemini destekli grafik yorumları almak ve sanal portföy yönetmek için geliştirilmiş Türkçe bir finans uygulamasıdır.

> Canlı uygulama: [borsa-analiz-aqr9.onrender.com](https://borsa-analiz-aqr9.onrender.com)

Uygulama eğitim ve analiz amaçlıdır. Gösterilen veriler gecikmeli olabilir; hiçbir içerik yatırım tavsiyesi değildir.

## Özellikler

### Piyasa ve hisse analizi

- 100 BIST ve 50 ABD hissesi içeren 150 sembollük katalog
- Sembol arama, piyasa filtresi ve fiyat/değişim sıralaması
- Ana sayfada piyasa göstergeleri ile en çok yükselen ve düşen hisseler
- Yahoo Finance üzerinden fiyat, günlük değişim ve OHLCV geçmişi
- Mum, hacim, RSI, MACD, SMA, EMA ve Bollinger grafikleri
- TradingView gelişmiş grafik ve teknik analiz bileşenleri

### AI grafik yorumu

- `gemini-3.5-flash` ile son 60 günlük OHLC verisi ve teknik göstergelere dayalı Türkçe yorum
- Özet, trend, göstergeler, destek/direnç ve riskler bölümlerinden oluşan sabit çıktı yapısı
- Eksik veya token sınırına ulaşmış yanıtların kullanıcıya yarım analiz olarak gösterilmesini engelleyen doğrulamalar
- Kullanıcı başına 30 saniyelik istek bekleme süresi ve başarılı yorumlar için 5 dakikalık önbellek
- AI yorumları yalnızca oturum açmış kullanıcılar tarafından oluşturulabilir

### Sanal portföy

- ASP.NET Core Identity ile kayıt ve oturum yönetimi
- Kullanıcıya özel birden fazla sanal portföy
- Güncel fiyatla alış/satış önizlemesi ve bakiye/pozisyon doğrulaması
- Ortalama maliyet yöntemiyle maliyet, gerçekleşen ve gerçekleşmemiş kâr/zarar hesabı
- Günlük değişim, pozisyon ağırlıkları, nakit oranı ve portföy dağılım grafiği
- İşlem defteri ve hisse bazında açılabilir işlem geçmişi
- İşlemler ve günlük kapanışlardan yeniden oluşturulan, en fazla bir yıllık portföy değer grafiği

## Teknolojiler

| Katman | Teknoloji |
| --- | --- |
| Uygulama | ASP.NET Core 8 MVC, C# |
| Kimlik doğrulama | ASP.NET Core Identity |
| Veri erişimi | Entity Framework Core 8, Npgsql |
| Veritabanı | PostgreSQL / Supabase |
| Piyasa verisi | Yahoo Finance Chart API |
| AI | Google Gemini API (`gemini-3.5-flash`) |
| Arayüz | Razor Views, Bootstrap 5, özel CSS |
| Grafikler | Lightweight Charts, Chart.js, TradingView Widgets |
| Dağıtım | Docker, Render Blueprint |

## Gereksinimler

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- PostgreSQL 14+ veya bir Supabase projesi
- AI yorumlarını kullanmak için geçerli bir Gemini API anahtarı
- Yahoo Finance erişimi için ek API anahtarı gerekmez

## Yerel kurulum

1. Repoyu klonlayın ve dizine geçin:

   ```bash
   git clone https://github.com/Farukckr/borsaAnaliz.git
   cd borsaAnaliz
   ```

2. Bağımlılıkları geri yükleyin:

   ```bash
   dotnet restore BorsaAnaliz.sln
   ```

3. Gizli ayarları .NET user-secrets ile tanımlayın:

   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=<host>;Port=5432;Database=<database>;Username=<user>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true" --project src/BorsaAnaliz.Web
   dotnet user-secrets set "Ai:ApiKey" "<gemini-api-key>" --project src/BorsaAnaliz.Web
   ```

   Gerçek bağlantı dizesini veya API anahtarını `appsettings.json`, `.env`, commit mesajı ya da dokümana yazmayın. Kök dizindeki `.env` dosyası Git tarafından yok sayılır.

4. Uygulamayı derleyin ve çalıştırın:

   ```bash
   dotnet build BorsaAnaliz.sln --configuration Release
   dotnet run --project src/BorsaAnaliz.Web
   ```

5. Tarayıcıdan `http://localhost:5122` adresini açın.

Uygulama başlangıçta bekleyen Entity Framework migration'larını otomatik uygular. Kullanılan veritabanı hesabının şema oluşturma/değiştirme yetkisine sahip olması gerekir.

## Yapılandırma

| .NET anahtarı | Ortam değişkeni | Açıklama |
| --- | --- | --- |
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | Zorunlu PostgreSQL bağlantı dizesi |
| `Ai:ApiKey` | `Ai__ApiKey` | Gemini yorumları için API anahtarı |
| `Ai:Provider` | `Ai__Provider` | Varsayılan: `Gemini` |
| `Ai:Model` | `Ai__Model` | Varsayılan: `gemini-3.5-flash` |
| `ASPNETCORE_ENVIRONMENT` | `ASPNETCORE_ENVIRONMENT` | Yerelde `Development`, üretimde `Production` |

AI anahtarı tanımlı değilse piyasa, grafik ve portföy özellikleri çalışmaya devam eder; yalnızca AI yorum alanı yapılandırma uyarısı gösterir.

## Docker ile çalıştırma

İmajı oluşturun:

```bash
docker build -t borsa-analiz .
```

Git tarafından izlenmeyen bir `.env` dosyası hazırlayın:

```dotenv
PORT=8080
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Host=<host>;Port=5432;Database=<database>;Username=<user>;Password=<password>;SSL Mode=Require;Trust Server Certificate=true
Ai__ApiKey=<gemini-api-key>
```

Konteyneri başlatın:

```bash
docker run --rm --env-file .env -p 8080:8080 borsa-analiz
```

Uygulama `http://localhost:8080` adresinden erişilebilir olur.

## Render dağıtımı

Kök dizindeki [`render.yaml`](render.yaml), Docker tabanlı Render servisini şu ayarlarla tanımlar:

- Frankfurt bölgesi
- `/` health check
- `main` dalındaki commitlerde otomatik deploy
- Render tarafından sağlanan `PORT` değerine otomatik bağlanma
- PostgreSQL bağlantı dizesi ve Gemini anahtarının Render arayüzünden gizli değer olarak girilmesi

Ayrıntılı Blueprint ve manuel servis adımları için [`docs/DEPLOY.md`](docs/DEPLOY.md) dosyasına bakın.

## Önemli rotalar ve API'ler

| Rota | Erişim | Açıklama |
| --- | --- | --- |
| `/` | Herkese açık | Piyasa özeti ve hareketli hisseler |
| `/Stocks` | Herkese açık | Hisse kataloğu |
| `/Stocks/Details/{symbol}` | Herkese açık | Grafikler, göstergeler ve AI kartı |
| `/api/stocks/{symbol}/history` | Herkese açık | OHLCV geçmişi |
| `/api/stocks/{symbol}/indicators` | Herkese açık | Teknik gösterge serileri |
| `/api/stocks/{symbol}/ai-comment` | Oturum gerekli | Gemini teknik analiz yorumu |
| `/Portfolio` | Oturum gerekli | Kullanıcının sanal portföyleri |
| `/api/portfolios/{portfolioId}/trade-preview` | Sahibi | Alış/satış önizlemesi |
| `/api/portfolios/{id}/value-history` | Sahibi | Günlük portföy değer serisi |

## Proje yapısı

```text
borsaAnaliz/
├── src/BorsaAnaliz.Web/
│   ├── Controllers/       MVC ve JSON API uçları
│   ├── Data/              DbContext, migration'lar ve sembol kataloğu
│   ├── Models/            Piyasa, portföy ve API modelleri
│   ├── Services/          Yahoo, Gemini, göstergeler ve portföy hesapları
│   ├── ViewModels/        Razor sayfa modelleri
│   ├── Views/             Türkçe MVC arayüzü
│   └── wwwroot/           CSS, JavaScript ve istemci kütüphaneleri
├── docs/DEPLOY.md         Render dağıtım kılavuzu
├── Dockerfile
├── render.yaml
└── BorsaAnaliz.sln
```

## Doğrulama

Temel kalite kontrolü:

```bash
dotnet build BorsaAnaliz.sln --configuration Release --no-restore
```

Manuel smoke test sırasında aşağıdaki akışlar kontrol edilmelidir:

1. Ana sayfa ve hisse listesi yükleniyor.
2. BIST ve ABD hisse detaylarında grafik/göstergeler veri gösteriyor.
3. Kullanıcı kaydı ve oturum açma çalışıyor.
4. Portföy oluşturma, alış, kısmi satış ve işlem önizlemesi çalışıyor.
5. Dağılım ve portföy değer grafikleri oluşuyor.
6. AI yorumu beş bölümü eksiksiz üretip `Bu bir yatırım tavsiyesi değildir.` cümlesiyle bitiyor.

Repoda şu anda ayrı bir otomatik test projesi bulunmamaktadır; Release derlemesi ve uygulama seviyesindeki smoke testler doğrulamanın temelini oluşturur.

## Veri ve güvenlik notları

- Portföy ve işlem sorguları oturum açmış kullanıcı kimliğiyle sınırlandırılır.
- AI ve işlem POST uçlarında anti-forgery doğrulaması uygulanır.
- Data Protection anahtarları PostgreSQL'de saklanır; yeni deploy sonrasında mevcut oturumlar korunabilir.
- Yahoo fiyatları 60 saniye, geçmiş veriler ve portföy değer serileri 1 saat önbellekte tutulur.
- Harici piyasa verileri gecikmeli, eksik veya geçici olarak erişilemez olabilir.
- AI çıktıları yalnızca sağlanan OHLC ve teknik gösterge verilerine dayanır; haber veya temel analiz kaynağı değildir.

## Yasal uyarı

Bu proje yatırım danışmanlığı hizmeti sunmaz. Uygulamadaki fiyatlar, göstergeler, AI yorumları ve sanal portföy sonuçları gerçek yatırım kararlarının tek dayanağı olarak kullanılmamalıdır.
