using Microsoft.Extensions.Logging;

namespace appRealiBlazorGps
{ 
 public static class MauiProgram
 {
  public static MauiApp CreateMauiApp()
  {
   var builder = MauiApp.CreateBuilder();

   // --- INIZIO MODIFICA PER SSL ---
   var handler = new HttpClientHandler();
   handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
   builder.Services.AddScoped(sp => new HttpClient(handler));
   // --- FINE MODIFICA ---


   //builder.Services.AddScoped(sp => new HttpClient());
   builder
    .UseMauiApp<App>()
    .ConfigureFonts(fonts =>
    {
     fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
    });

   builder.Services.AddMauiBlazorWebView();

#if DEBUG
 		builder.Services.AddBlazorWebViewDeveloperTools();
 		builder.Logging.AddDebug();
#endif

   return builder.Build();
  }
 }
}
