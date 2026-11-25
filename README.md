# PetAR

PetAR is an Android augmented-reality dog-care simulator made in Unity for AVR404. Pick and name one of five dogs, place it on a detected tabletop or floor or scan a matching caregiver card, then feed, play with, or rest your dog. The care interface shows hunger, happiness, and energy and saves progress on the device.

## Open the Unity project

1. Clone or download this repository. Open its root folder through Unity Hub with **Unity 2022.3.62f3** and install **Android Build Support**, including the SDK, NDK, and OpenJDK.
2. Open **Assets → Scenes → PetAR**. The menu and care preview run in the Unity Editor.
3. For the five animated 3D dogs, acquire [Bublisher’s 3D Stylized Animated Dogs Kit](https://assetstore.unity.com/packages/3d/characters/animals/mammals/3d-stylized-animated-dogs-kit-284699) in your Unity account. In **Window → Package Manager → My Assets**, download and import the kit. Keep its default import paths and metadata so the five prefab references resolve in the scene. The vendor models, textures, and animations are supplied through the Asset Store under [Unity’s Asset Store terms](https://unity.com/legal/as-terms).

## Try live AR

Switch the build platform to **Android** and build **PetAR**. Use a 64-bit [ARCore-supported Android device](https://developers.google.com/ar/devices/) with Google Play Services for AR. Camera access is required for live placement.

1. Choose a dog, enter its name, and open the AR camera.
2. Aim at a tabletop or floor until the mint placement ring appears, then tap the ring. The dog appears facing you and greets you with a tail wag. For image tracking, print the matching PNG from [Assets/PetCare/Tracking](Assets/PetCare/Tracking) at **10 cm × 10 cm**, scan the card, and tap **Start AR care**.
3. Tap **Feed** to place the bowl and start the eating animation. Hunger updates when the meal finishes. **Play** and **Rest** play their own reactions and update the care meters.
