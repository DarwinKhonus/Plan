# Plan

Desktopová aplikace pro plánování termínů zakázek. Odpovídá na otázku „kdy stihnu tuhle
poptávku?" a vizuálně ukáže, jestli se termíny nepřekrývají.

Je to plánovací nástroj pro jednoho člověka, ne účetní systém — neřeší fakturaci,
kontakty ani obchodní historii.

## Co umí

- **Zakázky** — název a termín od–do, přidání, úprava, smazání
- **Časová osa** — řádek na zakázku, tažením pruhu se termín posune, tažením okraje se
  protáhne jen začátek nebo konec; změna se ukládá hned
- **Rozdělení na úseky** — zakázku lze pravým tlačítkem rozdělit na víc částí a mezi ně
  vložit pauzu; pauza se nepočítá do hodin a neblokuje jiné zakázky
- **Milníky** — jednodenní značka v řádku zakázky (předání, dodání materiálu); najetím myší
  se zobrazí popis, kliknutím se milník upraví nebo smaže
- **Řazení zakázek** — automaticky podle termínu, nebo ručně: po vypnutí volby v nastavení
  lze zakázky v ose přetahovat svisle mezi sebou
- **Info** — přehled o zakázce pod pravým tlačítkem nebo F1
- **Detekce kolizí** — překrývající se zakázky se zvýrazní v ose i v tabulce a přepočítají
  se při každé změně; překryv pouze v nepracovní dny se za konflikt nepovažuje
- **Pracovní doba** — nastavitelné pracovní dny, časový rozsah a zohlednění českých
  státních svátků; z toho se u každé zakázky dopočítá informativní odhad hodin
- **Tabulkový výpis** — všechny zakázky seřazené podle termínu jako alternativa ke kalendáři
- **Aktualizace přímo z aplikace** — při startu se na pozadí ověří, jestli nevyšla novější
  verze; tlačítkem se instalátor stáhne, ověří proti kontrolnímu součtu a spustí

Data jsou v jediném souboru `%AppData%\Plan\plan.db` (SQLite). Aplikace nepotřebuje
server ani připojení k internetu; kontrola aktualizací je jediné, co jde ven, a když
selže, tiše se ignoruje.

## Instalace

