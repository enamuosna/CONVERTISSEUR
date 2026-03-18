# MXF Converter Pro — .NET 8 WPF

Application desktop de conversion vidéo inspirée de Wondershare UniConverter.

## ⚙️ Prérequis

- .NET 8 SDK (https://dotnet.microsoft.com/download)
- JetBrains Rider 2024+ ou Visual Studio 2022+
- Windows 10/11 x64
- Connexion internet au **premier démarrage** (téléchargement automatique de FFmpeg)

---

## 🚀 Démarrage rapide dans Rider

1. **Ouvrir le projet**
   - File → Open → sélectionner le dossier `MXFConverter`

2. **Restaurer les packages NuGet**
   - Rider le fait automatiquement, ou : `dotnet restore`

3. **Lancer l'application**
   - Clic sur ▶ Run (ou `Shift+F10`)
   - Au premier démarrage : FFmpeg sera téléchargé automatiquement (~80 MB)
     dans `%LOCALAPPDATA%\MXFConverter\ffmpeg`

---

## 🎬 Fonctionnalités

| Fonctionnalité            | Description                                            |
|---------------------------|--------------------------------------------------------|
| Glisser-déposer           | Drop direct depuis l'explorateur Windows               |
| Multi-fichiers            | Conversion par lots avec file d'attente                |
| Formats d'entrée          | MXF, MP4, MOV, MKV, AVI, WMV, FLV, WEBM, TS…         |
| Formats de sortie         | MP4, MOV, MKV, AVI, WMV, FLV, WEBM, MXF, MP3, WAV… |
| Préréglages qualité       | Haute qualité, Standard, Faible taille, Sans perte, H.265 |
| Format par fichier        | Chaque fichier peut avoir son propre format/qualité    |
| Dossier de sortie         | Personnalisable globalement                            |
| Annulation                | Annulation individuelle ou globale                     |
| Infos médias              | Résolution, codec, framerate, durée, taille           |
| Progression en temps réel | Barre de progression par fichier + globale             |
| UI sombre moderne         | Thème dark avec animations et icônes                  |

---

## 📦 Dépendances NuGet

| Package                    | Version | Rôle                              |
|----------------------------|---------|-----------------------------------|
| `Xabe.FFmpeg`              | 5.2.6   | Wrapper FFmpeg pour .NET          |
| `Xabe.FFmpeg.Downloader`   | 5.2.6   | Téléchargement automatique FFmpeg |
| `CommunityToolkit.Mvvm`    | 8.3.2   | MVVM helpers (ObservableObject)   |

---

## 🗂 Structure du projet

```
MXFConverter/
├── App.xaml / App.xaml.cs          ← Point d'entrée + init FFmpeg
├── Themes/
│   └── DarkTheme.xaml              ← Thème sombre global
├── Models/
│   └── ConversionItem.cs           ← Modèle d'un fichier à convertir
├── Services/
│   └── FFmpegService.cs            ← Logique de conversion FFmpeg
└── Views/
    ├── MainWindow.xaml             ← Interface principale
    └── MainWindow.xaml.cs          ← Code-behind
```

---

## 🎯 Préréglages qualité expliqués

| Préréglage      | CRF | Codec  | Usage recommandé              |
|-----------------|-----|--------|-------------------------------|
| Haute qualité   | 18  | H.264  | Archivage, diffusion pro      |
| Qualité standard| 23  | H.264  | Usage courant, bon compromis  |
| Faible taille   | 28  | H.264  | Partage web, stockage limité  |
| Sans perte      | 0   | H.264  | Master, post-production       |
| H.265 (HEVC)    | 20  | H.265  | 4K, petite taille + qualité   |

---

## ⚠️ Remarques

- Le **premier démarrage** peut prendre 1-2 minutes (téléchargement FFmpeg)
- FFmpeg est téléchargé **une seule fois** dans `%LOCALAPPDATA%\MXFConverter\ffmpeg`
- La conversion MXF → MP4 H.265 "Sans perte" préserve la qualité originale maximale
