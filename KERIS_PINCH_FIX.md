# Keris Sharpness Pinch Gesture Fix

## Problem
Right-hand pinch gesture for controlling Keris sharpness was not working properly.

## Root Cause
The pinch detection thresholds in KerisGestureController.cs were incorrectly calibrated:
- pinchThreshold = 1.45f (normalized by palm width) was too high
- This meant fingers needed to be 1.45x palm width apart to trigger - too far for a pinch!
- IndexExtensionAngle requirement of 135° was too strict

## Solution Applied
Modified Assets/Scripts/KerisGestureController.cs:

1. **Lowered pinchThreshold**: 1.45f → 0.75f
   - Now triggers when fingers are 0.75x palm width apart (closer = easier to pinch)
   
2. **Lowered pinchReleaseThreshold**: 1.95f → 1.05f
   - Reduces hysteresis, making gesture feel more responsive
   
3. **Relaxed IndexExtensionAngle**: 135° → 115°  
   - Allows more natural pinch hand poses

## Testing
1. Open Unity (changes auto-detected)
2. Enter Play mode
3. Go to Keris crafting scene
4. Make a pinch gesture with your RIGHT hand (thumb + index finger)
5. Lift your hand up/down while pinching
6. The Keris sharpness should now respond correctly

## Technical Details
- The gesture uses normalized pinch distance (PinchDistance / PalmWidth)
- Values < threshold = pinching detected
- Right hand (physicalRight=true) controls Sharpness blend shape
- Left hand (physicalRight=false) controls Curve blend shape
