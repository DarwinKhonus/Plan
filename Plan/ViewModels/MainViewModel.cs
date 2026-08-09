using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Plan.Data;
using Plan.Data.Domain;
using Plan.Mvvm;
using Plan.Services;

namespace Plan.ViewModels;

public class MainViewModel : ObservableObject
{
    /// <summary>Kolik dnů se v ose zobrazí před první a za poslední zakázkou.</summary>
    private const int RezervaDnu = 14;

    private const int MinimalniRozsahDnu = 60;

    /// <summary>Mez přiblížení v pixelech na den. Platí pro tlačítka i pro Ctrl+kolečko.</summary>
    public const double MinimalniSirkaDne = 6;

    public const double MaximalniSirkaDne = 80;

    private readonly ZakazkyRepository _zakazkyRepository;
    private readonly NastaveniRepository _nastaveniRepository;
    private readonly UpdateChecker _updateChecker;
    private readonly UpdateDownloader _updateDownloader;

    private PracovniKalendar _kalendar = new(new PracovniNastaveni());
    private ZakazkaViewModel? _vybranaZakazka;
    private DateOnly _prvniDen = DateOnly.FromDateTime(DateTime.Today);
    private int _pocetDnu = MinimalniRozsahDnu;
    private double _sirkaDne = 26;
    private double _sirkaViditelneCasti;
    private RadekTabulky? _vybranyRadek;

    /// <summary>
    /// Zakázky s rozbaleným stromem milníků, podle Id — přeskládání řádků vytváří nové
    /// instance, takže stav rozbalení nemůže žít na nich. Prázdná množina = vše sbalené.
    /// </summary>
    private readonly HashSet<int> _rozbaleneZakazky = [];
    private string? _informaceOAktualizaci;
    private string? _urlAktualizace;
    private string? _chybaDatabaze;
    private DostupnaAktualizace? _aktualizace;
    private bool _stahujeSe;
    private double _pokrokStahovani;
    private string? _stavStahovani;

    /// <summary>Tlumí přepočty během hromadných změn kolekce (načtení, přeřazení).</summary>
    private bool _hromadnaZmena;

    public MainViewModel(
        ZakazkyRepository zakazkyRepository,
        NastaveniRepository nastaveniRepository,
        UpdateChecker updateChecker,
        UpdateDownloader updateDownloader)
    {
        _updateDownloader = updateDownloader;
        _zakazkyRepository = zakazkyRepository;
        _nastaveniRepository = nastaveniRepository;
        _updateChecker = updateChecker;

        InfoCommand = new RelayCommand(
            () => PozadavekNaInfo?.Invoke(this, EventArgs.Empty),
            () => VybranaZakazka is not null);
        PridatCommand = new RelayCommand(() => PozadavekNaPridani?.Invoke(this, EventArgs.Empty));
        UpravitCommand = new RelayCommand(
            () => PozadavekNaUpravu?.Invoke(this, EventArgs.Empty),
            () => VybranaZakazka is not null);
        SmazatCommand = new RelayCommand(
            () => PozadavekNaSmazani?.Invoke(this, EventArgs.Empty),
            () => VybranaZakazka is not null);
        NastaveniCommand = new RelayCommand(() => PozadavekNaNastaveni?.Invoke(this, EventArgs.Empty));
        PriblizitCommand = new RelayCommand(() => SirkaDne = Math.Min(SirkaDne + 6, MaximalniSirkaDne));
        OddalitCommand = new RelayCommand(() => SirkaDne = Math.Max(SirkaDne - 6, MinimalniSirkaDne));
        SkocitNaDnesekCommand = new RelayCommand(() => PozadavekNaSkokNaDnesek?.Invoke(this, EventArgs.Empty));
        StahnoutAktualizaciCommand = new RelayCommand(
            () => PozadavekNaStazeniAktualizace?.Invoke(this, EventArgs.Empty),
            () => LzeStahnoutAktualizaci);

        Zakazky.CollectionChanged += (_, _) =>
        {
            if (!_hromadnaZmena)
            {
                PrepocitejVse();
            }
        };
    }

