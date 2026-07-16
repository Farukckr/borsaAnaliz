# AI Agent Workflow

A small workflow for using one model as the planner and Codex as the implementer.

## Roles

- Planner model: Claude/Fable or another high-reasoning model. It writes the plan only.
- Implementer model: Codex. It edits files, runs checks, and reports the result.

## Quick Start

1. Copy `AGENTS.md` and the `.agents` folder into the root of a project.
2. Ask the planner model:

```text
Read .agents/CLAUDE_PLANNER_PROMPT.md.
My request is: <describe the site/app/feature>.
Do not code. Write or update .agents/PLAN.md only.
```

3. Skim `.agents/PLAN.md` yourself. If the goal, scope, or acceptance criteria look wrong, tell the planner to fix them now — it is much cheaper than fixing the implementation later.
4. Commit (or back up) the project before implementation so any bad run is easy to undo.
5. Ask Codex:

```text
Read .agents/CODEX_IMPLEMENTER_PROMPT.md and .agents/PLAN.md.
Implement the plan.
```

## If Codex Gets Blocked

Codex sets the plan's Status to `blocked` and writes what went wrong into the plan's Implementation Report. Send the planner back in:

```text
Read .agents/CLAUDE_PLANNER_PROMPT.md and .agents/PLAN.md.
Codex is blocked. Read the Implementation Report and revise the plan.
```

Then ask Codex to implement again. Repeat until Status is `done`.

## Plan Lifecycle

`placeholder -> ready (planner) -> in-progress (Codex) -> done | blocked (Codex)`

Codex only implements plans whose Status is `ready` or `in-progress`, which prevents re-running stale or unfinished plans by accident.

## When To Use The Full Workflow

Use planner -> Codex for:

- New websites, apps, dashboards, or larger UI changes.
- Multi-file features.
- Refactors.
- Deploy or infrastructure work.
- Work where scope control matters.

Use Codex directly for:

- Tiny copy edits.
- Color/text/link changes.
- One-file fixes.
- Obvious bug fixes with clear expected behavior.

## Rule Of Thumb

If the task needs design decisions, architecture decisions, or more than 15 minutes of coding, plan first.
If the task is obvious, use Codex directly.

---

# Türkçe

İki yapay zekâyı iş bölümüyle kullanan küçük bir workflow: güçlü bir model (Claude/Fable) sadece **plan yazar**, Codex sadece **kodu yazar**. Aradaki köprü `.agents/PLAN.md` dosyasıdır — planner oraya yazar, Codex oradan okur.

## Roller

- Planner model: Claude/Fable veya başka bir güçlü muhakeme modeli. Sadece planı yazar.
- Implementer model: Codex. Dosyaları düzenler, kontrolleri çalıştırır, sonucu raporlar.

## Hızlı Başlangıç

1. `AGENTS.md` dosyasını ve `.agents` klasörünü projenin kök dizinine kopyala.
2. Planner modele şunu yapıştır:

```text
Read .agents/CLAUDE_PLANNER_PROMPT.md.
My request is: <istediğin site/uygulama/özelliği buraya yaz>.
Do not code. Write or update .agents/PLAN.md only.
```

3. `.agents/PLAN.md` dosyasına kendin göz at. Hedef, kapsam veya kabul kriterleri yanlışsa planner'a hemen düzelttir — plandaki hatayı düzeltmek, koddaki hatayı düzeltmekten çok daha ucuzdur.
4. Implementasyondan önce commit al (veya yedekle) ki kötü giden bir koşu kolayca geri alınabilsin.
5. Codex'e şunu yapıştır:

```text
Read .agents/CODEX_IMPLEMENTER_PROMPT.md and .agents/PLAN.md.
Implement the plan.
```

## Codex Takılırsa

Codex, planın Status alanını `blocked` yapar ve neyin ters gittiğini planın Implementation Report bölümüne yazar. Planner'ı tekrar devreye sok:

```text
Read .agents/CLAUDE_PLANNER_PROMPT.md and .agents/PLAN.md.
Codex is blocked. Read the Implementation Report and revise the plan.
```

Sonra Codex'e tekrar uygulamasını söyle. Status `done` olana kadar bu döngüyü tekrarla.

## Plan Yaşam Döngüsü

`placeholder -> ready (planner) -> in-progress (Codex) -> done | blocked (Codex)`

Codex yalnızca Status'u `ready` veya `in-progress` olan planları uygular; böylece bayat veya bitmemiş bir plan yanlışlıkla yeniden çalıştırılamaz.

## Tam Workflow Ne Zaman Kullanılır?

Planner -> Codex şunlar için:

- Yeni web sitesi, uygulama, dashboard veya büyük arayüz değişiklikleri.
- Çok dosyalı özellikler.
- Refactor işleri.
- Deploy veya altyapı işleri.
- Kapsam kontrolünün önemli olduğu işler.

Doğrudan Codex şunlar için:

- Ufak metin düzeltmeleri.
- Renk/yazı/link değişiklikleri.
- Tek dosyalık düzeltmeler.
- Beklenen davranışı net olan bariz hata düzeltmeleri.

## Pratik Kural

İş tasarım kararı, mimari kararı veya 15 dakikadan fazla kodlama gerektiriyorsa önce plan yaptır.
İş barizse doğrudan Codex'i kullan.
