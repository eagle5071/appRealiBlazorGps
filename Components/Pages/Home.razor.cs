using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Net.Http.Json;

namespace appRealiBlazorGps.Components.Pages
{
 public partial class Home
 {

  // Iniettiamo i servizi necessari (sostituiscono @inject)
  [Inject] public NavigationManager Nav { get; set; }
  [Inject] public HttpClient Http { get; set; }
  [Inject] public IJSRuntime JS { get; set; }

  // Variabili collegate al @bind del file HTML
  public string username { get; set; } = "";
  public string password { get; set; } = "";
  public string errorMessage { get; set; } = "";


  // Qui sposti le variabili
  public LoginRequest LoginModel { get; set; } = new();
  public string ErrorMessage { get; set; } = "";


// Metodo chiamato dal form @onsubmit="HandleLogin"
  public async Task HandleLogin()
  {
   errorMessage = ""; // Reset dell'errore ad ogni tentativo

   try
   {
    // Definiamo l'oggetto da inviare (deve corrispondere alla tua API)
    var loginData = new
    {
     username = username,
     password = password,
     durataminuti = 0
    };

    // Chiamata POST all'API
    // Nota: Sostituisci l'URL con quello reale del tuo server
    var response = await Http.PostAsJsonAsync($"{Costanti.apiurl}/api/login", loginData);

    if (response.IsSuccessStatusCode)
    {
     // Leggiamo la risposta (Token, User, ecc.)
     var result = await response.Content.ReadFromJsonAsync<LoginResponse>();

     if (result != null)
     {
      // Salviamo i dati nel sessionStorage (come facevi in JS)
      await JS.InvokeVoidAsync("sessionStorage.setItem", "token", result.token);
      await JS.InvokeVoidAsync("sessionStorage.setItem", "refreshtoken", result.refreshtoken);

      // Per l'oggetto user, lo serializziamo in stringa JSON
      var userJson = System.Text.Json.JsonSerializer.Serialize(result.user);
      await JS.InvokeVoidAsync("sessionStorage.setItem", "user", userJson);

      // Navighiamo alla pagina delle timbrature
      Nav.NavigateTo("/rilevazioni");
     }
    }
    else
    {
     errorMessage = "Credenziali non valide. Riprova.";
    }
   }
   catch (Exception ex)
   {
    // Gestione errori di rete o server offline
    errorMessage = "Errore di connessione: " + ex.Message;
    if (ex.InnerException != null)
    {
     errorMessage += $" | INNER: {ex.InnerException.Message}";
    }
   }
  }



 // Classe di supporto per leggere la risposta JSON del server
 public class LoginResponse
 {
  public string token { get; set; }
  public string refreshtoken { get; set; }
  public object user { get; set; }
 }


 public class LoginRequest 
  { 
    public string Username { get; set; } 
    public string Password { get; set; } 
  }
}
 }
