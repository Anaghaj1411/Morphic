# Webcam Display Setup Guide

This guide explains how to set up the webcam display with border and hand skeleton detection for the Taiwan lantern sculpting mode.

## What Was Added

1. **New Script:** `Assets/Scripts/UI/WebcamDisplayController.cs`
   - Handles positioning the webcam display in the top-left corner
   - Adds a border around the webcam feed
   - Configurable size (default: 280x210)
   - Configurable position (default: top-left offset)

2. **Updated Script:** `Assets/Scripts/DialogueManager.cs`
   - Added two new serialized fields:
     - `webcamDisplayController` - Reference to the webcam display GameObject
     - `handAnnotationController` - Reference to the hand skeleton annotation controller
   - Updated `BeginSculpting("Taiwan")` to activate webcam display and hand skeleton

## Unity Editor Setup Instructions

### Step 1: Create the Webcam Display UI

1. Open the **MainMenu.unity** scene
2. In the Hierarchy, right-click → **UI** → **Panel**
3. Rename it to `WebcamDisplayPanel`
4. With the panel selected:
   - Set **Canvas Scaler** to match your UI setup (likely "Scale With Screen Size")
   - Set **Rect Transform**:
     - Anchor: Center
     - Pivot: (0.5, 0.5)
     - Position: (150, -120) for top-left positioning
     - Width: 286 (280 + 3px border on each side)
     - Height: 216 (210 + 3px border on each side)
5. Add the **WebcamDisplayController** script to this panel
6. Configure the script:
   - **Display Size**: (280, 210)
   - **Anchored Position**: (150, -120)
   - **Border Thickness**: 3
   - **Border Color**: Gray (0.3, 0.3, 0.3, 1)
   - **Background Color**: Dark semi-transparent (0.1, 0.1, 0.1, 0.8)

### Step 2: Add MediaPipe Screen Component

1. Right-click on `WebcamDisplayPanel` → **UI** → **Raw Image**
2. Rename it to `WebcamScreen`
3. With `WebcamScreen` selected:
   - Set **Rect Transform**:
     - Anchor: Center
     - Pivot: (0.5, 0.5)
     - Position: (0, 0)
     - Width: 280
     - Height: 210
4. Add the **Mediapipe.Unity.Screen** script to this Raw Image
5. In the WebcamDisplayController script on the parent panel:
   - Drag `WebcamScreen` to the **Webcam Screen** field
   - Drag `WebcamDisplayPanel` (itself) to the **Border Panel** field

### Step 3: Find or Create Hand Annotation Controller

The hand skeleton annotation controller is part of the MediaPipe Unity SDK. You need to find it in your scene or add it:

**Option A: If it already exists in the scene:**
1. Look for a GameObject with `HandLandmarkerResultAnnotationController` component
2. This is usually part of the hand tracking setup
3. Note its name for the next step

**Option B: If you need to add it:**
1. Create an empty GameObject named `HandAnnotationController`
2. This should be a child of your Canvas or appropriate UI parent
3. The annotation controller draws the hand skeleton overlay on top of the webcam feed
4. Make sure it's properly configured with the MediaPipe annotation system

### Step 4: Wire Up References in DialogueManager

1. Find the **DialogueManager** GameObject in your scene
2. Select it and look at the Inspector
3. In the **Header: Webcam Display** section:
   - Drag `WebcamDisplayPanel` to the **Webcam Display Controller** field
   - Drag your `HandAnnotationController` GameObject to the **Hand Annotation Controller** field

### Step 5: Ensure Hand Tracking is Properly Set Up

Make sure your existing hand tracking setup includes:

1. **SculptHandLandmarkerRunner** - Should be on `taiwanHandTracking` GameObject
2. **Annotation Controller Reference** - The SculptHandLandmarkerRunner needs a reference to the hand annotation controller

Check `Assets/Scripts/Tracking/SculptHandLandmarkerRunner.cs` line 123-124:
```csharp
[SerializeField]
private HandLandmarkerResultAnnotationController annotationController;
```

Make sure this is assigned in the Inspector for your hand tracking GameObject.

### Step 6: Test the Setup

1. Save the scene
2. Run the scene
3. Navigate through the dialogue:
   - "Explore the Pottery Studio"
   - Select "Taiwan"
   - Click "Start Sculpting"
4. You should see:
   - The lantern mesh appear
   - Webcam feed in the top-left corner with a border
   - Hand skeleton overlay when hands are detected
   - Gesture indicator showing current gesture mode

## Expected Behavior

When the user clicks "Start Sculpting" for Taiwan:
1. The lantern mesh becomes visible
2. Hand tracking starts (webcam activates)
3. Webcam display appears in top-left corner with a gray border
4. Hand skeleton is drawn over the webcam feed when hands are detected
5. Gesture controller and indicator activate
6. User can now sculpt the lantern using hand gestures

## Troubleshooting

**Webcam not showing:**
- Check that the MediaPipe Screen component is properly assigned
- Verify the webcam is working in other applications
- Check Unity console for MediaPipe initialization errors

**No hand skeleton:**
- Ensure the HandAnnotationController is active
- Verify the annotation controller reference in SculptHandLandmarkerRunner
- Check that hands are being detected (look for debug logs)

**Border not visible:**
- Check that the border panel has an Image component
- Verify the border color alpha is not 0
- Ensure the panel is active and positioned correctly

**Position wrong:**
- Adjust the Anchored Position in WebcamDisplayController
- Positive X moves right, negative Y moves down
- (150, -120) should place it in the top-left area

## Customization

You can adjust these parameters in the WebcamDisplayController Inspector:

- **Display Size**: Change (280, 210) to make it larger or smaller
- **Anchored Position**: Adjust to move the webcam to different screen positions
- **Border Thickness**: Make the border thicker or thinner
- **Border Color**: Change the border color to match your UI theme
- **Background Color**: Adjust the background behind the webcam feed
