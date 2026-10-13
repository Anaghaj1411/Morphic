# Morphic Architecture Overview

## System Layer Stack

```
OUTPUT (Rendered mesh + UI)
    ↑
MESH DEFORMATION (Shape keys, Vertex colors)
    ↑
SCULPTING CONTROL (PinchBrush, FistRotate, TwoHandScale)
    ↑
GESTURE RECOGNITION (GestureModeManager - 6 modes, hysteresis)
    ↑
HAND TRACKING (MediaPipe - 21 landmarks)
    ↑
INPUT (WebCam)

SIDE: AI FEEDBACK (Optional async)
  Tracker → Client → Backend → OpenAI → Router → UI
```

---

## Module Breakdown (34 C# Files)

| Module | Files | Purpose |
|--------|-------|---------|
| AI | 5 | Backend communication, OpenAI integration |
| Tracking | 2 | Hand detection, metrics |
| Sculpting | 7 | Deformation tools |
| UI, Story, Input, Core, Data | - | UI & utilities |

---

## Data Flow

```
WebCam Input
    ↓
SculptHandFrame (21 landmarks + metrics)
    ↓
GestureModeManager (gesture classification)
    ↓
GestureMode (Idle, Width, BottomFlatten, TopGather, TopPinch, Puff)
    ↓
Active Controller (Based on mode)
    ├─ Pinch → ClayColorPainter (vertex painting)
    ├─ Fist → FistRotateSculpture (rotation)
    ├─ Two Hands → TwoHandScaleSculpture (scaling)
    └─ Keyboard → BasicSculptController (testing)
    ↓
Mesh Deformation (Shape Keys + Vertex Colors)
    ↓
Visual Update
    ↓
(Async) AI Feedback → UI Display
```

---

## Gesture Modes

```
Single Hand:
  • BottomFlatten (fist, ≤1.08 curl)
  • TopGather (pinch, ≤0.10 distance)

Two Hands:
  • Width (both fists)
  • TopPinch (both pinching)
  • Puff (both open, ≥1.18 curl)

Hysteresis:
  • Activation: 160ms hold
  • Idle timeout: 500ms
  • Two-hand grace: 400ms
```

---

## Key Classes

```
SculptHandLandmarkerRunner → SculptHandFrame
GestureModeManager → GestureMode enum
ClayColorPainter (vertex painting)
FistRotateSculpture (rotation)
TwoHandScaleSculpture (scaling)
MorphicAIClient (backend)
MorphicAIActionRouter (action dispatch)
BasicSculptController (keyboard testing)
```

---

## Configuration

**Detection:** fistMaxExtension 1.08, openMinExtension 1.18, pinchMaxDistance 0.10  
**Painting:** brushSize 0.08f, paintStrength 1f  
**Controls:** scaleSpeed 1f, rotationSpeed 80f/s, scale range 0.5x-4x

---

## AI Integration

```
User Action → JSON → HTTP POST /api/feedback
    ↓
Backend (Node.js/Express)
    ↓
OpenAI API
    ↓
Response → MorphicAIActionRouter → Event Callback → UI
```

Backend: https://morphic-openai-backend.onrender.com  
Mode: Live (OpenAI) or Test (toggle in settings)

---

## Performance

- Hand tracking: ~16-33ms
- Gesture detection: O(1)
- Shape key morphing: Fast (GPU)
- Vertex painting: O(n) per frame
- AI requests: Async (non-blocking)
- Target: 60 FPS

---

## Design Patterns

- **State Machine** → GestureModeManager
- **Observer** → OnFeedbackMessageReceived
- **Component-Based** → MonoBehaviour composition
- **Async** → AI requests non-blocking

---

**Generated:** October 4, 2026 | Repository: https://github.com/Anaghaj1411/Morphic.git
