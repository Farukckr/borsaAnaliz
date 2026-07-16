# Render'a Dağıtım

Bu proje, GitHub'daki özel `Farukckr/borsaAnaliz` reposundan Docker ile Render'a dağıtılır. Uygulama verileri Supabase Postgres'te tutulur; Render üzerinde ayrıca bir veritabanı veya kalıcı disk oluşturulmaz.

## Başlamadan önce

- Render hesabınızla giriş yapın ve GitHub hesabınızı bağlayın.
- Render'ın özel `Farukckr/borsaAnaliz` reposuna erişmesine izin verin.
- Supabase **Session pooler** bağlantı dizesini hazır edin. Port `5432` olmalıdır.
- Daha önce sohbette veya başka bir açık ortamda paylaşılmış AI anahtarını iptal edin ve yeni bir Gemini API anahtarı oluşturun. Eski anahtarı üretimde tekrar kullanmayın.
- Gerçek bağlantı dizesini veya API anahtarını bu repoya, `render.yaml` dosyasına ya da bir commit mesajına yazmayın.

## Seçenek A — Blueprint ile kurulum

Repo kökündeki `render.yaml`, servis ayarlarını hazırlar ve gizli değerleri Render arayüzünde sorar.

1. Render Dashboard'da **New → Blueprint** seçin.
2. GitHub kaynağı olarak `Farukckr/borsaAnaliz` reposunu bağlayın.
3. Blueprint dosyası olarak repo kökündeki `render.yaml` dosyasını kullanın.
4. Render istediğinde aşağıdaki iki gizli değeri girin:

   | Anahtar | Değer |
   | --- | --- |
   | `ConnectionStrings__DefaultConnection` | `<Supabase Session pooler bağlantı dizesi>` |
   | `Ai__ApiKey` | `<yeni Gemini API anahtarı>` |

5. Değişiklikleri uygulayıp ilk deploy'u başlatın.

`ASPNETCORE_ENVIRONMENT=Production`, Frankfurt bölgesi, ücretsiz instance, Docker runtime, `/` health check ve `main` commitlerinde otomatik deploy Blueprint içinde tanımlıdır.

## Seçenek B — Web Service'i elle kurma

1. Render Dashboard'da **New → Web Service** seçin.
2. **Git Provider** kaynağından `Farukckr/borsaAnaliz` reposunu seçin.
3. Servis ayarlarını şu şekilde doldurun:

   | Ayar | Değer |
   | --- | --- |
   | Name | `borsa-analiz` veya kullanılabilir benzer bir ad |
   | Region | `Frankfurt (EU Central)` |
   | Branch | `main` |
   | Runtime / Language | `Docker` |
   | Root Directory | boş bırakın |
   | Dockerfile Path | `./Dockerfile` |
   | Docker Build Context | `.` |
   | Instance Type | `Free` |
   | Health Check Path | `/` |
   | Auto-Deploy | her committe açık |

4. **Environment** bölümüne aşağıdaki değişkenleri ekleyin:

   | Anahtar | Değer |
   | --- | --- |
   | `ConnectionStrings__DefaultConnection` | `<Supabase Session pooler bağlantı dizesi>` |
   | `Ai__ApiKey` | `<yeni Gemini API anahtarı>` |
   | `ASPNETCORE_ENVIRONMENT` | `Production` |

5. `PORT` değişkenini elle eklemeyin. Render bunu sağlar; Docker giriş noktası uygulamayı otomatik olarak `0.0.0.0:$PORT` adresine bağlar.
6. **Create Web Service** ile ilk build ve deploy'u başlatın.

## İlk deploy'u kontrol etme

Render'ın **Events** ve **Logs** ekranlarında build'in tamamlanmasını bekleyin. Uygulama açılırken EF Core bekleyen migration'ları Supabase'e uygular. Başarılı deploy sonrasında Render, `https://<servis-adı>.onrender.com` biçiminde bir adres gösterir.

Aşağıdaki kontrolleri sırayla yapın:

1. `/` ana sayfası HTTPS üzerinden açılıyor ve HTTP 200 dönüyor.
2. `/Stocks` sayfası hisse listesini yüklüyor.
3. Yeni bir test hesabı kaydedilebiliyor ve oturum açılabiliyor.
4. Bir portföy oluşturulup örnek bir alış ve kısmi satış yapılabiliyor.
5. Bir hisse detayında grafik ve göstergeler yükleniyor.
6. **AI Yorumu** düğmesi yapılandırma hatası vermeden yanıt üretiyor.
7. Render'dan yeni bir deploy başlattıktan sonra mevcut oturum hâlâ geçerli kalıyor. Bu kontrol, Data Protection anahtarlarının Supabase'te kalıcı olduğunu doğrular.

Test için oluşturduğunuz kullanıcı, portföy ve işlemleri doğrulama bittikten sonra temizleyin. Sonuçları ve Render URL'sini `.agents/PLAN.md` içindeki Phase 4 uygulama raporuna ekleyin; gizli değerleri rapora yazmayın.

## Ücretsiz plan notları

- Render Free web servisleri 15 dakika HTTP/WebSocket trafiği almazsa uykuya geçer. Sonraki ilk istek yaklaşık 30–60 saniye sürebilir.
- Supabase Free projeleri uzun süre kullanılmadığında duraklatılabilir; bu durumda Supabase Dashboard'dan projeyi yeniden etkinleştirin.
- Render dosya sistemi kalıcı kabul edilmemelidir. Bu proje uygulama verisini ve Data Protection anahtarlarını Supabase'te tuttuğu için kalıcı diske ihtiyaç duymaz.

## Sık karşılaşılan sorunlar

- **Database authentication failed:** Render'daki `ConnectionStrings__DefaultConnection` değerini, Supabase Session pooler kullanıcı adını ve `5432` portunu kontrol edin.
- **Port scan / 502:** `PORT` değişkenini kaldırın ve Dockerfile'ın değiştirilmediğini doğrulayın.
- **AI yapılandırılmamış:** `Ai__ApiKey` adını ve yeni anahtarın etkin olduğunu kontrol edip **Save and deploy** seçin.
- **Repo listede görünmüyor:** Render GitHub uygulamasına özel repo erişimi verin ve bağlantıyı yenileyin.

## Resmî kaynaklar

- Render Web Services: https://render.com/docs/web-services
- Render Blueprint YAML: https://render.com/docs/blueprint-spec
- Render Environment Variables: https://render.com/docs/configure-environment-variables
- Render Free hizmetler: https://render.com/docs/free
