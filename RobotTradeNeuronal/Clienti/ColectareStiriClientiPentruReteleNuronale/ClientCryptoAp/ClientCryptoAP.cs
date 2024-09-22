using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RobotTradeNeuronal.Clienti.ColectareStiriClienti.ClientCryptoAp
{
    public class ClientCryptoAPI
    {
        private const string URL_BAZA = "https://api.alternative.me/v2";
        private static readonly HttpClient client = new HttpClient();

        public ClientCryptoAPI()
        {

        }


        public async Task<List<object>> ObtineListariAsync()
        {
            var endpoint = $"{URL_BAZA}/listings/";
            try
            {
                var raspuns = await client.GetAsync(endpoint);
                raspuns.EnsureSuccessStatusCode();
                var jsonResponse = await raspuns.Content.ReadAsStringAsync();

                var date = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);


                var criptomonede = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(date["data"].ToString());


                var listariTransformate = criptomonede.Select(c => new
                {
                    Id = (int)(long)c["id"],
                    Nume = c["name"].ToString(),
                    Simbol = c["symbol"].ToString()
                }).ToList();

                Console.WriteLine("Listari de criptomonede obtinute cu succes.");
                return listariTransformate.Cast<object>().ToList();
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Eroare la obtinerea listarilor: {e.Message}");
                return null;
            }
        }

       
        public async Task<List<object>> ObtineTickerAsync(int limita = 100, int start = 1, string conversie = "USD", string sortare = "rank", string structura = "dictionary")
        {
            var endpoint = $"{URL_BAZA}/ticker/";
            var parametrii = new Dictionary<string, string>
            {
                { "limit", limita.ToString() },
                { "start", start.ToString() },
                { "convert", conversie },
                { "sort", sortare },
                { "structure", structura }
            };

            var queryParams = string.Join("&", parametrii.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            var urlCompleta = $"{endpoint}?{queryParams}";

            try
            {
                var raspuns = await client.GetAsync(urlCompleta);
                raspuns.EnsureSuccessStatusCode();
                var jsonResponse = await raspuns.Content.ReadAsStringAsync();

                var date = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);
                var criptomonede = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(date["data"].ToString());

               
                var tickerSortat = criptomonede.OrderBy(c => (int)(long)c["rank"]).Select(c => new
                {
                    Id = (int)(long)c["id"], 
                    Nume = c["name"].ToString(),
                    Pret = Convert.ToDecimal(((Dictionary<string, object>)((Dictionary<string, object>)c["quotes"])[conversie])["price"]), // Conversie la decimal
                    Schimbare24h = Convert.ToDecimal(((Dictionary<string, object>)((Dictionary<string, object>)c["quotes"])[conversie])["percent_change_24h"])
                }).ToList();

                Console.WriteLine("Date despre ticker-ul criptomonedelor obtinute cu succes.");
                return tickerSortat.Cast<object>().ToList();
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Eroare la obtinerea ticker-ului: {e.Message}");
                return null;
            }
        }

       
        public async Task<object> ObtineTickerSpecificAsync(string idSauNume, string conversie = "USD", string structura = "dictionary")
        {
            var endpoint = $"{URL_BAZA}/ticker/{idSauNume}/";
            var parametrii = new Dictionary<string, string>
            {
                { "convert", conversie },
                { "structure", structura }
            };

            var queryParams = string.Join("&", parametrii.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            var urlCompleta = $"{endpoint}?{queryParams}";

            try
            {
                var raspuns = await client.GetAsync(urlCompleta);
                raspuns.EnsureSuccessStatusCode();
                var jsonResponse = await raspuns.Content.ReadAsStringAsync();

                var date = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);
                var criptomoneda = ((List<object>)date["data"]).Cast<Dictionary<string, object>>().FirstOrDefault();

                var criptomonedaTransformata = new
                {
                    Nume = criptomoneda["name"].ToString(),
                    Pret = Convert.ToDecimal(((Dictionary<string, object>)((Dictionary<string, object>)criptomoneda["quotes"])[conversie])["price"]),
                    Schimbare24h = Convert.ToDecimal(((Dictionary<string, object>)((Dictionary<string, object>)criptomoneda["quotes"])[conversie])["percent_change_24h"])
                };

                Console.WriteLine($"Date despre ticker-ul specific '{idSauNume}' obtinute cu succes.");
                return criptomonedaTransformata;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Eroare la obtinerea ticker-ului specific: {e.Message}");
                return null;
            }
        }

       
        public async Task<object> ObtineInformatiiGlobaleAsync(string conversie = "USD")
        {
            var endpoint = $"{URL_BAZA}/global/";
            var parametrii = new Dictionary<string, string>
            {
                { "convert", conversie }
            };

            var queryParams = string.Join("&", parametrii.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            var urlCompleta = $"{endpoint}?{queryParams}";

            try
            {
                var raspuns = await client.GetAsync(urlCompleta);
                raspuns.EnsureSuccessStatusCode();
                var jsonResponse = await raspuns.Content.ReadAsStringAsync();
                var date = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonResponse);

                Console.WriteLine("Informatii globale despre piata criptomonedelor obtinute cu succes.");
                return date["data"];
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Eroare la obtinerea informatiilor globale: {e.Message}");
                return null;
            }
        }
    }
}
