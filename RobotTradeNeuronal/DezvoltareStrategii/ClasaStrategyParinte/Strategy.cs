using System.Net.WebSockets;
using NLog;
using Skender.Stock.Indicators;
using RobotTradeNeuronal.Clienti.ClientBinance;
using Newtonsoft.Json;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.WindowsForms;
using OxyPlot.Axes;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Net.Http;

namespace RobotTradeNeuronal.DezvoltareStrategii.ClasaStrategyParinte
{
    internal class Strategy
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly Dictionary<string, int> Echivalent_TimpFrame = new Dictionary<string, int>
        {
            {"1m", 60}, {"5m", 300}, {"15m", 900}, {"30m", 1800}, {"1h", 3600}, {"4h", 14400}
        };

        public Binance Client { get; set; }
        public Contract Contract { get; set; }
        public string Schimb { get; set; }
        public string TimpFrame { get; set; }
        public double ProcentBalanta { get; set; }
        public double ProfitTinta { get; set; }
        public double StopPierdere { get; set; }
        public string NumeStrategie { get; set; }
        public dynamic PozitieCurenta { get; set; }
        public List<dynamic> Lumanari { get; set; }
        public List<dynamic> Tranzactii { get; set; }
        public List<string> Loguri { get; set; }
        public List<double> NiveluriCumparare { get; set; }
        public List<double> NiveluriVanzare { get; set; }
        public System.Timers.Timer MonitorizareTimer { get; set; }
        public System.Timers.Timer PozitieMonitorizareTimer { get; set; }

       
        private ClientWebSocket WebSocketClient;

        private static readonly HttpClient httpClient = new HttpClient();

        public Strategy(Binance client, Contract contract, string schimb, string timpFrame, double procentBalanta,
                         double profitTinta, double stopPierdere, string numeStrategie)
        {
            Client = client;
            Contract = contract;
            Schimb = schimb;
            TimpFrame = timpFrame;
            ProcentBalanta = procentBalanta;
            ProfitTinta = profitTinta;
            StopPierdere = stopPierdere;
            NumeStrategie = numeStrategie;
            PozitieCurenta = null;
            Lumanari = new List<dynamic>();
            Tranzactii = new List<dynamic>();
            Loguri = new List<string>();
            NiveluriCumparare = new List<double>();
            NiveluriVanzare = new List<double>();

           
            ObtineLumanariIstorice();

            MonitorizareTimer = new System.Timers.Timer(86400000); 
            MonitorizareTimer.Elapsed += async (sender, e) => await MonitorizeazaSiOptimizeazaAsync();
            MonitorizareTimer.Start();

            PozitieMonitorizareTimer = new System.Timers.Timer(1000); 
            PozitieMonitorizareTimer.Elapsed += async (sender, e) => await MonitorizeazaPozitiileAsync();
            PozitieMonitorizareTimer.Start();

          
            WebSocketClient = new ClientWebSocket();
            ConectareWebSocketAsync();
        }

        public void AdaugaLog(string mesaj)
        {
            Logger.Info(mesaj);
            Loguri.Add(mesaj);
        }

        
        public void ObtineLumanariIstorice()
        {
            try
            {
                var lumanariIstorice = Client.ObtineLumanariIstorice(Contract.Simbol, TimpFrame).Result;
                if (lumanariIstorice != null)
                {
                    Lumanari = lumanariIstorice.Select(c => new
                    {
                        Timestamp = c.OpenTime,
                        Open = c.Open,
                        High = c.High,
                        Low = c.Low,
                        Close = c.Close,
                        Volume = c.Volume
                    }).Cast<dynamic>().ToList();
                }
                else
                {
                    AdaugaLog("Nu s-au putut obține lumânările istorice.");
                }
            }
            catch (Exception ex)
            {
                AdaugaLog($"Eroare în ObtineLumanariIstorice: {ex.Message}");
            }
        }

       
        public List<double> CalculeazaEMA(List<double> preturi, int perioada)
        {
            var quotes = preturi.Select((p, i) => new Quote { Close = (decimal)p, Date = DateTime.Now.AddMinutes(i) }).ToList();
            var emaList = quotes.GetEma(perioada).ToList();

            return emaList.Select(e => (double)(e.Ema ?? 0)).ToList();
        }

      
        public List<double> CalculeazaRSI(List<double> preturi, int perioada = 14)
        {
            var quotes = preturi.Select((p, i) => new Quote { Close = (decimal)p, Date = DateTime.Now.AddMinutes(i) }).ToList();
            var rsiList = quotes.GetRsi(perioada).ToList();

            return rsiList.Select(r => (double)(r.Rsi ?? 0)).ToList();
        }

