# Project Unitas Webshop met Razor Pages

De webshop en het klantenportaal draaien op ASP.NET Core Razor Pages (.NET 10), ASP.NET Core Identity en Entity Framework Core. De QR-Servicedesk hoort bij het eerdere applicatieproject. De Razor-versie begint met een eigen database. Accounts, authenticatorregistraties en aanvragen uit de Node-versie zijn niet geïmporteerd.

## Starten met Visual Studio

1. Open `Project-Unitas.sln` in **Visual Studio 2026** met de workload **ASP.NET and web development**.
2. Kies `Unitas.Webshop` als opstartproject en selecteer het profiel **http**.
3. Druk op **Ctrl+F5**. De applicatie opent op [http://localhost:5177](http://localhost:5177).
4. Kies **Inloggen → Registreren**. Gebruik een wachtwoord van minimaal 8 tekens. Scan de QR-code met een authenticator-app en vul de getoonde code in.

De lokale database en de Data Protection-sleutels worden bij de eerste start gemaakt onder `Unitas.Webshop/App_Data`. Als je de ontwikkelgegevens bewaart, bewaar dan zowel de database als de sleutels. Voor het lokale **https**-profiel kun je het ASP.NET-ontwikkelcertificaat vertrouwen met `dotnet dev-certs https --trust`.

Alternatief vanuit de oplossingsmap, met de .NET 10 SDK.

```powershell
dotnet run --project .\Unitas.Webshop\Unitas.Webshop.csproj --launch-profile http
```

## Webshop doorlopen

1. Open een machine en toon de specificaties, veelgestelde vraag en handleiding.
2. Maak een klantaccount aan. Open het klantenportaal vóór en na MFA om te laten zien dat de pagina pas na verificatie toegankelijk is.
3. Vraag een offerte aan en controleer de aanvraag in het klantenportaal.
4. Log uit en meld opnieuw aan. Gebruik een nieuwe authenticatorcode. Dezelfde geaccepteerde code kan niet opnieuw worden gebruikt.
5. Voeg via een machinepagina een artikel toe aan de winkelwagen en kies **Naar afrekenen**. Vul het e-mailadres in, kies een betaalmethode en gebruik eventueel `UNITAS10` voor 10% korting. Kies **Bestelling plaatsen**. Met Gmail of SMTP ingesteld ontvangt de klant de factuur per e-mail.
6. Open **Klantenportaal → Mijn bestellingen** om de bestellingen te bekijken die met het ingelogde account zijn geplaatst. Gastbestellingen en eerdere bestellingen die alleen in een browsersessie stonden, worden niet aan een account gekoppeld.

De webshop neemt de gekozen betaalmethode op in de bevestiging. Voor online betalingen is een aparte betaalprovider nodig.

## Facturen mailen via Gmail OAuth

1. Meld je met de Gmail-afzender aan bij [Google Cloud Personal mode](https://console.cloud.google.com/auth/overview/personal). Maak een project en een OAuth-client van het type **Desktop-app**. Download het OAuth-clientbestand als JSON.
2. Schakel in hetzelfde project de [Gmail API](https://console.cloud.google.com/apis/library/gmail.googleapis.com) in.
3. Open `Configure-Gmail.cmd` in de oplossingsmap en geef het pad naar het gedownloade JSON-bestand op. De Windows-starter gebruikt `RemoteSigned` voor de duur van dat PowerShell-proces. Geef in de browser met het Google-account dat het project heeft aangemaakt toestemming om berichten te versturen. Het script slaat het geverifieerde afzenderadres en de OAuth-instellingen op via .NET User Secrets voor het lokale ontwikkelprofiel.
4. Herstart de webshop en plaats een bestelling met een eigen e-mailadres om de ontvangst te controleren.

Bewaar het OAuth-clientbestand buiten de projectmap. Met een extern Google Cloud-project in de status **Testing** verloopt de vernieuwingssleutel voor Gmail na zeven dagen. Gebruik voor één eigen afzender een nieuw Personal-project. [Google Auth Platform](https://support.google.com/cloud/answer/15544987)

Gebruik voor de server een beveiligde configuratie met onderstaande waarden. Voer het lokale script desgewenst eerst uit op de computer van de beheerder die de Gmail-afzender machtigt.

| Variabele | Waarde |
| --- | --- |
| `Gmail__ClientId` en `Gmail__ClientSecret` | Gegevens van de OAuth-client. |
| `Gmail__RefreshToken` | Vernieuwingssleutel van de gemachtigde Gmail-afzender. |
| `Gmail__From` | Het geverifieerde Gmail-adres van de afzender. |

Voor een andere SMTP-maildienst kun je `Smtp__Host`, `Smtp__Port`, `Smtp__From`, `Smtp__Username`, `Smtp__Password` en `Smtp__EnableSsl` gebruiken. De webshop kiest de Gmail API zodra `Gmail__ClientId` is ingesteld. Bij een mislukte verzending kan de klant vanaf de bevestigingspagina opnieuw versturen zolang de websessie beschikbaar is.

## Belangrijkste bestanden

| Bestand | Functie |
| --- | --- |
| `Unitas.Webshop/Program.cs` | Configuratie van Identity, cookies, MFA-beleid en de databaseprovider. |
| `Unitas.Webshop/Areas/Identity/Pages/Account/` | C# Razor PageModels en pagina’s voor registratie, wachtwoordcontrole, authenticator en uitloggen. |
| `Unitas.Webshop/Pages/Machines/` en `Pages/Portal.cshtml.cs` | Machinepagina’s, offerteaanvragen en klantgebonden toegang. |
| `Unitas.Webshop/Data/ApplicationDbContext.cs` | EF Core-model, Identity-opslag, versleutelde authenticatorgegevens en rapportageviews. |
| `Unitas.Webshop/Data/SingleUseAuthenticator.cs` | TOTP-provider van Identity met controle op hergebruik. |

## PostgreSQL voor de teamserver

1. Maak een **lege, aparte** database `unitas_webshop`. Maak als PostgreSQL-beheerder de loginrollen `unitas_webshop_app` en `unitas_webshop_dba` aan. Stel hun wachtwoorden interactief in met SCRAM. Gebruik de database- en schema-eigenaar voor de eerste schema-aanmaak.
2. Zet tijdelijk `ConnectionStrings__PostgreSql` op de verbinding van de eigenaar en voer uit vanuit de oplossingsmap.

   ```powershell
   dotnet run --project .\Unitas.Webshop\Unitas.Webshop.csproj --no-launch-profile -- --initialize-db
   ```

   Dit maakt de tabellen en rapportageviews aan. Bij een bestaande installatie voegt deze versie de tabel voor bestelhistorie toe met dezelfde opdracht en het eigenaarsaccount. Pas daarna het bijgewerkte rechtenbestand toe.
3. Pas `PostgreSql/rechten.sql` toe met het eigenaarsaccount en `psql -v ON_ERROR_STOP=1`. Gebruik voortaan `unitas_webshop_app` voor de webapp. Gebruik de DBA-rol voor directe rapportagetoegang.
4. Zet op de projectserver `ASPNETCORE_ENVIRONMENT=Production`. Configureer de runtimeverbinding via `ConnectionStrings__PostgreSql` in beveiligde configuratie. Gebruik in de Npgsql-verbindingsstring `SSL Mode=VerifyFull` en `Root Certificate=<pad naar CA-bestand>`. Beperk TCP 5432 tot de apphost en het beheersegment.
5. Gebruik HTTPS en stel `AllowedHosts` in op de definitieve apphost. Configureer `Identity__KeyDirectory` op een afgeschermde, blijvende map. Versleutel de sleutelring via `Identity__KeyCertificate` en het wachtwoord via beveiligde configuratie, of via `Identity__ProtectKeysWithDpapi=true` voor één Windows-server met een vaste servicegebruiker. Bewaar de sleutelring bij het herstelplan.

```sql
SELECT * FROM public.webshop_report ORDER BY product;
SELECT * FROM public.auth_report ORDER BY day DESC, event;
```

De lokale verificatie omvatte registratie, MFA-instelling, foutieve codes, weigering van hergebruik, inloggen na uitloggen, offerteopslag, afscherming tussen twee klantaccounts en auditregistratie. De gebruikersstromen zijn getest met SQLite en met een tijdelijke PostgreSQL-server. In de PostgreSQL-test werkten de TLS-verbinding, SCRAM, de beperkte applicatierol en directe DBA-rapportage. De schoolomgeving heeft een eigen acceptatietest nodig.

[ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity?view=aspnetcore-10.0) · [Visual Studio en .NET](https://learn.microsoft.com/en-us/dotnet/core/install/windows)