    /// <summary>
    /// Události, na které reaguje okno otevřením dialogu. ViewModel tak nemusí znát
    /// žádný WPF typ a zůstává testovatelný.
    /// </summary>
    public event EventHandler? PozadavekNaInfo;

    public event EventHandler? PozadavekNaPridani;

    public event EventHandler? PozadavekNaUpravu;

    public event EventHandler? PozadavekNaSmazani;

    public event EventHandler? PozadavekNaNastaveni;

    public event EventHandler? PozadavekNaSkokNaDnesek;

    public event EventHandler? PozadavekNaStazeniAktualizace;

    public ObservableCollection<ZakazkaViewModel> Zakazky { get; } = [];

    /// <summary>
    /// Plochá podoba zakázek a jejich milníků pro tabulku. Milník je vždy hned pod svou
    /// zakázkou, takže tabulka čte jako strom, ale sloupce zůstanou zarovnané.
    /// </summary>
    public ObservableCollection<RadekTabulky> RadkyTabulky { get; } = [];

    /// <summary>
    /// Vybraný řádek tabulky. Výběr milníku vybere i jeho zakázku, aby na ni fungovaly
    /// příkazy z panelu i z kontextové nabídky.
    /// </summary>
    public RadekTabulky? VybranyRadek
    {
        get => _vybranyRadek;
        set
        {
            if (SetProperty(ref _vybranyRadek, value) && value is not null)
            {
                VybranaZakazka = value.Zakazka;
            }
        }
    }

    public ICommand InfoCommand { get; }

    public ICommand PridatCommand { get; }

    public ICommand UpravitCommand { get; }

    public ICommand SmazatCommand { get; }

    public ICommand NastaveniCommand { get; }

    public ICommand PriblizitCommand { get; }

    public ICommand OddalitCommand { get; }

    public ICommand SkocitNaDnesekCommand { get; }

    public PracovniKalendar Kalendar
    {
        get => _kalendar;
        private set => SetProperty(ref _kalendar, value);
    }

    public ZakazkaViewModel? VybranaZakazka
    {
        get => _vybranaZakazka;
        set
        {
            if (!SetProperty(ref _vybranaZakazka, value))
            {
                return;
            }

            // Výběr z časové osy musí posunout i výběr v tabulce — ale jen když
            // vybraný řádek patří jiné zakázce, aby vybraný milník nepřeskočil
            // na řádek své zakázky.
            if (_vybranyRadek?.Zakazka != value)
            {
                VybranyRadek = RadkyTabulky.FirstOrDefault(r => !r.JeMilnik && r.Zakazka == value);
            }
        }
    }

    public DateOnly PrvniDen
    {
        get => _prvniDen;
        private set => SetProperty(ref _prvniDen, value);
    }

    public int PocetDnu
    {
        get => _pocetDnu;
        private set => SetProperty(ref _pocetDnu, value);
    }

    public double SirkaDne
    {
        get => _sirkaDne;
        set
        {
            if (SetProperty(ref _sirkaDne, value))
            {
                // Při jiném přiblížení se do okna vejde jiný počet dnů.
                PrepocitejRozsahOsy();
            }
        }
    }

    /// <summary>
    /// Šířka viditelné části osy v pixelech. Okno ji hlásí při změně velikosti, aby osa
    /// mohla vyplnit celou plochu bez ohledu na to, kam sahají zakázky.
    /// </summary>
    public double SirkaViditelneCasti
    {
        get => _sirkaViditelneCasti;
        set
        {
            if (SetProperty(ref _sirkaViditelneCasti, value))
            {
                PrepocitejRozsahOsy();
            }
        }
    }

    public string? InformaceOAktualizaci
    {
        get => _informaceOAktualizaci;
        private set
        {
            if (SetProperty(ref _informaceOAktualizaci, value))
            {
                OnPropertyChanged(nameof(JeDostupnaAktualizace));
            }
        }
    }

    public string? UrlAktualizace
    {
        get => _urlAktualizace;
        private set => SetProperty(ref _urlAktualizace, value);
    }

    public bool JeDostupnaAktualizace => !string.IsNullOrEmpty(_informaceOAktualizaci);

