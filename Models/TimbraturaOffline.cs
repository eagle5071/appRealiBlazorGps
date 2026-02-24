using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace appRealiBlazorGps.Models
{
 public class TimbraturaOffline
 {
  public Guid Id { get; set; } = Guid.NewGuid();
  public string Matricola { get; set; }
  public DateTime DataOra { get; set; }
  public string Tipo { get; set; } // Esempio: "TIMB", "FERI", "MALA"
  public double? Latitudine { get; set; }
  public double? Longitudine { get; set; }
  public bool Inviata { get; set; } = false;
 }

}
