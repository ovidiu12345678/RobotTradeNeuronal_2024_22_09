
using Microsoft.ML;
using Microsoft.ML.Data;
using RobotTradeNeuronal.Clienti.ClientBinance;
using RobotTradeNeuronal.DezvoltareStrategii.ClasaStrategyParinte;

namespace RobotTradeNeuronal.DezvoltareStrategii.StrategieMarkov
{
    internal class Markov : Strategy
    {
        private double[,] matriceMarkov;
        private MLContext mlContext;
        private ITransformer modelML;
        private ITransformer modelDL;

        private Binance client;
        private RobotTradeNeuronal.Clienti.ClientBinance.Contract contract;
        private string timpFrame;

        [LoadColumn(0)]
        public float[] Features { get; set; }

        [LoadColumn(1)]
        public float Label { get; set; }

        public float PredictedLabel { get; set; }

        public Markov()
            : base(null, null, "", "", 0, 0, 0, "")
        {
            AdaugaLog("Inițializare strategie Markov fără parametri.");
            mlContext = new MLContext();
            ConectareWebSocketAsync();
            InitializareMatriceMarkov();
            modelML = ConstruireModelML();
            modelDL = ConstruireModelDL();
            AdaugaLog("Inițializare completă a strategiei Markov fără parametri.");
        }

        public Markov(Binance client,
                      RobotTradeNeuronal.Clienti.ClientBinance.Contract contract,
                      string schimb,
                      string timpFrame,
                      double procentBalanta,
                      double profitTinta,
                      double stopPierdere,
                      string numeStrategie)
            : base(client, contract, schimb, timpFrame, procentBalanta, profitTinta, stopPierdere, numeStrategie)
        {
            this.client = client;
            this.contract = contract;
            this.timpFrame = timpFrame;

            AdaugaLog($"Inițializare strategie Markov pentru contractul {contract.Simbol} pe timeframe-ul {timpFrame}.");

            mlContext = new MLContext();
            ConectareWebSocketAsync();
            InitializareMatriceMarkov();
            modelML = ConstruireModelML();
            modelDL = ConstruireModelDL();

            AdaugaLog("Inițializare completă a strategiei Markov.");
        }

        private void InitializareMatriceMarkov()
        {
            AdaugaLog("Începem inițializarea matricei Markov...");

            var dateHistoriceTask = ColecteazaDateHistoriceAsync();
            dateHistoriceTask.Wait();
            var dateHistorice = dateHistoriceTask.Result;

            int numarStari = 6;

            var stari = MaparePreturiLaStari(dateHistorice, numarStari);

            matriceMarkov = CalculareMatriceMarkov(stari, numarStari);

            AdaugaLog("Matricea Markov a fost inițializată cu succes.");
        }

        private async Task<List<double>> ColecteazaDateHistoriceAsync()
        {
            AdaugaLog("Colectăm datele istorice de preț...");

            var lumanari = await client.ObtineLumanariIstorice(contract.Simbol, timpFrame);

            if (lumanari != null)
            {
                AdaugaLog($"Datele istorice de preț au fost colectate cu succes. Număr de înregistrări: {lumanari.Count}");
                return lumanari.Select(lumanare => (double)lumanare[4]).ToList();
            }
            else
            {
                AdaugaLog("Eroare la colectarea datelor istorice de preț.");
                return new List<double>();
            }
        }

        private List<int> MaparePreturiLaStari(List<double> preturi, int numarStari)
        {
            AdaugaLog("Mapăm prețurile la stări discrete pentru matricea Markov...");

            double pretMin = preturi.Min();
            double pretMax = preturi.Max();
            double interval = (pretMax - pretMin) / numarStari;

            var stari = preturi
                .Select(pret => (int)((pret - pretMin) / interval))
                .Select(stare => Math.Min(stare, numarStari - 1))
                .ToList();

            AdaugaLog($"Maparea prețurilor la {numarStari} stări a fost finalizată.");

            return stari;
        }

        private double[,] CalculareMatriceMarkov(List<int> stari, int numarStari)
        {
            AdaugaLog("Calculăm matricea de tranziție Markov...");

            var tranzitii = new double[numarStari, numarStari];

            var grupareTranzitii = stari
                .Zip(stari.Skip(1), (stareCurenta, stareUrmatoare) => new { StareCurenta = stareCurenta, StareUrmatoare = stareUrmatoare })
                .GroupBy(t => new { t.StareCurenta, t.StareUrmatoare })
                .Select(g => new
                {
                    g.Key.StareCurenta,
                    g.Key.StareUrmatoare,
                    Frecventa = g.Count()
                })
                .ToList();

            var sumaTranzitii = grupareTranzitii
                .GroupBy(t => t.StareCurenta)
                .Select(g => new
                {
                    Stare = g.Key,
                    Suma = g.Sum(t => t.Frecventa)
                })
                .ToDictionary(t => t.Stare, t => t.Suma);

            grupareTranzitii.ForEach(tranzitie =>
            {
                tranzitii[tranzitie.StareCurenta, tranzitie.StareUrmatoare] =
                    (double)tranzitie.Frecventa / sumaTranzitii[tranzitie.StareCurenta];
            });

            AdaugaLog("Matricea de tranziție Markov a fost calculată cu succes.");

            return tranzitii;
        }