    /// <summary>Nalezená aktualizace, dokud se nezačne stahovat.</summary>
    public DostupnaAktualizace? Aktualizace
    {
        get => _aktualizace;
        private set
        {
            if (SetProperty(ref _aktualizace, value))
            {
                OnPropertyChanged(nameof(LzeStahnoutAktualizaci));
                ObnovDostupnostPrikazu();
            }
        }
    }

    public bool LzeStahnoutAktualizaci => _aktualizace?.LzeStahnout == true && !_stahujeSe;

    /// <summary>
    /// Přinutí WPF znovu se zeptat příkazů, jestli jsou dostupné.
    /// </summary>
    /// <remarks>
    /// RelayCommand hlásí změnu dostupnosti přes CommandManager, který se ptá jen při
    /// vstupu uživatele nebo změně fokusu. Když se stav změní na pozadí — doběhlá kontrola
    /// aktualizací, znovunačtení dat — zůstalo by tlačítko šedé, dokud uživatel někam
    /// neklikne. Proto se dotaz vyvolá ručně.
    /// </remarks>
    private static void ObnovDostupnostPrikazu() => CommandManager.InvalidateRequerySuggested();

    public bool StahujeSe
    {
        get => _stahujeSe;
        private set
        {
            if (SetProperty(ref _stahujeSe, value))
            {
                OnPropertyChanged(nameof(LzeStahnoutAktualizaci));
                ObnovDostupnostPrikazu();
            }
        }
    }

    /// <summary>Průběh stahování v procentech.</summary>
    public double PokrokStahovani
    {
        get => _pokrokStahovani;
        private set => SetProperty(ref _pokrokStahovani, value);
    }

    public string? StavStahovani
    {
        get => _stavStahovani;
        private set => SetProperty(ref _stavStahovani, value);
    }

    public ICommand StahnoutAktualizaciCommand { get; }

    /// <summary>
    /// Stáhne a ověří instalátor. Vrátí cestu k souboru, který má okno spustit —
    /// spuštění procesu a ukončení aplikace ViewModel záměrně nedělá.
    /// </summary>
    public async Task<string?> StahniAktualizaciAsync()
    {
        if (Aktualizace is not { LzeStahnout: true } aktualizace)
        {
            return null;
        }

        StahujeSe = true;
        PokrokStahovani = 0;
        StavStahovani = "Stahuji…";

        try
        {
            var pokrok = new Progress<double>(podil => PokrokStahovani = podil * 100);
            var (vysledek, cesta, chyba) = await _updateDownloader.StahniAsync(aktualizace, pokrok);

            switch (vysledek)
            {
                case VysledekStazeni.Ok:
                    StavStahovani = "Spouštím instalaci…";
                    return cesta;

                case VysledekStazeni.NesouhlasiKontrolniSoucet:
                    StavStahovani = "Stažený soubor je poškozený, instalace se nespustí.";
                    return null;

                default:
                    StavStahovani = $"Stažení se nepovedlo: {chyba}";
                    return null;
            }
        }
        finally
        {
            StahujeSe = false;
        }
    }

    /// <summary>Popis potíže s databází, zobrazený jako pruh v okně. <c>null</c> = vše v pořádku.</summary>
    public string? ChybaDatabaze
    {
        get => _chybaDatabaze;
        private set
        {
            if (SetProperty(ref _chybaDatabaze, value))
            {
                OnPropertyChanged(nameof(MaChybuDatabaze));
            }
        }
    }

    public bool MaChybuDatabaze => !string.IsNullOrEmpty(_chybaDatabaze);

    /// <summary>
    /// Nastaví hlášku podle stavu databáze zjištěného při startu. Chyba se ukazuje
    /// jako pruh v okně, ne jako dialog — vedle ní tak zůstane vidět nabídka aktualizace,
    /// která je u staré verze nad novou databází přesně tím, co uživatel potřebuje.
    /// </summary>
    public void NastavStavDatabaze(StavDatabaze stav, string? podrobnosti)
    {
        ChybaDatabaze = stav switch
        {
            StavDatabaze.NovejsiNezAplikace =>
                "Databáze pochází z novější verze aplikace, než je tato. "
                + "Zakázky se proto nedají zobrazit — nainstalujte prosím aktuální verzi.",

            StavDatabaze.Nedostupna =>
                $"Databázi se nepodařilo otevřít: {podrobnosti}",

            _ => null,
        };
    }