        public List<double> CalculeazaKAMA(List<double> preturi, int perioada = 10)
        {
            var quotes = preturi.Select((p, i) => new Quote { Close = (decimal)p, Date = DateTime.Now.AddMinutes(i) }).ToList();
            var kamaList = quotes.GetKama(perioada).ToList();

            return kamaList.Select(k => (double)(k.Kama ?? 0)).ToList();
        }

     
        public List<double> CalculeazaAlma(List<double> preturi, int perioada = 14)
        {
            var quotes = preturi.Select((p, i) => new Quote { Close = (decimal)p, Date = DateTime.Now.AddMinutes(i) }).ToList();
            var almaList = quotes.GetAlma(perioada).ToList();

            return almaList.Select(a => (double)(a.Alma ?? 0)).ToList();
        }

       
        public virtual async Task VizualizeazaTrendulPieteiAsync()
        {
            try
            {
                var preturi = Lumanari.Select(c => (double)c.Close).ToList();
                var emaScurta = CalculeazaEMA(preturi, 20);
                var emaLunga = CalculeazaEMA(preturi, 50);
                var kama = CalculeazaKAMA(preturi, 10);
                var alema = CalculeazaAlma(preturi, 14);
                var rsi = CalculeazaRSI(preturi, 14);

                var modelGrafic = new PlotModel { Title = "Trendul Pieței" };

           
                var seriePreturi = new LineSeries { Title = "Prețuri", StrokeThickness = 2, Color = OxyColors.Blue };
                seriePreturi.Points.AddRange(preturi.Select((p, i) => new DataPoint(i, p)));
                modelGrafic.Series.Add(seriePreturi);

                
                var serieEmaScurta = new LineSeries { Title = "20 EMA", StrokeThickness = 2, Color = OxyColors.Green };
                serieEmaScurta.Points.AddRange(emaScurta.Select((e, i) => new DataPoint(i, e)));
                modelGrafic.Series.Add(serieEmaScurta);

              
                var serieEmaLunga = new LineSeries { Title = "50 EMA", StrokeThickness = 2, Color = OxyColors.Red };
                serieEmaLunga.Points.AddRange(emaLunga.Select((e, i) => new DataPoint(i, e)));
                modelGrafic.Series.Add(serieEmaLunga);

                var serieKama = new LineSeries { Title = "10 KAMA", StrokeThickness = 2, Color = OxyColors.Orange };
                serieKama.Points.AddRange(kama.Select((k, i) => new DataPoint(i, k)));
                modelGrafic.Series.Add(serieKama);

               
                var serieAlma = new LineSeries { Title = "14 ALMA", StrokeThickness = 2, Color = OxyColors.Purple };
                serieAlma.Points.AddRange(alema.Select((a, i) => new DataPoint(i, a)));
                modelGrafic.Series.Add(serieAlma);

                var serieRsi = new LineSeries { Title = "RSI", StrokeThickness = 2, Color = OxyColors.Pink };
                serieRsi.Points.AddRange(rsi.Select((r, i) => new DataPoint(i, r)));
                modelGrafic.Series.Add(serieRsi);

                var prag70 = new LineSeries { Title = "RSI 70", StrokeThickness = 1, Color = OxyColors.Red, LineStyle = LineStyle.Dash };
                prag70.Points.AddRange(new[] { new DataPoint(0, 70), new DataPoint(preturi.Count - 1, 70) });
                modelGrafic.Series.Add(prag70);

                var prag30 = new LineSeries { Title = "RSI 30", StrokeThickness = 1, Color = OxyColors.Green, LineStyle = LineStyle.Dash };
                prag30.Points.AddRange(new[] { new DataPoint(0, 30), new DataPoint(preturi.Count - 1, 30) });
                modelGrafic.Series.Add(prag30);

              
                var exportator = new PngExporter { Width = 600, Height = 400 };
                exportator.ExportToFile(modelGrafic, "market_trend.png");
            }
            catch (Exception ex)
            {
                AdaugaLog($"Eroare în VizualizeazaTrendulPietei: {ex.Message}");
            }
        }

        
        public virtual bool EstePiataInTrendPozitiv()
        {
            try
            {
                var preturi = Lumanari.Select(c => (double)c.Close).ToList();
                var emaScurta = CalculeazaEMA(preturi, 20);
                var emaLunga = CalculeazaEMA(preturi, 50);
                var kama = CalculeazaKAMA(preturi, 10);
                var alema = CalculeazaAlma(preturi, 14);
                var rsi = CalculeazaRSI(preturi, 14);

                return emaScurta.Last() > emaLunga.Last() &&
                       kama.Last() > emaLunga.Last() &&
                       alema.Last() > emaScurta.Last() &&
                       rsi.Last() > 50;
            }
            catch (Exception ex)
            {
                AdaugaLog($"Eroare în EstePiataInTrendPozitiv: {ex.Message}");
                return false;
            }
        }

