using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace appRealiBlazorGps.Components.Pages;

public partial class Rilevazioni: IDisposable
{
 private bool mostraModale = false;
 private string tipoRichiesta = "";
 private string matricola = "";
 private string nominativo = "Caricamento...";
 private bool mostraConfermaGPS = false;
 public bool mostraConfigurazione = false;
 private bool isGpsLoading = false;

 private int conteggioCoda = 0;

 [Inject] public NavigationManager? Nav { get; set; } = default!;

 [Inject] public HttpClient? Http { get; set; } = default!;
 [Inject] public IJSRuntime? JS { get; set; } = default!;

 [Inject] public AppSettingsService? Setting { get; set; }

 // Campi modale
 public DateTime dataInizio = DateTime.Now;
 public DateTime dataFine = DateTime.Now;
 public DateTime oraInizio = DateTime.Now;
 public DateTime oraFine = DateTime.Now;
 public string note = "";

 private List<Attivita>? storico;
 private UserModello? utenteLoggato;


 private void ForzaRefresh() => InvokeAsync(StateHasChanged);

 // 1. IL METODO DEVE ESSERE DEFINITO QUI (livello classe)
 private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
 {
  await InvokeAsync(async () =>
  {
   if (e.NetworkAccess == NetworkAccess.Internet)
   {
    await SincronizzaCoda();
   }
   StateHasChanged();
  });
 }

 protected override void OnInitialized()
 {
  try
  {
   // 1. Controllo di sicurezza fondamentale
   if (Setting == null) return;

   Setting.popupChiusoManualmente = false;

   // 2. Disiscrizione preventiva (per evitare doppie registrazioni in caso di refresh)
   Setting.OnSettingsChanged -= ForzaRefresh;
   Setting.OnSettingsChanged += ForzaRefresh;

   // 3. Connettività (assicurati che sia disponibile)
   try
   {
    Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
    Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
   }
   catch
   {
    // Alcuni dispositivi iOS vecchi o simulatori possono dare errore qui
   }
  }
  catch (Exception ex)
  {
   // Se non vedi questo alert, allora il problema non è qui
   // Ma se lo vedi, hai trovato perché le API non partivano!
   _ = App.Current.MainPage.DisplayAlert("OnInitialized", ex.Message, "OK");
  }
 }

 public void Dispose()
 {

  if (Setting != null) Setting.OnSettingsChanged -= ForzaRefresh;
  // Se Connectivity.Current è già stato distrutto dal sistema o è null, 
  // questo potrebbe lanciare un'eccezione che "sporca" lo stato dell'app.
  try
  {
   Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
  }
  catch { }


 }

 protected override async Task OnInitializedAsync()
 {
  try
  {
   if (Setting == null) return;

   if (Setting.UseStorage)
   {
    AggiornaConteggioCoda();
    // NON usare Task.Run qui. 
    // Usa una funzione "Fire and Forget" ma sul thread principale,
    // oppure aspetta che finisca prima di caricare lo storico.
    await SincronizzaCoda();
   }

   await CaricaStorico();
  }
  catch (Exception ex)
  {
   _ = App.Current.MainPage.DisplayAlert("OnInitializedAsync", ex.Message, "OK");
  }
 }

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

    Setting.MostraConfigurazione = false;
    Setting.MostraStatoRete = false;

