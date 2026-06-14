# Projekt-Grupowy-ID419

Aplikacja desktopowa do analizy zdjęć z Skaningowego Mikroskopu Elektronowego (SEM).  
Pozwala wykrywać i zliczać kryształy diamentu na zdjęciach mikroskopowych przy użyciu różnych algorytmów przetwarzania obrazu.

---

## Wymagania

| Składnik | Wersja minimalna |
|---|---|
| System operacyjny | Windows 10 / 11 |
| .NET | 8.0 |
| Python | 3.8+ (py / python / python3 dostępny w PATH) |

---

## Pierwsze uruchomienie

1. Sklonuj lub pobierz repozytorium.
2. Otwórz solution `DashboardApp/DashboardApp.sln` w Visual Studio i uruchom projekt **DashboardApp** (F5), lub uruchom gotowy plik `.exe` z folderu `bin/`.
3. Przy pierwszym uruchomieniu aplikacja **automatycznie**:
   - wykryje zainstalowanego Pythona (`py` / `python` / `python3`),
   - utworzy wirtualne środowisko (`scripts/venv/`),
   - zainstaluje wymagane biblioteki (`opencv-python`, `numpy`, `matplotlib`).
4. Postęp konfiguracji widoczny jest na pasku statusu u dołu okna. Przyciski są zablokowane do zakończenia instalacji.


---

## Struktura projektu

```
Projekt-Grupowy-ID419/
├── DashboardApp/                  # Aplikacja WPF (C#)
│   └── DashboardApp/
│       ├── MainWindow.xaml(.cs)   # Główne okno
│       └── LivePreviewWindow.xaml(.cs)  # Okno podglądu parametrów
├── scripts/
│   ├── scripts.json               # Definicje skryptów i ich parametrów
│   ├── requirements.txt           # Zależności Python
│   ├── setup_venv.bat             # Ręczna konfiguracja środowiska
│   ├── venv/                      # Wirtualne środowisko (ignorowane przez git)
│   ├── threshold_morphology_contours/
│   ├── another_scripts/
│   └── bdd_analysis/
└── README.md
```

---

## Obsługa interfejsu

#### Krok 1 — Wybór skryptu
Z listy rozwijanej **„Skrypt"** wybierz algorytm analizy (opis algorytmów poniżej).

#### Krok 2 — Wczytanie zdjęć
- Kliknij **📂 Wczytaj zdjęcie** i wybierz jeden lub więcej plików (JPG, PNG, BMP, TIFF).
- Alternatywnie **przeciągnij i upuść** pliki na środek okna.
- Obsługiwane formaty: `.jpg`, `.jpeg`, `.png`, `.bmp`, `.tiff`, `.tif`.

#### Krok 3 — Utnij dół (px)
Pole **„Utnij dół (px)"** pozwala odciąć dolną część każdego zdjęcia (np. pasek z metadanymi mikroskopu). Wpisz liczbę pikseli do odcięcia (domyślnie `0`).

#### Krok 4 — Podgląd i dostosowanie parametrów
Kliknij **„Przytnij i przekaż"**. Otworzy się okno **Podglądu na żywo** z pierwszym zdjęciem.

#### Krok 5 — Analiza wsadowa
Po ustaleniu parametrów kliknij **„✔ Zastosuj do wszystkich zdjęć"** — aplikacja przetworzy wszystkie wczytane pliki z identycznymi ustawieniami i wyświetli wyniki.

#### Przeglądanie wyników
Każdy wynik na liście pokazuje miniaturę i liczbę wykrytych kryształów.  
**Dwukrotne kliknięcie** na wynik otwiera go w powiększeniu z pełną listą parametrów po prawej stronie.

---

## Okno podglądu na żywo

Otwiera się automatycznie po kliknięciu „Przytnij i przekaż". Składa się z:

- **Lewa strona** — powiększony obraz wynikowy z naniesionymi konturami i numerami kryształów.
- **Prawa strona** — suwaki parametrów specyficzne dla wybranego algorytmu.
- **Wynik** — liczba wykrytych kryształów aktualizowana po każdej analizie.