        private ITransformer ConstruireModelML()
        {
            AdaugaLog("Construim modelul de învățare automată (ML)...");

            var trainingData = PregatireDateAntrenamentML();
            var dataView = mlContext.Data.LoadFromEnumerable(trainingData);

            var pipeline = mlContext.Transforms.Concatenate("Features", nameof(Markov.Features))
                            .Append(mlContext.Transforms.NormalizeMinMax("Features"))
                            .Append(mlContext.Transforms.Conversion.MapValueToKey(nameof(Markov.Label)))
                            .Append(mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(
                                labelColumnName: nameof(Markov.Label),
                                featureColumnName: "Features"))
                            .Append(mlContext.Transforms.Conversion.MapKeyToValue(nameof(Markov.PredictedLabel)));

            var model = pipeline.Fit(dataView);

            AdaugaLog("Modelul de învățare automată (ML) a fost construit cu succes.");

            return model;
        }

        private IEnumerable<Markov> PregatireDateAntrenamentML()
        {
            AdaugaLog("Pregătim datele de antrenament pentru modelul ML...");

            var dateHistoriceTask = ColecteazaDateHistoriceAsync();
            dateHistoriceTask.Wait();
            var dateHistorice = dateHistoriceTask.Result;

            var trainingData = dateHistorice
                .Select((pret, index) => new { pret, index })
                .Where(x => x.index <= dateHistorice.Count - 6)
                .Select(x => new Markov
                {
                    Features = dateHistorice.Skip(x.index).Take(5).Select(p => (float)p).ToArray(),
                    Label = dateHistorice[x.index + 5] > x.pret ? 1f : 0f
                });

            AdaugaLog($"Pregătirea datelor de antrenament a fost finalizată. Număr de instanțe: {trainingData.Count()}.");

            return trainingData;
        }

        private ITransformer ConstruireModelDL()
        {
            AdaugaLog("Construim modelul de învățare profundă (DL)...");

            var pipelineDL = mlContext.Transforms.Concatenate("Features", nameof(Markov.Features))
                            .Append(mlContext.Transforms.NormalizeMinMax("Features"))
                            .Append(mlContext.Model.LoadTensorFlowModel("path_to_tensorflow_model")
                            .ScoreTensorFlowModel(
                                inputColumnNames: new[] { "Features" },
                                outputColumnNames: new[] { "PredictedLabel" },
                                addBatchDimensionInput: true));

            var model = pipelineDL.Fit(mlContext.Data.LoadFromEnumerable(new List<Markov>()));

            AdaugaLog("Modelul de învățare profundă (DL) a fost construit cu succes.");

            return model;
        }

        private float CombinaPredictii(float predML, float predDL)
        {
            float predictieCombinata = new[] { predML, predDL }.Average();
            AdaugaLog($"Predicția ML: {predML}, Predicția DL: {predDL}, Predicția combinată: {predictieCombinata}");
            return predictieCombinata;
        }

        private List<dynamic> IdentificaOrderBlocks(List<dynamic> lumanari)
        {
            AdaugaLog("Identificăm blocurile de ordine în datele istorice...");

            var orderBlocks = lumanari
                .Skip(1)
                .Where((lumanare, i) => lumanare.High > lumanari[i].High && lumanare.Low > lumanari[i].Low)
                .ToList();

            AdaugaLog($"Am identificat {orderBlocks.Count} blocuri de ordine.");

            return orderBlocks;
        }

        private bool AreImbalance(dynamic lumanare, dynamic lumanareAnterioara)
        {
            double diferenta = Math.Abs((double)lumanare.Close - (double)lumanareAnterioara.Close);
            bool imbalance = diferenta > 0.01 * (double)lumanareAnterioara.Close;

            AdaugaLog($"Verificare dezechilibru între lumânări: {imbalance}");

            return imbalance;
        }

