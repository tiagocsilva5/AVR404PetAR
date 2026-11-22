# PetAR

PetAR is an Android augmented-reality dog-care simulator made in Unity for AVR404. Pick and name one of five dogs, place it on a detected tabletop or floor or scan a matching caregiver card, then feed, play with, or rest your dog. The care interface shows hunger, happiness, and energy and saves progress on the device.

## Open the Unity project

1. Clone or download this repository. Open its root folder through Unity Hub with **Unity 2022.3.62f3** and install **Android Build Support**, including the SDK, NDK, and OpenJDK.
2. Open **Assets → Scenes → PetAR**. The menu and care preview run in the Unity Editor.
3. For the five animated 3D dogs, acquire [Bublisher’s 3D Stylized Animated Dogs Kit](https://assetstore.unity.com/packages/3d/characters/animals/mammals/3d-stylized-animated-dogs-kit-284699) in your Unity account. In **Window → Package Manager → My Assets**, download and import the kit. Keep its default import paths and metadata so the five prefab references resolve in the scene. The vendor models, textures, and animations are supplied through the Asset Store under [Unity’s Asset Store terms](https://unity.com/legal/as-terms).

## Try live AR

Switch the build platform to **Android** and build **PetAR**. Use a 64-bit [ARCore-supported Android device](https://developers.google.com/ar/devices/) with Google Play Services for AR. Camera access is required for live placement.

1. Choose a dog, enter its name, and open the AR camera.
2. Move the phone slowly until the interface prompts you to tap a surface, then tap the tabletop or floor. For image tracking, print the five PNGs in [Assets/PetCare/Tracking](Assets/PetCare/Tracking) at **10 cm × 10 cm** each and scan the card assigned to your dog.
3. Tap **Start AR care**. Use **Feed**, **Play**, and **Rest** to test the corresponding reactions and meter changes.

The public Unity project includes the PetAR scene, game code, user interface, and caregiver cards. Surface readiness appears in the scan panel.

