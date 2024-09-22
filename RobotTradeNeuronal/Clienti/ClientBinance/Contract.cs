using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RobotTradeNeuronal.Clienti.ClientBinance
{
    internal class Contract
    {
        public string Simbol { get; set; }
        public string ActivBaza { get; set; }
        public string ActivCotat { get; set; }
        public int PreciziePret { get; set; }
        public int PrecizieCantitate { get; set; }
        public double MarimeTick { get; set; }
        public double MarimeLot { get; set; }
        public double PreciziePasPret { get; set; } 

        public Contract(dynamic infoContract, bool futures)
        {
            try
            {
                Simbol = infoContract.symbol;
                ActivBaza = infoContract.baseAsset;
                ActivCotat = infoContract.quoteAsset;

                if (futures)
                {
                    PreciziePret = int.Parse(infoContract.pricePrecision.ToString());
                    PrecizieCantitate = int.Parse(infoContract.quantityPrecision.ToString());
                }
                else
                {
                    var filtre = ((IEnumerable<dynamic>)infoContract.filters).ToList();
                    PreciziePret = filtre
                        .Where(f => f.filterType == "PRICE_FILTER")
                        .Select(f => f.tickSize.ToString().TrimEnd('0').Split('.')[1].Length)
                        .FirstOrDefault();

                    PrecizieCantitate = filtre
                        .Where(f => f.filterType == "LOT_SIZE")
                        .Select(f => f.stepSize.ToString().TrimEnd('0').Split('.')[1].Length)
                        .FirstOrDefault();
                }

                MarimeTick = 1 / Math.Pow(10, PreciziePret);
                MarimeLot = 1 / Math.Pow(10, PrecizieCantitate);

             
                PreciziePasPret = MarimeTick;

                Console.WriteLine($"Contract creat: Simbol={Simbol}, ActivBaza={ActivBaza}, ActivCotat={ActivCotat}, MarimeTick={MarimeTick}, MarimeLot={MarimeLot}, PreciziePasPret={PreciziePasPret}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Eroare la crearea contractului: {ex.Message}");
            }
        }
    }
}