| Przycisk | Działanie |
|---|---|
| 🔄 Odśwież podgląd | Uruchamia ponownie skrypt z aktualnymi ustawieniami suwaków |
| ✔ Zastosuj do wszystkich zdjęć | Zamyka okno i uruchamia analizę wsadową na wszystkich wczytanych zdjęciach |
| Anuluj | Zamyka okno bez uruchamiania analizy |

---

## Opis algorytmów i ich parametrów

### 1. Threshold + Morphology + Contours
Najprostszy i najszybszy algorytm. Konwertuje obraz do skali szarości, rozmywa go, proguje binarnie, usuwa szum morfologicznie i szuka konturów.

| Parametr | Zakres | Domyślnie | Opis |
|---|---|---|---|
| **Próg binaryzacji** | 0–255 | 80 | Główny próg oddzielający kryształy od tła. Piksele jaśniejsze od progu stają się białe. Zwiększ, jeśli wykrywa za dużo tła; zmniejsz, jeśli kryształy są ignorowane. |
| **Min. obszar konturu (px²)** | 1–500 | 15 | Minimalna powierzchnia konturu, który zostanie zaliczony jako kryształ. Odfiltruje drobny szum. |
| **Rozmycie Gaussa (kernel)** | 1/3/5/7/9 | 5 | Rozmiar kernela rozmycia przed progowaniem. Większa wartość = silniejsze wygładzenie = mniej fałszywych detekcji, ale mniej szczegółów. |
| **Iteracje morfologii** | 1–10 | 3 | Liczba powtórzeń operacji otwarcia morfologicznego (erosja + dylatacja). Więcej iteracji = agresywniejsze usuwanie szumu. |
| **Rozmiar kernela morfologii** | 3/5/7 | 3 | Rozmiar elementu strukturalnego używanego w morfologii. Większy kernel = usuwanie grubszych artefaktów. |

---

### 2. Morphology
Wykrywa kryształy na podstawie przejścia kolor→czerń (ciemne tło). Używa otwarcia i zamknięcia morfologicznego do wygładzenia maski tła, a następnie detekcji krawędzi przez erozję.

| Parametr | Zakres | Domyślnie | Opis |
|---|---|---|---|
| **Próg binaryzacji** | 0–255 | 40 | Próg oddzielający czarne tło od kryształów. Niskie wartości = tylko bardzo ciemne piksele to tło. |
| **Min. obszar konturu (px²)** | 1–500 | 50 | Minimalna powierzchnia obiektu. Odfiltruje szum i drobne artefakty. |
| **Rozmiar kernela** | 3/5/7 | 3 | Rozmiar elementu strukturalnego dla operacji morfologicznych. |
| **Iteracje otwarcia** | 1–10 | 2 | Liczba powtórzeń operacji otwarcia (usuwa małe jasne punkty z tła). |
| **Iteracje zamknięcia** | 1–10 | 2 | Liczba powtórzeń operacji zamknięcia (wypełnia małe dziury w kryształach). |

---

### 3. Canny Edge Detection
Wykrywa krawędzie algorytmem Canny'ego. Dobry do zdjęć z wyraźnymi krawędziami, słabszy gdy kontrast jest niski.

| Parametr | Zakres | Domyślnie | Opis |
|---|---|---|---|
| **Próg Canny dolny** | 0–255 | 80 | Dolny próg histerezy — krawędzie słabsze od tej wartości są odrzucane. |
| **Próg Canny górny** | 0–255 | 90 | Górny próg histerezy — krawędzie silniejsze od tej wartości są zawsze akceptowane. Zwykle wyższy od dolnego. |
| **Rozmiar kernela** | 3/5/7 | 3 | Rozmiar elementu strukturalnego do zamykania krawędzi. |
| **Iteracje zamknięcia** | 1–10 | 2 | Zamknięcie morfologiczne scala przerywane krawędzie w zamknięte kontury. |
| **Min. obszar konturu (px²)** | 1–500 | 50 | Minimalna powierzchnia konturu uznawanego za kryształ. |

---

