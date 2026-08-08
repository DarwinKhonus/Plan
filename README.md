# Plan

Desktopová aplikace pro plánování termínů zakázek. Odpovídá na otázku „kdy stihnu tuhle
poptávku?" a vizuálně ukáže, jestli se termíny nepřekrývají.

Je to plánovací nástroj pro jednoho člověka, ne účetní systém — neřeší fakturaci,
kontakty ani obchodní historii.

## Co umí

- **Zakázky** — název a termín od–do, přidání, úprava, smazání
- **Časová osa** — řádek na zakázku, tažením pruhu se termín posune, tažením okraje se
  protáhne jen začátek nebo konec; změna se ukládá hned
- **Detekce kolizí** — překrývající se zakázky se zvýrazní v ose i v tabulce a přepočítají
  se při každé změně
- **Pracovní doba** — nastavitelné pracovní dny, časový rozsah a zohlednění českých
  státních svátků; z toho se u každé zakázky dopočítá informativní odhad hodin
- **Tabulkový výpis** — všechny zakázky seřazené podle termínu jako alternativa ke kalendáři
- **Kontrola aktualizací** — při startu se na pozadí ověří, jestli nevyšla novější verze

Data jsou v jediném souboru `%AppData%\Plan\plan.db` (SQLite). Aplikace nepotřebuje
server ani připojení k internetu; kontrola aktualizací je jediné, co jde ven, a když
selže, tiše se ignoruje.

## Stažení

Nejnovější `.exe` je na stránce [Releases](https://github.com/DarwinKhonus/Plan/releases/latest).
Soubor je self-contained — stáhnete, spustíte, nic dalšího se neinstaluje.

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
pro win-x64 a přiloží ho jako asset k GitHub Release. Číslo verze se z tagu propíše do
`InformationalVersion` sestavení — právě podle něj pak běžící aplikace pozná, že vyšla
novější verze.

## Rozhodnutí, která stojí za zmínku

- **EF Core místo Dapperu** — kvůli migracím; schéma se dá měnit bez ztráty dat uživatele.
- **Nastavení v tabulce, ne v JSON souboru** — jeden soubor k záloze a jeden zdroj pravdy.
- **`DateOnly` místo `DateTime`** — termíny jsou v denní granularitě a časová složka by do
  detekce kolizí tahala chyby typu „23:59 vs 00:00".
- **Dotyk termínů se počítá jako kolize** — když jedna zakázka končí 10. 3. a druhá 10. 3.
  začíná, je to týž pracovní den.
- **Časová osa se kreslí ručně** místo skládání z WPF prvků — při stovkách dnů × zakázek
  by byl vizuální strom zbytečně těžký a tažení okrajů by se stejně muselo psát ručně.
- **Self-contained build** — uživatel nemusí řešit instalaci .NET runtime.