Ke každé verzi na stránce [Releases](https://github.com/DarwinKhonus/Plan/releases/latest)
jsou dva soubory:

| Soubor | Kdy ho použít |
| --- | --- |
| `Plan-X.Y.Z-setup.exe` | **Doporučeně.** Instalátor — vytvoří zástupce a zaregistruje odinstalaci. |
| `Plan-X.Y.Z-win-x64.exe` | Přenosná varianta, když nechcete nic instalovat. Stačí spustit. |

Instalátor se ptá jen na to, jestli chcete zástupce na ploše. Instaluje se **pouze pro
přihlášeného uživatele** do `%LocalAppData%\Programs\Plan`, takže nevyžaduje práva správce
ani nezobrazí výzvu UAC.

### Aktualizace

Když vyjde novější verze, aplikace to při startu pozná a nabídne pruh s tlačítkem
**Stáhnout a nainstalovat**. Instalátor se stáhne, ověří proti kontrolnímu součtu
z GitHub API a spustí se v tichém režimu; aplikace se přitom sama zavře. Ruční
stahování z prohlížeče tedy potřeba není — odkaz na stránku zůstává jen jako záloha.

Ruční cesta funguje pořád stejně: stáhněte nový `setup.exe` a spusťte ho přes stávající
instalaci — nic není potřeba odinstalovávat. Aplikace přistane ve stejné složce pod stejným názvem, takže **zástupce
na ploše zůstane na své pozici**. (Kdybyste místo instalátoru dávali přímo `.exe` s číslem
verze na plochu, každá verze by se objevila jako nová ikona na první volné pozici —
Windows si pozice pamatuje podle názvu souboru.)

Pokud aplikace během aktualizace běží, instalátor nabídne její zavření.

### Data

Databáze žije v `%AppData%\Plan\plan.db` mimo instalační složku. Aktualizace ani
odinstalace o ni nepřijde; případné změny schématu se aplikují migracemi při prvním
spuštění nové verze. Kdo chce smazat i data, smaže složku `%AppData%\Plan` ručně.

## Sestavení

Potřebujete [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) nebo novější
(novější SDK umí `net8.0-windows` cílit také) a Visual Studio 2022, případně jen CLI.

```bash
dotnet restore Plan.sln
dotnet build Plan.sln
dotnet run --project Plan/Plan.csproj
```

Ve Visual Studiu stačí otevřít `Plan.sln` a spustit projekt `Plan`.

Testy:

```bash
dotnet test Plan.Tests/Plan.Tests.csproj
```

## Struktura

| Projekt | Obsah |
| --- | --- |
| `Plan` | WPF aplikace — okna, ViewModely, ovládací prvek časové osy, kontrola aktualizací |
| `Plan.Data` | EF Core kontext, migrace, repozitáře a doménová logika (kolize, pracovní kalendář, svátky) |
| `Plan.Tests` | xUnit testy doménové logiky a validací |
| `installer` | Skript instalátoru pro Inno Setup 6 |

Doménová logika záměrně žije v `Plan.Data` bez vazby na WPF, aby šla testovat bez UI.

## Databáze a migrace

Schéma se verzuje přes EF Core Migrations. Při startu aplikace se čekající migrace
aplikují automaticky, takže **aktualizace aplikace nepřijde o existující data**.

Nová migrace po změně modelu:

```bash
dotnet tool restore
dotnet ef migrations add NazevZmeny --project Plan.Data --startup-project Plan.Data
```

## Vydání nové verze

Verze se řídí gitovými tagy v sémantickém formátu `vX.Y.Z`. Po pushnutí tagu se
[workflow Release](.github/workflows/release.yml) postará o zbytek:

```bash
git tag v1.0.0
git push origin v1.0.0
```

Workflow sestaví Release build, spustí testy, vytvoří self-contained single-file `.exe`
pro win-x64, zabalí ho instalátorem a obojí přiloží jako assety k GitHub Release. Číslo
verze se z tagu propíše do `InformationalVersion` sestavení — právě podle něj pak běžící
aplikace pozná, že vyšla novější verze.

Instalátor jde sestavit i lokálně, když máte [Inno Setup 6](https://jrsoftware.org/isdl.php):

```bash
dotnet publish Plan/Plan.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

```bash
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DAppVersion=1.0.0 /DSourceExe=..\publish\Plan.exe installer\Plan.iss
```

## Rozhodnutí, která stojí za zmínku

- **EF Core místo Dapperu** — kvůli migracím; schéma se dá měnit bez ztráty dat uživatele.
- **Nastavení v tabulce, ne v JSON souboru** — jeden soubor k záloze a jeden zdroj pravdy.
- **`DateOnly` místo `DateTime`** — termíny jsou v denní granularitě a časová složka by do
  detekce kolizí tahala chyby typu „23:59 vs 00:00".
- **Dotyk termínů se počítá jako kolize** — když jedna zakázka končí 10. 3. a druhá 10. 3.
  začíná, je to týž pracovní den. Pokud ale společný den vyjde na víkend nebo svátek,
  konflikt to není; termín je souvislý interval, ale pracuje se jen v pracovních dnech.
- **Zobrazení nepracovních dnů v pruhu je volitelné** — spojovací čára, bílé pruhy, nebo
  jen obrys. Žádná z variant není zjevně nejlepší: čára je nejčitelnější, ale vypadá stejně
  jako zakázka rozdělená na úseky; proto je to přepínač, ne pevné rozhodnutí.
- **Časová osa se kreslí ručně** místo skládání z WPF prvků — při stovkách dnů × zakázek
  by byl vizuální strom zbytečně těžký a tažení okrajů by se stejně muselo psát ručně.
- **Termín zakázky je tabulka úseků**, ne dvojice sloupců — jinak by nešlo vložit pauzu.
  Celkový rozsah `DatumOd`/`DatumDo` se dopočítává z úseků, aby neexistovaly dva zdroje pravdy.
- **Překrývající se úseky jedné zakázky se slijí**, jen dotýkající se ne — jinak by se
  rozdělení zakázky okamžitě samo vrátilo zpátky.
- **Ruční řazení se v ose pozná podle směru tažení** — svisle pořadí, vodorovně termín.
  Rozhoduje se po prvních pixelech, takže drobné chvění ruky režim nepřehodí.
- **Popisky dnů se při oddálení ředí po týdnech**, ne po desítkách dnů: měsíce mají různou
  délku, takže 1–10–20–30 by se na přelomu měsíce srazilo.
- **Self-contained build** — uživatel nemusí řešit instalaci .NET runtime.