### 4. Thresholding
Prosta binaryzacja progu + morfologia + detekcja konturów tła. Podobny do algorytmu nr 1, ale wykrywa obiekty jako „niespójności w tle" zamiast bezpośrednio jako jasne regiony.

| Parametr | Zakres | Domyślnie | Opis |
|---|---|---|---|
| **Próg binaryzacji** | 0–255 | 60 | Próg oddzielający tło od kryształów. |
| **Min. obszar konturu (px²)** | 1–500 | 50 | Minimalna powierzchnia obiektu. |
| **Rozmiar kernela** | 3/5/7 | 3 | Rozmiar elementu strukturalnego. |
| **Iteracje otwarcia** | 1–10 | 2 | Usuwa małe artefakty z maski binarnej. |
| **Iteracje zamknięcia** | 1–10 | 2 | Wypełnia dziury w obiektach. |

---

### 5. Watershed (basic)
Zaawansowany algorytm separacji stykających się obiektów. Używa transformaty odległości do wyznaczenia „nasion" (seed points), z których rozrasta się segmentacja jak woda wypełniająca zlewnie.

| Parametr | Zakres | Domyślnie | Opis |
|---|---|---|---|
| **Próg binaryzacji** | 0–255 | 150 | Wstępny próg binaryzacji + OTSU (wartość pełni rolę minimalnego progu). |
| **Rozmiar kernela** | 3/5/7 | 3 | Rozmiar elementu strukturalnego do czyszczenia maski. |
| **Iteracje otwarcia** | 1–10 | 2 | Usuwa szum przed wyznaczeniem nasion. |
| **Próg odległości (%)** | 5–50 | 10 | Procent maksymalnej wartości transformaty odległości używany do wyznaczania nasion. Większa wartość = mniej, ale pewniejszych nasion = mniej segmentów. |

---

### 6. Watershed + Preprocessing
Najbardziej rozbudowany algorytm z zaawansowanym preprocessingiem: wyrównanie histogramu CLAHE, medianowe odszumianie i sigmoidalne wzmocnienie kontrastu przed watershed.

| Parametr | Zakres | Domyślnie | Opis |
|---|---|---|---|
| **Sigmoid Alpha (siła)** | 1–30 | 15 | Stromość funkcji sigmoidalnej wzmacniającej kontrast. Większa wartość = mocniejszy kontrast, ostrzejszy podział jasne/ciemne. |
| **Sigmoid Beta×100 (próg)** | 1–50 | 13 | Punkt przegięcia krzywej sigmoidalnej (podzielony przez 100, więc 13 = 0.13). Przesuwa wzmocnienie w stronę ciemniejszych lub jaśniejszych pikseli. |
| **Min. obszar (px²)** | 10–500 | 150 | Minimalna powierzchnia segmentu uznawanego za kryształ. Wyższe wartości eliminują drobne artefakty. |
| **Próg odległości (%)** | 5–50 | 20 | Procent maksymalnej wartości wygładzonej transformaty odległości dla wyboru nasion. |
| **CLAHE clip limit** | 1–10 | 3 | Limit przycinania histogramu lokalnego wyrównania CLAHE. Wyższa wartość = agresywniejsze wyrównanie kontrastu lokalnego. |

---

## Dodawanie nowego skryptu

Aby dodać własny skrypt bez modyfikacji kodu aplikacji:

1. Umieść plik `.py` w folderze `scripts/` (w dowolnym podfolderze).
2. Skrypt musi przyjmować jako argumenty: `input_path output_path [param1] [param2] ...` i wypisać na stdout JSON w formacie:
   ```json
   { "Ilosc krysztalow": 42, "Status": "OK" }
   ```
3. Dopisz wpis do pliku `scripts/scripts.json`:
   ```json
   {
     "displayName": "Nazwa w menu",
     "relativePath": "scripts\\moj_folder\\moj_skrypt.py",
     "params": [
       { "name": "PARAM1", "displayName": "Opis parametru", "min": 0, "max": 100, "default": 50, "step": 1, "snapToTick": false }
     ]
   }
   ```
4. Uruchom aplikację — nowy skrypt pojawi się w liście automatycznie.

---

