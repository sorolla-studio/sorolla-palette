# Known Issues

Field incidents seen by studios integrating the SDK, with verified causes and fixes. One entry per issue, newest first. If you hit something not listed here, check [Troubleshooting](troubleshooting.md) first, then report it so it gets logged.

Entry format: symptom, root cause, fix, prevention. Each entry records the date first seen and the game/setup it was seen on.

---

## TestFlight upload rejected: "Invalid bundle structure … libFirebaseCpp*.a binary file is not permitted" (ITMS-90171)

**First seen**: 2026-07-08, Rolling Wheel (Unity 6000.4.4, Firebase packages 13.7.0).

**Symptom**: App Store Connect rejects the upload with code `90171` for `YourGame.app/Frameworks/libFirebaseCppApp.a`, `libFirebaseCppCrashlytics.a`, and/or `libFirebaseCppRemoteConfig.a`:

> Invalid bundle structure. The "….app/Frameworks/libFirebaseCppApp.a" binary file is not permitted. Your app cannot contain standalone executables or libraries…

**Root cause**: Those `.a` files are **static libraries**: they are compiled into the executable at link time and must never be copied into the app bundle. A correct Unity iOS export only links them (Frameworks build phase) and does not embed them. The rejection means they ended up in Xcode's **Embed Frameworks** copy phase, which happens when someone sets them to "Embed & Sign" in Xcode, or when building with **Append** onto a previously modified Xcode project. Not an SDK or Firebase-package issue: the same packages upload fine when not embedded.

**Fix** (no Unity rebuild needed):
1. Open the Xcode project, select the **Unity-iPhone** target → **General** → **Frameworks, Libraries, and Embedded Content**.
2. Set each `libFirebaseCpp*.a` entry to **Do Not Embed** (equivalent: Build Phases → Embed Frameworks → delete those entries).
3. Re-archive and upload. Runtime behavior is unchanged: the code was already linked into the binary.

**Prevention**: If it reappears on the next Unity build, you are building with **Append** onto a modified project. Do a clean **Replace** build into a fresh folder, and never set static `.a` libraries to Embed & Sign.