    public void OhlasChybuNacteni(Exception vyjimka)
    {
        // Když už stav databáze hlásí konkrétnější příčinu, obecnou hlášku nepřepisujeme.
        ChybaDatabaze ??= $"Data se nepodařilo načíst: {vyjimka.Message}";
    }

    public string SouhrnStavu
    {
        get
        {
            var pocetKolizi = Zakazky.Count(z => z.MaKolizi);
            var zakladni = $"{Zakazky.Count} {SklonujZakazky(Zakazky.Count)}";

            return pocetKolizi == 0
                ? $"{zakladni} · bez konfliktů"
                : $"{zakladni} · {pocetKolizi} v konfliktu";
        }
    }

    /// <summary>České skloňování: 1 zakázka, 2–4 zakázky, 0 a 5+ zakázek.</summary>
    private static string SklonujZakazky(int pocet) => pocet switch
    {
        1 => "zakázka",
        >= 2 and <= 4 => "zakázky",
        _ => "zakázek",
    };

    public async Task NactiAsync()
    {
        Kalendar = new PracovniKalendar(await _nastaveniRepository.NactiAsync());

        var zakazky = await _zakazkyRepository.NactiVseAsync();
        var vybraneId = VybranaZakazka?.Id;

        // Repozitář vrací ruční pořadí; při automatickém řazení se přeskládá podle termínu.
        if (Kalendar.Nastaveni.AutomatickeRazeni)
        {
            zakazky = zakazky
                .OrderBy(z => z.DatumOd)
                .ThenBy(z => z.DatumDo)
                .ToList();
        }

        _hromadnaZmena = true;
        try
        {
            foreach (var zakazka in Zakazky)
            {
                zakazka.PropertyChanged -= NaZmenuZakazky;
            }

            Zakazky.Clear();
            foreach (var zakazka in zakazky)
            {
                var vm = new ZakazkaViewModel(zakazka);
                vm.PropertyChanged += NaZmenuZakazky;
                Zakazky.Add(vm);
            }
        }
        finally
        {
            _hromadnaZmena = false;
        }

        // Znovunačtení vymění instance ViewModelů, takže výběr je potřeba obnovit podle Id.
        VybranaZakazka = vybraneId is null ? null : Zakazky.FirstOrDefault(z => z.Id == vybraneId);

        PrepocitejVse();

        // Načtení běží asynchronně, takže se dostupnost tlačítek nad zakázkou
        // sama neobnoví — viz ObnovDostupnostPrikazu.
        ObnovDostupnostPrikazu();
    }

    public async Task PridejAsync(ZakazkaEditViewModel editace)
    {
        await _zakazkyRepository.PridejAsync(editace.Nazev.Trim(), editace.VyslednyDatumOd, editace.VyslednyDatumDo);
        await NactiAsync();
    }

    public async Task UpravAsync(int id, ZakazkaEditViewModel editace)
    {
        await _zakazkyRepository.UpravAsync(id, editace.Nazev.Trim(), editace.VyslednyDatumOd, editace.VyslednyDatumDo);
        await NactiAsync();
    }

    public async Task SmazAsync(int id)
    {
        await _zakazkyRepository.SmazAsync(id);
        await NactiAsync();
    }

    /// <summary>Uloží úsek posunutý tažením na časové ose.</summary>
    public async Task UlozPosunutyUsekAsync(UsekViewModel usek)
    {
        await _zakazkyRepository.UlozUsekAsync(usek.Id, usek.DatumOd, usek.DatumDo);

        // Tažení mohlo úsek nasunout na sousední; repozitář je pak slije,
        // takže se musí načíst znovu, aby UI odpovídalo databázi.
        await NactiAsync();
    }

    /// <summary>Rozdělí úsek ke dni pod kurzorem na dvě navazující části.</summary>
    public async Task RozdelUsekAsync(UsekViewModel usek, DateOnly den)
    {
        if (await _zakazkyRepository.RozdelUsekAsync(usek.Id, den))
        {
            await NactiAsync();
        }
    }

    public async Task SmazUsekAsync(UsekViewModel usek)
    {
        if (await _zakazkyRepository.SmazUsekAsync(usek.Id))
        {
            await NactiAsync();
        }
    }

