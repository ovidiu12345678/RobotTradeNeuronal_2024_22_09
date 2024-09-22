from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
import torch
import torch.nn as nn
import torch.nn.functional as F
import torch.optim as optim
from sklearn.model_selection import train_test_split
from sklearn.metrics import accuracy_score
from sklearn.linear_model import LogisticRegression
from sklearn.preprocessing import MinMaxScaler
from typing import List
import numpy as np
import os
import logging

app = FastAPI()

# Configurãm logarea
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(levelname)s - %(message)s')

# Definim schema pentru cerere ?i rãspuns
class CerereMarkov(BaseModel):
    Caracteristici: List[float]  # Lista de caracteristici pentru predic?ie

class RaspunsMarkov(BaseModel):
    EtichetaPrezisa: float  # Rezultatul predic?iei

# Definim modelul neuronal Markov
class ModelMarkov(nn.Module):
    def __init__(self):
        super(ModelMarkov, self).__init__()
        self.fc1 = nn.Linear(5, 50)  # Primul strat: 5 intrãri, 50 ie?iri
        self.fc2 = nn.Linear(50, 20)  # Al doilea strat: 50 intrãri, 20 ie?iri
        self.fc3 = nn.Linear(20, 1)   # Al treilea strat: 20 intrãri, 1 ie?ire

    def forward(self, x):
        x = F.relu(self.fc1(x))  # Func?ia de activare ReLU pentru primul strat
        x = F.relu(self.fc2(x))  # Func?ia de activare ReLU pentru al doilea strat
        x = torch.sigmoid(self.fc3(x))  # Func?ia de activare Sigmoid pentru al treilea strat
        return x

# Instan?ierea modelului ?i încãrcarea ponderilor salvate
model = ModelMarkov()

# Calea cãtre fi?ierul .pth cu modelul salvat
cale_model = "E:/ROBIT_TRANZACTIONARE_GABY_CSHARP_DEXTOP_2024/RobotTradeNeuronal/RobotTradeNeuronal/models/ModelMarkov.pth"

# Încãrcãm starea modelului din fi?ier
try:
    model.load_state_dict(torch.load(cale_model, map_location=torch.device('cpu')))
    model.eval()  # Punem modelul în modul de evaluare
    logging.info("Modelul a fost încãrcat cu succes.")
except FileNotFoundError as e:
    logging.error(f"Eroare: Fi?ierul modelului nu a fost gãsit la calea specificatã: {e}")
except Exception as e:
    logging.error(f"Eroare la încãrcarea modelului: {e}")

# Functia pentru curatarea datelor: Eliminarea valorilor lipsa si normalizarea datelor
def curata_datele(date):
    """
    Curã?area datelor: eliminarea valorilor lipsã, normalizarea ?i scalarea datelor.
    """
    # Convertim datele  Intr-un array numpy pentru manipulare usoara
    date = np.array(date)
    
    # Inlocuim valorile NaN cu media coloanelor
    if np.isnan(date).any():
        medii_coloane = np.nanmean(date, axis=0)
        indici = np.where(np.isnan(date))
        date[indici] = np.take(medii_coloane, indici[1])
    
    # Normalizam datele intre 0 ?i 1
    scaler = MinMaxScaler()
    date_scalate = scaler.fit_transform(date)
    
    return date_scalate

# Definim o functie pentru a adauga intrari în jurnal
def adauga_log(mesaj):
    logging.info(mesaj)

# Functia de antrenare pentru modelul de invatare automata (ML)
def antreneaza_model_ml(caracteristici, etichete):
    try:
        if caracteristici is not None and etichete is not None:
            if len(caracteristici) != len(etichete):
                lungime_minima = min(len(caracteristici), len(etichete))
                caracteristici = caracteristici[:lungime_minima]
                etichete = etichete[:lungime_minima]

            if len(caracteristici) == len(etichete):
                X_antrenare, X_testare, y_antrenare, y_testare = train_test_split(caracteristici, etichete, test_size=0.2)
                X_antrenare = curata_datele(X_antrenare)
                X_testare = curata_datele(X_testare)

                model_ml = LogisticRegression()
                model_ml.fit(X_antrenare, y_antrenare)

                y_prezis = model_ml.predict(X_testare)
                acuratete = accuracy_score(y_testare, y_prezis)
                adauga_log(f"Acurate?ea modelului ML: {acuratete * 100:.2f}%")

                return True
            else:
                adauga_log("Dupã ajustare, dimensiunile caracteristicilor ?i etichetelor nu corespund.")
                return False
        else:
            adauga_log("Caracteristicile sau etichetele sunt None. Se omite antrenarea modelului ML.")
            return False
    except Exception as e:
        adauga_log(f"Eroare în antreneaza_model_ml: {e}")
        return False

# Functia de antrenare pentru modelul de invatare profunda (DL)
def antreneaza_model_dl(caracteristici, etichete):
    try:
        if caracteristici is not None and etichete is not None:
            if len(caracteristici) != len(etichete):
                lungime_minima = min(len(caracteristici), len(etichete))
                caracteristici = caracteristici[:lungime_minima]
                etichete = etichete[:lungime_minima]

            if len(caracteristici) == len(etichete):
                X_antrenare, X_testare, y_antrenare, y_testare = train_test_split(caracteristici, etichete, test_size=0.2)
                X_antrenare = curata_datele(X_antrenare)
                X_testare = curata_datele(X_testare)

                criteriu = nn.CrossEntropyLoss()
                optimizator = optim.Adam(model.parameters(), lr=0.001)

                for epoca in range(10):  # Antrenam pentru un numar fix de epoci
                    optimizator.zero_grad()
                    iesire = model(torch.tensor(X_antrenare, dtype=torch.float32))
                    pierdere = criteriu(iesire, torch.tensor(y_antrenare, dtype=torch.long))
                    pierdere.backward()
                    optimizator.step()

                with torch.no_grad():
                    iesire_test = model(torch.tensor(X_testare, dtype=torch.float32))
                    y_prezis = torch.argmax(iesire_test, dim=1).numpy()
                    acuratete = accuracy_score(y_testare, y_prezis)
                    adauga_log(f"Acurate?ea modelului DL: {acuratete * 100:.2f}%")
            else:
                adauga_log("Dupã ajustare, dimensiunile caracteristicilor ?i etichetelor nu corespund.")
    except Exception as e:
        adauga_log(f"Eroare în antreneaza_model_dl: {e}")

# Endpoint FastAPI pentru predic?ii
@app.post("/predict", response_model=RaspunsMarkov)
def prezice(cerere: CerereMarkov):
    try:
        # Conversia caracteristicilor de intrare în tensor
        tensor_intrare = torch.tensor(cerere.Caracteristici, dtype=torch.float32).unsqueeze(0)  # Formã: [1, 5]
        
        # Facem predic?ia fãrã a actualiza gradientul (mod de evaluare)
        with torch.no_grad():
            iesire = model(tensor_intrare)
            predictie = iesire.item()  # Extragem valoarea scalarã din tensor
        
        # Returnãm predic?ia sub forma unui rãspuns
        return RaspunsMarkov(EtichetaPrezisa=predictie)
    except Exception as e:
        logging.error(f"Eroare la prezicere: {e}")
        raise HTTPException(status_code=500, detail=str(e))

# Endpoint de testare pentru a verifica dacã serverul func?ioneazã
@app.get("/")
def citeste():
    return {"mesaj": "Serverul FastAPI este func?ional"}
