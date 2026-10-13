# MORPHIC PROJECT STUDY - Comprehensive Analysis

## 📋 Project Overview

**Project Name:** Morphic  
**Repository:** https://github.com/Anaghaj1411/Morphic.git  
**Current Branch:** Anagha_dev  
**Engine:** Unity 6000.4.7f1 LTS  
**Language:** C# (.NET)

Morphic is an AI-powered hand gesture-controlled digital clay sculpting application using MediaPipe for hand tracking and OpenAI for intelligent feedback.

---

## 🎯 Core Modules

### 1. Hand Tracking & Gesture Recognition
- **Location:** `Assets/Scripts/Tracking/`
- **Components:** `SculptHandLandmarkerRunner.cs`, `PinchDetector.cs`
- **Data:** `SculptHandFrame` with landmarks and metrics

### 2. Gesture Mode Detection
- **File:** `GestureModeManager.cs`
- **Modes:** Idle, Width, BottomFlatten, TopGather, TopPinch, Puff

### 3. Sculpting System
- **Location:** `Assets/Scripts/Sculpting/`
- **Tools:** PinchSculptBrush, FistRotateSculpture, TwoHandScaleSculpture

### 4. Color Painting
- **File:** `ClayColorPainter.cs`
- **Method:** Vertex color painting via raycast

### 5. AI Integration
- **Location:** `Assets/Scripts/AI/`
- **Backend:** https://morphic-openai-backend.onrender.com
- **Service:** OpenAI API

---

## 📁 Project Structure

```
Morphic/
├── Assets/Scripts/ (34 C# files)
│   ├── AI/ (MorphicAIClient, Settings, ActionRouter, etc)
│   ├── Tracking/ (Hand tracking & detection)
│   ├── Sculpting/ (Deformation & mesh tools)
│   ├── UI/, Story/, Input/, Core/, Data/
│   └── Root Controllers (ClayColorPainter, GestureModeManager, etc)
├── Assets/MediaPipeUnity/ (Hand tracking SDK)
├── Assets/Scenes/ (MainMenu, SculptingTest, HandTrackingTest)
├── Prefabs/, Materials/, Models/, Resources/
└── Morphic.slnx (Solution)
```

---

## 🔧 Technology Stack

- **Engine:** Unity 6000.4.7f1
- **Hand Tracking:** MediaPipe Unity SDK
- **AI Backend:** Node.js/Express + OpenAI API (Render.com)
- **UI:** TextMesh Pro
- **Input:** Unity InputSystem



## 🎮 Key Classes & Responsibilities

| Class | Module | Purpose |
|-------|--------|---------|
| SculptHandLandmarkerRunner | Tracking | Hand pose detection |
| GestureModeManager | Root | Gesture classification |
| ClayColorPainter | Root | Vertex painting |
| MorphicAIClient | AI | Backend communication |
| PinchSculptBrush | Sculpting | Pinch deformation |

---

## ⚙️ Configuration

**Gesture Detection:**
- fistMaxExtension: 1.08
- openMinExtension: 1.18
- pinchMaxDistance: 0.10
- modeActivationTime: 0.16s

**Painting:**
- brushSize: 0.08f
- paintStrength: 1f

**Keyboard (A/D: Width, W/S: Height, Q/E: Rotation, R: Reset)**

---

## 📝 Git History

```
955c91a - Update MORPHIC main menu
b4ba73c - Add AI integration
9485ae6 - Update sculpting system
d45bb7b - Initial hand controlled sculpting
ae5bb4e - Initial commit
```

**Status:** Clean, up to date

---

## 🚀 Features

✅ Hand gesture recognition (MediaPipe)
✅ Multi-hand support
✅ Mesh deformation
✅ Vertex color painting
✅ AI feedback & guidance
✅ Test/Live AI modes
✅ Shape key animations

---

**Generated:** October 4, 2026 | Version 1.0
