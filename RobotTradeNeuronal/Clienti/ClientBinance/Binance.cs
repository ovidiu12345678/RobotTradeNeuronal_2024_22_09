using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;

namespace RobotTradeNeuronal.Clienti.ClientBinance
{
    internal class Binance
    {
        public string CheiePublica { get; private set; }
        public string CheieSecreta { get; private set; }
        private bool futures;
        private string urlBaza;
        private Dictionary<string, Contract> contracte;
        private HttpClient client;
        private int idWebSocket = 1;
     
      
        private ClientWebSocket WebSocketClient;

        public Binance(string cheiePublica = null, string cheieSecreta = null, bool testnet = true, bool futures = false)
        {
            try
            {
                CheiePublica = cheiePublica;
                CheieSecreta = cheieSecreta;
                this.futures = futures;

                urlBaza = futures
                    ? testnet ? "https://testnet.binancefuture.com" : "https://fapi.binance.com"
                    : testnet ? "https://testnet.binance.vision" : "https://api.binance.com";

                contracte = new Dictionary<string, Contract>();
                client = new HttpClient();
                WebSocketClient = new ClientWebSocket(); 
                if (CheiePublica != null)
                {
                    client.DefaultRequestHeaders.Add("X-MBX-APIKEY", CheiePublica);
                }

                Console.WriteLine($"BinanceClient inițializat pe {(testnet ? "Testnet" : "Piața reală")} {(futures ? "Futures" : "Spot")}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Eroare la inițializarea clientului Binance: {ex.Message}");
            }
        }

       
        public void SeteazaCheile(string cheiePublica, string cheieSecreta)
        {
            try
            {
                CheiePublica = cheiePublica;
                CheieSecreta = cheieSecreta;
                client.DefaultRequestHeaders.Add("X-MBX-APIKEY", CheiePublica);
                Console.WriteLine("Cheile API au fost setate.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Eroare la setarea cheilor API: {ex.Message}");
            }
        }

        private string GenereazaSemnatura(Dictionary<string, string> date)
        {
            try
            {
                return new HMACSHA256(Encoding.UTF8.GetBytes(CheieSecreta))
                    .ComputeHash(Encoding.UTF8.GetBytes(string.Join("&", date.Select(kv => $"{kv.Key}={kv.Value}"))))
                    .Aggregate(new StringBuilder(), (sb, b) => sb.Append(b.ToString("x2")))
                    .ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Eroare la generarea semnăturii: {ex.Message}");
                return string.Empty;
            }
        }