        public async Task AnalizeazaSiVizualizeazaTrendulPietei()
        {
            try
            {
                bool esteTrendPozitiv = EstePiataInTrendPozitiv();

                AdaugaLog(esteTrendPozitiv
                    ? "Piața este într-un trend pozitiv. Vizualizăm trendul."
                    : "Piața este într-un trend negativ. Vizualizăm trendul.");

                await VizualizeazaTrendulPieteiAsync();
            }
            catch (Exception ex)
            {
                AdaugaLog($"Eroare în AnalizeazaSiVizualizeazaTrendulPietei: {ex.Message}");
            }
        }

     
        public virtual async Task MonitorizeazaPozitiileAsync()
        {
            try
            {
                if (PozitieCurenta == null) return;

                double pretCurent = await ObtinePretCurentAsync();
                if (pretCurent == 0) return;

                double profitPierdere = (pretCurent - (double)PozitieCurenta.PretIntrare) / (double)PozitieCurenta.PretIntrare * 100;
                AdaugaLog($"PnL: {profitPierdere}% pentru poziție {PozitieCurenta.Tip}");

                if (PozitieCurenta.Tip == "long" && pretCurent <= (double)PozitieCurenta.PretIntrare / 3)
                {
                    AdaugaLog($"Prețul a scăzut de 3x. Cumpărăm mai mult.");
                    DeschidePozitie(1);
                }
                else if (PozitieCurenta.Tip == "short" && pretCurent >= (double)PozitieCurenta.PretIntrare * 10)
                {
                    AdaugaLog($"Prețul a crescut de 10x. Închidem poziția.");
                    await InchidePozitieAsync();
                }

                if (profitPierdere >= ProfitTinta || profitPierdere <= -StopPierdere)
                    await InchidePozitieAsync();
            }
            catch (Exception ex)
            {
                AdaugaLog($"Eroare în MonitorizeazaPozitiileAsync: {ex.Message}");
            }
        }

        
        public virtual async Task MonitorizeazaSiOptimizeazaAsync()
        {
            try
            {
                var profituri = Tranzactii.Where(t => t.Stare == "inchis").Select(t => (double)t.PnL).ToList();
                double profitTotal = profituri.Sum();
                int nrTranzactii = profituri.Count;
                double rataCastig = nrTranzactii == 0 ? 0 : profituri.Count(p => p > 0) / (double)nrTranzactii;

                AdaugaLog($"Profit total: {profitTotal}%");
                AdaugaLog($"Rata câștig: {rataCastig * 100}%");

                if (rataCastig < 0.5)
                {
                    ProfitTinta *= 1.1;
                    StopPierdere *= 0.9;
                    AdaugaLog("Ajustare TP și SL.");
                }
            }
            catch (Exception ex)
            {
                AdaugaLog($"Eroare în MonitorizeazaSiOptimizeazaAsync: {ex.Message}");
            }
        }

        
        public async Task<double> ObtinePretCurentAsync()
        {
            try
            {
                var response = await Client.ObtinePreturiBidAsk(Contract.Simbol);
                if (response != null && response.ContainsKey("bid"))
                {
                    return (double)response["bid"];
                }
                else
                {
                    AdaugaLog("Eroare: răspunsul de la API este null.");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                AdaugaLog($"Eroare în ObținePretCurentAsync: {ex.Message}");
                return 0;
            }
        }

     
        public async Task InchidePozitieAsync()
        {
            try
            {
                if (PozitieCurenta == null) return;

                AdaugaLog($"Închid poziția {PozitieCurenta.Tip} la prețul curent.");
                var rezultatInchidere = await Client.AnuleazaComanda(Contract, PozitieCurenta.EntryId);
                if (rezultatInchidere != null)
                {
                    AdaugaLog("Poziția a fost închisă cu succes.");
                    PozitieCurenta = null;
                }
                else
                {
                    AdaugaLog("Eșec la închiderea poziției.");
                }
            }
            catch (Exception ex)
            {
                AdaugaLog($"Eroare în InchidePozitieAsync: {ex.Message}");
            }
        }

       
        public void DeschidePozitie(int rezultatSemnal)
        {
            try
            {
                if (rezultatSemnal < 0.0)
                {
                    AdaugaLog("Semnal negativ. Nu deschidem poziții.");
                    return;
                }

                double dimensiuneTranzactie = Client.ObtineDimensiuneTranzactie(Contract, (double)Lumanari.Last().Close, ProcentBalanta);
                if (dimensiuneTranzactie == 0)
                {
                    AdaugaLog("Nu s-a putut obține dimensiunea tranzacției.");
                    return;
                }

                string tipOrdin = rezultatSemnal >= 0 ? "buy" : "sell";
                string tipPozitie = rezultatSemnal >= 0 ? "long" : "short";
                AdaugaLog($"{tipPozitie} semnal pe {Contract.Simbol} {TimpFrame}");

                var statusOrdin = Client.PlaseazaComanda(Contract, "MARKET", dimensiuneTranzactie, tipOrdin).Result;
                if (statusOrdin != null && statusOrdin.Status == "filled")
                {
                    var nouaTranzactie = new
                    {
                        Time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        PretIntrare = statusOrdin.AvgPrice,
                        Contract = Contract,
                        Strategie = NumeStrategie,
                        Tip = tipPozitie,
                        Stare = "open",
                        PnL = 0,
                        Cantitate = statusOrdin.ExecutedQty,
                        EntryId = statusOrdin.OrderId
                    };
                    Tranzactii.Add(nouaTranzactie);
                    PozitieCurenta = nouaTranzactie;
                }
            }
            catch (Exception ex)
            {
                AdaugaLog($"Eroare în DeschidePozitie: {ex.Message}");
            }
        }

      
        public void VerificaStatusComanda(int orderId)
        {
            try
            {
                var statusComanda = Client.ObtineStareaComenzii(Contract, orderId).Result;
                if (statusComanda != null)
                {
                    AdaugaLog($"{Schimb} status comanda: {statusComanda.Status}");
                    if (statusComanda.Status == "filled")
                    {
                        var tranzactie = Tranzactii.FirstOrDefault(t => t.EntryId == orderId);
                        if (tranzactie != null)
                        {
                            tranzactie.PretIntrare = statusComanda.AvgPrice;
                            tranzactie.Cantitate = statusComanda.ExecutedQty;
                            PozitieCurenta = tranzactie;
                        }
                    }
                }
                else
                {
                    AdaugaLog($"Status comanda nu a fost primit pentru {orderId}.");
                }

               
                var t = new System.Timers.Timer(2000);
                t.Elapsed += (sender, e) => VerificaStatusComanda(orderId);
                t.Start();
            }
            catch (Exception ex)
            {
                AdaugaLog($"Eroare în VerificaStatusComanda: {ex.Message}");
            }
        }

        
        public string ParseTranzactii(double price, double size, long timestamp)
        {
            try
            {
                var timestampDiff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - timestamp;
                if (timestampDiff >= 2000)
                {
                    AdaugaLog($"Diferență de {timestampDiff} milisecunde între timpul curent și timpul tranzacției.");
                }

                if (!Lumanari.Any())
                {
                    AdaugaLog("Nu există lumânări disponibile pentru procesare. Obținem lumânările istorice.");
                    ObtineLumanariIstorice();
                    return "no_candle";
                }

                var ultimaLumanare = Lumanari.Last();

                if (timestamp < ultimaLumanare.Timestamp + Echivalent_TimpFrame[TimpFrame] * 1000)
                {
                    ultimaLumanare.Close = price;
                    ultimaLumanare.Volume += size;
                    ultimaLumanare.High = Math.Max(ultimaLumanare.High, price);
                    ultimaLumanare.Low = Math.Min(ultimaLumanare.Low, price);

                    Tranzactii
                        .Where(t => t.Stare == "open" && t.PretIntrare != null)
                        .ToList()
                        .ForEach(tranzactie => VerificaTP_SL(tranzactie));

                    return "same_candle";
                }
                else if (timestamp >= ultimaLumanare.Timestamp + 2 * Echivalent_TimpFrame[TimpFrame] * 1000)
                {
                    var numarLumanariLipsa = (timestamp - ultimaLumanare.Timestamp) / (Echivalent_TimpFrame[TimpFrame] * 1000) - 1;
                    AdaugaLog($"Lipsesc {numarLumanariLipsa} lumânări pentru {Contract.Simbol}.");

                    Enumerable.Range(1, (int)numarLumanariLipsa).ToList().ForEach(_ =>
                    {
                        var tsNou = ultimaLumanare.Timestamp + Echivalent_TimpFrame[TimpFrame] * 1000;
                        var lumanareNoua = new
                        {
                            Timestamp = tsNou,
                            Open = ultimaLumanare.Close,
                            High = ultimaLumanare.Close,
                            Low = ultimaLumanare.Close,
                            Close = ultimaLumanare.Close,
                            Volume = 0
                        };
                        Lumanari.Add(lumanareNoua);
                        ultimaLumanare = lumanareNoua;
                    });

                    var tsNouLumanare = ultimaLumanare.Timestamp + Echivalent_TimpFrame[TimpFrame] * 1000;
                    var lumanareNouaFinala = new
                    {
                        Timestamp = tsNouLumanare,
                        Open = price,
                        High = price,
                        Low = price,
                        Close = price,
                        Volume = size
                    };
                    Lumanari.Add(lumanareNouaFinala);

                
                    RecalculeazaIndicatori();

                    return "new_candle";
                }
                else
                {
                    var tsNou = ultimaLumanare.Timestamp + Echivalent_TimpFrame[TimpFrame] * 1000;
                    var lumanareNoua = new
                    {
                        Timestamp = tsNou,
                        Open = price,
                        High = price,
                        Low = price,
                        Close = price,
                        Volume = size
                    };
                    Lumanari.Add(lumanareNoua);
                    AdaugaLog($"Lumânare nouă pentru {Contract.Simbol} {TimpFrame}.");

                    RecalculeazaIndicatori();

                    return "new_candle";
                }
            }
            catch (Exception ex)
            {
                AdaugaLog($"Eroare în ParseTranzactii: {ex.Message}");
                return "error";
            }
        }

      
        public void RecalculeazaIndicatori()
        {
            try
            {
                var preturi = Lumanari.Select(c => (double)c.Close).ToList();

                var emaScurta = CalculeazaEMA(preturi, 20);
                var emaLunga = CalculeazaEMA(preturi, 50);
                var kama = CalculeazaKAMA(preturi, 10);
                var alema = CalculeazaAlma(preturi, 14);
                var rsi = CalculeazaRSI(preturi, 14);

             
            }
            catch (Exception ex)
            {
                AdaugaLog($"Eroare în RecalculeazaIndicatori: {ex.Message}");
            }
        }

    
        public void VerificaTP_SL(dynamic trade)
        {
            try
            {
                var price = ObtinePretCurentAsync().Result;
                if (trade.PretIntrare == null)
                {
                    AdaugaLog("PretIntrare este null. Nu putem calcula PnL.");
                    return;
                }

                var pnl = (price - trade.PretIntrare) / trade.PretIntrare * 100;
                if (trade.Tip == "short")
                    pnl = (trade.PretIntrare - price) / trade.PretIntrare * 100;

                var potentialProfit = (Lumanari.Last().Close - Lumanari.Last().Open) / Lumanari.Last().Open * 100;
                AdaugaLog($"Calcul PnL pentru poziție {trade.Tip}: {pnl:.2f}%");

                if (EvaluareInchiderePozitie(pnl, potentialProfit))
                    InchidePozitieAsync();
            }
            catch (Exception ex)
            {
                AdaugaLog($"Eroare în VerificaTP_SL: {ex.Message}");
            }
        }

      
        public bool EvaluareInchiderePozitie(double pnl, double potentialProfit)
        {
            var fees = CalculeazaComisioane(Contract.Simbol);
            var pnlNet = pnl - fees;
            AdaugaLog($"Calcul PnL net (după comisioane): {pnlNet:.2f}%");

            if (pnlNet < 0) return false;

            var diferenta = pnlNet - potentialProfit;
            return diferenta > 0;
        }

    
        public double CalculeazaComisioane(string simbol)
        {
            try
            {
                var tranzactii = Client.ObtineTranzactiiCont(simbol).Result;
                if (tranzactii == null || tranzactii.Count == 0) return 0;

                var totalFeesInBase = tranzactii.Sum(t => t.Comision);
                var pretCurent = ObtinePretCurentAsync().Result;
                return totalFeesInBase * pretCurent;
            }
            catch (Exception ex)
            {
                AdaugaLog($"Eroare în CalculeazaComisioane: {ex.Message}");
                return 0;
            }
        }

       
        public virtual async void ConectareWebSocketAsync()
        {
            try
            {
                var contracte = new List<Contract> { Contract };
                await Client.AbonareCanal(contracte, "trade");
                await AscultareWebSocketAsync();
            }
            catch (Exception ex)
            {
                AdaugaLog($"Eroare la conectarea WebSocket: {ex.Message}");
            }
        }

       
        public async Task AscultareWebSocketAsync()
        {
            var buffer = new byte[1024 * 4];
            var segment = new ArraySegment<byte>(buffer);

            try
            {
                while (WebSocketClient.State == WebSocketState.Open)
                {
                    var result = await WebSocketClient.ReceiveAsync(segment, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var json = Encoding.UTF8.GetString(segment.Array, 0, result.Count);
                        dynamic trade = JsonConvert.DeserializeObject(json);
                        ParseTranzactii((double)trade.p, (double)trade.q, (long)trade.T);
                    }
                }
            }
            catch (Exception ex)
            {
                AdaugaLog($"Eroare la recepționarea datelor WebSocket: {ex.Message}");
            }
        }

        public async Task<JArray> ColecteazaDateBinanceAsync()
        {
            try
            {
                var raspuns = await httpClient.GetAsync("https://testnet.binancefuture.com/fapi/v1/ticker/bookTicker");
                if (raspuns.IsSuccessStatusCode)
                {
                    var raspunsJson = await raspuns.Content.ReadAsStringAsync();
                    return JArray.Parse(raspunsJson);
                }
            }
            catch (Exception e)
            {
                AdaugaLog($"Eroare la colectarea datelor de la Binance: {e.Message}");
            }

            return null;
        }

        public async Task<JObject> ColecteazaDateCoingeckoAsync()
        {
            try
            {
                var raspuns = await httpClient.GetAsync("https://api.coingecko.com/api/v3/simple/price?ids=bitcoin&vs_currencies=usd");
                if (raspuns.IsSuccessStatusCode)
                {
                    var raspunsJson = await raspuns.Content.ReadAsStringAsync();
                    return JObject.Parse(raspunsJson);
                }
            }
            catch (Exception e)
            {
                AdaugaLog($"Eroare la colectarea datelor de la CoinGecko: {e.Message}");
            }

            return null;
        }

        public List<dynamic> SelecteazaDateFavorabile(JArray dateBinance, JObject dateCoingecko)
        {
            var caracteristici = new List<dynamic>();

            if (dateBinance != null)
                caracteristici.AddRange(ProceseazaDateBinance(dateBinance));

            if (dateCoingecko != null)
                caracteristici.AddRange(ProceseazaDateCoingecko(dateCoingecko));

            var caracteristiciFavorabile = caracteristici.Where(f => f.PretAsk >= 0.00).ToList();

            if (!caracteristiciFavorabile.Any())
            {
                AdaugaLog("Nu există date favorabile pentru tranzacționare.");
                return null;
            }

            AdaugaLog($"Date favorabile selectate: {string.Join(", ", caracteristiciFavorabile)}");
            return caracteristiciFavorabile;
        }

        private List<dynamic> ProceseazaDateBinance(JArray dateBinance)
        {
            return dateBinance
                .Select(b => new { PretAsk = (double)b["askPrice"], PretBid = (double)b["bidPrice"] })
                .ToList<dynamic>();
        }

        private List<dynamic> ProceseazaDateCoingecko(JObject dateCoingecko)
        {
            return dateCoingecko["bitcoin"]
                .Children()
                .Select(c => new { Pret = (double)c.First["usd"] })
                .ToList<dynamic>();
        }
    }
}
