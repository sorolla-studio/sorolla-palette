# Sorolla Palette - Fast Testing Workflow Guide

## The Problem
When developing a package, switching Git branches doesn't trigger Unity's `[InitializeOnLoad]` scripts, meaning the package setup doesn't run automatically. You'd normally have to close and reopen Unity to test changes.

## The Solution
We've added testing tools accessible via the Unity menu: **Tools > Sorolla Palette > Testing**

---

## 🚀 Fast Testing Workflow

### Method 1: Using Menu Commands (Fastest)
1. Make changes to package code on your `master` branch
2. Switch to empty branch: `git checkout empty-branch` (or switch via IDE)
3. In Unity menu: **Tools > Sorolla Palette > Testing > Full Reset & Rerun**
   - This clears manifest changes, resets state, and runs setup
4. Switch back to master: `git checkout master`
5. In Unity menu: **Tools > Sorolla Palette > Run Setup (Force)**
   - This reruns the setup with your latest changes

**No Unity restart needed!** ⚡

### Method 2: Manual Steps
If you prefer more control:

1. **Clear the manifest** (removes Sorolla dependencies):
   - Menu: **Tools > Sorolla Palette > Testing > Clear Manifest Changes**
   - Confirms before removing registries and dependencies

2. **Reset package state**:
   - Menu: **Tools > Sorolla Palette > Testing > Reset Package State**
   - Clears the SessionState flag

3. **Force setup to run again**:
   - Menu: **Tools > Sorolla Palette > Run Setup (Force)**
   - Runs setup even if it already ran this session

---

## 🛠 Available Testing Tools

### Main Menu: `Tools/Sorolla Palette/`

#### **Run Setup (Force)**
- Forces the package setup to run immediately
- Bypasses the "already ran" check
- Use this to test setup script changes

### Testing Submenu: `Tools/Sorolla Palette/Testing/`

#### **Reset Package State**
- Clears the SessionState flag that tracks if setup has run
- Next domain reload will trigger setup automatically
- Useful for testing the automatic setup flow

#### **Clear Manifest Changes**
- Removes all Sorolla-added entries from manifest.json:
  - OpenUPM registry
  - Google registry  
  - GameAnalytics SDK dependency
  - External Dependency Manager dependency
- Shows confirmation dialog before clearing
- Automatically refreshes Package Manager

#### **Full Reset & Rerun**
- Combines all steps in one command:
  1. Clears manifest changes
  2. Resets package state
  3. Forces setup to run again
- **Best for testing the complete flow**

#### **Show Session State**
- Displays current SessionState values
- Shows if setup has run this session
- Useful for debugging state issues

#### **Open Manifest**
- Opens Finder/Explorer at manifest.json location
- Quick access to verify changes

---

## 💡 Typical Development Workflow

### Testing Initial Package Import
Simulates a fresh user installing the package:

```bash
# 1. Switch to empty branch
git checkout empty-branch

# 2. In Unity: Tools > Sorolla Palette > Testing > Full Reset & Rerun
# This simulates a clean Unity project

# 3. Switch back to your dev branch
git checkout master

# 4. In Unity: Tools > Sorolla Palette > Run Setup (Force)
# Setup runs with your latest code changes

# 5. Check Console for setup logs
# Verify manifest.json was modified correctly
```

### Testing Setup Script Changes
When you modify `SorollaPaletteSetup.cs`:

```bash
# 1. Make your changes to the setup script

# 2. In Unity: Tools > Sorolla Palette > Testing > Reset Package State

# 3. In Unity: Tools > Sorolla Palette > Run Setup (Force)

# 4. Verify logs and manifest.json
```

### Testing from Scratch
Completely reset to test like a new user:

```bash
# 1. In Unity: Tools > Sorolla Palette > Testing > Clear Manifest Changes

# 2. Close Unity

# 3. Delete Library/ScriptAssemblies/ folder

# 4. Reopen Unity
# Setup will run automatically on first load
```

---

## 🔍 Understanding SessionState

The package uses `SessionState` to track if setup has run:
- **Persists during domain reloads** (script recompiles)
- **Cleared when Unity closes**
- **Cleared by "Reset Package State" tool**

This ensures setup runs:
- ✅ First time package is imported
- ✅ After Unity restart
- ❌ NOT on every script recompile (would be too slow)

---

## 📝 Verifying Setup Success

### Check Console Logs
Look for these messages:
```
[Sorolla Palette] Running initial setup...
[Sorolla Palette] Added OpenUPM registry to manifest.json
[Sorolla Palette] Added Google registry to manifest.json
[Sorolla Palette] Added GameAnalytics SDK dependency
[Sorolla Palette] Added External Dependency Manager dependency
[Sorolla Palette] Setup complete. Package Manager will resolve dependencies.
```

### Check manifest.json
Open with: **Tools > Sorolla Palette > Testing > Open Manifest**

Should contain:
```json
{
  "scopedRegistries": [
    {
      "name": "Game Package Registry by Google",
      "url": "https://unityregistry-pa.googleapis.com/",
      "scopes": ["com.google"]
    },
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.gameanalytics",
        "com.google.external-dependency-manager"
      ]
    }
  ],
  "dependencies": {
    "com.gameanalytics.sdk": "7.10.6",
    "com.google.external-dependency-manager": "https://github.com/googlesamples/unity-jar-resolver.git?path=upm"
  }
}
```

### Check Package Manager
Window > Package Manager:
- Should see GameAnalytics SDK (7.10.6)
- Should see External Dependency Manager

---

## 🐛 Troubleshooting

### Setup doesn't run after switching branches
- Use: **Tools > Sorolla Palette > Run Setup (Force)**
- SessionState persists across branch switches

### Manifest changes don't appear
- Check Console for errors in setup script
- Manually verify manifest.json has write permissions
- Try: **Tools > Sorolla Palette > Testing > Full Reset & Rerun**

### Package Manager doesn't resolve dependencies
- After manifest changes, Unity should auto-resolve
- If stuck, try: **Window > Package Manager > Refresh button**
- Check for errors in Package Manager UI

### Changes not taking effect
- Remember to save all files before testing
- Check if Assembly Definition files need reimport
- Try: **Assets > Reimport All**

---

## ⚡ Pro Tips

1. **Keep Console Open**: Setup logs tell you exactly what happened
2. **Use Full Reset**: When in doubt, use "Full Reset & Rerun"
3. **Check manifest.json**: Visual confirmation setup worked
4. **Test on Fresh Project**: Periodically test in a completely new Unity project
5. **Git Branches**: Keep an "empty" branch for quick reset testing

---

## 🎯 What to Test

Before publishing package changes:

- [ ] Fresh install (empty branch → master)
- [ ] Setup runs automatically on first import
- [ ] All registries added to manifest
- [ ] All dependencies added to manifest
- [ ] Package Manager resolves successfully
- [ ] GameAnalytics SDK appears in Packages
- [ ] External Dependency Manager appears in Packages
- [ ] No errors in Console
- [ ] No errors in Package Manager
- [ ] Assembly definitions compile correctly
- [ ] SorollaPalette API is accessible in user code

---

**Remember**: These testing tools are for development only. Users won't see them - they'll just add the package and everything works automatically! ✨