        private async Task<dynamic> TrimiteCerere(string metoda, string endpoint, Dictionary<string, string> date = null)
        {
            try
            {
                var url = $"{urlBaza}{endpoint}{(date != null ? $"?{string.Join("&", date.Select(kv => $"{kv.Key}={kv.Value}"))}" : "")}";
                var raspuns = await client.GetAsync(url);
                if (!raspuns.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Eroare: {raspuns.StatusCode}");
                    return null;
                }
                var json = await raspuns.Content.ReadAsStringAsync();
                Console.WriteLine($"Cerere {metoda} la {url} efectuată cu succes.");
                return JsonConvert.DeserializeObject(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Eroare la trimiterea cererii: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> ValideazaCheileApi()
        {
            try
            {
                var date = new Dictionary<string, string>
            {
                { "timestamp", DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString() }
            };
                date["signature"] = GenereazaSemnatura(date);

                var raspuns = await TrimiteCerere("GET", futures ? "/fapi/v2/account" : "/api/v3/account", date);
                if (raspuns != null && (raspuns.assets != null || raspuns.balances != null))
                {
                    Console.WriteLine("Cheile API sunt valide.");
                    return true;
                }

                Console.WriteLine("Cheile API sunt invalide.");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Eroare la validarea cheilor API: {ex.Message}");
                return false;
            }
        }

        public async Task<Dictionary<string, Contract>> ObtineContracte()
        {
            try
            {
                var infoExchange = await TrimiteCerere("GET", futures ? "/fapi/v1/exchangeInfo" : "/api/v3/exchangeInfo");
                contracte = ((IEnumerable<dynamic>)infoExchange.symbols)
                    .ToDictionary(
                        c => (string)c.symbol,
                        c => new Contract(c, futures)
                    );
                Console.WriteLine($"Contracte obținute: {contracte?.Count ?? 0}");
                return contracte;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Eroare la obținerea contractelor: {ex.Message}");
                return new Dictionary<string, Contract>();
            }
        }


        public async Task<Dictionary<string, double>> ObtinePreturiBidAsk(string simbol)
        {
            try
            {
                var date = await TrimiteCerere("GET", futures ? "/fapi/v1/ticker/bookTicker" : "/api/v3/ticker/bookTicker", new Dictionary<string, string> { { "symbol", simbol } });
                if (date != null)
                {
                    double bidPrice, askPrice;

               
                    bool bidValid = double.TryParse(date.bidPrice?.ToString(), out bidPrice);
                    bool askValid = double.TryParse(date.askPrice?.ToString(), out askPrice);

                    if (bidValid && askValid)
                    {
                        var preturi = new Dictionary<string, double>
                {
                    { "bid", bidPrice },
                    { "ask", askPrice }
                };
                        Console.WriteLine($"Prețuri bid/ask pentru {simbol}: bid={preturi["bid"]}, ask={preturi["ask"]}");
                        return preturi;
                    }
                    else
                    {
                        Console.WriteLine($"Eroare: Valorile bid sau ask nu sunt valide pentru simbolul {simbol}.");
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Eroare la obținerea prețurilor bid/ask pentru {simbol}: {ex.Message}");
                return null;
            }
        }



        public async Task<List<dynamic>> ObtineLumanariIstorice(string simbol, string interval = "1m")
        {
            try
            {
                var lumanari = await TrimiteCerere("GET", futures ? "/fapi/v1/klines" : "/api/v3/klines", new Dictionary<string, string>
            {
                { "symbol", simbol },
                { "interval", interval },
                { "limit", "100" }
            });

                if (lumanari != null)
                {
                    Console.WriteLine($"Lumânări istorice obținute pentru {simbol}, interval {interval}");
                    return ((IEnumerable<dynamic>)lumanari).ToList();
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Eroare la obținerea lumânărilor istorice pentru {simbol}: {ex.Message}");
                return null;
            }
        }

        public async Task<Dictionary<string, Sold>> ObtineSolduri()
        {
            try
            {
                var endpoint = futures ? "/fapi/v2/account" : "/api/v3/account";
                var date = new Dictionary<string, string> { { "timestamp", DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString() } };
                date["signature"] = GenereazaSemnatura(date);

                var dateCont = await TrimiteCerere("GET", endpoint, date);

                if (dateCont != null)
                {
                    IEnumerable<dynamic> active = futures ? dateCont.assets : dateCont.balances;

                    if (active != null)
                    {
                        var solduri = active.ToDictionary(
                            a => (string)a.asset,
                            a => new Sold(a, futures)
                        );
                        Console.WriteLine("Soldurile au fost obținute.");
                        return solduri;
                    }
                    else
                    {
                        Console.WriteLine("Nu există active sau balanțe disponibile.");
                    }
                }
                else
                {
                    Console.WriteLine("Eroare la obținerea contului: datele returnate sunt null.");
                }

                return new Dictionary<string, Sold>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Eroare la obținerea soldurilor: {ex.Message}");
                return new Dictionary<string, Sold>();
            }
        }

        public async Task<dynamic> PlaseazaComanda(Contract contract, string tipComanda, double cantitate, string directie, double? pret = null, string tif = "GTC")
        {
            try
            {
                var dateComanda = new Dictionary<string, string>
            {
                { "symbol", contract.Simbol },
                { "side", directie.ToUpper() },
                { "type", tipComanda.ToUpper() },
                { "quantity", Math.Round(cantitate, contract.PrecizieCantitate).ToString() },
                { "timestamp", DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString() }
            };

                if (pret.HasValue)
                {
                    dateComanda["price"] = Math.Round(pret.Value, contract.PreciziePret).ToString($"F{contract.PreciziePret}");
                    dateComanda["timeInForce"] = tif;
                }

                dateComanda["signature"] = GenereazaSemnatura(dateComanda);
                var raspuns = await TrimiteCerere("POST", futures ? "/fapi/v1/order" : "/api/v3/order", dateComanda);
                if (raspuns != null)
                {
                    Console.WriteLine($"Comanda {tipComanda} plasată: {JsonConvert.SerializeObject(raspuns)}");
                }
                return raspuns;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Eroare la plasarea comenzii: {ex.Message}");
                return null;
            }
        }

        public async Task AbonareCanal(List<Contract> contracte, string canal, bool reconectare = false)
        {
            try
            {
                if (contracte.Count > 200)
                {
                    Console.WriteLine("Abonarea la mai mult de 200 de simboluri poate eșua. Vă rugăm să reduceți numărul de simboluri.");
                }

                var date = new
                {
                    metoda = "SUBSCRIBE",
                    @parametri = new List<string>(),
                    id = idWebSocket++
                };

                foreach (var contract in contracte)
                {
                    string abonare = $"{contract.Simbol.ToLower()}@{canal}";
                    date.@parametri.Add(abonare);
                }

                if (date.@parametri.Count > 0)
                {
                    string jsonDate = JsonConvert.SerializeObject(date);

                    try
                    {
                        var buffer = Encoding.UTF8.GetBytes(jsonDate);
                        var segment = new ArraySegment<byte>(buffer);
                        await WebSocketClient.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None); // Folosind ClientWebSocket
                        Console.WriteLine($"Abonare la canal pentru {string.Join(", ", date.@parametri)} reușită.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Eroare WebSocket la abonare: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Eroare la abonarea canalului: {ex.Message}");
            }
        }

        public double ObtineDimensiuneTranzactie(Contract contract, double pret, double procentBalanta)
        {
            try
            {
                Console.WriteLine("Obține dimensiune tranzacție...");

                var solduri = ObtineSolduri().Result;

                if (solduri != null && solduri.ContainsKey(contract.ActivCotat))
                {
                    var sold = futures
                        ? solduri[contract.ActivCotat].Blocat + solduri[contract.ActivCotat].Liber
                        : solduri[contract.ActivCotat].Liber;

                    var dimensiuneTranzactie = sold * procentBalanta / 100 / pret;
                    dimensiuneTranzactie = Math.Round(dimensiuneTranzactie / contract.MarimeLot) * contract.MarimeLot;

                    Console.WriteLine($"Sold curent pentru {contract.ActivCotat} = {sold}, dimensiune tranzacție = {dimensiuneTranzactie}");

                    return dimensiuneTranzactie;
                }
                else
                {
                    Console.WriteLine($"Nu există sold disponibil pentru activul cotat {contract.ActivCotat}.");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Eroare la calcularea dimensiunii tranzacției: {ex.Message}");
                return 0;
            }
        }

        public async Task<List<dynamic>> ObtineTranzactiiCont(string simbol, long timpInceput = 0, long timpSfarsit = 0, int limita = 500)
        {
            try
            {
                var date = new Dictionary<string, string>
            {
                { "symbol", simbol },
                { "limit", limita.ToString() },
                { "timestamp", DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString() }
            };

                if (timpInceput > 0)
                {
                    date["startTime"] = timpInceput.ToString();
                }
                if (timpSfarsit > 0)
                {
                    date["endTime"] = timpSfarsit.ToString();
                }

                date["signature"] = GenereazaSemnatura(date);

                var tranzactii = await TrimiteCerere("GET", "/fapi/v1/userTrades", date);

                if (tranzactii != null && tranzactii is IEnumerable<dynamic>)
                {
                    Console.WriteLine($"Tranzacții obținute pentru simbolul {simbol}: {tranzactii.Count()} tranzacții.");

                    return tranzactii.ToList();
                }
                else
                {
                    Console.WriteLine($"Eroare la obținerea tranzacțiilor pentru simbolul {simbol}.");
                    return new List<dynamic>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Eroare la obținerea tranzacțiilor din cont: {ex.Message}");
                return new List<dynamic>();
            }
        }

        public async Task<dynamic> ObtineStareaComenzii(Contract contract, int idComanda)
        {
            var date = new Dictionary<string, string>
            {
                { "symbol", contract.Simbol },
                { "orderId", idComanda.ToString() },
                { "timestamp", DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString() }
            };
            date["signature"] = GenereazaSemnatura(date);

            var endpoint = futures ? "/fapi/v1/order" : "/api/v3/order";
            var stareComanda = await TrimiteCerere("GET", endpoint, date);

            if (stareComanda != null)
            {
                if (!futures)
                {
                    if (stareComanda["status"] == "FILLED")
                    {
                        stareComanda["pretMediu"] = await ObtinePretExecutie(contract, idComanda);
                    }
                    else
                    {
                        stareComanda["pretMediu"] = 0;
                    }
                }
                return stareComanda;
            }

            return null;
        }

        public async Task<double> ObtinePretExecutie(Contract contract, int idComanda)
        {
            var date = new Dictionary<string, string>
            {
                { "symbol", contract.Simbol },
                { "timestamp", DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString() }
            };
            date["signature"] = GenereazaSemnatura(date);

            var tranzactii = await TrimiteCerere("GET", "/api/v3/myTrades", date);

            double pretMediu = 0;
            if (tranzactii != null)
            {
                double cantitateExecutata = 0;
                foreach (var t in tranzactii)
                {
                    if ((int)t["orderId"] == idComanda)
                    {
                        cantitateExecutata += double.Parse((string)t["qty"]);
                    }
                }

                foreach (var t in tranzactii)
                {
                    if ((int)t["orderId"] == idComanda)
                    {
                        double procent = double.Parse((string)t["qty"]) / cantitateExecutata;
                        pretMediu += double.Parse((string)t["price"]) * procent;
                    }
                }
            }

            return Math.Round(pretMediu / contract.PreciziePasPret) * contract.PreciziePasPret;
        }

        public async Task<dynamic> AnuleazaComanda(Contract contract, int idComanda)
        {
            var date = new Dictionary<string, string>
            {
                { "orderId", idComanda.ToString() },
                { "symbol", contract.Simbol },
                { "timestamp", DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString() }
            };
            date["signature"] = GenereazaSemnatura(date);

            var endpoint = futures ? "/fapi/v1/order" : "/api/v3/order";
            var stareComanda = await TrimiteCerere("DELETE", endpoint, date);

            if (stareComanda != null)
            {
                if (!futures)
                {
                    stareComanda["pretMediu"] = await ObtinePretExecutie(contract, idComanda);
                }
                return stareComanda; // Returnează starea comenzii după anulare
            }

            return null;
        }

        public async Task<Dictionary<string, double>> ObtineInformatiiComision()
        {
            try
            {
                var endpoint = futures ? "/fapi/v2/account" : "/api/v3/account";
                var date = new Dictionary<string, string> { { "timestamp", DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString() } };
                date["signature"] = GenereazaSemnatura(date);

                var infoCont = await TrimiteCerere("GET", endpoint, date);

                if (infoCont != null)
                {
                    double rataReducereBnb = infoCont.canTrade && infoCont.makerCommission > 0 ? 0.75 : 1.0;
                    double comisionMaker = infoCont.makerCommission / 10000.0;
                    double comisionTaker = infoCont.takerCommission / 10000.0;

                    Console.WriteLine("Informații despre comision obținute cu succes.");

                    return new Dictionary<string, double>
                {
                    { "reducereBnb", rataReducereBnb },
                    { "comisionMaker", comisionMaker },
                    { "comisionTaker", comisionTaker }
                };
                }
                else
                {
                    Console.WriteLine("Eroare la obținerea informațiilor despre comision.");
                    return new Dictionary<string, double> { { "reducereBnb", 1.0 }, { "comisionMaker", 0.001 }, { "comisionTaker", 0.001 } };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Eroare la obținerea informațiilor despre comision: {ex.Message}");
                return new Dictionary<string, double> { { "reducereBnb", 1.0 }, { "comisionMaker", 0.001 }, { "comisionTaker", 0.001 } };
            }
        }
    }
}
