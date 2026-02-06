using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace appRealiBlazorGps.Components.Pages;

public partial class Rilevazioni
{
 private bool mostraModale = false;
 private string tipoRichiesta = "";
 private string matricola = "";
 private string nominativo = "Caricamento...";
 private bool mostraConfermaGPS = false;
 private bool isGpsLoading = false;
 [Inject] public NavigationManager Nav { get; set; }

 [Inject] public HttpClient Http { get; set; }
 [Inject] public IJSRuntime JS { get; set; }


 // Campi modale
 public DateTime dataInizio = DateTime.Now;
 public DateTime dataFine = DateTime.Now;
 public DateTime oraInizio = DateTime.Now;
 public DateTime oraFine = DateTime.Now;
 public string note = "";

 private List<Attivita>? storico;
 private UserModello? utenteLoggato;

 protected override async Task OnAfterRenderAsync(bool firstRender)
 {

  if (firstRender)
  {
   var textInfo = System.Globalization.CultureInfo.CurrentCulture.TextInfo;

   // 1. Leggi la stringa JSON dal sessionStorage
   var userJson = await JS.InvokeAsync<string>("sessionStorage.getItem", "user");

   if (!string.IsNullOrEmpty(userJson))
   {
    // 2. Converti la stringa in un oggetto C#
    utenteLoggato = JsonSerializer.Deserialize<UserModello>(userJson);

    // 3. Ora puoi assegnare i dati alle variabili della pagina
    matricola = utenteLoggato.matricola;
    nominativo = textInfo.ToTitleCase(utenteLoggato.cognome?.ToLower() ?? "") + " " + textInfo.ToTitleCase(utenteLoggato.nome?.ToLower() ?? ""); ;

    // Forza l'aggiornamento della grafica
    StateHasChanged();
   }
  }
 }


 private async Task EffettuaLogout()
 {
  bool conferma = await JS.InvokeAsync<bool>("confirm", "Vuoi uscire dall'applicazione?");
  if (conferma)
  {
   await JS.InvokeVoidAsync("sessionStorage.clear"); // Pulisce tutto (token, matricola, ecc.)
   Nav.NavigateTo("/"); // Torna alla pagina iniziale
  }
 }

 #region "Storico"


 private async Task CaricaStorico()
 {
  if (string.IsNullOrEmpty(matricola)) return;

  try
  {
   // Sostituisci con il tuo indirizzo IP reale
   var response = await Http.GetAsync($"{Costanti.apiurl}/api/rilevazionitimbrature/storico/{matricola}");
   var jsonGrezzo = await response.Content.ReadAsStringAsync();

   // 2. Configuriamo la deserializzazione
   var options = new JsonSerializerOptions
   {
    PropertyNameCaseInsensitive = true,
    // Questo aiuta a ignorare alcuni errori di conversione
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
   };

   // 3. Convertiamo manualmente
   storico = JsonSerializer.Deserialize<List<Attivita>>(jsonGrezzo, options);
   StateHasChanged();
  }
  catch (Exception ex)
  {
   Console.WriteLine($"Errore: {ex.Message}");
  }
 }

 private string GetIcona(string tipo) => tipo switch
 {
  "Timbratura" => "📍",
  "Ferie" => "🏖️",
  "Permesso" => "⏱️",
  "Malattia" => "🤒",
  _ => "📄"
 };

 // Modifica la classe Attivita per combaciare con i nomi JSON del server
 public class Attivita
 {
  [JsonPropertyName("tipo")]
  public string Tipo { get; set; } = "";

  [JsonPropertyName("startDate")]
  public object? StartDateRaw { get; set; } // object per gestire sia stringa che {}

  [JsonPropertyName("endDate")]
  public object? EndDateRaw { get; set; }

  [JsonPropertyName("startTime")]
  public object? StartTimeRaw { get; set; }

  [JsonPropertyName("endTime")]
  public object? EndTimeRaw { get; set; }

  [JsonPropertyName("reason")]
  public object? ReasonRaw { get; set; }

  [JsonPropertyName("status")]
  public string Status { get; set; } = "";

  // --- PROPRIETÀ DI SUPPORTO PER L'HTML ---

  public string GetStatusCss => Status?.ToLower() ?? "inviata";

  // Funzione per pulire i dati che arrivano come {}
  private string GetStringFromRaw(object? raw)
  {
   if (raw is JsonElement element)
   {
    if (element.ValueKind == JsonValueKind.String)
     return element.GetString() ?? "";
   }
   return ""; // Se è {} o nullo, torna vuoto
  }

