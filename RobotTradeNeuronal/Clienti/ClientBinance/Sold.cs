using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RobotTradeNeuronal.Clienti.ClientBinance
{
    internal class Sold
    {
        public string Activ { get; set; }
        public double Liber { get; set; }
        public double Blocat { get; set; }

        public Sold(dynamic info, bool futures = false)
        {
            try
            {
                Activ = info.asset;
                Liber = futures
                    ? Convert.ToDouble(info.availableBalance ?? "0")
                    : Convert.ToDouble(info.free ?? "0");

                Blocat = futures
                    ? Convert.ToDouble(info.marginBalance ?? "0") - Liber
                    : Convert.ToDouble(info.locked ?? "0");

                Console.WriteLine($"Sold actualizat: Activ={Activ}, Liber={Liber}, Blocat={Blocat}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Eroare la actualizarea soldului: {ex.Message}");
            }
        }
    }
}