    // Forza l'aggiornamento della grafica
    StateHasChanged();
   }
  }
 }

 private bool mostraConfermaLogout = false;
 private void EffettuaLogout()
 {
  mostraConfermaLogout = true; // Mostra la modale invece del confirm browser
 }

 private async Task ConfermaLogoutEffettiva()
 {
  await JS.InvokeVoidAsync("sessionStorage.clear");
  Nav.NavigateTo("/");
 }

 private Attivita? selectedItem = null;
 private bool isLoadingReason = false;

 private async Task RecoverReason(Attivita item)
 {
  // Se l'utente clicca sull'elemento già aperto, lo chiudiamo
  if (selectedItem == item)
  {
   selectedItem = null;
   return;
  }

  // Impostiamo l'elemento selezionato e attiviamo il caricamento
  selectedItem = item;

  // Carichiamo la Reason dall'API solo se non l'abbiamo già caricata
  //if (string.IsNullOrEmpty(item.Reason))
  {
   isLoadingReason = true;
   StateHasChanged();
   try
   {
    // Sostituisci con la tua chiamata API reale
    // Esempio: item.Reason = await Http.GetStringAsync($"api/presenze/reason/{item.Codice}");
    item.ReasonRaw = await GetReasonFromApi(item);
   }
   catch (Exception ex)
   {
    item.ReasonRaw = "Impossibile recuperare la motivazione.";
   }
   finally
   {
    isLoadingReason = false;
    StateHasChanged();
   }
  }
 }

 // Esempio di metodo per la chiamata
 private async Task<string> GetReasonFromApi(Attivita item)
 {

  var url = Setting.ServerUrl.ToLower();
  // Se l'utente ha dimenticato di scrivere http://, lo aggiungiamo noi per sicurezza
  if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
  {
   url = $"http://{url}";
  }


  // Qui metti la tua logica HttpClient
  // Esempio finto:
  var response = await Http.GetStringAsync($"{url}/api/rilevazionitimbrature/getreason?tipo={item.Tipo}&id={item.Id}");
 return response;

  //await Task.Delay(500); // Simula attesa rete
  //return "Caricato da API: Richiesta approvata dal responsabile.";
 }



 #region "Storico"


 public MarkupString FormattaRagione(string ragione)
 {
  if (string.IsNullOrEmpty(ragione)) return (MarkupString)"";

  // Cerchiamo le parentesi quadre e mettiamo uno span rosso intorno ad esse
  // Sostituisce "[" con "<span style='color:red'>[" 
  // e "]" con "]</span>"
  var testoFormattato = ragione
      .Replace("[", "<span style='color:red; font-weight:bold;'>[")
      .Replace("]", "]</span>");

  return (MarkupString)testoFormattato;
 }


 private async Task CaricaStorico()
 {

  if (string.IsNullOrEmpty(matricola)) {
   //await App.Current.MainPage.DisplayAlert("Carica storico", "Matricola assente", "OK");
   return;
  }

  var url = Setting.ServerUrl.ToLower();
  // Se l'utente ha dimenticato di scrivere http://, lo aggiungiamo noi per sicurezza
  if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
  {
   url = $"http://{url}";
  }

  try
  {
   // Sostituisci con il tuo indirizzo IP reale
   //var response = await Http.GetAsync($"{Costanti.apiurl}/api/rilevazionitimbrature/storico/{matricola}");
   var response = await Http.GetAsync($"{url}/api/rilevazionitimbrature/storico/{matricola}");
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
   await App.Current.MainPage.DisplayAlert("Storico", ex.Message+" --- "+ ex.InnerException, "OK");
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
  [JsonPropertyName("id")]
  public int Id { get; set; }

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
    return $"{dateDisplay}<br/>{sTime} / {eTime}";
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

 private void AggiornaConteggioCoda()
 {
  string codaJson = Preferences.Get("coda_offline", "[]");
  try
  {
   var lista = JsonSerializer.Deserialize<List<object>>(codaJson);
   conteggioCoda = lista?.Count ?? 0;
  }
  catch
  {
   conteggioCoda = 0;
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

  var url = Setting.ServerUrl.ToLower();
  // Se l'utente ha dimenticato di scrivere http://, lo aggiungiamo noi per sicurezza
  if (!url.StartsWith("http",StringComparison.OrdinalIgnoreCase))
  {
   url = $"http://{url}";
  }

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
   //var request = new HttpRequestMessage(HttpMethod.Post, $"{Costanti.apiurl}/api/malattia/aggiungi");
   var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/api/malattia/aggiungi");
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

  // Se l'utente ha dimenticato di scrivere http://, lo aggiungiamo noi per sicurezza
  var url = Setting.ServerUrl.ToLower();
  if (!url.StartsWith("http"))
  {
   url = $"http://{url}";
  }

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
   reason = motivoPermesso,
   status = "Richiesta"
  };

  try
  {
   //var request = new HttpRequestMessage(HttpMethod.Post, $"{Costanti.apiurl}/api/permessi/aggiungi");
   var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/api/permessi/aggiungi");
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
  // Se l'utente ha dimenticato di scrivere http://, lo aggiungiamo noi per sicurezza
  var url = Setting.ServerUrl.ToLower();
  if (!url.StartsWith("http",StringComparison.OrdinalIgnoreCase))
  {
   url = $"http://{url}";
  }

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
   //   var request = new HttpRequestMessage(HttpMethod.Post, $"{Costanti.apiurl}/api/ferie/aggiungi");
   var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/api/ferie/aggiungi");
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

  // FASE 1. Recupero il token
  var token = await JS.InvokeAsync<string>("sessionStorage.getItem", "token");
  // Se l'utente ha dimenticato di scrivere http://, lo aggiungiamo noi per sicurezza
  var url = Setting.ServerUrl.ToLower();
  if (!url.StartsWith("http"))
  {
   url = $"http://{url}";
  }

  if (string.IsNullOrEmpty(matricola))
  {
   await JS.InvokeVoidAsync("alert", "Matricola non trovata!");
   return;
  }


  // --- FASE 2: GPS ---
  double latitudine = 0;
  double longitudine = 0;
  isGpsLoading = false;
  mostraConfermaGPS = false;
  StateHasChanged();


  // FASE 3 CONTROLLO SE L'UTENTE HA ABILITATO IL GPS NELLE SETTINGS
  if (Setting.UseGeolocation == true)
  {

   isGpsLoading = true;
   mostraConfermaGPS = false;
   StateHasChanged();

   // FASE 3 PICCOLO RITARDO
   await Task.Delay(100);

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
     await App.Current.MainPage.DisplayAlert("Errore", "Impossibile recuperare GPS:" + ex.Message, "OK");
     return;
    }
   }
   else
   {
    // L'utente ha detto di no alla finestra di sistema
    isGpsLoading = false;
    await App.Current.MainPage.DisplayAlert("Errore", "Permesso negato dal sistema.", "OK");
    return;
   }
  }




  // --- FASE 4: COSTRUZIONE OGGETTO ---
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


  if (Setting.UseStorage == true)
  {
   // --- FASE 5: IL BIVIO OFFLINE ---
   var rete = Connectivity.Current.NetworkAccess;
   if (rete != NetworkAccess.Internet)
   {
    // SIAMO OFFLINE: Salva localmente e chiudi
    await SalvaOffline(data);
    AggiornaConteggioCoda();
    await App.Current.MainPage.DisplayAlert("Offline", "Connessione assente. La timbratura è stata salvata sul telefono e verrà inviata appena possibile.", "OK");
    isGpsLoading = false;
    StateHasChanged();
    return;
   }
  }


  try
  {
   // Usiamo l'endpoint corretto per le timbrature
   var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/api/rilevazionitimbrature/inserisci");
   //request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
   request.Content = JsonContent.Create(data);
   var response = await Http.SendAsync(request);

   // 2. Controllo specifico per il batch "In Pausa" (Errore 503)
   Setting.popupChiusoManualmente = false;
   Setting.NotifyChanges();
   if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
   {
    Setting.ServerInManutenzione = true;
    Setting.NotifyChanges();
    return;
   }

   if (response.IsSuccessStatusCode)
   {

    Setting.ServerInManutenzione = false;
    Setting.NotifyChanges();
    await CaricaStorico(); // Aggiorna la timeline per vedere il nuovo punto
   }
   else
   {
    Setting.ServerInManutenzione = false; // Se risponde (anche male), non è manutenzione
    Setting.NotifyChanges();

    if ( Setting.UseStorage == true ) {
     await SalvaOffline(data);
     AggiornaConteggioCoda();
     await App.Current.MainPage.DisplayAlert("Offline", "Connessione assente. La timbratura è stata salvata sul telefono e verrà inviata appena possibile.", "OK");
    }
    else{

     // Leggiamo il messaggio di errore che arriva dal server
     var errorDetails = await response.Content.ReadAsStringAsync();
     var statusCode = (int)response.StatusCode;
     // Stampiamo in console per il programmatore
     // Avvisiamo l'utente
     if (errorDetails=="Duplicato"){
      await App.Current.MainPage.DisplayAlert("Offline", "Timbratura gia' inviata.", "OK");

     }
     else
     {
      await App.Current.MainPage.DisplayAlert("Offline", "Connessione assente. La timbratura non è stata inviata. Riprovare piu' tardi.", "OK");

     }
     //await App.Current.MainPage.DisplayAlert("Errore", $"ERRORE API: {statusCode} - {errorDetails}", "OK");
    }
   }
  }
  catch (Exception ex)
  {
   Setting.ServerInManutenzione = false; // Non è 503, è un problema di rete
   Setting.NotifyChanges();
   if (Setting.UseStorage == true ) {
    await SalvaOffline(data);
    AggiornaConteggioCoda();
   }
   await App.Current.MainPage.DisplayAlert("Errore", $"Errore di connessione: {ex.Message}", "OK");
  }
  finally
  {
   isGpsLoading = false;
   StateHasChanged();
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

 #region "connessione internet"

 private async Task SalvaOffline(object timb)
 {
  try
  {
   // Recuperiamo la vecchia coda (se esiste)
   string codaJson = Preferences.Get("coda_offline", "[]");
   // 2. La converto in lista (uso dynamic o il modello TimbraturaOffline)
   var lista = JsonSerializer.Deserialize<List<object>>(codaJson) ?? new List<object>();
   // Aggiungiamo la nuova
   lista.Add(timb);

   // Salviamo tutto di nuovo nelle Preferences
   Preferences.Set("coda_offline", JsonSerializer.Serialize(lista));

  }
  catch (Exception ex)
  {
   await App.Current.MainPage.DisplayAlert("Errore", $"Errore salvataggio offline: {ex.Message}", "OK");
  }
 }

 // Questo servirà per svuotare la coda quando torna internet
 private async Task SincronizzaCoda()
 {
  // 1. Se non c'è internet, è inutile provare
  if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
  {
   AggiornaConteggioCoda(); // Aggiorna solo il numero nel box
   return;
  }

  // 2. Recupero la coda
  string codaJson = Preferences.Get("coda_offline", "[]");
  var lista = JsonSerializer.Deserialize<List<object>>(codaJson) ?? new List<object>();

  // Se la coda è vuota, non fare nulla
  if (lista.Count == 0)
  {
   conteggioCoda = 0;
   StateHasChanged();
   return;
  }

  var url = Setting.ServerUrl;
  if (!url.StartsWith("http")) url = $"http://{url}";

  // Creiamo una lista temporanea per tenere traccia di cosa inviamo con successo
  var listaDaRimuovere = new List<object>();

  foreach (var timb in lista)
  {
   try
   {
    var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/api/rilevazionitimbrature/inserisci");
    request.Content = JsonContent.Create(timb);

    var response = await Http.SendAsync(request);
    // 1. Recuperiamo il contenuto dell'errore (il tuo "Hai già timbrato oggi!")
    var messaggioServer = await response.Content.ReadAsStringAsync();

    if (response.IsSuccessStatusCode)
    {
     // Segnamo questa timbratura come inviata con successo
     listaDaRimuovere.Add(timb);
    }
    else
    {
     if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
     {
      if (messaggioServer == "Duplicato")
      {
       listaDaRimuovere.Add(timb);
      }
     }
    }
   }

   catch (Exception ex)
   {
    // Se fallisce una singola timbratura (es. timeout), ci fermiamo e riproveremo dopo
    await App.Current.MainPage.DisplayAlert("Errore", $"Sincronizzazione fallita per un elemento: {ex.Message}", "OK");
    break;
   }
  }

  // 3. Rimuoviamo dalla coda solo quelle inviate davvero
  if (listaDaRimuovere.Any())
  {
   foreach (var r in listaDaRimuovere)
   {
    lista.Remove(r);
   }

   // Salviamo la coda aggiornata (vuota o con le rimanenti)
   Preferences.Set("coda_offline", JsonSerializer.Serialize(lista));

   // Aggiorniamo la UI
   await CaricaStorico();
   StateHasChanged();
  }

  // Aggiorniamo il contatore UI
  AggiornaConteggioCoda();
  StateHasChanged();

 }

 #endregion






 // Classe di supporto (deve avere gli stessi nomi del JSON)
 public class UserModello
 {
  public string? matricola { get; set; }
  public string? cognome { get; set; }
  public string? nome { get; set; }
 }

}