  public string FormattaDati()
  {
   string sDate = GetStringFromRaw(StartDateRaw);
   string eDate = GetStringFromRaw(EndDateRaw);
   string sTime = GetStringFromRaw(StartTimeRaw);
   string eTime = GetStringFromRaw(EndTimeRaw);

   DateTime.TryParse(sDate, out DateTime dtStart);
   string dateDisplay = dtStart != DateTime.MinValue ? dtStart.ToString("dd/MM/yyyy") : "";

   if (Tipo == "Timbratura")
   {
    return $"{dateDisplay} - {dtStart:HH:mm}";
   }
   else if (Tipo == "Permesso")
   {
    return $"{dateDisplay} - {sTime} / {eTime}";
   }
   else // Ferie o Malattia
   {
    DateTime.TryParse(eDate, out DateTime dtEnd);
    if (dtEnd != DateTime.MinValue && dtEnd != dtStart)
     dateDisplay += $" – {dtEnd:dd/MM/yyyy}";

    return dateDisplay;
   }
  }

 }

 #endregion

 #region "Malattia"
 // Variabili per il modale Malattia
 private DateTime dataInizioMalattia = DateTime.Now;
 private DateTime dataFineMalattia = DateTime.Now;
 private string motivoMalattia = "";

 public async Task InviaMalattia()
 {
  // 1. Recupero il token e la matricola (che abbiamo già caricato nel OnAfterRender)
  var token = await JS.InvokeAsync<string>("sessionStorage.getItem", "token");

  if (string.IsNullOrEmpty(matricola))
  {
   await JS.InvokeVoidAsync("alert", "Devi inserire la matricola!");
   return;
  }

  // 2. Costruzione dell'oggetto (uguale alla struttura richiesta dal server)
  var data = new
  {
   id = 0,
   tipo = "Malattia",
   timestamp = DateTime.Now,
   startdate = dataInizioMalattia.Date.AddHours(23).AddMinutes(59),// Aggiungiamo T23:59:00 come facevi in JS
   enddate = dataFineMalattia.Date.AddHours(23).AddMinutes(59),    // Aggiungiamo T23:59:00 come facevi in JS
   reason = motivoMalattia,
   matricola = matricola,
   status = "Richiesta"
  };

  try
  {
   // 3. Invio POST con Header Authorization
   var request = new HttpRequestMessage(HttpMethod.Post, $"{Costanti.apiurl}/api/malattia/aggiungi");
   request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
   request.Content = JsonContent.Create(data);

   var response = await Http.SendAsync(request);

   if (response.IsSuccessStatusCode)
   {
    ChiudiModale();
    await CaricaStorico(); // Ricarica la lista per vedere la nuova riga
   }
   else
   {
    await JS.InvokeVoidAsync("alert", "Errore invio malattia");
   }
  }
  catch (Exception ex)
  {
   await JS.InvokeVoidAsync("alert", $"Errore: {ex.Message}");
  }
 }

 #endregion

 #region "Permessi"

 // Variabili per il modale Permessi
 private DateTime dataPermesso = DateTime.Now;
 private string oraInizioPermesso = "09:00"; // Valore predefinito
 private string oraFinePermesso = "10:00";   // Valore predefinito
 private string motivoPermesso = "";

 public async Task InviaPermesso()
 {
  // Recupero il token da sessionStorage
  var token = await JS.InvokeAsync<string>("sessionStorage.getItem", "token");

  if (string.IsNullOrEmpty(matricola))
  {
   await JS.InvokeVoidAsync("alert", "Devi inserire la matricola prima di effettuare la richiesta!");
   return;
  }

  // Costruiamo l'oggetto rispettando i nomi campo del tuo JSON (attenzione a maiuscole/minuscole come vuole la tua API)
  var data = new
  {
   id = 0,
   tipo = "Permesso",
   timeStamp = DateTime.Now,
   datapermesso = dataPermesso.Date.AddHours(23).AddMinutes(59), // Come nel tuo JS
   orainizio = oraInizioPermesso,
   orafine = oraFinePermesso,
   matricola = matricola,
   motivo = motivoPermesso,
   status = "Richiesta"
  };

  try
  {
   var request = new HttpRequestMessage(HttpMethod.Post, $"{Costanti.apiurl}/api/permessi/aggiungi");
   request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
   request.Content = JsonContent.Create(data);

   var response = await Http.SendAsync(request);

   if (response.IsSuccessStatusCode)
   {
    await JS.InvokeVoidAsync("alert", "Permesso inviato con successo!");
    ChiudiModale();
    await CaricaStorico(); // Rinfresca la timeline
   }
   else
   {
    var errore = await response.Content.ReadAsStringAsync();
    await JS.InvokeVoidAsync("alert", $"Errore invio permesso: {response.StatusCode}");
   }
  }
  catch (Exception ex)
  {
   await JS.InvokeVoidAsync("alert", $"Errore di rete: {ex.Message}");
  }
 }
 #endregion

 #region "Ferie"

 // Variabili per il modale Ferie
 private DateTime dataInizioFerie = DateTime.Now;
 private DateTime dataFineFerie = DateTime.Now;
 private string motivoFerie = "";