        public async Task MonitorizeazaSiDeschidePozitii(List<float[]> dateInput, List<dynamic> lumanari)
        {
            AdaugaLog("Începem monitorizarea pentru deschiderea de poziții...");

            AnalizeazaSiVizualizeazaTrendulPietei();

            var tasks = dateInput.Select(input =>
            {
                var predictionEngineML = mlContext.Model.CreatePredictionEngine<Markov, Markov>(modelML);
                var predictionEngineDL = mlContext.Model.CreatePredictionEngine<Markov, Markov>(modelDL);

                Features = input;
                var rezultatML = predictionEngineML.Predict(this);
                var rezultatDL = predictionEngineDL.Predict(this);

                PredictedLabel = CombinaPredictii(rezultatML.PredictedLabel, rezultatDL.PredictedLabel);

                AdaugaLog($"Predicție combinată: {PredictedLabel}");

                if (PredictedLabel >= 0.5)
                {
                    AdaugaLog("Semnal detectat: poziție LONG.");
                    DeschidePozitie(1); // Long
                }
                else
                {
                    AdaugaLog("Semnal detectat: poziție SHORT.");
                    DeschidePozitie(-1); // Short
                }

                return Task.CompletedTask;
            });

            await Task.WhenAll(tasks);

            AdaugaLog("Monitorizarea pentru deschiderea de poziții a fost finalizată.");
        }

        public async Task MonitorizeazaSiInchidePozitii(List<float[]> dateInput, List<dynamic> lumanari)
        {
            AdaugaLog("Începem monitorizarea pentru închiderea de poziții...");

            var tasks = dateInput.Select(async input =>
            {
                var predictionEngineML = mlContext.Model.CreatePredictionEngine<Markov, Markov>(modelML);
                var predictionEngineDL = mlContext.Model.CreatePredictionEngine<Markov, Markov>(modelDL);

                Features = input;
                var rezultatML = predictionEngineML.Predict(this);
                var rezultatDL = predictionEngineDL.Predict(this);

                PredictedLabel = CombinaPredictii(rezultatML.PredictedLabel, rezultatDL.PredictedLabel);

                AdaugaLog($"Predicție combinată: {PredictedLabel}");

                if (PredictedLabel < 0.5 && PozitieCurenta != null && PozitieCurenta.Tip == "long")
                {
                    AdaugaLog("Semnal pentru închiderea poziției LONG curente.");
                    await InchidePozitieAsync();
                }
                else if (PredictedLabel >= 0.5 && PozitieCurenta != null && PozitieCurenta.Tip == "short")
                {
                    AdaugaLog("Semnal pentru închiderea poziției SHORT curente.");
                    await InchidePozitieAsync();
                }
                else
                {
                    AdaugaLog("Niciun semnal pentru închiderea poziției curente.");
                }
            });

            await Task.WhenAll(tasks);

            AdaugaLog("Monitorizarea pentru închiderea de poziții a fost finalizată.");
        }

        public async Task MonitorizeazaMarkovSiOptimizeaza()
        {
            AdaugaLog("Monitorizăm strategia Markov și optimizăm parametrii...");

            await MonitorizeazaSiOptimizeazaAsync();

            InitializareMatriceMarkov();

            AdaugaLog("Optimizarea strategiei Markov a fost finalizată.");
        }

        public void FolosesteVerificaStatusComanda(int orderId)
        {
            AdaugaLog($"Verificăm statusul comenzii cu ID-ul {orderId}...");
            VerificaStatusComanda(orderId);
        }

        public void FolosesteVerificaTP_SL()
        {
            if (PozitieCurenta != null)
            {
                AdaugaLog("Verificăm Take Profit și Stop Loss pentru poziția curentă...");
                VerificaTP_SL(PozitieCurenta);
            }
            else
            {
                AdaugaLog("Nu există nicio poziție deschisă pentru a verifica TP/SL.");
            }
        }

        public async Task FolosesteSelecteazaDateFavorabile()
        {
            AdaugaLog("Selectăm date favorabile pentru tranzacționare...");

            var dateBinanceTask = ColecteazaDateBinanceAsync();
            var dateCoingeckoTask = ColecteazaDateCoingeckoAsync();

            await Task.WhenAll(dateBinanceTask, dateCoingeckoTask);

            var dateBinance = dateBinanceTask.Result;
            var dateCoingecko = dateCoingeckoTask.Result;

            var dateFavorabile = dateBinance != null && dateCoingecko != null
                ? SelecteazaDateFavorabile(dateBinance, dateCoingecko)
                : null;

            if (dateFavorabile != null && dateFavorabile.Count > 0)
            {
                AdaugaLog($"Date favorabile găsite: {dateFavorabile.Count} instrumente.");
            }
            else
            {
                AdaugaLog("Nu s-au găsit date favorabile pentru tranzacționare.");
            }
        }
    }
}