    public async Task PridejMilnikAsync(int zakazkaId, DateOnly datum, string nazev)
    {
        await _zakazkyRepository.PridejMilnikAsync(zakazkaId, datum, nazev.Trim());
        await NactiAsync();
    }

    /// <summary>Lze zakázky přetahovat mezi sebou? Jen když je vypnuté automatické řazení.</summary>
    public bool LzeMenitPoradi => !Kalendar.Nastaveni.AutomatickeRazeni;

    /// <summary>
    /// Přesune zakázku na jinou pozici. Během tažení se volá opakovaně, proto se sem
    /// nic neukládá — zápis do databáze dělá <see cref="UlozPoradiAsync"/> po dotažení.
    /// </summary>
    public void PresunZakazku(ZakazkaViewModel zakazka, int cilovyIndex)
    {
        var stavajici = Zakazky.IndexOf(zakazka);
        cilovyIndex = Math.Clamp(cilovyIndex, 0, Zakazky.Count - 1);

        if (stavajici < 0 || stavajici == cilovyIndex)
        {
            return;
        }

        // Přeskládání během tažení nemá spouštět přepočty; ty proběhnou po dotažení.
        _hromadnaZmena = true;
        try
        {
            Zakazky.Move(stavajici, cilovyIndex);
        }
        finally
        {
            _hromadnaZmena = false;
        }
    }

    public async Task UlozPoradiAsync()
    {
        await _zakazkyRepository.UlozPoradiAsync([.. Zakazky.Select(z => z.Id)]);
        PostavRadkyTabulky();
    }

    /// <summary>Rozbalí nebo sbalí strom milníků u zakázky.</summary>
    public void PrepniRozbaleni(ZakazkaViewModel zakazka)
    {
        if (!_rozbaleneZakazky.Remove(zakazka.Id))
        {
            _rozbaleneZakazky.Add(zakazka.Id);
        }

        PostavRadkyTabulky();
    }

    public async Task UpravMilnikAsync(MilnikViewModel milnik, DateOnly datum, string nazev)
    {
        await _zakazkyRepository.UpravMilnikAsync(milnik.Id, datum, nazev.Trim());
        await NactiAsync();
    }

    public async Task SmazMilnikAsync(MilnikViewModel milnik)
    {
        await _zakazkyRepository.SmazMilnikAsync(milnik.Id);
        await NactiAsync();
    }

    public async Task UlozNastaveniAsync(PracovniNastaveni nastaveni)
    {
        var razeniSeZmenilo = Kalendar.Nastaveni.AutomatickeRazeni != nastaveni.AutomatickeRazeni;

        await _nastaveniRepository.UlozAsync(nastaveni);
        Kalendar = new PracovniKalendar(nastaveni);
        OnPropertyChanged(nameof(LzeMenitPoradi));

        // Zapnutí automatického řazení musí zakázky hned přeskládat, proto plné načtení.
        if (razeniSeZmenilo)
        {
            await NactiAsync();
            return;
        }

        // Kolize závisí na pracovních dnech, takže změna nastavení je musí přepočítat taky.
        PrepocitejKolize();
        PrepocitejHodiny();
    }

    /// <summary>
    /// Kontrola aktualizací na pozadí. Nikdy nevyhazuje výjimku a nikdy neblokuje start —
    /// když se nepovede, uživatel prostě jen neuvidí notifikaci.
    /// </summary>
    public async Task ZkontrolujAktualizaceAsync()
    {
        var aktualizace = await _updateChecker.ZkontrolujAsync();
        if (aktualizace is null)
        {
            // Aplikace je aktuální, takže dřív stažený instalátor už je jen zbytečných
            // desítek megabajtů v TEMP.
            UpdateDownloader.UklidStazene();
            return;
        }

        Aktualizace = aktualizace;
        UrlAktualizace = aktualizace.StrankaReleaseUrl;
        InformaceOAktualizaci = $"K dispozici je novější verze {aktualizace.Verze}.";
    }