 public async Task InviaFerie()
 {
  // Recupero il token
  var token = await JS.InvokeAsync<string>("sessionStorage.getItem", "token");

  if (string.IsNullOrEmpty(matricola))
  {
   await JS.InvokeVoidAsync("alert", "Devi inserire la matricola!");
   return;
  }

  // Costruzione dell'oggetto JSON
  var data = new
  {
   id = 0,
   tipo = "Ferie",
   timestamp = DateTime.Now,
   // Replichiamo il formato ISO con orario di fine giornata
   startdate = dataInizioFerie.Date.AddHours(23).AddMinutes(59),
   enddate = dataFineFerie.Date.AddHours(23).AddMinutes(59),
   reason = motivoFerie,
   matricola = matricola,
   status = "Richiesta"
  };

  try
  {
   var request = new HttpRequestMessage(HttpMethod.Post, $"{Costanti.apiurl}/api/ferie/aggiungi");
   request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
   request.Content = JsonContent.Create(data);

   var response = await Http.SendAsync(request);

   if (response.IsSuccessStatusCode)
   {
    await JS.InvokeVoidAsync("alert", "Richiesta ferie inviata!");
    ChiudiModale();
    await CaricaStorico(); // Ricarica la timeline
   }
   else
   {
    await JS.InvokeVoidAsync("alert", "Errore invio ferie");
   }
  }
  catch (Exception ex)
  {
   await JS.InvokeVoidAsync("alert", $"Errore: {ex.Message}");
  }
 }


 #endregion

 #region"timbratura"

 public async Task InviaTimbratura()
 {


  // 1. Recupero il token
  var token = await JS.InvokeAsync<string>("sessionStorage.getItem", "token");

  // --- FASE 1: PARLA CON JS E CHIUDI SUBITO ---
  // Prendo il token subito, così poi posso "dimenticarmi" di JS
  if (string.IsNullOrEmpty(matricola))
  {
   await JS.InvokeVoidAsync("alert", "Matricola non trovata!");
   return;
  }

  // --- FASE 2: OPERAZIONI DI SISTEMA (GPS) ---
  isGpsLoading = true;
  mostraConfermaGPS = false;
  StateHasChanged();

  // FASE 3 PICCOLO RITARDO
  await Task.Delay(100);

  //FASE 4 CHIEDO PERMEESI
  double latitudine = 0;
  double longitudine = 0;


  var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

  if (status == PermissionStatus.Granted)
  {
   try
   {

    var location = await Task.Run(async () =>
    {
     return await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5)));
    });

    if (location != null)
    {
     latitudine = location.Latitude;
     longitudine = location.Longitude;
    }
   }
   catch (Exception ex)
   {
   // Se il GPS è spento o non autorizzato, usiamo 0,0 o avvisiamo l'utente
   Console.WriteLine($"Impossibile recuperare GPS: {ex.Message}");
  }

  // Costruiamo l'oggetto con le coordinate reali
  var data = new
   {
    id = 0,
    tipo = "Timbratura",
    timestamp = DateTime.Now,
    lat = latitudine,
    lon = longitudine,
    startDate = DateTime.Now,
    endDate = DateTime.Now,
    reason = "",
    matricola = matricola,
    status = "Inviata"
   };

   try
   {
    // Usiamo l'endpoint corretto per le timbrature
    var request = new HttpRequestMessage(HttpMethod.Post, $"{Costanti.apiurl}/api/rilevazionitimbrature/inserisci");
    //request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    request.Content = JsonContent.Create(data);

    var response = await Http.SendAsync(request);

    if (response.IsSuccessStatusCode)
    {
     // await JS.InvokeVoidAsync("alert", "Timbratura effettuata con successo! 📍");
     await CaricaStorico(); // Aggiorna la timeline per vedere il nuovo punto
    }
    else
    {

     // Leggiamo il messaggio di errore che arriva dal server
     var errorDetails = await response.Content.ReadAsStringAsync();
     var statusCode = (int)response.StatusCode;

     // Stampiamo in console per il programmatore
     Console.WriteLine($"ERRORE API: {statusCode} - {errorDetails}");

     // Avvisiamo l'utente
     await App.Current.MainPage.DisplayAlert("Errore",$"ERRORE API: {statusCode} - {errorDetails}","OK");
     //await JS.InvokeVoidAsync("alert", $"Errore {statusCode}: {errorDetails}");


     // await JS.InvokeVoidAsync("alert", "Errore durante la timbratura.");
    }
   }
   catch (Exception ex)
   {
    await App.Current.MainPage.DisplayAlert("Errore", $"Errore di connessione: {ex.Message}", "OK");
    //await JS.InvokeVoidAsync("alert", $"Errore di connessione: {ex.Message}");
   }
   finally
   {
    isGpsLoading = false;

   }


 }
  else
  {
   // L'utente ha detto di no alla finestra di sistema
   isGpsLoading = false;
   await App.Current.MainPage.DisplayAlert("Errore", "Permesso negato dal sistema.", "OK");
  }





 }

 #endregion

 #region "Modale"

 public void ApriModale(string tipo)
 {
  tipoRichiesta = tipo;
  mostraModale = true;
 }

 public void ChiudiModale() => mostraModale = false;

 #endregion







 // Classe di supporto (deve avere gli stessi nomi del JSON)
 public class UserModello
 {
  public string matricola { get; set; }
  public string cognome { get; set; }
  public string nome { get; set; }
 }

}