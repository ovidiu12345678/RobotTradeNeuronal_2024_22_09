using Newtonsoft.Json;
using RobotTradeNeuronal.Clienti.ClientBinance;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RobotTradeNeuronal
{
    public partial class NeuronalBot : MaterialSkin.Controls.MaterialForm
    {
        private Binance binance;
        private List<string> listaCompletaContracte = new List<string>();
        private List<string> mesajeJurnal = new List<string>();
        private List<DetaliiContract> listaDetaliiContracte = new List<DetaliiContract>();

        public NeuronalBot()
        {
            InitializeComponent();
            binance = null;

            labelClientSymbol.AutoSize = true;
            labelClientExchange.AutoSize = true;
            labelClientBid.AutoSize = true;
            labelClientAsk.AutoSize = true;
            labelMesajeJurnal.AutoSize = true;

            btnClientStergeObiecteCampuri.Visible = false;

            btnClientStergeObiecteCampuri.Click += btnClientStergeObiecteCampuri_Click;

            comboBoxClientBinance.KeyDown += comboBoxClientBinance_KeyDown;
            comboBoxClientBinance.SelectedIndexChanged += comboBoxClientBinance_SelectedIndexChanged;
        }

        #region Încărcare contracte și căutare în ComboBox
        // Metoda separata pentru incarcarea contractelor
        private async Task IncarcaContracte()
        {
            if (binance == null)
            {
                AfiseazaInJurnal("Cheile API nu sunt setate. Va rugam sa le setati mai intai.");
                return;
            }

            try
            {
                if (listaCompletaContracte.Count == 0)
                {
                    var contracte = await binance.ObtineContracte();
                    listaCompletaContracte = contracte.Keys.ToList();

                    comboBoxClientBinance.Items.Clear();
                    comboBoxClientBinance.Items.AddRange(listaCompletaContracte.ToArray());

                    comboBoxClientBinance.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                    comboBoxClientBinance.AutoCompleteSource = AutoCompleteSource.ListItems;
                }
            }
            catch (Exception ex)
            {
                AfiseazaInJurnal($"Eroare la incarcarea contractelor Binance: {ex.Message}");
            }
        }

       
        private async void comboBoxClientBinance_Enter(object sender, EventArgs e)
        {
            await IncarcaContracte();
        }
        #endregion

        #region Eveniment pentru clic pe simbolul din ComboBox
        private void comboBoxClientBinance_SelectedIndexChanged(object sender, EventArgs e)
        {
            PopuleazaDetaliiContract();
        }
        #endregion

        #region Eveniment pentru apăsarea tastei Enter
        private void comboBoxClientBinance_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                PopuleazaDetaliiContract();
            }
        }
        #endregion

        #region Metoda comună pentru popularea detaliilor contractului
        private async void PopuleazaDetaliiContract()
        {
            string simbol = comboBoxClientBinance.Text.ToUpper();

            if (!string.IsNullOrEmpty(simbol))
            {
                try
                {
                    if (binance == null)
                    {
                        AfiseazaInJurnal("Cheile API nu sunt setate. Va rugam sa le setati mai intai.");
                        return;
                    }

                    var contracte = await binance.ObtineContracte();

                    if (contracte.ContainsKey(simbol))
                    {
                        var contract = contracte[simbol];
                        var preturi = await binance.ObtinePreturiBidAsk(simbol);

                        AfiseazaInJurnal($"Raspuns de la API pentru {simbol}: {JsonConvert.SerializeObject(preturi)}");

                        if (preturi != null && preturi.ContainsKey("bid") && preturi.ContainsKey("ask"))
                        {
                            var detaliiContract = new DetaliiContract
                            {
                                Simbol = contract.Simbol,
                                Exchange = "Binance",
                                Bid = preturi["bid"].ToString(),
                                Ask = preturi["ask"].ToString()
                            };

                            listaDetaliiContracte.Add(detaliiContract);

                            labelClientSymbol.Text = detaliiContract.Simbol;
                            labelClientExchange.Text = detaliiContract.Exchange;
                            labelClientBid.Text = detaliiContract.Bid;
                            labelClientAsk.Text = detaliiContract.Ask;

                            AfiseazaInJurnal($"Bid: {preturi["bid"]}, Ask: {preturi["ask"]}");

                            await binance.AbonareCanal(new List<Contract> { contract }, "bookTicker");

                            btnClientStergeObiecteCampuri.Visible = true;
                        }
                        else
                        {
                            AfiseazaInJurnal("Preturile bid/ask nu au fost gasite în raspunsul API.");
                        }

                        
                    }
                    else
                    {
                        AfiseazaInJurnal("Simbolul nu a fost gasit în lista de contracte.");
                    }
                }
                catch (Exception ex)
                {
                    AfiseazaInJurnal($"Eroare la obtinerea datelor pentru {simbol}: {ex.Message}");
                }
            }
            else
            {
                AfiseazaInJurnal("Introduceti un simbol valid pentru a obtine detalii.");
            }
        }
        #endregion

        #region Ștergerea câmpurilor de contract și ascunderea butonului X
        private void btnClientStergeObiecteCampuri_Click(object sender, EventArgs e)
        {
            comboBoxClientBinance.Text = string.Empty;
            labelClientSymbol.Text = string.Empty;
            labelClientExchange.Text = string.Empty;
            labelClientBid.Text = string.Empty;
            labelClientAsk.Text = string.Empty;

            btnClientStergeObiecteCampuri.Visible = false;

            AfiseazaInJurnal("Campurile au fost resetate.");
        }
        #endregion

        #region Adaugare strategie
        private void btnMeniuActivitatiAddStrategy_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(labelClientSymbol.Text))
            {
                AfiseazaInJurnal($"Strategie creata pentru {labelClientSymbol.Text}");
            }
            else
            {
                AfiseazaInJurnal("Nu a fost selectat niciun simbol. Va rugam sa selectati un simbol.");
            }
        }
        #endregion

        #region Introducerea Cheilor API
        private void ClientAddKeyApi_Click(object sender, EventArgs e)
        {
            Form apiKeyForm = new Form
            {
                Text = "Introducere Chei API",
                Size = new Size(395, 160),
                BackColor = Color.FromArgb(0, 0, 64),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen
            };

            Label labelPublic = new Label
            {
                Text = "API Key:",
                ForeColor = Color.BurlyWood,
                Location = new Point(10, 10),
                Size = new Size(80, 20)
            };
            TextBox textBoxPublic = new TextBox
            {
                Location = new Point(100, 10),
                Size = new Size(270, 25),
                ForeColor = Color.BurlyWood,
                BackColor = Color.FromArgb(0, 0, 64),
                Font = new Font("Segoe UI", 9, FontStyle.Bold | FontStyle.Italic)
            };

            Label labelSecret = new Label
            {
                Text = "Secret Key:",
                ForeColor = Color.BurlyWood,
                Location = new Point(10, 40),
                Size = new Size(80, 20)
            };
            TextBox textBoxSecret = new TextBox
            {
                Location = new Point(100, 40),
                Size = new Size(270, 25),
                ForeColor = Color.BurlyWood,
                BackColor = Color.FromArgb(0, 0, 64),
                Font = new Font("Segoe UI", 9, FontStyle.Bold | FontStyle.Italic),
                PasswordChar = '*'
            };

            ComboBox comboTestnetReal = new ComboBox
            {
                Location = new Point(10, 75),
                Size = new Size(80, 25),
                ForeColor = Color.BurlyWood,
                BackColor = Color.FromArgb(0, 0, 64),
                Font = new Font("Segoe UI", 9, FontStyle.Bold | FontStyle.Italic),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            comboTestnetReal.Items.AddRange(new string[] { "Testnet", "Real" });
            comboTestnetReal.SelectedIndex = 0;

            ComboBox comboSpotFutures = new ComboBox
            {
                Location = new Point(100, 75),
                Size = new Size(80, 25),
                ForeColor = Color.BurlyWood,
                BackColor = Color.FromArgb(0, 0, 64),
                Font = new Font("Segoe UI", 9, FontStyle.Bold | FontStyle.Italic),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            comboSpotFutures.Items.AddRange(new string[] { "Spot", "Futures" });
            comboSpotFutures.SelectedIndex = 0;

            Button btnValideaza = new Button
            {
                Text = "Validează",
                Size = new Size(100, 30),
                Location = new Point(270, 75),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.BurlyWood,
                BackColor = Color.FromArgb(0, 0, 64),
                Font = new Font("Segoe UI", 9, FontStyle.Bold | FontStyle.Italic),
                Cursor = Cursors.Hand
            };

            btnValideaza.Click += async (s, ev) =>
            {
                string apiKey = textBoxPublic.Text;
                string secretKey = textBoxSecret.Text;
                bool esteTestnet = comboTestnetReal.SelectedItem.ToString() == "Testnet";
                bool esteFutures = comboSpotFutures.SelectedItem.ToString() == "Futures";

                if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(secretKey))
                {
                    AfiseazaInJurnal("Trebuie să introduci ambele chei API.");
                }
                else
                {
                    binance = new Binance(apiKey, secretKey, esteTestnet, esteFutures);
                    bool apiValida = await binance.ValideazaCheileApi();
                    if (apiValida)
                    {
                        AfiseazaInJurnal("Cheile API au fost setate corect.");
                        apiKeyForm.Close();
                    }
                    else
                    {
                        AfiseazaInJurnal("Cheile API sunt invalide.");
                    }
                }
            };

            apiKeyForm.Controls.Add(labelPublic);
            apiKeyForm.Controls.Add(textBoxPublic);
            apiKeyForm.Controls.Add(labelSecret);
            apiKeyForm.Controls.Add(textBoxSecret);
            apiKeyForm.Controls.Add(comboTestnetReal);
            apiKeyForm.Controls.Add(comboSpotFutures);
            apiKeyForm.Controls.Add(btnValideaza);

            apiKeyForm.ShowDialog();
        }
        #endregion

        #region Metodă pentru afișarea mesajelor în panelJurnalAcrivitati
        private void AfiseazaInJurnal(string mesaj)
        {
            mesajeJurnal.Add($"{DateTime.Now}: {mesaj}");
            panelJurnalAcrivitati.Controls.Clear();

            mesajeJurnal
                .Select((mesaj, index) => new Label
                {
                    AutoSize = true,
                    ForeColor = Color.BlueViolet,
                    Text = mesaj,
                    Location = new Point(5, index * 20)
                })
                .ToList()
                .ForEach(label => panelJurnalAcrivitati.Controls.Add(label));
        }
        #endregion
    }
}
