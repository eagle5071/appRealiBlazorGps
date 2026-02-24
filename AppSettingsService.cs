using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace appRealiBlazorGps
{
 public class AppSettingsService
 {
  private const string ServerUrlKey = "server_ip_address";
  private const string UseGeoKey = "use_geolocation";
  private const string UseInternalStorage = "use_storage";

  public string ServerUrl
  {
   get => Preferences.Default.Get(ServerUrlKey, string.Empty);
   set => Preferences.Default.Set(ServerUrlKey, value);
  }

  public bool UseGeolocation
  {
   get => Preferences.Default.Get(UseGeoKey, false);
   set => Preferences.Default.Set(UseGeoKey, value);
  }

  public bool UseStorage
  {
   get => Preferences.Default.Get(UseInternalStorage, false);
   set => Preferences.Default.Set(UseInternalStorage, value);
  }

  public bool IsConfigured => !string.IsNullOrWhiteSpace(ServerUrl);


  // Aggiungi questo evento
  public event Action OnSettingsChanged;

  public void NotifyChanges() => OnSettingsChanged?.Invoke();

  // Modifica le proprietà MostraConfigurazione per notificare quando cambiano
  private bool _mostraConfigurazione;
  public bool MostraConfigurazione
  {
   get => _mostraConfigurazione;
   set { _mostraConfigurazione = value; NotifyChanges(); }
  }

  private bool _mostraStatoRete;
  public bool MostraStatoRete
  {
   get => _mostraStatoRete;
   set { _mostraStatoRete = value; NotifyChanges(); }
  }


  public bool ServerInManutenzione { get; set; } = false;
  public bool popupChiusoManualmente { get; set; } = false;
 }
}