    private void NaZmenuZakazky(object? sender, PropertyChangedEventArgs e)
    {
        // Přepočet při každé změně termínu, tedy i průběžně během tažení pruhu.
        if (e.PropertyName is nameof(ZakazkaViewModel.DatumOd) or nameof(ZakazkaViewModel.DatumDo))
        {
            PrepocitejKolize();
            PrepocitejHodiny();
        }
    }

    private void PrepocitejVse()
    {
        PrepocitejKolize();
        PrepocitejHodiny();
        PrepocitejRozsahOsy();
        PostavRadkyTabulky();
    }

    /// <summary>
    /// Přeskládá řádky tabulky: zakázka a pod ní její milníky podle data, pokud je
    /// strom rozbalený. Výchozí stav je sbaleno.
    /// </summary>
    private void PostavRadkyTabulky()
    {
        var vybranaZakazkaId = _vybranyRadek?.Zakazka.Id;
        var vybranyMilnikId = _vybranyRadek?.Milnik?.Id;

        foreach (var radek in RadkyTabulky)
        {
            radek.Dispose();
        }

        RadkyTabulky.Clear();

        foreach (var zakazka in Zakazky)
        {
            var jeRozbalena = _rozbaleneZakazky.Contains(zakazka.Id);
            RadkyTabulky.Add(new RadekTabulky(zakazka, jeRozbalena));

            if (!jeRozbalena)
            {
                continue;
            }

            var milniky = zakazka.Milniky.OrderBy(m => m.Datum).ToList();
            for (var i = 0; i < milniky.Count; i++)
            {
                RadkyTabulky.Add(new RadekTabulky(zakazka, milniky[i], i == milniky.Count - 1));
            }
        }

        // Přeskládání vytvoří nové instance řádků, takže výběr obnovíme podle Id.
        _vybranyRadek = RadkyTabulky.FirstOrDefault(r =>
            r.Zakazka.Id == vybranaZakazkaId && r.Milnik?.Id == vybranyMilnikId);

        OnPropertyChanged(nameof(VybranyRadek));
    }

    private void PrepocitejKolize()
    {
        // S kalendářem, aby překryv jen přes víkend nebo svátek nehlásil konflikt.
        var kolidujici = KolizeDetektor.NajdiKolidujici(Zakazky.Select(z => z.ToEntity()), Kalendar);

        foreach (var zakazka in Zakazky)
        {
            zakazka.MaKolizi = kolidujici.Contains(zakazka.Id);
        }

        OnPropertyChanged(nameof(SouhrnStavu));
    }

    private void PrepocitejHodiny()
    {
        foreach (var zakazka in Zakazky)
        {
            // Napříč úseky, takže se pauza mezi nimi do odhadu nezapočítá.
            zakazka.PocetPracovnichDnu = Kalendar.PocetPracovnichDnu(zakazka.Rozsahy);
            zakazka.OdhadHodin = zakazka.PocetPracovnichDnu * Kalendar.Nastaveni.HodinDenne;
        }
    }

    private void PrepocitejRozsahOsy()
    {
        var dnes = DateOnly.FromDateTime(DateTime.Today);

        var zacatek = Zakazky.Count > 0 ? Zakazky.Min(z => z.DatumOd) : dnes;
        var konec = Zakazky.Count > 0 ? Zakazky.Max(z => z.DatumDo) : dnes;

        // Dnešek musí být v ose vidět i tehdy, když jsou všechny zakázky v budoucnu nebo minulosti.
        if (dnes < zacatek)
        {
            zacatek = dnes;
        }

        if (dnes > konec)
        {
            konec = dnes;
        }

        PrvniDen = zacatek.AddDays(-RezervaDnu);

        var dnyPodleZakazek = konec.DayNumber - PrvniDen.DayNumber + 1 + RezervaDnu;

        // Osa má vyplnit celé okno i tehdy, když zakázky sahají jen na pár dnů —
        // jinak by za posledním pruhem zůstávalo prázdné bílé místo.
        var dnyPodleOkna = _sirkaViditelneCasti > 0 && _sirkaDne > 0
            ? (int)Math.Ceiling(_sirkaViditelneCasti / _sirkaDne) + 1
            : MinimalniRozsahDnu;

        PocetDnu = Math.Max(dnyPodleZakazek, dnyPodleOkna);
    }

}
