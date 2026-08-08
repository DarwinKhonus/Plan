using Plan.Services;

namespace Plan.Tests;

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("V2.0.0", "2.0.0")]
    [InlineData(" v1.0.0 ", "1.0.0")]
    [InlineData("1.4.2+abc1234", "1.4.2")]        // InformationalVersion s hashem commitu
    [InlineData("1.4.2-beta.1", "1.4.2")]         // předrelease
    [InlineData("v1.4.2-rc1+deadbee", "1.4.2")]
    [InlineData("1.2", "1.2")]
    public void ParsujVerzi_zvlada_bezne_tvary(string vstup, string ocekavano)
    {
        Assert.Equal(Version.Parse(ocekavano), UpdateChecker.ParsujVerzi(vstup));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("release-2024")]
    [InlineData("v")]
    public void ParsujVerzi_vrati_null_pro_neplatny_vstup(string? vstup)
    {
        Assert.Null(UpdateChecker.ParsujVerzi(vstup));
    }

    [Fact]
    public void Novejsi_verze_je_vetsi_nez_starsi()
    {
        Assert.True(UpdateChecker.ParsujVerzi("v1.3.0") > UpdateChecker.ParsujVerzi("v1.2.9"));
        Assert.True(UpdateChecker.ParsujVerzi("v2.0.0") > UpdateChecker.ParsujVerzi("v1.99.99"));
        Assert.True(UpdateChecker.ParsujVerzi("v1.0.1") > UpdateChecker.ParsujVerzi("v1.0.0"));
    }

    [Fact]
    public void Stejna_verze_v_obou_tvarech_se_rovna()
    {
        // Tag na GitHubu má prefix "v", InformationalVersion sestavení ne — bez normalizace
        // by aplikace nabízela update sama na sebe.
        Assert.Equal(UpdateChecker.ParsujVerzi("v1.2.3"), UpdateChecker.ParsujVerzi("1.2.3+abc123"));
    }

    [Fact]
    public void Aktualni_verze_je_vzdy_ctitelna()
    {
        // Kdyby AssemblyInformationalVersion chyběla nebo měla neočekávaný tvar,
        // updater by porovnával proti null a spadl při startu.
        Assert.NotNull(UpdateChecker.AktualniVerze);
    }
}

// Poznámka: samotné volání GitHub API se netestuje záměrně — test závislý na síti
// by v CI byl nestabilní a po vydání prvního release by navíc začal selhávat.
// Tichost selhání zajišťuje catch-all v ZkontrolujAsync.
