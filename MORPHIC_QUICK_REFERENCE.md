# MORPHIC - Quick Reference Guide

## Project At A Glance

**What:** AI-powered hand gesture-controlled 3D clay sculpting application  
**Tech:** Unity 6000.4.7f1, C#, MediaPipe (hand tracking), OpenAI (AI feedback)  
**Current Status:** Production-ready, clean git tree  

---

## Core Modules (34 C# Files)

| Module | Files | Purpose |
|--------|-------|---------|
| **AI** | 5 | OpenAI integration, backend communication |
| **Tracking** | 2 | MediaPipe hand detection, gesture detection |
| **Sculpting** | 7 | Mesh deformation tools (pinch, rotate, scale) |
| **UI** | N/A | Menu, language selection, feedback display |
| **Story, Input, Core, Data** | N/A | Utilities and components |

---

## Gesture Modes Detected

```
Idle (default)
├── Single Hand
│   ├── BottomFlatten (fist) → Flatten sculpture
│   └── TopGather (pinch) → Gather/pinch sculpture
└── Two Hands
    ├── Width (both fists) → Scale width
    ├── TopPinch (both pinching) → Pinch deformation
    └── Puff (both open) → Expand sculpture
```

---

## Key Gestures & Thresholds

| Gesture | Detection | Single | Double | Threshold |
|---------|-----------|--------|--------|-----------|
| Fist | Finger curl ratio | ✓ | ✓ | ≤1.08 |
| Open | Finger extension | ✗ | ✓ | ≥1.18 |
| Pinch | Thumb-index distance | ✓ | ✓ | ≤0.10 |

**Activation:** 160ms hold time, 500ms idle timeout, 400ms two-hand grace period

---

## Architecture Layers

```
Input (WebCam)
    ↓ MediaPipe HandLandmarker
Hand Landmarks & Metrics
    ↓ GestureModeManager
Gesture Recognition
    ↓ Controller Selection
Sculpting System (Pinch, Fist, Two-Hand)
    ↓
Mesh Deformation (Shape Keys + Vertex Colors)
    ↓ (Optional)
AI Feedback System (OpenAI Backend)
    ↓
On-Screen Feedback
```

---

## Main Classes

| Class | Location | Role |
|-------|----------|------|
| `SculptHandLandmarkerRunner` | Tracking/ | Hand pose detection |
| `GestureModeManager` | Root | Gesture state machine |
| `ClayColorPainter` | Root | Vertex painting system |
| `BasicSculptController` | Sculpting/ | Keyboard controls |
| `PinchSculptBrush` | Sculpting/ | Pinch deformation |
| `TwoHandScaleSculpture` | Sculpting/ | Two-hand scaling |
| `MorphicAIClient` | AI/ | Backend communication |
| `MorphicAIActionRouter` | AI/ | AI action dispatch |

---

## Keyboard Controls (Testing)

```
A / D     → Width adjustment (scale: 1f/sec)
W / S     → Height adjustment
Q / E     → Rotation (80°/sec)
R         → Reset to initial state
Scale     → 0.5x to 4x range
```

---

## Configuration Parameters

**Gesture Detection (GestureModeManager):**
```
fistMaxExtension: 1.08
openMinExtension: 1.18
pinchMaxDistance: 0.10
modeActivationTime: 0.16s
idleTimeout: 0.5s
twoHandGraceTime: 0.4s
```

**Painting (ClayColorPainter):**
```
brushSize: 0.08f
paintStrength: 1f
pinchStartDistance: 0.055f
pinchReleaseDistance: 0.08f (hysteresis)
```

---

## AI Integration

**Backend:** https://morphic-openai-backend.onrender.com  
**Endpoints:**
- `/api/feedback` (Live mode with OpenAI)
- `/api/feedback-test` (Test mode)

**Response Structure:**
```json
{
  "success": boolean,
  "feedback": {
    "message": string,
    "action": {
      "type": string,
      "target": string,
      "value": number
    }
  }
}
```

---

## Project Structure

```
Morphic/
├── Assets/Scripts/ (34 C# files)
│   ├── AI/, Tracking/, Sculpting/, UI/, etc
├── Assets/MediaPipeUnity/ (Hand tracking SDK)
├── Assets/Scenes/ (MainMenu, SculptingTest, HandTrackingTest)
├── ProjectSettings/
├── Morphic.slnx (Solution file)
└── *.csproj files
```

---

## Git Status

```
Latest: 955c91a - Update MORPHIC main menu and language page
Status: Clean, up to date with origin/Anagha_dev
Branch: Anagha_dev
```

---

## Key Features

✅ Real-time hand gesture recognition (MediaPipe)  
✅ Multi-hand support with grace period  
✅ Gesture-based 3D mesh deformation  
✅ Vertex color painting system  
✅ AI-powered feedback (OpenAI)  
✅ Test/Live AI mode toggle  
✅ Shape key morphing  
✅ Undo/Redo capability  
✅ Multi-language support  

---

## Design Patterns

- **State Machine** → GestureModeManager
- **Observer** → AI event callbacks
- **Component-Based** → MonoBehaviour composition
- **Layered Architecture** → Clean separation of concerns

---

## Performance Notes

- Hand tracking latency: ~16-33ms
- Capable of 60 FPS operation
- Painting O(n) complexity (n = vertices in brush radius)
- AI requests are async (non-blocking)
- Works offline (AI is optional)

---

## Entry Points for Development

1. **New Gestures:** Modify `GestureModeManager.DetectGestureMode()`
2. **New AI Actions:** Extend `MorphicAIActionRouter`
3. **New Sculpting Tools:** Create new controller script
4. **UI/Languages:** Modify `LanguagePageController`

---

**Generated:** October 4, 2026  
**Version:** 1.0  
**Repository:** https://github.com/Anaghaj1411/Morphic.git
